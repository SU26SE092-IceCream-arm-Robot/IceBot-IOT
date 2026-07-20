using IceBot.Machines;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class SerialFrameCodecTests
    {
        [Fact]
        public void Build_DispenseCup_MatchesDocumentedExample()
        {
            // Doc "301 Cup-Dropping Machine Serial Communication Protocol V0.0.3":
            // Drop one cup: 04 07 aa 01 00 B6 ff
            var frame = SerialFrameCodec.Build(0x04, SerialFrameCodec.InstructionSet, 0x01, 0x00);

            Assert.Equal(new byte[] { 0x04, 0x07, 0xAA, 0x01, 0x00, 0xB6, 0xFF }, frame);
        }

        [Fact]
        public void Build_SetsLengthToTotalByteCount()
        {
            var noData = SerialFrameCodec.Build(0x01, SerialFrameCodec.InstructionQuery);
            Assert.Equal(5, noData.Length);
            Assert.Equal(5, noData[1]); // length code == total bytes

            var twoData = SerialFrameCodec.Build(0x04, SerialFrameCodec.InstructionSet, 0x01, 0x00);
            Assert.Equal(7, twoData.Length);
            Assert.Equal(7, twoData[1]);
        }

        [Fact]
        public void Build_ChecksumIsLow8BitsOfSumExcludingChecksumAndEnd()
        {
            var frame = SerialFrameCodec.Build(0x04, SerialFrameCodec.InstructionSet, 0x01, 0x00);
            // sum of first 5 bytes: 0x04+0x07+0xAA+0x01+0x00 = 0xB6
            Assert.Equal(0xB6, frame[frame.Length - 2]);
        }

        [Fact]
        public void Build_AlwaysEndsWith0xFF()
        {
            var frame = SerialFrameCodec.Build(0x03, SerialFrameCodec.InstructionSet);
            Assert.Equal(SerialFrameCodec.EndCode, frame[frame.Length - 1]);
        }

        [Fact]
        public void TryValidate_AcceptsAWellFormedFrame()
        {
            var frame = SerialFrameCodec.Build(0x04, SerialFrameCodec.InstructionSet, 0x01, 0x00);

            var ok = SerialFrameCodec.TryValidate(frame, out var error);

            Assert.True(ok);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void TryValidate_RejectsCorruptedChecksum()
        {
            var frame = SerialFrameCodec.Build(0x04, SerialFrameCodec.InstructionSet, 0x01, 0x00);
            frame[frame.Length - 2] ^= 0xFF; // flip the checksum byte

            var ok = SerialFrameCodec.TryValidate(frame, out var error);

            Assert.False(ok);
            Assert.Contains("Checksum", error);
        }

        [Fact]
        public void TryValidate_RejectsWrongLengthCode()
        {
            var frame = SerialFrameCodec.Build(0x04, SerialFrameCodec.InstructionSet, 0x01, 0x00);
            frame[1] = 0x08; // declared length no longer matches actual

            var ok = SerialFrameCodec.TryValidate(frame, out var error);

            Assert.False(ok);
            Assert.Contains("Length", error);
        }

        [Fact]
        public void TryValidate_RejectsMissingEndCode()
        {
            var frame = SerialFrameCodec.Build(0x04, SerialFrameCodec.InstructionSet, 0x01, 0x00);
            frame[frame.Length - 1] = 0x00; // not 0xFF

            var ok = SerialFrameCodec.TryValidate(frame, out _);

            Assert.False(ok);
        }
    }
}
