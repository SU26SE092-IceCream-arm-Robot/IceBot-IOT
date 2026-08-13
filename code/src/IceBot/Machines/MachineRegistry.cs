using System;
using System.Collections.Generic;
using System.IO;

namespace IceBot.Machines
{
    // Plugin-only registry: every peripheral driver comes from
    // %ProgramData%/IceBot/drivers/*/driver.json in both development and production.
    // An empty drivers directory intentionally produces an empty machine list.
    internal static class MachineRegistry
    {
        private static readonly MachinePluginLoadResult PluginResult = MachinePluginLoader.Load(
            MachineDriverDirectory.Resolve());

        public static readonly IReadOnlyList<IMachineModule> Modules = BuildModules();
        public static IReadOnlyList<string> PluginErrors => PluginResult.Errors;

        private static readonly Dictionary<string, IMachineModule> ByStepName = BuildStepIndex();

        public static bool TryGetModule(string stepFileName, out IMachineModule module)
        {
            var key = Path.GetFileNameWithoutExtension(stepFileName);
            return ByStepName.TryGetValue(key, out module!);
        }

        private static IReadOnlyList<IMachineModule> BuildModules()
        {
            var map = new Dictionary<string, IMachineModule>(StringComparer.OrdinalIgnoreCase);
            foreach (var plugin in PluginResult.Modules)
            {
                MachinePluginLoader.ValidateModule(plugin);
                map[plugin.MachineType] = plugin;
            }
            return new List<IMachineModule>(map.Values);
        }

        private static Dictionary<string, IMachineModule> BuildStepIndex()
        {
            var map = new Dictionary<string, IMachineModule>(StringComparer.OrdinalIgnoreCase);
            foreach (var module in Modules)
            {
                foreach (var step in module.StepNames)
                {
                    if (map.ContainsKey(step))
                        throw new InvalidOperationException($"StepName '{step}' bi trung giua cac driver may.");
                    map[step] = module;
                }
            }
            return map;
        }
    }
}
