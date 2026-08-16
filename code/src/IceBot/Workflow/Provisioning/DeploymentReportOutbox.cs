using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IceBot.Api;
using IceBot.Config;

namespace IceBot.Workflow
{
    internal sealed class DeploymentReportData
    {
        public Guid CommandId { get; set; }
        public Guid SourceEventId { get; set; }
        public long SequenceNumber { get; set; }
        public DateTimeOffset EdgeCreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid DeploymentId { get; set; }
        public Guid ConfigurationReleaseId { get; set; }
        public string ReleaseChecksum { get; set; } = string.Empty;
    }

    internal static class DeploymentReportOutbox
    {
        private static readonly object Gate = new object();
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public static void Enqueue(Guid commandId, FullEdgeDeploymentPayload payload, string status)
        {
            lock (Gate)
            {
                var directory = Path.Combine(AppConfig.GetReportOutboxDirectory(), "deployments");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, commandId.ToString("D") + "-" + status + ".json");
                if (File.Exists(path)) return;

                var report = new DeploymentReportData
                {
                    CommandId = commandId,
                    SourceEventId = StableEventId(commandId, status),
                    SequenceNumber = SiteConfigStore.NextExecutionReportSequence(),
                    EdgeCreatedAt = DateTimeOffset.UtcNow,
                    Status = status,
                    DeploymentId = payload.DeploymentId,
                    ConfigurationReleaseId = payload.ConfigurationReleaseId,
                    ReleaseChecksum = payload.ReleaseChecksum
                };
                var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.WriteAllText(temporary, JsonSerializer.Serialize(report, JsonOptions), new UTF8Encoding(false));
                    File.Move(temporary, path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        public static void Flush()
        {
            lock (Gate)
            {
                var directory = Path.Combine(AppConfig.GetReportOutboxDirectory(), "deployments");
                if (!Directory.Exists(directory)) return;
                foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(item => item, StringComparer.Ordinal))
                {
                    DeploymentReportData report;
                    try
                    {
                        report = JsonSerializer.Deserialize<DeploymentReportData>(File.ReadAllText(path), JsonOptions)
                            ?? throw new InvalidDataException("Deployment report outbox entry is empty.");
                    }
                    catch (JsonException ex)
                    {
                        Quarantine(path, ex);
                        continue;
                    }
                    catch (InvalidDataException ex)
                    {
                        Quarantine(path, ex);
                        continue;
                    }

                    try
                    {
                        EdgeDeploymentApi.ReportDeployment(report);
                        File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[DEPLOYMENT-OUTBOX] Chua gui duoc report; se thu lai: " + ex.Message);
                        break;
                    }
                }
            }
        }

        private static void Quarantine(string path, Exception exception)
        {
            var directory = Path.Combine(Path.GetDirectoryName(path)!, "invalid");
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, Path.GetFileName(path));
            if (File.Exists(destination)) File.Delete(destination);
            File.Move(path, destination);
            File.WriteAllText(destination + ".error.txt", exception.Message, new UTF8Encoding(false));
            Console.WriteLine("[DEPLOYMENT-OUTBOX] Da cach ly report khong hop le: " + Path.GetFileName(path));
        }

        private static Guid StableEventId(Guid commandId, string status)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(commandId.ToString("D") + ":" + status));
                var bytes = new byte[16];
                Array.Copy(hash, bytes, bytes.Length);
                return new Guid(bytes);
            }
        }
    }
}
