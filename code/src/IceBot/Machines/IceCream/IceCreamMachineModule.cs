using System;
using System.Collections.Generic;

namespace IceBot.Machines.IceCream
{
    // Plugs the ice cream dispenser (custom STM32 board) into the system. This is the whole
    // "module" for this machine: which steps trigger it, and what signal to send. See
    // IceCreamMachineClient for the actual serial protocol.
    internal sealed class IceCreamMachineModule : IMachineTrigger, IMachineDiagnostics
    {
        public string MachineType => "ice_cream";

        public string DisplayName => "May lam kem";

        // Add one entry per flavor/size step file as they're authored; all of them dispense
        // through this same physical machine.
        public IReadOnlyCollection<string> StepNames { get; } = new[] { "ice_chocolate_s" };

        public void Trigger(string comPort)
        {
            using (var client = new IceCreamMachineClient(comPort))
            {
                client.Connect();
                Console.WriteLine($"[MACHINE] {DisplayName} @ {comPort}: dispensing ice cream (motor up)...");
                var ok = client.RunUp();
                Console.WriteLine(ok ? "[MACHINE] Dispense OK." : "[MACHINE] Dispense FAILED.");
                if (!ok)
                {
                    throw new InvalidOperationException("May lam kem bao loi (setting failed).");
                }
            }
        }

        public string GetStatusText(string comPort)
        {
            using (var client = new IceCreamMachineClient(comPort))
            {
                client.Connect();
                return client.QueryStatus().ToString();
            }
        }

        public void TestConnection(string comPort)
        {
            using (var client = new IceCreamMachineClient(comPort))
            {
                client.Connect();
                client.QueryStatus();
            }
        }
    }
}
