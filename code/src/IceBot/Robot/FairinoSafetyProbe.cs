using System;
using System.Threading;
using fairino;

namespace IceBot.Robot
{
    internal static class FairinoSafetyProbe
    {
        internal static FairinoSafetySnapshot Read(string robotIp)
        {
            var robot = new fairino.Robot();
            try
            {
                var connectResult = robot.RPC(robotIp);
                if (connectResult != 0)
                    return FairinoSafetySnapshot.Unknown($"RPC failed ({connectResult}).");

                // The SDK fills its realtime package on a background receiver. Do not
                // accept the zero-initialized package immediately after RPC connects.
                Thread.Sleep(500);

                var communicationState = -1;
                var communicationResult = robot.GetSDKComState(ref communicationState);
                var emergencyStop = (byte)255;
                var emergencyResult = robot.GetRobotEmergencyStopState(ref emergencyStop);
                var safetyStop0 = (byte)255;
                var safetyStop1 = (byte)255;
                var safetyStopResult = robot.GetSafetyStopState(ref safetyStop0, ref safetyStop1);
                var mainError = int.MinValue;
                var subError = int.MinValue;
                var errorResult = robot.GetRobotErrorCode(ref mainError, ref subError);

                if (communicationResult != 0 || communicationState != 0 ||
                    emergencyResult != 0 || safetyStopResult != 0 || errorResult != 0)
                {
                    return FairinoSafetySnapshot.Unknown(
                        $"telemetry read failed (sdk={communicationResult}/{communicationState}, emergency={emergencyResult}, safety={safetyStopResult}, error={errorResult}).");
                }

                if (emergencyStop != 0 || safetyStop0 != 0 || safetyStop1 != 0 || mainError != 0 || subError != 0)
                {
                    return FairinoSafetySnapshot.Unsafe(
                        $"emergency={emergencyStop}, safetyStop0={safetyStop0}, safetyStop1={safetyStop1}, robotError={mainError}/{subError}.");
                }

                return FairinoSafetySnapshot.Safe();
            }
            catch (Exception ex)
            {
                return FairinoSafetySnapshot.Unknown(ex.Message);
            }
            finally
            {
                try { robot.CloseRPC(); }
                catch { /* A failed probe must never hide its original result. */ }
            }
        }
    }

    internal sealed class FairinoSafetySnapshot
    {
        private FairinoSafetySnapshot(string safety, string detail)
        {
            Safety = safety;
            Detail = detail;
        }

        internal string Safety { get; }
        internal string Detail { get; }

        internal static FairinoSafetySnapshot Safe() => new FairinoSafetySnapshot("Safe", "Fairino telemetry is healthy.");
        internal static FairinoSafetySnapshot Unsafe(string detail) => new FairinoSafetySnapshot("Unsafe", detail);
        internal static FairinoSafetySnapshot Unknown(string detail) => new FairinoSafetySnapshot("Unknown", detail);
    }
}
