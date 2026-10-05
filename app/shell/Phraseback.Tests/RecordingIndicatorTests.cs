using System.Runtime.InteropServices;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Phraseback.App.Windows;
using Phraseback.Client;
using Xunit;

namespace Phraseback.Tests;

public sealed class RecordingIndicatorTests
{
    [AvaloniaTheory]
    [InlineData(100, 100, 640, 400)]
    [InlineData(-1920, -200, 1920, 1080)]
    [InlineData(0, 0, 1, 1)]
    public void NativeOutlineIsHollowLayeredNonactivatingAndExcluded(int x, int y, int width, int height)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var outline = new RecordingIndicator(new CaptureArea(x, y, width, height), () => Assert.Fail("Unexpected close"));
        var style = GetWindowLongPtrW(outline.Handle, -20).ToInt64();
        Assert.Equal(0x080800A0L, style & 0x080800A0L);
        Assert.True(GetWindowDisplayAffinity(outline.Handle, out var affinity));
        Assert.Equal(0x11u, affinity);
        Assert.True(GetWindowRect(outline.Handle, out var bounds));
        Assert.Equal((x, y, width, height), (bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top));
        var region = CreateRectRgn(0, 0, 0, 0);
        try
        {
            Assert.NotEqual(0, GetWindowRgn(outline.Handle, region));
            Assert.False(PtInRegion(region, width / 2, height / 2));
            if (width > 4 && height > 4) Assert.True(PtInRegion(region, 0, height / 2));
        }
        finally { DeleteObject(region); }
    }

    [AvaloniaFact]
    public void ExternalCloseRequestsStopOnceButNormalDisposalDoesNot()
    {
        if (!OperatingSystem.IsWindows()) return;
        var stops = 0;
        using (var outline = new RecordingIndicator(new CaptureArea(0, 0, 100, 100), () => stops++))
        {
            SendMessageW(outline.Handle, 0x10, UIntPtr.Zero, IntPtr.Zero);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(IntPtr.Zero, outline.Handle);
            Assert.Equal(1, stops);
        }
        using (var outline = new RecordingIndicator(new CaptureArea(0, 0, 100, 100), () => stops++))
        {
            var handle = outline.Handle;
            outline.Dispose(); outline.Dispose();
            Assert.False(IsWindow(handle));
        }
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, stops);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtrW(IntPtr window, int index);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowDisplayAffinity(IntPtr window, out uint affinity);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr window, IntPtr region);
    [DllImport("user32.dll")] private static extern IntPtr SendMessageW(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(IntPtr window);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PtInRegion(IntPtr region, int x, int y);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr value);
}
