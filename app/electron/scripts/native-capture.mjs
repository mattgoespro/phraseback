import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { mkdir, mkdtemp, readFile, readdir, writeFile } from 'node:fs/promises';
import { execFileSync, spawn } from 'node:child_process';

const repo = path.resolve(import.meta.dirname, '../../..');
await mkdir(path.join(repo, '.tmp/electron'), { recursive: true });
const root = await mkdtemp(path.join(repo, '.tmp/electron/capture-'));
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated native capture test\n');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const engineIndex = process.argv.indexOf('--engine');
const enginePath = engineIndex < 0 ? path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe') : path.resolve(process.argv[engineIndex + 1]);
let surface;
const exclusion = process.argv.includes('--exclusion');
if (process.argv.includes('--performance') || exclusion) {
  surface = spawn(path.join(repo, 'app/electron/node_modules/electron/dist/electron.exe'), [path.join(import.meta.dirname, 'changing-surface.cjs')], {
    cwd: path.join(repo, 'app/electron'), stdio: ['pipe', 'pipe', 'pipe']
  });
  surface.stderr.resume();
  await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Changing surface did not start')), 8_000);
    surface.stdout.on('data', chunk => { if (String(chunk).includes('SURFACE_READY')) { clearTimeout(timer); resolve(); } });
    surface.on('exit', code => { clearTimeout(timer); reject(new Error(`Changing surface exited: ${code}`)); });
  });
  let visible = false;
  for (let attempt = 0; attempt < 8 && !visible; attempt++) {
    try { execFileSync('powershell.exe', ['-NoProfile', '-File', path.join(import.meta.dirname, 'surface-visible.ps1')], { stdio: 'ignore' }); visible = true; }
    catch { await new Promise(resolve => setTimeout(resolve, 200)); }
  }
  if (!visible) { surface.kill(); throw new Error('Synthetic capture surface did not become visible'); }
}
const application = await electron.launch({
  executablePath: packaged || path.join(repo, 'app/electron/node_modules/electron/dist/electron.exe'),
  cwd: packaged ? path.dirname(packaged) : path.join(repo, 'app/electron'),
  args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', enginePath],
  timeout: 10_000
});
try {
  const page = await application.firstWindow({ timeout: 10_000 });
  const activate = async locator => surface ? locator.evaluate(element => element.click()) : locator.click();
  await activate(page.getByRole('button', { name: 'Open navigation' }));
  await activate(page.getByRole('banner').getByRole('button', { name: 'New recording' }));
  await activate(page.getByRole('button', { name: 'Start recording' }));
  await page.getByRole('heading', { name: 'Recording your screen' }).waitFor({ timeout: 8_000 });
  if (surface) execFileSync('powershell.exe', ['-NoProfile', '-File', path.join(import.meta.dirname, 'surface-visible.ps1')]);
  if (exclusion) {
    const appProcess = await application.evaluate(() => process.pid);
    await application.evaluate(({ BrowserWindow }) => { const window = BrowserWindow.getAllWindows().find(window => window.webContents.getURL().startsWith('file:')); window?.setAlwaysOnTop(true, 'screen-saver'); window?.show(); window?.focus(); window?.moveTop(); });
    await new Promise(resolve => setTimeout(resolve, 300));
    const appBounds = await application.evaluate(({ BrowserWindow }) => { const window = BrowserWindow.getAllWindows().find(window => window.webContents.getURL().startsWith('file:')); return { bounds: window?.getBounds(), visible: window?.isVisible(), topmost: window?.isAlwaysOnTop() }; });
    const covering = JSON.parse(execFileSync('powershell.exe', ['-NoProfile', '-File', path.join(import.meta.dirname, 'window-at-point.ps1')], { encoding: 'utf8' }));
    if (covering.pid !== appProcess) throw new Error(`App did not cover the synthetic exclusion marker: ${JSON.stringify({covering,appProcess,appBounds})}`);
  }
  await page.waitForTimeout(process.argv.includes('--performance') || process.argv.includes('--long') || exclusion ? 8500 : 4700);
  const stop = process.argv.includes('--hotkey') ? 'hotkey' : process.argv.includes('--floating') ? 'floating' : 'main';
  const stoppingAt = performance.now();
  if (stop === 'hotkey') execFileSync('powershell.exe', ['-NoProfile', '-File', path.join(import.meta.dirname, 'send-hotkey.ps1')]);
  else if (stop === 'floating') {
    let control;
    for (const candidate of application.windows()) {
      if (candidate !== page && candidate.url().startsWith('data:text/html') && await candidate.getByRole('link', { name: 'Stop' }).count()) { control = candidate; break; }
    }
    if (!control) throw new Error('Floating recording controls missing');
    await activate(control.getByRole('link', { name: 'Stop' }));
  } else await activate(page.getByRole('button', { name: 'Stop recording' }));
  await page.getByRole('heading', { name: 'Desktop workflow' }).waitFor({ timeout: 10_000 });
  const stopToReviewMs = performance.now() - stoppingAt;
  const sessions = await readdir(path.join(root, 'sessions'));
  const recording = path.join(root, 'sessions', sessions[0]);
  if (surface) execFileSync('powershell.exe', ['-NoProfile', '-File', path.join(import.meta.dirname, 'captured-surface.ps1'), '-Path', path.join(recording, 'frames', '0000000.png'), ...(exclusion ? ['-CheckCenter'] : [])]);
  const metrics = JSON.parse(await readFile(path.join(recording, 'capture-metrics.json'), 'utf8'));
  const lastElapsedMs = metrics.gpu_memory?.recent?.at(-1)?.elapsed_ms || 0;
  console.log(JSON.stringify({ capture: 'completed', stop, root, samples: metrics.samples, accepted: metrics.accepted,
    duplicates: metrics.duplicates, missed: metrics.missed, elapsed_ms: lastElapsedMs,
    effective_samples_per_second: lastElapsedMs ? metrics.samples * 1000 / lastElapsedMs : null,
    stop_to_review_ms: Math.round(stopToReviewMs), pool_bytes: metrics.pool_bytes,
    gpu_local_peak_bytes: metrics.gpu_memory?.local_peak_bytes }));
} finally {
  await application.close();
  surface?.kill();
}
