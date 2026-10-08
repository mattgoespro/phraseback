import { contextBridge, ipcRenderer } from 'electron';
import type { EngineEvent, PhrasebackApi } from '../shared/api';

let closeHandler: () => Promise<boolean> = async () => true;
ipcRenderer.on('app:before-close', async () => {
  let saved = false;
  try { saved = await closeHandler(); } catch { /* Keep the window open when a draft cannot be saved. */ }
  ipcRenderer.send('app:close-result', saved);
});

const api: PhrasebackApi = {
  request: (method, params) => ipcRenderer.invoke('engine:request', method, params),
  reconnect: () => ipcRenderer.invoke('engine:reconnect'),
  startCapture: options => ipcRenderer.invoke('capture:start', options),
  chooseRegion: source => ipcRenderer.invoke('capture:choose-region', source),
  stopCapture: () => ipcRenderer.invoke('capture:stop'),
  onCaptureFinished: listener => {
    const handler = (_event: Electron.IpcRendererEvent, recordingId: string | null) => listener(recordingId);
    ipcRenderer.on('capture:finished', handler);
    return () => ipcRenderer.removeListener('capture:finished', handler);
  },
  chooseExportFolder: () => ipcRenderer.invoke('export:choose-folder'),
  copyText: value => ipcRenderer.invoke('clipboard:write', value),
  onEvent: listener => {
    const handler = (_event: Electron.IpcRendererEvent, message: EngineEvent) => listener(message);
    ipcRenderer.on('engine:event', handler);
    return () => ipcRenderer.removeListener('engine:event', handler);
  },
  onDisconnect: listener => {
    const handler = (_event: Electron.IpcRendererEvent, reason: string) => listener(reason);
    ipcRenderer.on('engine:disconnected', handler);
    return () => ipcRenderer.removeListener('engine:disconnected', handler);
  },
  onCloseRequested: listener => {
    closeHandler = listener;
    return () => { if (closeHandler === listener) closeHandler = async () => true; };
  }
};

contextBridge.exposeInMainWorld('phraseback', api);
