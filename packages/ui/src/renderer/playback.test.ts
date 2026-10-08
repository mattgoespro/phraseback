import { describe, expect, it } from 'vitest';
import { frameAtTime } from './playback';

describe('review playback', () => {
  it('uses recorded timestamps rather than a fixed frame interval', () => {
    const frames = [{ time_ms: 0 }, { time_ms: 100 }, { time_ms: 900 }];
    expect(frameAtTime(frames, 600)).toBe(1);
    expect(frameAtTime(frames, 900)).toBe(2);
    expect(frameAtTime(frames, 2000)).toBe(2);
  });
});
