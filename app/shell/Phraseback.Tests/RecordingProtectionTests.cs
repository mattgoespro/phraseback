using System.ComponentModel;
using Avalonia.Headless.XUnit;
using Phraseback.App;
using Phraseback.App.Windows;
using Phraseback.Client;
using Xunit;

namespace Phraseback.Tests;

[CollectionDefinition("Native stop shortcut", DisableParallelization = true)]
public sealed class StopShortcutCollection;

[Collection("Native stop shortcut")]
public sealed class RecordingProtectionTests
{
    [AvaloniaFact]
    public async Task RecordingStartConflictLeavesWorkspaceEnabledAndEvidenceUntouched()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows shortcut readiness check.");
        using var fixture = new Fixture();
        var window = new MainWindow(); // Never show a window or request desktop capture.
        await using var workspace = window.Workspace;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        var screens = await workspace.ScreensAsync();
        Assert.SkipWhen(screens.Length == 0, "Display enumeration unavailable on this runner.");
        var screen = screens[0];
        var setup = new RecordingSetup(screen,
            new CaptureArea(screen.Left, screen.Top, Math.Min(100, screen.Width), Math.Min(100, screen.Height)),
            "Must not record", "Shortcut conflict fixture");
        var original = File.ReadAllBytes(fixture.ProjectPath);
        var source = File.ReadAllBytes(Path.Combine(fixture.Directory, "frames", "0000000.png"));
        using (var owner = new RecordingProtection(() => Assert.Fail("No input was sent.")))
        {
            await owner.Ready;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var error = await Assert.ThrowsAsync<Win32Exception>(() => window.RunRecordingAsync(setup));
                Assert.Contains("Ctrl+Shift+F9 is unavailable", error.Message);
                Assert.True(window.IsEnabled);
                Assert.False(workspace.Busy);
                Assert.True(workspace.IsConnected);
                Assert.Single(Directory.GetDirectories(Path.Combine(fixture.Root, "sessions")));
                Assert.Equal(original, File.ReadAllBytes(fixture.ProjectPath));
                Assert.Equal(source, File.ReadAllBytes(Path.Combine(fixture.Directory, "frames", "0000000.png")));
            }
        }
        // The failed path did not retain its hotkey; preparing again remains usable.
        using var replacement = new RecordingProtection(() => { });
        await replacement.Ready;
        Assert.NotEmpty(await workspace.PrepareCaptureAsync(screen, setup.Area, "Retry", "No capture"));
        Assert.False(workspace.Busy);
    }

    [Fact]
    public async Task ConflictIsReportedWithoutStealingTheExistingRegistration()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var owner = new RecordingProtection(() => Assert.Fail("No keyboard input was sent."));
        await owner.Ready;
        using (var conflict = new RecordingProtection(() => Assert.Fail("Conflicting shortcut fired.")))
        {
            var error = await Assert.ThrowsAsync<Win32Exception>(() => conflict.Ready);
            Assert.Contains("Ctrl+Shift+F9 is unavailable", error.Message);
        }
        // Disposing a failed contender must not unregister the original owner.
        using (var conflict = new RecordingProtection(() => { }))
            await Assert.ThrowsAsync<Win32Exception>(() => conflict.Ready);
        owner.Dispose();
        using var replacement = new RecordingProtection(() => { });
        await replacement.Ready;
    }

    [Fact]
    public async Task ImmediateDisposalDoesNotLeaveTheShortcutRegistered()
    {
        if (!OperatingSystem.IsWindows()) return;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            using var early = new RecordingProtection(() => Assert.Fail("No keyboard input was sent."));
            early.Dispose();
            early.Dispose();
            // Observe startup completion too: a cancelled early startup is valid,
            // but silently failing because a previous iteration leaked is not.
            try { await early.Ready; }
            catch (OperationCanceledException) { }
            using var next = new RecordingProtection(() => { });
            await next.Ready;
        }
    }
}
