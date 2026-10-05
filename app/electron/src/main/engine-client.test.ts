import { EventEmitter } from 'node:events';
import { PassThrough } from 'node:stream';
import { describe, expect, it } from 'vitest';
import { EngineClient } from './engine-client';
import { FrameDecoder, encodeFrame } from './protocol';

function harness() {
  const input = new PassThrough();
  const output = new PassThrough();
  const exited = new EventEmitter();
  const client = new EngineClient(input, output, exited, 100);
  const requests: any[] = [];
  const decoder = new FrameDecoder();
  input.on('data', (chunk: Buffer) => requests.push(...decoder.push(chunk)));
  return { client, output, exited, requests };
}

describe('engine session', () => {
  it('matches response IDs and preserves one session', async () => {
    const h = harness();
    const first = h.client.request('hello', { notifications: true });
    await Promise.resolve();
    const id = h.requests[0].id;
    h.output.write(encodeFrame({ protocol: 1, id, session: 's', result: { protocol_minor: 2 }, error: null }));
    expect(await first).toEqual({ protocol_minor: 2 });
    const second = h.client.request('capabilities', {});
    await Promise.resolve();
    h.output.write(encodeFrame({ protocol: 1, id: h.requests[1].id, session: 'other', result: {}, error: null }));
    await expect(second).rejects.toThrow('session');
    h.client.close();
  });

  it('emits notifications and rejects pending requests on disconnect', async () => {
    const h = harness();
    const events: unknown[] = [];
    h.client.on('notification', value => events.push(value));
    h.output.write(encodeFrame({ protocol: 1, session: 's', event: 'operation_saved', data: { id: 'one' } }));
    expect(events).toHaveLength(1);
    const pending = h.client.request('hello', {});
    h.exited.emit('exit');
    await expect(pending).rejects.toThrow('disconnected');
  });

  it('delivers every durable save acknowledgement before completion', async () => {
    const h = harness();
    const events: string[] = [];
    h.client.on('notification', value => events.push(`${value.event}:${value.data.result?.saved ?? value.data.state}`));
    const pending = h.client.request('hello', { notifications: true });
    await Promise.resolve();
    const id = h.requests[0].id;
    h.output.write(Buffer.concat([
      encodeFrame({ protocol: 1, session: 's', event: 'operation_saved', data: { result: { saved: 1 } } }),
      encodeFrame({ protocol: 1, session: 's', event: 'operation_saved', data: { result: { saved: 2 } } }),
      encodeFrame({ protocol: 1, session: 's', event: 'operation_status', data: { state: 'completed' } }),
      encodeFrame({ protocol: 1, id, session: 's', result: { protocol_minor: 2 }, error: null })
    ]));
    await pending;
    expect(events).toEqual(['operation_saved:1', 'operation_saved:2', 'operation_status:completed']);
    h.client.close();
  });

  it('bounds queued engine requests while the engine is busy', async () => {
    const h = harness();
    const pending = Array.from({ length: 64 }, () => h.client.request('library_page', {}));
    await expect(h.client.request('library_page', {})).rejects.toThrow('queue is full');
    h.client.close();
    await Promise.allSettled(pending);
  });
});
