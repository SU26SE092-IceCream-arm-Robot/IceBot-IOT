using System;
using System.Collections.Generic;
using IceBot.Driver.IceCream;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class IceCreamDriverTests
    {
        [Fact]
        public void SerialFrameCodec_BuildsVerifiedControllerFrames()
        {
            Assert.Equal(new byte[] { 0x01, 0x05, 0x55, 0x5B, 0xFF },
                SerialFrameCodec.Build(0x01, SerialFrameCodec.InstructionQuery));
            Assert.Equal(new byte[] { 0x02, 0x07, 0xAA, 0x14, 0x03, 0xCA, 0xFF },
                SerialFrameCodec.Build(0x02, SerialFrameCodec.InstructionSet, 20, 3));
            Assert.Equal(new byte[] { 0x03, 0x07, 0xAA, 0x14, 0x01, 0xC9, 0xFF },
                SerialFrameCodec.Build(0x03, SerialFrameCodec.InstructionSet, 20, 1));
            Assert.Equal(new byte[] { 0x04, 0x05, 0xAA, 0xB3, 0xFF },
                SerialFrameCodec.Build(0x04, SerialFrameCodec.InstructionSet));

            // Duration is encoded in tenths of a second: 1.2s -> 12, 1.6s -> 16.
            Assert.Equal(new byte[] { 0x02, 0x07, 0xAA, 0x14, 0x0C, 0xD3, 0xFF },
                SerialFrameCodec.Build(0x02, SerialFrameCodec.InstructionSet, 20, 12));
            Assert.Equal(new byte[] { 0x03, 0x07, 0xAA, 0x14, 0x10, 0xD8, 0xFF },
                SerialFrameCodec.Build(0x03, SerialFrameCodec.InstructionSet, 20, 16));
        }

        [Fact]
        public void SerialFrameCodec_RejectsCorruptChecksum()
        {
            var frame = new byte[] { 0x01, 0x07, 0x55, 0x00, 0x00, 0x00, 0xFF };
            Assert.False(SerialFrameCodec.TryValidate(frame, out var error));
            Assert.Contains("Checksum", error);
        }

        [Fact]
        public void Driver_ExposesCanonicalMachineIdentity()
        {
            var driver = new IceCreamDriver(_ => new FakeClient(), _ => { });
            Assert.Equal("ice_cream", driver.MachineType);
            Assert.Contains("ice_cream", driver.StepNames);
        }

        [Fact]
        public void Trigger_UpRunsUntilUpperLimitOnly()
        {
            var client = new FakeClient();
            client.Statuses.Enqueue(Standby());
            client.Statuses.Enqueue(Standby());
            client.Statuses.Enqueue(Standby());
            client.Statuses.Enqueue(Standby());
            var delays = new List<int>();
            var driver = new IceCreamDriver(port =>
            {
                Assert.Equal("COM7", port);
                return client;
            }, delays.Add);

            driver.Trigger("COM7", "UP");

            Assert.Equal(new[]
            {
                "Connect", "QueryStatus", "RunUp:20:0", "QueryStatus", "QueryStatus", "Dispose"
            }, client.Calls);
            Assert.Empty(delays);
        }

        [Fact]
        public void Trigger_DownRunsUntilLowerLimitOnly()
        {
            var client = new FakeClient();
            client.Statuses.Enqueue(Standby());
            client.Statuses.Enqueue(Standby());
            client.Statuses.Enqueue(Standby());
            var driver = new IceCreamDriver(_ => client, _ => { });

            driver.Trigger("COM7", "DOWN");

            Assert.Equal(new[]
            {
                "Connect", "QueryStatus", "RunDown:20:0", "QueryStatus", "QueryStatus", "Dispose"
            }, client.Calls);
        }

        [Fact]
        public void Trigger_RejectsFaultBeforeMotorMotion()
        {
            var client = new FakeClient();
            client.Statuses.Enqueue(new IceCreamMachineStatus
            {
                MotorFault = true,
                SystemState = IceCreamMachineState.Fault,
            });
            var driver = new IceCreamDriver(_ => client, _ => { });

            var error = Assert.Throws<InvalidOperationException>(() => driver.Trigger("COM7", "UP"));

            Assert.Contains("bao loi", error.Message);
            Assert.Equal(new[] { "Connect", "QueryStatus", "Dispose" }, client.Calls);
        }

        [Fact]
        public void Trigger_RejectedUpAttemptsEmergencyStop()
        {
            var client = new FakeClient { RunUpAccepted = false };
            client.Statuses.Enqueue(Standby());
            var driver = new IceCreamDriver(_ => client, _ => { });

            var error = Assert.Throws<InvalidOperationException>(() => driver.Trigger("COM7", "UP"));

            Assert.Contains("UP", error.Message);
            Assert.Equal(new[] { "Connect", "QueryStatus", "RunUp:20:0", "Stop", "Dispose" }, client.Calls);
        }

        private static IceCreamMachineStatus Standby() => new IceCreamMachineStatus
        {
            SystemState = IceCreamMachineState.Standby,
        };

        private sealed class FakeClient : IIceCreamMachineClient
        {
            public Queue<IceCreamMachineStatus> Statuses { get; } = new Queue<IceCreamMachineStatus>();
            public List<string> Calls { get; } = new List<string>();
            public bool RunUpAccepted { get; set; } = true;
            public bool RunDownAccepted { get; set; } = true;
            public bool StopAccepted { get; set; } = true;

            public void Connect() => Calls.Add("Connect");
            public IceCreamMachineStatus QueryStatus()
            {
                Calls.Add("QueryStatus");
                return Statuses.Dequeue();
            }
            public bool RunUp(byte speedPercent, byte durationSeconds)
            {
                Calls.Add($"RunUp:{speedPercent}:{durationSeconds}");
                return RunUpAccepted;
            }
            public bool RunDown(byte speedPercent, byte durationSeconds)
            {
                Calls.Add($"RunDown:{speedPercent}:{durationSeconds}");
                return RunDownAccepted;
            }
            public bool Stop()
            {
                Calls.Add("Stop");
                return StopAccepted;
            }
            public void Dispose() => Calls.Add("Dispose");
        }
    }
}