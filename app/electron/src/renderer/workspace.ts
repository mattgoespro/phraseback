import type { Frame as ContractFrame, Step as ContractStep, LibraryEntry as ContractLibraryEntry, Project as ContractProject, Snapshot as ContractSnapshot, OperationStatus } from '../shared/contracts.generated';

export type Frame = ContractFrame;
export type Step = ContractStep;
export type LibraryEntry = ContractLibraryEntry;
export type Snapshot = Omit<ContractSnapshot, 'project' | 'frame_count' | 'step_count'> & { frame_count: number; step_count: number; project: ContractProject };
export type Operation = Omit<OperationStatus, 'result'> & { result: Record<string, unknown> | null };
export type Request = <T>(method: string, params: Record<string, unknown>) => Promise<T>;

const delay = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

export async function loadLibrary(request: Request, refresh = true, offset = 0, catalogId: string | null = null) {
  for (;;) {
    const page = await request<{ items: LibraryEntry[]; indexing: boolean; catalog_id: string; next_offset: number | null }>('library_page', { offset, limit: 64, catalog_id: catalogId, refresh });
    if (!page.indexing) return page;
    catalogId = page.catalog_id;
    refresh = false;
    await delay(75);
  }
}

export async function loadRecording(request: Request, recordingId: string, onOpening?: (operation: Operation | null) => void): Promise<Snapshot> {
  const params = { recording_id: recordingId, paged: true };
  let opening = await request<Operation>('prepare_open', params);
  onOpening?.(opening);
  try {
    while (!opening.finished) {
      await delay(75);
      const status = await request<Operation>('operation_status', {});
      if (status.id !== opening.id) throw new Error('Recording recovery identity changed');
      opening = status;
    }
  } finally {
    onOpening?.(null);
  }
  if (opening.state !== 'completed') throw new Error(opening.error || 'Recording could not be opened');
  const snapshot = await request<Snapshot>('open_project', params);
  async function page<T>(kind: 'frames' | 'steps', count: number): Promise<T[]> {
    const items: T[] = [];
    while (items.length < count) {
      const result = await request<{ items: T[]; revision: number }>('metadata_page', { recording_id: recordingId, revision: snapshot.revision, kind, offset: items.length, limit: 128 });
      if (result.revision !== snapshot.revision || !result.items.length || items.length + result.items.length > count) throw new Error('Invalid recording metadata page');
      items.push(...result.items);
    }
    return items;
  }
  const [frames, steps] = await Promise.all([page<Frame>('frames', snapshot.frame_count), page<Step>('steps', snapshot.step_count)]);
  return { ...snapshot, project: { ...snapshot.project, frames, steps } };
}
