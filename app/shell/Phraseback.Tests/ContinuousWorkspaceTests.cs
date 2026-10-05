using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Phraseback.App;
using Xunit;

namespace Phraseback.Tests;

public sealed class ContinuousWorkspaceTests
{
    [AvaloniaFact]
    public async Task RecordingDetailsUseTheLatestTaskDraft()
    {
        using var fixture = new Fixture();
        var window = new MainWindow();
        await using var workspace = window.Workspace;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        await window.RefreshPromptAsync();
        window.FindControl<TextBox>("TaskInput")!.Text = "Newest task";
        var details = await window.ReadRecordingDetailsAsync();
        Assert.Equal("Newest task", details!.Task);
    }
    [AvaloniaFact]
    public async Task ChoosingAlreadyLoadedRecordingLeavesReadyForReview()
    {
        using var fixture = new Fixture();
        var window = new MainWindow();
        await using var workspace = window.Workspace;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        Assert.True(window.FindControl<Control>("ReadyPage")!.IsVisible);
        window.FindControl<ListBox>("LibraryList")!.SelectedItem = workspace.Library[0];
        await Task.Delay(100);
        Assert.True(window.FindControl<Control>("ReviewPage")!.IsVisible);
    }
    [AvaloniaFact]
    public void LaunchShowsReadyAndSidebarNavigation()
    {
        var window = new MainWindow();
        Assert.True(window.FindControl<Control>("ReadyPage")!.IsVisible);
        Assert.False(window.FindControl<Control>("ReviewPage")!.IsVisible);
        Assert.NotNull(window.FindControl<Button>("NewRecordingNavigation"));
        Assert.NotNull(window.FindControl<Button>("SettingsNavigation"));
    }

    [AvaloniaTheory]
    [InlineData(1024, 700)]
    [InlineData(1280, 800)]
    public async Task PromptAndReviewPreserveSelectionAndDraft(int width, int height)
    {
        using var fixture = new Fixture();
        var window = new MainWindow();
        await using var workspace = window.Workspace;
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        var content = (Control)window.Content!; window.Content = null;
        var host = new Window { Width = width, Height = height, Content = content };
        host.Show();
        try
        {
            window.FindControl<Button>("InspectorToggle")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            workspace.Action = "Preserved draft";
            var selected = workspace.Selected?.Step.Id;
            window.FindControl<Button>("GetPromptButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(200);
            Assert.True(window.FindControl<Control>("PromptPanel")!.IsVisible);
            window.FindControl<Button>("ContinueReviewButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(selected, workspace.Selected?.Step.Id);
            Assert.Equal("Preserved draft", workspace.Action);
            Assert.True(window.FindControl<Control>("DescriptionInspector")!.IsVisible);
            Assert.True(window.FindControl<Control>("MomentList")!.IsVisible);
        }
        finally { host.Close(); }
    }
}
