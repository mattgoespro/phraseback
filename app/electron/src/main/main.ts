import { app, BrowserWindow, clipboard, dialog, globalShortcut, ipcMain, net, protocol, screen } from 'electron';
import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { existsSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { randomUUID } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { validateRequest, validateResponse } from './contracts';
import { EngineClient } from './engine-client';
import type { EngineEvent } from '../shared/api';
import type { CaptureArea, CaptureScreen } from '../shared/api';
import { regionToPixels } from './capture-geometry';

protocol.registerSchemesAsPrivileged([{ scheme: 'phraseback-media', privileges: { standard: true, secure: true, supportFetchAPI: true, stream: true } }]);

let window: BrowserWindow | null = null;
let child: ChildProcessWithoutNullStreams | null = null;
let engine: EngineClient | null = null;
const media = new Map<string, string>();
const mediaTokens = new Map<string, string>();
let capture: { id: string | null; stop: boolean; controls: BrowserWindow; outline: BrowserWindow[] } | null = null;
let allowClose = false;
let closeRequested = false;
let finishClose: ((saved: boolean) => void) | null = null;
let closeDeadline: NodeJS.Timeout | null = null;
let reconnecting = false;

function argument(name: string): string | null {
  const position = process.argv.indexOf(name);
  if (position < 0) return null;
  const value = process.argv[position + 1];
  if (!value || value.startsWith('--')) throw new Error(`${name} requires a path`);
  return path.resolve(value);
}

function paths() {
  const localData = process.env.LOCALAPPDATA || path.join(tmpdir(), 'Phraseback');
  const legacy = path.join(localData, 'FlowRecorder');
  const root = argument('--data-root') || process.env.PHRASEBACK_DATA || process.env.FLOW_RECORDER_DATA || (existsSync(legacy) ? legacy : path.join(localData, 'Phraseback'));
  const executable = argument('--engine') || (app.isPackaged
    ? path.join(path.dirname(process.execPath), 'Phraseback.Engine.exe')
    : path.resolve(__dirname, '../../../../.tmp/rebuild/prototype-payload/Phraseback.Engine.exe'));
  if (!app.isPackaged && !existsSync(path.join(root, '.flow-recorder-development')))
    throw new Error('Development builds require a marked isolated data root.');
  return { root: path.resolve(root), executable };
}

async function request<T>(method: string, params: Record<string, unknown>, internal = false): Promise<T> {
  if (!engine) throw new Error('Local engine unavailable');
  if (!internal && (method === 'shutdown' || method === 'start_capture' || method === 'prepare_capture')) throw new Error('Command unavailable in the UI');
  validateRequest(method, params);
  const result = await engine.request<T>(method, params);
  validateResponse(method, result);
  if (method === 'frame' && result && typeof result === 'object' && 'path' in result) {
    const reference = result as { path: string } & Record<string, unknown>;
    if (typeof reference.path === 'string' && path.isAbsolute(reference.path) && path.extname(reference.path).toLowerCase() === '.png') {
      const token = mediaTokens.get(reference.path) || randomUUID();
      media.delete(token);
      media.set(token, reference.path);
      mediaTokens.set(reference.path, token);
      while (media.size > 128) {
        const oldest = media.keys().next().value!;
        mediaTokens.delete(media.get(oldest)!);
        media.delete(oldest);
      }
      return { ...reference, url: `phraseback-media://image/${token}` } as T;
    }
  }
  return result;
}

async function startEngine() {
  const { root, executable } = paths();
  if (!existsSync(executable)) throw new Error(`Engine not found: ${executable}`);
  child = spawn(executable, ['--data-root', root], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
  child.stderr.resume(); // Drain content-free diagnostics without sending them to the renderer.
  engine = new EngineClient(child.stdin, child.stdout, child);
  engine.on('notification', message => window?.webContents.send('engine:event', { event: message.event, data: message.data } satisfies EngineEvent));
  engine.on('disconnected', (error: Error) => window?.webContents.send('engine:disconnected', error.message));
  try { await request('hello', { notifications: true }); }
  catch (error) {
    engine.removeAllListeners('disconnected'); engine.close();
    if (child.exitCode === null) child.kill();
    child.stdout.destroy(); child.stderr.destroy(); child.stdin.destroy();
    engine = null; child = null;
    throw error;
  }
}

async function reconnectEngine(): Promise<void> {
  if (reconnecting || capture || closeRequested) throw new Error('Engine cannot reconnect while recording or closing');
  reconnecting = true;
  try {
    engine?.removeAllListeners('disconnected');
    engine?.close();
    if (child && child.exitCode === null) child.kill();
    child?.stdout.destroy(); child?.stderr.destroy(); child?.stdin.destroy();
    engine = null; child = null;
    media.clear();
    mediaTokens.clear();
    await startEngine();
  } finally { reconnecting = false; }
}

async function createWindow() {
  const preferences = engine ? await request<{ width: number; height: number; maximized: boolean }>('ui_preferences', {}).catch(() => null) : null;
  const work = screen.getPrimaryDisplay().workArea;
  const width = Math.min(Math.max(900, preferences?.width || 1280), work.width);
  const height = Math.min(Math.max(620, preferences?.height || 800), work.height);
  window = new BrowserWindow({
    title: 'Phraseback', width, height, minWidth: 900, minHeight: 620, show: false,
    backgroundColor: '#181818', autoHideMenuBar: true,
    webPreferences: { preload: path.join(__dirname, '../preload/preload.js'), contextIsolation: true, sandbox: true, nodeIntegration: false, webSecurity: true }
  });
  window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  window.webContents.on('will-navigate', event => event.preventDefault());
  await window.loadFile(path.join(__dirname, '../renderer/index.html'));
  window.show();
  if (preferences && !preferences.maximized) {
    const bounds = window.getBounds();
    window.setBounds({ x: bounds.x, y: bounds.y, width, height });
  }
  if (preferences?.maximized) window.maximize();
  window.on('close', event => {
    if (allowClose || !window || closeRequested) { if (!allowClose) event.preventDefault(); return; }
    event.preventDefault(); closeRequested = true;
    void (async () => {
      try {
        if (capture) {
          await stopCapture();
          for (let i = 0; capture && i < 50; i++) await new Promise(resolve => setTimeout(resolve, 100));
          if (capture) throw new Error('Recording is still stopping');
        }
        const saved = await new Promise<boolean>(resolve => {
          finishClose = resolve;
          window?.webContents.send('app:before-close');
          closeDeadline = setTimeout(() => { if (finishClose === resolve) { finishClose = null; closeDeadline = null; resolve(false); } }, 8000);
          closeDeadline.unref();
        });
        if (!saved) return;
        if (engine) {
          const bounds = window!.getNormalBounds();
          const savedWidth = preferences && Math.abs(bounds.width - width) <= 2 ? width : bounds.width;
          const savedHeight = preferences && Math.abs(bounds.height - height) <= 2 ? height : bounds.height;
          await request('save_ui_preferences', { width: savedWidth, height: savedHeight, maximized: window!.isMaximized(), theme: 'dark' });
          await request('shutdown', {}, true).catch(() => undefined);
        }
        engine?.close();
        if (child && child.exitCode === null) child.kill();
        child?.stdout.destroy(); child?.stderr.destroy(); child?.stdin.destroy(); child?.unref();
        allowClose = true; window?.close();
      } catch (error) { dialog.showErrorBox('Could not close Phraseback', error instanceof Error ? error.message : String(error)); }
      finally { closeRequested = false; }
    })();
  });
}

async function stopCapture(): Promise<void> {
  if (!capture) return;
  capture.stop = true;
  if (capture.id) await request('cancel_operation', { id: capture.id });
}

function captureWindow(bounds: Electron.Rectangle, html: string, clickThrough: boolean): BrowserWindow {
  const overlay = new BrowserWindow({
    x: bounds.x, y: bounds.y, width: bounds.width, height: bounds.height,
    frame: false, transparent: true, alwaysOnTop: true, skipTaskbar: true,
    show: false, resizable: false, focusable: !clickThrough,
    backgroundColor: '#00000000', webPreferences: { contextIsolation: true, sandbox: true, nodeIntegration: false }
  });
  overlay.setContentProtection(true);
  if (clickThrough) overlay.setIgnoreMouseEvents(true);
  void overlay.loadURL(`data:text/html,${encodeURIComponent(html)}`).then(() => overlay.showInactive());
  return overlay;
}

function captureOutline(bounds: Electron.Rectangle): BrowserWindow[] {
  const edge = Math.min(3, bounds.width, bounds.height);
  // Keep Chromium surfaces to the four thin edges; a display-sized transparent
  // overlay competes with Windows Graphics Capture on the reference display.
  const edges = [
    { x: bounds.x, y: bounds.y, width: bounds.width, height: edge },
    { x: bounds.x, y: bounds.y + bounds.height - edge, width: bounds.width, height: edge },
    { x: bounds.x, y: bounds.y + edge, width: edge, height: Math.max(1, bounds.height - 2 * edge) },
    { x: bounds.x + bounds.width - edge, y: bounds.y + edge, width: edge, height: Math.max(1, bounds.height - 2 * edge) }
  ];
  return edges.map(bounds => captureWindow(bounds, '<html><body style="margin:0;background:#a48bdf;width:100vw;height:100vh"></body></html>', true));
}

function captureBounds(source: CaptureScreen, area: CaptureArea): Electron.Rectangle {
  const display = screen.getAllDisplays().find(d => Math.abs(d.bounds.width * d.scaleFactor - source.width) <= 2 && Math.abs(d.bounds.height * d.scaleFactor - source.height) <= 2)
    || screen.getPrimaryDisplay();
  const factor = display.scaleFactor;
  return { x: Math.round(display.bounds.x + (area.left - source.left) / factor), y: Math.round(display.bounds.y + (area.top - source.top) / factor), width: Math.max(1, Math.round(area.width / factor)), height: Math.max(1, Math.round(area.height / factor)) };
}

async function chooseRegion(source: CaptureScreen): Promise<CaptureArea | null> {
  const display = screen.getAllDisplays().find(d => Math.abs(d.bounds.width * d.scaleFactor - source.width) <= 2 && Math.abs(d.bounds.height * d.scaleFactor - source.height) <= 2)
    || screen.getPrimaryDisplay();
  const overlay = new BrowserWindow({ ...display.bounds, frame: false, transparent: true, alwaysOnTop: true, skipTaskbar: true, resizable: false, backgroundColor: '#00000000', webPreferences: { contextIsolation: true, sandbox: true, nodeIntegration: false } });
  const html = `<!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1"></head><body style="margin:0;background:#0009;cursor:crosshair;overflow:hidden;font:14px Segoe UI;color:#fff"><div style="position:absolute;top:18px;left:18px;padding:9px 12px;border-radius:8px;background:#25252a">Drag a rectangle to record · Esc to cancel</div><div id="box" style="position:absolute;display:none;border:2px solid #b498f0;background:#b498f033;pointer-events:none"></div><script>let start=null;const box=document.getElementById('box');addEventListener('pointerdown',e=>{start={x:e.clientX,y:e.clientY};box.style.display='block'});addEventListener('pointermove',e=>{if(!start)return;let x=Math.min(start.x,e.clientX),y=Math.min(start.y,e.clientY),w=Math.abs(e.clientX-start.x),h=Math.abs(e.clientY-start.y);Object.assign(box.style,{left:x+'px',top:y+'px',width:w+'px',height:h+'px'})});addEventListener('pointerup',e=>{if(!start)return;let x=Math.min(start.x,e.clientX),y=Math.min(start.y,e.clientY),w=Math.abs(e.clientX-start.x),h=Math.abs(e.clientY-start.y);location.href='phraseback-region://select/?x='+x+'&y='+y+'&w='+w+'&h='+h});addEventListener('keydown',e=>{if(e.key==='Escape')location.href='phraseback-region://cancel/'})</script></body></html>`;
  return new Promise(async resolve => {
    let finished = false;
    const complete = (area: CaptureArea | null) => { if (finished) return; finished = true; resolve(area); if (!overlay.isDestroyed()) overlay.close(); };
    overlay.webContents.on('will-navigate', (event, url) => {
      event.preventDefault();
      const parsed = new URL(url);
      if (parsed.hostname !== 'select') return complete(null);
      const drag = { x: Number(parsed.searchParams.get('x')), y: Number(parsed.searchParams.get('y')), width: Number(parsed.searchParams.get('w')), height: Number(parsed.searchParams.get('h')) };
      if (Object.values(drag).some(value => !Number.isFinite(value))) return complete(null);
      const area = regionToPixels(source, display.scaleFactor, drag);
      complete(area.width > 0 && area.height > 0 ? area : null);
    });
    overlay.on('closed', () => complete(null));
    try { await overlay.loadURL(`data:text/html,${encodeURIComponent(html)}`); overlay.focus(); }
    catch { complete(null); }
  });
}

async function startCapture(options: { screen: CaptureScreen; area: CaptureArea; title: string; context: string }): Promise<string | null> {
  if (!window || capture) throw new Error('Capture already active');
  validateRequest('prepare_capture', options);
  const prepared = await request<{ token: string }>('prepare_capture', options, true);
  if (!globalShortcut.register('CommandOrControl+Shift+F9', () => { void stopCapture(); })) throw new Error('Ctrl+Shift+F9 is unavailable. Close the conflicting app before recording.');
  const bounds = captureBounds(options.screen, options.area);
  const outline = captureOutline(bounds);
  const workArea = screen.getPrimaryDisplay().workArea;
  const controls = captureWindow({ x: workArea.x + workArea.width - 260, y: workArea.y + 20, width: 240, height: 58 }, '<html><body style="margin:0;padding:9px;background:#29292c;color:white;font:13px Segoe UI;border-radius:8px"><span>Recording · Ctrl+Shift+F9</span><a href="phraseback-stop://now" style="float:right;padding:7px;color:white;background:#875fdb;border-radius:6px;text-decoration:none">Stop</a></body></html>', false);
  controls.webContents.on('will-navigate', event => { event.preventDefault(); void stopCapture(); });
  window.setContentProtection(true);
  capture = { id: null, stop: false, controls, outline };
  type CaptureStatus = { id: string; finished: boolean; state: string; message: string; error: string | null; recording_id: string | null };
  const captureEngine = engine!;
  let latestStatus: CaptureStatus | null = null;
  let statusWake: (() => void) | null = null;
  let statusError: Error | null = null;
  const onNotification = (message: { event: string; data: unknown }) => {
    if (message.event !== 'operation_status') return;
    latestStatus = message.data as CaptureStatus;
    statusWake?.(); statusWake = null;
  };
  const onDisconnect = (error: Error) => { statusError = error; statusWake?.(); statusWake = null; };
  captureEngine.on('notification', onNotification);
  captureEngine.on('disconnected', onDisconnect);
  try {
    for (let seconds = 3; seconds > 0; seconds--) {
      window.webContents.send('engine:event', { event: 'operation_status', data: { message: `Recording in ${seconds}…` } });
      await new Promise(resolve => setTimeout(resolve, 1000));
      if (capture.stop) return null;
    }
    let operation = await request<CaptureStatus>('start_capture', { token: prepared.token, exclusion_ready: true, shortcut_ready: true }, true);
    capture.id = operation.id;
    if (capture.stop) await request('cancel_operation', { id: operation.id });
    while (!operation.finished) {
      window.webContents.send('engine:event', { event: 'operation_status', data: operation });
      if (!latestStatus && !statusError) await new Promise<void>(resolve => { statusWake = resolve; });
      if (statusError) throw statusError;
      const next = latestStatus!;
      latestStatus = null;
      if (next.id !== operation.id) throw new Error('Capture operation identity changed');
      operation = next;
    }
    if (operation.state !== 'completed' && operation.state !== 'cancelled') throw new Error(operation.error || operation.state);
    window.webContents.send('capture:finished', operation.recording_id);
    return operation.recording_id;
  } finally {
    captureEngine.off('notification', onNotification);
    captureEngine.off('disconnected', onDisconnect);
    capture = null;
    controls.close(); for (const edge of outline) edge.close(); window.setContentProtection(false);
    globalShortcut.unregister('CommandOrControl+Shift+F9');
  }
}

app.whenReady().then(async () => {
  protocol.handle('phraseback-media', async request => {
    const token = new URL(request.url).pathname.slice(1);
    const file = media.get(token);
    if (!file || !existsSync(file) || statSync(file).size > 32 * 1024 * 1024) return new Response(null, { status: 404 });
    return net.fetch(pathToFileURL(file).toString());
  });
  ipcMain.handle('engine:request', async (event, method: unknown, params: unknown) => {
    if (event.sender !== window?.webContents || typeof method !== 'string' || typeof params !== 'object' || params === null || Array.isArray(params)) throw new Error('Invalid request origin');
    return request(method, params as Record<string, unknown>);
  });
  ipcMain.handle('engine:reconnect', async event => {
    if (event.sender !== window?.webContents) throw new Error('Invalid request origin');
    await reconnectEngine();
  });
  ipcMain.handle('export:choose-folder', async event => {
    if (event.sender !== window?.webContents || !window) throw new Error('Invalid request origin');
    const choice = await dialog.showOpenDialog(window, { properties: ['openDirectory'] });
    return choice.canceled ? null : choice.filePaths[0];
  });
  ipcMain.handle('clipboard:write', (event, value: unknown) => {
    if (event.sender !== window?.webContents || typeof value !== 'string' || value.length > 2_000_000) throw new Error('Invalid clipboard request');
    clipboard.writeText(value);
  });
  ipcMain.on('app:close-result', (event, saved: unknown) => {
    if (event.sender !== window?.webContents || !finishClose) return;
    const resolve = finishClose; finishClose = null;
    if (closeDeadline) { clearTimeout(closeDeadline); closeDeadline = null; }
    resolve(saved === true);
  });
  ipcMain.handle('capture:start', async (event, options: unknown) => {
    if (event.sender !== window?.webContents || !options || typeof options !== 'object') throw new Error('Invalid capture request');
    return startCapture(options as { screen: CaptureScreen; area: CaptureArea; title: string; context: string });
  });
  ipcMain.handle('capture:choose-region', async (event, source: unknown) => {
    if (event.sender !== window?.webContents || !source || typeof source !== 'object') throw new Error('Invalid capture request');
    return chooseRegion(source as CaptureScreen);
  });
  ipcMain.handle('capture:stop', async event => {
    if (event.sender !== window?.webContents) throw new Error('Invalid capture request');
    return stopCapture();
  });
  let startupError: string | null = null;
  try { await startEngine(); }
  catch (error) { startupError = error instanceof Error ? error.message : String(error); }
  await createWindow();
  if (startupError) window?.webContents.send('engine:disconnected', startupError);
}).catch(error => dialog.showErrorBox('Phraseback startup failed', error instanceof Error ? error.message : String(error)));

app.on('window-all-closed', () => app.quit());
app.on('before-quit', event => {
  if (!allowClose && window && !window.isDestroyed()) { event.preventDefault(); setImmediate(() => window?.close()); return; }
  engine?.close();
  if (child && !child.killed) child.kill();
});
