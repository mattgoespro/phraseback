using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Automation.Peers;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Phraseback.App;
using Phraseback.Client;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Phraseback.Tests.TestApp))]

namespace Phraseback.Tests;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var realDrawing = Environment.GetEnvironmentVariable("FLOW_REVIEW_ENDURANCE") == "1";
        var builder = AppBuilder.Configure<App.App>();
        if (realDrawing) builder.UseSkia();
        return builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = !realDrawing });
    }
}

public sealed class PrototypeTests
{
    [AvaloniaFact]
    public async Task HidingDetailsPreservesSelectedMomentAndDraft()
    {
        using var fixture = new Fixture();
        var window = new MainWindow();
        await using var workspace = window.Workspace;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        var content = Assert.IsAssignableFrom<Control>(window.Content);
        window.Content = null;
        content.DataContext = workspace;
        var host = new Window { Width = 1024, Height = 700, Content = content };
        host.Show();
        try
        {
            var selected = workspace.Selected?.Step.Id;
            workspace.Action = "Draft survives panel changes";
            var toggle = window.FindControl<Button>("InspectorToggle")!;
            var inspector = window.FindControl<Control>("DescriptionInspector")!;
            Assert.False(inspector.IsVisible);
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(inspector.IsVisible);
            Assert.Equal(selected, workspace.Selected?.Step.Id);
            Assert.Equal("Draft survives panel changes", workspace.Action);
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(inspector.IsVisible);
        }
        finally { host.Close(); }
    }

    [AvaloniaFact]
    public async Task MomentStatusDistinguishesGeneratedAndManualDescriptions()
    {
        using var fixture = new Fixture();
        var project = JsonSerializer.Deserialize<Project>(File.ReadAllText(fixture.ProjectPath), Contract.Json)!;
        project = project with { Steps = [project.Steps[0] with { Status = "generated", Action = "Open settings", Result = "Settings appear" }] };
        File.WriteAllText(fixture.ProjectPath, JsonSerializer.Serialize(project, Contract.Json));
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        Assert.Equal("Generated · review", workspace.Moments[0].StatusLabel);
        await workspace.ReviewAsync();
        Assert.Equal("Reviewed · generated", workspace.Moments[0].StatusLabel);
    }

    [AvaloniaFact]
    public void RecordingAndMomentRowsExposeHumanReadableNames()
    {
        var window = new MainWindow();
        window.ShowStage(WorkspaceStage.Review);
        var content = Assert.IsAssignableFrom<Control>(window.Content);
        window.Content = null;
        var library = window.FindControl<ListBox>("LibraryList")!;
        var moments = window.FindControl<ListBox>("MomentList")!;
        library.ItemsSource = new[] { new LibraryEntry("private-id", "Settings demo", "2026-09-25", 2000, 1) };
        moments.ItemsSource = new[] { new Moment(new Step(0, "step-id", "Save settings", "Action details", "Result details", "edited", true, false, ""), 1, "00:01.000", "Manual") };
        var host = new Window { Width = 1024, Height = 700, Content = content };
        host.Show(); host.UpdateLayout();
        try
        {
            var selector = window.FindControl<Button>("RecordingSelector")!;
            selector.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            host.UpdateLayout();
            var libraryRow = Assert.IsType<ListBoxItem>(library.ContainerFromIndex(0));
            var momentRow = Assert.IsType<ListBoxItem>(moments.ContainerFromIndex(0));
            Assert.Equal("Settings demo", ControlAutomationPeer.CreatePeerForElement(libraryRow)!.GetName());
            Assert.Equal("1. Save settings. 00:01.000. Manual", ControlAutomationPeer.CreatePeerForElement(momentRow)!.GetName());
        }
        finally { host.Close(); }
    }

    [Fact]
    public void SharedGoldenHandshakeHasExpectedVersionAndMethod()
    {
        using var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "contracts", "hello.request.json")));
        Assert.Equal(Contract.Major, golden.RootElement.GetProperty("protocol").GetInt32());
        Assert.Equal("hello", golden.RootElement.GetProperty("method").GetString());
    }

    [Theory]
    [InlineData("hello.response.json", false)]
    [InlineData("error.response.json", true)]
    public void SharedGoldenResponsesDeserialize(string file, bool error)
    {
        var envelope = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "contracts", file)), Contract.Json)!;
        Assert.Equal((uint)Contract.Major, envelope.Protocol);
        Assert.Equal(error, envelope.Error is not null);
        Assert.Equal(!error, envelope.Result is not null);
        Assert.NotEmpty(envelope.Session);
    }

    [Fact]
    public void EmptyWorkspaceStartsWithoutAnEditableDraft()
    {
        var workspace = new Workspace();
        Assert.False(workspace.HasProject);
        Assert.False(workspace.CanEdit);
        Assert.Empty(workspace.Moments);
    }

    [AvaloniaTheory]
    [InlineData(1280, 800)]
    [InlineData(1024, 700)]
    public void LayoutKeepsPreviewAndInspectorWithinWindow(int width, int height)
    {
        var window = new MainWindow { Width = width, Height = height };
        window.ShowStage(WorkspaceStage.Review);
        var content = Assert.IsAssignableFrom<Control>(window.Content);
        // Attach the real workspace content so control templates and styles participate
        // in layout, without triggering MainWindow's engine startup event.
        window.Content = null;
        var host = new Window { Width = width, Height = height, Content = content };
        host.Show();
        host.UpdateLayout();
        try
        {
            content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height));
            Assert.NotNull(window.FindControl<ListBox>("MomentList"));
            Assert.NotNull(window.FindControl<ListBox>("LibraryList"));
            foreach (var name in new[] { "MomentList", "EvidencePreviewPanel" })
            {
                var control = window.FindControl<Control>(name)!;
                var origin = control.TranslatePoint(new Point(0, 0), content)!.Value;
                Assert.InRange(control.Bounds.Width, 1, width);
                Assert.InRange(control.Bounds.Height, 1, height);
                Assert.InRange(origin.X, 0, width);
                Assert.InRange(origin.Y, 0, height);
                Assert.InRange(origin.X + control.Bounds.Width, 0, width);
                Assert.InRange(origin.Y + control.Bounds.Height, 0, height);
            }
            Assert.False(window.FindControl<Control>("DescriptionInspector")!.IsVisible);
        }
        finally { host.Close(); }
    }

    [Theory]
    [InlineData("Generating", "Cancelled")]
    [InlineData("Save failure", "Ready")]
    [InlineData("Model absent", "Settings")]
    [InlineData("Empty library", "Recording setup")]
    public void DesignStatesHaveExplicitTransitionsWithoutAnEngine(string before, string after)
    {
        var states = new PrototypeStates { SelectedCase = before };
        Assert.True(states.ShowAction);
        states.Act();
        Assert.Equal(after, states.SelectedCase);
    }

    [AvaloniaTheory]
    [InlineData("Generating")]
    [InlineData("Save failure")]
    [InlineData("Settings")]
    [InlineData("Recording setup")]
    [InlineData("Grouped alternatives")]
    [InlineData("Empty library")]
    public void DesignStatesBuildAndFitTheirWindow(string state)
    {
        var window = new PrototypeWindow(state);
        // The headless native window reports its own default client size until shown.
        // Measure the content directly to test the layout without opening a native window.
        var content = Assert.IsAssignableFrom<Control>(window.Content);
        content.Measure(new Size(960, 680)); content.Arrange(new Rect(0, 0, 960, 680));
        Assert.Equal(state, window.States.SelectedCase);
        Assert.InRange(content.DesiredSize.Width, 1, 960);
        Assert.InRange(content.DesiredSize.Height, 1, 680);
    }

    [Fact]
    public async Task RealEngineAcknowledgesDurableEditsAndRejectsStaleRevision()
    {
        using var fixture = new Fixture();
        await using var client = await EngineClient.StartAsync(Fixture.Engine, fixture.Root);
        var idle = await client.RequestAsync<JsonElement>("operation_status", new { }, TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Null, idle.ValueKind);
        var snapshot = await client.RequestAsync<Snapshot>("open_project", new { recording_id = "sample" }, TestContext.Current.CancellationToken);
        var updated = await client.RequestAsync<Snapshot>("edit_step", new
        {
            recording_id = "sample", revision = snapshot.Revision, step_id = "first",
            title = "A revised title", action = "Manual action", result = "Visible result", uncertainty = ""
        }, TestContext.Current.CancellationToken);
        Assert.Equal(snapshot.Revision + 1, updated.Revision);
        var onDisk = JsonSerializer.Deserialize<Project>(File.ReadAllText(fixture.ProjectPath), Contract.Json)!;
        Assert.Equal("Manual action", onDisk.Steps[0].Action);
        Assert.True(onDisk.Steps[0].Manual);
        Assert.True(File.Exists(Path.Combine(fixture.Directory, "legacy-backup-rust-v1", "project.json")));
        var failure = await Assert.ThrowsAsync<EngineException>(() => client.RequestAsync<Snapshot>("review_step", new { recording_id = "sample", revision = snapshot.Revision, step_id = "first", reviewed = true }, TestContext.Current.CancellationToken));
        Assert.Equal("stale_revision", failure.Code);
    }

    [Fact]
    public async Task RejectedDuplicateSelectionLeavesMetadataRevisionAndChoiceMarkerUntouched()
    {
        using var fixture = new Fixture();
        var project = JsonSerializer.Deserialize<Project>(File.ReadAllText(fixture.ProjectPath), Contract.Json)!;
        project = project with {
            Frames = [project.Frames[0], new Frame(project.Frames[0].File, 125)],
            Steps = [project.Steps[0], new Step(1, "second", "Second", "Manual note", "", "edited", true, true, "")],
            DurationMs = 250
        };
        File.WriteAllText(fixture.ProjectPath, JsonSerializer.Serialize(project, Contract.Json));
        var original = File.ReadAllBytes(fixture.ProjectPath);
        await using var client = await EngineClient.StartAsync(Fixture.Engine, fixture.Root);
        var snapshot = await client.RequestAsync<Snapshot>("open_project", new { recording_id = "sample" }, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<EngineException>(() => client.RequestAsync<Snapshot>("replace_step",
            new { recording_id = "sample", revision = snapshot.Revision, step_id = "second", frame = 0 }, TestContext.Current.CancellationToken));
        Assert.Equal(original, File.ReadAllBytes(fixture.ProjectPath));
        Assert.False(File.Exists(Path.Combine(fixture.Directory, ".rust-selection-edited")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Directory, "legacy-backup-rust-v1")));
        var image = await client.RequestAsync<ImageReference>("frame", new { recording_id = "sample", index = 0 }, TestContext.Current.CancellationToken);
        Assert.Equal(snapshot.Revision, image.Revision);
    }

    [AvaloniaFact]
    public async Task FailedSaveRetainsViewModelDraftAndBlocksNavigation()
    {
        using var fixture = new Fixture();
        File.WriteAllText(Path.Combine(fixture.Directory, "legacy-backup-rust-v1"), "intentional failure");
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        Assert.NotNull(workspace.Preview);
        workspace.Action = "Keep this unsaved draft";
        Assert.False(await workspace.FlushAsync());
        Assert.Equal("Keep this unsaved draft", workspace.Action);
        Assert.True(workspace.HasError);
        Assert.False(await workspace.SelectAsync(workspace.Moments[0]));
        Assert.Equal("Keep this unsaved draft", workspace.Action);
    }

    [Fact]
    public async Task EnginePushesOperationCompletionWithoutPollingAndAcceptsRequestsMeanwhile()
    {
        using var fixture = new Fixture();
        await using var client = await EngineClient.StartAsync(Fixture.Engine, fixture.Root);
        var snapshot = await client.RequestSnapshotAsync("open_project", new { recording_id = "sample" }, TestContext.Current.CancellationToken);
        var operation = await client.RequestAsync<OperationStatus>("organize", new { recording_id = "sample", revision = snapshot.Revision }, TestContext.Current.CancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var capabilities = await client.RequestAsync<JsonElement>("capabilities", new { }, deadline.Token);
        Assert.True(capabilities.GetProperty("export").GetBoolean());
        do { operation = await client.NextOperationAsync(operation.Id, deadline.Token); } while (!operation.Finished);
        Assert.Equal("completed", operation.State);
        Assert.Equal(snapshot.Revision, operation.Revision);
        Assert.Equal("sample", operation.RecordingId);
    }

    [AvaloniaFact]
    public async Task RapidSeeksAndPrefetchDoNotLeaveAnObsoleteImageOrBlockShutdown()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        var seeks = Enumerable.Range(0, 40).Select(_ => workspace.SeekAsync(0));
        await Task.WhenAll(seeks);
        Assert.NotNull(workspace.Preview);
        Assert.False(workspace.HasError, workspace.Error);
        await workspace.DisposeAsync();
        Assert.Null(workspace.Preview);
    }

    [Fact]
    public void SharedGoldenNotificationCarriesOperationIdentityAndTerminalState()
    {
        var notification = JsonSerializer.Deserialize<EngineNotification>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "contracts", "operation.notification.json")), Contract.Json)!;
        Assert.Equal("operation_status", notification.Event);
        var status = notification.Data.Deserialize<OperationStatus>(Contract.Json)!;
        Assert.Equal("golden-operation", status.Id);
        Assert.True(status.Finished);
        Assert.Equal(3, status.Revision);
    }

    [Fact]
    public async Task GenerationSavesAreDeliveredBeforeCompletionEvenBeyondTheWorkerQueueSize()
    {
        using var fixture = new Fixture();
        var project = JsonSerializer.Deserialize<Project>(File.ReadAllText(fixture.ProjectPath), Contract.Json)!;
        project = project with {
            Frames = Enumerable.Range(0, 12).Select(i => new Frame("frames/0000000.png", i * 125)).ToArray(),
            Steps = Enumerable.Range(0, 12).Select(i => new Step(i, $"step-{i}", "Pending", "", "", "pending", false, false, "")).ToArray(),
            DurationMs = 1500
        };
        File.WriteAllText(fixture.ProjectPath, JsonSerializer.Serialize(project, Contract.Json));
        await using var client = await EngineClient.StartAsync(Fixture.Engine, fixture.Root);
        var snapshot = await client.RequestSnapshotAsync("open_project", new { recording_id = "sample" }, TestContext.Current.CancellationToken);
        var operation = await client.RequestAsync<OperationStatus>("generate", new { recording_id = "sample", revision = snapshot.Revision }, TestContext.Current.CancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var acknowledged = new HashSet<string>();
        try { do
        {
            operation = await client.NextOperationAsync(operation.Id, deadline.Token);
            if (operation.Result is { } result && result.TryGetProperty("saved_step", out var step))
                acknowledged.Add(step.GetProperty("id").GetString()!);
        } while (!operation.Finished); }
        catch (OperationCanceledException)
        {
            var status = await client.RequestAsync<OperationStatus>("operation_status", new { }, TestContext.Current.CancellationToken);
            throw new Xunit.Sdk.XunitException($"Timed out after {acknowledged.Count} save acknowledgements; last={operation.State}, current={status.State}, finished={status.Finished}, progress={status.Progress}.");
        }
        Assert.Equal(12, acknowledged.Count);
        Assert.Equal(12, operation.Result!.Value.GetProperty("failed").GetInt32());
        var saved = JsonSerializer.Deserialize<Project>(File.ReadAllText(fixture.ProjectPath), Contract.Json)!;
        Assert.All(saved.Steps, step => Assert.Equal("failed", step.Status));
    }

    [AvaloniaFact]
    public async Task WorkspaceDecodesEvidenceAndReopensAcknowledgedDraft()
    {
        using var fixture = new Fixture();
        await using (var workspace = new Workspace())
        {
            await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
            Assert.False(workspace.HasError, workspace.Error);
            Assert.Equal(2, workspace.Preview!.PixelSize.Width);
            workspace.Action = "Saved through the view model";
            Assert.True(await workspace.FlushAsync());
            Assert.Equal("Saved through the view model", workspace.Moments[0].Step.Action);
            await workspace.ReviewAsync();
            Assert.True(workspace.Moments[0].Step.Reviewed);
        }
        await using var reopened = new Workspace();
        await reopened.ConnectAsync(Fixture.Engine, fixture.Root, null);
        Assert.Equal("Saved through the view model", reopened.Action);
        Assert.True(reopened.Moments[0].Step.Reviewed);
    }

    [AvaloniaFact]
    public async Task MissingEvidenceClearsOldPreviewWithoutDiscardingAnnotations()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        Assert.NotNull(workspace.Preview);
        workspace.Action = "Retain my explanation";
        Assert.True(await workspace.FlushAsync());
        File.Delete(Path.Combine(fixture.Directory, "frames", "0000000.png"));
        await workspace.SeekAsync(0);
        Assert.Null(workspace.Preview);
        Assert.StartsWith("Preview unavailable:", workspace.Error);
        Assert.Equal("Retain my explanation", workspace.Action);
        Assert.True(workspace.CanEdit);
    }

    [AvaloniaFact]
    public async Task CurationAndContextEditsPersistWithoutDeletingEvidence()
    {
        using var fixture = new Fixture();
        var project = JsonSerializer.Deserialize<Project>(File.ReadAllText(fixture.ProjectPath), Contract.Json)!;
        var second = Path.Combine(fixture.Directory, "frames", "0000001.png");
        File.Copy(Path.Combine(fixture.Directory, "frames", "0000000.png"), second);
        project = project with { Frames = [project.Frames[0], new Frame("frames/0000001.png", 125)], DurationMs = 250 };
        File.WriteAllText(fixture.ProjectPath, JsonSerializer.Serialize(project, Contract.Json));
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        workspace.Action = "My protected wording";
        await workspace.SeekAsync(1);
        Assert.True(await workspace.ReplaceCurrentFrameAsync());
        Assert.Equal("My protected wording", workspace.Action);
        Assert.Equal(1, workspace.Selected!.Step.Frame);
        Assert.Equal("stale", workspace.Selected.Step.Status);
        await workspace.SeekAsync(0);
        Assert.True(await workspace.AddCurrentFrameAsync());
        Assert.Equal(2, workspace.Moments.Count);
        Assert.True(await workspace.RemoveSelectedAsync());
        Assert.Single(workspace.Moments);
        Assert.True(File.Exists(second));
        Assert.True(await workspace.EditProjectAsync("Renamed recording", "Useful context"));
        var saved = JsonSerializer.Deserialize<Project>(File.ReadAllText(fixture.ProjectPath), Contract.Json)!;
        Assert.Equal("Renamed recording", saved.Title);
        Assert.Equal("Useful context", saved.Context);
        Assert.Equal("My protected wording", saved.Steps[0].Action);
    }

    [AvaloniaFact]
    public async Task PreviewCacheEvictsWithinItsPixelBudget()
    {
        using var fixture = new Fixture();
        using var cache = new PreviewCache(4);
        var path = Path.Combine(fixture.Directory, "frames", "0000000.png");
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        cache.Put("one", new Avalonia.Media.Imaging.Bitmap(path));
        cache.Put("two", new Avalonia.Media.Imaging.Bitmap(path));
        Assert.Equal(4, cache.Bytes);
        Assert.Equal(1, cache.Count);
        Assert.Equal(4, cache.PeakBytes);
        Assert.Equal(1, cache.Evictions);
        Assert.Null(cache.Get("one"));
        Assert.NotNull(cache.Get("two"));
        using var speculative = new Avalonia.Media.Imaging.Bitmap(path);
        Assert.False(cache.TryPutPrefetched("three", speculative, cache.Get("two")));
        Assert.NotNull(cache.Get("two"));
    }
}

internal sealed class Fixture : IDisposable
{
    public static string Engine => Environment.GetEnvironmentVariable("FLOW_REBUILD_ENGINE")
        ?? throw new InvalidOperationException("Run scripts/rebuild.ps1 -Task Test or set FLOW_REBUILD_ENGINE.");
    public string Root { get; } = Path.Combine(Environment.GetEnvironmentVariable("FLOW_REBUILD_TEST_ROOT") ?? Path.Combine(Path.GetTempPath(), "PhrasebackRebuildTests"), Guid.NewGuid().ToString("N"));
    public string Directory => Path.Combine(Root, "sessions", "sample");
    public string ProjectPath => Path.Combine(Directory, "project.json");
    public Fixture()
    {
        System.IO.Directory.CreateDirectory(Path.Combine(Directory, "frames"));
        File.WriteAllText(Path.Combine(Root, ".flow-recorder-development"), "");
        // A valid tiny PNG is enough to exercise asynchronous preview decoding.
        File.WriteAllBytes(Path.Combine(Directory, "frames", "0000000.png"), Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEklEQVR4nGPUsAlgYGBgYgADAAhyALhorTR/AAAAAElFTkSuQmCC"));
        var project = new Project(1, "Fixture", "", "2026-09-20T12:00:00", [new Frame("frames/0000000.png", 0)],
            [new Step(0, "first", "First", "", "", "pending", false, false, "")], 125, 2, 2, "", "ready");
        File.WriteAllText(ProjectPath, JsonSerializer.Serialize(project, Contract.Json));
    }
    public void Dispose() => System.IO.Directory.Delete(Root, recursive: true);
}
