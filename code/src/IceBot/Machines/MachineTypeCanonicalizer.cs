using System;
using System.Collections.Generic;

namespace IceBot.Machines
{
    internal static class MachineTypeCanonicalizer
    {
        private static readonly IReadOnlyDictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["icemachine"] = "ice_cream",
                ["ice_cream"] = "ice_cream",
                ["ice_cream_machine"] = "ice_cream",
                ["ice_cream_machine_1"] = "ice_cream",
                ["ice_cream_machine_2"] = "ice_cream",
                ["bt_cup_l90"] = "bt_cup_l90",
                ["cup_droping_machine"] = "bt_cup_l90",
                ["cup_droping_machine_1"] = "bt_cup_l90",
                ["cup_droping_machine_2"] = "bt_cup_l90"
            };

        public static string Canonicalize(string machineType)
        {
            var normalized = (machineType ?? string.Empty).Trim();
            return Aliases.TryGetValue(normalized, out var canonical) ? canonical : normalized;
        }
    }
}
