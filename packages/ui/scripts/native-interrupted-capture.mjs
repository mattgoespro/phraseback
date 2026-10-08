import { _electron as electron } from 'playwright-core';
import { execFileSync, spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { mkdir, mkdtemp, readFile, readdir, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
await mkdir(path.join(repo, '.tmp/electron'), { recursive: true });
const root = await mkdtemp(path.join(repo, '.tmp/electron/interrupted-capture-'));
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated interrupted capture\n');
const surface = spawn(path.join(repo, 'packages/ui/node_modules/electron/dist/electron.exe'), [path.join(import.meta.dirname, 'changing-surface.cjs')], {
  cwd: path.join(repo, 'packages/ui'), stdio: ['ignore', 'pipe', 'pipe']
});
surface.stderr.resume();
try {
  await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Synthetic surface startup timed out')), 8_000);
    surface.stdout.on('data', chunk => { if (String(chunk).includes('SURFACE_READY')) { clearTimeout(timer); resolve(); } });
    surface.on('exit', code => { clearTimeout(timer); reject(new Error(`Synthetic surface exited: ${code}`)); });
  });
  let visible = false;
  for (let attempt = 0; attempt < 8 && !visible; attempt++) {
    try { execFileSync('powershell.exe', ['-NoProfile', '-File', path.join(import.meta.dirname, 'surface-visible.ps1')], { stdio: 'ignore' }); visible = true; }
    catch { await new Promise(resolve => setTimeout(resolve, 200)); }
  }
  if (!visible) throw new Error('Synthetic surface was not visible');
  const application = await electron.launch({
    executablePath: packaged || path.join(repo, 'packages/ui/node_modules/electron/dist/electron.exe'),
    cwd: packaged ? path.dirname(packaged) : path.join(repo, 'packages/ui'),
    args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe')], timeout: 10_000
  });
  try {
    const page = await application.firstWindow();
    const activate = locator => locator.evaluate(element => element.click());
    await activate(page.getByRole('button', { name: 'Open navigation' }));
    await activate(page.getByRole('banner').getByRole('button', { name: 'New recording' }));
    await activate(page.getByRole('button', { name: 'Start recording' }));
    await page.getByRole('heading', { name: 'Recording your screen' }).waitFor({ timeout: 8_000 });
    await page.waitForTimeout(6_000);
    const enginePid = Number(execFileSync('powershell.exe', ['-NoProfile', '-File', path.join(import.meta.dirname, 'engine-pid.ps1'), '-Root', root], { encoding: 'utf8', timeout: 5_000 }).trim());
    process.kill(enginePid);
    surface.kill();
    const sessions = await readdir(path.join(root, 'sessions'));
    if (sessions.length !== 1) throw new Error(`Expected one interrupted recording; found ${sessions.length}`);
    const directory = path.join(root, 'sessions', sessions[0]);
    const firstFrame = path.join(directory, 'frames', '0000000.png');
    execFileSync('powershell.exe', ['-NoProfile', '-File', path.join(import.meta.dirname, 'captured-surface.ps1'), '-Path', firstFrame]);
    const originalHash = createHash('sha256').update(await readFile(firstFrame)).digest('hex');
    await page.getByRole('button', { name: 'Reconnect', exact: true }).waitFor({ timeout: 8_000 });
    await page.getByRole('button', { name: 'Reconnect', exact: true }).click();
    await page.getByRole('heading', { name: 'Library' }).waitFor({ timeout: 10_000 });
    await page.locator('.library-list').getByRole('button').first().click();
    await page.getByRole('heading', { name: 'Desktop workflow' }).waitFor({ timeout: 15_000 });
    const recovered = JSON.parse(await readFile(path.join(directory, 'project.json'), 'utf8'));
    if (recovered.state !== 'ready' || recovered.frames.length < 5 || !recovered.error.includes('interrupted')) throw new Error('Recording did not recover usable evidence');
    if (createHash('sha256').update(await readFile(firstFrame)).digest('hex') !== originalHash) throw new Error('Recovery changed the original PNG');
    console.log(JSON.stringify({ capture_recovery: 'passed', root, frames: recovered.frames.length, original_png_unchanged: true }));
  } finally { await application.close(); }
} finally { surface.kill(); }
