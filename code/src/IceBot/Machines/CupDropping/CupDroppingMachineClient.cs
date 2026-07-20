using System;
using System.IO.Ports;
using IceBot.Machines;

namespace IceBot.Machines.CupDropping
{
    // Host-side client for the "301 Cup-Dropping Machine Serial Communication Protocol V0.0.3".
    // IceBot (PC) is the host; the cup-dropping machine mainboard is the slave.
    internal sealed class CupDroppingMachineClient : IDisposable
    {
        private const int BaudRate = 115200;
        private const int ResponseTimeoutMs = 1000;
        private const int MaxResends = 3;

        private readonly SerialPort _port;
        private bool _connected;

        public CupDroppingMachineClient(string comPort)
        {
            _port = new SerialPort(comPort, BaudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = ResponseTimeoutMs,
                WriteTimeout = ResponseTimeoutMs,
            };
        }

        public void Connect()
        {
            _port.Open();
            _connected = true;
        }

        public CupMachineStatus QueryStatus()
        {
            EnsureConnected();
            var request = SerialFrameCodec.Build(0x01, SerialFrameCodec.InstructionQuery);
            var response = SendWithRetry(request);

            var data1 = response[3];
            var data2 = response[4];
            return new CupMachineStatus
            {
                NoCup = (data1 & 0x01) != 0,
                CupNotTaken = (data1 & 0x02) != 0,
                DrawerOpen = (data1 & 0x04) != 0,
                MotorFault = (data1 & 0x08) != 0,
                ArmInPlace = (data1 & 0x10) != 0,
                SystemState = (CupMachineState)data2,
            };
        }

        // Beverage Number = 1 ("Drop one cup"); Data1 reserved = 0x00.
        public bool DispenseCup()
        {
            EnsureConnected();
            var request = SerialFrameCodec.Build(0x04, SerialFrameCodec.InstructionSet, 0x01, 0x00);
            var response = SendWithRetry(request);
            return response[3] == 0x01;
        }

        public bool Shutdown()
        {
            EnsureConnected();
            var request = SerialFrameCodec.Build(0x03, SerialFrameCodec.InstructionSet);
            var response = SendWithRetry(request);
            return response[3] == 0x01;
        }

        private void EnsureConnected()
        {
            if (!_connected)
            {
                throw new InvalidOperationException("Cup-dropping machine is not connected.");
            }
        }

        // Doc: resend if no reply within 1s; after 3 resends with no reply, it's a communication error.
        private byte[] SendWithRetry(byte[] request)
        {
            Exception? lastError = null;
            for (var attempt = 0; attempt <= MaxResends; attempt++)
            {
                try
                {
                    _port.DiscardInBuffer();
                    _port.Write(request, 0, request.Length);
                    return ReadResponse();
                }
                catch (TimeoutException ex)
                {
                    lastError = ex;
                }
            }

            throw new InvalidOperationException(
                $"Cup-dropping machine communication error: no valid reply after {MaxResends} resend(s).", lastError);
        }

        private byte[] ReadResponse()
        {
            var header = ReadExact(2); // CommandCode, LengthCode
            var length = header[1];
            if (length < 5)
            {
                throw new InvalidOperationException($"Invalid length code {length} from cup-dropping machine.");
            }

            var rest = ReadExact(length - 2);
            var frame = new byte[length];
            Array.Copy(header, frame, 2);
            Array.Copy(rest, 0, frame, 2, rest.Length);

            if (!SerialFrameCodec.TryValidate(frame, out var error))
            {
                throw new InvalidOperationException($"Invalid response from cup-dropping machine: {error}");
            }

            return frame;
        }

        private byte[] ReadExact(int count)
        {
            var buffer = new byte[count];
            var offset = 0;
            while (offset < count)
            {
                offset += _port.Read(buffer, offset, count - offset);
            }

            return buffer;
        }

        public void Dispose()
        {
            if (!_connected)
            {
                return;
            }

            if (_port.IsOpen)
            {
                _port.Close();
            }

            _port.Dispose();
            _connected = false;
        }
    }
}
