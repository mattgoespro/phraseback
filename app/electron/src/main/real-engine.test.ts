import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { chmodSync, cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import { EngineClient } from './engine-client';
import { validateRequest, validateResponse } from './contracts';
import { encodeFrame, FrameDecoder } from './protocol';
import { loadLibrary, loadRecording } from '../renderer/workspace';

const repo = path.resolve(import.meta.dirname, '../../../..');
const binary = process.env.PHRASEBACK_TEST_ENGINE || path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe');

describe.skipIf(!existsSync(binary))('real Rust engine contract', () => {
  function isolatedRoot(): string {
    const parent = path.join(repo, '.tmp/electron');
    mkdirSync(parent, { recursive: true });
    const root = mkdtempSync(path.join(parent, 'contract-'));
    writeFileSync(path.join(root, '.flow-recorder-development'), 'isolated contract test\n');
    return root;
  }

  it('handshakes, validates a library page and shuts down on isolated data', async () => {
    const root = isolatedRoot();
    const child = spawn(binary, ['--data-root', root], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    child.stderr.resume();
    const client = new EngineClient(child.stdin, child.stdout, child, 5_000);
    try {
      validateRequest('hello', { notifications: true });
      const hello = await client.request('hello', { notifications: true });
      validateResponse('hello', hello);
      expect(hello).toMatchObject({ protocol_minor: 2, notifications: true });
      validateRequest('library_page', { offset: 0, limit: 10 });
      const page = await client.request('library_page', { offset: 0, limit: 10 });
      validateResponse('library_page', page);
      expect(page).toMatchObject({ items: [] });
      const shutdown = await client.request('shutdown', {});
      validateResponse('shutdown', shutdown);
    } finally {
      client.close();
      if (child.exitCode === null) child.kill();
    }
  });

  it('rejects an incompatible handshake before opening data', async () => {
    const root = isolatedRoot();
    const child = spawn(binary, ['--data-root', root], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    child.stderr.resume();
    try {
      const response = new Promise<unknown>((resolve, reject) => {
        const decoder = new FrameDecoder();
        const timer = setTimeout(() => reject(new Error('Version rejection timed out')), 3_000);
        child.stdout.on('data', (chunk: Buffer) => {
          try {
            const frames = decoder.push(chunk);
            if (frames.length) { clearTimeout(timer); resolve(frames[0]); }
          } catch (error) { clearTimeout(timer); reject(error); }
        });
      });
      child.stdin.write(encodeFrame({ protocol: 2, id: 1, method: 'hello', params: { notifications: true } }));
      await expect(response).resolves.toMatchObject({ error: { code: 'protocol_mismatch' } });
    } finally { if (child.exitCode === null) child.kill(); }
  });

  it('terminates on a malformed frame without touching the isolated library', async () => {
    const root = isolatedRoot();
    const child = spawn(binary, ['--data-root', root], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    child.stderr.resume(); child.stdout.resume();
    try {
      child.stdin.write(Buffer.alloc(4)); // Zero-length frame is forbidden.
      const [code] = await once(child, 'exit');
      expect(code).toBe(1);
      expect(existsSync(path.join(root, 'sessions'))).toBe(false);
    } finally { if (child.exitCode === null) child.kill(); }
  });

  it('rejects stale Review edits against a real version-1 recording', async () => {
    const root = isolatedRoot();
    cpSync(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
    const child = spawn(binary, ['--data-root', root], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    child.stderr.resume();
    const client = new EngineClient(child.stdin, child.stdout, child, 5_000);
    try {
      await client.request('hello', { notifications: false });
      const library = await loadLibrary((method, params) => client.request(method, params));
      expect(library.items.length).toBeGreaterThan(0);
      const snapshot = await loadRecording((method, params) => client.request(method, params), library.items[0].id);
      expect(snapshot.project.steps.length).toBeGreaterThan(0);
      const step = snapshot.project.steps[0];
      await expect(client.request('review_step', { recording_id: snapshot.recording_id, revision: snapshot.revision + 1, step_id: step.id, reviewed: true, paged: true }))
        .rejects.toThrow();
      await client.request('shutdown', {});
    } finally {
      client.close();
      if (child.exitCode === null) child.kill();
    }
  });

  it('rejects a failed durable save and leaves the original project unchanged', async () => {
    const root = isolatedRoot();
    cpSync(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
    const child = spawn(binary, ['--data-root', root], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    child.stderr.resume();
    const client = new EngineClient(child.stdin, child.stdout, child, 5_000);
    let projectFile = '';
    try {
      await client.request('hello', { notifications: false });
      const library = await loadLibrary((method, params) => client.request(method, params));
      const snapshot = await loadRecording((method, params) => client.request(method, params), library.items[0].id);
      const step = snapshot.project.steps[0];
      projectFile = path.join(root, 'sessions', snapshot.recording_id, 'project.json');
      const original = readFileSync(projectFile);
      chmodSync(projectFile, 0o444);
      await expect(client.request('edit_step', {
        recording_id: snapshot.recording_id, revision: snapshot.revision, step_id: step.id,
        title: `${step.title} changed`, action: step.action, result: step.result,
        uncertainty: step.uncertainty, paged: true
      })).rejects.toThrow();
      expect(readFileSync(projectFile)).toEqual(original);
      const reopened = await loadRecording((method, params) => client.request(method, params), snapshot.recording_id);
      expect(reopened.revision).toBeGreaterThan(snapshot.revision); // Reopen creates a fresh view revision.
      expect(reopened.project.steps[0].title).toBe(step.title);
      await client.request('shutdown', {});
    } finally {
      if (projectFile && existsSync(projectFile)) chmodSync(projectFile, 0o666);
      client.close();
      if (child.exitCode === null) child.kill();
    }
  });
});
