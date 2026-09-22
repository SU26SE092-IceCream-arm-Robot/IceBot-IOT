using System;
using System.IO;

namespace IceBot.Machines
{
    internal static class MachineDriverDirectory
    {
        public static string Resolve()
        {
            // Keep each Edge installation self-contained. In development this resolves
            // beside the executable in code/src/IceBot/bin/<Configuration>/net472;
            // the project copies the tracked DRIVER-DLL packages there at build time.
            return Path.Combine(AppContext.BaseDirectory, "drivers");
        }
    }
}
