using System.Collections.Generic;
using System.Linq;
using IceBot.Machines;

namespace IceBot.Workflow
{
    // Orders an unordered set of workflow steps (e.g. everything one order needs: cup +
    // flavor + topping + tray) into the physical sequence the arm should run them in, so
    // WorkflowRunner can merge/chain them into one continuous workflow. Order is driven by
    // each machine's IMachineModule.Position (lower = earlier on the line).
    //
    // Every step is expected to belong to a registered machine (see MachineRegistry) — a step
    // that resolves to no machine sorts after everything else, keeping its relative input
    // order, as a defensive fallback only. It signals a missing MachineRegistry registration,
    // not a supported case.
    internal static class WorkflowQueueBuilder
    {
        public static IReadOnlyList<string> BuildQueue(IEnumerable<string> stepNames)
        {
            return stepNames
                .Select((name, index) => (name, index, position: ResolvePosition(name)))
                .OrderBy(step => step.position)
                .ThenBy(step => step.index)
                .Select(step => step.name)
                .ToList();
        }

        private static int ResolvePosition(string stepName)
        {
            return MachineRegistry.TryGetModule(stepName, out var module) ? module.Position : int.MaxValue;
        }
    }
}
