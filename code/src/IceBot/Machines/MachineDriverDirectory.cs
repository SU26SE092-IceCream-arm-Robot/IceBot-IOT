using System;
using System.IO;

namespace IceBot.Machines
{
    internal static class MachineDriverDirectory
    {
        public static string Resolve()
        {
            var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrWhiteSpace(commonData))
                throw new InvalidOperationException("Khong xac dinh duoc thu muc ProgramData cua Windows.");

            return Path.Combine(commonData, "IceBot", "drivers");
        }
    }
}
