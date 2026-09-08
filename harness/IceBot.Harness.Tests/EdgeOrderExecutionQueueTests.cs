using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IceBot.Workflow;
using IceBot.Workflow.Execution;
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
        public void RecoverAwaitingAcknowledgements_ReplaysAckAndActivatesDurableJob()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-order-ack-recovery-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var order = BuildOrder(1);
                Assert.Equal(OrderAdmissionResult.Accepted, EdgeOrderExecutionQueue.TryAdmit(order, directory));
                var acknowledged = new List<Guid>();
                var reports = new List<string>();
                ProductionReportSink sink = (_, __, status, ___, ____, _____) => reports.Add(status);

                var recovered = EdgeOrderCommandReceiver.RecoverAwaitingAcknowledgements(
                    directory,
                    commandId =>
                    {
                        Assert.Equal("AwaitingAck", EdgeOrderExecutionQueue.LoadAll(directory)[0].Status);
                        acknowledged.Add(commandId);
                    },
                    sink);

                Assert.Equal(1, recovered);
                Assert.Equal(new[] { order.CommandId }, acknowledged);
                Assert.Equal(new[] { "Accepted" }, reports);
                Assert.Equal("Pending", EdgeOrderExecutionQueue.LoadAll(directory)[0].Status);
                Assert.Equal(0, EdgeOrderCommandReceiver.RecoverAwaitingAcknowledgements(
                    directory, commandId => acknowledged.Add(commandId), sink));
                Assert.Single(acknowledged);
                Assert.Single(reports);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Fact]
        public void ExecuteOrderLifecycle_ValidatesPersistsExecutesAndCompletesInOrder()
        {
            var root = Path.Combine(Path.GetTempPath(), "icebot-order-e2e-test-" + Guid.NewGuid().ToString("N"));
            var inbox = Path.Combine(root, "inbox");
            var jobs = Path.Combine(root, "jobs");
            var workflow = Path.Combine(root, "workflow");
            Directory.CreateDirectory(workflow);
            try
            {
                var seed = BuildOrder(1);
                seed.SchemaVersion = 5;
                var artifactId = Guid.NewGuid();
                var lua = Encoding.UTF8.GetBytes("WaitMs(1)\nTriggerDevice(\"ice_cream\", \"ON\")");
                var scriptName = artifactId.ToString("D") + ".lua";
                File.WriteAllBytes(Path.Combine(workflow, scriptName), lua);
                seed.OrderLines[0].RobotPrograms[0].Artifacts.Add(new ReceivedArtifact
                {
                    RobotArtifactId = artifactId,
                    RunOrder = 1,
                    ArtifactChecksum = Sha256(lua),
                    RuntimeTargetCode = "FAIRINO_LUA_V1",
                    MachineModelCode = "FR5"
                });
                var order = EdgeOrderInbox.Validate(seed.CommandId, JsonSerializer.Serialize(seed));

                EdgeOrderInbox.ValidateForThisEdge(order, order.KioskId, order.TargetExecutionEndpointId,
                    order.ConfigurationReleaseId, order.ReleaseChecksum, workflow, DateTimeOffset.UtcNow);
                Assert.True(EdgeOrderInbox.TryStore(order, inbox));
                Assert.Equal(OrderAdmissionResult.Accepted, EdgeOrderExecutionQueue.TryAdmit(order, jobs));

                var reports = new List<string>();
                ProductionReportSink sink = (_, __, status, ___, ____, _____) => reports.Add(status);
                Assert.Equal(1, EdgeOrderCommandReceiver.RecoverAwaitingAcknowledgements(
                    jobs, _ => { }, sink));

                var job = Assert.IsType<DurableOrderJob>(EdgeOrderExecutionQueue.NextRunnable(jobs));
                var unit = EdgeOrderExecutionQueue.BeginNextUnit(job, jobs, sink);
                var plan = WorkflowPlanComposer.Compose(workflow,
                    unit.Artifacts.Select(item => item.ScriptFileName).ToArray());
                var runtime = new RecordingRuntime();
                WorkflowPlanExecutor.Execute(plan, runtime);
                EdgeOrderExecutionQueue.CompleteUnit(job.CommandId, unit.SourceProductionJobId, jobs, sink);

                Assert.Equal(new[] { "Accepted", "Running", "Completed" }, reports);
                Assert.Equal(new[] { "Wait:1", "Trigger:ice_cream:ON" }, runtime.Calls);
                Assert.Equal("Completed", Assert.Single(EdgeOrderExecutionQueue.LoadAll(jobs)).Status);
                Assert.Single(Directory.GetFiles(inbox, "*.json"));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
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
        public void RecoverInterruptedJobs_PreparesLocalRetryAndRecordsInterruptionOnce()
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

                Assert.Equal("Pending", recovered.Status);
                Assert.Equal("Pending", recovered.Units[0].Status);
                Assert.Equal("RuntimeRestartedDuringExecution", Assert.Single(recovered.Units[0].Interruptions).Reason);
                Assert.Equal("Pending", recovered.Units[1].Status);
                Assert.Equal(1, reportCount);
                EdgeOrderExecutionQueue.RecoverInterruptedJobs(directory, (_, __) => reportCount++);
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

        [Fact]
        public void RestartAfterTwoCompletedUnits_RetriesThirdThenFinishesFourth()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-retry-" + Guid.NewGuid().ToString("N"));
            ProductionReportSink sink = (_, __, ___, ____, _____, ______) => { };
            try
            {
                var order = BuildOrder(4);
                order.OrderLines[0].ProductionUnitStartNo = 1;
                EdgeOrderExecutionQueue.TryAdmit(order, directory);
                EdgeOrderExecutionQueue.Activate(order.CommandId, directory, sink);
                for (var index = 0; index < 2; index++)
                {
                    var next = EdgeOrderExecutionQueue.NextRunnable(directory)!;
                    var unit = EdgeOrderExecutionQueue.BeginNextUnit(next, directory, sink);
                    EdgeOrderExecutionQueue.CompleteUnit(order.CommandId, unit.SourceProductionJobId, directory, sink);
                }
                var third = EdgeOrderExecutionQueue.BeginNextUnit(EdgeOrderExecutionQueue.NextRunnable(directory)!, directory, sink);
                Assert.Equal(3, third.ProductionUnitNo);
                // No completion is persisted, even if the physical action has finished.
                for (var restart = 0; restart < 2; restart++)
                {
                    EdgeOrderExecutionQueue.RecoverInterruptedJobs(directory, (_, __) => { });
                    third = EdgeOrderExecutionQueue.BeginNextUnit(EdgeOrderExecutionQueue.NextRunnable(directory)!, directory, sink);
                    Assert.Equal(3, third.ProductionUnitNo);
                    Assert.Equal(restart + 2, third.Attempt);
                }
                Assert.Equal(2, third.Interruptions.Count);
                EdgeOrderExecutionQueue.CompleteUnit(order.CommandId, third.SourceProductionJobId, directory, sink);
                var fourth = EdgeOrderExecutionQueue.BeginNextUnit(EdgeOrderExecutionQueue.NextRunnable(directory)!, directory, sink);
                Assert.Equal(4, fourth.ProductionUnitNo);
                EdgeOrderExecutionQueue.CompleteUnit(order.CommandId, fourth.SourceProductionJobId, directory, sink);
                EdgeOrderExecutionQueue.RecoverInterruptedJobs(directory, (_, __) => throw new Exception("Completed unit retried"));
                Assert.Null(EdgeOrderExecutionQueue.NextRunnable(directory));
                var saved = Assert.Single(EdgeOrderExecutionQueue.LoadAll(directory));
                Assert.All(saved.Units, unit => Assert.Equal("Completed", unit.Status));
                Assert.Equal(1, saved.Units[0].Attempt);
                Assert.Equal(1, saved.Units[1].Attempt);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Fact]
        public void CompletionReportFailure_PreservesCompletedUnitAndRecoversReport()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-report-recovery-" + Guid.NewGuid().ToString("N"));
            ProductionReportSink sink = (_, __, ___, ____, _____, ______) => { };
            try
            {
                var order = BuildOrder(1);
                EdgeOrderExecutionQueue.TryAdmit(order, directory);
                EdgeOrderExecutionQueue.Activate(order.CommandId, directory, sink);
                var unit = EdgeOrderExecutionQueue.BeginNextUnit(EdgeOrderExecutionQueue.NextRunnable(directory)!, directory, sink);
                Assert.Throws<IOException>(() => EdgeOrderExecutionQueue.CompleteUnit(order.CommandId, unit.SourceProductionJobId,
                    directory, (_, __, ___, ____, _____, ______) => throw new IOException("outbox unavailable")));
                EdgeOrderExecutionQueue.FailUnit(order.CommandId, unit.SourceProductionJobId, directory, new Exception("report failed"));
                EdgeOrderExecutionQueue.RecoverInterruptedJobs(directory, (_, __) => throw new Exception("Must not retry"));
                Assert.Equal("Completed", EdgeOrderExecutionQueue.LoadAll(directory)[0].Units[0].Status);
                var reports = 0;
                EdgeOrderExecutionQueue.RecoverCompletionReports(directory, (_, __, status, ___, ____, _____) =>
                {
                    Assert.Equal("Completed", status);
                    reports++;
                });
                EdgeOrderExecutionQueue.RecoverCompletionReports(directory, (_, __, ___, ____, _____, ______) => reports++);
                Assert.Equal(1, reports);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Fact]
        public void Preflight_RejectsChangedOrMissingLuaBeforeExecution()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-preflight-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var artifact = new ReceivedArtifact { RobotArtifactId = Guid.NewGuid(), ArtifactChecksum = Sha256(Encoding.UTF8.GetBytes("WaitMs(1)")) };
                var unit = new DurableProductionUnit { Artifacts = new List<ReceivedArtifact> { artifact } };
                Assert.Throws<FileNotFoundException>(() => OrderExecutionPreflight.ValidateArtifacts(unit, directory));
                File.WriteAllText(Path.Combine(directory, artifact.ScriptFileName), "WaitMs(1)", new UTF8Encoding(false));
                OrderExecutionPreflight.ValidateArtifacts(unit, directory);
                File.WriteAllText(Path.Combine(directory, artifact.ScriptFileName), "WaitMs(2)");
                Assert.Throws<InvalidDataException>(() => OrderExecutionPreflight.ValidateArtifacts(unit, directory));
            }
            finally { Directory.Delete(directory, true); }
        }

        private static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private sealed class RecordingRuntime : IWorkflowRuntime
        {
            public List<string> Calls { get; } = new List<string>();
            public void MoveJ(WorkflowInstruction instruction) => Calls.Add("MoveJ");
            public void MoveL(WorkflowInstruction instruction) => Calls.Add("MoveL");
            public void SetDo(int index, int state, bool toolOutput) => Calls.Add("SetDo");
            public void Wait(int milliseconds) => Calls.Add("Wait:" + milliseconds);
            public void TriggerDevice(string machineType, string command) => Calls.Add("Trigger:" + machineType + ":" + command);
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
