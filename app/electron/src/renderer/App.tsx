import { useCallback, useEffect, useRef, useState } from 'react';
import { ChevronDown, ChevronLeft, ChevronRight, CircleCheck, Ellipsis, FolderOpen, Menu, Pause, Play, Settings2, SkipBack, SkipForward, Video, X } from 'lucide-react';
import { loadLibrary, loadRecording, type LibraryEntry, type Operation, type Snapshot, type Step } from './workspace';
import { frameAtTime } from './playback';
import type { ModelStatus } from '../shared/contracts.generated';
import { draftRecovery } from './recovery-draft';

type View = 'review' | 'library' | 'capture' | 'recording' | 'settings' | 'prompt';
type Draft = Pick<Step, 'title' | 'action' | 'result' | 'uncertainty'>;
const request = window.phraseback.request;
const formatTime = (milliseconds: number) => `${String(Math.floor(milliseconds / 60000)).padStart(2, '0')}:${String(Math.floor(milliseconds / 1000) % 60).padStart(2, '0')}.${String(milliseconds % 1000).padStart(3, '0')}`;
const message = (error: unknown) => error instanceof Error ? error.message : String(error);

export function App() {
  const [view, setView] = useState<View>('review');
  const [library, setLibrary] = useState<LibraryEntry[]>([]);
  const [catalog, setCatalog] = useState<{ id: string; next: number | null }>({ id: '', next: null });
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [frameIndex, setFrameIndex] = useState(0);
  const [preview, setPreview] = useState<string | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [dirty, setDirty] = useState(false);
  const [details, setDetails] = useState(false);
  const [menu, setMenu] = useState<'nav' | 'recordings' | 'more' | null>(null);
  const [busy, setBusy] = useState(false);
  const [activity, setActivity] = useState('Connecting to the local engine…');
  const [savedDescriptions, setSavedDescriptions] = useState(0);
  const [error, setError] = useState('');
  const [playing, setPlaying] = useState(false);
  const [screens, setScreens] = useState<{ id: string; name: string; left: number; top: number; width: number; height: number }[]>([]);
  const [screenId, setScreenId] = useState('');
  const [captureTitle, setCaptureTitle] = useState('Desktop workflow');
  const [captureContext, setCaptureContext] = useState('');
  const [regionMode, setRegionMode] = useState(false);
  const [region, setRegion] = useState({ left: 0, top: 0, width: 800, height: 600 });
  const [model, setModel] = useState<ModelStatus | null>(null);
  const [activeOperationId, setActiveOperationId] = useState<string | null>(null);
  const [regenerateDialog, setRegenerateDialog] = useState(false);
  const [replaceManual, setReplaceManual] = useState(false);
  const [prompt, setPrompt] = useState('');
  const [promptTemplate, setPromptTemplate] = useState('');
  const [defaultTemplate, setDefaultTemplate] = useState('');
  const [detailsDialog, setDetailsDialog] = useState(false);
  const [recordingTitle, setRecordingTitle] = useState('');
  const [recordingTask, setRecordingTask] = useState('');
  const [recordingContext, setRecordingContext] = useState('');
  const [originalUrl, setOriginalUrl] = useState<string | null>(null);
  const [contextMenu, setContextMenu] = useState<{ x: number; y: number; stepId: string | null } | null>(null);
  const [alternativeFrames, setAlternativeFrames] = useState<number[] | null>(null);
  const [alternativeStepId, setAlternativeStepId] = useState<string | null>(null);
  const [organizationId, setOrganizationId] = useState<string | null>(null);
  const [openingId, setOpeningId] = useState<string | null>(null);
  const [recoveryChoice, setRecoveryChoice] = useState<'saved' | 'missing' | null>(null);
  const [disconnected, setDisconnected] = useState(false);
  const recoveryBlocked = useRef(false);
  const openVersion = useRef(0);
  const seekVersion = useRef(0);
  const latest = useRef<Snapshot | null>(null);
  const draftRef = useRef<Draft | null>(null);
  const draftVersion = useRef(0);
  const savedVersion = useRef(0);
  const saveInFlight = useRef<Promise<boolean> | null>(null);
  const closeSave = useRef<() => Promise<boolean>>(async () => true);
  const activeOperationKind = useRef<string | null>(null);
  const selectedStep = snapshot?.project.steps.find(step => step.id === selectedId) ?? null;

  const refreshLibrary = useCallback(async () => {
    const page = await loadLibrary(request);
    setLibrary(page.items);
    setCatalog({ id: page.catalog_id, next: page.next_offset });
    return page.items;
  }, []);

  const openRecording = useCallback(async (id: string, preference?: { stepId?: string; frame?: number }) => {
    const version = ++openVersion.current;
    setBusy(true); setError(''); setActivity('Opening recording…');
    try {
      const loaded = await loadRecording(request, id, operation => { if (version === openVersion.current) setOpeningId(operation?.finished ? null : operation?.id ?? null); });
      if (version !== openVersion.current) return;
      latest.current = loaded; setSnapshot(loaded); setPreview(null);
      const first = loaded.project.steps.find(step => step.id === preference?.stepId)
        ?? loaded.project.steps.find(step => step.frame === preference?.frame)
        ?? loaded.project.steps[0] ?? null;
      setSelectedId(first?.id ?? null); draftRef.current = first ? { title: first.title, action: first.action, result: first.result, uncertainty: first.uncertainty } : null; setDraft(draftRef.current);
      draftVersion.current = 0; savedVersion.current = 0;
      setDirty(false); setFrameIndex(first?.frame ?? 0); setView('review'); setMenu(null); setActivity('Ready');
    } catch (caught) { if (version === openVersion.current) { setError(message(caught)); setActivity('Recording unavailable'); } }
    finally { if (version === openVersion.current) { setOpeningId(null); setBusy(false); } }
  }, []);

  useEffect(() => {
    let active = true;
    const startup = async () => {
      try {
        const items = await refreshLibrary();
        if (active && items[0]) await openRecording(items[0].id);
        else if (active) setActivity('Start a recording to begin');
      } catch (caught) { if (active) setError(message(caught)); }
    };
    void startup();
    const offEvent = window.phraseback.onEvent(event => {
      if (event.event === 'operation_status' || event.event === 'organization_status') {
        const operation = event.data as Operation;
        setActivity(operation.message);
      }
      if (event.event === 'operation_saved' && activeOperationKind.current === 'generate') setSavedDescriptions(count => count + 1);
    });
    const offDisconnect = window.phraseback.onDisconnect(reason => { setDisconnected(true); setError(`Engine disconnected: ${reason}`); setActivity('Connection lost; unsaved text stays visible.'); });
    return () => { active = false; offEvent(); offDisconnect(); };
  }, [openRecording, refreshLibrary]);

  useEffect(() => {
    if (!snapshot?.project.frames.length) { setPreview(null); return; }
    const version = ++seekVersion.current;
    setPreview(null);
    void request<{ url: string; recording_id: string; index: number; revision: number }>('frame', { recording_id: snapshot.recording_id, index: frameIndex, preview_width: 1440 })
      .then(reference => {
        if (version === seekVersion.current && reference.recording_id === snapshot.recording_id && reference.index === frameIndex && reference.revision === snapshot.revision) setPreview(reference.url);
      }).catch(caught => { if (version === seekVersion.current) setError(`Preview unavailable: ${message(caught)}`); });
  }, [snapshot, frameIndex]);

  useEffect(() => {
    if (!playing || !snapshot?.project.frames.length) return;
    const started = performance.now();
    const offset = snapshot.project.frames[frameIndex]?.time_ms ?? 0;
    const timer = setInterval(() => {
      const position = offset + performance.now() - started;
      setFrameIndex(frameAtTime(snapshot.project.frames, position));
      if (position >= snapshot.project.duration_ms) setPlaying(false);
    }, 30);
    return () => clearInterval(timer);
  }, [playing, snapshot]);

  async function saveDraft(): Promise<boolean> {
    if (recoveryBlocked.current) { setError('Resolve the recording conflict before saving this draft.'); return false; }
    if (saveInFlight.current) return saveInFlight.current;
    const task = flushDraft();
    saveInFlight.current = task;
    try { return await task; }
    finally { if (saveInFlight.current === task) saveInFlight.current = null; }
  }
  async function reconnect() {
    setBusy(true);
    try {
      await window.phraseback.reconnect();
      setDisconnected(false);
      const current = latest.current;
      if (current) {
        let restored: Snapshot;
        try { restored = await loadRecording(request, current.recording_id); }
        catch (caught) {
          if (!dirty) throw caught;
          recoveryBlocked.current = true; setRecoveryChoice('missing');
          setError('The recording is unavailable. Your draft remains visible; copy it before discarding the draft.');
          return;
        }
        if (dirty) {
          const recovery = selectedId && draftRef.current ? draftRecovery(current, restored, selectedId, draftRef.current) : 'conflict';
          if (recovery === 'conflict') {
            recoveryBlocked.current = true; setRecoveryChoice('saved');
            setError('Saved evidence or description changed while disconnected. Your draft remains visible; copy it before reopening the recording.');
            return;
          }
          recoveryBlocked.current = false; setRecoveryChoice(null);
          latest.current = restored; setSnapshot(restored);
          if (recovery === 'saved') { savedVersion.current = draftVersion.current; setDirty(false); setActivity('Draft was saved before the connection ended'); }
          else if (!await saveDraft()) return;
        } else await openRecording(current.recording_id, { stepId: selectedId || undefined, frame: frameIndex });
      }
      await refreshLibrary();
      if (!current) setView('library');
      setError(''); setActivity('Reconnected');
    } catch (caught) { setError(`Reconnect failed: ${message(caught)}`); }
    finally { setBusy(false); }
  }
  async function discardRecoveryDraft() {
    const choice = recoveryChoice;
    const id = latest.current?.recording_id;
    recoveryBlocked.current = false; setRecoveryChoice(null);
    draftVersion.current = savedVersion.current = 0; draftRef.current = null;
    setDraft(null); setDirty(false); setError('');
    if (choice === 'saved' && id) await openRecording(id);
    else {
      latest.current = null; setSnapshot(null); setSelectedId(null); setPreview(null);
      await refreshLibrary(); setView('library'); setActivity('Choose a recording');
    }
  }
  closeSave.current = saveDraft;
  useEffect(() => window.phraseback.onCloseRequested(() => closeSave.current()), []);

  async function flushDraft(): Promise<boolean> {
    if (recoveryBlocked.current) return false;
    if (draftVersion.current === savedVersion.current) return true;
    if (!latest.current || !selectedId || !draftRef.current) return false;
    try {
      setActivity('Saving description…');
      while (draftVersion.current !== savedVersion.current) {
        const current: Snapshot = latest.current!;
        const fields = { ...draftRef.current! };
        const version = draftVersion.current;
        const result = await request<Snapshot>('edit_step', { recording_id: current.recording_id, revision: current.revision, step_id: selectedId, ...fields, paged: true });
        const updated: Snapshot = { ...current, revision: result.revision, project: { ...current.project, steps: current.project.steps.map((step: Step) => step.id === selectedId ? { ...step, ...fields, manual: true } : step) } };
        latest.current = updated; setSnapshot(updated); savedVersion.current = version;
      }
      setDirty(false); setActivity('Saved locally'); setError('');
      return true;
    } catch (caught) { setError(`Draft not saved: ${message(caught)}`); setActivity('Draft retained'); return false; }
  }

  function changeDraft(key: keyof Draft, value: string) {
    if (!draftRef.current) return;
    draftRef.current = { ...draftRef.current, [key]: value };
    setDraft(draftRef.current); ++draftVersion.current; setDirty(true);
  }

  useEffect(() => {
    if (!dirty || !selectedId) return;
    const timer = setTimeout(() => { void saveDraft(); }, 500);
    return () => clearTimeout(timer);
  }, [draft, dirty, selectedId]);

  async function selectRecording(id: string) { if (await saveDraft()) { setPlaying(false); await openRecording(id); } }
  async function selectStep(step: Step) {
    if (!await saveDraft()) return;
    setSelectedId(step.id); draftRef.current = { title: step.title, action: step.action, result: step.result, uncertainty: step.uncertainty }; setDraft(draftRef.current); draftVersion.current = savedVersion.current = 0; setDirty(false); setFrameIndex(step.frame); setPlaying(false);
  }
  async function review() {
    if (!snapshot || !selectedId || !await saveDraft()) return;
    try {
      setBusy(true);
      const result = await request<Snapshot>('review_step', { recording_id: latest.current!.recording_id, revision: latest.current!.revision, step_id: selectedId, reviewed: true, paged: true });
      const updated = { ...latest.current!, revision: result.revision, project: { ...latest.current!.project, steps: latest.current!.project.steps.map(step => step.id === selectedId ? { ...step, reviewed: true } : step) } };
      latest.current = updated; setSnapshot(updated); setActivity('Moment reviewed');
    } catch (caught) { setError(message(caught)); } finally { setBusy(false); }
  }
  async function runOperation(method: string, params: Record<string, unknown>) {
    if (!await saveDraft()) return;
    setBusy(true); setError(''); setMenu(null);
    activeOperationKind.current = method;
    if (method === 'generate') setSavedDescriptions(0);
    try {
      let operation = await request<Operation>(method, params);
      setActiveOperationId(operation.id);
      while (!operation.finished) {
        setActivity(operation.message);
        await new Promise(resolve => setTimeout(resolve, 100));
        const next = await request<Operation>('operation_status', {});
        if (next.id !== operation.id) throw new Error('Operation identity changed');
        operation = next;
      }
      if (operation.state !== 'completed') {
        if (operation.recording_id) await openRecording(operation.recording_id, { stepId: selectedId || undefined });
        await refreshLibrary();
        if (operation.state === 'cancelled') { setActivity('Operation cancelled · saved progress retained'); return; }
        throw new Error(operation.error || operation.state);
      }
      if (operation.recording_id) await openRecording(operation.recording_id);
      if (method.startsWith('model_')) setModel(await request<typeof model>('model_status', {}));
      await refreshLibrary(); setActivity(operation.message || 'Operation complete');
    } catch (caught) { setError(message(caught)); }
    finally { activeOperationKind.current = null; setActiveOperationId(null); setBusy(false); }
  }
  async function cancelActiveOperation() {
    if (!activeOperationId) return;
    try { await request('cancel_operation', { id: activeOperationId }); setActivity('Stopping operation…'); }
    catch (caught) { setError(message(caught)); }
  }
  async function cancelOpening() {
    if (!openingId) return;
    try { await request('cancel_operation', { id: openingId }); setActivity('Cancelling recovery…'); }
    catch (caught) { setError(message(caught)); }
  }
  async function releaseModel() {
    try { setModel(await request<ModelStatus>('model_release', {})); setActivity('Model memory released'); }
    catch (caught) { setError(message(caught)); }
  }
  async function regenerate(single: boolean) {
    if (!snapshot) return;
    setRegenerateDialog(false);
    await runOperation('generate', { recording_id: snapshot.recording_id, revision: snapshot.revision, step_id: single ? selectedId : null, force: true, replace_manual: replaceManual });
  }
  async function organizeRecording() {
    if (!await saveDraft() || !latest.current || organizationId) return;
    const current = latest.current;
    setError(''); setActivity('Organizing screenshots…');
    let id: string | null = null;
    try {
      let operation = await request<Operation>('organize_background', { recording_id: current.recording_id, revision: current.revision });
      id = operation.id; setOrganizationId(id);
      while (!operation.finished) {
        setActivity(operation.message || 'Organizing screenshots…');
        await new Promise(resolve => setTimeout(resolve, 100));
        const next = await request<Operation | null>('organization_status', {});
        if (!next || next.id !== id) throw new Error('Organization identity changed');
        operation = next;
      }
      if (operation.state === 'cancelled') { setActivity('Organization cancelled'); return; }
      if (operation.state !== 'completed') throw new Error(operation.error || operation.state);
      if (!await saveDraft()) return;
      if (latest.current?.recording_id !== current.recording_id || latest.current.revision !== current.revision) {
        setActivity('Recording changed; organization suggestions were discarded'); return;
      }
      await request<Snapshot>('apply_organization', { id, recording_id: current.recording_id, revision: current.revision, paged: true });
      await openRecording(current.recording_id, { stepId: selectedId || undefined });
      setActivity('Screenshots organized · ready for review');
    } catch (caught) { setError(`Organizing unavailable: ${message(caught)}`); }
    finally { setOrganizationId(null); }
  }
  async function cancelOrganization() {
    if (!organizationId) return;
    try { await request('cancel_organization', { id: organizationId }); setActivity('Stopping organization…'); }
    catch (caught) { setError(message(caught)); }
  }
  async function exportRecording() {
    if (!snapshot) return;
    const parent = await window.phraseback.chooseExportFolder();
    if (parent) await runOperation('export', { recording_id: snapshot.recording_id, revision: snapshot.revision, parent });
  }
  async function openCapture() {
    if (!await saveDraft()) return;
    try { const choices = await request<typeof screens>('screens', {}); setScreens(choices); setScreenId(choices[0]?.id || ''); setView('capture'); setMenu(null); }
    catch (caught) { setError(message(caught)); }
  }
  async function beginCapture() {
    const selected = screens.find(screen => screen.id === screenId);
    if (!selected) return;
    const area = regionMode ? region : { left: selected.left, top: selected.top, width: selected.width, height: selected.height };
    if (area.width <= 0 || area.height <= 0 || area.left < selected.left || area.top < selected.top || area.left + area.width > selected.left + selected.width || area.top + area.height > selected.top + selected.height) {
      setError('Choose a rectangle inside the selected display.'); return;
    }
    setView('recording'); setBusy(true); setError('');
    try {
      const id = await window.phraseback.startCapture({ screen: selected, area, title: captureTitle, context: captureContext });
      await refreshLibrary();
      if (id) await openRecording(id);
      else setView('review');
    } catch (caught) { setError(message(caught)); setView('capture'); }
    finally { setBusy(false); }
  }
  async function openSettings() {
    try { setModel(await request<typeof model>('model_status', {})); const template = await request<{ template: string; default_template: string }>('prompt_template', {}); setPromptTemplate(template.template); setDefaultTemplate(template.default_template); setView('settings'); setMenu(null); }
    catch (caught) { setError(message(caught)); }
  }
  async function openPrompt() {
    if (!snapshot || !await saveDraft()) return;
    try {
      const current = latest.current!;
      const result = await request<{ recording_id: string; revision: number; markdown: string }>('render_prompt', { recording_id: current.recording_id, revision: current.revision });
      if (result.recording_id === current.recording_id && result.revision === current.revision) { setPrompt(result.markdown); setView('prompt'); setMenu(null); }
    } catch (caught) { setError(message(caught)); }
  }
  async function saveRecordingDetails() {
    if (!snapshot || !await saveDraft()) return;
    try {
      const current = latest.current!;
      const result = await request<Snapshot>('edit_project', { recording_id: current.recording_id, revision: current.revision, title: recordingTitle, task: recordingTask, context: recordingContext, paged: true });
      const updated = { ...current, revision: result.revision, project: { ...current.project, title: recordingTitle, task: recordingTask, context: recordingContext } };
      latest.current = updated; setSnapshot(updated); setDetailsDialog(false); setActivity('Recording details saved'); await refreshLibrary();
    } catch (caught) { setError(message(caught)); }
  }
  async function showOriginal() {
    if (!snapshot) return;
    try { const reference = await request<{ url: string }>('frame', { recording_id: snapshot.recording_id, index: frameIndex }); setOriginalUrl(reference.url); setMenu(null); }
    catch (caught) { setError(message(caught)); }
  }
  async function curate(method: 'add_step' | 'replace_step' | 'remove_step', frame = frameIndex) {
    if (!snapshot || !await saveDraft()) return;
    const selected = contextMenu?.stepId ?? alternativeStepId ?? selectedId;
    if (method !== 'add_step' && !selected) return;
    setContextMenu(null); setAlternativeFrames(null); setAlternativeStepId(null); setBusy(true);
    try {
      const current = latest.current!;
      const params = method === 'add_step'
        ? { recording_id: current.recording_id, revision: current.revision, frame, paged: true }
        : method === 'replace_step'
          ? { recording_id: current.recording_id, revision: current.revision, step_id: selected!, frame, paged: true }
          : { recording_id: current.recording_id, revision: current.revision, step_id: selected!, paged: true };
      await request<Snapshot>(method, params);
      await openRecording(current.recording_id, method === 'remove_step' ? undefined : { stepId: method === 'add_step' ? undefined : selected!, frame });
      await refreshLibrary(); setActivity('Moment selection saved');
    } catch (caught) { setError(message(caught)); }
    finally { setBusy(false); }
  }
  async function showAlternatives() {
    const target = snapshot?.project.steps.find(step => step.id === contextMenu?.stepId) ?? selectedStep;
    if (!snapshot || !target || !await saveDraft()) return;
    try {
      const suggestions = await request<{ intervals: number[][] }>('alternatives', { recording_id: snapshot.recording_id });
      const interval = suggestions.intervals.find(([first, last]) => target.frame >= first && target.frame <= last);
      setAlternativeFrames(interval ? Array.from({ length: Math.min(128, interval[1] - interval[0] + 1) }, (_, index) => interval[0] + index) : []);
      setAlternativeStepId(target.id);
      setContextMenu(null);
    } catch (caught) { setError(message(caught)); }
  }

  return <div className="app-shell" onKeyDown={event => { if (event.key === 'Escape') { setMenu(null); setContextMenu(null); setAlternativeFrames(null); } }} onClick={() => { if (contextMenu) setContextMenu(null); }}>
    <header className="topbar">
      <div className="popover-anchor"><button className="icon-button menu-trigger" aria-label="Open navigation" aria-expanded={menu === 'nav'} onClick={() => setMenu(menu === 'nav' ? null : 'nav')}><Menu size={17} /></button>
        {menu === 'nav' && <div className="popover nav-popover"><button onClick={() => { setView('library'); setMenu(null); }}><FolderOpen size={15} /> Library</button><button onClick={() => void openCapture()}><Video size={15} /> New recording</button><button disabled={!snapshot} onClick={() => void openPrompt()}>Get prompt</button><button onClick={() => void openSettings()}><Settings2 size={15} /> Settings</button></div>}
      </div>
      <span className="brand-mark" aria-label="Phraseback">P</span>
      <button className="crumb" onClick={() => { setView('library'); setMenu(null); }}>Library</button><span className="slash">/</span>
      <div className="popover-anchor recording-anchor"><button className="crumb recording-crumb" aria-expanded={menu === 'recordings'} onClick={() => setMenu(menu === 'recordings' ? null : 'recordings')}><span>{snapshot?.project.title || 'Choose recording'}</span><ChevronDown size={14} strokeWidth={2} /></button>
        {menu === 'recordings' && <div className="popover recording-popover">{library.map(item => <button key={item.id} className={item.id === snapshot?.recording_id ? 'current' : ''} onClick={() => void selectRecording(item.id)}>{item.title}</button>)}{catalog.next !== null && <button onClick={async () => { const page = await loadLibrary(request, false, catalog.next!, catalog.id); setLibrary(list => [...list, ...page.items]); setCatalog({ id: page.catalog_id, next: page.next_offset }); }}>Load more</button>}</div>}
      </div><span className="slash">/</span><span className="current-crumb">{view === 'review' ? 'Review' : view === 'capture' || view === 'recording' ? 'Record' : view === 'settings' ? 'Settings' : view === 'prompt' ? 'Prompt' : 'Library'}</span>
      <div className="topbar-spacer" />
      <div className="popover-anchor"><button className="icon-button" aria-label="More actions" aria-expanded={menu === 'more'} onClick={() => setMenu(menu === 'more' ? null : 'more')}><Ellipsis size={18} /></button>
        {menu === 'more' && <div className="popover more-popover"><button onClick={() => { setDetails(!details); setMenu(null); }}> {details ? 'Hide' : 'Show'} description details</button><button disabled={!snapshot} onClick={() => { if (snapshot) { setRecordingTitle(snapshot.project.title); setRecordingTask(snapshot.project.task || ''); setRecordingContext(snapshot.project.context || ''); setDetailsDialog(true); setMenu(null); } }}>Recording details</button><button disabled={!snapshot} onClick={() => void showOriginal()}>View original pixels</button><button disabled={!snapshot || busy} onClick={() => { setMenu(null); void (organizationId ? cancelOrganization() : organizeRecording()); }}>{organizationId ? 'Cancel organizing' : 'Organize'}</button><button disabled={!snapshot || busy} onClick={() => void runOperation('generate', { recording_id: snapshot!.recording_id, revision: snapshot!.revision, step_id: null, force: false, replace_manual: false })}>Generate descriptions</button><button disabled={!snapshot || busy} onClick={() => { setReplaceManual(false); setRegenerateDialog(true); setMenu(null); }}>Regenerate descriptions…</button><button disabled={!snapshot || busy} onClick={() => void exportRecording()}>Export</button></div>}
      </div>
    </header>
    {error && <div className="error-banner" role="alert"><span>{error}</span>{recoveryChoice ? <button onClick={() => void discardRecoveryDraft()} disabled={busy}>{recoveryChoice === 'saved' ? 'Use saved version' : 'Discard draft and open Library'}</button> : disconnected || error.startsWith('Engine disconnected:') || error.startsWith('Reconnect failed:') ? <button onClick={() => void reconnect()} disabled={busy}>Reconnect</button> : null}<button aria-label="Dismiss error" onClick={() => setError('')}><X size={14} /></button></div>}
    {view === 'review' && <main className={`review ${details ? 'with-details' : ''}`}>
      <section className="review-main">
        <div className="review-heading"><div><h1>{snapshot?.project.title || 'Your recording studio'}</h1><p>{snapshot ? `${snapshot.project.steps.length} selected moments · ${formatTime(snapshot.project.duration_ms)}` : 'Choose a recording or start a new one'}</p></div></div>
        <div className="evidence-stage" onContextMenu={event => { event.preventDefault(); if (snapshot) setContextMenu({ x: event.clientX, y: event.clientY, stepId: selectedId }); }}>{preview ? <img src={preview} alt="Recorded screen evidence" /> : <div className="empty-stage">{snapshot ? 'Loading evidence…' : <><p>No recording open</p><button className="primary-button" onClick={() => void openCapture()}>New recording</button></>}</div>}</div>
        <div className="player"><button className="icon-button" aria-label="Previous frame" disabled={!snapshot} onClick={() => { setPlaying(false); setFrameIndex(Math.max(0, frameIndex - 1)); }}><SkipBack size={17} /></button><button className="play-button" disabled={!snapshot} onClick={() => setPlaying(!playing)}>{playing ? <Pause size={15} /> : <Play size={15} fill="currentColor" />}<span>{playing ? 'Pause' : 'Play'}</span></button><button className="icon-button" aria-label="Next frame" disabled={!snapshot} onClick={() => { setPlaying(false); setFrameIndex(Math.min((snapshot?.project.frames.length ?? 1) - 1, frameIndex + 1)); }}><SkipForward size={17} /></button><input aria-label="Recording timeline" type="range" min={0} max={Math.max(0, (snapshot?.project.frames.length ?? 1) - 1)} value={frameIndex} disabled={!snapshot} onChange={event => { setPlaying(false); setFrameIndex(Number(event.target.value)); }} /><time>{formatTime(snapshot?.project.frames[frameIndex]?.time_ms ?? 0)}</time></div>
        <div className="moments-strip" aria-label="Selected moments">{snapshot?.project.steps.map((step, index) => <button key={step.id} className={`moment ${step.id === selectedId ? 'active' : ''}`} onClick={() => void selectStep(step)} onContextMenu={event => { event.preventDefault(); setContextMenu({ x: event.clientX, y: event.clientY, stepId: step.id }); }} onKeyDown={event => { if ((event.shiftKey && event.key === 'F10') || event.key === 'ContextMenu') { event.preventDefault(); const box = event.currentTarget.getBoundingClientRect(); setContextMenu({ x: box.left + 16, y: box.top + 16, stepId: step.id }); } }} aria-pressed={step.id === selectedId}><time>{formatTime(snapshot.project.frames[step.frame]?.time_ms ?? 0)}</time><span>{step.title || `Moment ${index + 1}`}</span>{step.reviewed && <CircleCheck size={13} />}</button>)}</div>
      </section>
      {details && <aside className="details-pane"><div className="details-title"><h2>Moment details</h2><button className="icon-button" aria-label="Close details" onClick={() => setDetails(false)}><X size={16} /></button></div>{selectedStep && draft ? <div className="details-form"><label>Title<input value={draft.title} onChange={event => changeDraft('title', event.target.value)} /></label><label>Action<textarea value={draft.action} onChange={event => changeDraft('action', event.target.value)} /></label><label>Result<textarea value={draft.result} onChange={event => changeDraft('result', event.target.value)} /></label><label>Uncertainty<textarea value={draft.uncertainty} onChange={event => changeDraft('uncertainty', event.target.value)} /></label><div className="details-actions"><button className="secondary-button" disabled={!dirty || busy} onClick={() => void saveDraft()}>Save description</button><button className="secondary-button" disabled={selectedStep.reviewed || busy} onClick={() => void review()}>Mark reviewed</button></div></div> : <p className="muted">Choose a moment to view its details.</p>}</aside>}
    </main>}
    {view === 'library' && <main className="simple-view"><div className="simple-heading"><h1>Library</h1><button className="primary-button" onClick={() => void openCapture()}>New recording</button></div><div className="library-list">{library.map(item => <button key={item.id} onClick={() => void selectRecording(item.id)}><span className="library-icon"><Video size={19} /></span><span><strong>{item.title}</strong><small>{new Date(item.created).toLocaleDateString()} · {item.steps} moments · {formatTime(item.duration_ms)}</small></span><ChevronRight size={16} /></button>)}</div>{catalog.next !== null && <button className="secondary-button" onClick={async () => { const page = await loadLibrary(request, false, catalog.next!, catalog.id); setLibrary(list => [...list, ...page.items]); setCatalog({ id: page.catalog_id, next: page.next_offset }); }}>Load more recordings</button>}</main>}
    {view === 'capture' && <main className="simple-view narrow-view"><h1>New recording</h1><p className="muted">Choose a display. A three-second countdown lets you switch to the window you want to record.</p><label className="field">Display<select value={screenId} onChange={event => { setScreenId(event.target.value); setRegionMode(false); }} >{screens.map(screen => <option key={screen.id} value={screen.id}>{screen.name} · {screen.width} × {screen.height}</option>)}</select></label><div className="capture-mode"><button className={!regionMode ? 'selected' : ''} onClick={() => setRegionMode(false)}>Full display</button><button className={regionMode ? 'selected' : ''} onClick={async () => { const selected = screens.find(s => s.id === screenId); if (!selected) return; const area = await window.phraseback.chooseRegion(selected); if (area) { setRegion(area); setRegionMode(true); } }}>Choose rectangle…</button></div>{regionMode && <div className="region-fields">{(['left', 'top', 'width', 'height'] as const).map(key => <label key={key}>{key}<input type="number" value={region[key]} onChange={event => setRegion({ ...region, [key]: Number(event.target.value) })} /></label>)}</div>}<label className="field">Recording title<input value={captureTitle} onChange={event => setCaptureTitle(event.target.value)} /></label><label className="field">Context <small>Optional notes, not observed evidence</small><textarea value={captureContext} onChange={event => setCaptureContext(event.target.value)} /></label><div className="capture-actions"><button className="primary-button" disabled={busy || !screens.length} onClick={() => void beginCapture()}>Start recording</button><button className="secondary-button" onClick={() => setView('review')}>Back to review</button></div><p className="muted">Stop with Ctrl+Shift+F9 or the floating Stop button. Screenshots stay on this computer.</p></main>}
    {view === 'recording' && <main className="simple-view recording-view"><div><Video size={31} /><h1>Recording your screen</h1><p className="muted">{activity}</p><button className="primary-button" onClick={() => void window.phraseback.stopCapture()}>Stop recording</button></div></main>}
    {view === 'settings' && <main className="simple-view narrow-view"><h1>Settings</h1><section className="setting-group"><h2>Local AI model</h2><p className="muted">Models run on this computer. Install may download verified model files; GPU failure does not switch to CPU automatically.</p><label className="field">Preset<select value={model?.selected || ''} disabled={busy} onChange={async event => { try { setModel(await request<ModelStatus>('model_select', { preset: event.target.value })); } catch (caught) { setError(message(caught)); } }}>{model?.presets.map(preset => <option key={preset.id} value={preset.id}>{preset.title}</option>)}</select></label><p className="muted">{model?.state || 'Unavailable'} · {model?.verified ? 'Verified' : model?.assets_present ? 'Files found · verification required' : 'Not installed'}</p><p className="muted">{model?.presets.find(preset => preset.id === model.selected)?.assets.reduce((sum, asset) => sum + asset.size, 0) ? `${((model.presets.find(preset => preset.id === model.selected)?.assets.reduce((sum, asset) => sum + asset.size, 0) || 0) / 1073741824).toFixed(2)} GiB assets` : ''}</p><div className="settings-actions"><button className="secondary-button" disabled={busy} onClick={() => void runOperation('model_install', {})}>Install / repair</button><button className="secondary-button" disabled={busy || !model?.assets_present} onClick={() => void runOperation('model_verify', {})}>Verify existing files</button><button className="secondary-button" disabled={busy} onClick={() => void releaseModel()}>Release model memory</button><button className="secondary-button" disabled={busy || !model?.assets_present} onClick={() => { if (window.confirm('Remove this model’s downloaded files? Recordings and descriptions will remain.')) void runOperation('model_remove', { confirmed: true }); }}>Remove model files…</button></div></section><section className="setting-group"><h2>Prompt format</h2><p className="muted">Edit the reusable local template used by Get prompt.</p><label className="field">Template<textarea className="template-input" value={promptTemplate} onChange={event => setPromptTemplate(event.target.value)} /></label><div className="settings-actions"><button className="secondary-button" onClick={async () => { try { const saved = await request<{ template: string }>('save_prompt_template', { template: promptTemplate }); setPromptTemplate(saved.template); setActivity('Prompt format saved'); } catch (caught) { setError(message(caught)); } }}>Save format</button><button className="secondary-button" onClick={async () => { try { const saved = await request<{ template: string }>('reset_prompt_template', {}); setPromptTemplate(saved.template); setActivity('Prompt format reset'); } catch (caught) { setError(message(caught)); } }} disabled={promptTemplate === defaultTemplate}>Reset default</button></div></section></main>}
    {view === 'prompt' && <main className="simple-view prompt-view"><div className="simple-heading"><h1>Get prompt</h1><button className="primary-button" onClick={() => void window.phraseback.copyText(prompt).then(() => setActivity('Prompt copied'))}>Copy prompt</button></div><p className="muted">Review your moments, then paste this prompt into your agent.</p><pre>{prompt}</pre><button className="secondary-button" onClick={() => setView('review')}>Back to review</button></main>}
    {detailsDialog && <div className="dialog-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) setDetailsDialog(false); }}><section className="dialog" role="dialog" aria-modal="true" aria-label="Recording details"><div className="details-title"><h2>Recording details</h2><button className="icon-button" onClick={() => setDetailsDialog(false)} aria-label="Close recording details"><X size={16} /></button></div><label className="field">Recording title<input value={recordingTitle} onChange={event => setRecordingTitle(event.target.value)} /></label><label className="field">Task <small>Your intended outcome</small><textarea value={recordingTask} onChange={event => setRecordingTask(event.target.value)} /></label><label className="field">Context <small>Not observed evidence</small><textarea value={recordingContext} onChange={event => setRecordingContext(event.target.value)} /></label><button className="primary-button" onClick={() => void saveRecordingDetails()}>Save details</button></section></div>}
    {regenerateDialog && <div className="dialog-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) setRegenerateDialog(false); }}><section className="dialog" role="dialog" aria-modal="true" aria-label="Regenerate descriptions"><div className="details-title"><h2>Regenerate descriptions</h2><button className="icon-button" onClick={() => setRegenerateDialog(false)} aria-label="Close regenerate descriptions"><X size={16} /></button></div><p className="muted">Generate fresh descriptions and bypass cached results.</p><label className="check-field"><input type="checkbox" checked={replaceManual} onChange={event => setReplaceManual(event.target.checked)} /> Also replace my manual descriptions</label><div className="settings-actions"><button className="secondary-button" disabled={!selectedId} onClick={() => void regenerate(true)}>Selected moment</button><button className="primary-button" onClick={() => void regenerate(false)}>All moments</button></div></section></div>}
    {originalUrl && <div className="original-backdrop"><div className="original-toolbar"><span>Original pixels</span><button className="icon-button" aria-label="Close original pixels" onClick={() => setOriginalUrl(null)}><X size={18} /></button></div><div className="original-scroll"><img src={originalUrl} alt="Original recorded pixels" /></div></div>}
    {contextMenu && <div className="popover context-popover" role="menu" style={{ left: Math.min(contextMenu.x, window.innerWidth - 190), top: Math.min(contextMenu.y, window.innerHeight - 155) }} onClick={event => event.stopPropagation()}><button role="menuitem" onClick={() => void curate('add_step')}>Select current frame</button><button role="menuitem" disabled={!contextMenu.stepId} onClick={() => void curate('replace_step')}>Use frame for moment</button><button role="menuitem" disabled={!contextMenu.stepId} onClick={() => void curate('remove_step')}>Remove moment</button><button role="menuitem" disabled={!contextMenu.stepId} onClick={() => void showAlternatives()}>Nearby frames…</button></div>}
    {alternativeFrames && <div className="dialog-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) setAlternativeFrames(null); }}><section className="dialog" role="dialog" aria-modal="true" aria-label="Nearby frames"><div className="details-title"><h2>Nearby frames</h2><button className="icon-button" aria-label="Close nearby frames" onClick={() => setAlternativeFrames(null)}><X size={16} /></button></div><div className="alternative-list">{alternativeFrames.length ? alternativeFrames.map(index => <button key={index} onClick={() => void curate('replace_step', index)}>{formatTime(snapshot?.project.frames[index]?.time_ms ?? 0)}</button>) : <p className="muted">No nearby alternatives available.</p>}</div></section></div>}
    <footer className="statusbar"><span className={dirty ? 'unsaved' : ''}>{dirty ? 'Draft not saved' : activity}</span><div>{activeOperationId && activeOperationKind.current === 'generate' && <span>{savedDescriptions} descriptions saved</span>}{openingId && <button onClick={() => void cancelOpening()}>Cancel opening</button>}{activeOperationId && <button onClick={() => void cancelActiveOperation()}>Cancel operation</button>}{busy && <span className="busy-dot" aria-label="Working" />}{snapshot && <><button aria-label="Previous moment" onClick={() => { const i = snapshot.project.steps.findIndex(s => s.id === selectedId); if (i > 0) void selectStep(snapshot.project.steps[i - 1]); }}><ChevronLeft size={15} /></button><button aria-label="Next moment" onClick={() => { const i = snapshot.project.steps.findIndex(s => s.id === selectedId); if (i < snapshot.project.steps.length - 1) void selectStep(snapshot.project.steps[i + 1]); }}><ChevronRight size={15} /></button></>}</div></footer>
  </div>;
}
