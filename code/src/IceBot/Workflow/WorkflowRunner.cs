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
        // Each entry in scriptFileNames is a workflow step: its .lua file is uploaded and run
        // on the Fairino controller top-to-bottom (points/actions inside the file are opaque to
        // IceBot). If the step also targets a peripheral machine wired directly to this PC
        // (MachineRegistry), IceBot sends that machine's own signal/command right after the
        // arm finishes running the step's .lua file — the two are not alternatives, the
        // peripheral trigger always follows the arm reaching that step's end position.
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

                for (var i = 0; i < scriptFileNames.Count; i++)
                {
                    var stepName = scriptFileNames[i];
                    Console.WriteLine();
                    Console.WriteLine($"[WORKFLOW] Step {i + 1}/{scriptFileNames.Count}: {stepName}");

                    var fullPath = Path.Combine(workflowDir, stepName);
                    if (!File.Exists(fullPath))
                    {
                        throw new FileNotFoundException($"Script not found in workflow folder: {stepName}", fullPath);
                    }

                    armExecutor.RunScript(fullPath);

                    if (MachineRegistry.TryGetMachineType(stepName, out var machineType))
                    {
                        RunPeripheralTrigger(machineType);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("[WORKFLOW] All scripts completed.");
        }

        // Fires right after the arm finishes the step's .lua file (arm is already in position).
        private static void RunPeripheralTrigger(string machineType)
        {
            var comPort = SiteConfigStore.Load().GetMachinePort(machineType);
            if (string.IsNullOrWhiteSpace(comPort))
            {
                throw new InvalidOperationException(
                    $"Chua cau hinh cong COM cho may '{machineType}'. Chon menu 1 de cau hinh.");
            }

            switch (machineType)
            {
                case MachineRegistry.CupDropping:
                    RunCupDroppingTrigger(comPort);
                    break;
                default:
                    throw new InvalidOperationException($"Khong ho tro loai may '{machineType}'.");
            }
        }

        private static void RunCupDroppingTrigger(string comPort)
        {
            using (var client = new CupDroppingMachineClient(comPort))
            {
                client.Connect();
                Console.WriteLine($"[MACHINE] cup_dropping @ {comPort}: dispensing cup...");
                var ok = client.DispenseCup();
                Console.WriteLine(ok ? "[MACHINE] Dispense OK." : "[MACHINE] Dispense FAILED.");
                if (!ok)
                {
                    throw new InvalidOperationException("May tha coc bao loi (setting failed).");
                }
            }
        }
    }
}
