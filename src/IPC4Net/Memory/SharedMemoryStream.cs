namespace QRWells.IPC4Net.Memory;

public sealed class SharedMemoryStream : Stream
{
    private readonly byte[] _buffer;
    private readonly SharedMemory _memory;
    private readonly int _size;
    private int _writePosition;
    private int _readPosition;
    private int _bytesRead;

    internal SharedMemoryStream(SharedMemory memory, FileAccess access = FileAccess.Read)
    {
        _memory = memory;
        _size = memory.Size - 8;
        _buffer = new byte[_size];
        _memory.ReadContent(_buffer.AsSpan());

        _writePosition = _memory.BytesWritten;
        CanRead = access.HasFlag(FileAccess.Read);
        CanWrite = access.HasFlag(FileAccess.Write);
    }

    public override bool CanRead { get; }
    public override bool CanSeek => false;
    public override bool CanWrite { get; }
    public override long Length => _size;

    public override long Position
    {
        get => _writePosition;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        // Commit changes to shared memory
        _memory.Write(_buffer.AsSpan()[_writePosition..]);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (!CanRead) throw new NotSupportedException();
        var read = Math.Min(count, _size);
        _buffer.AsSpan().Slice(_writePosition, read).CopyTo(buffer.AsSpan().Slice(offset, read));
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!CanWrite) throw new NotSupportedException();
        if (count > _size - _writePosition) throw new ArgumentOutOfRangeException(nameof(count));

        buffer.AsSpan().Slice(offset, count).CopyTo(_buffer.AsSpan().Slice(_writePosition, count));

        _writePosition += count;
    }
}