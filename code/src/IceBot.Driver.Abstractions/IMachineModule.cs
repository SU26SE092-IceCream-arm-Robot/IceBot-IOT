using System.Collections.Generic;

namespace IceBot.Machines
{
    /// <summary>Stable contract implemented by every built-in or external machine driver.</summary>
    public interface IMachineModule
    {
        string MachineType { get; }
        string DisplayName { get; }
        IReadOnlyCollection<string> StepNames { get; }
    }
}
