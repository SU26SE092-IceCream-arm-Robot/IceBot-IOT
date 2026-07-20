using System.Collections.Generic;

namespace IceBot.Machines
{
    // Identity for one machine/station on the line. Every workflow step (.lua file) belongs
    // to exactly one machine identifier — there is no such thing as a step with no machine —
    // so every machine, whether or not it needs a serial connection, gets one of these
    // registered in MachineRegistry.Modules. This is what WorkflowQueueBuilder uses to sort
    // steps into physical order.
    //
    // A machine that also needs to send it own signal over a COM port (Path A — see
    // PROJECT_CONTEXT.md) additionally implements IMachineTrigger.
    internal interface IMachineModule
    {
        // Stable id for this machine — used as the MachinePorts config key for machines that
        // also implement IMachineTrigger.
        string MachineType { get; }

        // Human-readable label for setup prompts and logs (Vietnamese).
        string DisplayName { get; }

        // This machine's physical position along the production line (lower = earlier).
        // Used by WorkflowQueueBuilder to order a set of steps into one continuous workflow —
        // e.g. the cup dropper is upstream of the ice cream dispenser, so it must sort first.
        // Positions don't need to be contiguous; only relative order matters.
        int Position { get; }

        // Workflow step names (.lua file, without extension) that belong to this machine.
        IReadOnlyCollection<string> StepNames { get; }
    }
}
