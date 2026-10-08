export const MAX_MESSAGE_BYTES = 8 * 1024 * 1024;

export function encodeFrame(value: unknown): Buffer {
  const body = Buffer.from(JSON.stringify(value), 'utf8');
  if (!body.length || body.length > MAX_MESSAGE_BYTES) throw new Error('Invalid engine frame size');
  const header = Buffer.alloc(4);
  header.writeUInt32LE(body.length);
  return Buffer.concat([header, body]);
}

export type EngineResponse = { protocol: 1; id: number; session: string; result: unknown; error: null | { code: string; message: string } };
export type EngineNotification = { protocol: 1; session: string; event: 'operation_status' | 'operation_saved' | 'organization_status'; data: unknown };

export function validateEnvelope(value: unknown): EngineResponse | EngineNotification {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) throw new Error('Invalid engine envelope');
  const v = value as Record<string, unknown>;
  if (v.protocol !== 1 || typeof v.session !== 'string' || !v.session) throw new Error('Engine identity mismatch');
  if ('id' in v) {
    if (!Number.isSafeInteger(v.id) || (v.id as number) < 0 || !('result' in v) || !('error' in v)) throw new Error('Invalid engine response');
    if (v.error !== null) {
      const error = v.error as Record<string, unknown> | null;
      if (!error || typeof error.code !== 'string' || typeof error.message !== 'string' || v.result !== null) throw new Error('Invalid engine failure');
    }
    return v as EngineResponse;
  }
  if (!['operation_status', 'operation_saved', 'organization_status'].includes(String(v.event)) || !('data' in v)) throw new Error('Invalid engine notification');
  return v as EngineNotification;
}

export class FrameDecoder {
  private pending = Buffer.alloc(0);

  push(chunk: Buffer): unknown[] {
    this.pending = Buffer.concat([this.pending, chunk]);
    const result: unknown[] = [];
    while (this.pending.length >= 4) {
      const length = this.pending.readUInt32LE(0);
      if (!length || length > MAX_MESSAGE_BYTES) throw new Error('Invalid engine frame size');
      if (this.pending.length < 4 + length) break;
      result.push(JSON.parse(this.pending.subarray(4, 4 + length).toString('utf8')));
      this.pending = this.pending.subarray(4 + length);
    }
    return result;
  }
}
