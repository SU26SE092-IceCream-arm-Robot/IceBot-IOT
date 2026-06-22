using System;
using System.IO;
using System.Threading;
using fairino;

namespace IceBot.Robot
{
    internal sealed class FairinoLuaExecutor : IDisposable
    {
        private readonly fairino.Robot _robot;
        private readonly string _robotIp;
        private bool _connected;

        public FairinoLuaExecutor(string robotIp)
        {
            _robotIp = robotIp;
            _robot = new fairino.Robot();
        }

        public void Connect()
        {
            Console.WriteLine($"[CONNECT] Connecting to robot at {_robotIp}...");
            int result = _robot.RPC(_robotIp);
            if (result != 0)
            {
                throw new InvalidOperationException($"RPC failed with error code {result}. Check robot power, network, and IP.");
            }

            _connected = true;
            Console.WriteLine("[CONNECT] Connected.");
        }

        public void RunScript(string localLuaPath)
        {
            if (!_connected)
            {
                throw new InvalidOperationException("Robot is not connected.");
            }

            if (!File.Exists(localLuaPath))
            {
                throw new FileNotFoundException("Lua script not found.", localLuaPath);
            }

            var fileName = Path.GetFileName(localLuaPath);
            var controllerPath = $"/fruser/{fileName}";

            Console.WriteLine($"[SCRIPT] Uploading {fileName}...");
            string uploadError = string.Empty;
            int uploadResult = _robot.LuaUpload(localLuaPath, ref uploadError);
            if (uploadResult != 0)
            {
                throw new InvalidOperationException($"LuaUpload failed ({uploadResult}): {uploadError}");
            }

            Console.WriteLine($"[SCRIPT] Loading {controllerPath}...");
            int loadResult = _robot.Mode(0);
            if (loadResult != 0)
            {
                throw new InvalidOperationException($"Mode(0) failed with error code {loadResult}.");
            }

            loadResult = _robot.ProgramLoad(controllerPath);
            if (loadResult != 0)
            {
                throw new InvalidOperationException($"ProgramLoad failed with error code {loadResult}.");
            }

            Console.WriteLine("[SCRIPT] Running...");
            int runResult = _robot.ProgramRun();
            if (runResult != 0)
            {
                throw new InvalidOperationException($"ProgramRun failed with error code {runResult}.");
            }

            WaitUntilProgramFinished();
            Console.WriteLine($"[SCRIPT] Finished {fileName}.");
        }

        private void WaitUntilProgramFinished()
        {
            const int pollIntervalMs = 200;
            const int timeoutMs = 30 * 60 * 1000;
            var startedAt = Environment.TickCount;

            while (true)
            {
                byte motionDone = 0;
                int result = _robot.GetRobotMotionDone(ref motionDone);
                if (result != 0)
                {
                    throw new InvalidOperationException($"GetRobotMotionDone failed with error code {result}.");
                }

                if (motionDone != 0)
                {
                    return;
                }

                if (Environment.TickCount - startedAt > timeoutMs)
                {
                    throw new TimeoutException("Timed out waiting for robot program to finish.");
                }

                Thread.Sleep(pollIntervalMs);
            }
        }

        public void Dispose()
        {
            if (!_connected)
            {
                return;
            }

            Console.WriteLine("[CONNECT] Disconnecting...");
            _robot.CloseRPC();
            _connected = false;
        }
    }
}
