using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using IceBot.Cli;
using IceBot.Workflow;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class LocalDiagnosticsTests
    {
        [Fact]
        public void Read_HandlesPartialLogAndCorruptJobWithoutChangingProductionState()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-diagnostics-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.Empty(LocalDiagnostics.Read(directory));
                Directory.CreateDirectory(Path.Combine(directory, "logs"));
                Directory.CreateDirectory(Path.Combine(directory, "order-jobs"));
                File.WriteAllText(Path.Combine(directory, "logs", "runtime-events.jsonl"),
                    JsonSerializer.Serialize(new { timestamp = DateTimeOffset.Now, kind = "UncleanShutdownDetected", detail = "unknown" }) + "\n{partial");
                var job = new DurableOrderJob { OrderNumber = "ORD-42", OrderId = Guid.NewGuid(), AcceptedAt = DateTimeOffset.Now, Status = "Running" };
                job.Units.Add(new DurableProductionUnit { ProductionUnitNo = 3, Status = "Running", Attempt = 2,
                    Interruptions = { new InterruptedProductionAttempt { Attempt = 1, DetectedAt = DateTimeOffset.Now } } });
                var path = Path.Combine(directory, "order-jobs", "valid.json");
                var original = JsonSerializer.Serialize(job);
                File.WriteAllText(path, original);
                File.WriteAllText(Path.Combine(directory, "order-jobs", "broken.json"), "{");
                var entries = LocalDiagnostics.Read(directory);
                Assert.Equal(2, entries.Count(item => item.Category == "ReadError"));
                Assert.Contains("chua xac dinh", Assert.Single(entries, item => item.Category == "App").Detail);
                var order = Assert.Single(LocalDiagnostics.Filter(entries, "Order", "ord-42", DateTime.Today));
                Assert.Contains("Cay 3: Running", order.Detail);
                Assert.Contains("Gian doan lan 1", order.Detail);
                Assert.Empty(LocalDiagnostics.Filter(entries, "", "", DateTime.Today.AddDays(1)));
                Assert.Equal(original, File.ReadAllText(path));
                var export = LocalDiagnostics.Export(new[] { order }, Path.Combine(directory, "exports"));
                Assert.Contains("ORD-42", File.ReadAllText(export));
                Assert.DoesNotContain("CompletionReport", File.ReadAllText(export));
                Assert.Equal(original, File.ReadAllText(path));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
