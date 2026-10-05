import type { CaptureArea, CaptureScreen } from '../shared/api';

export function regionToPixels(source: Pick<CaptureScreen, 'left' | 'top' | 'width' | 'height'>, scale: number, drag: { x: number; y: number; width: number; height: number }): CaptureArea {
  const left = Math.max(0, Math.min(source.width, Math.round(drag.x * scale)));
  const top = Math.max(0, Math.min(source.height, Math.round(drag.y * scale)));
  const right = Math.max(left, Math.min(source.width, Math.round((drag.x + drag.width) * scale)));
  const bottom = Math.max(top, Math.min(source.height, Math.round((drag.y + drag.height) * scale)));
  return { left: source.left + left, top: source.top + top, width: right - left, height: bottom - top };
}
