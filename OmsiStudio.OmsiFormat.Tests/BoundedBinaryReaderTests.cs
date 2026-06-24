using System;
using System.IO;
using System.Text;
using Xunit;
using OmsiStudio.OmsiFormat.Parser;

namespace OmsiStudio.OmsiFormat.Tests;

public class BoundedBinaryReaderTests
{
    [Fact]
    public void ReadPrimitives_WithValidLittleEndianData_Succeeds()
    {
        // Arrange
        // Byte: 0x55
        // UInt16: 0x1234 -> 34 12
        // UInt32: 0xDEADBEEF -> EF BE AD DE
        // Int32: -42 -> D6 FF FF FF
        // Float: 1.5f -> 00 00 C0 3F
        byte[] data = [
            0x55,
            0x34, 0x12,
            0xEF, 0xBE, 0xAD, 0xDE,
            0xD6, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0xC0, 0x3F
        ];

        using var ms = new MemoryStream(data);
        using var reader = new BoundedBinaryReader(ms);

        // Act & Assert
        Assert.Equal(0x55, reader.ReadByte());
        Assert.Equal(0x1234, reader.ReadUInt16());
        Assert.Equal(0xDEADBEEF, reader.ReadUInt32());
        Assert.Equal(-42, reader.ReadInt32());
        Assert.Equal(1.5f, reader.ReadSingle());
    }

    [Fact]
    public void ReadPrimitives_PastEndOfStream_ThrowsEndOfStreamException()
    {
        // Arrange
        byte[] data = [0x01];
        using var ms = new MemoryStream(data);
        using var reader = new BoundedBinaryReader(ms);

        // Act
        reader.ReadByte(); // Position is now 1, Length is 1

        // Assert
        Assert.Throws<EndOfStreamException>(() => reader.ReadByte());
        Assert.Throws<EndOfStreamException>(() => reader.ReadUInt16());
        Assert.Throws<EndOfStreamException>(() => reader.ReadUInt32());
        Assert.Throws<EndOfStreamException>(() => reader.ReadInt32());
        Assert.Throws<EndOfStreamException>(() => reader.ReadSingle());
    }

    [Fact]
    public void SeekAndSkip_WithinBounds_Succeeds()
    {
        // Arrange
        byte[] data = [0x00, 0x11, 0x22, 0x33, 0x44, 0x55];
        using var ms = new MemoryStream(data);
        using var reader = new BoundedBinaryReader(ms);

        // Act & Assert
        Assert.Equal(0, reader.Position);
        
        reader.Skip(2);
        Assert.Equal(2, reader.Position);
        Assert.Equal(0x22, reader.ReadByte()); // Position becomes 3

        reader.Seek(1, SeekOrigin.Begin);
        Assert.Equal(1, reader.Position);
        Assert.Equal(0x11, reader.ReadByte()); // Position becomes 2

        reader.Seek(-1, SeekOrigin.End);
        Assert.Equal(5, reader.Position);
        Assert.Equal(0x55, reader.ReadByte());
    }

    [Fact]
    public void SeekAndSkip_OutsideBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        byte[] data = [0x00, 0x11, 0x22];
        using var ms = new MemoryStream(data);
        using var reader = new BoundedBinaryReader(ms);

        // Assert seek before start
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Seek(-1, SeekOrigin.Begin));
        // Assert seek after end
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Seek(4, SeekOrigin.Begin));
        // Assert skip past end
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Skip(4));
        // Assert negative skip throws
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Skip(-1));
    }

    [Fact]
    public void ReadBoundedString_WithValidBoundaries_Succeeds()
    {
        // Arrange
        string expected = "OMSI Object";
        byte[] stringBytes = Encoding.ASCII.GetBytes(expected);
        using var ms = new MemoryStream(stringBytes);
        using var reader = new BoundedBinaryReader(ms);

        // Act
        string actual = reader.ReadBoundedString(stringBytes.Length, 20, Encoding.ASCII);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ReadBoundedString_ExceedingMaximumAllowedLength_ThrowsInvalidDataException()
    {
        // Arrange
        byte[] data = [0x00, 0x01, 0x02];
        using var ms = new MemoryStream(data);
        using var reader = new BoundedBinaryReader(ms);

        // Assert that asking for length 5 when limit is 3 throws
        Assert.Throws<InvalidDataException>(() => reader.ReadBoundedString(5, 3, Encoding.ASCII));
    }

    [Fact]
    public void ReadBoundedString_TruncatedPayload_ThrowsEndOfStreamException()
    {
        // Arrange
        byte[] data = [0x41, 0x42]; // "AB"
        using var ms = new MemoryStream(data);
        using var reader = new BoundedBinaryReader(ms);

        // Act & Assert
        // Asking for 5 bytes when only 2 are available should fail with EndOfStreamException
        Assert.Throws<EndOfStreamException>(() => reader.ReadBoundedString(5, 10, Encoding.ASCII));
    }

    private class PartialReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly int _maxReadSize;

        public PartialReadStream(Stream inner, int maxReadSize = 1)
        {
            _inner = inner;
            _maxReadSize = maxReadSize;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        public override int Read(byte[] buffer, int offset, int count)
        {
            int toRead = Math.Min(count, _maxReadSize);
            return _inner.Read(buffer, offset, toRead);
        }

        public override int Read(Span<byte> buffer)
        {
            int toRead = Math.Min(buffer.Length, _maxReadSize);
            return _inner.Read(buffer.Slice(0, toRead));
        }
    }

    [Fact]
    public void ReadPrimitivesAndStrings_WithPartialReadStream_Succeeds()
    {
        // Arrange
        // UInt32: 0xDEADBEEF -> EF BE AD DE
        // String: "Test" -> 54 65 73 74
        byte[] data = [
            0xEF, 0xBE, 0xAD, 0xDE,
            0x54, 0x65, 0x73, 0x74
        ];

        using var baseMs = new MemoryStream(data);
        using var partialStream = new PartialReadStream(baseMs, maxReadSize: 1); // Returns at most 1 byte per Read call
        using var reader = new BoundedBinaryReader(partialStream);

        // Act & Assert
        // ReadUInt32 (4 bytes) - requires multiple read calls of 1 byte each
        Assert.Equal(0xDEADBEEF, reader.ReadUInt32());
        
        // ReadBoundedString (4 bytes)
        Assert.Equal("Test", reader.ReadBoundedString(4, 10, Encoding.ASCII));
    }
}
