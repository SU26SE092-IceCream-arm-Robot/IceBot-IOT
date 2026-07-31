using System.Collections.Generic;

namespace IceBot.Machines
{
    // Identity for one machine/station on the line. Every workflow step (.lua file) belongs
    // to exactly one machine identifier — there is no such thing as a step with no machine —
    // so every machine, whether or not it needs a serial connection, gets one of these
    // registered in MachineRegistry.Modules.
    //
    // A machine that also needs to send its own signal over RS485 (see PROJECT_CONTEXT.md)
    // additionally implements IMachineTrigger.
    internal interface IMachineModule
    {
        // Stable id for this machine — used as the MachinePorts config key for machines that
        // also implement IMachineTrigger.
        string MachineType { get; }

        // Human-readable label for setup prompts and logs (Vietnamese).
        string DisplayName { get; }

        // Workflow step names (.lua file, without extension) that belong to this machine.
        IReadOnlyCollection<string> StepNames { get; }
    }
}
