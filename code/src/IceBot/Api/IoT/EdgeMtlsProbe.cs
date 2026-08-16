using System;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using IceBot.Config;
using IceBot.Robot;
using IceBot.Robot.Hardware;
using IceBot.Workflow;

namespace IceBot.Api
{
    internal static class EdgeMtlsProbe
    {
        public static bool SendHeartbeat(out string message)
        {
            var settings = SiteConfigStore.Load();
            if (settings.ExecutionEndpointId == Guid.Empty || settings.FullEdgeRuntimeId == Guid.Empty)
            {
                message = "Thieu Execution Endpoint ID hoac Full Edge Runtime ID.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(settings.ExecutionClientCertificatePath) ||
                !File.Exists(settings.ExecutionClientCertificatePath))
            {
                message = "Khong tim thay PFX client mTLS.";
                return false;
            }
            if (!Uri.TryCreate(settings.BeApiUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri) ||
                baseUri.Scheme != Uri.UriSchemeHttps)
            {
                message = "BE_API_URL phai la HTTPS URL hop le.";
                return false;
            }

            try
            {
                using (var certificate = new X509Certificate2(
                    settings.ExecutionClientCertificatePath,
                    AppConfig.ExecutionClientCertificatePassword,
                    X509KeyStorageFlags.DefaultKeySet))
                using (var handler = new HttpClientHandler())
                {
                    handler.ClientCertificates.Add(certificate);
                    using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) })
                    using (var content = new StringContent(JsonSerializer.Serialize(new
                    {
                        originNodeId = settings.FullEdgeRuntimeId,
                        heartbeatSequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        reportedAt = DateTimeOffset.UtcNow,
                        status = "Online",
                        robotStatus = "Setup",
                        networkStatus = "Online",
                        appVersion = typeof(EdgeMtlsProbe).Assembly.GetName().Version?.ToString(),
                        pendingSyncEventCount = 0
                    }), Encoding.UTF8, "application/json"))
                    using (var response = client.PostAsync(
                        new Uri(baseUri, $"api/v1/iot/execution-endpoints/{settings.ExecutionEndpointId:D}/heartbeat"),
                        content).GetAwaiter().GetResult())
                    {
                        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        if (!response.IsSuccessStatusCode)
                        {
                            message = $"BE tu choi heartbeat mTLS (HTTP {(int)response.StatusCode}): {body}";
                            return false;
                        }
                        message = "BE da xac thuc chung chi va nhan heartbeat mTLS.";
                        return true;
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is System.Threading.Tasks.TaskCanceledException || ex is System.Security.Cryptography.CryptographicException)
            {
                message = "Kiem tra mTLS that bai: " + DescribeTransportFailure(ex);
                return false;
            }
        }

        public static bool SendReportedDevices(out string message)
        {
            var settings = SiteConfigStore.Load();
            if (!TryCreateClient(settings, out var client, out var baseUri, out message))
                return false;

            using (client)
            {
                var devices = new ConfiguredRobotDeviceDiscovery().Discover(settings)
                    .OrderBy(item => item.SourceDeviceKey, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (devices.Length == 0)
                {
                    message = "Khong co robot device profile de report; kiem tra PRIMARY_ROBOT_* trong site config.";
                    return false;
                }

                var signature = string.Join("|", devices.Select(item =>
                    $"{item.SourceDeviceKey}:{item.DeviceId:D}:{item.RuntimeTargetCode}:{item.MachineModelCode}"));
                var snapshotRevision = SiteConfigStore.GetReportedDevicesSnapshotRevision(signature);

                try
                {
                    using (var content = new StringContent(JsonSerializer.Serialize(new
                    {
                        sourceExecutorId = settings.FullEdgeRuntimeId,
                        snapshotRevision,
                        observedAt = DateTimeOffset.UtcNow,
                        devices = devices.Select(item => new
                        {
                            sourceDeviceKey = item.SourceDeviceKey,
                            deviceId = item.DeviceId,
                            runtimeTargetCode = item.RuntimeTargetCode,
                            machineModelCode = item.MachineModelCode
                        }).ToArray()
                    }), Encoding.UTF8, "application/json"))
                    using (var response = client.PutAsync(
                        new Uri(baseUri, $"api/v1/iot/execution-endpoints/{settings.ExecutionEndpointId:D}/reported-devices"),
                        content).GetAwaiter().GetResult())
                    {
                        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        if (!response.IsSuccessStatusCode)
                        {
                            message = $"BE tu choi reported devices mTLS (HTTP {(int)response.StatusCode}): {body}";
                            return false;
                        }

                        message = $"BE da nhan hardware snapshot revision {snapshotRevision} ({devices.Length} device).";
                        return true;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is System.Threading.Tasks.TaskCanceledException || ex is System.Security.Cryptography.CryptographicException)
                {
                    message = "Report hardware profile that bai: " + DescribeTransportFailure(ex);
                    return false;
                }
            }
        }

        public static bool SendHeartbeatAndReportedDevices(out string message)
        {
            if (!SendHeartbeat(out var heartbeatMessage))
            {
                message = heartbeatMessage;
                return false;
            }

            var devicesReported = SendReportedDevices(out var devicesMessage);
            message = heartbeatMessage + " " + devicesMessage;
            return devicesReported;
        }

        public static bool SendReadiness(out string message)
        {
            var settings = SiteConfigStore.Load();
            if (!TryCreateClient(settings, out var client, out var baseUri, out message))
                return false;

            using (client)
            {
                try
                {
                    const long minimumFreeSpaceBytes = 256L * 1024 * 1024;
                    var localStateDirectory = AppConfig.GetOrderJobsDirectory();
                    var storageWritable = CanWriteLocalState(localStateDirectory);
                    var root = Path.GetPathRoot(Path.GetFullPath(localStateDirectory));
                    var freeSpaceBytes = string.IsNullOrWhiteSpace(root) ? 0 : new DriveInfo(root).AvailableFreeSpace;
                    var pendingReports = ProductionReportOutbox.GetPendingCount();
                    var hasActiveWork = EdgeOrderExecutionQueue.HasActiveOrUnresolvedWork(localStateDirectory);
                    var readiness = storageWritable && freeSpaceBytes >= minimumFreeSpaceBytes ? "Ready" : "NotReady";
                    var revision = SiteConfigStore.NextExecutionReadinessRevision();

                    using (var content = new StringContent(JsonSerializer.Serialize(new
                    {
                        sourceExecutorId = settings.FullEdgeRuntimeId,
                        stateRevision = revision,
                        executorReportedAt = DateTimeOffset.UtcNow,
                        readiness,
                        activity = hasActiveWork ? "Busy" : "Idle",
                        // Simulation is an explicit local test mode. A physical Fairino runtime
                        // must continue to report Unknown until it has trustworthy safety input.
                        safety = AppConfig.RobotExecutionMode == RobotExecutionMode.Simulated ? "Safe" : "Unknown",
                        physicalOutputState = "Unknown",
                        localPersistenceHealth = new
                        {
                            storageWritable,
                            freeSpaceBytes,
                            minimumRequiredFreeSpaceBytes = minimumFreeSpaceBytes,
                            localDatabaseHealth = storageWritable ? "Healthy" : "Unhealthy",
                            pendingEventCount = pendingReports,
                            maximumPendingEventCount = 1000
                        },
                        capabilities = Array.Empty<object>()
                    }), Encoding.UTF8, "application/json"))
                    using (var response = client.PostAsync(
                        new Uri(baseUri, $"api/v1/iot/execution-endpoints/{settings.ExecutionEndpointId:D}/readiness"),
                        content).GetAwaiter().GetResult())
                    {
                        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        if (!response.IsSuccessStatusCode)
                        {
                            message = $"BE tu choi readiness mTLS (HTTP {(int)response.StatusCode}): {body}";
                            return false;
                        }

                        message = $"BE da nhan readiness revision {revision}: {readiness}, {(hasActiveWork ? "Busy" : "Idle")}.";
                        return true;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is System.Threading.Tasks.TaskCanceledException || ex is System.Security.Cryptography.CryptographicException || ex is IOException)
                {
                    message = "Report readiness that bai: " + DescribeTransportFailure(ex);
                    return false;
                }
            }
        }

        public static bool SendSimulatedInventoryObservations(out string message)
        {
            if (AppConfig.RobotExecutionMode != RobotExecutionMode.Simulated)
            {
                message = "Simulated inventory uplink is disabled outside Simulated mode.";
                return true;
            }

            var settings = SiteConfigStore.Load();
            if (settings.SimulatedInventoryObservations.Count == 0)
            {
                message = "No simulated sensor-gateway observations are configured.";
                return true;
            }
            if (!TryCreateClient(settings, out var client, out var baseUri, out message))
                return false;

            using (client)
            {
                try
                {
                    var now = DateTimeOffset.UtcNow;
                    using (var content = new StringContent(JsonSerializer.Serialize(new
                    {
                        sourceExecutorId = settings.FullEdgeRuntimeId,
                        observations = settings.SimulatedInventoryObservations.Select(item => new
                        {
                            sourceEventId = Guid.NewGuid(),
                            ingredientDispenserStateId = item.IngredientDispenserStateId,
                            deviceId = item.DeviceId,
                            observationSequence = now.ToUnixTimeMilliseconds(),
                            observedLevelStatus = item.Level,
                            observedAt = now
                        }).ToArray()
                    }), Encoding.UTF8, "application/json"))
                    using (var response = client.PostAsync(new Uri(baseUri,
                        $"api/v1/iot/execution-endpoints/{settings.ExecutionEndpointId:D}/simulated-inventory-observations"), content).GetAwaiter().GetResult())
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            message = $"Simulated sensor-gateway report rejected (HTTP {(int)response.StatusCode}).";
                            return false;
                        }
                        message = $"Reported {settings.SimulatedInventoryObservations.Count} simulated sensor-gateway observations.";
                        return true;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is System.Threading.Tasks.TaskCanceledException || ex is System.Security.Cryptography.CryptographicException)
                {
                    message = "Simulated sensor-gateway report failed: " + DescribeTransportFailure(ex);
                    return false;
                }
            }
        }

        private static bool CanWriteLocalState(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, ".write-probe-" + Guid.NewGuid().ToString("N"));
                using (File.Create(path)) { }
                File.Delete(path);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static bool TryCreateClient(SiteSettings settings, out HttpClient client, out Uri baseUri, out string message)
        {
            client = null!;
            baseUri = null!;
            message = string.Empty;
            if (settings.ExecutionEndpointId == Guid.Empty || settings.FullEdgeRuntimeId == Guid.Empty)
            {
                message = "Thieu Execution Endpoint ID hoac Full Edge Runtime ID.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(settings.ExecutionClientCertificatePath) ||
                !File.Exists(settings.ExecutionClientCertificatePath))
            {
                message = "Khong tim thay PFX client mTLS.";
                return false;
            }
            if (!Uri.TryCreate(settings.BeApiUrl.TrimEnd('/') + "/", UriKind.Absolute, out baseUri) ||
                baseUri.Scheme != Uri.UriSchemeHttps)
            {
                message = "BE_API_URL phai la HTTPS URL hop le.";
                return false;
            }

            try
            {
                var certificate = new X509Certificate2(
                    settings.ExecutionClientCertificatePath,
                    AppConfig.ExecutionClientCertificatePassword,
                    X509KeyStorageFlags.DefaultKeySet);
                var handler = new HttpClientHandler();
                handler.ClientCertificates.Add(certificate);
                client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
                return true;
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                message = "Khong tai duoc PFX client mTLS: " + ex.Message;
                return false;
            }
        }

        private static string DescribeTransportFailure(Exception exception)
        {
            var details = exception.Message;
            for (var inner = exception.InnerException; inner != null; inner = inner.InnerException)
            {
                details += " -> " + inner.Message;
            }

            return details;
        }
    }
}
