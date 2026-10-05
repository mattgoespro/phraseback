import { describe, expect, it } from 'vitest';
import { FrameDecoder, encodeFrame, validateEnvelope } from './protocol';

describe('engine framing', () => {
  it('decodes split and adjacent frames', () => {
    const decoder = new FrameDecoder();
    const first = encodeFrame({ protocol: 1, id: 1, session: 'a', result: {}, error: null });
    const second = encodeFrame({ protocol: 1, id: 2, session: 'a', result: null, error: { code: 'x', message: 'bad' } });
    expect(decoder.push(first.subarray(0, 3))).toEqual([]);
    expect(decoder.push(Buffer.concat([first.subarray(3), second]))).toHaveLength(2);
  });

  it('rejects oversized and empty frames', () => {
    const decoder = new FrameDecoder();
    expect(() => decoder.push(Buffer.alloc(4))).toThrow();
    const large = Buffer.alloc(4);
    large.writeUInt32LE(8 * 1024 * 1024 + 1);
    expect(() => new FrameDecoder().push(large)).toThrow();
  });

  it('rejects malformed envelopes', () => {
    expect(() => validateEnvelope({ protocol: 1, id: 1, session: 'a', result: {}, error: null })).not.toThrow();
    expect(() => validateEnvelope({ protocol: 1, id: 1, session: 'a', result: {}, error: { code: 'x', message: 'bad' } })).toThrow();
    expect(() => validateEnvelope({ protocol: 2, id: 1, session: 'a', result: {}, error: null })).toThrow();
  });
});
