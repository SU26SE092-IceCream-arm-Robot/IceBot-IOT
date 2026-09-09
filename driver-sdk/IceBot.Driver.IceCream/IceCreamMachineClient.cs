using System;
using System.IO.Ports;

namespace IceBot.Driver.IceCream
{
    internal sealed class IceCreamMachineClient : IIceCreamMachineClient
    {
        private const int BaudRate = 115200;
        private const int ResponseTimeoutMs = 1000;
        private const int MaxResends = 3;

        private readonly SerialPort _port;
        private bool _connected;

        public IceCreamMachineClient(string comPort)
        {
            if (string.IsNullOrWhiteSpace(comPort)) throw new ArgumentException("COM port is required.", nameof(comPort));
            _port = new SerialPort(comPort, BaudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = ResponseTimeoutMs,
                WriteTimeout = ResponseTimeoutMs,
                Handshake = Handshake.None,
                DtrEnable = false,
                RtsEnable = false,
            };
        }

        public void Connect()
        {
            _port.Open();
            _connected = true;
        }

        public IceCreamMachineStatus QueryStatus()
        {
            var response = SendWithRetry(
                SerialFrameCodec.Build(0x01, SerialFrameCodec.InstructionQuery),
                expectedCommand: 0x01,
                expectedInstruction: SerialFrameCodec.InstructionQuery,
                expectedLength: 7);
            var status = response[3];
            return new IceCreamMachineStatus
            {
                LowStock = (status & 0x01) != 0,
                OutOfStock = (status & 0x02) != 0,
                MotorFault = (status & 0x04) != 0,
                Busy = (status & 0x08) != 0,
                SystemState = (IceCreamMachineState)response[4],
            };
        }

        public bool RunUp(byte speedPercent, byte durationTenths) =>
            RunMotor(0x02, speedPercent, durationTenths);

        public bool RunDown(byte speedPercent, byte durationTenths) =>
            RunMotor(0x03, speedPercent, durationTenths);

        public bool Stop()
        {
            var response = SendWithRetry(
                SerialFrameCodec.Build(0x04, SerialFrameCodec.InstructionSet),
                expectedCommand: 0x04,
                expectedInstruction: SerialFrameCodec.InstructionSet,
                expectedLength: 6);
            return response[3] == 0x01;
        }

        private bool RunMotor(byte command, byte speedPercent, byte durationTenths)
        {
            if (speedPercent > 100) throw new ArgumentOutOfRangeException(nameof(speedPercent));
            var response = SendWithRetry(
                SerialFrameCodec.Build(command, SerialFrameCodec.InstructionSet, speedPercent, durationTenths),
                expectedCommand: command,
                expectedInstruction: SerialFrameCodec.InstructionSet,
                expectedLength: 6);
            return response[3] == 0x01;
        }

        private byte[] SendWithRetry(byte[] request, byte expectedCommand, byte expectedInstruction, int expectedLength)
        {
            EnsureConnected();
            Exception? lastError = null;
            for (var attempt = 0; attempt <= MaxResends; attempt++)
            {
                try
                {
                    _port.DiscardInBuffer();
                    _port.Write(request, 0, request.Length);
                    return ReadResponse(expectedCommand, expectedInstruction, expectedLength);
                }
                catch (TimeoutException ex)
                {
                    lastError = ex;
                }
            }
            throw new InvalidOperationException(
                $"Ice-cream machine communication error: no valid reply after {MaxResends} resend(s).", lastError);
        }

        private byte[] ReadResponse(byte expectedCommand, byte expectedInstruction, int expectedLength)
        {
            var header = ReadExact(2);
            var length = header[1];
            if (length < 5 || length > 32)
                throw new InvalidOperationException($"Invalid length code {length} from ice-cream machine.");

            var rest = ReadExact(length - 2);
            var frame = new byte[length];
            Array.Copy(header, frame, 2);
            Array.Copy(rest, 0, frame, 2, rest.Length);
            if (!SerialFrameCodec.TryValidate(frame, out var error))
                throw new InvalidOperationException($"Invalid response from ice-cream machine: {error}");
            if (frame.Length != expectedLength || frame[0] != expectedCommand || frame[2] != expectedInstruction)
                throw new InvalidOperationException("Unexpected response from ice-cream machine.");
            return frame;
        }

        private byte[] ReadExact(int count)
        {
            var buffer = new byte[count];
            var offset = 0;
            while (offset < count) offset += _port.Read(buffer, offset, count - offset);
            return buffer;
        }

        private void EnsureConnected()
        {
            if (!_connected) throw new InvalidOperationException("Ice-cream machine is not connected.");
        }

        public void Dispose()
        {
            if (_port.IsOpen) _port.Close();
            _port.Dispose();
            _connected = false;
        }
    }
}