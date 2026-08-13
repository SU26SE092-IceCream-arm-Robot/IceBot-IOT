using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using IceBot.Config;
using IceBot.Workflow;

namespace IceBot.Api
{
    internal sealed class EdgeCommandPullData
    {
        public DateTimeOffset ServerTime { get; set; }
        public List<EdgeCommandData> Commands { get; set; } = new List<EdgeCommandData>();
    }

    internal sealed class EdgeCommandData
    {
        public Guid CommandId { get; set; }
        public string CommandType { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
    }

    internal sealed class FullEdgeDeploymentPayload
    {
        public Guid DeploymentId { get; set; }
        public Guid ConfigurationReleaseId { get; set; }
        public string ReleaseChecksum { get; set; } = string.Empty;
        public FullEdgeBundleData? FullEdgeBundle { get; set; }
        public List<DeploymentArtifactData> Artifacts { get; set; } = new List<DeploymentArtifactData>();
    }

    internal sealed class FullEdgeBundleData
    {
        public int FormatVersion { get; set; }
        public string Checksum { get; set; } = string.Empty;
        public long ContentLengthBytes { get; set; }
        public int ArtifactCount { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public DateTimeOffset DownloadUrlExpiresAt { get; set; }
    }

    internal sealed class DeploymentArtifactData
    {
        public Guid RobotArtifactId { get; set; }
        public string ArtifactChecksum { get; set; } = string.Empty;
        public long ContentLengthBytes { get; set; }
    }

    internal static class EdgeDeploymentApi
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static EdgeCommandPullData PullCommands(int maxCommands = 10)
        {
            return Send<EdgeCommandPullData>(
                "commands/pull",
                new { maxCommands, edgeTime = DateTimeOffset.UtcNow });
        }

        public static void AcknowledgeAccepted(Guid commandId)
        {
            Acknowledge(commandId, "Accepted", true);
        }

        public static void AcknowledgeReceived(Guid commandId)
        {
            Acknowledge(commandId, "Received", false);
        }

        public static void AcknowledgeExecutorBusy(Guid commandId)
        {
            Acknowledge(commandId, "ExecutorBusy", false, "QueueCapacity", "Edge queue is at its 10-unit capacity.");
        }

        public static void AcknowledgeRejected(Guid commandId, string code, string message)
        {
            Acknowledge(commandId, "Rejected", false, code, message);
        }

        public static void ReportDeployment(
            Guid commandId,
            FullEdgeDeploymentPayload payload,
            string status,
            long sequenceNumber)
        {
            var now = DateTimeOffset.UtcNow;
            Send<object>(
                $"commands/{commandId:D}/reports",
                new
                {
                    sourceEventId = Guid.NewGuid(),
                    sequenceNumber,
                    edgeCreatedAt = now,
                    executorReportedAt = now,
                    reportType = "Deployment",
                    status,
                    deploymentId = payload.DeploymentId,
                    sourceConfigurationReleaseId = payload.ConfigurationReleaseId,
                    releaseChecksum = payload.ReleaseChecksum,
                    physicalOutputMayHaveOccurred = false,
                    stockMovements = Array.Empty<object>()
                });
        }

        public static void ReportProduction(ProductionReportData report)
        {
            Send<object>(
                $"commands/{report.CommandId:D}/reports",
                new
                {
                    sourceEventId = report.SourceEventId,
                    sequenceNumber = report.SequenceNumber,
                    edgeCreatedAt = report.EdgeCreatedAt,
                    executorReportedAt = report.EdgeCreatedAt,
                    reportType = "ProductionExecution",
                    status = report.Status,
                    sourceProductionJobId = report.SourceProductionJobId,
                    orderItemId = report.OrderItemId,
                    productionUnitNo = report.ProductionUnitNo,
                    productionUnitQuantity = report.ProductionUnitQuantity,
                    activeSetVersion = report.ActiveSetVersion,
                    activeSetChecksum = report.ActiveSetChecksum,
                    sourceConfigurationReleaseId = report.SourceConfigurationReleaseId,
                    releaseChecksum = report.ReleaseChecksum,
                    physicalOutputMayHaveOccurred = report.PhysicalOutputMayHaveOccurred,
                    errorCode = report.ErrorCode,
                    errorMessage = report.ErrorMessage,
                    stockMovements = Array.Empty<object>()
                });
        }

        public static byte[] DownloadBundle(FullEdgeBundleData bundle)
        {
            if (string.IsNullOrWhiteSpace(bundle.DownloadUrl))
                throw new InvalidOperationException("BE command does not contain a bundle download URL.");
            if (bundle.DownloadUrlExpiresAt <= DateTimeOffset.UtcNow)
                throw new InvalidOperationException("BE bundle download URL has expired; pull the command again.");
            if (!Uri.TryCreate(bundle.DownloadUrl, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("BE bundle download URL is invalid.");

            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) })
            using (var response = client.GetAsync(uri).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (bytes.LongLength != bundle.ContentLengthBytes)
                    throw new InvalidDataException("Downloaded bundle size does not match the BE descriptor.");
                return bytes;
            }
        }

        public static FullEdgeDeploymentPayload ParseFullEdgeDeployment(EdgeCommandData command)
        {
            if (!string.Equals(command.CommandType, "DeployConfiguration", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Command is not a DeployConfiguration command.");
            var payload = JsonSerializer.Deserialize<FullEdgeDeploymentPayload>(command.PayloadJson, JsonOptions);
            if (payload == null || payload.DeploymentId == Guid.Empty || payload.ConfigurationReleaseId == Guid.Empty ||
                payload.FullEdgeBundle == null)
                throw new InvalidDataException("DeployConfiguration payload is incomplete or invalid.");
            return payload;
        }

        private static T Send<T>(string relativePath, object body)
        {
            ValidateConfiguration(out var baseUri, out var endpointId, out var certificatePath);
            var certificate = new X509Certificate2(
                certificatePath,
                AppConfig.ExecutionClientCertificatePassword,
                X509KeyStorageFlags.DefaultKeySet);
            if (!certificate.HasPrivateKey)
                throw new InvalidOperationException("Execution client certificate does not contain a private key.");

            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(certificate);
            using (handler)
            using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) })
            using (var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"))
            using (var response = client.PostAsync(
                new Uri(baseUri, $"api/v1/iot/execution-endpoints/{endpointId:D}/{relativePath}"),
                content).GetAwaiter().GetResult())
            {
                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                ApiEnvelope<T>? envelope = null;
                if (!string.IsNullOrWhiteSpace(json))
                    envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(json, JsonOptions);
                if (!response.IsSuccessStatusCode || envelope == null || !envelope.Succeeded)
                    throw new InvalidOperationException(envelope?.Message ?? $"BE request failed (HTTP {(int)response.StatusCode}).");
                return envelope.Data!;
            }
        }

        private static void Acknowledge(
            Guid commandId,
            string status,
            bool localStatePersisted,
            string? rejectionCode = null,
            string? rejectionMessage = null)
        {
            Send<object>(
                $"commands/{commandId:D}/ack",
                new
                {
                    ackStatus = status,
                    acknowledgedAt = DateTimeOffset.UtcNow,
                    rejectionCode,
                    rejectionMessage,
                    physicalOutputMayHaveOccurred = false,
                    localStatePersisted
                });
        }

        private static void ValidateConfiguration(out Uri baseUri, out Guid endpointId, out string certificatePath)
        {
            endpointId = AppConfig.ExecutionEndpointId;
            certificatePath = AppConfig.ExecutionClientCertificatePath;
            var privateBeUrl = AppConfig.BeApiUrl;
            if (string.IsNullOrWhiteSpace(privateBeUrl))
                throw new InvalidOperationException("Missing BE private URL. Set BE_API_URL/ICEBOT_BE_API_URL after the NetBird address is known.");
            if (!Uri.TryCreate(privateBeUrl.TrimEnd('/') + "/", UriKind.Absolute, out baseUri!) ||
                baseUri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("BE private URL must be an absolute HTTPS URL reachable through NetBird.");
            if (endpointId == Guid.Empty)
                throw new InvalidOperationException("Execution endpoint ID is not configured.");
            if (string.IsNullOrWhiteSpace(certificatePath) || !File.Exists(certificatePath))
                throw new InvalidOperationException("Execution client certificate PFX path is missing or does not exist.");
        }
    }

    internal sealed class ApiEnvelope<T>
    {
        public bool Succeeded { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
}
