using System;
using System.Collections.Generic;

namespace IceBot.Machines.CupDropping
{
    // Plugs the cup-dropping machine into the system. This is the whole "module" for this
    // machine: which steps trigger it, and what signal to send. See CupDroppingMachineClient
    // for the actual serial protocol.
    internal sealed class CupDroppingMachineModule : IMachineTrigger, IMachineDiagnostics
    {
        public string MachineType => "cup_dropping";

        public string DisplayName => "May tha coc";

        // First station on the line — the cup must exist before anything can be dispensed into it.
        public int Position => 1;

        public IReadOnlyCollection<string> StepNames { get; } = new[] { "cup_s" };

        public void Trigger(string comPort)
        {
            using (var client = new CupDroppingMachineClient(comPort))
            {
                client.Connect();
                Console.WriteLine($"[MACHINE] {DisplayName} @ {comPort}: dispensing cup...");
                var ok = client.DispenseCup();
                Console.WriteLine(ok ? "[MACHINE] Dispense OK." : "[MACHINE] Dispense FAILED.");
                if (!ok)
                {
                    throw new InvalidOperationException("May tha coc bao loi (setting failed).");
                }
            }
        }

        public string GetStatusText(string comPort)
        {
            using (var client = new CupDroppingMachineClient(comPort))
            {
                client.Connect();
                return client.QueryStatus().ToString();
            }
        }
    }
}
