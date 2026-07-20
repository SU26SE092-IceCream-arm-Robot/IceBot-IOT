using System;

namespace IceBot.Machines
{
    // Frame format per "301 Cup-Dropping Machine Serial Communication Protocol":
    // CommandCode + LengthCode + InstructionCode + Data1..DataN + ChecksumCode + EndCode(0xFF)
    internal static class SerialFrameCodec
    {
        public const byte EndCode = 0xFF;
        public const byte InstructionQuery = 0x55;
        public const byte InstructionSet = 0xAA;

        public static byte[] Build(byte commandCode, byte instructionCode, params byte[] data)
        {
            var length = (byte)(5 + data.Length);
            var frame = new byte[length];
            frame[0] = commandCode;
            frame[1] = length;
            frame[2] = instructionCode;
            Array.Copy(data, 0, frame, 3, data.Length);
            frame[length - 2] = ComputeChecksum(frame, length - 2);
            frame[length - 1] = EndCode;
            return frame;
        }

        public static byte ComputeChecksum(byte[] frame, int countExcludingChecksumAndEnd)
        {
            var sum = 0;
            for (var i = 0; i < countExcludingChecksumAndEnd; i++)
            {
                sum += frame[i];
            }

            return (byte)(sum & 0xFF);
        }

        public static bool TryValidate(byte[] frame, out string error)
        {
            error = string.Empty;

            if (frame.Length < 5)
            {
                error = "Frame too short.";
                return false;
            }

            var declaredLength = frame[1];
            if (declaredLength != frame.Length)
            {
                error = $"Length mismatch: declared {declaredLength}, actual {frame.Length}.";
                return false;
            }

            if (frame[frame.Length - 1] != EndCode)
            {
                error = "Missing end code 0xFF.";
                return false;
            }

            var expected = ComputeChecksum(frame, frame.Length - 2);
            var actual = frame[frame.Length - 2];
            if (expected != actual)
            {
                error = $"Checksum mismatch: expected {expected:X2}, got {actual:X2}.";
                return false;
            }

            return true;
        }
    }
}
