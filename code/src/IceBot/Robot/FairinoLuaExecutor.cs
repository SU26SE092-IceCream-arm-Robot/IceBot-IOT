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

        // Moves the arm to a teaching point stored on the robot controller (saved via the
        // Fairino app), addressed by its name — e.g. "robot_home". IceBot does not store the
        // joint values; it reads them live from the controller and issues a blocking MoveJ.
        public void MoveToTeachingPoint(string pointName)
        {
            if (!_connected)
            {
                throw new InvalidOperationException("Robot is not connected.");
            }

            Console.WriteLine($"[MOVE] Reading teaching point '{pointName}'...");
            // Layout per SDK: {x,y,z,rx,ry,rz, j1..j6, tool, wobj, speed, acc, e1..e4}
            var data = new double[20];
            int readResult = _robot.GetRobotTeachingPoint(pointName, ref data);
            if (readResult != 0)
            {
                throw new InvalidOperationException(
                    $"GetRobotTeachingPoint('{pointName}') failed ({readResult}). Check the point name saved on the robot.");
            }

            var jointPos = new JointPos(data[6], data[7], data[8], data[9], data[10], data[11]);
            var descPose = new DescPose(data[0], data[1], data[2], data[3], data[4], data[5]);
            var exaxis = new ExaxisPos(data[16], data[17], data[18], data[19]);
            var noOffset = new DescPose(0, 0, 0, 0, 0, 0);
            var tool = (int)data[12];
            var user = (int)data[13];
            var vel = (float)(data[14] > 0 ? data[14] : 30.0);

            int modeResult = _robot.Mode(0);
            if (modeResult != 0)
            {
                throw new InvalidOperationException($"Mode(0) failed with error code {modeResult}.");
            }

            int enableResult = _robot.RobotEnable(1);
            if (enableResult != 0)
            {
                throw new InvalidOperationException($"RobotEnable(1) failed with error code {enableResult}.");
            }

            Console.WriteLine($"[MOVE] Moving to '{pointName}'...");
            // blendT = -1.0 → blocking: returns once the arm is in place.
            int moveResult = _robot.MoveJ(jointPos, descPose, tool, user, vel, 100.0f, 100.0f, exaxis, -1.0f, 0, noOffset);
            if (moveResult != 0)
            {
                throw new InvalidOperationException($"MoveJ to '{pointName}' failed with error code {moveResult}.");
            }

            Console.WriteLine($"[MOVE] Reached '{pointName}'.");
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
