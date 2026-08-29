using System;
using System.Collections.Generic;
using System.IO;
using IceBot.Config;
using IceBot.Machines;
using IceBot.Robot;
using IceBot.Workflow.Execution;
using System.Threading;

namespace IceBot.Workflow
{
    internal static class WorkflowRunner
    {
        // Name of the arm's home teaching point, saved on the robot controller via the Fairino
        // app. WorkflowRunner reads it from the controller and moves there — it is not a .lua
        // step file. Rename here if the point is saved under a different name on the robot.
        internal const string HomeTeachingPoint = "IceBot_Home";

        // Each entry in scriptFileNames is a workflow step: its .lua file is uploaded and run
        // on the Fairino controller top-to-bottom (points/actions inside the file are opaque to
        // IceBot). If the step also targets a peripheral machine wired directly to this PC
        // (MachineRegistry), IceBot sends that machine's own signal/command right after the
        // arm finishes running the step's .lua file — the two are not alternatives, the
        // peripheral trigger always follows the arm reaching that step's end position.
        //
        // The arm only returns to its home teaching point (IceBot_Home) at the two points the
        // physical process calls for: once at the start of a run (stands in for "just
        // connected / reset") and once at the end (stands in for "this item is done"). Steps
        // in between run back-to-back with no jump back to home — see the Lua chaining rule.
        public static void RunQueue(IReadOnlyList<string> scriptFileNames, string robotIp)
        {
            RunQueue(scriptFileNames, robotIp, AppConfig.GetWorkflowDirectory());
        }

        // Overload for running steps from a folder other than workflow/ (e.g. test-workflow/'s
        // sample robot-test script, which is not a BE-provisioned file).
        public static void RunQueue(IReadOnlyList<string> scriptFileNames, string robotIp, string workflowDir)
        {
            Console.WriteLine($"[WORKFLOW] Directory: {workflowDir}");

            Console.WriteLine($"[WORKFLOW] Queue ({scriptFileNames.Count} step(s)):");
            for (var i = 0; i < scriptFileNames.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {scriptFileNames[i]}");
            }

            // Compose and validate before the first physical motion.
            var plan = WorkflowPlanComposer.Compose(workflowDir, scriptFileNames);
            ValidateDeviceTriggers(plan);
            Console.WriteLine($"[WORKFLOW] Execution plan: {plan.Instructions.Count} lenh.");

            using (var armExecutor = RobotWorkflowExecutorFactory.Create(robotIp))
            {
                armExecutor.Connect();

                armExecutor.MoveToTeachingPoint(HomeTeachingPoint);

                WorkflowPlanExecutor.Execute(plan, new EdgeWorkflowRuntime(armExecutor));


                Console.WriteLine();
                armExecutor.MoveToTeachingPoint(HomeTeachingPoint);
            }

            Console.WriteLine();
            Console.WriteLine("[WORKFLOW] All scripts completed.");
        }

        private static void ValidateDeviceTriggers(WorkflowPlan plan)
        {
            var settings = SiteConfigStore.Load();
            foreach (var instruction in plan.Instructions)
            {
                if (instruction.Kind != WorkflowInstructionKind.TriggerDevice) continue;
                if (!IsTriggerCommand(instruction.Command))
                    throw new InvalidDataException($"{instruction.SourceFile}:{instruction.SourceLine}: Command '{instruction.Command}' chua duoc ho tro; dung ON hoac TRIGGER.");
                if (!MachineRegistry.TryGetModuleByMachineType(instruction.MachineType, out var module))
                    throw new InvalidDataException($"{instruction.SourceFile}:{instruction.SourceLine}: Khong co driver cho machineType '{instruction.MachineType}'.");
                if (!(module is IMachineTrigger))
                    throw new InvalidDataException($"{instruction.SourceFile}:{instruction.SourceLine}: Driver '{instruction.MachineType}' khong ho tro trigger.");
                if (!IsPeripheralSimulationEnabled && string.IsNullOrWhiteSpace(settings.GetMachinePort(module.MachineType)))
                    throw new InvalidOperationException($"Chua cau hinh cong COM cho may '{module.DisplayName}'.");
            }
        }

        private static bool IsTriggerCommand(string command) =>
            string.Equals(command, "ON", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "TRIGGER", StringComparison.OrdinalIgnoreCase);

        internal static bool IsPeripheralSimulationEnabled =>
            AppConfig.RobotExecutionMode == RobotExecutionMode.Simulated;

        private sealed class EdgeWorkflowRuntime : IWorkflowRuntime
        {
            private readonly IRobotWorkflowExecutor _robot;
            public EdgeWorkflowRuntime(IRobotWorkflowExecutor robot) => _robot = robot;
            public void MoveJ(WorkflowInstruction instruction) => _robot.MoveJ(instruction);
            public void MoveL(WorkflowInstruction instruction) => _robot.MoveL(instruction);
            public void SetDo(int index, int state, bool toolOutput) => _robot.SetDo(index, state, toolOutput);
            public void Wait(int milliseconds) => Thread.Sleep(milliseconds);
            public void TriggerDevice(string machineType, string command)
            {
                if (!IsTriggerCommand(command) || !MachineRegistry.TryGetModuleByMachineType(machineType, out var module) || !(module is IMachineTrigger trigger))
                    throw new InvalidOperationException($"TriggerDevice khong hop le cho '{machineType}'.");
                if (IsPeripheralSimulationEnabled)
                {
                    Console.WriteLine($"[SIMULATOR] Peripheral trigger simulated: {module.MachineType} {command}.");
                    return;
                }
                trigger.Trigger(SiteConfigStore.Load().GetMachinePort(trigger.MachineType));
            }
        }
    }
}
