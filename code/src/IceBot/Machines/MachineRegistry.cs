using System;
using System.Collections.Generic;
using System.IO;
using IceBot.Machines.CupDropping;
using IceBot.Machines.IceCream;

namespace IceBot.Machines
{
    // Built-ins are defaults. A valid DLL plugin under drivers/*/driver.json replaces the
    // built-in with the same MachineType, without changing or rebuilding IceBot.exe.
    internal static class MachineRegistry
    {
        private static readonly IMachineModule[] BuiltInModules =
        {
            new CupDroppingMachineModule(),
            new IceCreamMachineModule(),
        };

        private static readonly MachinePluginLoadResult PluginResult = MachinePluginLoader.Load(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "drivers"));

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
            foreach (var module in BuiltInModules) map[module.MachineType] = module;
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
