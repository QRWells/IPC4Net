using System.Runtime.InteropServices;

namespace QRWells.IPC4Net.Unix;

public class LibC
{
    [Flags]
    public enum FcntlCommand
    {
        DuplicateFd = 0,
        GetFdFlags = 1,
        SetFdFlags = 2,
        GetFileStatusFlags = 3,
        SetFileStatusFlags = 4
    }

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
    public enum NotifyMask : uint
    {
        Access = 0x00000001, // File was accessed
        Modify = 0x00000002, // File was modified
        Attrib = 0x00000004, // Metadata changed
        CloseWrite = 0x00000008, // Writtable file was closed
        CloseNoWrite = 0x00000010, // Unwrittable file closed
        Open = 0x00000020, // File was opened
        MovedFrom = 0x00000040, // File was moved from X
        MovedTo = 0x00000080, // File was moved to Y
        Create = 0x00000100, // Subfile was created
        Delete = 0x00000200, // Subfile was deleted
        DeleteSelf = 0x00000400, // Self was deleted
        MoveSelf = 0x00000800 // Self was moved
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

    private const string LibraryName = "libc.so.6";

    [DllImport(LibraryName, EntryPoint = "shm_open", SetLastError = true, CharSet = CharSet.Ansi,
        CallingConvention = CallingConvention.Cdecl)]
    public static extern int SharedMemoryOpen(string name, int oflag, int mode);

    [DllImport(LibraryName, EntryPoint = "shm_unlink", SetLastError = true, CharSet = CharSet.Ansi,
        CallingConvention = CallingConvention.Cdecl)]
    public static extern int SharedMemoryUnlink(string name);

    [DllImport(LibraryName, EntryPoint = "ftruncate", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SharedMemoryTruncate(int fd, int length);

    [DllImport(LibraryName, EntryPoint = "mmap", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint MemoryMap(nint addr, ulong length, int prot, int flags, int fd, long offset);

    [DllImport(LibraryName, EntryPoint = "munmap", SetLastError = true)]
    public static extern int MemUnmap(nint addr, ulong length);

    [DllImport(LibraryName, EntryPoint = "open", CharSet = CharSet.Ansi)]
    public static extern int Open(string path, int flags, int mode);

    [DllImport(LibraryName, EntryPoint = "close")]
    public static extern int Close(int fd);

    [DllImport(LibraryName, EntryPoint = "readlink", SetLastError = true, CharSet = CharSet.Ansi,
        CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int ReadLink(string path, byte* buffer, int bufferSize);

    [DllImport(LibraryName, EntryPoint = "inotify_init", SetLastError = true)]
    public static extern int InotifyInit();

    [DllImport(LibraryName, EntryPoint = "inotify_add_watch", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern int InotifyAddWatch(int fd, string path, uint mask);

    [DllImport(LibraryName, EntryPoint = "inotify_rm_watch", SetLastError = true)]
    public static extern int InotifyRemoveWatch(int fd, int wd);

    [DllImport(LibraryName, EntryPoint = "read", SetLastError = true)]
    public static extern unsafe int Read(int fd, byte* buffer, int count);

    [DllImport(LibraryName, EntryPoint = "write", SetLastError = true)]
    public static extern unsafe int Write(int fd, byte* buffer, int count);

    [DllImport(LibraryName, EntryPoint = "fcntl", SetLastError = true)]
    public static extern int Fcntl(int fd, int cmd, int arg);

    [StructLayout(LayoutKind.Sequential)]
    public struct inotify_event
    {
        public int wd; // Watch descriptor 
        public uint mask; // Mask describing event 
        public uint cookie; // Unique cookie associating related events (for rename(2))
        public uint len; // Size of name field

        public static unsafe char* Name(inotify_event* e)
        {
            return (char*)((byte*)e + sizeof(inotify_event));
        }

        public static string? Name(inotify_event e)
        {
            unsafe
            {
                return Marshal.PtrToStringAnsi((nint)Name(&e));
            }
        }
    }
}