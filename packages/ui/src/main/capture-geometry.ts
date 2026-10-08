import type { CaptureArea, CaptureScreen } from '../shared/api';

export type DisplayLike = { bounds: { x: number; y: number; width: number; height: number }; scaleFactor: number };

function close(left: number, right: number): boolean {
  return Math.abs(left - right) <= 2;
}

export function matchCaptureDisplay<T extends DisplayLike>(displays: readonly T[], source: Pick<CaptureScreen, 'left' | 'top' | 'width' | 'height'>, fallback: T): T {
  const size = (display: T) => close(display.bounds.width * display.scaleFactor, source.width) && close(display.bounds.height * display.scaleFactor, source.height);
  const origin = (display: T) => close(display.bounds.x * display.scaleFactor, source.left) && close(display.bounds.y * display.scaleFactor, source.top);
  return displays.find(display => size(display) && origin(display)) ?? displays.find(size) ?? fallback;
}

export function regionToPixels(source: Pick<CaptureScreen, 'left' | 'top' | 'width' | 'height'>, scale: number, drag: { x: number; y: number; width: number; height: number }): CaptureArea {
  const left = Math.max(0, Math.min(source.width, Math.round(drag.x * scale)));
  const top = Math.max(0, Math.min(source.height, Math.round(drag.y * scale)));
  const right = Math.max(left, Math.min(source.width, Math.round((drag.x + drag.width) * scale)));
  const bottom = Math.max(top, Math.min(source.height, Math.round((drag.y + drag.height) * scale)));
  return { left: source.left + left, top: source.top + top, width: right - left, height: bottom - top };
}
