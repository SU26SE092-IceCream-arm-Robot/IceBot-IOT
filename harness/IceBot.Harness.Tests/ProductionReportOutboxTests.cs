using System;
using System.IO;
using System.Text.Json;
using IceBot.Workflow;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class ProductionReportOutboxTests
    {
        [Fact]
        public void Enqueue_PersistsCompletePerUnitReportWithStableSequence()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-report-outbox-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var job = new DurableOrderJob
                {
                    CommandId = Guid.NewGuid(),
                    ConfigurationReleaseId = Guid.NewGuid(),
                    ReleaseChecksum = "release-checksum",
                    ActiveSetVersion = 7,
                    ActiveSetChecksum = "active-checksum"
                };
                var unit = new DurableProductionUnit
                {
                    SourceProductionJobId = Guid.NewGuid(),
                    OrderItemId = Guid.NewGuid(),
                    ProductionUnitNo = 3
                };

                ProductionReportOutbox.Enqueue(job, unit, "Failed", true, "MachineFault", "No response", directory, 42);
                var path = Assert.Single(Directory.GetFiles(directory, "*.json"));
                var report = JsonSerializer.Deserialize<ProductionReportData>(File.ReadAllText(path));

                Assert.NotNull(report);
                Assert.Equal(42, report!.SequenceNumber);
                Assert.Equal(job.CommandId, report.CommandId);
                Assert.Equal(unit.SourceProductionJobId, report.SourceProductionJobId);
                Assert.Equal(unit.OrderItemId, report.OrderItemId);
                Assert.Equal(3, report.ProductionUnitNo);
                Assert.Equal(1, report.ProductionUnitQuantity);
                Assert.True(report.PhysicalOutputMayHaveOccurred);
                Assert.Equal("MachineFault", report.ErrorCode);
                Assert.NotEqual(Guid.Empty, report.SourceEventId);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
