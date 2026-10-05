#if DEBUG
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Phraseback.Client;

namespace Phraseback.App.Windows;

/// <summary>Opt-in cross-process pointer check. No engine, recording, or desktop screenshots.</summary>
internal static class IndicatorInputCheck
{
    public static Window? Create(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("--indicator-input-check" or "--indicator-input-overlay")) return null;
        var root = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(root);
        if (args[0] == "--indicator-input-overlay")
        {
            var host = new Window { Width = 1, Height = 1, Opacity = 0, ShowActivated = false, ShowInTaskbar = false };
            RecordingIndicator? outline = null;
            host.Opened += async (_, _) =>
            {
                try
                {
                    var area = JsonSerializer.Deserialize<CaptureArea>(File.ReadAllText(Path.Combine(root, "area.json")), Contract.Json)!;
                    outline = new RecordingIndicator(area, () => host.Close()); outline.Show();
                    File.WriteAllText(Path.Combine(root, "overlay-ready.json"), JsonSerializer.Serialize(new { pid = Environment.ProcessId, handle = outline.Handle.ToInt64() }));
                    await Task.Delay(TimeSpan.FromSeconds(25));
                }
                finally { outline?.Dispose(); host.Close(); }
            };
            host.Closed += (_, _) => outline?.Dispose();
            return host;
        }

        var status = new TextBlock { Text = "Start, then click CENTER and EDGE. Auto-closes in 25 seconds.", TextWrapping = TextWrapping.Wrap, Height = 48 };
        var start = new Button { Content = "Start input check" };
        var center = new Button { Content = "CENTER", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var edge = new Button { Content = "EDGE", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var layout = new StackPanel { Margin = new Thickness(24), Spacing = 24, Children = { status, start, center, edge } };
        var window = new Window { Title = "Phraseback isolated input check", Width = 520, Height = 350,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = Brushes.DimGray, Content = layout };
        window.Opened += async (_, _) => { await Task.Delay(TimeSpan.FromSeconds(30)); window.Close(); };
        Process? child = null;
        var centerClicks = 0; var edgeClicks = 0; var started = false;
        void Save()
        {
            var live = child is { HasExited: false } && File.Exists(Path.Combine(root, "overlay-ready.json"));
            File.WriteAllText(Path.Combine(root, "input-check.json"), JsonSerializer.Serialize(new { centerClicks, edgeClicks, overlayLive = live, pid = Environment.ProcessId }));
            status.Text = $"CENTER: {centerClicks} · EDGE: {edgeClicks} · separate overlay live: {live}";
        }
        center.Click += (_, _) => { centerClicks++; Save(); };
        edge.Click += (_, _) => { edgeClicks++; Save(); };
        async Task Start()
        {
            if (started) return;
            started = true; start.IsEnabled = false;
            // The center button lies in the hole; the bottom native border bisects EDGE.
            var topLeft = window.PointToScreen(new Point(16, 16));
            var edgeCenter = edge.PointToScreen(new Point(edge.Bounds.Width / 2, edge.Bounds.Height / 2));
            var right = window.PointToScreen(new Point(window.ClientSize.Width - 16, 0));
            var area = new CaptureArea(topLeft.X, topLeft.Y, right.X - topLeft.X, edgeCenter.Y - topLeft.Y + 1);
            File.WriteAllText(Path.Combine(root, "area.json"), JsonSerializer.Serialize(area, Contract.Json));
            var launch = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            launch.ArgumentList.Add("--indicator-input-overlay"); launch.ArgumentList.Add(root);
            child = Process.Start(launch);
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(root, "overlay-ready.json")) && deadline.Elapsed < TimeSpan.FromSeconds(3)) await Task.Delay(50);
            if (!window.IsVisible) return;
            window.Activate(); Save();
            await Task.Delay(TimeSpan.FromSeconds(25) - deadline.Elapsed);
            window.Close();
        }
        start.Click += async (_, _) => await Start();
        window.Opened += async (_, _) => await Start();
        window.Closed += (_, _) => { if (child is { HasExited: false }) child.Kill(); child?.Dispose(); };
        return window;
    }
}
#endif
