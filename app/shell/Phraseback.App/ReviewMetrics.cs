using System.Diagnostics;
using Avalonia.Threading;

namespace Phraseback.App;

public sealed record TimingSummary(long TotalSamples, int RetainedSamples, double? P95Ms, double? MaximumMs);
public sealed record ReviewPerformance(TimingSummary CachedSeek, TimingSummary UncachedSeek, TimingSummary Decode,
    TimingSummary Dispatch, long SupersededSeeks, long FailedSeeks, long PreviewBytes, long PreviewPeakBytes,
    long PreviewEvictions, long ThumbnailBytes, int ThumbnailCount, long WorkingSetBytes);
public sealed record MemorySample(double ElapsedMs, long WorkingSetBytes, long PreviewBytes, long ThumbnailBytes);
public sealed record ReviewSamples(double[] CachedSeekMs, double[] UncachedSeekMs, double[] DecodeMs, double[] DispatchMs, MemorySample[] Memory);

/// <summary>Content-free timing windows. Storage stays constant during long recordings.</summary>
public sealed class TimingWindow(int capacity = 1024)
{
    private readonly double[] values = new double[capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity))];
    private long count;
    public void Add(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        values[count++ % values.Length] = milliseconds;
    }
    public TimingSummary Summary()
    {
        var sorted = values.Take((int)Math.Min(count, values.Length)).Order().ToArray();
        return new(count, sorted.Length, sorted.Length == 0 ? null : sorted[(int)Math.Ceiling(sorted.Length * .95) - 1],
            sorted.Length == 0 ? null : sorted[^1]);
    }
    public double[] Samples()
    {
        var retained = (int)Math.Min(count, values.Length);
        var start = count < values.Length ? 0 : (int)(count % values.Length);
        return Enumerable.Range(0, retained).Select(i => values[(start + i) % values.Length]).ToArray();
    }
}

/// <summary>One outstanding probe at most; measures posted-work latency, not timer intervals.</summary>
public sealed class DispatcherProbe : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private int disposed;
    public Task Completion { get; }
    public DispatcherProbe(Action<double> sample)
    {
        var cancellation = stop.Token;
        Completion = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    await Task.Delay(250, cancellation);
                    var started = Stopwatch.GetTimestamp();
                    var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    Dispatcher.UIThread.Post(() =>
                    {
                        try { if (!cancellation.IsCancellationRequested) sample(Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
                        finally { delivered.TrySetResult(); }
                    }, DispatcherPriority.Normal);
                    await delivered.Task.WaitAsync(cancellation);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        });
    }
    public void Dispose()
    { if (Interlocked.Exchange(ref disposed, 1) == 0) { stop.Cancel(); stop.Dispose(); } }
}

public sealed partial class Workspace
{
    private readonly TimingWindow cachedSeeks = new(), uncachedSeeks = new(), decodes = new(), dispatch = new();
    private long supersededSeeks, failedSeeks;
    private readonly Queue<MemorySample> memorySamples = new();
    private readonly long metricsStarted = Stopwatch.GetTimestamp();
    private double lastMemorySample = -1000;
    public void RecordDispatchLatency(double milliseconds)
    {
        dispatch.Add(milliseconds);
        var elapsed = Stopwatch.GetElapsedTime(metricsStarted).TotalMilliseconds;
        if (elapsed - lastMemorySample < 1000) return;
        lastMemorySample = elapsed;
        if (memorySamples.Count == 1024) memorySamples.Dequeue();
        memorySamples.Enqueue(new(elapsed, Environment.WorkingSet, previewCache.Bytes, ThumbnailBytes));
    }
    public ReviewSamples PerformanceSamples() => new(cachedSeeks.Samples(), uncachedSeeks.Samples(), decodes.Samples(), dispatch.Samples(), memorySamples.ToArray());
    public ReviewPerformance PerformanceSnapshot() => new(cachedSeeks.Summary(), uncachedSeeks.Summary(), decodes.Summary(),
        dispatch.Summary(), supersededSeeks, failedSeeks, previewCache.Bytes, previewCache.PeakBytes, previewCache.Evictions,
        ThumbnailBytes, ThumbnailCount, Environment.WorkingSet);
}
