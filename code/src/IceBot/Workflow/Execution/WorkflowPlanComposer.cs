using System;
using System.Collections.Generic;
using System.IO;

namespace IceBot.Workflow.Execution
{
    internal static class WorkflowPlanComposer
    {
        private const long MaxArtifactBytes = 1024 * 1024;
        private const int MaxPlanInstructions = 10000;

        public static WorkflowPlan Compose(string workflowDirectory, IReadOnlyList<string> orderedFiles)
        {
            if (orderedFiles == null || orderedFiles.Count == 0)
                throw new InvalidDataException("Workflow phai co it nhat mot Lua artifact.");

            var root = Path.GetFullPath(workflowDirectory);
            var instructions = new List<WorkflowInstruction>();
            var sources = new List<string>();
            foreach (var requestedName in orderedFiles)
            {
                if (string.IsNullOrWhiteSpace(requestedName) || Path.GetFileName(requestedName) != requestedName ||
                    !requestedName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Ten Lua artifact khong an toan: '{requestedName}'.");

                var path = Path.GetFullPath(Path.Combine(root, requestedName));
                if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Lua artifact vuot khoi workflow directory: '{requestedName}'.");
                if (!File.Exists(path)) throw new FileNotFoundException("Khong tim thay Lua artifact.", path);
                if (new FileInfo(path).Length > MaxArtifactBytes)
                    throw new InvalidDataException($"Lua artifact '{requestedName}' vuot qua {MaxArtifactBytes} bytes.");

                instructions.AddRange(LuaWorkflowParser.Parse(File.ReadAllText(path), requestedName));
                if (instructions.Count > MaxPlanInstructions)
                    throw new InvalidDataException($"Execution plan vuot qua {MaxPlanInstructions} lenh.");
                sources.Add(requestedName);
            }
            return new WorkflowPlan(instructions, sources);
        }
    }
}
