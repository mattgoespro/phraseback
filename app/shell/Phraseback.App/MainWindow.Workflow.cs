using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Phraseback.App.Windows;

namespace Phraseback.App;

internal enum WorkspaceStage { Ready, Recording, Review, PromptResults, Settings }

public sealed partial class MainWindow
{
    private WorkspaceStage stage = WorkspaceStage.Ready;
    private WorkspaceStage settingsReturnStage = WorkspaceStage.Ready;
    private bool reviewInspectorBeforePrompt;
    private readonly DispatcherTimer recordingClock = new() { Interval = TimeSpan.FromSeconds(1) };
    private long recordingStarted;
    private ContentControl? modelSettings;
    private void InitializeContinuousWorkspace()
    {
        ReadyPage.Content = new RecordingSetupView([]);
        TaskInput.TextChanged += (_, _) => { if (!loadingTask) taskDirty = true; };
        recordingClock.Tick += (_, _) => RecordingElapsed.Text = $"Recording · {TimeSpan.FromMilliseconds(Environment.TickCount64 - recordingStarted):mm\\:ss}";
        Workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Workspace.HasProject) or nameof(Workspace.Activity) or nameof(Workspace.HasUnsavedDraft))
                QueuePromptRefresh();
        };
        ShowStage(WorkspaceStage.Ready);
    }
    internal void ShowStage(WorkspaceStage next)
    {
        if (next == WorkspaceStage.Settings && stage != WorkspaceStage.Settings) settingsReturnStage = stage;
        stage = next;
        ReadyPage.IsVisible = next == WorkspaceStage.Ready;
        RecordingPage.IsVisible = next == WorkspaceStage.Recording;
        ReviewPage.IsVisible = next is WorkspaceStage.Review or WorkspaceStage.PromptResults;
        SettingsPage.IsVisible = next == WorkspaceStage.Settings;
        StageLabel.Text = next switch { WorkspaceStage.Ready => "Ready to record", WorkspaceStage.Recording => "Recording", WorkspaceStage.PromptResults => "Prompt results", WorkspaceStage.Settings => "Settings", _ => "Review" };
        NewRecordingNavigation.Classes.Set("accent", next is WorkspaceStage.Ready or WorkspaceStage.Recording);
        RecordingsNavigation.Classes.Set("accent", next is WorkspaceStage.Review or WorkspaceStage.PromptResults);
        SettingsNavigation.Classes.Set("accent", next == WorkspaceStage.Settings);
        NewRecordingNavigation.IsEnabled = RecordingsNavigation.IsEnabled = SettingsNavigation.IsEnabled = next != WorkspaceStage.Recording;
        MoreToggle.IsEnabled = RecordingSelector.IsEnabled = next != WorkspaceStage.Recording;
        UpdatePromptLayout();
        QueuePromptRefresh();
    }
    private void UpdatePromptLayout()
    {
        if (ReviewBody is null) return;
        var visible = !inspectorOpen || stage == WorkspaceStage.PromptResults;
        PromptPanel.IsVisible = visible;
        ReviewBody.ColumnDefinitions[1].Width = new GridLength(visible ? 5 : 0);
        ReviewBody.ColumnDefinitions[2].Width = visible ? stage == WorkspaceStage.PromptResults ? new GridLength(1.5, GridUnitType.Star) : new GridLength(320) : new GridLength(0);
        ReviewBody.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        GetPromptButton.IsVisible = stage != WorkspaceStage.PromptResults;
        ContinueReviewButton.IsVisible = stage == WorkspaceStage.PromptResults;
        CopyPromptButton.Classes.Set("accent", stage == WorkspaceStage.PromptResults);
    }
    private async Task LoadCaptureSetupAsync()
    {
        try
        {
            var setup = new RecordingSetupView(await Workspace.ScreensAsync());
            setup.StartRequested += async value =>
            {
                if (recordingTask is not null || Workspace.Busy || closing) return;
                try { recordingTask = RunRecordingAsync(value); await recordingTask; }
                catch (Exception ex) { if (!closing) await ShowPanel("Recording unavailable", ex.Message); }
                finally { recordingTask = null; }
            };
            ReadyPage.Content = setup;
        }
        catch (Exception ex) { if (!closing) await ShowPanel("Capture setup unavailable", ex.Message); }
    }
    private async void StopRecordingClick(object? sender, RoutedEventArgs e)
    { recordingCountdown?.Cancel(); await Workspace.CancelOperationAsync(); }
    private async void GetPromptClick(object? sender, RoutedEventArgs e)
    {
        StopPlayback();
        if (!await SaveTaskAsync() || !await Workspace.FlushAsync()) return;
        reviewInspectorBeforePrompt = inspectorOpen;
        SetInspectorOpen(false); ShowStage(WorkspaceStage.PromptResults);
        await RefreshPromptAsync();
    }
    private void ContinueReviewClick(object? sender, RoutedEventArgs e)
    { ShowStage(WorkspaceStage.Review); SetInspectorOpen(reviewInspectorBeforePrompt); }
}
