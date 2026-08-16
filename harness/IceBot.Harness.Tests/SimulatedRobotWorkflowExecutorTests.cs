using System;
using System.IO;
using IceBot.Config;
using IceBot.Robot;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class SimulatedRobotWorkflowExecutorTests
    {
        [Fact]
        public void Simulator_RunsOpaqueExistingLuaWithoutContactingRobot()
        {
            var path = CreateLuaFile();
            try
            {
                using (var executor = new SimulatedRobotWorkflowExecutor())
                {
                    executor.Connect();
                    executor.MoveToTeachingPoint("robot_home");
                    executor.RunScript(path);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Factory_UsesSimulatorOnlyWhenExplicitlyConfigured()
        {
            var original = Environment.GetEnvironmentVariable("ICEBOT_ROBOT_EXECUTION_MODE");
            try
            {
                Environment.SetEnvironmentVariable("ICEBOT_ROBOT_EXECUTION_MODE", "Simulated");
                Assert.IsType<SimulatedRobotWorkflowExecutor>(RobotWorkflowExecutorFactory.Create("192.168.58.2"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("ICEBOT_ROBOT_EXECUTION_MODE", original);
            }
        }

        [Fact]
        public void Simulator_FailsAtConfiguredStep()
        {
            var originalMode = Environment.GetEnvironmentVariable("ICEBOT_ROBOT_EXECUTION_MODE");
            var originalStep = Environment.GetEnvironmentVariable("ICEBOT_SIMULATED_FAIL_STEP");
            var path = CreateLuaFile();
            try
            {
                Environment.SetEnvironmentVariable("ICEBOT_ROBOT_EXECUTION_MODE", "Simulated");
                Environment.SetEnvironmentVariable("ICEBOT_SIMULATED_FAIL_STEP", "1");
                using (var executor = new SimulatedRobotWorkflowExecutor())
                {
                    executor.Connect();
                    Assert.Throws<InvalidOperationException>(() => executor.RunScript(path));
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("ICEBOT_ROBOT_EXECUTION_MODE", originalMode);
                Environment.SetEnvironmentVariable("ICEBOT_SIMULATED_FAIL_STEP", originalStep);
                File.Delete(path);
            }
        }

        private static string CreateLuaFile()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".lua");
            File.WriteAllText(path, "-- opaque test Lua\n");
            return path;
        }
    }
}
