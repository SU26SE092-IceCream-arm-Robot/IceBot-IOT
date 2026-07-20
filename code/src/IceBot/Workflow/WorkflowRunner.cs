using System;
using System.Collections.Generic;
using System.IO;
using IceBot.Config;
using IceBot.Machines;
using IceBot.Robot;

namespace IceBot.Workflow
{
    internal static class WorkflowRunner
    {
        // Name of the arm's home teaching point, saved on the robot controller via the Fairino
        // app. WorkflowRunner reads it from the controller and moves there — it is not a .lua
        // step file. Rename here if the point is saved under a different name on the robot.
        private const string HomeTeachingPoint = "robot_home";

        // Each entry in scriptFileNames is a workflow step: its .lua file is uploaded and run
        // on the Fairino controller top-to-bottom (points/actions inside the file are opaque to
        // IceBot). If the step also targets a peripheral machine wired directly to this PC
        // (MachineRegistry), IceBot sends that machine's own signal/command right after the
        // arm finishes running the step's .lua file — the two are not alternatives, the
        // peripheral trigger always follows the arm reaching that step's end position.
        //
        // The arm only returns to its home teaching point (robot_home) at the two points the
        // physical process calls for: once at the start of a run (stands in for "just
        // connected / reset") and once at the end (stands in for "this item is done"). Steps
        // in between run back-to-back with no jump back to home — see the Lua chaining rule.
        public static void RunQueue(IReadOnlyList<string> scriptFileNames, string robotIp)
        {
            var workflowDir = AppConfig.GetWorkflowDirectory();
            Console.WriteLine($"[WORKFLOW] Directory: {workflowDir}");

            Console.WriteLine($"[WORKFLOW] Queue ({scriptFileNames.Count} step(s)):");
            for (var i = 0; i < scriptFileNames.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {scriptFileNames[i]}");
            }

            using (var armExecutor = new FairinoLuaExecutor(robotIp))
            {
                armExecutor.Connect();

                armExecutor.MoveToTeachingPoint(HomeTeachingPoint);

                for (var i = 0; i < scriptFileNames.Count; i++)
                {
                    var stepName = scriptFileNames[i];
                    Console.WriteLine();
                    Console.WriteLine($"[WORKFLOW] Step {i + 1}/{scriptFileNames.Count}: {stepName}");
                    RunStep(armExecutor, workflowDir, stepName);
                }

                Console.WriteLine();
                armExecutor.MoveToTeachingPoint(HomeTeachingPoint);
            }

            Console.WriteLine();
            Console.WriteLine("[WORKFLOW] All scripts completed.");
        }

        private static void RunStep(FairinoLuaExecutor armExecutor, string workflowDir, string stepName)
        {
            var fullPath = Path.Combine(workflowDir, stepName);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Script not found in workflow folder: {stepName}", fullPath);
            }

            armExecutor.RunScript(fullPath);

            // Every step belongs to some machine (MachineRegistry), but only machines wired
            // over serial (IMachineTrigger) need a follow-up signal — a pure arm-motion
            // machine's whole action is the .lua file that just ran.
            if (MachineRegistry.TryGetModule(stepName, out var module) && module is IMachineTrigger trigger)
            {
                Trigger(trigger);
            }
        }

        // Fires right after the arm finishes the step's .lua file (arm is already in position).
        private static void Trigger(IMachineTrigger trigger)
        {
            var comPort = SiteConfigStore.Load().GetMachinePort(trigger.MachineType);
            if (string.IsNullOrWhiteSpace(comPort))
            {
                throw new InvalidOperationException(
                    $"Chua cau hinh cong COM cho may '{trigger.DisplayName}'. Chon menu 1 de cau hinh.");
            }

            trigger.Trigger(comPort);
        }
    }
}
