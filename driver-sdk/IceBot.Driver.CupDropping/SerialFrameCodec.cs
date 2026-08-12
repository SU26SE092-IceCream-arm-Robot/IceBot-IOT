using System;

namespace IceBot.Driver.CupDropping
{
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

        private static byte ComputeChecksum(byte[] frame, int count)
        {
            var sum = 0;
            for (var index = 0; index < count; index++) sum += frame[index];
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
            if (frame[1] != frame.Length)
            {
                error = $"Length mismatch: declared {frame[1]}, actual {frame.Length}.";
                return false;
            }
            if (frame[frame.Length - 1] != EndCode)
            {
                error = "Missing end code 0xFF.";
                return false;
            }

            var expected = ComputeChecksum(frame, frame.Length - 2);
            var actual = frame[frame.Length - 2];
            if (expected == actual) return true;
            error = $"Checksum mismatch: expected {expected:X2}, got {actual:X2}.";
            return false;
        }
    }
}
