using System;
using System.Collections.Generic;
using System.IO;
using IceBot.Workflow;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class EdgeOrderExecutionQueueTests
    {
        [Fact]
        public void TryAdmit_AllowsOnlyOneCustomerSessionUntilCompletion()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-order-queue-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.Equal(OrderAdmissionResult.Accepted, EdgeOrderExecutionQueue.TryAdmit(BuildOrder(4), directory));
                Assert.Equal(OrderAdmissionResult.Busy, EdgeOrderExecutionQueue.TryAdmit(BuildOrder(1), directory));
                Assert.True(EdgeOrderExecutionQueue.HasActiveOrUnresolvedWork(directory));
                Assert.Single(EdgeOrderExecutionQueue.LoadAll(directory));
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
