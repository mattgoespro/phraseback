using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System.Diagnostics;
using System.Text.Json;
using Phraseback.Client;

namespace Phraseback.App.Windows;

/// <summary>Opt-in interactive hardware check. Captures only its own opaque synthetic surface.</summary>
internal static class NativeRecordingCheck
{
    public static async Task Run(MainWindow owner, Workspace workspace, string root, int seconds = 5, bool fullDisplay = false, string? expectedStop = null, string workload = "colors")
    {
        if (seconds is < 5 or > 30) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (expectedStop is not (null or "button" or "shortcut")) throw new ArgumentException("Expected button or shortcut.", nameof(expectedStop));
        if (workload is not ("colors" or "detailed")) throw new ArgumentException("Unknown capture workload.", nameof(workload));
        var surface = new Border { Background = Brushes.DarkSlateBlue, Child = new TextBlock {
            Text = "SYNTHETIC CAPTURE CHECK\nNo desktop content is needed", Foreground = Brushes.White, FontSize = 24,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center } };
        var detailed = workload == "detailed" ? new DetailedCaptureSurface() : null;
        var fixture = new Window { Title = "Phraseback synthetic capture check", Width = 640, Height = 400,
            WindowDecorations = WindowDecorations.None, CanResize = false, Topmost = true, Content = (Control?)detailed ?? surface,
            WindowStartupLocation = WindowStartupLocation.CenterScreen };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        var ticks = new List<double>(); var clock = Stopwatch.StartNew(); var previous = clock.Elapsed.TotalMilliseconds; var frame = 0;
        timer.Tick += (_, _) => { var now = clock.Elapsed.TotalMilliseconds; ticks.Add(now - previous); previous = now;
            if (detailed is not null) detailed.Advance();
            else surface.Background = new SolidColorBrush(Color.FromRgb((byte)(40 + frame * 31 % 180), (byte)(30 + frame * 17 % 150), (byte)(50 + frame++ * 47 % 170))); };
        RecordingIndicator? indicator = null;
        var captureDrained = false;
        string? observedStop = null;
        try
        {
            if (fullDisplay) fixture.WindowState = WindowState.FullScreen;
            fixture.Show(); await Task.Delay(500);
            var inset = fullDisplay ? 0 : 4;
            var topLeft = fixture.PointToScreen(new Point(inset, inset)); var bottomRight = fixture.PointToScreen(new Point(fixture.ClientSize.Width - inset, fixture.ClientSize.Height - inset));
            var area = new CaptureArea(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
            var screen = (await workspace.ScreensAsync()).Single(s => area.Left >= s.Left && area.Top >= s.Top && area.Left + area.Width <= s.Left + s.Width && area.Top + area.Height <= s.Top + s.Height);
            var stopped = new Stopwatch();
            if (expectedStop is not null)
            {
                timer.Start();
                await owner.RunRecordingAsync(new RecordingSetup(screen, area, "Production controls check", $"Application-owned synthetic {workload} fixture only"), source =>
                {
                    if (observedStop is not null) return;
                    observedStop = source; stopped.Start();
                }).WaitAsync(TimeSpan.FromSeconds(20));
                if (observedStop != expectedStop) throw new InvalidOperationException($"Expected {expectedStop}; observed {observedStop ?? "no stop action"}.");
                if (!owner.IsEnabled) throw new InvalidOperationException("The studio remained disabled after stopping.");
            }
            else
            {
                using var protection = new RecordingProtection(() => _ = workspace.CancelOperationAsync()); await protection.Ready;
                indicator = new RecordingIndicator(area, () => _ = workspace.CancelOperationAsync()); indicator.Show();
                RecordingProtection.Exclude(owner);
                var token = await workspace.PrepareCaptureAsync(screen, area, "Native synthetic capture check", $"Application-owned synthetic {workload} fixture only");
                timer.Start(); await Task.Delay(3000);
                var recording = workspace.RecordAsync(token); await Task.Delay(TimeSpan.FromSeconds(seconds)); stopped.Start();
                await workspace.CancelOperationAsync(); await recording.WaitAsync(TimeSpan.FromSeconds(15));
            }
            stopped.Stop();
            captureDrained = true;
            // Capture has drained: uncover the desktop before organizing and seek checks.
            timer.Stop(); indicator?.Dispose(); indicator = null; fixture.Close();
            if (workspace.HasError) throw new InvalidOperationException(workspace.Error);
            if (workspace.CapturePreviewCount == 0) throw new InvalidOperationException("Live evidence preview did not decode any saved capture frames.");
            var project = workspace.Snapshot ?? throw new InvalidOperationException("Recording was not reopened.");
            if (project.Project.Frames.Length < 15) throw new InvalidOperationException("The changing fixture did not produce enough unique evidence.");
            var organizationPendingAtReview = workspace.Organizing;
            await workspace.OrganizationCompletion.WaitAsync(TimeSpan.FromSeconds(120));
            // Exercise both first-time and repeated seeks without capturing unrelated desktop content.
            foreach (var index in Enumerable.Range(0, Math.Min(20, project.Project.Frames.Length))) await workspace.SeekAsync(index);
            foreach (var index in Enumerable.Range(0, Math.Min(20, project.Project.Frames.Length)).Reverse()) await workspace.SeekAsync(index);
            // Large originals can be replaced by derivatives between the forward/backward
            // passes, making both passes uncached. Warm a small stable working set explicitly.
            for (var repeat = 0; repeat < 20; repeat++) await workspace.SeekAsync(repeat % 2);
            if (workspace.PerformanceSamples().CachedSeekMs.Length == 0)
                throw new InvalidOperationException("Native review check did not exercise a cached seek.");
            await Task.Delay(300);
            owner.UpdateLayout();
            using (var rendered = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)owner.ClientSize.Width, (int)owner.ClientSize.Height)))
            { rendered.Render(owner); rendered.Save(Path.Combine(root, "review.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default); }
            ticks.Sort();
            var report = new { passed = true, recording_id = project.RecordingId, frames = project.Project.Frames.Length,
                duration_ms = project.Project.DurationMs, width = area.Width, height = area.Height, screen,
                stop_to_review_ms = stopped.Elapsed.TotalMilliseconds, organization_pending = organizationPendingAtReview,
                ui_tick_p95_ms = ticks[(int)(ticks.Count * .95)],
                review_performance = workspace.PerformanceSnapshot(),
                review_samples = workspace.PerformanceSamples(),
                requested_seconds = seconds, full_display = fullDisplay, expected_stop = expectedStop, observed_stop = observedStop, workload,
                note = "Synthetic application-owned workload only, not a trace of another application. Repeated runs, hardware interruption and accessibility checks remain separate acceptance evidence." };
            await File.WriteAllTextAsync(Path.Combine(root, "native-check.json"), JsonSerializer.Serialize(report, Contract.Json));
        }
        catch (Exception ex)
        { await File.WriteAllTextAsync(Path.Combine(root, "native-check.json"), JsonSerializer.Serialize(new { passed = false, error = ex.Message, expected_stop = expectedStop, observed_stop = observedStop }, Contract.Json)); }
        finally
        {
            // Never uncover unrelated desktop content while a failed/timed-out capture
            // could still be running. Disposing the connection stops its owned engine;
            // the launcher's process-tree deadline remains the final fallback.
            if (!captureDrained)
            {
                try { await workspace.CancelOperationAsync(); }
                finally { await workspace.DisposeAsync(); }
            }
            timer.Stop(); indicator?.Dispose(); fixture.Close(); owner.Close();
        }
    }
}
