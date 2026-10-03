using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using IceBot.Api;
using IceBot.Config;

namespace IceBot.Workflow
{
    internal sealed class ProductionReportData
    {
        public Guid CommandId { get; set; }
        public Guid SourceEventId { get; set; }
        public long SequenceNumber { get; set; }
        public DateTimeOffset EdgeCreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid SourceProductionJobId { get; set; }
        public Guid OrderItemId { get; set; }
        public int ProductionUnitNo { get; set; }
        public int ProductionUnitQuantity { get; set; } = 1;
        public Guid SourceConfigurationReleaseId { get; set; }
        public string ReleaseChecksum { get; set; } = string.Empty;
        public long? ActiveSetVersion { get; set; }
        public string? ActiveSetChecksum { get; set; }
        public bool PhysicalOutputMayHaveOccurred { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }

    internal static class ProductionReportOutbox
    {
        private static readonly object Gate = new object();
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public static void Enqueue(DurableOrderJob job, DurableProductionUnit unit, string status,
            bool physicalOutputMayHaveOccurred, string? errorCode, string? errorMessage)
        {
            if (status == "Completed" && unit.CompletionReport != null)
            {
                Store(unit.CompletionReport, AppConfig.GetReportOutboxDirectory());
                return;
            }
            Enqueue(job, unit, status, physicalOutputMayHaveOccurred, errorCode, errorMessage,
                AppConfig.GetReportOutboxDirectory(), SiteConfigStore.NextExecutionReportSequence());
        }

        internal static void Enqueue(DurableOrderJob job, DurableProductionUnit unit, string status,
            bool physicalOutputMayHaveOccurred, string? errorCode, string? errorMessage,
            string directory, long sequenceNumber)
        {
            lock (Gate)
            {
                Store(Create(job, unit, status, physicalOutputMayHaveOccurred, errorCode, errorMessage, sequenceNumber), directory);
            }
        }

        internal static ProductionReportData Create(DurableOrderJob job, DurableProductionUnit unit, string status,
            bool physicalOutputMayHaveOccurred, string? errorCode, string? errorMessage, long sequenceNumber)
        {
                return new ProductionReportData
                {
                    CommandId = job.CommandId,
                    SourceEventId = Guid.NewGuid(),
                    SequenceNumber = sequenceNumber,
                    EdgeCreatedAt = DateTimeOffset.UtcNow,
                    Status = status,
                    SourceProductionJobId = unit.SourceProductionJobId,
                    OrderItemId = unit.OrderItemId,
                    ProductionUnitNo = unit.ProductionUnitNo,
                    SourceConfigurationReleaseId = job.ConfigurationReleaseId,
                    ReleaseChecksum = job.ReleaseChecksum,
                    ActiveSetVersion = job.ActiveSetVersion,
                    ActiveSetChecksum = job.ActiveSetChecksum,
                    PhysicalOutputMayHaveOccurred = physicalOutputMayHaveOccurred,
                    ErrorCode = errorCode,
                    ErrorMessage = errorMessage
                };
        }

        internal static void Store(ProductionReportData report, string directory)
        {
            lock (Gate)
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, report.SequenceNumber.ToString("D20") + "-" + report.SourceEventId.ToString("N") + ".json");
                var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
                if (File.Exists(path)) return;
                try
                {
                    var bytes = new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(report, JsonOptions));
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    File.Move(temporary, path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        public static void Flush()
        {
            Flush(AppConfig.GetReportOutboxDirectory(), EdgeDeploymentApi.ReportProduction, AppConfig.GetLogsDirectory());
        }

        internal static void Flush(string directory, Action<ProductionReportData> sender, string? logsDirectory = null)
        {
            lock (Gate)
            {
                if (!Directory.Exists(directory)) return;
                foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(item => item, StringComparer.Ordinal))
                {
                    ProductionReportData report;
                    try
                    {
                        report = JsonSerializer.Deserialize<ProductionReportData>(File.ReadAllText(path), JsonOptions)
                            ?? throw new InvalidDataException("Production report outbox entry is empty.");
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
                        sender(report);
                        File.Delete(path);
                    }
                    catch (EdgeApiException ex) when (IsNonRetryableClientError(ex.StatusCode))
                    {
                        var detail = $"Backend tu choi report {Path.GetFileName(path)} (HTTP {(int)ex.StatusCode}: {ex.Message}). Cay {report.ProductionUnitNo} (OrderItem: {report.OrderItemId}). Huy bo file khoi outbox.";
                        Console.WriteLine("[REPORT-OUTBOX] " + detail);
                        LogIncident(detail, logsDirectory);
                        File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[REPORT-OUTBOX] Chua gui duoc report; se thu lai: " + ex.Message);
                        break;
                    }
                }
            }
        }

        private static void LogIncident(string detail, string? logsDirectory)
        {
            try
            {
                var directory = logsDirectory ?? AppConfig.GetLogsDirectory();
                RuntimeSession.RecordEvent(directory, "ReportRejected", detail);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[REPORT-OUTBOX] Khong the ghi log su co: " + ex.Message);
            }
        }

        private static bool IsNonRetryableClientError(HttpStatusCode statusCode)
        {
            int code = (int)statusCode;
            return code >= 400 && code < 500 && code != 408 && code != 429;
        }

        public static int GetPendingCount()
        {
            lock (Gate)
            {
                var directory = AppConfig.GetReportOutboxDirectory();
                return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json").Length : 0;
            }
        }

        private static void Quarantine(string path, Exception exception)
        {
            var directory = Path.Combine(AppConfig.GetReportOutboxDirectory(), "invalid");
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, Path.GetFileName(path));
            if (File.Exists(destination)) File.Delete(destination);
            File.Move(path, destination);
            File.WriteAllText(destination + ".error.txt", exception.Message, new UTF8Encoding(false));
            Console.WriteLine("[REPORT-OUTBOX] Da cach ly report khong hop le: " + Path.GetFileName(path));
        }
    }
}
