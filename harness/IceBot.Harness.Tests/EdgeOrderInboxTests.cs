using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void Validate_AcceptsAllBackendSupportedSchemaVersions(int schemaVersion)
        {
            var commandId = Guid.NewGuid();
            var json = BuildPayload(commandId, Guid.NewGuid(), 1).Replace("\"SchemaVersion\":5", $"\"SchemaVersion\":{schemaVersion}");
            Assert.Equal(schemaVersion, EdgeOrderInbox.Validate(commandId, json).SchemaVersion);
        }

        [Fact]
        public void ValidateForThisEdge_AcceptsMatchingIdentityReleaseAndLuaChecksum()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-artifact-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var commandId = Guid.NewGuid();
                var order = EdgeOrderInbox.Validate(commandId, BuildPayload(commandId, Guid.NewGuid(), 1));
                var artifact = Assert.Single(Assert.Single(Assert.Single(order.OrderLines).RobotPrograms).Artifacts);
                var bytes = Encoding.UTF8.GetBytes("print('verified')");
                File.WriteAllBytes(Path.Combine(directory, artifact.ScriptFileName), bytes);
                artifact.ArtifactChecksum = Sha256(bytes);

                EdgeOrderInbox.ValidateForThisEdge(order, order.KioskId, order.TargetExecutionEndpointId,
                    order.ConfigurationReleaseId, order.ReleaseChecksum, directory, DateTimeOffset.UtcNow);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Fact]
        public void ValidateForThisEdge_RejectsMoreThanFourUnits()
        {
            var commandId = Guid.NewGuid();
            var order = EdgeOrderInbox.Validate(commandId, BuildPayload(commandId, Guid.NewGuid(), 5));

            var error = Assert.Throws<OrderRejectionException>(() => EdgeOrderInbox.ValidateForThisEdge(
                order, order.KioskId, order.TargetExecutionEndpointId, order.ConfigurationReleaseId,
                order.ReleaseChecksum, Path.GetTempPath(), DateTimeOffset.UtcNow));

            Assert.Equal("OrderQuantityLimit", error.Code);
        }

        [Fact]
        public void ValidateForThisEdge_RejectsAnotherEndpoint()
        {
            var commandId = Guid.NewGuid();
            var order = EdgeOrderInbox.Validate(commandId, BuildPayload(commandId, Guid.NewGuid(), 1));
            var error = Assert.Throws<OrderRejectionException>(() => EdgeOrderInbox.ValidateForThisEdge(
                order, order.KioskId, Guid.NewGuid(), order.ConfigurationReleaseId,
                order.ReleaseChecksum, Path.GetTempPath(), DateTimeOffset.UtcNow));
            Assert.Equal("WrongEndpoint", error.Code);
        }

        [Fact]
        public void ValidateForThisEdge_RejectsInactiveRelease()
        {
            var commandId = Guid.NewGuid();
            var order = EdgeOrderInbox.Validate(commandId, BuildPayload(commandId, Guid.NewGuid(), 1));
            var error = Assert.Throws<OrderRejectionException>(() => EdgeOrderInbox.ValidateForThisEdge(
                order, order.KioskId, order.TargetExecutionEndpointId, Guid.NewGuid(),
                order.ReleaseChecksum, Path.GetTempPath(), DateTimeOffset.UtcNow));
            Assert.Equal("ReleaseMismatch", error.Code);
        }

        [Fact]
        public void ValidateForThisEdge_RejectsExpiredCommand()
        {
            var commandId = Guid.NewGuid();
            var order = EdgeOrderInbox.Validate(commandId, BuildPayload(commandId, Guid.NewGuid(), 1));
            var error = Assert.Throws<OrderRejectionException>(() => EdgeOrderInbox.ValidateForThisEdge(
                order, order.KioskId, order.TargetExecutionEndpointId, order.ConfigurationReleaseId,
                order.ReleaseChecksum, Path.GetTempPath(), order.CommandExpiryAt.AddSeconds(1)));
            Assert.Equal("CommandExpired", error.Code);
        }

        [Fact]
        public void ValidateForThisEdge_RejectsInstalledLuaWithWrongChecksum()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-artifact-mismatch-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var commandId = Guid.NewGuid();
                var order = EdgeOrderInbox.Validate(commandId, BuildPayload(commandId, Guid.NewGuid(), 1));
                var artifact = order.OrderLines[0].RobotPrograms[0].Artifacts[0];
                File.WriteAllText(Path.Combine(directory, artifact.ScriptFileName), "tampered");

                var error = Assert.Throws<OrderRejectionException>(() => EdgeOrderInbox.ValidateForThisEdge(
                    order, order.KioskId, order.TargetExecutionEndpointId, order.ConfigurationReleaseId,
                    order.ReleaseChecksum, directory, DateTimeOffset.UtcNow));
                Assert.Equal("ArtifactChecksumMismatch", error.Code);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Fact]
        public void OrderedArtifacts_UsesBindingOrderThenRunOrder()
        {
            var order = new ReceivedOrderCommand
            {
                OrderLines = new System.Collections.Generic.List<ReceivedOrderLine>
                {
                    new ReceivedOrderLine
                    {
                        RobotPrograms = new System.Collections.Generic.List<ReceivedRobotProgram>
                        {
                            Program(2, Artifact(3), Artifact(1)),
                            Program(1, Artifact(2))
                        }
                    }
                }
            };

            Assert.Equal(new[] { 2, 1, 3 }, EdgeOrderInbox.OrderedArtifacts(order).Select(item => item.RunOrder));
        }

        private static ReceivedRobotProgram Program(int bindingOrder, params ReceivedArtifact[] artifacts) =>
            new ReceivedRobotProgram { BindingOrder = bindingOrder, Artifacts = artifacts.ToList() };

        private static ReceivedArtifact Artifact(int runOrder) =>
            new ReceivedArtifact { RobotArtifactId = Guid.NewGuid(), RunOrder = runOrder, ArtifactChecksum = "checksum" };

        private static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
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
