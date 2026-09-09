using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IceBot.Workflow.Execution
{
    internal static class ActiveWorkflowPeripheralDiscovery
    {
        internal static IReadOnlyList<string> DiscoverMachineTypes(string workflowDirectory)
        {
            if (string.IsNullOrWhiteSpace(workflowDirectory) || !Directory.Exists(workflowDirectory))
                return Array.Empty<string>();

            var machineTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Directory.GetFiles(workflowDirectory, "*.lua", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var sourceFile = Path.GetFileName(path);
                foreach (var instruction in LuaWorkflowParser.Parse(File.ReadAllText(path), sourceFile))
                {
                    if (instruction.Kind == WorkflowInstructionKind.TriggerDevice &&
                        !string.IsNullOrWhiteSpace(instruction.MachineType))
                        machineTypes.Add(instruction.MachineType.Trim());
                }
            }

            return machineTypes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
