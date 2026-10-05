using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Phraseback.App;
using Phraseback.Client;
using Xunit;

namespace Phraseback.Tests;

public sealed class ReviewResourcesTests
{
    [AvaloniaFact]
    public async Task HorizontalMomentRibbonRecyclesThumbnailsForLongRecordings()
    {
        using var fixture = new Fixture();
        var window = new MainWindow();
        window.ShowStage(WorkspaceStage.Review);
        await using var workspace = window.Workspace;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        var seed = workspace.Moments[0];
        for (var i = 1; i < 100; i++) workspace.Moments.Add(seed with { Number = i + 1 });
        var content = (Control)window.Content!;
        window.Content = null;
        content.DataContext = workspace;
        var host = new Window { Width = 1024, Height = 700, Content = content };
        host.Show();
        try
        {
            var ribbon = window.FindControl<ListBox>("MomentList")!;
            host.UpdateLayout();
            var scroll = ribbon.GetVisualDescendants().OfType<ScrollViewer>().Single();
            await Task.Delay(100);
            host.UpdateLayout();
            scroll.Offset = new Avalonia.Vector(scroll.Extent.Width - scroll.Viewport.Width, 0);
            host.UpdateLayout();
            var last = Assert.IsType<ListBoxItem>(ribbon.ContainerFromIndex(99));
            var thumbnail = last.GetVisualDescendants().OfType<MomentThumbnail>().Single();
            await Task.Delay(100);
            await thumbnail.Loading;
            Assert.True(thumbnail.Source is not null, $"Last thumbnail missing; count={workspace.ThumbnailCount}, offset={scroll.Offset}, extent={scroll.Extent}, viewport={scroll.Viewport}");
            Assert.InRange(workspace.ThumbnailCount, 1, 32);
            Assert.InRange(workspace.ThumbnailBytes, 1, 3 * 1024 * 1024);
        }
        finally { host.Close(); }
        Assert.Equal(0, workspace.ThumbnailCount);
    }

    [Fact]
    public async Task WindowPreferencesRoundTripWithoutChangingEvidenceOrRevision()
    {
        using var fixture = new Fixture();
        var before = File.ReadAllBytes(fixture.ProjectPath);
        await using (var client = await EngineClient.StartAsync(Fixture.Engine, fixture.Root))
        {
            var project = await client.RequestSnapshotAsync("open_project", new { recording_id = "sample" }, TestContext.Current.CancellationToken);
            var defaults = await client.RequestAsync<UiPreferences>("ui_preferences", new { }, TestContext.Current.CancellationToken);
            Assert.Equal(1280u, defaults.Width);
            Assert.Equal("dark", defaults.Theme);
            var saved = await client.RequestAsync<UiPreferences>("save_ui_preferences", new UiPreferences(1450, 910, true, "dark"), TestContext.Current.CancellationToken);
            Assert.True(saved.Maximized);
            Assert.Equal("dark", saved.Theme);
            await Assert.ThrowsAsync<EngineException>(() => client.RequestAsync<UiPreferences>("save_ui_preferences", new UiPreferences(0, 910, false), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<EngineException>(() => client.RequestAsync<UiPreferences>("save_ui_preferences", new UiPreferences(1450, 910, true, "unknown"), TestContext.Current.CancellationToken));
            // Preference writes do not invalidate a project's asynchronous evidence work.
            var reference = await client.RequestAsync<ImageReference>("frame", new { recording_id = "sample", index = 0 }, TestContext.Current.CancellationToken);
            Assert.Equal(project.Revision, reference.Revision);
        }
        await using var reopened = await EngineClient.StartAsync(Fixture.Engine, fixture.Root);
        Assert.Equal(new UiPreferences(1450, 910, true, "dark"), await reopened.RequestAsync<UiPreferences>("ui_preferences", new { }, TestContext.Current.CancellationToken));
        Assert.Equal(before, File.ReadAllBytes(fixture.ProjectPath));
    }

    [Fact]
    public async Task CorruptPreferencesRecoverAndFailedPublicationReturnsAnError()
    {
        using var fixture = new Fixture();
        var path = Path.Combine(fixture.Root, "ui-preferences-rust-v1.json");
        File.WriteAllText(path, "{");
        await using var client = await EngineClient.StartAsync(Fixture.Engine, fixture.Root);
        Assert.Equal(800u, (await client.RequestAsync<UiPreferences>("ui_preferences", new { }, TestContext.Current.CancellationToken)).Height);
        Assert.Equal("{", File.ReadAllText(path));
        File.Delete(path); Directory.CreateDirectory(path);
        await Assert.ThrowsAsync<EngineException>(() => client.RequestAsync<UiPreferences>("save_ui_preferences", new UiPreferences(1280, 800, false), TestContext.Current.CancellationToken));
    }

    [AvaloniaFact]
    public async Task RealizedThumbnailReleasesPixelsOnRecycleAndDetach()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        var thumbnail = new MomentThumbnail { DataContext = workspace.Moments[0] };
        var host = new Window { Content = thumbnail };
        host.Show();
        try
        {
            await thumbnail.Loading;
            Assert.NotNull(thumbnail.Source);
            Assert.Equal(1, workspace.ThumbnailCount);
            thumbnail.DataContext = null;
            Assert.Null(thumbnail.Source);
            Assert.Equal(0, workspace.ThumbnailBytes);
            thumbnail.DataContext = workspace.Moments[0];
            host.Content = null;
            await thumbnail.Loading;
            Assert.Null(thumbnail.Source);
            Assert.Equal(0, workspace.ThumbnailCount);
        }
        finally { host.Close(); }
    }

    [AvaloniaFact]
    public async Task ThumbnailReservationsStayBoundedAndCancellationDoesNotBreakTransport()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        var moment = workspace.Moments[0];
        var leases = new List<ThumbnailLease>();
        try
        {
            for (var i = 0; i < 32; i++) leases.Add(Assert.IsType<ThumbnailLease>(await workspace.LoadThumbnailAsync(moment)));
            Assert.Null(await workspace.LoadThumbnailAsync(moment));
            Assert.InRange(workspace.ThumbnailBytes, 1, 3 * 1024 * 1024);
            leases[0].Dispose(); leases[0].Dispose();
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workspace.LoadThumbnailAsync(moment, cancelled.Token));
            using var replacement = await workspace.LoadThumbnailAsync(moment);
            Assert.NotNull(replacement);
            Assert.True(workspace.IsConnected);
            Assert.Null(await workspace.LoadThumbnailAsync(moment with { RecordingId = "different" }));
        }
        finally { foreach (var lease in leases) lease.Dispose(); }
        Assert.Equal(0, workspace.ThumbnailCount);
    }

    [Fact]
    public void TimingWindowsHaveBoundedStorageAndNearestRankPercentiles()
    {
        var timings = new TimingWindow(100);
        Assert.Null(timings.Summary().P95Ms);
        for (var i = 1; i <= 1000; i++) timings.Add(i);
        var summary = timings.Summary();
        Assert.Equal(1000, summary.TotalSamples);
        Assert.Equal(100, summary.RetainedSamples);
        Assert.Equal(995, summary.P95Ms);
        Assert.Equal(1000, summary.MaximumMs);
        Assert.Equal(Enumerable.Range(901, 100).Select(value => (double)value), timings.Samples());
        Assert.Throws<ArgumentOutOfRangeException>(() => timings.Add(double.NaN));
    }

    [AvaloniaFact]
    public async Task DispatcherProbeMeasuresPostedWorkAndStopsWithoutWaitingForTheUi()
    {
        var samples = new TimingWindow();
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var probe = new DispatcherProbe(value => { samples.Add(value); first.TrySetResult(); });
        await first.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        probe.Dispose();
        await probe.Completion.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        var count = samples.Summary().TotalSamples;
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(count, samples.Summary().TotalSamples);
    }

    [AvaloniaFact]
    public async Task CorruptDerivedThumbnailsFallBackWithoutChangingOriginals()
    {
        using var fixture = new Fixture();
        var original = Path.Combine(fixture.Directory, "frames", "0000000.png");
        var before = File.ReadAllBytes(original);
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        using (var first = await workspace.LoadThumbnailAsync(workspace.Moments[0])) Assert.NotNull(first);
        var directory = Path.Combine(fixture.Directory, "previews-rust-v1");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!Directory.Exists(directory) || Directory.GetFiles(directory, "*.png").Length < 2)
            await Task.Delay(20, deadline.Token);
        foreach (var file in Directory.GetFiles(directory, "*.png")) File.WriteAllText(file, "corrupt disposable preview");
        using var recovered = await workspace.LoadThumbnailAsync(workspace.Moments[0]);
        Assert.NotNull(recovered);
        Assert.Equal(before, File.ReadAllBytes(original));
        Assert.True(workspace.CanEdit);
    }

    [AvaloniaFact]
    public async Task SeekMetricsSeparateCacheHitsAndRetainNoEvidenceContent()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        for (var i = 0; i < 4; i++) await workspace.SeekAsync(0);
        var report = workspace.PerformanceSnapshot();
        Assert.True(report.CachedSeek.TotalSamples > 0);
        Assert.True(report.UncachedSeek.TotalSamples > 0);
        Assert.True(report.Decode.TotalSamples > 0);
        Assert.InRange(report.PreviewPeakBytes, 1, 125L * 1024 * 1024);
        Assert.Equal(0, report.FailedSeeks);
    }
}
