using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace Phraseback.App;

public sealed partial class MainWindow
{
    private PromptSettingsView? promptSettings;
    private CancellationTokenSource? promptRefresh;
    private long promptRequest;
    private string? taskRecording;
    private readonly SemaphoreSlim taskSaves = new(1);
    private bool taskDirty, loadingTask;
    internal async Task<Phraseback.Client.Project?> ReadRecordingDetailsAsync()
    {
        if (!await SaveTaskAsync() || !await Workspace.FlushAsync()) return null;
        return Workspace.Snapshot?.Project;
    }
    internal async Task ShowSettingsAsync()
    {
        try
        {
            promptSettings ??= new PromptSettingsView(Workspace, QueuePromptRefresh);
            await promptSettings.LoadAsync();
            if (SettingsPage.Content is null)
            {
                var promptTab = new Button { Content = "Prompt format" }; var modelTab = new Button { Content = "Local model" };
                var back = new Button { Content = "Back to workspace", Classes = { "subtle" } };
                var content = new ContentControl { Content = promptSettings };
                modelSettings = new ContentControl { Content = new ModelSettingsView(Workspace) };
                promptTab.Click += (_, _) => { content.Content = promptSettings; promptTab.Classes.Add("accent"); modelTab.Classes.Remove("accent"); };
                modelTab.Click += (_, _) => { content.Content = modelSettings; modelTab.Classes.Add("accent"); promptTab.Classes.Remove("accent"); };
                back.Click += (_, _) => ShowStage(settingsReturnStage);
                promptTab.Classes.Add("accent");
                var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(28, 18), Children = { promptTab, modelTab, back } };
                var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
                grid.Children.Add(tabs); Grid.SetRow(content, 1); grid.Children.Add(content);
                SettingsPage.Content = grid;
            }
        }
        catch (Exception ex) { if (!closing) await ShowPanel("Settings unavailable", ex.Message); }
    }
    private void QueuePromptRefresh()
    {
        promptRefresh?.Cancel(); promptRefresh?.Dispose(); promptRefresh = new CancellationTokenSource();
        _ = DebouncedPromptAsync(promptRefresh.Token, ++promptRequest);
    }
    private async Task DebouncedPromptAsync(CancellationToken cancellation, long request)
    {
        try
        {
            await Task.Delay(180, cancellation);
            if (closing || cancellation.IsCancellationRequested || !ReviewPage.IsVisible || Workspace.Busy) return;
            await RefreshPromptAsync(request);
        }
        catch (OperationCanceledException) { }
    }
    internal async Task RefreshPromptAsync(long? request = null)
    {
        var identity = request ?? ++promptRequest;
        try
        {
            var prompt = await Workspace.RenderPromptAsync();
            if (closing || identity != promptRequest || prompt is null) return;
            PromptText.Text = prompt.Markdown;
            if (taskRecording != prompt.RecordingId || !taskDirty)
            {
                loadingTask = true;
                TaskInput.Text = Workspace.Snapshot?.Project.Task ?? ""; taskRecording = prompt.RecordingId; taskDirty = false;
                loadingTask = false;
            }
            PromptContext.Text = Workspace.Snapshot?.Project.Context ?? "";
        }
        catch (Exception ex) { if (identity == promptRequest) PromptText.Text = $"Prompt unavailable: {ex.Message}"; }
    }
    private async Task<bool> SaveTaskAsync()
    {
        await taskSaves.WaitAsync();
        try
        {
        if (Workspace.Snapshot is not { } snapshot || taskRecording != snapshot.RecordingId) return true;
        if ((TaskInput.Text ?? "") == snapshot.Project.Task) return true;
        var value = TaskInput.Text ?? "";
        var saved = await Workspace.EditProjectAsync(snapshot.Project.Title, snapshot.Project.Context, value);
        if (saved && TaskInput.Text == value) taskDirty = false;
        return saved;
        }
        finally { taskSaves.Release(); }
    }
    private async void TaskLostFocus(object? sender, RoutedEventArgs e) { if (await SaveTaskAsync()) QueuePromptRefresh(); }
    private async void CopyPromptClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!await SaveTaskAsync()) return;
            var prompt = await Workspace.RenderPromptAsync(flush: true);
            if (prompt is null || Clipboard is null) return;
            await Clipboard.SetTextAsync(prompt.Markdown); PromptText.Text = prompt.Markdown;
            CopyPromptButton.Content = "Copied";
            await Task.Delay(1200); CopyPromptButton.Content = "Copy prompt";
        }
        catch (Exception ex) { if (!closing) await ShowPanel("Prompt not copied", ex.Message); }
    }
}
