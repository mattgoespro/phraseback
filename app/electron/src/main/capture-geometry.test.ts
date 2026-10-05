import { describe, expect, it } from 'vitest';
import { matchCaptureDisplay, regionToPixels } from './capture-geometry';

describe('capture region mapping', () => {
  it('maps a DIP drag to physical pixels on a scaled display', () => {
    expect(regionToPixels({ left: 100, top: 200, width: 2560, height: 1600 }, 1.25, { x: 80, y: 40, width: 400, height: 300 }))
      .toEqual({ left: 200, top: 250, width: 500, height: 375 });
  });

  it('clamps a drag to the selected screen', () => {
    expect(regionToPixels({ left: 0, top: 0, width: 100, height: 100 }, 1, { x: -10, y: 20, width: 130, height: 90 }))
      .toEqual({ left: 0, top: 20, width: 100, height: 80 });
  });

  it('picks the selected display when two monitors share a resolution', () => {
    const primary = { id: 1, bounds: { x: 0, y: 0, width: 1920, height: 1080 }, scaleFactor: 1 };
    const secondary = { id: 2, bounds: { x: 1920, y: 0, width: 1920, height: 1080 }, scaleFactor: 1 };
    expect(matchCaptureDisplay([primary, secondary], { left: 1920, top: 0, width: 1920, height: 1080 }, primary).id).toBe(2);
    expect(matchCaptureDisplay([primary, secondary], { left: 0, top: 0, width: 1920, height: 1080 }, secondary).id).toBe(1);
  });
});
