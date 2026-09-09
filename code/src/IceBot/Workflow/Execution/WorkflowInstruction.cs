using System;
using System.Collections.Generic;

namespace IceBot.Workflow.Execution
{
    internal enum WorkflowInstructionKind
    {
        MoveJ,
        MoveL,
        SetDo,
        SetToolDo,
        Wait,
        TriggerDevice
    }

    internal sealed class WorkflowInstruction
    {
        public WorkflowInstructionKind Kind { get; set; }
        public string SourceFile { get; set; } = string.Empty;
        public int SourceLine { get; set; }
        public IReadOnlyList<double> Values { get; set; } = Array.Empty<double>();
        public double Speed { get; set; }
        public double Acceleration { get; set; }
        public int Index { get; set; }
        public int State { get; set; }
        public int DelayMilliseconds { get; set; }
        public string MachineType { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
    }

    internal sealed class WorkflowPlan
    {
        public WorkflowPlan(IReadOnlyList<WorkflowInstruction> instructions, IReadOnlyList<string> sourceFiles)
        {
            Instructions = instructions;
            SourceFiles = sourceFiles;
        }

        public IReadOnlyList<WorkflowInstruction> Instructions { get; }
        public IReadOnlyList<string> SourceFiles { get; }
    }
}
