using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace OmsiStudio.OmsiFormat.Parser;

/// <summary>
/// A wrapper over Stream providing safe, little-endian, bounded binary reading operations.
/// </summary>
public sealed class BoundedBinaryReader : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundedBinaryReader"/> class.
    /// </summary>
    /// <param name="stream">The underlying stream to read from.</param>
    /// <param name="leaveOpen">True to keep the stream open after disposal; otherwise, false.</param>
    public BoundedBinaryReader(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Gets the current position within the stream.
    /// </summary>
    public long Position => _stream.Position;

    /// <summary>
    /// Gets the total length of the stream.
    /// </summary>
    public long Length => _stream.Length;

    /// <summary>
    /// Gets the number of bytes remaining to be read.
    /// </summary>
    public long RemainingBytes => _stream.Length - _stream.Position;

    private void EnsureRemaining(long bytesRequired)
    {
        if (bytesRequired < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesRequired), "Required bytes count cannot be negative.");
        }

        if (RemainingBytes < bytesRequired)
        {
            throw new EndOfStreamException($"Read request failed: requested {bytesRequired} bytes but only {RemainingBytes} bytes remain.");
        }
    }

    private void ReadExactly(Span<byte> buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = _stream.Read(buffer.Slice(totalRead));
            if (read == 0)
            {
                throw new EndOfStreamException($"Unexpected end of stream while reading exactly {buffer.Length} bytes. Read {totalRead} bytes.");
            }
            totalRead += read;
        }
    }

    /// <summary>
    /// Reads a single byte from the stream.
    /// </summary>
    public byte ReadByte()
    {
        EnsureRemaining(1);
        int value = _stream.ReadByte();
        if (value == -1)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading byte.");
        }
        return (byte)value;
    }

    /// <summary>
    /// Reads a 2-byte unsigned short (little-endian) from the stream.
    /// </summary>
    public ushort ReadUInt16()
    {
        EnsureRemaining(2);
        Span<byte> buffer = stackalloc byte[2];
        ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }

    /// <summary>
    /// Reads a 4-byte unsigned integer (little-endian) from the stream.
    /// </summary>
    public uint ReadUInt32()
    {
        EnsureRemaining(4);
        Span<byte> buffer = stackalloc byte[4];
        ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    /// <summary>
    /// Reads a 4-byte signed integer (little-endian) from the stream.
    /// </summary>
    public int ReadInt32()
    {
        EnsureRemaining(4);
        Span<byte> buffer = stackalloc byte[4];
        ReadExactly(buffer);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    /// <summary>
    /// Reads a 4-byte floating-point value (little-endian) from the stream.
    /// </summary>
    public float ReadSingle()
    {
        EnsureRemaining(4);
        Span<byte> buffer = stackalloc byte[4];
        ReadExactly(buffer);
        return BinaryPrimitives.ReadSingleLittleEndian(buffer);
    }

    /// <summary>
    /// Seeks the stream position with safety boundaries check.
    /// </summary>
    public void Seek(long offset, SeekOrigin origin)
    {
        long targetPosition;
        try
        {
            targetPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(_stream.Position + offset),
                SeekOrigin.End => checked(_stream.Length + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
        }
        catch (OverflowException ex)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), ex, "Seek operation offset overflowed.");
        }

        if (targetPosition < 0 || targetPosition > _stream.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), $"Seek target position {targetPosition} is outside stream boundaries (0 to {_stream.Length}).");
        }

        _stream.Seek(targetPosition, SeekOrigin.Begin);
    }

    /// <summary>
    /// Skips a number of bytes in the stream with safety boundaries check.
    /// </summary>
    public void Skip(long count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Skip count cannot be negative.");
        }

        Seek(count, SeekOrigin.Current);
    }

    /// <summary>
    /// Reads a string of specified length, bounded by maximum allowed length constraints.
    /// </summary>
    public string ReadBoundedString(int length, int maxLength, Encoding encoding)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "String length cannot be negative.");
        }

        if (maxLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length limit cannot be negative.");
        }

        if (length > maxLength)
        {
            throw new InvalidDataException($"String length ({length}) exceeds maximum allowed length ({maxLength}).");
        }

        EnsureRemaining(length);

        if (length == 0)
        {
            return string.Empty;
        }

        byte[] buffer = new byte[length];
        ReadExactly(buffer.AsSpan());

        return encoding.GetString(buffer);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }
}
