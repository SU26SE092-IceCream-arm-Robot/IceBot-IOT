using System;
using System.Collections.Generic;

namespace IceBot.Machines
{
    internal static class MachineTypeCanonicalizer
    {
        private static readonly IReadOnlyDictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["icemachine"] = "ice_cream"
            };

        public static string Canonicalize(string machineType)
        {
            var normalized = (machineType ?? string.Empty).Trim();
            return Aliases.TryGetValue(normalized, out var canonical) ? canonical : normalized;
        }
    }
}
