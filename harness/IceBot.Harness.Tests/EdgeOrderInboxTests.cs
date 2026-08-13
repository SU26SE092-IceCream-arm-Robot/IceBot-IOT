using System;
using System.IO;
using System.Text.Json;
using IceBot.Workflow;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class EdgeOrderInboxTests
    {
        [Fact]
        public void Validate_AcceptsBackendSchema5ExecuteOrder()
        {
            var commandId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var payload = BuildPayload(commandId, orderId, 2);

            var result = EdgeOrderInbox.Validate(commandId, payload);

            Assert.Equal(commandId, result.CommandId);
            Assert.Equal(orderId, result.OrderId);
            Assert.Equal("ORD-001", result.OrderNumber);
        }

        [Fact]
        public void Validate_RejectsNonPositiveQuantity()
        {
            var commandId = Guid.NewGuid();
            var error = Assert.Throws<FormatException>(() =>
                EdgeOrderInbox.Validate(commandId, BuildPayload(commandId, Guid.NewGuid(), 0)));

            Assert.Contains("quantity", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TryStore_DeduplicatesByCommandId()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-inbox-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var commandId = Guid.NewGuid();
                var order = EdgeOrderInbox.Validate(commandId, BuildPayload(commandId, Guid.NewGuid(), 1));

                Assert.True(EdgeOrderInbox.TryStore(order, directory));
                Assert.False(EdgeOrderInbox.TryStore(order, directory));
                Assert.Single(Directory.GetFiles(directory, "*.json"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static string BuildPayload(Guid commandId, Guid orderId, int quantity)
        {
            return JsonSerializer.Serialize(new
            {
                SchemaVersion = 5,
                CommandId = commandId,
                OrderId = orderId,
                OrderNumber = "ORD-001",
                KioskId = Guid.NewGuid(),
                TargetExecutionEndpointId = Guid.NewGuid(),
                ConfigurationReleaseId = Guid.NewGuid(),
                ReleaseChecksum = "release-checksum",
                CommandExpiryAt = DateTimeOffset.UtcNow.AddMinutes(5),
                OrderLines = new[]
                {
                    new
                    {
                        OrderItemId = Guid.NewGuid(),
                        Quantity = quantity,
                        ProductionUnitStartNo = 1,
                        RobotPrograms = new[]
                        {
                            new
                            {
                                BindingOrder = 1,
                                Artifacts = new[]
                                {
                                    new
                                    {
                                        RobotArtifactId = Guid.NewGuid(),
                                        RunOrder = 1,
                                        ArtifactChecksum = "artifact-checksum"
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }
    }
}
