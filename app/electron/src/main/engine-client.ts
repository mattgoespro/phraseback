import { EventEmitter } from 'node:events';
import type { Writable, Readable } from 'node:stream';
import { encodeFrame, FrameDecoder, validateEnvelope, type EngineResponse } from './protocol';

type Pending = { resolve(value: unknown): void; reject(reason: Error): void; timer: NodeJS.Timeout };

export class EngineClient extends EventEmitter {
  private readonly decoder = new FrameDecoder();
  private readonly pending = new Map<number, Pending>();
  private session: string | null = null;
  private id = 0;
  private disconnected = false;
  private queue: Promise<unknown> = Promise.resolve();
  private outstanding = 0;

  constructor(private readonly input: Writable, output: Readable, exited: EventEmitter, private readonly timeoutMs = 10_000) {
    super();
    output.on('data', (chunk: Buffer) => {
      try {
        for (const frame of this.decoder.push(chunk)) this.accept(frame);
      } catch (error) { this.fail(error instanceof Error ? error : new Error(String(error))); }
    });
    output.on('error', error => this.fail(error));
    output.on('end', () => this.fail(new Error('Engine disconnected')));
    exited.on('exit', () => this.fail(new Error('Engine disconnected')));
  }

  request<T = unknown>(method: string, params: Record<string, unknown>): Promise<T> {
    if (this.outstanding >= 64) return Promise.reject(new Error('Engine request queue is full'));
    this.outstanding++;
    const operation = this.queue.then(() => this.send(method, params)).finally(() => { this.outstanding--; }) as Promise<T>;
    this.queue = operation.catch(() => undefined);
    return operation;
  }

  private send(method: string, params: Record<string, unknown>): Promise<unknown> {
    if (this.disconnected) return Promise.reject(new Error('Engine disconnected'));
    const id = ++this.id;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id);
        this.fail(new Error(`Engine request timed out: ${method}`));
      }, this.timeoutMs);
      this.pending.set(id, { resolve, reject, timer });
      try { this.input.write(encodeFrame({ protocol: 1, id, method, params })); }
      catch (error) { this.fail(error instanceof Error ? error : new Error(String(error))); }
    });
  }

  private accept(frame: unknown): void {
    const message = validateEnvelope(frame);
    if (this.session !== null && message.session !== this.session) throw new Error('Engine session changed');
    this.session ??= message.session;
    if (!('id' in message)) {
      this.emit('notification', message);
      return;
    }
    const response = message as EngineResponse;
    const pending = this.pending.get(response.id);
    if (!pending) throw new Error('Unexpected engine response');
    this.pending.delete(response.id);
    clearTimeout(pending.timer);
    if (response.error) pending.reject(Object.assign(new Error(response.error.message), { code: response.error.code }));
    else pending.resolve(response.result);
  }

  private fail(error: Error): void {
    if (this.disconnected) return;
    this.disconnected = true;
    for (const pending of this.pending.values()) { clearTimeout(pending.timer); pending.reject(error); }
    this.pending.clear();
    this.emit('disconnected', error);
  }

  close(): void { this.fail(new Error('Engine disconnected')); this.input.end(); }
}
