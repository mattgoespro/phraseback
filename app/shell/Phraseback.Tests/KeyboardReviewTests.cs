using System.Text.Json;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Phraseback.App;
using Phraseback.Client;
using Xunit;

namespace Phraseback.Tests;

public sealed class KeyboardReviewTests
{
    [AvaloniaTheory]
    [InlineData(1024, 700)]
    [InlineData(1280, 800)]
    public async Task TabReachesReviewControlsAndArrowSelectionFlushesTypedDraft(int width, int height)
    {
        using var fixture = new Fixture();
        var project = JsonSerializer.Deserialize<Project>(File.ReadAllText(fixture.ProjectPath), Contract.Json)!;
        File.Copy(Path.Combine(fixture.Directory, "frames", "0000000.png"), Path.Combine(fixture.Directory, "frames", "0000001.png"));
        project = project with
        {
            Frames = [project.Frames[0], new Frame("frames/0000001.png", 125)],
            Steps = [project.Steps[0], new Step(1, "second", "Second", "", "", "pending", false, false, "")],
            DurationMs = 250
        };
        File.WriteAllText(fixture.ProjectPath, JsonSerializer.Serialize(project, Contract.Json));
        var window = new MainWindow();
        await using var workspace = window.Workspace;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        var content = Assert.IsAssignableFrom<Control>(window.Content);
        window.Content = null;
        content.DataContext = workspace;
        var host = new Window { Width = width, Height = height, Content = content };
        host.Show(); host.UpdateLayout();
        try
        {
            var selector = window.FindControl<Button>("RecordingSelector")!;
            Assert.True(selector.Focus(NavigationMethod.Tab));
            Assert.Equal("Choose recording", ControlAutomationPeer.CreatePeerForElement(selector)!.GetName());
            var toggle = window.FindControl<Button>("InspectorToggle")!;
            toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            host.UpdateLayout();
            Assert.True(window.FindControl<Control>("DescriptionInspector")!.IsVisible);

            var action = content.GetVisualDescendants().OfType<TextBox>().Single(t => ControlAutomationPeer.CreatePeerForElement(t)!.GetName() == "Action description");
            Assert.True(action.Focus(NavigationMethod.Tab));
            host.KeyTextInput("Keyboard-authored description");
            Assert.Equal("Keyboard-authored description", workspace.Action);
            Assert.True(workspace.HasUnsavedDraft);
            var moments = window.FindControl<ListBox>("MomentList")!;
            Assert.True(Assert.IsType<ListBoxItem>(moments.ContainerFromIndex(0)).Focus(NavigationMethod.Tab));
            Press(host, Key.Down);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (workspace.Selected?.Step.Id != "second" && DateTime.UtcNow < deadline) await Task.Delay(10);
            Assert.Equal("second", workspace.Selected?.Step.Id);
            Assert.Equal(1, workspace.FrameIndex);
            var saved = JsonSerializer.Deserialize<Project>(File.ReadAllText(fixture.ProjectPath), Contract.Json)!;
            Assert.Equal("Keyboard-authored description", saved.Steps[0].Action);
            Assert.True(saved.Steps[0].Manual);
            Assert.False(workspace.HasUnsavedDraft);
            Assert.Empty(workspace.Error);
        }
        finally { host.Close(); }
    }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.UpdateLayout();
    }
}
