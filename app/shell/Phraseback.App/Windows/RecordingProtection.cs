using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Phraseback.App.Windows;

internal sealed class RecordingProtection : IDisposable
{
    private readonly Thread thread;
    private uint threadId;
    private int disposed;
    private readonly TaskCompletionSource<bool> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public RecordingProtection(Action stop)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows recording is required.");
        thread = new Thread(() =>
        {
            threadId = GetCurrentThreadId();
            PeekMessageW(out _, IntPtr.Zero, 0, 0, 0);
            if (Volatile.Read(ref disposed) != 0) { ready.TrySetCanceled(); return; }
            if (!RegisterHotKey(IntPtr.Zero, 0x4652, 0x4006, 0x78)) { ready.SetException(new Win32Exception(Marshal.GetLastWin32Error(), "Ctrl+Shift+F9 is unavailable. Close the conflicting app before recording.")); return; }
            ready.SetResult(true);
            try
            {
                // Dispose can race thread startup before a message queue exists.
                // The durable flag covers that lost-message window.
                while (Volatile.Read(ref disposed) == 0 && GetMessageW(out var message, IntPtr.Zero, 0, 0) > 0)
                    if (message.Message == 0x312) Dispatcher.UIThread.Post(() =>
                    {
                        if (Volatile.Read(ref disposed) == 0) stop();
                    });
            }
            finally { UnregisterHotKey(IntPtr.Zero, 0x4652); }
        }) { IsBackground = true, Name = "Phraseback stop shortcut" };
        thread.Start();
    }
    public Task Ready => ready.Task.WaitAsync(TimeSpan.FromSeconds(3));
    public static void Exclude(Window window)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? throw new InvalidOperationException("Recording window is not ready.");
        Exclude(handle);
    }
    internal static void Exclude(IntPtr handle)
    {
        if (!SetWindowDisplayAffinity(handle, 0x11)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot exclude recording controls from capture.");
    }
    public static void Restore(Window window)
    {
        if (window.TryGetPlatformHandle()?.Handle is { } handle && !SetWindowDisplayAffinity(handle, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot restore the studio window's capture visibility.");
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        // Ready success publishes the thread ID only after its queue exists.
        // A late worker observes disposed itself and never enters the message loop.
        if (ready.Task.IsCompletedSuccessfully) PostThreadMessageW(threadId, 0x12, UIntPtr.Zero, IntPtr.Zero);
        thread.Join(500);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeMessage { public IntPtr Window; public uint Message; public UIntPtr WParam; public IntPtr LParam; public uint Time; public Point Point; public uint Private; }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern int GetMessageW(out NativeMessage message, IntPtr window, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessageW(out NativeMessage message, IntPtr window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool PostThreadMessageW(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);
}
