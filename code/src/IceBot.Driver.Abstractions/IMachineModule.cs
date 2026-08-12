using System.Collections.Generic;

namespace IceBot.Machines
{
    /// <summary>Stable contract implemented by every external machine driver DLL.</summary>
    public interface IMachineModule
    {
        string MachineType { get; }
        string DisplayName { get; }
        IReadOnlyCollection<string> StepNames { get; }
    }
}
