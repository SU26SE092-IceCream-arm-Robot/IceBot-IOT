using System;
using System.IO;
using System.Threading;
using IceBot.Config;

namespace IceBot.Robot
{
    internal enum RobotExecutionMode
    {
        Fairino,
        Simulated
    }

    internal interface IRobotWorkflowExecutor : IDisposable
    {
        void Connect();
        void MoveToTeachingPoint(string pointName);
        void RunScript(string localLuaPath);
    }

    internal static class RobotWorkflowExecutorFactory
    {
        public static IRobotWorkflowExecutor Create(string robotIp) =>
            AppConfig.RobotExecutionMode == RobotExecutionMode.Simulated
                ? new SimulatedRobotWorkflowExecutor()
                : new FairinoLuaExecutor(robotIp);
    }

    // The simulator deliberately treats Lua as opaque. It exercises Edge durability,
    // ordering, reporting, and recovery without claiming any physical validation.
    internal sealed class SimulatedRobotWorkflowExecutor : IRobotWorkflowExecutor
    {
        private int _stepNo;
        private bool _connected;

        public void Connect()
        {
            _connected = true;
            Console.WriteLine("[SIMULATOR] Robot execution is simulated; no Fairino RPC connection is opened.");
        }

        public void MoveToTeachingPoint(string pointName)
        {
            EnsureConnected();
            Console.WriteLine($"[SIMULATOR] Teaching point '{pointName}' accepted.");
        }

        public void RunScript(string localLuaPath)
        {
            EnsureConnected();
            if (!File.Exists(localLuaPath))
                throw new FileNotFoundException("Lua script not found.", localLuaPath);
            if (new FileInfo(localLuaPath).Length == 0)
                throw new InvalidDataException("Lua script is empty.");

            _stepNo++;
            Console.WriteLine($"[SIMULATOR] Running opaque Lua step {_stepNo}: {Path.GetFileName(localLuaPath)}.");
            if (AppConfig.SimulatedStepDelayMilliseconds > 0)
                Thread.Sleep(AppConfig.SimulatedStepDelayMilliseconds);
            if (AppConfig.SimulatedFailStep == _stepNo)
                throw new InvalidOperationException($"Simulated failure at Lua step {_stepNo}.");
        }

        public void Dispose() => _connected = false;

        private void EnsureConnected()
        {
            if (!_connected)
                throw new InvalidOperationException("Simulator is not connected.");
        }
    }
}
