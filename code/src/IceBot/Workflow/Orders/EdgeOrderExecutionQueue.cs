using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using IceBot.Api;
using IceBot.Config;

namespace IceBot.Workflow
{
    internal enum OrderAdmissionResult { Accepted, AlreadyStored, Busy }

    internal sealed class DurableOrderJob
    {
        public Guid CommandId { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public Guid ConfigurationReleaseId { get; set; }
        public string ReleaseChecksum { get; set; } = string.Empty;
        public long? ActiveSetVersion { get; set; }
        public string? ActiveSetChecksum { get; set; }
        public DateTimeOffset AcceptedAt { get; set; }
        public string Status { get; set; } = "AwaitingAck";
        public List<DurableProductionUnit> Units { get; set; } = new List<DurableProductionUnit>();
    }

    internal sealed class DurableProductionUnit
    {
        public Guid SourceProductionJobId { get; set; }
        public Guid OrderItemId { get; set; }
        public int ProductionUnitNo { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public List<ReceivedArtifact> Artifacts { get; set; } = new List<ReceivedArtifact>();
    }

    internal static class EdgeOrderExecutionQueue
    {
        private static readonly object Gate = new object();
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public static bool Contains(Guid commandId, string jobsDirectory)
        {
            lock (Gate) return File.Exists(JobPath(jobsDirectory, commandId));
        }

        public static OrderAdmissionResult TryAdmit(ReceivedOrderCommand order, string jobsDirectory)
        {
            lock (Gate)
            {
                Directory.CreateDirectory(jobsDirectory);
                if (File.Exists(JobPath(jobsDirectory, order.CommandId))) return OrderAdmissionResult.AlreadyStored;
                var occupied = LoadAll(jobsDirectory).SelectMany(job => job.Units)
                    .Count(unit => unit.Status == "Pending" || unit.Status == "Running");
                if (occupied + order.TotalQuantity > AppConfig.MaxPendingProductionUnits) return OrderAdmissionResult.Busy;

                var job = new DurableOrderJob
                {
                    CommandId = order.CommandId,
                    OrderId = order.OrderId,
                    OrderNumber = order.OrderNumber,
                    ConfigurationReleaseId = order.ConfigurationReleaseId,
                    ReleaseChecksum = order.ReleaseChecksum,
                    ActiveSetVersion = order.ActiveSetVersion,
                    ActiveSetChecksum = order.ActiveSetChecksum,
                    AcceptedAt = DateTimeOffset.UtcNow
                };
                foreach (var line in order.OrderLines)
                {
                    var artifacts = line.RobotPrograms.OrderBy(program => program.BindingOrder)
                        .SelectMany(program => program.Artifacts.OrderBy(artifact => artifact.RunOrder)).ToList();
                    for (var index = 0; index < line.Quantity; index++)
                    {
                        job.Units.Add(new DurableProductionUnit
                        {
                            SourceProductionJobId = Guid.NewGuid(),
                            OrderItemId = line.OrderItemId,
                            ProductionUnitNo = line.ProductionUnitStartNo + index,
                            Artifacts = artifacts
                        });
                    }
                }
                Save(job, jobsDirectory);
                return OrderAdmissionResult.Accepted;
            }
        }

        public static void Activate(Guid commandId, string jobsDirectory)
        {
            lock (Gate)
            {
                var job = Load(JobPath(jobsDirectory, commandId));
                if (job.Status != "AwaitingAck") return;
                job.Status = "Pending";
                Save(job, jobsDirectory);
                foreach (var unit in job.Units)
                    ProductionReportOutbox.Enqueue(job, unit, "Accepted", false, null, null);
            }
        }

        public static void RecoverInterruptedJobs(string jobsDirectory)
        {
            lock (Gate)
            {
                foreach (var job in LoadAll(jobsDirectory))
                {
                    var changed = false;
                    var recovered = new List<DurableProductionUnit>();
                    foreach (var unit in job.Units.Where(unit => unit.Status == "Running"))
                    {
                        unit.Status = "RequiresManualIntervention";
                        unit.ErrorCode = "RuntimeRestartedDuringExecution";
                        unit.ErrorMessage = "Edge restarted while this production unit was running; automatic retry is unsafe.";
                        recovered.Add(unit);
                        changed = true;
                    }
                    if (changed)
                    {
                        job.Status = "RequiresManualIntervention";
                        Save(job, jobsDirectory);
                        foreach (var unit in recovered)
                            ProductionReportOutbox.Enqueue(job, unit, unit.Status, true, unit.ErrorCode, unit.ErrorMessage);
                    }
                }
            }
        }

        public static DurableOrderJob? NextRunnable(string jobsDirectory)
        {
            lock (Gate)
            {
                var jobs = LoadAll(jobsDirectory);
                if (jobs.Any(job => job.Status == "Failed" || job.Status == "RequiresManualIntervention")) return null;
                return jobs.Where(job => job.Status == "Pending" && job.Units.Any(unit => unit.Status == "Pending"))
                    .OrderBy(job => job.AcceptedAt).FirstOrDefault();
            }
        }

        public static DurableProductionUnit BeginNextUnit(DurableOrderJob selected, string jobsDirectory)
        {
            lock (Gate)
            {
                var job = Load(JobPath(jobsDirectory, selected.CommandId));
                var unit = job.Units.First(candidate => candidate.Status == "Pending");
                unit.Status = "Running";
                unit.StartedAt = DateTimeOffset.UtcNow;
                job.Status = "Running";
                Save(job, jobsDirectory);
                ProductionReportOutbox.Enqueue(job, unit, "Running", false, null, null);
                return unit;
            }
        }

        public static void CompleteUnit(Guid commandId, Guid sourceJobId, string jobsDirectory)
        {
            lock (Gate)
            {
                var job = Load(JobPath(jobsDirectory, commandId));
                var unit = job.Units.Single(item => item.SourceProductionJobId == sourceJobId);
                unit.Status = "Completed";
                unit.CompletedAt = DateTimeOffset.UtcNow;
                job.Status = job.Units.All(item => item.Status == "Completed") ? "Completed" : "Pending";
                Save(job, jobsDirectory);
                ProductionReportOutbox.Enqueue(job, unit, "Completed", true, null, null);
            }
        }

        public static void FailUnit(Guid commandId, Guid sourceJobId, string jobsDirectory, Exception error)
        {
            lock (Gate)
            {
                var job = Load(JobPath(jobsDirectory, commandId));
                var unit = job.Units.Single(item => item.SourceProductionJobId == sourceJobId);
                unit.Status = "Failed";
                unit.CompletedAt = DateTimeOffset.UtcNow;
                unit.ErrorCode = "WorkflowExecutionFailed";
                unit.ErrorMessage = error.Message;
                job.Status = "Failed";
                Save(job, jobsDirectory);
                ProductionReportOutbox.Enqueue(job, unit, "Failed", true, unit.ErrorCode, unit.ErrorMessage);
            }
        }

        internal static IReadOnlyList<DurableOrderJob> LoadAll(string directory)
        {
            if (!Directory.Exists(directory)) return Array.Empty<DurableOrderJob>();
            return Directory.GetFiles(directory, "*.json").Select(Load).ToArray();
        }

        private static DurableOrderJob Load(string path) =>
            JsonSerializer.Deserialize<DurableOrderJob>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Stored order job is empty: " + path);

        private static void Save(DurableOrderJob job, string directory)
        {
            Directory.CreateDirectory(directory);
            var destination = JobPath(directory, job.CommandId);
            var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temporary, JsonSerializer.Serialize(job, JsonOptions), new UTF8Encoding(false));
            try { if (File.Exists(destination)) File.Replace(temporary, destination, null); else File.Move(temporary, destination); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static string JobPath(string directory, Guid commandId) => Path.Combine(directory, commandId.ToString("D") + ".json");
    }

    internal sealed class EdgeOrderExecutionWorker : IDisposable
    {
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private Thread? _thread;

        public void Start()
        {
            if (_thread != null) return;
            EdgeOrderExecutionQueue.RecoverInterruptedJobs(AppConfig.GetOrderJobsDirectory());
            _thread = new Thread(Loop) { IsBackground = true, Name = "IceBot-OrderExecutor" };
            _thread.Start();
        }

        private void Loop()
        {
            while (!_stop.WaitOne(0))
            {
                try
                {
                    ProductionReportOutbox.Flush();
                    var job = EdgeOrderExecutionQueue.NextRunnable(AppConfig.GetOrderJobsDirectory());
                    if (job == null) { _stop.WaitOne(TimeSpan.FromSeconds(2)); continue; }
                    var unit = EdgeOrderExecutionQueue.BeginNextUnit(job, AppConfig.GetOrderJobsDirectory());
                    Console.WriteLine($"[ORDER] Bat dau {job.OrderNumber}, cay {unit.ProductionUnitNo}.");
                    try
                    {
                        WorkflowRunner.RunQueue(unit.Artifacts.Select(item => item.ScriptFileName).ToArray(), AppConfig.RobotIp);
                        EdgeOrderExecutionQueue.CompleteUnit(job.CommandId, unit.SourceProductionJobId, AppConfig.GetOrderJobsDirectory());
                        Console.WriteLine($"[ORDER] Hoan tat {job.OrderNumber}, cay {unit.ProductionUnitNo}.");
                    }
                    catch (Exception ex)
                    {
                        EdgeOrderExecutionQueue.FailUnit(job.CommandId, unit.SourceProductionJobId, AppConfig.GetOrderJobsDirectory(), ex);
                        Console.WriteLine($"[ORDER] LOI {job.OrderNumber}, cay {unit.ProductionUnitNo}: {ex.Message}");
                    }
                    ProductionReportOutbox.Flush();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ORDER] Worker error: " + ex.Message);
                    _stop.WaitOne(TimeSpan.FromSeconds(5));
                }
            }
        }

        public void Dispose() { _stop.Set(); _thread?.Join(TimeSpan.FromSeconds(10)); _stop.Dispose(); }
    }
}
