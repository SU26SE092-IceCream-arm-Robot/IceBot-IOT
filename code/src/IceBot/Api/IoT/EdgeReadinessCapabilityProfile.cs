using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using IceBot.Robot;

namespace IceBot.Api

{
internal static class EdgeReadinessCapabilityProfile
{
    internal static IReadOnlyList<EdgeReadinessCapability> For(RobotExecutionMode executionMode, string safety = "Unknown")
    {
        return executionMode == RobotExecutionMode.Simulated ||
               (executionMode == RobotExecutionMode.Fairino && string.Equals(safety, "Safe", StringComparison.Ordinal))
            ? new[] { new EdgeReadinessCapability("ROBOT_ARM", "ARM_PRIMARY", true, null) }
            : Array.Empty<EdgeReadinessCapability>();
    }
}

internal sealed class EdgeReadinessCapability
{
    internal EdgeReadinessCapability(string capabilityCode, string workcellCode, bool isAvailable, string? unavailableReason)
    {
        CapabilityCode = capabilityCode;
        WorkcellCode = workcellCode;
        IsAvailable = isAvailable;
        UnavailableReason = unavailableReason;
    }

    [JsonPropertyName("capabilityCode")]
    public string CapabilityCode { get; private set; }

    [JsonPropertyName("workcellCode")]
    public string WorkcellCode { get; private set; }

    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; private set; }

    [JsonPropertyName("unavailableReason")]
    public string? UnavailableReason { get; private set; }
}
}
