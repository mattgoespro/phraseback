export function frameAtTime(frames: readonly { time_ms: number }[], timeMs: number): number {
  if (!frames.length) return 0;
  let low = 0;
  let high = frames.length - 1;
  while (low < high) {
    const middle = Math.ceil((low + high) / 2);
    if (frames[middle].time_ms <= timeMs) low = middle;
    else high = middle - 1;
  }
  return low;
}
