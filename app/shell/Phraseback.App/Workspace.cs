using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Phraseback.Client;

namespace Phraseback.App;

public sealed record Moment(Step Step, int Number, string Time, string StatusLabel, Workspace? Owner = null, string RecordingId = "")
{
    public string Title => Step.Title;
    public string Subtitle => $"{Time}  ·  {StatusLabel}";
    public string AccessibleName => $"{Number}. {Title}. {Time}. {StatusLabel}";
}

public sealed partial class Workspace : INotifyPropertyChanged, IAsyncDisposable
{
    private EngineClient? engine;
    private Snapshot? snapshot;
    private readonly SemaphoreSlim saves = new(1);
    private readonly SemaphoreSlim seeks = new(1);
    private CancellationTokenSource? debounce;
    private long draftVersion, savedVersion, seekVersion;
    private string title = "", action = "", result = "", uncertainty = "";
    private string activity = "Connecting to the local engine…", error = "", projectTitle = "Your recording studio";
    private Bitmap? preview;
    private readonly PreviewCache previewCache = new(125L * 1024 * 1024);
    private Moment? selected;
    private int frameIndex;
    private bool loading, switching, disposed, busy;
    private bool connected, recoveryConflict;
    private string? engineExecutable, dataRoot;
    private Action<int>? superviseEngine;
    private string? operationId;
    private string? openingId;
    private EngineClient? openingClient;
    public bool IsOpening => openingId is not null;

    private async Task<Snapshot> ReadProjectAsync(EngineClient client, string id)
    {
        try
        {
            return await client.RequestSnapshotAsync("open_project", new { recording_id = id }, openingProgress: status =>
            {
                openingClient = client; openingId = status.Id;
                Activity = status.Message; Raise(nameof(IsOpening)); Raise(nameof(CanCancel));
            });
        }
        finally
        {
            openingClient = null; openingId = null;
            Raise(nameof(IsOpening)); Raise(nameof(CanCancel));
        }
    }
    public ObservableCollection<LibraryEntry> Library { get; } = [];
    public ObservableCollection<Moment> Moments { get; } = [];
    public Snapshot? Snapshot => snapshot;
    public Moment? Selected => selected;
    public bool HasProject => snapshot is not null;
    public bool HasSelection => selected is not null;
    public bool IsConnected => connected;
    public bool CanReconnect => !connected && !switching && !busy && !disposed && engineExecutable is not null;
    public bool RecoveryConflict => recoveryConflict;
    public bool HasUnsavedDraft => draftVersion != savedVersion;
    public bool CanEdit => HasSelection && connected && !switching && !disposed && !busy;
    public bool CanCurate => HasProject && connected && !switching && !disposed && !busy;
    public bool Busy { get => busy; private set { Set(ref busy, value); RaiseConnection(); } }
    public string? ExportDestination { get; private set; }
    public string ProjectTitle { get => projectTitle; private set { if (Set(ref projectTitle, value)) Raise(nameof(RecordingSelectorLabel)); } }
    public string RecordingSelectorLabel => snapshot is null ? "Choose recording" : ProjectTitle;
    public string Activity { get => activity; private set => Set(ref activity, value); }
    public string Error { get => error; private set { Set(ref error, value); Raise(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public string Title { get => title; set { if (Set(ref title, value)) Changed(); } }
    public string Action { get => action; set { if (Set(ref action, value)) Changed(); } }
    public string Result { get => result; set { if (Set(ref result, value)) Changed(); } }
    public string Uncertainty { get => uncertainty; set { if (Set(ref uncertainty, value)) Changed(); } }
    public Bitmap? Preview { get => preview; private set => Set(ref preview, value); }
    public int MaximumFrame => Math.Max(0, (snapshot?.Project.Frames.Length ?? 1) - 1);
    public int FrameIndex { get => frameIndex; set { if (Set(ref frameIndex, value)) { Raise(nameof(TimeLabel)); _ = SeekAsync(value); } } }
    public string TimeLabel => snapshot is null ? "00:00.000" : FormatTime(snapshot.Project.Frames.ElementAtOrDefault(FrameIndex)?.TimeMs ?? 0);
    public string RecordingInfo => snapshot is null ? "Open a recording to begin" : $"{Moments.Count} selected {(Moments.Count == 1 ? "moment" : "moments")}  ·  {FormatTime(snapshot.Project.DurationMs)}";
    public string MomentCount => $"{Moments.Count} selected";
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task ConnectAsync(string executable, string root, Action<int>? supervise)
    {
        if (engineExecutable is not null) throw new InvalidOperationException("Use reconnect to replace an engine session.");
        engineExecutable = executable; dataRoot = root; superviseEngine = supervise;
        await ReconnectAsync();
    }

    public async Task<bool> ReconnectAsync(bool discardDraft = false)
    {
        if (!CanReconnect) return false;
        switching = true; connected = false; RaiseConnection();
        debounce?.Cancel(); ++seekVersion;
        await saves.WaitAsync();
        await seeks.WaitAsync();
        EngineClient? candidate = null;
        var previousId = snapshot?.RecordingId;
        var selectedId = selected?.Step.Id;
        var previousFrame = frameIndex;
        var draft = HasUnsavedDraft && snapshot is not null && selectedId is not null
            ? new RecoveryDraft(snapshot, selectedId, Title, Action, Result, Uncertainty) : null;
        try
        {
            Activity = "Reconnecting to the local engine…";
            if (engine is not null) { var old = engine; engine = null; await old.DisposeAsync(); }
            if (disposed) return false;
            candidate = await EngineClient.StartAsync(engineExecutable!, dataRoot!, superviseEngine);
            var library = await ReadLibraryPageAsync(candidate, 0, null, true);
            Snapshot? recovered = null;
            string? openError = null;
            if (previousId is not null)
            {
                try { recovered = await ReadProjectAsync(candidate, previousId); }
                catch (EngineException ex) { openError = ex.Message; }
            }
            var previousAvailable = recovered is not null;
            if (draft is not null && !discardDraft && !previousAvailable)
            {
                recoveryConflict = true;
                throw new IOException("The original recording is unavailable. Your draft is retained. Restore the recording and reconnect, or copy the draft before choosing Use saved version.");
            }
            if (recovered is null && library.Items.FirstOrDefault() is { } first && first.Id != previousId)
            {
                try { recovered = await ReadProjectAsync(candidate, first.Id); openError = null; }
                catch (EngineException ex) { openError = ex.Message; }
            }
            if (disposed) return false;
            if (!discardDraft && draft is not null && (recovered is null || !draft.CanRestore(recovered)))
            {
                recoveryConflict = true;
                throw new IOException("Saved evidence or description changed while disconnected. Your draft is retained. Copy it before choosing Use saved version, or restore the original evidence and reconnect.");
            }
            engine = candidate; candidate = null;
            snapshot = recovered;
            Preview = null; previewCache.Dispose();
            ApplyLibraryPage(library, true);
            ProjectTitle = snapshot?.Project.Title ?? "Your recording studio";
            Moments.Clear(); RebuildMoments();
            Raise(nameof(MomentCount)); Raise(nameof(TimeLabel));
            LoadDraft(Moments.FirstOrDefault(m => m.Step.Id == selectedId) ?? Moments.FirstOrDefault());
            if (!discardDraft && draft is not null)
            {
                loading = true;
                Title = draft.Title; Action = draft.Action; Result = draft.Result; Uncertainty = draft.Uncertainty;
                loading = false;
                draftVersion = 1; savedVersion = 0;
            }
            recoveryConflict = false; connected = true; Error = openError ?? snapshot?.Project.Error ?? "";
            Activity = openError is not null ? "Recording unavailable · choose another recording or start a new one"
                : HasUnsavedDraft ? "Reconnected · draft retained; choose Save draft to persist" : "Ready · descriptions save automatically";
            Raise(nameof(HasProject)); Raise(nameof(MaximumFrame)); Raise(nameof(RecordingInfo));
            _ = ObserveDisconnectAsync(engine);
            return true;
        }
        catch (Exception ex) { Error = ex.Message; Activity = HasUnsavedDraft ? "Engine unavailable · draft retained" : "Engine unavailable"; return false; }
        finally
        {
            if (candidate is not null) await candidate.DisposeAsync();
            switching = false; seeks.Release(); saves.Release(); RaiseConnection();
            if (connected && !disposed) await SeekAsync(previousId is null ? selected?.Step.Frame ?? 0 : previousFrame);
        }
    }

    private async Task ObserveDisconnectAsync(EngineClient observed)
    {
        await observed.Disconnected;
        if (disposed || !ReferenceEquals(engine, observed)) return;
        connected = false; debounce?.Cancel(); ++seekVersion;
        ResetBackground();
        Activity = HasUnsavedDraft ? "Engine disconnected · draft retained" : "Engine disconnected · saved progress is recoverable";
        Error = "The local engine stopped. Reconnect to recover saved progress. Unsaved descriptions remain here.";
        RaiseConnection();
    }

    private void RaiseConnection()
    {
        Raise(nameof(IsConnected)); Raise(nameof(CanReconnect)); Raise(nameof(RecoveryConflict));
        Raise(nameof(CanEdit)); Raise(nameof(CanCurate)); Raise(nameof(HasUnsavedDraft));
    }

    public async Task<bool> OpenAsync(LibraryEntry entry)
    {
        if (!connected || engine is null || switching || disposed || busy) return false;
        switching = true; Raise(nameof(CanEdit));
        try
        {
            if (!await FlushAsync()) return false;
            ++seekVersion;
            snapshot = await ReadProjectAsync(engine, entry.Id);
            Preview = null; previewCache.Dispose();
            ProjectTitle = snapshot.Project.Title;
            RebuildMoments();
            LoadDraft(Moments.FirstOrDefault());
            Raise(nameof(HasProject)); Raise(nameof(MaximumFrame)); Raise(nameof(RecordingInfo));
            await SeekAsync(selected?.Step.Frame ?? 0);
            Activity = "Ready · descriptions save automatically";
            Error = snapshot.Project.Error;
            return true;
        }
        catch (Exception ex) { Error = ex.Message; return false; }
        finally { switching = false; Raise(nameof(CanEdit)); }
    }

    public async Task<bool> SelectAsync(Moment? moment)
    {
        if (!connected || moment is null || switching || disposed || busy) return false;
        switching = true; Raise(nameof(CanEdit));
        try
        {
            if (!await FlushAsync()) return false;
            LoadDraft(Moments.FirstOrDefault(m => m.Step.Id == moment.Step.Id));
            await SeekAsync(selected?.Step.Frame ?? 0);
            return true;
        }
        finally { switching = false; Raise(nameof(CanEdit)); }
    }

    private void LoadDraft(Moment? moment)
    {
        loading = true;
        selected = moment;
        Title = moment?.Step.Title ?? ""; Action = moment?.Step.Action ?? "";
        Result = moment?.Step.Result ?? ""; Uncertainty = moment?.Step.Uncertainty ?? "";
        draftVersion = savedVersion = 0;
        loading = false;
        Raise(nameof(HasUnsavedDraft));
        Raise(nameof(Selected)); Raise(nameof(HasSelection)); Raise(nameof(CanEdit));
    }

    private void Changed()
    {
        if (loading || selected is null || disposed) return;
        draftVersion++;
        Raise(nameof(HasUnsavedDraft));
        Activity = "Unsaved changes…";
        debounce?.Cancel(); debounce?.Dispose();
        debounce = new CancellationTokenSource();
        _ = SaveAfterDelayAsync(debounce.Token);
    }
    private async Task SaveAfterDelayAsync(CancellationToken cancellation)
    {
        try { await Task.Delay(350, cancellation); await FlushAsync(); }
        catch (OperationCanceledException) { }
    }

    public async Task<bool> FlushAsync()
    {
        await saves.WaitAsync();
        try
        {
            if (disposed) return false;
            if (!connected)
            {
                if (!HasUnsavedDraft) return true;
                Error = "Not saved — reconnect first. Your draft is still here.";
                Activity = "Save failed · draft retained";
                return false;
            }
            while (draftVersion != savedVersion && engine is not null && snapshot is not null && selected is not null)
            {
                var version = draftVersion;
                Activity = "Saving description…";
                snapshot = await engine.RequestSnapshotAsync("edit_step", new
                {
                    recording_id = snapshot.RecordingId, revision = snapshot.Revision, step_id = selected.Step.Id,
                    title = Title, action = Action, result = Result, uncertainty = Uncertainty
                });
                savedVersion = version;
                // Retain the user's in-flight draft; acknowledgements only update authoritative rows.
                RebuildMoments();
                Error = "";
            }
            Activity = snapshot is null ? "No recording open" : "All changes saved locally";
            Raise(nameof(HasUnsavedDraft));
            return true;
        }
        catch (Exception ex) { Error = $"Not saved — {ex.Message} Your draft is still here. Retry before leaving."; Activity = "Save failed · draft retained"; return false; }
        finally { saves.Release(); }
    }

    public async Task ReviewAsync()
    {
        if (switching || disposed || busy || engine is null || snapshot is null || selected is null) return;
        switching = true; Raise(nameof(CanEdit));
        try
        {
            if (!await FlushAsync()) return;
            snapshot = await engine.RequestSnapshotAsync("review_step", new { recording_id = snapshot.RecordingId, revision = snapshot.Revision, step_id = selected.Step.Id, reviewed = true });
            RebuildMoments(); Activity = "Moment marked reviewed";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { switching = false; Raise(nameof(CanEdit)); }
    }

    private void RebuildMoments()
    {
        if (snapshot is null) return;
        var id = selected?.Step.Id;
        for (var i = 0; i < snapshot.Project.Steps.Length; i++)
        {
            var step = snapshot.Project.Steps[i];
            var status = step.Reviewed
                ? step.Manual ? "Reviewed · manual" : step.Status == "generated" ? "Reviewed · generated" : "Reviewed"
                : step.Status switch { "pending" => "Pending", "stale" => "Evidence changed", "edited" => "Manual", "generated" => "Generated · review", "failed" => "Generation failed", _ => "Needs review" };
            var row = new Moment(step, i + 1, FormatTime(snapshot.Project.Frames[step.Frame].TimeMs), status, this, snapshot.RecordingId);
            if (i >= Moments.Count) Moments.Add(row);
            else if (Moments[i] != row) Moments[i] = row;
        }
        while (Moments.Count > snapshot.Project.Steps.Length) Moments.RemoveAt(Moments.Count - 1);
        selected = Moments.FirstOrDefault(m => m.Step.Id == id);
        Raise(nameof(Selected)); Raise(nameof(MomentCount)); Raise(nameof(RecordingInfo));
    }

    public Task<bool> AddCurrentFrameAsync() => CurateAsync("add_step");
    public Task<bool> ReplaceCurrentFrameAsync() => CurateAsync("replace_step");
    public Task<bool> RemoveSelectedAsync() => CurateAsync("remove_step");
    private async Task<bool> CurateAsync(string method)
    {
        if (!CanCurate || engine is null || snapshot is null || (method != "add_step" && selected is null)) return false;
        switching = true; Raise(nameof(CanEdit)); Raise(nameof(CanCurate));
        var previousId = selected?.Step.Id;
        var frame = FrameIndex;
        try
        {
            if (!await FlushAsync()) return false;
            snapshot = await engine.RequestSnapshotAsync(method, new { recording_id = snapshot.RecordingId, revision = snapshot.Revision, step_id = previousId, frame });
            RebuildMoments();
            LoadDraft(method == "add_step" ? Moments.FirstOrDefault(m => m.Step.Frame == frame)
                : Moments.FirstOrDefault(m => m.Step.Id == previousId) ?? Moments.FirstOrDefault());
            if (selected is not null) await SeekAsync(selected.Step.Frame);
            Activity = "Screenshot selection saved"; Error = "";
            return true;
        }
        catch (Exception ex) { Error = ex.Message; return false; }
        finally { switching = false; Raise(nameof(CanEdit)); Raise(nameof(CanCurate)); }
    }

    public async Task<bool> EditProjectAsync(string projectName, string context, string? task = null)
    {
        if (!CanCurate || engine is null || snapshot is null) return false;
        switching = true; Raise(nameof(CanEdit)); Raise(nameof(CanCurate));
        try
        {
            if (!await FlushAsync()) return false;
            snapshot = await engine.RequestSnapshotAsync("edit_project", new { recording_id = snapshot.RecordingId, revision = snapshot.Revision, title = projectName, context, task });
            ProjectTitle = snapshot.Project.Title;
            RebuildMoments(); LoadDraft(selected);
            var row = Library.Select((entry, index) => (entry, index)).FirstOrDefault(p => p.entry.Id == snapshot.RecordingId);
            if (row.entry is not null) Library[row.index] = row.entry with { Title = ProjectTitle, Steps = Moments.Count };
            Activity = "Recording details saved"; Error = "";
            return true;
        }
        catch (Exception ex) { Error = ex.Message; return false; }
        finally { switching = false; Raise(nameof(CanEdit)); Raise(nameof(CanCurate)); }
    }

    public async Task SeekAsync(int index)
    {
        if (disposed || engine is null || snapshot is null || snapshot.Project.Frames.Length == 0) return;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var cacheHit = false;
        var identity = ++seekVersion;
        var recordingId = snapshot.RecordingId;
        index = Math.Clamp(index, 0, MaximumFrame);
        frameIndex = index; Raise(nameof(FrameIndex)); Raise(nameof(TimeLabel));
        // Never label the previous image as the newly selected evidence while it
        // loads, or leave that old image visible after a missing/corrupt-file error.
        Preview = null;
        if (Error.StartsWith("Preview unavailable:", StringComparison.Ordinal)) Error = "";
        await seeks.WaitAsync();
        try
        {
            if (disposed || identity != seekVersion) return;
            var reference = await engine.RequestAsync<ImageReference>("frame", new { recording_id = recordingId, index, preview_width = 1440 });
            if (disposed || identity != seekVersion) return;
            if (reference.RecordingId != recordingId || reference.Index != index || reference.Revision != snapshot.Revision) return;
            if (previewCache.Get(reference.Path) is { } cached) { cacheHit = true; Preview = cached; return; }
            var width = Math.Clamp(snapshot.Project.Width, 1, 1440);
            Bitmap image;
            try { image = await DecodeAsync(reference.Path, width); }
            catch
            {
                // A damaged disposable derivative cannot make original evidence unreadable.
                reference = await engine.RequestAsync<ImageReference>("frame", new { recording_id = recordingId, index });
                if (disposed || identity != seekVersion) return;
                image = await DecodeAsync(reference.Path, width);
            }
            if (disposed || identity != seekVersion || snapshot.RecordingId != recordingId) { image.Dispose(); return; }
            try { previewCache.Put(reference.Path, image); }
            catch { image.Dispose(); throw; }
            Preview = image;
        }
        catch (Exception ex) { if (identity == seekVersion) Error = $"Preview unavailable: {ex.Message}"; }
        finally
        {
            if (identity != seekVersion || disposed) supersededSeeks++;
            else if (Preview is null) failedSeeks++;
            else (cacheHit ? cachedSeeks : uncachedSeeks).Add(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            seeks.Release();
            if (!disposed && identity == seekVersion && Preview is not null) _ = PrefetchAdjacentAsync(index, identity, recordingId);
        }
    }

    private async Task<Bitmap> DecodeAsync(string path, int width)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try { return await Task.Run(() => { using var stream = File.OpenRead(path); return Bitmap.DecodeToWidth(stream, width); }); }
        finally { decodes.Add(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }

    private async Task PrefetchAdjacentAsync(int index, long identity, string recordingId)
    {
        // Give direct seeks priority. One decoder and two speculative neighbours maximum.
        await Task.Delay(80);
        if (disposed || identity != seekVersion || !await seeks.WaitAsync(0)) return;
        try
        {
            foreach (var adjacent in new[] { index + 1, index - 1 })
            {
                if (disposed || identity != seekVersion || engine is null || snapshot?.RecordingId != recordingId) return;
                if (adjacent < 0 || adjacent >= snapshot.Project.Frames.Length) continue;
                var reference = await engine.RequestAsync<ImageReference>("frame", new { recording_id = recordingId, index = adjacent, preview_width = 1440 });
                if (disposed || identity != seekVersion || reference.Revision != snapshot.Revision) return;
                if (previewCache.Get(reference.Path) is not null) continue;
                var width = Math.Clamp(snapshot.Project.Width, 1, 1440);
                var image = await DecodeAsync(reference.Path, width);
                if (disposed || identity != seekVersion || snapshot.RecordingId != recordingId) { image.Dispose(); return; }
                try
                {
                    // Pin the displayed bitmap at the MRU end before speculative eviction.
                    if (!previewCache.TryPutPrefetched(reference.Path, image, Preview)) image.Dispose();
                }
                catch { image.Dispose(); throw; }
            }
        }
        catch (Exception) { /* Speculative cache failures must not hide valid evidence. */ }
        finally { seeks.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        if (IsOpening) await CancelOperationAsync();
        ResetBackground(); ++libraryVersion;
        debounce?.Cancel(); debounce?.Dispose(); seekVersion++;
        await saves.WaitAsync();
        await seeks.WaitAsync();
        await thumbnailDecoder.WaitAsync();
        try { if (engine is not null) await engine.DisposeAsync(); Preview = null; previewCache.Dispose(); foreach (var thumbnail in thumbnails.ToArray()) thumbnail.Dispose(); }
        finally { thumbnailDecoder.Release(); seeks.Release(); saves.Release(); }
    }
    public async Task<IReadOnlyList<AlternativePreview>> LoadAlternativesAsync()
    {
        var images = new List<AlternativePreview>();
        if (disposed || engine is null || snapshot is null) return images;
        try
        {
            var count = Math.Min(3, snapshot.Project.Frames.Length);
            for (var index = 0; index < count; index++)
            {
                var reference = await engine.RequestAsync<ImageReference>("frame", new { recording_id = snapshot.RecordingId, index });
                var width = Math.Clamp(snapshot.Project.Width, 1, 640);
                var bitmap = await Task.Run(() => { using var stream = File.OpenRead(reference.Path); return Bitmap.DecodeToWidth(stream, width); });
                images.Add(new AlternativePreview($"Alternative {index + 1}", bitmap));
            }
            return images;
        }
        catch { foreach (var image in images) image.Image.Dispose(); throw; }
    }
    public async Task<Bitmap?> LoadOriginalAsync()
    {
        if (disposed || engine is null || snapshot is null || snapshot.Project.Frames.Length == 0) return null;
        var reference = await engine.RequestAsync<ImageReference>("frame", new { recording_id = snapshot.RecordingId, index = FrameIndex });
        return await Task.Run(() => { using var stream = File.OpenRead(reference.Path); return new Bitmap(stream); });
    }
    public async Task<CaptureScreen[]> ScreensAsync() => engine is null ? [] : await engine.RequestAsync<CaptureScreen[]>("screens", new { });
    public async Task<string> PrepareCaptureAsync(CaptureScreen screen, CaptureArea area, string title, string context)
    {
        if (!connected || engine is null || Busy || !await FlushAsync()) throw new InvalidOperationException("Reconnect and save pending changes before recording.");
        var prepared = await engine.RequestAsync<CapturePreparation>("prepare_capture", new { screen, area, title, context });
        return prepared.Token;
    }
    public async Task RecordAsync(string token, CancellationToken stopRequested = default)
    {
        if (!connected || engine is null || Busy) return;
        ClearCapturePreview(); CapturePreviewCount = 0;
        ++seekVersion; Preview = null; previewCache.Dispose();
        Busy = true;
        try
        {
            stopRequested.ThrowIfCancellationRequested();
            var operation = await engine.RequestAsync<OperationStatus>("start_capture", new { token, exclusion_ready = true, shortcut_ready = true });
            // Stop may have been pressed while the start reply (and its operation ID) was in flight.
            operationId = operation.Id;
            if (stopRequested.IsCancellationRequested) await CancelOperationAsync();
            await FollowOperationAsync(operation);
            operationId = null;
            if (!disposed && snapshot is not null && snapshot.Project.Frames.Length > 0)
                await OrganizeAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { Busy = false; operationId = null; ClearCapturePreview(); }
    }
    public Task GenerateAsync(bool single = false, bool force = false, bool replaceManual = false) => snapshot is null ? Task.CompletedTask : ProjectOperationAsync("generate", new { recording_id = snapshot.RecordingId, revision = snapshot.Revision, step_id = single ? selected?.Step.Id : null, force, replace_manual = replaceManual });
    public async Task<ModelStatus> ModelStatusAsync() => engine is null ? throw new InvalidOperationException("Engine unavailable") : await engine.RequestAsync<ModelStatus>("model_status", new { });
    public async Task<UiPreferences> LoadUiPreferencesAsync() => engine is null ? throw new InvalidOperationException("Engine unavailable") : await engine.RequestAsync<UiPreferences>("ui_preferences", new { });
    public async Task SaveUiPreferencesAsync(UiPreferences preferences)
    {
        if (!connected || engine is null) throw new InvalidOperationException("Reconnect before saving window preferences.");
        await engine.RequestAsync<UiPreferences>("save_ui_preferences", preferences);
    }
    public async Task<ModelStatus> SelectModelAsync(string preset) => engine is null ? throw new InvalidOperationException("Engine unavailable") : await engine.RequestAsync<ModelStatus>("model_select", new { preset });
    public async Task<ModelStatus> ReleaseModelAsync() => engine is null ? throw new InvalidOperationException("Engine unavailable") : await engine.RequestAsync<ModelStatus>("model_release", new { });
    public Task InstallModelAsync(bool verifyOnly) => ProjectOperationAsync(verifyOnly ? "model_verify" : "model_install", new { });
    public Task RemoveModelAsync() => ProjectOperationAsync("model_remove", new { confirmed = true });
    public Task ExportAsync(string parent) => snapshot is null ? Task.CompletedTask : ProjectOperationAsync("export", new { recording_id = snapshot.RecordingId, revision = snapshot.Revision, parent });
    private async Task ProjectOperationAsync(string method, object parameters)
    {
        if (!connected || engine is null || Busy || disposed || !await FlushAsync()) return;
        Busy = true; ExportDestination = null;
        try
        {
            var request = System.Text.Json.JsonSerializer.SerializeToNode(parameters, Contract.Json)!.AsObject();
            if (snapshot is not null) request["revision"] = snapshot.Revision;
            await FollowOperationAsync(await engine.RequestAsync<OperationStatus>(method, request));
        }
        catch (Exception ex) { if (!disposed) Error = ex.Message; }
        finally { Busy = false; operationId = null; }
    }
    public async Task<int[]> AlternativeFramesAsync()
    {
        if (engine is null || snapshot is null || selected is null || Busy) return [];
        var suggestions = await engine.RequestAsync<Suggestions>("alternatives", new { recording_id = snapshot.RecordingId });
        var range = suggestions.Intervals.FirstOrDefault(p => p.Length == 2 && selected.Step.Frame >= p[0] && selected.Step.Frame <= p[1]);
        return range is null ? [] : Enumerable.Range(range[0], range[1] - range[0] + 1).ToArray();
    }
    private async Task FollowOperationAsync(OperationStatus operation)
    {
        if (engine is null) return;
        operationId = operation.Id;
        var previousRecording = snapshot?.RecordingId;
        while (!operation.Finished && !disposed)
        {
            _ = LoadCapturePreviewAsync(operation);
            Activity = operation.Message;
            var next = await engine.NextOperationAsync(operationId);
            if (next.Id != operationId) throw new IOException("The engine operation changed unexpectedly.");
            operation = next;
            if (!disposed && snapshot is not null && operation.RecordingId == snapshot.RecordingId && operation.Revision == snapshot.Revision
                && operation.Result is { } update && update.TryGetProperty("saved_step", out var savedStep))
            {
                var step = savedStep.Deserialize<Step>(Contract.Json)!;
                var index = Array.FindIndex(snapshot.Project.Steps, s => s.Id == step.Id);
                if (index >= 0) { snapshot.Project.Steps[index] = step; RebuildMoments(); LoadDraft(selected); }
            }
        }
        if (disposed) return;
        Busy = false;
        if ((operation.RecordingId ?? previousRecording) is { } id)
            await OpenAsync(new LibraryEntry(id, "", "", 0, 0));
        _ = RefreshLibraryAsync();
        Activity = operation.State == "completed" ? "Operation complete" : operation.State;
        if (operation.Result is { } result && result.TryGetProperty("destination", out var destination)) { ExportDestination = destination.GetString(); Activity = "Export complete · " + ExportDestination; }
        if (operation.Error is not null) Error = operation.Error;
        else if (operation.Result is { } outcome && outcome.TryGetProperty("failed", out var failed) && failed.GetInt32() > 0) Error = $"{failed.GetInt32()} descriptions failed. Saved descriptions remain available; verify the model in Settings and retry failed moments.";
    }
    public async Task CancelOperationAsync()
    {
        if (openingId is { } recovery && openingClient is { } client)
        {
            Activity = "Cancelling recovery · original evidence is retained…";
            try { await client.RequestAsync<System.Text.Json.JsonElement>("cancel_operation", new { id = recovery }); }
            catch (Exception ex) { Error = ex.Message; }
            return;
        }
        if (operationId is null) { await CancelOrganizationAsync(); return; }
        if (engine is null) return;
        Activity = "Stopping · saved progress is retained…";
        try { await engine.RequestAsync<System.Text.Json.JsonElement>("cancel_operation", new { id = operationId }); }
        catch (Exception ex) { Error = ex.Message; }
    }
    private static string FormatTime(long milliseconds) => TimeSpan.FromMilliseconds(milliseconds).ToString(@"mm\:ss\.fff");
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Raise(name); return true; }
    private void Raise([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name is nameof(CanEdit) or nameof(HasProject)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCurate)));
        if (name == nameof(CanEdit)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanReconnect)));
        if (name is nameof(CanCurate) or nameof(CanEdit) or nameof(HasProject) or nameof(Busy))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanOrganize)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanLoadMore)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCancel)));
        }
        if (name == nameof(Activity)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusLine)));
    }
}
