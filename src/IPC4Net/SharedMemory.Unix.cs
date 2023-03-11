using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using static QRWells.IPC4Net.LibC;

namespace QRWells.IPC4Net;

public class SharedMemory : IDisposable
{
    private const int WriteFieldOffset = 0;
    private const int ReadFieldOffset = 4;
    private SafeFileHandle _fileHandle;

    private SharedMemory(string name, int size, bool closeOnDispose, bool needPopulate)
    {
        Name = name;
        Size = size;
        OpenSharedMemory(closeOnDispose);
        MapMemory(needPopulate);
        BytesRead = 0;
        BytesWritten = 0;
    }

    public string Name { get; }
    public int Size { get; }
    public MemoryHandle MemoryHandle { get; private set; }
    public unsafe void* Pointer => MemoryHandle.Pointer;

    public int BytesWritten
    {
        get
        {
            unsafe
            {
                return Marshal.ReadInt32((nint)MemoryHandle.Pointer, WriteFieldOffset);
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
                return Marshal.ReadInt32((nint)MemoryHandle.Pointer, ReadFieldOffset);
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

    public void Dispose()
    {
        Close((int)_fileHandle.DangerousGetHandle());
        SharedMemoryUnlink(Name);
        unsafe
        {
            MemUnmap((nint)MemoryHandle.Pointer, (ulong)Size);
        }

        GC.SuppressFinalize(this);
    }

    public static SharedMemory Create(string name, long size)
    {
        return new SharedMemory(name, (int)size, true, true);
    }

    public static SharedMemory Open(string name, long size)
    {
        return new SharedMemory(name, (int)size, false, true);
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

    private void MapMemory(bool needPopulate)
    {
        var populateFlag = needPopulate ? MemoryFlags.Populate : 0;
        var memoryAddress = MemoryMap(0, (ulong)Size, (int)(MemoryProtection.Write | MemoryProtection.Read),
            (int)(MemoryFlags.Shared | populateFlag | MemoryFlags.Locked), (int)_fileHandle.DangerousGetHandle(), 0);
        unsafe
        {
            MemoryHandle = new MemoryHandle((void*)memoryAddress);
        }
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

    public void Write(ReadOnlySequence<byte> sequence)
    {
        sequence.CopyTo(InternalSpan);
        BytesWritten += (int)sequence.Length;
    }

    public T Read<T>() where T : unmanaged
    {
        unsafe
        {
            T value = default;
            var ptr = Unsafe.AsPointer(ref value);
            var size = Marshal.SizeOf<T>();
            var span = new Span<byte>(ptr, size);
            InternalSpan.Slice(0, size).CopyTo(span);
            BytesRead += size;
            return value;
        }
    }
}