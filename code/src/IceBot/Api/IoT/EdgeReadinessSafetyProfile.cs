using IceBot.Robot;

namespace IceBot.Api
{
    internal static class EdgeReadinessSafetyProfile
    {
        internal static string For(RobotExecutionMode executionMode, FairinoSafetySnapshot? physicalSafety = null)
        {
            if (executionMode == RobotExecutionMode.Simulated)
                return "Safe";

            return physicalSafety?.Safety ?? "Unknown";
        }
    }
}
