using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Phraseback.App.Windows;
using Phraseback.Client;

namespace Phraseback.App;

public sealed partial class MainWindow : Window
{
    public Workspace Workspace { get; } = new();
    private readonly string[] arguments;
    private ChildJob? job;
    private bool mayClose, closing, selecting, playing;
    private readonly DispatcherTimer playback = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private long playbackStart;
    private long playbackOffset;
    private CancellationTokenSource? recordingCountdown;
    private Task? recordingTask;
    private double normalWidth = 1280, normalHeight = 800;
    private bool preferencesLoaded;
    private bool inspectorOpen = true;
    private GridLength inspectorWidth = new(320);
    private DispatcherProbe? dispatcherProbe;

    public MainWindow() : this([]) { }
    public MainWindow(string[] args)
    {
        InitializeComponent(); arguments = args; DataContext = Workspace;
        SetInspectorOpen(false);
        InitializeContinuousWorkspace();
        MomentList.AddHandler(InputElement.KeyDownEvent, MomentListKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        SizeChanged += (_, _) =>
        {
            if (preferencesLoaded && WindowState == WindowState.Normal)
            { normalWidth = Width; normalHeight = Height; }
        };
        Opened += async (_, _) =>
        {
            dispatcherProbe = new DispatcherProbe(Workspace.RecordDispatchLatency);
            try
            {
                var paths = StartupPaths.Resolve(arguments, AppContext.BaseDirectory,
                    StartupPaths.DataOverride(Environment.GetEnvironmentVariable("PHRASEBACK_DATA"), Environment.GetEnvironmentVariable("FLOW_RECORDER_DATA")),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
#if DEBUG
                paths.ValidateDevelopmentRoot();
#endif
                if (OperatingSystem.IsWindows()) job = new ChildJob();
                await Workspace.ConnectAsync(paths.Engine, paths.DataRoot, job is null ? null : job.Attach);
                if (Workspace.IsConnected)
                {
                    var preferences = await Workspace.LoadUiPreferencesAsync();
                    var screen = Screens.ScreenFromWindow(this);
                    var availableWidth = screen is null ? 8192 : screen.WorkingArea.Width / screen.Scaling;
                    var availableHeight = screen is null ? 8192 : screen.WorkingArea.Height / screen.Scaling;
                    Width = normalWidth = Math.Clamp(preferences.Width, MinWidth, Math.Max(MinWidth, availableWidth));
                    Height = normalHeight = Math.Clamp(preferences.Height, MinHeight, Math.Max(MinHeight, availableHeight));
                    preferencesLoaded = true;
                    await LoadCaptureSetupAsync();
                    if (preferences.Maximized) WindowState = WindowState.Maximized;
                }
                if (Workspace.IsConnected && arguments.Contains("--native-check"))
                {
                    var durationIndex = Array.IndexOf(arguments, "--native-check-seconds");
                    var seconds = durationIndex < 0 ? 5 : int.Parse(arguments[durationIndex + 1], System.Globalization.CultureInfo.InvariantCulture);
                    var stopIndex = Array.IndexOf(arguments, "--native-check-stop");
                    var stopSource = stopIndex < 0 ? null : arguments[stopIndex + 1];
                    var workloadIndex = Array.IndexOf(arguments, "--native-check-workload");
                    var workload = workloadIndex < 0 ? "colors" : arguments[workloadIndex + 1];
                    await NativeRecordingCheck.Run(this, Workspace, paths.DataRoot, seconds, arguments.Contains("--native-check-full-display"), stopSource, workload);
                }
#if DEBUG
                if (Workspace.IsConnected && arguments.Contains("--workflow-check"))
                    await NativeWorkflowCheck.Run(this, Workspace, paths.DataRoot);
#endif
            }
            catch (Exception ex) { await ShowPanel("Startup failed", ex.Message); }
        };
        Closing += async (_, e) =>
        {
            if (mayClose) return;
            e.Cancel = true; StopPlayback();
            if (closing) return;
            closing = true;
            recordingCountdown?.Cancel();
            if (Workspace.IsOpening) await Workspace.CancelOperationAsync();
            if (recordingTask is not null) { await Workspace.CancelOperationAsync(); await recordingTask; }
            if (!await SaveTaskAsync() || !await Workspace.FlushAsync()) { closing = false; return; }
            if (preferencesLoaded && Workspace.IsConnected)
            {
                try { await Workspace.SaveUiPreferencesAsync(CurrentPreferences()); }
                catch (Exception ex) { closing = false; await ShowPanel("Window preferences not saved", ex.Message); return; }
            }
            IsEnabled = false;
            dispatcherProbe?.Dispose();
            await Workspace.DisposeAsync(); job?.Dispose(); mayClose = true; Close();
        };
        playback.Tick += async (_, _) =>
        {
            var project = Workspace.Snapshot?.Project;
            if (project is null || project.Frames.Length == 0) { StopPlayback(); return; }
            var time = playbackOffset + Environment.TickCount64 - playbackStart;
            var index = Array.FindLastIndex(project.Frames, f => f.TimeMs <= time);
            if (index >= 0 && index != Workspace.FrameIndex) await Workspace.SeekAsync(index);
            if (time >= project.DurationMs) StopPlayback();
        };
    }

    private async void LibraryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LibraryList.SelectedItem is not LibraryEntry entry || Workspace.Busy || recordingTask is not null) return;
        RecordingPopup.IsOpen = false; StopPlayback();
        if (!await SaveTaskAsync()) return;
        if (Workspace.Snapshot?.RecordingId != entry.Id) await Workspace.OpenAsync(entry);
        if (Workspace.HasProject) ShowStage(WorkspaceStage.Review);
    }
    private void RecordingSelectorClick(object? sender, RoutedEventArgs e)
    {
        NavigationPopup.IsOpen = false; MorePopup.IsOpen = false;
        RecordingPopup.PlacementTarget = ReferenceEquals(sender, RecordingsNavigation) ? RecordingsNavigation : RecordingSelector;
        RecordingPopup.IsOpen = !RecordingPopup.IsOpen;
        if (RecordingPopup.IsOpen) { LibraryList.SelectedItem = null; Dispatcher.UIThread.Post(() => LibraryList.Focus()); }
    }
    private void NavigationToggleClick(object? sender, RoutedEventArgs e)
    { RecordingPopup.IsOpen = false; MorePopup.IsOpen = false; NavigationPopup.IsOpen = !NavigationPopup.IsOpen; }
    private void MoreToggleClick(object? sender, RoutedEventArgs e)
    { RecordingPopup.IsOpen = false; NavigationPopup.IsOpen = false; MorePopup.IsOpen = !MorePopup.IsOpen; }
    private async void MomentChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (selecting || MomentList.SelectedItem is not Moment moment || moment.Step.Id == Workspace.Selected?.Step.Id) return;
        selecting = true; StopPlayback();
        try { await Workspace.SelectAsync(moment); MomentList.SelectedItem = Workspace.Selected; }
        finally { selecting = false; }
    }
    private void MomentListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Down or Key.Up) || MomentList.SelectedIndex < 0) return;
        MomentList.SelectedIndex = Math.Clamp(MomentList.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, MomentList.ItemCount - 1);
        e.Handled = true;
    }
    private async void PreviousClick(object? sender, RoutedEventArgs e) { StopPlayback(); await Workspace.SeekAsync(Workspace.FrameIndex - 1); }
    private async void NextClick(object? sender, RoutedEventArgs e) { StopPlayback(); await Workspace.SeekAsync(Workspace.FrameIndex + 1); }
    private void PlayClick(object? sender, RoutedEventArgs e)
    {
        if (playing) { StopPlayback(); return; }
        if (Workspace.Snapshot is null) return;
        playbackOffset = Workspace.Snapshot.Project.Frames.ElementAtOrDefault(Workspace.FrameIndex)?.TimeMs ?? 0;
        playbackStart = Environment.TickCount64; playing = true; PlayButton.Content = "Pause"; playback.Start();
    }
    private void StopPlayback() { playback.Stop(); playing = false; PlayButton.Content = "Play"; }
    private async void ReviewClick(object? sender, RoutedEventArgs e) => await Workspace.ReviewAsync();
    private async void SaveClick(object? sender, RoutedEventArgs e) => await Workspace.FlushAsync();
    private async void ReconnectClick(object? sender, RoutedEventArgs e) { StopPlayback(); await Workspace.ReconnectAsync(); }
    private async void RecoveryConflictClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window { Title = "Recover description draft", Width = 620, Height = 520, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var retained = new TextBox { Text = $"{Workspace.Title}\n\n{Workspace.Action}\n\n{Workspace.Result}\n\n{Workspace.Uncertainty}", IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        Avalonia.Automation.AutomationProperties.SetName(retained, "Retained unsaved description, select and copy before discarding");
        var useSaved = new Button { Content = "Discard this draft and use saved version" };
        var keep = new Button { Content = "Keep draft" };
        useSaved.Click += (_, _) => dialog.Close(true); keep.Click += (_, _) => dialog.Close(false);
        dialog.Content = new Grid { Margin = new Avalonia.Thickness(24), RowDefinitions = new RowDefinitions("Auto,*,Auto"), Children = {
            new TextBlock { Text = "Saved evidence changed. Copy your retained draft below before discarding it. Nothing is overwritten on disk.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            retained, new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, Children = { keep, useSaved } } } };
        Grid.SetRow(retained, 1); Grid.SetRow(((Grid)dialog.Content).Children[2], 2);
        if (await dialog.ShowDialog<bool>(this)) await Workspace.ReconnectAsync(discardDraft: true);
    }
    private async void OriginalClick(object? sender, RoutedEventArgs e)
    {
        StopPlayback();
        try
        {
            using var bitmap = await Workspace.LoadOriginalAsync();
            if (bitmap is null || closing) return;
            var dialog = new Window { Title = "Original pixels", Width = 1000, Height = 720, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            dialog.Content = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = new Image { Source = bitmap, Width = bitmap.PixelSize.Width, Height = bitmap.PixelSize.Height, Stretch = Avalonia.Media.Stretch.Fill } };
            await dialog.ShowDialog(this);
        }
        catch (Exception ex) { await ShowPanel("Preview unavailable", ex.Message); }
    }
    private async void DetailsClick(object? sender, RoutedEventArgs e)
    {
        StopPlayback();
        if (await ReadRecordingDetailsAsync() is not { } project) return;
        var dialog = new Window { Title = "Recording details", Width = 540, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var name = new TextBox { Text = project.Title };
        var task = new TextBox { Text = project.Task, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 70 };
        Avalonia.Automation.AutomationProperties.SetName(task, "User intended task");
        var context = new TextBox { Text = project.Context, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 150 };
        Avalonia.Automation.AutomationProperties.SetName(name, "Recording title");
        Avalonia.Automation.AutomationProperties.SetName(context, "User context, not observed evidence");
        var error = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var save = new Button { Content = "Save details" };
        save.Click += async (_, _) => { save.IsEnabled = false; if (await Workspace.EditProjectAsync(name.Text ?? "", context.Text ?? "", task.Text ?? "")) dialog.Close(); else { error.Text = Workspace.Error; save.IsEnabled = true; } };
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 12, Children = {
            new TextBlock { Text = "Recording title" }, name,
            new TextBlock { Text = "Task (your requested outcome)" }, task,
            new TextBlock { Text = "Context (not observed evidence)" }, context, error, save } };
        await dialog.ShowDialog(this);
    }
    private async void SettingsClick(object? sender, RoutedEventArgs e)
    { if (!Workspace.Busy && await SaveTaskAsync() && await Workspace.FlushAsync()) { StopPlayback(); ShowStage(WorkspaceStage.Settings); await ShowSettingsAsync(); } }
    private UiPreferences CurrentPreferences() => new((uint)Math.Clamp(normalWidth, 1024, 8192),
        (uint)Math.Clamp(normalHeight, 700, 8192), WindowState == WindowState.Maximized, "dark");
    private void InspectorToggleClick(object? sender, RoutedEventArgs e)
    {
        MorePopup.IsOpen = false;
        SetInspectorOpen(!inspectorOpen);
        InspectorToggle.Focus();
    }
    private void SetInspectorOpen(bool open)
    {
        if (inspectorOpen == open) return;
        if (!open && StudioGrid.ColumnDefinitions[2].Width.Value > 0)
            inspectorWidth = StudioGrid.ColumnDefinitions[2].Width;
        inspectorOpen = open;
        DescriptionInspector.IsVisible = open;
        InspectorSplitter.IsVisible = open;
        StudioGrid.ColumnDefinitions[1].Width = new GridLength(open ? 5 : 0);
        StudioGrid.ColumnDefinitions[2].MinWidth = open ? 280 : 0;
        StudioGrid.ColumnDefinitions[2].Width = open ? inspectorWidth : new GridLength(0);
        InspectorToggle.Content = open ? "Hide descriptions" : "Edit descriptions";
        if (open && stage == WorkspaceStage.Ready) ShowStage(WorkspaceStage.Review);
        UpdatePromptLayout();
    }
    private async void GenerateClick(object? sender, RoutedEventArgs e) { StopPlayback(); await Workspace.GenerateAsync(); }
    private async void RegenerateClick(object? sender, RoutedEventArgs e)
    {
        StopPlayback();
        var dialog = new Window { Title = "Regenerate descriptions", Width = 540, Height = 280, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var manual = new CheckBox { Content = "Also replace my manual descriptions", IsChecked = false };
        var single = new Button { Content = "Selected moment" }; var all = new Button { Content = "All moments" };
        single.Click += (_, _) => dialog.Close(true); all.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 18, Children = {
            new TextBlock { Text = "Generate fresh descriptions and bypass cached results.", TextWrapping = Avalonia.Media.TextWrapping.Wrap }, manual,
            new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12, Children = { single, all } } } };
        var choice = await dialog.ShowDialog<bool?>(this);
        if (choice is not null) await Workspace.GenerateAsync(choice.Value, true, manual.IsChecked == true);
    }
    private async void OrganizeClick(object? sender, RoutedEventArgs e) { StopPlayback(); await Workspace.OrganizeAsync(); }
    private async void LoadMoreClick(object? sender, RoutedEventArgs e) => await Workspace.LoadMoreRecordingsAsync();
    private async void CancelClick(object? sender, RoutedEventArgs e) => await Workspace.CancelOperationAsync();
    private async void ExportClick(object? sender, RoutedEventArgs e)
    {
        StopPlayback();
        if (!await SaveTaskAsync() || !await Workspace.FlushAsync()) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions { Title = "Export recording, screenshots and Markdown", AllowMultiple = false });
        if (folders.Count == 0) return;
        var path = folders[0].Path.IsFile ? folders[0].Path.LocalPath : null;
        if (path is null) { await ShowPanel("Export unavailable", "Choose a local filesystem folder."); return; }
        await Workspace.ExportAsync(path);
        if (Workspace.ExportDestination is { } destination && !closing) await ShowPanel("Export complete", destination + "\n\nIncludes the full timed GIF, original selected PNGs and a Markdown transcript. Unreviewed or stale descriptions remain explicitly marked.");
    }
    private async void RecordingClick(object? sender, RoutedEventArgs e)
    {
        if (recordingTask is not null || Workspace.Busy || closing || !await SaveTaskAsync() || !await Workspace.FlushAsync()) return;
        StopPlayback(); ShowStage(WorkspaceStage.Ready);
        await LoadCaptureSetupAsync();
    }
    internal async Task RunRecordingAsync(RecordingSetup setup, Action<string>? stopObserved = null)
    {
        using var countdown = new CancellationTokenSource(); recordingCountdown = countdown;
        var label = new TextBlock { Text = "Preparing recording…", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        var stop = new Button { Content = "Cancel" };
        bool finished = false;
        async void Stop(string source) { if (finished) return; stopObserved?.Invoke(source); countdown.Cancel(); await Workspace.CancelOperationAsync(); }
        stop.Click += (_, _) => Stop("button");
        var controls = new Window { Title = "Recording controls", Width = 410, Height = 80, CanResize = false,
            Topmost = true, ShowInTaskbar = false, ShowActivated = false, WindowDecorations = WindowDecorations.None,
            Content = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 16,
                Margin = new Avalonia.Thickness(12), Children = { label, stop } } };
        RecordingIndicator? indicator = null;
        var studioExcluded = false;
        controls.Closing += (_, e) => { if (!finished) { e.Cancel = true; Stop("controls-closed"); } };
        try
        {
            ShowStage(WorkspaceStage.Recording);
            RecordingElapsed.Text = "Preparing recording…";
            var token = await Workspace.PrepareCaptureAsync(setup.Screen, setup.Area, setup.Title, setup.Context);
            using var protection = new RecordingProtection(() => Stop("shortcut"));
            await protection.Ready;
            countdown.Token.ThrowIfCancellationRequested();
            indicator = new RecordingIndicator(setup.Area, () => Stop("indicator-closed"));
            indicator.Show(); controls.Show();
            RecordingProtection.Exclude(this); studioExcluded = true;
            RecordingProtection.Exclude(controls);
            for (var remaining = 3; remaining > 0; remaining--)
            { label.Text = $"Recording in {remaining}…"; RecordingElapsed.Text = label.Text; await Task.Delay(1000, countdown.Token); }
            countdown.Token.ThrowIfCancellationRequested();
            label.Text = "Recording · Ctrl+Shift+F9"; stop.Content = "Stop"; stop.Classes.Add("recording");
            recordingStarted = Environment.TickCount64; RecordingElapsed.Text = "Recording · 00:00"; recordingClock.Start();
            await Workspace.RecordAsync(token, countdown.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            finished = true; recordingCountdown = null; recordingClock.Stop();
            ShowStage(Workspace.HasProject ? WorkspaceStage.Review : WorkspaceStage.Ready);
            try { controls.Close(); }
            finally
            {
                try { indicator?.Dispose(); }
                finally
                {
                    try { if (studioExcluded) RecordingProtection.Restore(this); }
                    finally { if (!closing) { IsEnabled = true; Activate(); } }
                }
            }
        }
    }
    private async void StatesClick(object? sender, RoutedEventArgs e) => await ShowDesignAsync("Ready");
    private async Task ShowDesignAsync(string state)
    {
        StopPlayback();
        if (!await Workspace.FlushAsync()) return;
        var dialog = new PrototypeWindow(state);
        try { await dialog.LoadEvidenceAsync(Workspace); await dialog.ShowDialog(this); }
        catch (Exception ex) { dialog.Close(); await ShowPanel("Preview unavailable", ex.Message); }
    }
    private async Task ShowPanel(string title, string body)
    {
        var dialog = new Window { Title = title, Width = 480, Height = 460, MinWidth = 400, MinHeight = 350, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var close = new Button { Content = "Back to workspace", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new Grid
        {
            Margin = new Avalonia.Thickness(28), RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Children =
            {
                new TextBlock { Text = title, FontSize = 22, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                Place(new ScrollViewer { Content = new TextBlock { Text = body, TextWrapping = Avalonia.Media.TextWrapping.Wrap, LineHeight = 22 }, Margin = new Avalonia.Thickness(0,20) },1),
                Place(close,2)
            }
        };
        await dialog.ShowDialog(this);
    }
    private static T Place<T>(T control, int row) where T : Control { Grid.SetRow(control,row); return control; }
}
