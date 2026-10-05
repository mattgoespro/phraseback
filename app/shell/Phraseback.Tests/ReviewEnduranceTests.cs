using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Phraseback.App;
using Phraseback.Client;
using Xunit;

namespace Phraseback.Tests;

public sealed class ReviewEnduranceTests
{
    [AvaloniaFact]
    public async Task RealDecoderRepeatedSeeksPlateauAndReleaseOnClose()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("FLOW_REVIEW_ENDURANCE") == "1",
            "Opt-in real Skia decode endurance; run scripts/rebuild_review_endurance.ps1.");
        var output = Environment.GetEnvironmentVariable("FLOW_REVIEW_ENDURANCE_OUTPUT")
            ?? throw new InvalidOperationException("Isolated evidence output is required.");
        using var fixture = new Fixture();
        const int width = 1440, height = 900, frameCount = 64, seeks = 1200;
        var frames = new List<Frame>();
        var pixels = new byte[width * height * 4];
        for (var frame = 0; frame < frameCount; frame++)
        {
            for (var p = 0; p < pixels.Length; p += 4)
            { pixels[p] = (byte)(frame * 3); pixels[p + 1] = (byte)(p / 4 % 251); pixels[p + 2] = (byte)(frame * 7); pixels[p + 3] = 255; }
            using var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using (var locked = bitmap.Lock())
            {
                Assert.Equal(width * 4, locked.RowBytes);
                Marshal.Copy(pixels, 0, locked.Address, pixels.Length);
            }
            var relative = $"frames/{frame:D7}.png";
            bitmap.Save(Path.Combine(fixture.Directory, relative), PngBitmapEncoderOptions.Default);
            frames.Add(new Frame(relative, (uint)(frame * 125)));
        }
        var project = new Project(1, "Review endurance", "Synthetic only", "2026-09-23T12:00:00",
            frames.ToArray(), [], frameCount * 125, width, height, "", "ready");
        File.WriteAllText(fixture.ProjectPath, JsonSerializer.Serialize(project, Contract.Json));
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        var samples = new List<Sample>();
        using var process = Process.GetCurrentProcess();
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < seeks; i++)
        {
            // Coprime stride traverses all 64 frames, exceeding the cache's capacity.
            await workspace.SeekAsync(i * 17 % frameCount);
            Assert.NotNull(workspace.Preview);
            Assert.Equal(new PixelSize(width, height), workspace.Preview.PixelSize);
            Assert.True(clock.Elapsed < TimeSpan.FromMinutes(3), "Review endurance deadline exceeded.");
            if (i % 20 == 0)
            {
                process.Refresh();
                var metrics = workspace.PerformanceSnapshot();
                samples.Add(new Sample(i, clock.Elapsed.TotalMilliseconds, process.PrivateMemorySize64,
                    process.WorkingSet64, metrics.PreviewBytes));
            }
        }
        var final = workspace.PerformanceSnapshot();
        await workspace.DisposeAsync();
        var released = workspace.PerformanceSnapshot();
        long P95(IEnumerable<long> values) { var sorted = values.Order().ToArray(); return sorted[(int)Math.Ceiling(sorted.Length * .95) - 1]; }
        var middle = P95(samples.Where(s => s.Seek >= 400 && s.Seek < 800).Select(s => s.PrivateBytes));
        var last = P95(samples.Where(s => s.Seek >= 800).Select(s => s.PrivateBytes));
        var passed = final.PreviewPeakBytes <= 125L * 1024 * 1024 && final.PreviewPeakBytes > 100L * 1024 * 1024
            && final.PreviewEvictions > 1000 && final.FailedSeeks == 0
            && last - middle <= 32L * 1024 * 1024 && released.PreviewBytes == 0;
        await File.WriteAllTextAsync(Path.Combine(output, "review-endurance.json"), JsonSerializer.Serialize(new
        {
            passed, seeks, frameCount, width, height, samples, final, released,
            middle_private_p95 = middle, final_private_p95 = last, growth_bytes = last - middle,
            allowed_growth_bytes = 32L * 1024 * 1024,
            limitations = "Real CPU Skia decoding through Workspace/engine, synthetic PNGs, no window/GPU rendering or desktop capture. Cache accounting zero is not a claim that the OS returns all process memory immediately."
        }, Contract.Json));
        Assert.True(passed, "Review cache/memory acceptance failed; inspect retained report.");
        Assert.Null(workspace.Preview);
    }

    private sealed record Sample(int Seek, double ElapsedMs, long PrivateBytes, long WorkingSetBytes, long CacheBytes);
}
