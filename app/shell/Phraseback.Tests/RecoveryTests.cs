using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Phraseback.App;
using Phraseback.Client;
using Xunit;

namespace Phraseback.Tests;

public sealed class RecoveryTests
{
    [AvaloniaFact]
    public async Task UnopenableNewestRecordingKeepsLibraryConnectedAndOtherProjectsEditable()
    {
        using var fixture = new Fixture();
        var broken = Path.Combine(fixture.Root, "sessions", "broken");
        Directory.CreateDirectory(broken);
        var bad = ReadProject(fixture) with { Created = "2099-01-01T00:00:00", Steps = [ReadProject(fixture).Steps[0] with { Frame = 999 }] };
        File.WriteAllText(Path.Combine(broken, "project.json"), JsonSerializer.Serialize(bad, Contract.Json));
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        Assert.True(workspace.IsConnected);
        Assert.False(workspace.HasProject);
        Assert.True(workspace.HasError);
        Assert.Equal(2, workspace.Library.Count);
        Assert.True(await workspace.OpenAsync(workspace.Library.Single(p => p.Id == "sample")));
        Assert.False(workspace.HasError);
        workspace.Action = "Other recordings still work";
        Assert.True(await workspace.FlushAsync(), workspace.Error);
        Assert.Equal(workspace.Action, ReadProject(fixture).Steps[0].Action);
        Assert.False(await workspace.OpenAsync(workspace.Library.Single(p => p.Id == "broken")));
        Assert.Equal("sample", workspace.Snapshot!.RecordingId);
        workspace.Action = "Failed opening preserves the prior revision";
        Assert.True(await workspace.FlushAsync(), workspace.Error);
        Assert.Equal(workspace.Action, ReadProject(fixture).Steps[0].Action);
    }

    [Fact]
    public void StartupPathsPreferExplicitPathsThenEnvironmentThenUserDefaults()
    {
        using var fixture = new Fixture();
        var explicitPaths = StartupPaths.Resolve(["--engine", Fixture.Engine, "--data-root", fixture.Root], "ignored", "ignored", "ignored");
        Assert.Equal(Path.GetFullPath(Fixture.Engine), explicitPaths.Engine);
        Assert.Equal(fixture.Root, explicitPaths.DataRoot);
        explicitPaths.ValidateDevelopmentRoot();
        var environment = StartupPaths.Resolve([], fixture.Root, fixture.Root, "ignored");
        Assert.Equal(Path.Combine(fixture.Root, "Phraseback.Engine.exe"), environment.Engine);
        Assert.Equal(fixture.Root, environment.DataRoot);
        var defaults = StartupPaths.Resolve([], fixture.Root, null, fixture.Root);
        Assert.Equal(Path.Combine(fixture.Root, "Phraseback"), defaults.DataRoot);
        Assert.Throws<IOException>(defaults.ValidateDevelopmentRoot);
        Assert.False(Directory.Exists(defaults.DataRoot));
        Assert.Throws<ArgumentException>(() => StartupPaths.Resolve(["--data-root"], fixture.Root, null, fixture.Root));
        Assert.Throws<ArgumentException>(() => StartupPaths.Resolve(["--engine", "--data-root", fixture.Root], fixture.Root, null, fixture.Root));
    }

    [Fact]
    public void RebrandingKeepsExistingLibraryAndExplicitOverride()
    {
        using var fixture = new Fixture();
        var legacy = Path.Combine(fixture.Root, "FlowRecorder");
        Directory.CreateDirectory(legacy);
        var sentinel = Path.Combine(legacy, "recording-sentinel.txt");
        File.WriteAllText(sentinel, "existing recording");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "Phraseback"));

        Assert.Equal(legacy, StartupPaths.Resolve([], fixture.Root, null, fixture.Root).DataRoot);
        Assert.Equal(fixture.Root, StartupPaths.Resolve(["--data-root", fixture.Root], fixture.Root, legacy, fixture.Root).DataRoot);
        Assert.Equal(fixture.Root, StartupPaths.Resolve([], fixture.Root, fixture.Root, fixture.Root).DataRoot);
        Assert.Equal("existing recording", File.ReadAllText(sentinel));
        Assert.Equal("new override", StartupPaths.DataOverride("new override", "legacy override"));
        Assert.Equal("legacy override", StartupPaths.DataOverride(null, "legacy override"));
        Assert.Equal("legacy override", StartupPaths.DataOverride(" ", "legacy override"));
    }

    [AvaloniaFact]
    public async Task IdleCrashIsVisibleAndReconnectRestoresSavedProject()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        var pid = 0;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, id => pid = id);
        workspace.Action = "Durable before crash";
        Assert.True(await workspace.FlushAsync());
        await CrashAsync(workspace, pid);
        Assert.True(workspace.CanReconnect);
        Assert.False(workspace.CanEdit);
        Assert.Contains("disconnected", workspace.Activity);
        var originalPid = pid;
        Assert.True(await workspace.ReconnectAsync(), workspace.Error);
        Assert.NotEqual(originalPid, pid);
        Assert.Equal("Durable before crash", workspace.Action);
        Assert.NotNull(workspace.Preview);
        Assert.False(workspace.HasUnsavedDraft);
        Assert.True(workspace.CanEdit);
    }

    [AvaloniaFact]
    public async Task UnsavedDraftSurvivesCrashAndReconnectUntilExplicitSave()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        var pid = 0;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, id => pid = id);
        var backupBlock = Path.Combine(fixture.Directory, "legacy-backup-rust-v1");
        File.WriteAllText(backupBlock, "test-only backup failure");
        workspace.Action = "Retained offline draft";
        Assert.False(await workspace.FlushAsync());
        await CrashAsync(workspace, pid);
        File.Delete(backupBlock);
        Assert.False(await workspace.OpenAsync(workspace.Library[0]));
        Assert.True(await workspace.ReconnectAsync(), workspace.Error);
        Assert.Equal("Retained offline draft", workspace.Action);
        Assert.True(workspace.HasUnsavedDraft);
        Assert.Equal("", ReadProject(fixture).Steps[0].Action);
        Assert.True(await workspace.FlushAsync(), workspace.Error);
        Assert.Equal("Retained offline draft", ReadProject(fixture).Steps[0].Action);
        Assert.False(workspace.HasUnsavedDraft);
    }

    [AvaloniaTheory]
    [InlineData("description")]
    [InlineData("context")]
    [InlineData("selection")]
    public async Task ConflictingSavedStateNeverSilentlyOverwritesDraftOrEvidence(string change)
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        var pid = 0;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, id => pid = id);
        await CrashAsync(workspace, pid);
        workspace.Action = "Keep this draft";
        var project = ReadProject(fixture);
        project = change switch
        {
            "context" => project with { Context = "Different evidence context" },
            "selection" => project with { Steps = [] },
            _ => project with { Steps = [project.Steps[0] with { Action = "Other writer", Manual = true, Status = "edited" }] }
        };
        WriteProject(fixture, project);
        var originalBytes = File.ReadAllBytes(fixture.ProjectPath);
        Assert.False(await workspace.ReconnectAsync());
        Assert.True(workspace.RecoveryConflict);
        Assert.True(workspace.CanReconnect);
        Assert.Equal("Keep this draft", workspace.Action);
        Assert.False(await workspace.FlushAsync());
        Assert.Equal(originalBytes, File.ReadAllBytes(fixture.ProjectPath));
        Assert.True(await workspace.ReconnectAsync(discardDraft: true), workspace.Error);
        Assert.False(workspace.RecoveryConflict);
        Assert.False(workspace.HasUnsavedDraft);
        Assert.Equal(project.Steps.FirstOrDefault()?.Action ?? "", workspace.Action);
        Assert.Equal(originalBytes, File.ReadAllBytes(fixture.ProjectPath));
    }

    [AvaloniaFact]
    public async Task MatchingCommittedDraftCanRecoverAfterLostAcknowledgement()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        var pid = 0;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, id => pid = id);
        await CrashAsync(workspace, pid);
        workspace.Action = "Committed without acknowledgement";
        var project = ReadProject(fixture);
        project.Steps[0] = project.Steps[0] with { Action = workspace.Action, Status = "edited", Manual = true, Reviewed = false };
        WriteProject(fixture, project);
        Assert.True(await workspace.ReconnectAsync(), workspace.Error);
        Assert.True(await workspace.FlushAsync(), workspace.Error);
        Assert.Equal("Committed without acknowledgement", ReadProject(fixture).Steps[0].Action);
    }

    [AvaloniaFact]
    public async Task FailedReconnectRetainsDraftAndDoesNotStealLiveOwnerLock()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        var pid = 0;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, id => pid = id);
        await CrashAsync(workspace, pid);
        workspace.Action = "Retain across failed retries";
        await using (var owner = await EngineClient.StartAsync(Fixture.Engine, fixture.Root))
        {
            Assert.False(await workspace.ReconnectAsync());
            Assert.Equal("Retain across failed retries", workspace.Action);
            await owner.RequestAsync<Library>("list_projects", new { }, TestContext.Current.CancellationToken);
        }
        Assert.True(await workspace.ReconnectAsync(), workspace.Error);
        Assert.True(await workspace.FlushAsync());
    }

    [AvaloniaFact]
    public async Task InitialConnectionFailureCanBeRetriedWithoutRestartingShell()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        await using (var owner = await EngineClient.StartAsync(Fixture.Engine, fixture.Root))
        {
            await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
            Assert.False(workspace.IsConnected);
            Assert.True(workspace.CanReconnect);
        }
        Assert.True(await workspace.ReconnectAsync(), workspace.Error);
        Assert.True(workspace.HasProject);
    }

    [AvaloniaFact]
    public async Task MissingProjectRetainsDraftAndRequiresExplicitDiscardBeforeEmptyLibrary()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        var pid = 0;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, id => pid = id);
        await CrashAsync(workspace, pid);
        workspace.Action = "Text from unavailable recording";
        // Move only this test-owned project, retaining every evidence file.
        Directory.Move(fixture.Directory, Path.Combine(fixture.Root, "retained-project"));
        Assert.False(await workspace.ReconnectAsync());
        Assert.True(workspace.RecoveryConflict);
        Assert.Equal("Text from unavailable recording", workspace.Action);
        Assert.True(await workspace.ReconnectAsync(discardDraft: true), workspace.Error);
        Assert.False(workspace.HasProject);
        Assert.False(workspace.HasUnsavedDraft);
        Assert.Empty(workspace.Library);
        Assert.Empty(workspace.Moments);
    }

    [AvaloniaTheory]
    [InlineData(1024, 700)]
    [InlineData(1280, 800)]
    public async Task ConflictRecoveryActionsRemainVisibleAndKeyboardFocusable(int width, int height)
    {
        using var fixture = new Fixture();
        var window = new MainWindow();
        await using var workspace = window.Workspace;
        var pid = 0;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, id => pid = id);
        await CrashAsync(workspace, pid);
        workspace.Action = "Unsaved conflict";
        WriteProject(fixture, ReadProject(fixture) with { Context = "Changed context" });
        Assert.False(await workspace.ReconnectAsync());
        var content = Assert.IsAssignableFrom<Control>(window.Content);
        window.Content = null;
        var host = new Window { Width = width, Height = height, Content = content, DataContext = workspace };
        host.Show(); host.UpdateLayout();
        try
        {
            content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height));
            foreach (var name in new[] { "ReconnectButton", "RecoveryConflictButton" })
            {
                var button = window.FindControl<Button>(name)!;
                Assert.True(button.IsVisible);
                Assert.True(button.IsEnabled);
                Assert.True(button.Focus());
                var origin = button.TranslatePoint(new Point(0, 0), content)!.Value;
                Assert.InRange(origin.X, 0, width);
                Assert.InRange(origin.X + button.Bounds.Width, 0, width);
                Assert.InRange(origin.Y + button.Bounds.Height, 0, height);
            }
        }
        finally { host.Close(); }
    }

    private static Project ReadProject(Fixture fixture) => JsonSerializer.Deserialize<Project>(File.ReadAllText(fixture.ProjectPath), Contract.Json)!;
    private static void WriteProject(Fixture fixture, Project project) => File.WriteAllText(fixture.ProjectPath, JsonSerializer.Serialize(project, Contract.Json));
    private static async Task CrashAsync(Workspace workspace, int pid)
    {
        using var child = Process.GetProcessById(pid);
        child.Kill(entireProcessTree: true);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        await child.WaitForExitAsync(deadline.Token);
        while (workspace.IsConnected) await Task.Delay(10, deadline.Token);
    }
}
