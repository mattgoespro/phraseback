import { describe, expect, it } from 'vitest';
import { draftRecovery, type DraftFields } from './recovery-draft';
import type { Snapshot } from './workspace';

const fields: DraftFields = { title: 'Changed', action: 'Click', result: 'Opened', uncertainty: '' };
function snapshot(): Snapshot {
  return {
    recording_id: 'r', revision: 2, frame_count: 1, step_count: 1,
    project: { version: 1, title: 'Demo', context: '', created: '', frames: [{ file: '0.png', time_ms: 0 }],
      steps: [{ id: 's', frame: 0, title: 'Original', action: '', result: '', uncertainty: '', status: 'pending', manual: false, reviewed: false }],
      duration_ms: 0, width: 100, height: 100, error: '', state: 'ready' }
  };
}

describe('draft recovery', () => {
  it('permits saving when evidence and selected description are unchanged', () => {
    expect(draftRecovery(snapshot(), snapshot(), 's', fields)).toBe('save');
  });
  it('accepts a committed draft when only its acknowledgement was lost', () => {
    const recovered = snapshot();
    recovered.revision++;
    recovered.project.steps[0] = { ...recovered.project.steps[0], ...fields, manual: true, status: 'edited' };
    expect(draftRecovery(snapshot(), recovered, 's', fields)).toBe('saved');
  });
  it('holds the draft when evidence or another description changed', () => {
    const differentFrame = snapshot();
    differentFrame.project.frames[0].file = 'other.png';
    expect(draftRecovery(snapshot(), differentFrame, 's', fields)).toBe('conflict');
    const differentStep = snapshot();
    differentStep.project.steps[0].title = 'Other writer';
    expect(draftRecovery(snapshot(), differentStep, 's', fields)).toBe('conflict');
  });
});
