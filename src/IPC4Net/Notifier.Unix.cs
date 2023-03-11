using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace QRWells.IPC4Net;

public class Notifier : IDisposable
{
    private readonly unsafe byte* _buffer;
    private readonly SafeFileHandle _handle;
    private int _nonBlocking;

    public Notifier()
    {
        var fd = LibC.InotifyInit();
        _handle = new SafeFileHandle(fd, true);
        unsafe
        {
            _buffer = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        }
    }

    public void Dispose()
    {
        var _ = LibC.Close(_handle.DangerousGetHandle().ToInt32());
        unsafe
        {
            NativeMemory.AlignedFree(_buffer);
        }

        GC.SuppressFinalize(this);
    }

    public IEnumerable<FileEvent> ReadEvents()
    {
        var events = new List<FileEvent>();
        unsafe
        {
            var bytesRead = LibC.Read(_handle.DangerousGetHandle().ToInt32(), _buffer, 4096);
            if (bytesRead < 0) throw new Exception(Marshal.GetLastWin32Error().ToString());

            long offset = 0;
            while (offset < bytesRead)
            {
                var iEvent = Marshal.PtrToStructure<LibC.inotify_event>((nint)(_buffer + offset));
                events.Add(new FileEvent
                {
                    Mask = iEvent.mask,
                    Name = LibC.inotify_event.Name(iEvent),
                    Wfd = iEvent.wd
                });
                offset += sizeof(LibC.inotify_event) + iEvent.len;
            }
        }

        return events;
    }

    public int Register(string path, LibC.NotifyMask mask)
    {
        var wd = LibC.InotifyAddWatch(_handle.DangerousGetHandle().ToInt32(), path, (uint)mask);
        if (wd == -1) throw new Exception("Failed to register path");

        return wd;
    }

    public void Unregister(int wd)
    {
        var res = LibC.InotifyRemoveWatch(_handle.DangerousGetHandle().ToInt32(), wd);
        if (res < 0) throw new Exception(Marshal.GetLastWin32Error().ToString());
    }

    public void SetNonBlocking()
    {
        if (Volatile.Read(ref _nonBlocking) == 1 || Interlocked.CompareExchange(ref _nonBlocking, 1, 0) == 1) return;

        var flags = LibC.Fcntl(_handle.DangerousGetHandle().ToInt32(), (int)LibC.FcntlCommand.SetFileStatusFlags, 0);
        if (flags < 0) throw new Exception(Marshal.GetLastWin32Error().ToString());

        var res = LibC.Fcntl(_handle.DangerousGetHandle().ToInt32(), (int)LibC.FcntlCommand.SetFileStatusFlags,
            flags | (int)LibC.OpenOption.NonBlock);
        if (res == 0) return;
        Interlocked.Exchange(ref _nonBlocking, 0);
        throw new Exception(Marshal.GetLastWin32Error().ToString());
    }
}