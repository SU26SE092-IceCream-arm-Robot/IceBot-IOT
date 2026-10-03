using System;
using System.IO;
using System.Net;
using IceBot.Api;
using IceBot.Workflow;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class ProductionReportOutboxTests
    {
        [Fact]
        public void Flush_WhenBackendReturns400BadRequest_DeletesOutboxReportFileAndLogsIncident()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-outbox-test-400-" + Guid.NewGuid().ToString("N"));
            var logsDirectory = Path.Combine(directory, "logs");
            try
            {
                var orderItemId = Guid.NewGuid();
                var report = new ProductionReportData
                {
                    CommandId = Guid.NewGuid(),
                    SourceEventId = Guid.NewGuid(),
                    SequenceNumber = 1,
                    Status = "Completed",
                    SourceProductionJobId = Guid.NewGuid(),
                    OrderItemId = orderItemId,
                    ProductionUnitNo = 1
                };

                ProductionReportOutbox.Store(report, directory);
                Assert.Single(Directory.GetFiles(directory, "*.json"));

                // When backend rejects with HTTP 400 (e.g. order cancelled/closed on backend)
                ProductionReportOutbox.Flush(directory, _ =>
                    throw new EdgeApiException(HttpStatusCode.BadRequest, "Production execution status transition is not allowed."),
                    logsDirectory);

                // File should be deleted so it does not block subsequent outbox reports
                Assert.Empty(Directory.GetFiles(directory, "*.json"));

                // Incident must be logged into runtime-events.jsonl
                var logFile = Path.Combine(logsDirectory, "runtime-events.jsonl");
                Assert.True(File.Exists(logFile));
                var logContent = File.ReadAllText(logFile);
                Assert.Contains("ReportRejected", logContent);
                Assert.Contains("HTTP 400", logContent);
                Assert.Contains(orderItemId.ToString(), logContent);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void Flush_WhenBackendReturns409Conflict_DeletesOutboxReportFile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-outbox-test-409-" + Guid.NewGuid().ToString("N"));
            try
            {
                var report = new ProductionReportData
                {
                    CommandId = Guid.NewGuid(),
                    SourceEventId = Guid.NewGuid(),
                    SequenceNumber = 1,
                    Status = "Completed"
                };

                ProductionReportOutbox.Store(report, directory);
                Assert.Single(Directory.GetFiles(directory, "*.json"));

                // When backend rejects with HTTP 409 Conflict (non-retryable client error)
                ProductionReportOutbox.Flush(directory, _ =>
                    throw new EdgeApiException(HttpStatusCode.Conflict, "Conflict state."));

                // File should be deleted
                Assert.Empty(Directory.GetFiles(directory, "*.json"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void Flush_WhenBackendReturnsTransientError_RetainsReportFileForRetry()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-outbox-test-transient-" + Guid.NewGuid().ToString("N"));
            try
            {
                var report = new ProductionReportData
                {
                    CommandId = Guid.NewGuid(),
                    SourceEventId = Guid.NewGuid(),
                    SequenceNumber = 1,
                    Status = "Completed"
                };

                ProductionReportOutbox.Store(report, directory);
                Assert.Single(Directory.GetFiles(directory, "*.json"));

                // Transient 500 or network failure should keep the file in outbox
                ProductionReportOutbox.Flush(directory, _ =>
                    throw new InvalidOperationException("Network connection failed."));

                // File must still be present for retry
                Assert.Single(Directory.GetFiles(directory, "*.json"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void Flush_WhenBackendReturns429TooManyRequests_RetainsReportFileForRetry()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-outbox-test-429-" + Guid.NewGuid().ToString("N"));
            try
            {
                var report = new ProductionReportData
                {
                    CommandId = Guid.NewGuid(),
                    SourceEventId = Guid.NewGuid(),
                    SequenceNumber = 1,
                    Status = "Completed"
                };

                ProductionReportOutbox.Store(report, directory);
                Assert.Single(Directory.GetFiles(directory, "*.json"));

                // HTTP 429 Too Many Requests is retryable
                ProductionReportOutbox.Flush(directory, _ =>
                    throw new EdgeApiException((HttpStatusCode)429, "Rate limit exceeded."));

                // File must still be present for retry
                Assert.Single(Directory.GetFiles(directory, "*.json"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void Flush_WhenSuccessful_DeletesReportFile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-outbox-test-success-" + Guid.NewGuid().ToString("N"));
            try
            {
                var report = new ProductionReportData
                {
                    CommandId = Guid.NewGuid(),
                    SourceEventId = Guid.NewGuid(),
                    SequenceNumber = 1,
                    Status = "Completed"
                };

                ProductionReportOutbox.Store(report, directory);
                Assert.Single(Directory.GetFiles(directory, "*.json"));

                bool sent = false;
                ProductionReportOutbox.Flush(directory, _ => sent = true);

                Assert.True(sent);
                Assert.Empty(Directory.GetFiles(directory, "*.json"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
