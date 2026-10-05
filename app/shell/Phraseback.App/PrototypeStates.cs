using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;

namespace Phraseback.App;

/// <summary>Explicit design simulation. Never calls capture, inference, installation or project writes.</summary>
public sealed class PrototypeStates : INotifyPropertyChanged
{
    public string[] Cases { get; } = ["Ready", "Loaded review", "Long title", "Many moments", "Empty library", "Grouped alternatives", "Generating", "Cancelled", "Model absent", "Save failure", "Disconnected recovery", "Recording setup", "Settings"];
    public string[] Presets { get; } = ["Qwen3-VL 2B · CPU", "Qwen3-VL 2B · Vulkan", "Qwen3-VL 4B · Vulkan", "Qwen3-VL 8B · Vulkan"];
    public string[] Sources { get; } = ["Display 1 · synthetic 2560 × 1600", "Rectangular region · synthetic display"];
    public ObservableCollection<AlternativePreview> Alternatives { get; } = [];
    private string selectedCase = "Ready";
    private AlternativePreview? alternative;
    private string selectionNote = "All source images remain available.";
    public string SelectedCase
    {
        get => selectedCase;
        set
        {
            if (selectedCase == value) return;
            selectedCase = value;
            foreach (var name in new[] { nameof(SelectedCase), nameof(Heading), nameof(Explanation), nameof(Activity), nameof(ActionLabel), nameof(ShowProgress), nameof(ShowAlternatives), nameof(ShowSetup), nameof(ShowSettings), nameof(ShowEmpty), nameof(ShowEvidence), nameof(ShowAction), nameof(IsError) }) Raise(name);
        }
    }
    public AlternativePreview? Alternative { get => alternative; set { alternative = value; Raise(); Raise(nameof(Image)); } }
    public Bitmap? Image => Alternative?.Image;
    public bool ShowProgress => SelectedCase == "Generating";
    public bool ShowAlternatives => SelectedCase == "Grouped alternatives";
    public bool ShowSetup => SelectedCase == "Recording setup";
    public bool ShowSettings => SelectedCase == "Settings";
    public bool ShowEmpty => SelectedCase == "Empty library";
    public bool ShowEvidence => !ShowSetup && !ShowSettings && !ShowEmpty;
    public bool IsError => SelectedCase is "Save failure" or "Disconnected recovery";
    public bool ShowAction => SelectedCase is not "Ready" and not "Cancelled" and not "Settings";
    public string SelectionNote { get => selectionNote; private set { selectionNote = value; Raise(); } }
    public string Heading => SelectedCase switch
    {
        "Ready" => "Space for the evidence",
        "Loaded review" => "Review a selected moment",
        "Long title" => "A recording title that stays readable even when it wraps across several lines",
        "Many moments" => "A longer recording, still easy to scan",
        "Empty library" => "Your first recording starts here",
        "Grouped alternatives" => "One moment. More than one useful frame.",
        "Generating" => "Describing selected moments",
        "Cancelled" => "Stopped. Your progress stays.",
        "Model absent" => "Set up a local model",
        "Save failure" => "Your changes are still here",
        "Disconnected recovery" => "Connection lost. Draft kept here.",
        "Recording setup" => "What would you like to record?",
        _ => "Settings"
    };
    public string Explanation => SelectedCase switch
    {
        "Ready" => "The working workspace uses Rust for opening, editing, saving and review acknowledgements. These additional states are synthetic previews only.",
        "Loaded review" => "Compare the selected screenshot with its action and visible result before marking it reviewed.",
        "Long title" => "The full title remains available while the evidence keeps its space.",
        "Many moments" => "Only visible thumbnails are decoded. Frame evidence and review status remain available as you scroll.",
        "Empty library" => "Record one display or a region. Choose useful moments, review their descriptions and export when ready.",
        "Grouped alternatives" => "A quick sequence can contain an important intermediate result. Compare alternatives before choosing. None of the source frames are deleted.",
        "Generating" => "Preview: moment 3 of 5. Editing is frozen while generation runs. Completed descriptions are saved individually.",
        "Cancelled" => "Preview: two completed descriptions remain. The other moments still need descriptions. Manual wording is unchanged.",
        "Model absent" => "Analysis runs on this computer. Choose CPU or Vulkan explicitly. Hardware compatibility and asset verification come before inference.",
        "Save failure" => "Preview: storage is unavailable. Your draft remains in the inspector. Navigation and export stay blocked until persistence succeeds.",
        "Disconnected recovery" => "The local engine stopped. Reconnect to recover saved progress; an unsaved draft stays visible until you save or explicitly discard it.",
        "Recording setup" => "Synthetic source choices only. The working app supports display or region capture with a countdown, excluded controls and a global Stop shortcut.",
        _ => "Design preview only. These choices do not change your current application, model files or preferences."
    };
    public string Activity => SelectedCase switch
    {
        "Generating" => "Generating locally · 2 of 5 complete",
        "Cancelled" => "Cancelled · completed work retained",
        "Save failure" => "Not saved · draft retained",
        "Disconnected recovery" => "Engine disconnected · draft retained",
        "Model absent" => "No model installed in this synthetic scenario",
        "Grouped alternatives" => SelectionNote,
        _ => "Design simulation · no recording or model operations"
    };
    public string ActionLabel => SelectedCase switch
    {
        "Generating" => "Cancel simulation",
        "Save failure" => "Simulate successful retry",
        "Model absent" => "Choose a model",
        "Empty library" => "Set up a recording",
        "Grouped alternatives" => "Choose this alternative",
        "Recording setup" => "Preview capture readiness",
        _ => "Back"
    };
    public void Act()
    {
        switch (SelectedCase)
        {
            case "Generating": SelectedCase = "Cancelled"; break;
            case "Save failure": SelectedCase = "Ready"; break;
            case "Model absent": SelectedCase = "Settings"; break;
            case "Empty library": SelectedCase = "Recording setup"; break;
            case "Grouped alternatives":
                SelectionNote = $"Preview choice: {Alternative?.Label ?? "no alternative"}. Source recording unchanged.";
                Raise(nameof(Activity)); break;
            case "Recording setup":
                SelectionNote = "Before capture: choose a display or region, check SDR, then start a 3-second countdown.";
                break;
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

public sealed record AlternativePreview(string Label, Bitmap Image);
