using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IceBot.Machines;
using IceBot.Robot;
using IceBot.Workflow;
using IceBot.Workflow.Execution;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class WorkflowExecutionPlanTests
    {
        [Theory]
        [InlineData("icemachine", "ice_cream")]
        [InlineData("ICE_cream", "ICE_cream")]
        [InlineData(" bt_cup_l90 ", "bt_cup_l90")]
        public void MachineTypeCanonicalizer_NormalizesKnownAliasAndWhitespace(string input, string expected)
        {
            Assert.Equal(expected, MachineTypeCanonicalizer.Canonicalize(input));
        }

        [Fact]
        public void Parse_FairinoStudioOutputBuildsTypedExecutionPlan()
        {
            var lua = @"
local ACTIVE_TOOL_NAME = ""None""
local USED_DEVICES = {""ice_cream""}
local function toDouble(v)
    return tonumber(tostring(v)) + 0.0
end
MoveJ({toDouble(""1""), toDouble(""2""), toDouble(""3""), toDouble(""4""), toDouble(""5""), toDouble(""6"")}, 0, 0, toDouble(""30.0""), toDouble(""40.0""), toDouble(""-1.0""), toDouble(""-1.0""))
MoveL({toDouble(""10""), toDouble(""20""), toDouble(""30""), toDouble(""1""), toDouble(""2""), toDouble(""3"")}, 0, 0, toDouble(""25.0""), toDouble(""35.0""), toDouble(""-1.0""), toDouble(""-1.0""))
SetDO(1, 1, 0)
SetToolDO(1, 0, 0, 0)
WaitMs(500)
TriggerDevice(""ice_cream"", ""ON"")";

            var plan = LuaWorkflowParser.Parse(lua, "step.lua");

            Assert.Equal(new[]
            {
                WorkflowInstructionKind.MoveJ,
                WorkflowInstructionKind.MoveL,
                WorkflowInstructionKind.SetDo,
                WorkflowInstructionKind.SetToolDo,
                WorkflowInstructionKind.Wait,
                WorkflowInstructionKind.TriggerDevice
            }, plan.Select(item => item.Kind));
            Assert.Equal(new double[] { 1, 2, 3, 4, 5, 6 }, plan[0].Values);
            Assert.Equal(30, plan[0].Speed);
            Assert.Equal("ice_cream", plan[5].MachineType);
            Assert.Equal("ON", plan[5].Command);
        }

        [Fact]
        public void Parse_CycleLoopExpandsToFiniteInstructions()
        {
            var lua = @"
for loop_idx = 1, 3 do
    WaitMs(10)
    TriggerDevice(""ice_cream"", ""ON"")
end";

            var plan = LuaWorkflowParser.Parse(lua, "loop.lua");

            Assert.Equal(6, plan.Count);
            Assert.Equal(3, plan.Count(item => item.Kind == WorkflowInstructionKind.TriggerDevice));
        }

        [Theory]
        [InlineData("os.execute(\"bad\")")]
        [InlineData("local start_time = os.clock()\nwhile (os.clock() - start_time < 2) do\nWaitMs(1)\nend")]
        [InlineData("MoveJ({1,2,3,4,5,6}, 0, 0, 0, 30, -1, -1)")]
        [InlineData("SetDO(16, 1, 0)")]
        [InlineData("SetToolDO(2, 1, 0, 0)")]
        public void Parse_RejectsUnsafeOrUnboundedWorkflow(string lua)
        {
            Assert.Throws<InvalidDataException>(() => LuaWorkflowParser.Parse(lua, "unsafe.lua"));
        }

        [Fact]
        public void Compose_PreservesBackendArtifactOrder()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-plan-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, "first.lua"), "WaitMs(100)");
                File.WriteAllText(Path.Combine(directory, "second.lua"), "TriggerDevice(\"ice_cream\", \"ON\")");

                var plan = WorkflowPlanComposer.Compose(directory, new[] { "second.lua", "first.lua" });

                Assert.Equal(new[] { "second.lua", "first.lua" }, plan.SourceFiles);
                Assert.Equal(WorkflowInstructionKind.TriggerDevice, plan.Instructions[0].Kind);
                Assert.Equal(WorkflowInstructionKind.Wait, plan.Instructions[1].Kind);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Fact]
        public void Compose_RejectsArtifactPathTraversal()
        {
            Assert.Throws<InvalidDataException>(() =>
                WorkflowPlanComposer.Compose(Path.GetTempPath(), new[] { "../outside.lua" }));
        }

        [Fact]
        public void Execute_DispatchesEveryInstructionInOrder()
        {
            var instructions = new[]
            {
                Instruction(WorkflowInstructionKind.MoveJ),
                Instruction(WorkflowInstructionKind.MoveL),
                new WorkflowInstruction { Kind = WorkflowInstructionKind.SetDo, Index = 1, State = 1 },
                new WorkflowInstruction { Kind = WorkflowInstructionKind.SetToolDo, Index = 2, State = 0 },
                new WorkflowInstruction { Kind = WorkflowInstructionKind.Wait, DelayMilliseconds = 500 },
                new WorkflowInstruction { Kind = WorkflowInstructionKind.TriggerDevice, MachineType = "ice_cream", Command = "ON" }
            };
            var runtime = new RecordingRuntime();

            WorkflowPlanExecutor.Execute(new WorkflowPlan(instructions, new[] { "step.lua" }), runtime);

            Assert.Equal(new[] { "MoveJ", "MoveL", "SetDO:1:1", "SetToolDO:2:0", "Wait:500", "Trigger:ice_cream:ON" }, runtime.Calls);
        }

        [Theory]
        [InlineData(143)]
        [InlineData(-4)]
        public void HomeFallback_AppliesOnlyToVerifiedPointOnController37(int errorCode)
        {
            var data = new double[20];

            var applied = FairinoLuaExecutor.TryApplyHomeFallback("IceBot_Home", errorCode, ref data);

            Assert.True(applied);
            Assert.Equal(new[] { 95.150, -71.889, 127.806, -56.970, 90.004, 0.105 }, data.Skip(6).Take(6));
            Assert.Equal(40.0, data[14]);
        }

        [Fact]
        public void RuntimeHomePoint_UsesVerifiedFallbackIdentity()
        {
            Assert.Equal("IceBot_Home", WorkflowRunner.HomeTeachingPoint);
        }

        [Theory]
        [InlineData("Other_Home", 143)]
        [InlineData("IceBot_Home", 142)]
        [InlineData("IceBot_Home", -2)]
        [InlineData("icebot_home", 143)]
        public void HomeFallback_RejectsUnexpectedPointOrError(string pointName, int errorCode)
        {
            var original = new double[20];
            var data = original;

            var applied = FairinoLuaExecutor.TryApplyHomeFallback(pointName, errorCode, ref data);

            Assert.False(applied);
            Assert.Same(original, data);
        }
        private static WorkflowInstruction Instruction(WorkflowInstructionKind kind) => new WorkflowInstruction
        {
            Kind = kind,
            Values = new double[] { 1, 2, 3, 4, 5, 6 },
            Speed = 30,
            Acceleration = 30
        };

        private sealed class RecordingRuntime : IWorkflowRuntime
        {
            public List<string> Calls { get; } = new List<string>();
            public void MoveJ(WorkflowInstruction instruction) => Calls.Add("MoveJ");
            public void MoveL(WorkflowInstruction instruction) => Calls.Add("MoveL");
            public void SetDo(int index, int state, bool toolOutput) => Calls.Add($"{(toolOutput ? "SetToolDO" : "SetDO")}:{index}:{state}");
            public void Wait(int milliseconds) => Calls.Add($"Wait:{milliseconds}");
            public void TriggerDevice(string machineType, string command) => Calls.Add($"Trigger:{machineType}:{command}");
        }
    }
}
