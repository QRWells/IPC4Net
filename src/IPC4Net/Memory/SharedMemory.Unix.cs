using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using static QRWells.IPC4Net.Unix.LibC;

namespace QRWells.IPC4Net.Memory;

public sealed class SharedMemory : IDisposable
{
    private const int WriteFieldOffset = 0;
    private const int ReadFieldOffset = 4;
    private readonly bool _needPopulate;
    private readonly Notifier _notifier;
    private int _bytesRead;

    private int _bytesWritten;
    private SafeFileHandle _fileHandle;

    private SharedMemoryStream? _readStream;
    private SharedMemoryStream? _readWriteStream;
    private int _wid;
    private SharedMemoryStream? _writeStream;

    public SharedMemory(string name, int size, bool closeOnDispose, bool needPopulate)
    {
        Name = name;
        Size = size;
        _notifier = new Notifier();
        OpenSharedMemory(closeOnDispose);
        MapMemory();
        InitNotifier();
        BytesRead = 0;
        BytesWritten = 0;
        _needPopulate = needPopulate;
    }

    public string Name { get; }
    public int Size { get; private set; }
    public MemoryHandle MemoryHandle { get; private set; }
    public unsafe void* Pointer => MemoryHandle.Pointer;

    public int BytesWritten
    {
        get
        {
            unsafe
            {
                var newBytesWritten = Marshal.ReadInt32((nint)MemoryHandle.Pointer, WriteFieldOffset);
                if (newBytesWritten == _bytesWritten) return newBytesWritten;
                OnWriteModified?.Invoke(this, newBytesWritten - _bytesWritten);
                _bytesWritten = newBytesWritten;
                return newBytesWritten;
            }
        }

        private set
        {
            unsafe
            {
                Marshal.WriteInt32((nint)MemoryHandle.Pointer, WriteFieldOffset, value);
            }
        }
    }

    public int BytesRead
    {
        get
        {
            unsafe
            {
                var newBytesRead = Marshal.ReadInt32((nint)MemoryHandle.Pointer, ReadFieldOffset);
                if (newBytesRead == _bytesRead) return newBytesRead;
                OnReadModified?.Invoke(this, newBytesRead - _bytesRead);
                _bytesRead = newBytesRead;
                return newBytesRead;
            }
        }

        private set
        {
            unsafe
            {
                Marshal.WriteInt32((nint)MemoryHandle.Pointer, ReadFieldOffset, value);
            }
        }
    }

    public ReadOnlySpan<byte> Span
    {
        get
        {
            unsafe
            {
                return new Span<byte>(Unsafe.Add<int>(Pointer, 2), Size - 8);
            }
        }
    }

    private Span<byte> InternalSpan
    {
        get
        {
            unsafe
            {
                return new Span<byte>(Unsafe.Add<int>(Pointer, 2), Size - 8);
            }
        }
    }

    public SharedMemoryStream ReadStream => _readStream ??= new SharedMemoryStream(this);
    public SharedMemoryStream WriteStream => _writeStream ??= new SharedMemoryStream(this, FileAccess.Write);

    public SharedMemoryStream ReadWriteStream =>
        _readWriteStream ??= new SharedMemoryStream(this, FileAccess.ReadWrite);

    public void Dispose()
    {
        _ = Close((int)_fileHandle.DangerousGetHandle());
        _ = SharedMemoryUnlink(Name);
        unsafe
        {
            _ = MemUnmap((nint)MemoryHandle.Pointer, (ulong)Size);
        }

        _notifier.Dispose();
    }

    private void InitNotifier()
    {
        var buffer = new byte[128];
        var pid = Environment.ProcessId;
        var fdsPath = $"/proc/{pid}/fd/";
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                var readLength = ReadLink(fdsPath + _fileHandle.DangerousGetHandle().ToInt32(), ptr,
                    buffer.Length);
                var pathName = Marshal.PtrToStringAnsi((nint)ptr, readLength);
                _wid = _notifier.Register(pathName, NotifyMask.Modify);
            }
        }
    }

    public event EventHandler<int>? OnWriteModified;
    public event EventHandler<int>? OnReadModified;

    public void WaitNotify()
    {
        while (true)
            if (_notifier.ReadEvents().Any(e => e.Wfd == _wid))
                break;
    }

    private void OpenSharedMemory(bool closeOnDispose)
    {
        var flags = closeOnDispose ? OpenOption.CloseOnExec : OpenOption.ReadOnly;
        var fd = SharedMemoryOpen(Name, (int)(flags | OpenOption.Create | OpenOption.ReadWrite),
            Convert.ToInt32("777", 8));
        if (fd == -1) throw new Exception($"Failed to open shared memory with {Marshal.GetLastWin32Error()}");

        _fileHandle = new SafeFileHandle(fd, true);

        var res = SharedMemoryTruncate(fd, Size);
        if (res == -1) throw new Exception($"Failed to open shared memory with {Marshal.GetLastWin32Error()}");
    }

    private void MapMemory()
    {
        var populateFlag = _needPopulate ? MemoryFlags.Populate : 0;
        var memoryAddress = MemoryMap(0, (ulong)Size, (int)(MemoryProtection.Write | MemoryProtection.Read),
            (int)(MemoryFlags.Shared | populateFlag | MemoryFlags.Locked), (int)_fileHandle.DangerousGetHandle(), 0);
        unsafe
        {
            MemoryHandle = new MemoryHandle((void*)memoryAddress);
        }
    }

    public unsafe bool TryResize(int size)
    {
        if (size <= Size) return false;

        var oldHandle = MemoryHandle;
        var oldSize = Size;
        var res = SharedMemoryTruncate((int)_fileHandle.DangerousGetHandle(), size);
        if (res == -1) throw new Exception($"Failed to open shared memory with {Marshal.GetLastWin32Error()}");
        var populateFlag = _needPopulate ? MemoryFlags.Populate : 0;
        var memoryAddress = MemoryMap(0, (ulong)size, (int)(MemoryProtection.Write | MemoryProtection.Read),
            (int)(MemoryFlags.Shared | populateFlag | MemoryFlags.Locked), (int)_fileHandle.DangerousGetHandle(), 0);
        lock (this)
        {
            if (oldHandle.Pointer != MemoryHandle.Pointer) oldHandle = default;

            MemoryHandle = new MemoryHandle((void*)memoryAddress);
            Size = size;
        }

        if (oldHandle.Pointer != default) _ = MemUnmap((nint)oldHandle.Pointer, (ulong)oldSize);

        // reset streams
        _readStream = null;
        _writeStream = null;
        _readWriteStream = null;

        return true;
    }

    public void Write<T>(T value) where T : unmanaged
    {
        unsafe
        {
            var ptr = Unsafe.AsPointer(ref value);
            var size = Marshal.SizeOf<T>();
            var span = new Span<byte>(ptr, size);
            span.CopyTo(InternalSpan);
            BytesWritten += size;
        }
    }

    public void Write(ReadOnlySpan<byte> sequence)
    {
        sequence.CopyTo(InternalSpan);
        BytesWritten += sequence.Length;
    }

    public T Read<T>() where T : unmanaged
    {
        unsafe
        {
            T value = default;
            var ptr = Unsafe.AsPointer(ref value);
            var size = Marshal.SizeOf<T>();
            var span = new Span<byte>(ptr, size);
            InternalSpan[..size].CopyTo(span);
            BytesRead += size;
            return value;
        }
    }

    internal void ReadContent(Span<byte> sequence)
    {
        InternalSpan[..sequence.Length].CopyTo(sequence);
    }
}