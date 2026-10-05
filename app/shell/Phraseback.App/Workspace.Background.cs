using Phraseback.Client;

namespace Phraseback.App;

public sealed partial class Workspace
{
    private bool organizing;
    private string backgroundActivity = "";
    private string? organizationId;
    private Task organizationCompletion = Task.CompletedTask;
    private CancellationTokenSource? organizationWatch;
    public bool Organizing => organizing;
    public bool CanOrganize => CanCurate && !organizing;
    public bool CanCancel => Busy || organizing || IsOpening;
    public string StatusLine => backgroundActivity.Length == 0 ? Activity : $"{Activity} · {backgroundActivity}";
    public Task OrganizationCompletion => organizationCompletion;

    private void BackgroundState(bool active, string message)
    {
        organizing = active; backgroundActivity = message;
        Raise(nameof(Organizing)); Raise(nameof(CanOrganize)); Raise(nameof(CanCancel)); Raise(nameof(StatusLine));
    }

    private void ResetBackground()
    {
        organizationWatch?.Cancel(); organizationWatch?.Dispose(); organizationWatch = null;
        organizationId = null; BackgroundState(false, "");
    }

    public async Task OrganizeAsync()
    {
        if (!CanOrganize || engine is null || snapshot is null) return;
        BackgroundState(true, "Organizing screenshots…");
        var client = engine;
        try
        {
            if (!await FlushAsync() || disposed || !ReferenceEquals(client, engine)) { BackgroundState(false, ""); return; }
            var operation = await client.RequestAsync<OperationStatus>("organize_background", new { recording_id = snapshot.RecordingId, revision = snapshot.Revision });
            if (disposed || !ReferenceEquals(client, engine)) return;
            organizationId = operation.Id;
            organizationWatch?.Dispose(); organizationWatch = new CancellationTokenSource();
            organizationCompletion = FollowOrganizationAsync(client, operation, organizationWatch.Token);
        }
        catch (Exception ex) { if (!disposed && ReferenceEquals(client, engine)) { BackgroundState(false, ""); Error = $"Organizing unavailable: {ex.Message}"; } }
    }

    private async Task FollowOrganizationAsync(EngineClient client, OperationStatus operation, CancellationToken cancellation)
    {
        var id = operation.Id;
        try
        {
            while (!operation.Finished)
                operation = await client.NextOrganizationAsync(id, cancellation);
            if (disposed || !ReferenceEquals(client, engine) || id != organizationId) return;
            if (operation.State == "failed") { Error = $"Organizing unavailable: {operation.Error}"; return; }
            if (operation.State != "completed") return;
            await saves.WaitAsync(cancellation);
            var applying = false;
            try
            {
                // The engine cannot see an unacknowledged editor draft. Do not request
                // authoritative publication until both shell and engine identities match.
                if (disposed || switching || Busy || HasUnsavedDraft || snapshot is null
                    || snapshot.RecordingId != operation.RecordingId || snapshot.Revision != operation.Revision) return;
                switching = applying = true; ++seekVersion; Raise(nameof(CanEdit));
                var selectedId = selected?.Step.Id;
                var updated = await client.RequestSnapshotAsync("apply_organization", new { id, recording_id = snapshot.RecordingId, revision = snapshot.Revision }, cancellation);
                if (disposed || !ReferenceEquals(client, engine)) return;
                snapshot = updated;
                RebuildMoments();
                LoadDraft(Moments.FirstOrDefault(m => m.Step.Id == selectedId) ?? Moments.FirstOrDefault());
                Activity = "Screenshots organized · ready for review";
            }
            finally { if (applying) { switching = false; Raise(nameof(CanEdit)); } saves.Release(); }
            // A seek that was in flight against the previous revision may have
            // cleared its preview before rejecting that obsolete reply.
            if (!disposed && ReferenceEquals(client, engine) && snapshot?.RecordingId == operation.RecordingId && Preview is null)
                await SeekAsync(FrameIndex);
        }
        catch (OperationCanceledException) { }
        catch (EngineException ex) when (ex.Code == "stale_revision") { /* A newer edit always wins. */ }
        catch (Exception ex) { if (!disposed && ReferenceEquals(client, engine)) Error = $"Organizing unavailable: {ex.Message}"; }
        finally { if (id == organizationId) { organizationId = null; BackgroundState(false, ""); } }
    }

    public async Task CancelOrganizationAsync()
    {
        if (engine is null || organizationId is null) return;
        try { await engine.RequestAsync<Cancelled>("cancel_organization", new { id = organizationId }); }
        catch (Exception ex) { if (!disposed) Error = ex.Message; }
    }
}
