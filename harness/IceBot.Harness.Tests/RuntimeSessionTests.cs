using System;
using System.IO;
using System.Threading;
using IceBot;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class RuntimeSessionTests
    {
        [Fact]
        public void Session_DetectsUncleanExitAndRejectsConcurrentOwner()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-session-" + Guid.NewGuid().ToString("N"));
            var mutex = "IceBot-Test-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var session = new RuntimeSession(directory, mutex))
                {
                    Exception? secondError = null;
                    var thread = new Thread(() =>
                    {
                        try { using (var second = new RuntimeSession(directory, mutex)) { } }
                        catch (Exception ex) { secondError = ex; }
                    });
                    thread.Start();
                    Assert.True(thread.Join(5000));
                    Assert.IsType<InvalidOperationException>(secondError);
                    session.RecordFailure(new Exception("simulated crash"));
                }
                Assert.Equal("Running", File.ReadAllText(Path.Combine(directory, "session-state.txt")));
                using (var restarted = new RuntimeSession(directory, mutex)) { }
                Assert.Equal("CleanShutdown", File.ReadAllText(Path.Combine(directory, "session-state.txt")));
                var log = File.ReadAllText(Path.Combine(directory, "runtime-events.jsonl"));
                Assert.Contains("UncleanShutdownDetected", log);
                Assert.Contains("simulated crash", log);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Fact]
        public void CompletedReport_ReplayUsesIdenticalEnvelope()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-report-replay-" + Guid.NewGuid().ToString("N"));
            try
            {
                var report = IceBot.Workflow.ProductionReportOutbox.Create(new IceBot.Workflow.DurableOrderJob(),
                    new IceBot.Workflow.DurableProductionUnit(), "Completed", true, null, null, 123);
                IceBot.Workflow.ProductionReportOutbox.Store(report, directory);
                var path = Assert.Single(Directory.GetFiles(directory));
                var content = File.ReadAllText(path);
                IceBot.Workflow.ProductionReportOutbox.Store(report, directory);
                Assert.Single(Directory.GetFiles(directory));
                File.Delete(path); // Delivery succeeded, but the process died before clearing the job flag.
                IceBot.Workflow.ProductionReportOutbox.Store(report, directory);
                Assert.Equal(content, File.ReadAllText(path));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
