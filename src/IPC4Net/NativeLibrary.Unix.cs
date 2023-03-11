using System.Runtime.InteropServices;

namespace QRWells.IPC4Net;

public class LibC
{
    [Flags]
    public enum MemoryFlags
    {
        Shared = 0x01,
        Private = 0x02,
        Fixed = 0x10,
        Locked = 0x2000,
        Populate = 0x8000
    }

    [Flags]
    public enum MemoryProtection
    {
        None = 0x0,
        Read = 0x1,
        Write = 0x2,
        Exec = 0x4,
        GrowsDown = 0x01000000,
        GrowsUp = 0x02000000
    }

    [Flags]
    public enum OpenOption
    {
        ReadOnly = 0b00,
        WriteOnly = 0b01,
        ReadWrite = 0b10,
        AccessModeMask = 0b11,

        Create = 0x40,
        Exclusive = 0x80,
        Truncate = 0x200,
        Append = 0x400,
        NonBlock = 0x800,
        Sync = 0x1000,
        Async = 0x2000,

        Direct = 0x4000,
        LargeFile = 0x8000,
        Directory = 0x10000,
        NoFollow = 0x20000,
        NoATime = 0x40000,
        CloseOnExec = 0x80000
    }

    [DllImport("libc.so.6", EntryPoint = "shm_open", SetLastError = true, CharSet = CharSet.Ansi,
        CallingConvention = CallingConvention.Cdecl)]
    public static extern int SharedMemoryOpen(string name, int oflag, int mode);

    [DllImport("libc.so.6", EntryPoint = "shm_unlink", SetLastError = true, CharSet = CharSet.Ansi,
        CallingConvention = CallingConvention.Cdecl)]
    public static extern int SharedMemoryUnlink(string name);

    [DllImport("libc.so.6", EntryPoint = "ftruncate", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SharedMemoryTruncate(int fd, int length);

    [DllImport("libc.so.6", EntryPoint = "mmap", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint MemoryMap(nint addr, ulong length, int prot, int flags, int fd, long offset);

    [DllImport("libc.so.6", EntryPoint = "munmap", SetLastError = true)]
    public static extern int MemUnmap(nint addr, ulong length);

    [DllImport("libc.so.6", EntryPoint = "open", CharSet = CharSet.Ansi)]
    public static extern int Open(string path, int flags, int mode);

    [DllImport("libc.so.6", EntryPoint = "close")]
    public static extern int Close(int fd);

    [DllImport("libc.so.6", EntryPoint = "readlink", SetLastError = true, CharSet = CharSet.Ansi,
        CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int ReadLink(string path, byte* buffer, int bufferSize);
}