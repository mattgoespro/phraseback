using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Phraseback.Client;

namespace Phraseback.App.Windows;

/// <summary>A native hollow outline, never a transparent full-desktop input surface.</summary>
internal sealed class RecordingIndicator : IDisposable
{
    private const string ClassName = "Phraseback.RecordingOutline";
    private const uint ExtendedStyle = 0x00080000 | 0x20 | 0x08000000 | 0x80; // layered, transparent, no-activate, tool
    private static readonly WindowProcedure Procedure = WindowProc;
    private static readonly Dictionary<IntPtr, RecordingIndicator> Windows = new();
    private static ushort windowClass;
    private readonly Action unexpectedClose;
    private bool disposing;
    internal IntPtr Handle { get; private set; }

    public RecordingIndicator(CaptureArea area, Action unexpectedClose)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (area.Width <= 0 || area.Height <= 0) throw new ArgumentOutOfRangeException(nameof(area));
        this.unexpectedClose = unexpectedClose;
        if (windowClass == 0)
        {
            var brush = CreateSolidBrush(0x004B5BD6); // COLORREF #D65B4B, active capture boundary
            var registration = new WindowClass { Procedure = Marshal.GetFunctionPointerForDelegate(Procedure),
                Instance = GetModuleHandleW(null), Background = brush, Name = ClassName };
            windowClass = RegisterClassW(ref registration);
            if (windowClass == 0) { DeleteObject(brush); throw NativeError("Cannot register recording outline."); }
            // Class and its brush live for the shell's lifetime.
        }
        Handle = CreateWindowExW(ExtendedStyle, ClassName, "Recording region", 0x80000000,
            area.Left, area.Top, area.Width, area.Height, IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);
        if (Handle == IntPtr.Zero) throw NativeError("Cannot create recording outline.");
        Windows.Add(Handle, this);
        try
        {
            // Remove the entire interior from the HWND, independently of layered hit testing.
            var outer = CreateRectRgn(0, 0, area.Width, area.Height);
            var inset = Math.Min(2, Math.Min(area.Width, area.Height) / 2);
            var inner = CreateRectRgn(inset, inset, area.Width - inset, area.Height - inset);
            try
            {
                if (outer == IntPtr.Zero || inner == IntPtr.Zero || CombineRgn(outer, outer, inner, 4) == 0)
                    throw NativeError("Cannot shape recording outline.");
                if (SetWindowRgn(Handle, outer, true) == 0) throw NativeError("Cannot apply recording outline.");
                outer = IntPtr.Zero; // Window owns the region after success.
            }
            finally { if (outer != IntPtr.Zero) DeleteObject(outer); if (inner != IntPtr.Zero) DeleteObject(inner); }
            if (!SetLayeredWindowAttributes(Handle, 0, 255, 2)) throw NativeError("Cannot make recording outline input-transparent.");
            RecordingProtection.Exclude(Handle);
        }
        catch { Dispose(); throw; }
    }

    public void Show()
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(Handle == IntPtr.Zero, this);
        if (!SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10 | 0x40))
            throw NativeError("Cannot show recording outline.");
    }

    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (Handle == IntPtr.Zero) return;
        disposing = true;
        if (!DestroyWindow(Handle)) throw NativeError("Cannot close recording outline.");
    }

    private static IntPtr WindowProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam)
    {
        if (message == 0x82 && Windows.Remove(window, out var indicator)) // WM_NCDESTROY
        {
            indicator.Handle = IntPtr.Zero;
            if (!indicator.disposing) Dispatcher.UIThread.Post(indicator.unexpectedClose);
        }
        return DefWindowProcW(window, message, wParam, lParam);
    }
    private static Win32Exception NativeError(string message) => new(Marshal.GetLastWin32Error(), message);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr WindowProcedure(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
    {
        public uint Style; public IntPtr Procedure; public int ClassExtra, WindowExtra;
        public IntPtr Instance, Icon, Cursor, Background; public string? Menu; public string Name;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassW(ref WindowClass value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowExW(uint extended, string className, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetLayeredWindowAttributes(IntPtr window, uint key, byte alpha, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowRgn(IntPtr window, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern int CombineRgn(IntPtr destination, IntPtr first, IntPtr second, int mode);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr value);
}
