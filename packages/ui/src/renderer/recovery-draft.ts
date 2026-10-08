import type { Snapshot, Step } from './workspace';

export type DraftFields = Pick<Step, 'title' | 'action' | 'result' | 'uncertainty'>;
export type DraftRecovery = 'save' | 'saved' | 'conflict';

export function draftRecovery(original: Snapshot, recovered: Snapshot, stepId: string, draft: DraftFields): DraftRecovery {
  const before = original.project;
  const after = recovered.project;
  if (original.recording_id !== recovered.recording_id || before.context !== after.context ||
      before.width !== after.width || before.height !== after.height ||
      before.frames.length !== after.frames.length || before.steps.length !== after.steps.length ||
      before.frames.some((frame, index) => frame.file !== after.frames[index].file || frame.time_ms !== after.frames[index].time_ms) ||
      before.steps.some((step, index) => step.id !== after.steps[index].id || step.frame !== after.steps[index].frame)) return 'conflict';
  const previous = before.steps.find(step => step.id === stepId);
  const current = after.steps.find(step => step.id === stepId);
  if (!previous || !current) return 'conflict';
  if (current.title === draft.title && current.action === draft.action && current.result === draft.result &&
      current.uncertainty === draft.uncertainty && current.manual && current.status === 'edited' && !current.reviewed) return 'saved';
  return current.title === previous.title && current.action === previous.action && current.result === previous.result &&
    current.uncertainty === previous.uncertainty && current.status === previous.status && current.manual === previous.manual &&
    current.reviewed === previous.reviewed ? 'save' : 'conflict';
}
