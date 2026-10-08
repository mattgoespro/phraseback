export type EngineEvent = { event: 'operation_status' | 'operation_saved' | 'organization_status'; data: unknown };

export type PhrasebackApi = {
  request<T>(method: string, params: Record<string, unknown>): Promise<T>;
  reconnect(): Promise<void>;
  startCapture(options: { screen: CaptureScreen; area: CaptureArea; title: string; context: string }): Promise<string | null>;
  chooseRegion(source: CaptureScreen): Promise<CaptureArea | null>;
  stopCapture(): Promise<void>;
  onCaptureFinished(listener: (recordingId: string | null) => void): () => void;
  chooseExportFolder(): Promise<string | null>;
  copyText(value: string): Promise<void>;
  onEvent(listener: (event: EngineEvent) => void): () => void;
  onDisconnect(listener: (reason: string) => void): () => void;
  onCloseRequested(handler: () => Promise<boolean>): () => void;
};

export type CaptureScreen = { id: string; name: string; left: number; top: number; width: number; height: number };
export type CaptureArea = { left: number; top: number; width: number; height: number };

declare global { interface Window { phraseback: PhrasebackApi } }
