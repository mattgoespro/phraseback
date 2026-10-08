import { describe, expect, it } from 'vitest';
import { loadRecording } from './workspace';

describe('recording loading', () => {
  it('prepares recovery and reads paged evidence before publishing', async () => {
    const calls: string[] = [];
    const request = async (method: string, params: Record<string, unknown>): Promise<any> => {
      calls.push(method);
      if (method === 'prepare_open') return { id: 'open', state: 'completed', finished: true };
      if (method === 'open_project') return { recording_id: 'r', revision: 4, frame_count: 1, step_count: 1, project: { title: 'Test', frames: [], steps: [] } };
      if (method === 'metadata_page') return params.kind === 'frames'
        ? { revision: 4, items: [{ file: 'a', time_ms: 0 }] }
        : { revision: 4, items: [{ id: 'step', frame: 0 }] };
      throw new Error(method);
    };
    const result = await loadRecording(request, 'r');
    expect(result.project.frames).toHaveLength(1);
    expect(result.project.steps).toHaveLength(1);
    expect(calls).toEqual(['prepare_open', 'open_project', 'metadata_page', 'metadata_page']);
  });
});
