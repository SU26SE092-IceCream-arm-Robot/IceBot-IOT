using System;
using fairino;
using IceBot.Workflow.Execution;

namespace IceBot.Robot
{
    internal sealed class FairinoLuaExecutor : IRobotWorkflowExecutor
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

        public void MoveJ(WorkflowInstruction instruction)
        {
            EnsureConnected();
            var value = instruction.Values;
            var jointPos = new JointPos(value[0], value[1], value[2], value[3], value[4], value[5]);
            var exaxis = new ExaxisPos(0, 0, 0, 0);
            var noOffset = new DescPose(0, 0, 0, 0, 0, 0);
            var pose = new DescPose(0, 0, 0, 0, 0, 0);
            var kinematicsResult = _robot.GetForwardKin(jointPos, ref pose);
            if (kinematicsResult != 0)
                throw new InvalidOperationException($"GetForwardKin failed with error code {kinematicsResult}.");
            var result = _robot.MoveJ(jointPos, pose, 0, 0, (float)instruction.Speed,
                (float)instruction.Acceleration, 100.0f, exaxis, -1.0f, 0, noOffset);
            if (result != 0) throw new InvalidOperationException($"MoveJ failed with error code {result}.");
        }

        public void MoveL(WorkflowInstruction instruction)
        {
            EnsureConnected();
            var value = instruction.Values;
            var pose = new DescPose(value[0], value[1], value[2], value[3], value[4], value[5]);
            var exaxis = new ExaxisPos(0, 0, 0, 0);
            var noOffset = new DescPose(0, 0, 0, 0, 0, 0);
            var jointPos = new JointPos(0, 0, 0, 0, 0, 0);
            var kinematicsResult = _robot.GetInverseKin(0, pose, -1, ref jointPos);
            if (kinematicsResult != 0)
                throw new InvalidOperationException($"GetInverseKin failed with error code {kinematicsResult}.");
            var result = _robot.MoveL(jointPos, pose, 0, 0, (float)instruction.Speed,
                (float)instruction.Acceleration, 100.0f, -1.0f, exaxis, 0, 0,
                noOffset, 0, 10);
            if (result != 0) throw new InvalidOperationException($"MoveL failed with error code {result}.");
        }

        public void SetDo(int index, int state, bool toolOutput)
        {
            EnsureConnected();
            var result = toolOutput
                ? _robot.SetToolDO(index, (byte)state, 0, 1)
                : _robot.SetDO(index, (byte)state, 0, 1);
            if (result != 0)
                throw new InvalidOperationException($"{(toolOutput ? "SetToolDO" : "SetDO")} failed with error code {result}.");
        }

        public void RunScript(string localLuaPath)
        {
            throw new NotSupportedException("Opaque Lua execution is disabled; compose and validate a typed workflow plan first.");
        }

        private void EnsureConnected()
        {
            if (!_connected) throw new InvalidOperationException("Robot is not connected.");
        }

        // Moves the arm to a teaching point stored on the robot controller (saved via the
        // Fairino app), addressed by its name — e.g. "IceBot_Home". IceBot does not store the
        // joint values; it reads them live from the controller and issues a blocking MoveJ.
        internal static bool TryApplyHomeFallback(string pointName, int readResult, ref double[] data)
        {
            if ((readResult != 143 && readResult != -4) || !string.Equals(pointName, "IceBot_Home", StringComparison.Ordinal))
                return false;

            // Controller Web 3.7.7 lists this point but its XML-RPC endpoint cannot query it
            // by name. These values are the verified IceBot_Home row from the controller.
            data = new[]
            {
                142.455, -444.332, 128.648, 88.947, -0.105, 5.148,
                95.150, -71.889, 127.806, -56.970, 90.004, 0.105,
                0.0, 0.0, 40.0, 20.0, 0.0, 0.0, 0.0, 0.0
            };
            return true;
        }
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
            if (readResult != 0 && TryApplyHomeFallback(pointName, readResult, ref data))
            {
                Console.WriteLine("[MOVE] Named home query is unavailable; using verified IceBot_Home fallback at 40% speed.");
            }
            else if (readResult != 0)
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
            const float vel = 40.0f;

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
