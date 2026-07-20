using System;
using System.Collections.Generic;
using System.IO;
using IceBot.Machines.CupDropping;

namespace IceBot.Machines
{
    // The one place to touch (besides writing the module itself) when adding a new machine:
    // add its module instance to Modules below. Everything else — WorkflowRunner triggering
    // it (if it implements IMachineTrigger), ConfigSetupWizard asking for its COM port,
    // WorkflowQueueBuilder ordering steps by its Position, the console test menu listing it —
    // reads from this registry automatically.
    //
    // Every workflow step (.lua file) belongs to exactly one machine identifier — there is no
    // such thing as a step with no machine. Register every machine here, not just the ones
    // that need a serial connection (see IMachineModule vs IMachineTrigger).
    internal static class MachineRegistry
    {
        public static readonly IReadOnlyList<IMachineModule> Modules = new IMachineModule[]
        {
            new CupDroppingMachineModule(),
        };

        private static readonly Dictionary<string, IMachineModule> ByStepName = BuildStepIndex();

        public static bool TryGetModule(string stepFileName, out IMachineModule module)
        {
            var key = Path.GetFileNameWithoutExtension(stepFileName);
            return ByStepName.TryGetValue(key, out module!);
        }

        private static Dictionary<string, IMachineModule> BuildStepIndex()
        {
            var map = new Dictionary<string, IMachineModule>(StringComparer.OrdinalIgnoreCase);
            foreach (var module in Modules)
            {
                foreach (var step in module.StepNames)
                {
                    map[step] = module;
                }
            }

            return map;
        }
    }
}
