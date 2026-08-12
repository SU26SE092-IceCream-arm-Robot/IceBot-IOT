using System;
using System.Collections.Generic;
using IceBot.Machines;

namespace IceBot.Driver.Template
{
    // Copy this project for a new machine, then implement its physical protocol here.
    public sealed class TemplateMachineDriver : IMachineTrigger, IMachineDiagnostics
    {
        public string MachineType => "replace_with_stable_machine_type";
        public string DisplayName => "May mau";
        public IReadOnlyCollection<string> StepNames { get; } = new[] { "replace_with_lua_step" };

        public void TestConnection(string connectionName) =>
            throw new NotImplementedException("Implement the machine connection check.");

        public void Trigger(string connectionName) =>
            throw new NotImplementedException("Implement the machine command protocol.");

        public string GetStatusText(string connectionName) =>
            throw new NotImplementedException("Implement the machine status query.");
    }
}
