using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using IceBot.Workflow;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class EdgeOrderExecutionQueueTests
    {
        [Fact]
        public void TryAdmit_LimitsQueueToTenProductionUnits()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-order-queue-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.Equal(OrderAdmissionResult.Accepted, EdgeOrderExecutionQueue.TryAdmit(BuildOrder(4), directory));
                Assert.Equal(OrderAdmissionResult.Accepted, EdgeOrderExecutionQueue.TryAdmit(BuildOrder(4), directory));
                Assert.Equal(OrderAdmissionResult.Accepted, EdgeOrderExecutionQueue.TryAdmit(BuildOrder(2), directory));
                Assert.Equal(OrderAdmissionResult.Busy, EdgeOrderExecutionQueue.TryAdmit(BuildOrder(1), directory));
                Assert.Equal(10, EdgeOrderExecutionQueue.LoadAll(directory)[0].Units.Count +
                    EdgeOrderExecutionQueue.LoadAll(directory)[1].Units.Count +
                    EdgeOrderExecutionQueue.LoadAll(directory)[2].Units.Count);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Fact]
        public void ValidateForThisEdge_RejectsAnotherKiosk()
        {
            var order = BuildOrder(1);
            var error = Assert.Throws<OrderRejectionException>(() => EdgeOrderInbox.ValidateForThisEdge(
                order, Guid.NewGuid(), order.TargetExecutionEndpointId, order.ConfigurationReleaseId,
                order.ReleaseChecksum, Path.GetTempPath(), DateTimeOffset.UtcNow));

            Assert.Equal("WrongKiosk", error.Code);
        }

        [Fact]
        public void TryAdmit_IsIdempotentByCommandId()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-order-idempotency-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var order = BuildOrder(2);
                Assert.Equal(OrderAdmissionResult.Accepted, EdgeOrderExecutionQueue.TryAdmit(order, directory));
                Assert.Equal(OrderAdmissionResult.AlreadyStored, EdgeOrderExecutionQueue.TryAdmit(order, directory));
                Assert.Single(EdgeOrderExecutionQueue.LoadAll(directory));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Fact]
        public void TryAdmit_CreatesOneDurableJobPerUnitWithConsecutiveUnitNumbers()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-order-unit-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var order = BuildOrder(3);
                order.OrderLines[0].ProductionUnitStartNo = 4;

                EdgeOrderExecutionQueue.TryAdmit(order, directory);
                var job = Assert.Single(EdgeOrderExecutionQueue.LoadAll(directory));

                Assert.Equal(new[] { 4, 5, 6 }, job.Units.ConvertAll(unit => unit.ProductionUnitNo));
                Assert.Equal(3, new HashSet<Guid>(job.Units.ConvertAll(unit => unit.SourceProductionJobId)).Count);
                Assert.All(job.Units, unit => Assert.Equal("Pending", unit.Status));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Fact]
        public void RecoverInterruptedJobs_MarksRunningUnitForManualInterventionAndReportsOnce()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-order-recovery-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var order = BuildOrder(2);
                EdgeOrderExecutionQueue.TryAdmit(order, directory);
                var job = EdgeOrderExecutionQueue.LoadAll(directory)[0];
                job.Status = "Running";
                job.Units[0].Status = "Running";
                File.WriteAllText(Path.Combine(directory, job.CommandId.ToString("D") + ".json"), JsonSerializer.Serialize(job));
                var reportCount = 0;

                EdgeOrderExecutionQueue.RecoverInterruptedJobs(directory, (_, __) => reportCount++);
                var recovered = EdgeOrderExecutionQueue.LoadAll(directory)[0];

                Assert.Equal("RequiresManualIntervention", recovered.Status);
                Assert.Equal("RequiresManualIntervention", recovered.Units[0].Status);
                Assert.Equal("RuntimeRestartedDuringExecution", recovered.Units[0].ErrorCode);
                Assert.Equal("Pending", recovered.Units[1].Status);
                Assert.Equal(1, reportCount);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Fact]
        public void NextRunnable_StopsQueueWhenAnyJobRequiresManualIntervention()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-order-block-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var order = BuildOrder(1);
                EdgeOrderExecutionQueue.TryAdmit(order, directory);
                var job = EdgeOrderExecutionQueue.LoadAll(directory)[0];
                job.Status = "RequiresManualIntervention";
                job.Units[0].Status = "RequiresManualIntervention";
                File.WriteAllText(Path.Combine(directory, job.CommandId.ToString("D") + ".json"), JsonSerializer.Serialize(job));

                Assert.Null(EdgeOrderExecutionQueue.NextRunnable(directory));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static ReceivedOrderCommand BuildOrder(int quantity)
        {
            return new ReceivedOrderCommand
            {
                CommandId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                OrderNumber = "ORD-QUEUE",
                KioskId = Guid.NewGuid(),
                TargetExecutionEndpointId = Guid.NewGuid(),
                ConfigurationReleaseId = Guid.NewGuid(),
                ReleaseChecksum = "release",
                CommandExpiryAt = DateTimeOffset.UtcNow.AddMinutes(5),
                OrderLines = new List<ReceivedOrderLine>
                {
                    new ReceivedOrderLine
                    {
                        OrderItemId = Guid.NewGuid(),
                        Quantity = quantity,
                        RobotPrograms = new List<ReceivedRobotProgram>
                        {
                            new ReceivedRobotProgram { BindingOrder = 1 }
                        }
                    }
                }
            };
        }
    }
}
