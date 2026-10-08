// Generated from contracts/commands.schema.json. Do not edit by hand.
use serde::{Deserialize, Serialize};
use serde_json::Value;

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct UiPreferences {
    pub width: u32,
    pub height: u32,
    pub maximized: bool,
    #[serde(default)]
    pub theme: Option<String>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ApplyOrganizationRequest {
    pub recording_id: String,
    pub revision: u64,
    pub id: String,
    #[serde(default)]
    pub paged: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct LibraryPageRequest {
    #[serde(default)]
    pub offset: u64,
    #[serde(default = "default_librarypagerequest_limit")]
    pub limit: u64,
    #[serde(default)]
    pub catalog_id: Option<String>,
    #[serde(default)]
    pub refresh: bool,
}

fn default_librarypagerequest_limit() -> u64 {
    64
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct LibraryPage {
    pub items: Vec<LibraryEntry>,
    pub catalog_id: String,
    #[serde(deserialize_with = "required_nullable")]
    pub next_offset: Option<i32>,
    pub indexing: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct EmptyRequest {
    #[serde(default)]
    pub revision: u64,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct HelloRequest {
    #[serde(default)]
    pub notifications: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct HelloResult {
    pub engine_version: String,
    pub protocol_minor: i32,
    #[serde(default)]
    pub notifications: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Capabilities {
    pub prototype: bool,
    pub shared_data: bool,
    pub capture: bool,
    pub generation: bool,
    pub export: bool,
    pub editing: bool,
    pub source_enumeration: bool,
    pub system_selection: bool,
    pub region_cropping: bool,
    pub cursor_modes: Vec<String>,
    pub control_exclusion: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Frame {
    pub file: String,
    pub time_ms: i64,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Step {
    pub frame: i32,
    pub id: String,
    pub title: String,
    pub action: String,
    pub result: String,
    pub status: String,
    pub manual: bool,
    pub reviewed: bool,
    pub uncertainty: String,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Project {
    pub version: i32,
    pub title: String,
    pub context: String,
    pub created: String,
    pub frames: Vec<Frame>,
    pub steps: Vec<Step>,
    pub duration_ms: i64,
    pub width: i32,
    pub height: i32,
    pub error: String,
    pub state: String,
    #[serde(default = "default_project_task")]
    pub task: String,
}

fn default_project_task() -> String {
    "".into()
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Snapshot {
    pub recording_id: String,
    pub revision: i64,
    pub project: Project,
    #[serde(default)]
    pub frame_count: i32,
    #[serde(default)]
    pub step_count: i32,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct FramePage {
    pub items: Vec<Frame>,
    pub revision: i64,
    #[serde(deserialize_with = "required_nullable")]
    pub next_offset: Option<i32>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct StepPage {
    pub items: Vec<Step>,
    pub revision: i64,
    #[serde(deserialize_with = "required_nullable")]
    pub next_offset: Option<i32>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct LibraryEntry {
    pub id: String,
    pub title: String,
    pub created: String,
    pub duration_ms: i64,
    pub steps: i32,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Library {
    pub items: Vec<LibraryEntry>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ImageReference {
    pub path: String,
    pub recording_id: String,
    pub index: i32,
    pub revision: i64,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct CaptureScreen {
    pub id: String,
    pub name: String,
    pub left: i32,
    pub top: i32,
    pub width: i32,
    pub height: i32,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct CaptureArea {
    pub left: i32,
    pub top: i32,
    pub width: i32,
    pub height: i32,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct CapturePreparation {
    pub token: String,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct OperationStatus {
    pub id: String,
    pub kind: String,
    pub state: String,
    pub progress: i32,
    pub message: String,
    #[serde(deserialize_with = "required_nullable")]
    pub recording_id: Option<String>,
    pub revision: i64,
    pub finished: bool,
    #[serde(deserialize_with = "required_nullable")]
    pub error: Option<String>,
    #[serde(deserialize_with = "required_nullable")]
    pub result: Option<Value>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Suggestions {
    pub selected: Vec<i32>,
    pub intervals: Vec<Vec<i32>>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ModelAsset {
    pub name: String,
    pub url: String,
    pub size: i64,
    pub sha256: String,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ModelPreset {
    pub id: String,
    pub title: String,
    pub model_id: String,
    pub revision: String,
    pub gpu: bool,
    pub ram_gb: i32,
    pub suggested_vram_gb: i32,
    pub assets: Vec<ModelAsset>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ModelStatus {
    pub presets: Vec<ModelPreset>,
    pub selected: String,
    pub state: String,
    pub assets_present: bool,
    pub verified: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Cancelled {
    pub cancelling: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ShutdownResult {
    pub stopped: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct OpenProjectRequest {
    pub recording_id: String,
    #[serde(default)]
    pub paged: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct MetadataPageRequest {
    pub recording_id: String,
    pub revision: u64,
    pub kind: String,
    pub offset: u64,
    #[serde(default = "default_metadatapagerequest_limit")]
    pub limit: u64,
}

fn default_metadatapagerequest_limit() -> u64 {
    128
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct FrameRequest {
    pub recording_id: String,
    pub index: u64,
    #[serde(default)]
    pub preview_width: Option<u64>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct EditStepRequest {
    pub recording_id: String,
    pub revision: u64,
    pub step_id: String,
    pub title: String,
    pub action: String,
    pub result: String,
    pub uncertainty: String,
    #[serde(default)]
    pub paged: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ReviewStepRequest {
    pub recording_id: String,
    pub revision: u64,
    pub step_id: String,
    pub reviewed: bool,
    #[serde(default)]
    pub paged: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct AddStepRequest {
    pub recording_id: String,
    pub revision: u64,
    pub frame: u64,
    #[serde(default)]
    pub step_id: Option<String>,
    #[serde(default)]
    pub paged: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct RemoveStepRequest {
    pub recording_id: String,
    pub revision: u64,
    pub step_id: String,
    #[serde(default)]
    pub frame: Option<u64>,
    #[serde(default)]
    pub paged: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ReplaceStepRequest {
    pub recording_id: String,
    pub revision: u64,
    pub step_id: String,
    pub frame: u64,
    #[serde(default)]
    pub paged: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct EditProjectRequest {
    pub recording_id: String,
    pub revision: u64,
    pub title: String,
    pub context: String,
    #[serde(default)]
    pub paged: bool,
    #[serde(default)]
    pub task: Option<String>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ProjectOperationRequest {
    pub recording_id: String,
    pub revision: u64,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct AlternativesRequest {
    pub recording_id: String,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct PrepareCaptureRequest {
    pub screen: CaptureScreen,
    pub area: CaptureArea,
    pub title: String,
    pub context: String,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct StartCaptureRequest {
    pub token: String,
    pub exclusion_ready: bool,
    pub shortcut_ready: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct CancelOperationRequest {
    pub id: String,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ModelSelectRequest {
    pub preset: String,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ModelRemoveRequest {
    pub confirmed: bool,
    #[serde(default)]
    pub revision: u64,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct GenerateRequest {
    pub recording_id: String,
    pub revision: u64,
    #[serde(default)]
    pub step_id: Option<String>,
    #[serde(default)]
    pub force: bool,
    #[serde(default)]
    pub replace_manual: bool,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ExportRequest {
    pub recording_id: String,
    pub revision: u64,
    pub parent: String,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct PromptTemplate {
    pub template: String,
    pub default_template: String,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct SavePromptTemplateRequest {
    pub template: String,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct RenderPromptRequest {
    pub recording_id: String,
    pub revision: u64,
    #[serde(default)]
    pub template: Option<String>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct RenderedPrompt {
    pub recording_id: String,
    pub revision: u64,
    pub markdown: String,
}

fn required_nullable<'de, D, T>(deserializer: D) -> Result<Option<T>, D::Error>
where
    D: serde::Deserializer<'de>,
    T: Deserialize<'de>,
{
    Option::<T>::deserialize(deserializer)
}

fn check<T: serde::de::DeserializeOwned>(value: &Value) -> Result<(), String> {
    serde_json::from_value::<T>(value.clone())
        .map(|_| ())
        .map_err(|_| "Invalid command payload".into())
}

#[rustfmt::skip]
pub fn validate_request(method: &str, value: &Value) -> Result<(), String> {
    match method {
        "prepare_open" => check::<OpenProjectRequest>(value),
        "library_page" => check::<LibraryPageRequest>(value),
        "organize_background" => check::<ProjectOperationRequest>(value),
        "organization_status" => check::<EmptyRequest>(value),
        "cancel_organization" => check::<CancelOperationRequest>(value),
        "apply_organization" => check::<ApplyOrganizationRequest>(value),
        "hello" => check::<HelloRequest>(value),
        "capabilities" => check::<EmptyRequest>(value),
        "screens" => check::<EmptyRequest>(value),
        "list_projects" => check::<EmptyRequest>(value),
        "open_project" => check::<OpenProjectRequest>(value),
        "metadata_page" => check::<MetadataPageRequest>(value),
        "frame" => check::<FrameRequest>(value),
        "edit_step" => check::<EditStepRequest>(value),
        "review_step" => check::<ReviewStepRequest>(value),
        "add_step" => check::<AddStepRequest>(value),
        "remove_step" => check::<RemoveStepRequest>(value),
        "replace_step" => check::<ReplaceStepRequest>(value),
        "edit_project" => check::<EditProjectRequest>(value),
        "organize" => check::<ProjectOperationRequest>(value),
        "alternatives" => check::<AlternativesRequest>(value),
        "prepare_capture" => check::<PrepareCaptureRequest>(value),
        "start_capture" => check::<StartCaptureRequest>(value),
        "operation_status" => check::<EmptyRequest>(value),
        "cancel_operation" => check::<CancelOperationRequest>(value),
        "ui_preferences" => check::<EmptyRequest>(value),
        "save_ui_preferences" => check::<UiPreferences>(value),
        "model_status" => check::<EmptyRequest>(value),
        "model_select" => check::<ModelSelectRequest>(value),
        "model_release" => check::<EmptyRequest>(value),
        "model_install" => check::<EmptyRequest>(value),
        "model_verify" => check::<EmptyRequest>(value),
        "model_remove" => check::<ModelRemoveRequest>(value),
        "generate" => check::<GenerateRequest>(value),
        "export" => check::<ExportRequest>(value),
        "shutdown" => check::<EmptyRequest>(value),
        "prompt_template" => check::<EmptyRequest>(value),
        "save_prompt_template" => check::<SavePromptTemplateRequest>(value),
        "reset_prompt_template" => check::<EmptyRequest>(value),
        "render_prompt" => check::<RenderPromptRequest>(value),
        _ => Err("Unknown command".into()),
    }
}

#[rustfmt::skip]
pub fn validate_response(method: &str, request: &Value, value: &Value) -> Result<(), String> {
    match method {
        "prepare_open" => check::<OperationStatus>(value),
        "library_page" => check::<LibraryPage>(value),
        "organize_background" => check::<OperationStatus>(value),
        "organization_status" => check::<Option<OperationStatus>>(value),
        "cancel_organization" => check::<Cancelled>(value),
        "apply_organization" => check::<Snapshot>(value),
        "hello" => check::<HelloResult>(value),
        "capabilities" => check::<Capabilities>(value),
        "screens" => check::<Vec<CaptureScreen>>(value),
        "list_projects" => check::<Library>(value),
        "open_project" => check::<Snapshot>(value),
        "metadata_page" => if request["kind"] == "frames" { check::<FramePage>(value) } else { check::<StepPage>(value) },
        "frame" => check::<ImageReference>(value),
        "edit_step" => check::<Snapshot>(value),
        "review_step" => check::<Snapshot>(value),
        "add_step" => check::<Snapshot>(value),
        "remove_step" => check::<Snapshot>(value),
        "replace_step" => check::<Snapshot>(value),
        "edit_project" => check::<Snapshot>(value),
        "organize" => check::<OperationStatus>(value),
        "alternatives" => check::<Suggestions>(value),
        "prepare_capture" => check::<CapturePreparation>(value),
        "start_capture" => check::<OperationStatus>(value),
        "operation_status" => check::<Option<OperationStatus>>(value),
        "cancel_operation" => check::<Cancelled>(value),
        "ui_preferences" => check::<UiPreferences>(value),
        "save_ui_preferences" => check::<UiPreferences>(value),
        "model_status" => check::<ModelStatus>(value),
        "model_select" => check::<ModelStatus>(value),
        "model_release" => check::<ModelStatus>(value),
        "model_install" => check::<OperationStatus>(value),
        "model_verify" => check::<OperationStatus>(value),
        "model_remove" => check::<OperationStatus>(value),
        "generate" => check::<OperationStatus>(value),
        "export" => check::<OperationStatus>(value),
        "shutdown" => check::<ShutdownResult>(value),
        "prompt_template" => check::<PromptTemplate>(value),
        "save_prompt_template" => check::<PromptTemplate>(value),
        "reset_prompt_template" => check::<PromptTemplate>(value),
        "render_prompt" => check::<RenderedPrompt>(value),
        _ => Err("Unknown command".into()),
    }
}
