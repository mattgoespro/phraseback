// Generated from packages/engine/contracts/*.schema.json. Do not edit.
export type PhrasebackPrivateStdioProtocolEnvelopes = Request | Response | Notification;
export type Response = {
  protocol: number;
  id: number;
  session: string;
  result: {} | unknown[] | null;
  error: Error | null;
} & Response1;
export type Response1 =
  | {
      error?: null;
      result?: {} | unknown[] | null;
    }
  | {
      error?: Error;
      result?: null;
    };

export interface Request {
  protocol: number;
  id: number;
  method: string;
  params: {};
}
export interface Error {
  code: string;
  message: string;
}
export interface Notification {
  protocol: number;
  session: string;
  event: 'operation_status' | 'operation_saved' | 'organization_status';
  data: {};
}

export interface CommandDefinitions {
  UiPreferences?: UiPreferences;
  ApplyOrganizationRequest?: ApplyOrganizationRequest;
  LibraryPageRequest?: LibraryPageRequest;
  LibraryPage?: LibraryPage;
  EmptyRequest?: EmptyRequest;
  HelloRequest?: HelloRequest;
  HelloResult?: HelloResult;
  Capabilities?: Capabilities;
  Frame?: Frame;
  Step?: Step;
  Project?: Project;
  Snapshot?: Snapshot;
  FramePage?: FramePage;
  StepPage?: StepPage;
  LibraryEntry?: LibraryEntry;
  Library?: Library;
  ImageReference?: ImageReference;
  CaptureScreen?: CaptureScreen;
  CaptureArea?: CaptureArea;
  CapturePreparation?: CapturePreparation;
  OperationStatus?: OperationStatus;
  Suggestions?: Suggestions;
  ModelAsset?: ModelAsset;
  ModelPreset?: ModelPreset;
  ModelStatus?: ModelStatus;
  Cancelled?: Cancelled;
  ShutdownResult?: ShutdownResult;
  OpenProjectRequest?: OpenProjectRequest;
  MetadataPageRequest?: MetadataPageRequest;
  FrameRequest?: FrameRequest;
  EditStepRequest?: EditStepRequest;
  ReviewStepRequest?: ReviewStepRequest;
  AddStepRequest?: AddStepRequest;
  RemoveStepRequest?: RemoveStepRequest;
  ReplaceStepRequest?: ReplaceStepRequest;
  EditProjectRequest?: EditProjectRequest;
  ProjectOperationRequest?: ProjectOperationRequest;
  AlternativesRequest?: AlternativesRequest;
  PrepareCaptureRequest?: PrepareCaptureRequest;
  StartCaptureRequest?: StartCaptureRequest;
  CancelOperationRequest?: CancelOperationRequest;
  ModelSelectRequest?: ModelSelectRequest;
  ModelRemoveRequest?: ModelRemoveRequest;
  GenerateRequest?: GenerateRequest;
  ExportRequest?: ExportRequest;
  PromptTemplate?: PromptTemplate;
  SavePromptTemplateRequest?: SavePromptTemplateRequest;
  RenderPromptRequest?: RenderPromptRequest;
  RenderedPrompt?: RenderedPrompt;
}
export interface UiPreferences {
  width: number;
  height: number;
  maximized: boolean;
  theme?: string | null;
}
export interface ApplyOrganizationRequest {
  recording_id: string;
  revision: number;
  id: string;
  paged?: boolean;
}
export interface LibraryPageRequest {
  offset?: number;
  limit?: number;
  catalog_id?: string | null;
  refresh?: boolean;
}
export interface LibraryPage {
  items: LibraryEntry[];
  catalog_id: string;
  next_offset: number | null;
  indexing: boolean;
}
export interface LibraryEntry {
  id: string;
  title: string;
  created: string;
  duration_ms: number;
  steps: number;
}
export interface EmptyRequest {
  revision?: number;
}
export interface HelloRequest {
  notifications?: boolean;
}
export interface HelloResult {
  engine_version: string;
  protocol_minor: number;
  notifications?: boolean;
}
export interface Capabilities {
  prototype: boolean;
  shared_data: boolean;
  capture: boolean;
  generation: boolean;
  export: boolean;
  editing: boolean;
  source_enumeration: boolean;
  system_selection: boolean;
  region_cropping: boolean;
  cursor_modes: string[];
  control_exclusion: boolean;
}
export interface Frame {
  file: string;
  time_ms: number;
}
export interface Step {
  frame: number;
  id: string;
  title: string;
  action: string;
  result: string;
  status: string;
  manual: boolean;
  reviewed: boolean;
  uncertainty: string;
}
export interface Project {
  version: number;
  title: string;
  context: string;
  created: string;
  frames: Frame1[];
  steps: Step1[];
  duration_ms: number;
  width: number;
  height: number;
  error: string;
  state: string;
  task?: string;
}
export interface Frame1 {
  file: string;
  time_ms: number;
}
export interface Step1 {
  frame: number;
  id: string;
  title: string;
  action: string;
  result: string;
  status: string;
  manual: boolean;
  reviewed: boolean;
  uncertainty: string;
}
export interface Snapshot {
  recording_id: string;
  revision: number;
  project: Project1;
  frame_count?: number;
  step_count?: number;
}
export interface Project1 {
  version: number;
  title: string;
  context: string;
  created: string;
  frames: Frame1[];
  steps: Step1[];
  duration_ms: number;
  width: number;
  height: number;
  error: string;
  state: string;
  task?: string;
}
export interface FramePage {
  items: Frame2[];
  revision: number;
  next_offset: number | null;
}
export interface Frame2 {
  file: string;
  time_ms: number;
}
export interface StepPage {
  items: Step2[];
  revision: number;
  next_offset: number | null;
}
export interface Step2 {
  frame: number;
  id: string;
  title: string;
  action: string;
  result: string;
  status: string;
  manual: boolean;
  reviewed: boolean;
  uncertainty: string;
}
export interface Library {
  items: LibraryEntry1[];
}
export interface LibraryEntry1 {
  id: string;
  title: string;
  created: string;
  duration_ms: number;
  steps: number;
}
export interface ImageReference {
  path: string;
  recording_id: string;
  index: number;
  revision: number;
}
export interface CaptureScreen {
  id: string;
  name: string;
  left: number;
  top: number;
  width: number;
  height: number;
}
export interface CaptureArea {
  left: number;
  top: number;
  width: number;
  height: number;
}
export interface CapturePreparation {
  token: string;
}
export interface OperationStatus {
  id: string;
  kind: string;
  state: string;
  progress: number;
  message: string;
  recording_id: string | null;
  revision: number;
  finished: boolean;
  error: string | null;
  result: {} | null;
}
export interface Suggestions {
  selected: number[];
  intervals: number[][];
}
export interface ModelAsset {
  name: string;
  url: string;
  size: number;
  sha256: string;
}
export interface ModelPreset {
  id: string;
  title: string;
  model_id: string;
  revision: string;
  gpu: boolean;
  ram_gb: number;
  suggested_vram_gb: number;
  assets: ModelAsset1[];
}
export interface ModelAsset1 {
  name: string;
  url: string;
  size: number;
  sha256: string;
}
export interface ModelStatus {
  presets: ModelPreset1[];
  selected: string;
  state: string;
  assets_present: boolean;
  verified: boolean;
}
export interface ModelPreset1 {
  id: string;
  title: string;
  model_id: string;
  revision: string;
  gpu: boolean;
  ram_gb: number;
  suggested_vram_gb: number;
  assets: ModelAsset1[];
}
export interface Cancelled {
  cancelling: boolean;
}
export interface ShutdownResult {
  stopped: boolean;
}
export interface OpenProjectRequest {
  recording_id: string;
  paged?: boolean;
}
export interface MetadataPageRequest {
  recording_id: string;
  revision: number;
  kind: string;
  offset: number;
  limit?: number;
}
export interface FrameRequest {
  recording_id: string;
  index: number;
  preview_width?: number | null;
}
export interface EditStepRequest {
  recording_id: string;
  revision: number;
  step_id: string;
  title: string;
  action: string;
  result: string;
  uncertainty: string;
  paged?: boolean;
}
export interface ReviewStepRequest {
  recording_id: string;
  revision: number;
  step_id: string;
  reviewed: boolean;
  paged?: boolean;
}
export interface AddStepRequest {
  recording_id: string;
  revision: number;
  frame: number;
  step_id?: string | null;
  paged?: boolean;
}
export interface RemoveStepRequest {
  recording_id: string;
  revision: number;
  step_id: string;
  frame?: number | null;
  paged?: boolean;
}
export interface ReplaceStepRequest {
  recording_id: string;
  revision: number;
  step_id: string;
  frame: number;
  paged?: boolean;
}
export interface EditProjectRequest {
  recording_id: string;
  revision: number;
  title: string;
  context: string;
  paged?: boolean;
  task?: string | null;
}
export interface ProjectOperationRequest {
  recording_id: string;
  revision: number;
}
export interface AlternativesRequest {
  recording_id: string;
}
export interface PrepareCaptureRequest {
  screen: CaptureScreen1;
  area: CaptureArea1;
  title: string;
  context: string;
}
export interface CaptureScreen1 {
  id: string;
  name: string;
  left: number;
  top: number;
  width: number;
  height: number;
}
export interface CaptureArea1 {
  left: number;
  top: number;
  width: number;
  height: number;
}
export interface StartCaptureRequest {
  token: string;
  exclusion_ready: boolean;
  shortcut_ready: boolean;
}
export interface CancelOperationRequest {
  id: string;
}
export interface ModelSelectRequest {
  preset: string;
}
export interface ModelRemoveRequest {
  confirmed: boolean;
  revision?: number;
}
export interface GenerateRequest {
  recording_id: string;
  revision: number;
  step_id?: string | null;
  force?: boolean;
  replace_manual?: boolean;
}
export interface ExportRequest {
  recording_id: string;
  revision: number;
  parent: string;
}
export interface PromptTemplate {
  template: string;
  default_template: string;
}
export interface SavePromptTemplateRequest {
  template: string;
}
export interface RenderPromptRequest {
  recording_id: string;
  revision: number;
  template?: string | null;
}
export interface RenderedPrompt {
  recording_id: string;
  revision: number;
  markdown: string;
}
