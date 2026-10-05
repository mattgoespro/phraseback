import { _electron as electron } from 'playwright-core';
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { chmod, cp, mkdtemp, readFile, rename, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const root = await mkdtemp(path.join(repo, '.tmp/electron/recovery-conflict-'));
await cp(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated recovery conflict\n');
const file = path.join(root, 'sessions', 'synthetic-settings', 'project.json');
const original = JSON.parse(await readFile(file, 'utf8'));
const missing = process.argv.includes('--missing');
const retained = path.join(root, 'retained-project');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const executablePath = packaged || path.join(repo, 'app/electron/node_modules/electron/dist/electron.exe');
const application = await electron.launch({ executablePath,
  cwd: packaged ? path.dirname(executablePath) : path.join(repo, 'app/electron'),
  args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, 'dist/Phraseback/Phraseback.Engine.exe')], timeout: 15_000 });
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor({ timeout: 15_000 });
  await page.getByRole('button', { name: 'More actions' }).click();
  await page.getByRole('button', { name: 'Show description details' }).click();
  await chmod(file, 0o444);
  const action = page.getByRole('textbox', { name: 'Action', exact: true });
  await action.fill('Unsaved draft survives the crash');
  await page.getByRole('alert').getByText(/Draft not saved/).waitFor({ timeout: 6_000 });
  const enginePid = Number(execFileSync('powershell.exe', ['-NoProfile', '-File', path.join(import.meta.dirname, 'engine-pid.ps1'), '-Root', root], { encoding: 'utf8', timeout: 5_000 }).trim());
  process.kill(enginePid);
  await page.getByRole('alert').getByText(/Engine disconnected/).waitFor({ timeout: 6_000 });
  await chmod(file, 0o666);
  if (missing) {
    if (!path.resolve(file).startsWith(root + path.sep) || !path.resolve(retained).startsWith(root + path.sep)) throw new Error('Unsafe isolated fixture move');
    await rename(path.dirname(file), retained);
  } else {
    const changed = { ...original, context: 'Saved context changed after the engine crash' };
    await writeFile(file, JSON.stringify(changed));
  }
  const savedFile = missing ? path.join(retained, 'project.json') : file;
  const beforeReconnect = await readFile(savedFile);
  await page.getByRole('button', { name: 'Reconnect', exact: true }).click();
  const resolution = page.getByRole('button', { name: missing ? 'Discard draft and open Library' : 'Use saved version' });
  await resolution.waitFor({ timeout: 10_000 });
  await application.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0].setSize(1024, 700));
  const reachable = await resolution.evaluate(button => {
    const box = button.getBoundingClientRect(); button.focus();
    return document.activeElement === button && box.left >= 0 && box.top >= 0 && box.right <= innerWidth && box.bottom <= innerHeight;
  });
  if (!reachable) throw new Error('Recovery action is not visible and focusable at 1024×700');
  if (await action.inputValue() !== 'Unsaved draft survives the crash') throw new Error('Conflict discarded the unsaved draft');
  await action.fill('Still unsaved after conflict');
  await page.waitForTimeout(700);
  if (!beforeReconnect.equals(await readFile(savedFile))) throw new Error('Conflicting draft overwrote saved project');
  await resolution.click();
  if (missing) await page.getByRole('heading', { name: 'Library' }).waitFor({ timeout: 10_000 });
  else if (await action.inputValue() !== original.steps[0].action) throw new Error('Saved version did not reopen');
  console.log(JSON.stringify({ recovery_conflict: missing ? 'missing_recording_resolved' : 'draft_retained_then_explicitly_discarded', root, saved_project_unchanged: true }));
} finally {
  await chmod(missing ? path.join(retained, 'project.json') : file, 0o666).catch(() => undefined);
  await application.close();
}
