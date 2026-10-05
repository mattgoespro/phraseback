using Phraseback.Client;

namespace Phraseback.App;

/// <summary>A shell-owned draft survives transport replacement, never a revision number.</summary>
internal sealed record RecoveryDraft(Snapshot Original, string StepId, string Title, string Action, string Result, string Uncertainty)
{
    public bool CanRestore(Snapshot recovered)
    {
        var before = Original.Project;
        var after = recovered.Project;
        if (Original.RecordingId != recovered.RecordingId || before.Context != after.Context
            || before.Width != after.Width || before.Height != after.Height
            || !before.Frames.SequenceEqual(after.Frames)
            || !before.Steps.Select(s => (s.Id, s.Frame)).SequenceEqual(after.Steps.Select(s => (s.Id, s.Frame)))) return false;
        var original = before.Steps.FirstOrDefault(s => s.Id == StepId);
        var current = after.Steps.FirstOrDefault(s => s.Id == StepId);
        // An acknowledgement can be lost after the engine commits the exact draft.
        return current is not null && (current == original ||
            (current.Title == Title && current.Action == Action && current.Result == Result && current.Uncertainty == Uncertainty
             && current.Manual && current.Status == "edited" && !current.Reviewed));
    }
}
