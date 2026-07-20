using System;
using System.Collections.Generic;
using System.IO;

namespace IceBot.Machines
{
    // Maps a workflow step (the .lua step file name) to a peripheral machine wired
    // directly to this PC over a serial port. The step's .lua file always runs first
    // (arm moves through it top-to-bottom); once it finishes, IceBot sends this machine's
    // own signal/command over serial — the arm is already in position by then. Steps not
    // listed here are plain arm-motion steps with no follow-up peripheral trigger.
    internal static class MachineRegistry
    {
        public const string CupDropping = "cup_dropping";

        private static readonly Dictionary<string, string> StepToMachineType =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "cup_s", CupDropping },
            };

        public static bool TryGetMachineType(string stepFileName, out string machineType)
        {
            var key = Path.GetFileNameWithoutExtension(stepFileName);
            return StepToMachineType.TryGetValue(key, out machineType!);
        }
    }
}
