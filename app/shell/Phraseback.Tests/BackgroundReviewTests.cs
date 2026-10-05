using System.Text.Json;
using Avalonia.Headless.XUnit;
using Phraseback.App;
using Phraseback.Client;
using Xunit;

namespace Phraseback.Tests;

public sealed class BackgroundReviewTests
{
    [AvaloniaFact]
    public async Task LibraryLoadsInPagesAndReconnectFindsProjectOutsideFirstPage()
    {
        using var fixture = new Fixture();
        var project = Read(fixture) with { Frames = [], Steps = [], Created = "2026-09-01" };
        for (var i = 0; i < 139; i++)
        {
            var directory = Path.Combine(fixture.Root, "sessions", $"older-{i:D3}");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "project.json"), JsonSerializer.Serialize(project with { Title = $"Older {i}" }, Contract.Json));
        }
        await using var workspace = new Workspace();
        var pid = 0;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, id => pid = id);
        Assert.Equal(64, workspace.Library.Count);
        Assert.True(workspace.CanLoadMore);
        await workspace.LoadMoreRecordingsAsync();
        Assert.Equal(128, workspace.Library.Count);
        await workspace.LoadMoreRecordingsAsync();
        Assert.Equal(140, workspace.Library.Count);
        Assert.False(workspace.HasMoreRecordings);
        var older = workspace.Library[^1];
        Assert.True(await workspace.OpenAsync(older));
        using var child = System.Diagnostics.Process.GetProcessById(pid);
        child.Kill(entireProcessTree: true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (workspace.IsConnected) await Task.Delay(10, deadline.Token);
        Assert.True(await workspace.ReconnectAsync(), workspace.Error);
        Assert.Equal(older.Id, workspace.Snapshot!.RecordingId);
        Assert.Equal(64, workspace.Library.Count);
    }

    [AvaloniaFact]
    public async Task BackgroundOrganizingKeepsReviewEditableAndNeverReplacesInFlightDraft()
    {
        using var fixture = new Fixture();
        var project = Read(fixture);
        project = project with { Frames = Enumerable.Range(0, 4000).Select(i => new Frame(project.Frames[0].File, i * 125)).ToArray(), DurationMs = 500000 };
        File.WriteAllText(fixture.ProjectPath, JsonSerializer.Serialize(project, Contract.Json));
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        await workspace.OrganizeAsync();
        Assert.False(workspace.Busy);
        Assert.True(workspace.CanEdit);
        workspace.Action = "Draft typed while organizing";
        await workspace.OrganizationCompletion.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal("Draft typed while organizing", workspace.Action);
        Assert.Equal("first", workspace.Selected!.Step.Id);
        Assert.True(await workspace.FlushAsync(), workspace.Error);
        Assert.Equal("Draft typed while organizing", Read(fixture).Steps[0].Action);
    }

    [Fact]
    public async Task BackgroundNotificationsDoNotConsumeForegroundNotifications()
    {
        using var fixture = new Fixture();
        await using var client = await EngineClient.StartAsync(Fixture.Engine, fixture.Root);
        var snapshot = await client.RequestSnapshotAsync("open_project", new { recording_id = "sample" }, TestContext.Current.CancellationToken);
        var background = await client.RequestAsync<OperationStatus>("organize_background", new { recording_id = "sample", revision = snapshot.Revision }, TestContext.Current.CancellationToken);
        var foreground = await client.RequestAsync<OperationStatus>("generate", new { recording_id = "sample", revision = snapshot.Revision }, TestContext.Current.CancellationToken);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!background.Finished) background = await client.NextOrganizationAsync(background.Id, deadline.Token);
        while (!foreground.Finished) foreground = await client.NextOperationAsync(foreground.Id, deadline.Token);
        Assert.True(background.Finished);
        Assert.True(foreground.Finished);
        Assert.NotEqual(background.Id, foreground.Id);
    }

    private static Project Read(Fixture fixture) => JsonSerializer.Deserialize<Project>(File.ReadAllText(fixture.ProjectPath), Contract.Json)!;

    [AvaloniaFact]
    public async Task ShutdownCancelsBackgroundObserverAndReleasesProjectOwnership()
    {
        using var fixture = new Fixture();
        var project = Read(fixture);
        project = project with { Frames = Enumerable.Range(0, 4000).Select(i => new Frame(project.Frames[0].File, i * 125)).ToArray(), DurationMs = 500000 };
        File.WriteAllText(fixture.ProjectPath, JsonSerializer.Serialize(project, Contract.Json));
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        await workspace.OrganizeAsync();
        await workspace.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
        await workspace.OrganizationCompletion.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await using var reopened = await EngineClient.StartAsync(Fixture.Engine, fixture.Root);
        var snapshot = await reopened.RequestSnapshotAsync("open_project", new { recording_id = "sample" }, TestContext.Current.CancellationToken);
        Assert.Equal(4000, snapshot.FrameCount);
    }
}
