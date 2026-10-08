import { _electron as electron } from 'playwright-core';
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { mkdir, mkdtemp, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
if (packagedIndex < 0) throw new Error('Use --packaged <exe>');
const executablePath = path.resolve(process.argv[packagedIndex + 1]);
await mkdir(path.join(repo, '.tmp/electron'), { recursive: true });
const root = await mkdtemp(path.join(repo, '.tmp/electron/forced-close-'));
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated forced-close test\n');
const application = await electron.launch({ executablePath, cwd: path.dirname(executablePath), args: ['--data-root', root], timeout: 10_000 });
const main = application.process();
let enginePid = 0;
let appPid = 0;
const running = pid => { try { process.kill(pid, 0); return true; } catch { return false; } };
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'Your recording studio' }).waitFor({ timeout: 10_000 });
  appPid = Number(execFileSync('powershell.exe', ['-NoProfile', '-File', path.join(import.meta.dirname, 'app-pid.ps1'), '-Root', root], { encoding: 'utf8', timeout: 5_000 }).trim());
  enginePid = Number(execFileSync('powershell.exe', ['-NoProfile', '-File', path.join(import.meta.dirname, 'engine-pid.ps1'), '-Root', root], { encoding: 'utf8', timeout: 5_000 }).trim());
  process.kill(appPid);
  const start = performance.now();
  while (running(enginePid) && performance.now() - start < 5_000) await new Promise(resolve => setTimeout(resolve, 100));
  if (running(enginePid)) throw new Error('Engine child survived forced UI termination');
  const engineExitMs = Math.round(performance.now() - start);
  const reopened = await electron.launch({ executablePath, cwd: path.dirname(executablePath), args: ['--data-root', root], timeout: 10_000 });
  try { await (await reopened.firstWindow()).getByRole('heading', { name: 'Your recording studio' }).waitFor({ timeout: 10_000 }); }
  finally { await reopened.close(); }
  console.log(JSON.stringify({ forced_close: 'engine_exited_and_lock_released', root, engine_exit_ms: engineExitMs }));
} finally {
  if (running(main.pid)) main.kill();
  if (appPid && running(appPid)) process.kill(appPid);
  if (enginePid && running(enginePid)) process.kill(enginePid);
}
