import { describe, expect, it } from 'vitest';
import { regionToPixels } from './capture-geometry';

describe('capture region mapping', () => {
  it('maps a DIP drag to physical pixels on a scaled display', () => {
    expect(regionToPixels({ left: 100, top: 200, width: 2560, height: 1600 }, 1.25, { x: 80, y: 40, width: 400, height: 300 }))
      .toEqual({ left: 200, top: 250, width: 500, height: 375 });
  });

  it('clamps a drag to the selected screen', () => {
    expect(regionToPixels({ left: 0, top: 0, width: 100, height: 100 }, 1, { x: -10, y: 20, width: 130, height: 90 }))
      .toEqual({ left: 0, top: 20, width: 100, height: 80 });
  });
});
