using System;
using System.Collections.Generic;
using System.IO;
using IceBot.Config;
using IceBot.Robot;

namespace IceBot.Workflow
{
    internal static class WorkflowRunner
    {
        public static void RunQueue(IReadOnlyList<string> scriptFileNames, string robotIp)
        {
            var workflowDir = AppConfig.GetWorkflowDirectory();
            Console.WriteLine($"[WORKFLOW] Directory: {workflowDir}");

            var resolvedScripts = new List<string>();
            foreach (var scriptFileName in scriptFileNames)
            {
                var fullPath = Path.Combine(workflowDir, scriptFileName);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Script not found in workflow folder: {scriptFileName}", fullPath);
                }

                resolvedScripts.Add(fullPath);
            }

            Console.WriteLine($"[WORKFLOW] Queue ({resolvedScripts.Count} script(s)):");
            for (var i = 0; i < resolvedScripts.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {Path.GetFileName(resolvedScripts[i])}");
            }

            using (var executor = new FairinoLuaExecutor(robotIp))
            {
                executor.Connect();

                for (var i = 0; i < resolvedScripts.Count; i++)
                {
                    Console.WriteLine();
                    Console.WriteLine($"[WORKFLOW] Step {i + 1}/{resolvedScripts.Count}");
                    executor.RunScript(resolvedScripts[i]);
                }
            }

            Console.WriteLine();
            Console.WriteLine("[WORKFLOW] All scripts completed.");
        }
    }
}
