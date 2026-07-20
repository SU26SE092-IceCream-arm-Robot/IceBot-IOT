using System.Collections.Generic;

namespace IceBot.Machines
{
    // A self-contained controller for one peripheral machine wired directly to this PC
    // (Path A — see PROJECT_CONTEXT.md). To plug in a new machine: implement this interface
    // under Machines/<MachineName>/ and add one instance to MachineRegistry.Modules — nothing
    // else in the app needs to change.
    internal interface IMachineModule
    {
        // Stable id for this machine type — used as the MachinePorts config key.
        string MachineType { get; }

        // Human-readable label for setup prompts and logs (Vietnamese).
        string DisplayName { get; }

        // Workflow step names (.lua file, without extension) that trigger this machine.
        IReadOnlyCollection<string> StepNames { get; }

        // Send this machine's own signal/command over comPort. Called by WorkflowRunner
        // right after the arm finishes running the step's .lua file (arm is already in position).
        void Trigger(string comPort);
    }
}
