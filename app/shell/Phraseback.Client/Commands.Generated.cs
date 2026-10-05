// Generated from contracts/commands.schema.json. Do not edit by hand.
#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Phraseback.Client;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record UiPreferences([property: JsonRequired] uint Width, [property: JsonRequired] uint Height, [property: JsonRequired] bool Maximized, string? Theme = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ApplyOrganizationRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision, [property: JsonRequired] string Id, bool Paged = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record LibraryPageRequest(ulong Offset = 0, ulong Limit = 64, string? CatalogId = null, bool Refresh = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record LibraryPage([property: JsonRequired] LibraryEntry[] Items, [property: JsonRequired] string CatalogId, [property: JsonRequired] int? NextOffset, [property: JsonRequired] bool Indexing);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record EmptyRequest(ulong Revision = 0);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record HelloRequest(bool Notifications = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record HelloResult([property: JsonRequired] string EngineVersion, [property: JsonRequired] int ProtocolMinor, bool Notifications = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record Capabilities([property: JsonRequired] bool Prototype, [property: JsonRequired] bool SharedData, [property: JsonRequired] bool Capture, [property: JsonRequired] bool Generation, [property: JsonRequired] bool Export, [property: JsonRequired] bool Editing, [property: JsonRequired] bool SourceEnumeration, [property: JsonRequired] bool SystemSelection, [property: JsonRequired] bool RegionCropping, [property: JsonRequired] string[] CursorModes, [property: JsonRequired] bool ControlExclusion);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record Frame([property: JsonRequired] string File, [property: JsonRequired] long TimeMs);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record Step([property: JsonRequired] int Frame, [property: JsonRequired] string Id, [property: JsonRequired] string Title, [property: JsonRequired] string Action, [property: JsonRequired] string Result, [property: JsonRequired] string Status, [property: JsonRequired] bool Manual, [property: JsonRequired] bool Reviewed, [property: JsonRequired] string Uncertainty);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record Project([property: JsonRequired] int Version, [property: JsonRequired] string Title, [property: JsonRequired] string Context, [property: JsonRequired] string Created, [property: JsonRequired] Frame[] Frames, [property: JsonRequired] Step[] Steps, [property: JsonRequired] long DurationMs, [property: JsonRequired] int Width, [property: JsonRequired] int Height, [property: JsonRequired] string Error, [property: JsonRequired] string State, string Task = "");

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record Snapshot([property: JsonRequired] string RecordingId, [property: JsonRequired] long Revision, [property: JsonRequired] Project Project, int FrameCount = 0, int StepCount = 0);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record FramePage([property: JsonRequired] Frame[] Items, [property: JsonRequired] long Revision, [property: JsonRequired] int? NextOffset);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record StepPage([property: JsonRequired] Step[] Items, [property: JsonRequired] long Revision, [property: JsonRequired] int? NextOffset);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record LibraryEntry([property: JsonRequired] string Id, [property: JsonRequired] string Title, [property: JsonRequired] string Created, [property: JsonRequired] long DurationMs, [property: JsonRequired] int Steps);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record Library([property: JsonRequired] LibraryEntry[] Items);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ImageReference([property: JsonRequired] string Path, [property: JsonRequired] string RecordingId, [property: JsonRequired] int Index, [property: JsonRequired] long Revision);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record CaptureScreen([property: JsonRequired] string Id, [property: JsonRequired] string Name, [property: JsonRequired] int Left, [property: JsonRequired] int Top, [property: JsonRequired] int Width, [property: JsonRequired] int Height);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record CaptureArea([property: JsonRequired] int Left, [property: JsonRequired] int Top, [property: JsonRequired] int Width, [property: JsonRequired] int Height);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record CapturePreparation([property: JsonRequired] string Token);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record OperationStatus([property: JsonRequired] string Id, [property: JsonRequired] string Kind, [property: JsonRequired] string State, [property: JsonRequired] int Progress, [property: JsonRequired] string Message, [property: JsonRequired] string? RecordingId, [property: JsonRequired] long Revision, [property: JsonRequired] bool Finished, [property: JsonRequired] string? Error, [property: JsonRequired] JsonElement? Result);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record Suggestions([property: JsonRequired] int[] Selected, [property: JsonRequired] int[][] Intervals);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ModelAsset([property: JsonRequired] string Name, [property: JsonRequired] string Url, [property: JsonRequired] long Size, [property: JsonRequired] string Sha256);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ModelPreset([property: JsonRequired] string Id, [property: JsonRequired] string Title, [property: JsonRequired] string ModelId, [property: JsonRequired] string Revision, [property: JsonRequired] bool Gpu, [property: JsonRequired] int RamGb, [property: JsonRequired] int SuggestedVramGb, [property: JsonRequired] ModelAsset[] Assets);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ModelStatus([property: JsonRequired] ModelPreset[] Presets, [property: JsonRequired] string Selected, [property: JsonRequired] string State, [property: JsonRequired] bool AssetsPresent, [property: JsonRequired] bool Verified);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record Cancelled([property: JsonRequired] bool Cancelling);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ShutdownResult([property: JsonRequired] bool Stopped);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record OpenProjectRequest([property: JsonRequired] string RecordingId, bool Paged = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record MetadataPageRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision, [property: JsonRequired] string Kind, [property: JsonRequired] ulong Offset, ulong Limit = 128);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record FrameRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Index, ulong? PreviewWidth = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record EditStepRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision, [property: JsonRequired] string StepId, [property: JsonRequired] string Title, [property: JsonRequired] string Action, [property: JsonRequired] string Result, [property: JsonRequired] string Uncertainty, bool Paged = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ReviewStepRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision, [property: JsonRequired] string StepId, [property: JsonRequired] bool Reviewed, bool Paged = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record AddStepRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision, [property: JsonRequired] ulong Frame, string? StepId = null, bool Paged = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record RemoveStepRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision, [property: JsonRequired] string StepId, ulong? Frame = null, bool Paged = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ReplaceStepRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision, [property: JsonRequired] string StepId, [property: JsonRequired] ulong Frame, bool Paged = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record EditProjectRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision, [property: JsonRequired] string Title, [property: JsonRequired] string Context, bool Paged = false, string? Task = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ProjectOperationRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record AlternativesRequest([property: JsonRequired] string RecordingId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record PrepareCaptureRequest([property: JsonRequired] CaptureScreen Screen, [property: JsonRequired] CaptureArea Area, [property: JsonRequired] string Title, [property: JsonRequired] string Context);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record StartCaptureRequest([property: JsonRequired] string Token, [property: JsonRequired] bool ExclusionReady, [property: JsonRequired] bool ShortcutReady);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record CancelOperationRequest([property: JsonRequired] string Id);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ModelSelectRequest([property: JsonRequired] string Preset);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ModelRemoveRequest([property: JsonRequired] bool Confirmed, ulong Revision = 0);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record GenerateRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision, string? StepId = null, bool Force = false, bool ReplaceManual = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ExportRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision, [property: JsonRequired] string Parent);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record PromptTemplate([property: JsonRequired] string Template, [property: JsonRequired] string DefaultTemplate);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record SavePromptTemplateRequest([property: JsonRequired] string Template);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record RenderPromptRequest([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision, string? Template = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record RenderedPrompt([property: JsonRequired] string RecordingId, [property: JsonRequired] ulong Revision, [property: JsonRequired] string Markdown);

public static class CommandPayloads
{
    private static void Check<T>(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) throw new JsonException("Null command payload");
        CheckElements(value.Deserialize<T>(Contract.Json));
    }
    private static void CheckElements(object? value)
    {
        if (value is null) throw new JsonException("Null required payload element");
        switch (value)
        {
            case Array array: foreach (var item in array) CheckElements(item); break;
            case LibraryPage item: CheckElements(item.Items); break;
            case Capabilities item: CheckElements(item.CursorModes); break;
            case Project item: CheckElements(item.Frames); CheckElements(item.Steps); break;
            case Snapshot item: CheckElements(item.Project); break;
            case FramePage item: CheckElements(item.Items); break;
            case StepPage item: CheckElements(item.Items); break;
            case Library item: CheckElements(item.Items); break;
            case Suggestions item: CheckElements(item.Selected); CheckElements(item.Intervals); break;
            case ModelPreset item: CheckElements(item.Assets); break;
            case ModelStatus item: CheckElements(item.Presets); break;
            case PrepareCaptureRequest item: CheckElements(item.Screen); CheckElements(item.Area); break;
        }
    }
    public static void ValidateRequest(string method, JsonElement value)
    {
        switch (method)
        {
            case "prepare_open": Check<OpenProjectRequest>(value); break;
            case "library_page": Check<LibraryPageRequest>(value); break;
            case "organize_background": Check<ProjectOperationRequest>(value); break;
            case "organization_status": Check<EmptyRequest>(value); break;
            case "cancel_organization": Check<CancelOperationRequest>(value); break;
            case "apply_organization": Check<ApplyOrganizationRequest>(value); break;
            case "hello": Check<HelloRequest>(value); break;
            case "capabilities": Check<EmptyRequest>(value); break;
            case "screens": Check<EmptyRequest>(value); break;
            case "list_projects": Check<EmptyRequest>(value); break;
            case "open_project": Check<OpenProjectRequest>(value); break;
            case "metadata_page": Check<MetadataPageRequest>(value); break;
            case "frame": Check<FrameRequest>(value); break;
            case "edit_step": Check<EditStepRequest>(value); break;
            case "review_step": Check<ReviewStepRequest>(value); break;
            case "add_step": Check<AddStepRequest>(value); break;
            case "remove_step": Check<RemoveStepRequest>(value); break;
            case "replace_step": Check<ReplaceStepRequest>(value); break;
            case "edit_project": Check<EditProjectRequest>(value); break;
            case "organize": Check<ProjectOperationRequest>(value); break;
            case "alternatives": Check<AlternativesRequest>(value); break;
            case "prepare_capture": Check<PrepareCaptureRequest>(value); break;
            case "start_capture": Check<StartCaptureRequest>(value); break;
            case "operation_status": Check<EmptyRequest>(value); break;
            case "cancel_operation": Check<CancelOperationRequest>(value); break;
            case "ui_preferences": Check<EmptyRequest>(value); break;
            case "save_ui_preferences": Check<UiPreferences>(value); break;
            case "model_status": Check<EmptyRequest>(value); break;
            case "model_select": Check<ModelSelectRequest>(value); break;
            case "model_release": Check<EmptyRequest>(value); break;
            case "model_install": Check<EmptyRequest>(value); break;
            case "model_verify": Check<EmptyRequest>(value); break;
            case "model_remove": Check<ModelRemoveRequest>(value); break;
            case "generate": Check<GenerateRequest>(value); break;
            case "export": Check<ExportRequest>(value); break;
            case "shutdown": Check<EmptyRequest>(value); break;
            case "prompt_template": Check<EmptyRequest>(value); break;
            case "save_prompt_template": Check<SavePromptTemplateRequest>(value); break;
            case "reset_prompt_template": Check<EmptyRequest>(value); break;
            case "render_prompt": Check<RenderPromptRequest>(value); break;
            default: throw new JsonException("Unknown command");
        }
    }

    public static void ValidateResponse(string method, JsonElement request, JsonElement value)
    {
        switch (method)
        {
            case "prepare_open": Check<OperationStatus>(value); break;
            case "library_page": Check<LibraryPage>(value); break;
            case "organize_background": Check<OperationStatus>(value); break;
            case "organization_status": if (value.ValueKind != JsonValueKind.Null) Check<OperationStatus>(value); break;
            case "cancel_organization": Check<Cancelled>(value); break;
            case "apply_organization": Check<Snapshot>(value); break;
            case "hello": Check<HelloResult>(value); break;
            case "capabilities": Check<Capabilities>(value); break;
            case "screens": Check<CaptureScreen[]>(value); break;
            case "list_projects": Check<Library>(value); break;
            case "open_project": Check<Snapshot>(value); break;
            case "metadata_page": if (request.GetProperty("kind").GetString() == "frames") Check<FramePage>(value); else Check<StepPage>(value); break;
            case "frame": Check<ImageReference>(value); break;
            case "edit_step": Check<Snapshot>(value); break;
            case "review_step": Check<Snapshot>(value); break;
            case "add_step": Check<Snapshot>(value); break;
            case "remove_step": Check<Snapshot>(value); break;
            case "replace_step": Check<Snapshot>(value); break;
            case "edit_project": Check<Snapshot>(value); break;
            case "organize": Check<OperationStatus>(value); break;
            case "alternatives": Check<Suggestions>(value); break;
            case "prepare_capture": Check<CapturePreparation>(value); break;
            case "start_capture": Check<OperationStatus>(value); break;
            case "operation_status": if (value.ValueKind != JsonValueKind.Null) Check<OperationStatus>(value); break;
            case "cancel_operation": Check<Cancelled>(value); break;
            case "ui_preferences": Check<UiPreferences>(value); break;
            case "save_ui_preferences": Check<UiPreferences>(value); break;
            case "model_status": Check<ModelStatus>(value); break;
            case "model_select": Check<ModelStatus>(value); break;
            case "model_release": Check<ModelStatus>(value); break;
            case "model_install": Check<OperationStatus>(value); break;
            case "model_verify": Check<OperationStatus>(value); break;
            case "model_remove": Check<OperationStatus>(value); break;
            case "generate": Check<OperationStatus>(value); break;
            case "export": Check<OperationStatus>(value); break;
            case "shutdown": Check<ShutdownResult>(value); break;
            case "prompt_template": Check<PromptTemplate>(value); break;
            case "save_prompt_template": Check<PromptTemplate>(value); break;
            case "reset_prompt_template": Check<PromptTemplate>(value); break;
            case "render_prompt": Check<RenderedPrompt>(value); break;
            default: throw new JsonException("Unknown command");
        }
    }
}
