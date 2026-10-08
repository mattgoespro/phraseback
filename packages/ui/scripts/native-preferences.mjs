import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { mkdir, mkdtemp, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const executablePath = packaged || path.join(repo, 'packages/ui/node_modules/electron/dist/electron.exe');
await mkdir(path.join(repo, '.tmp/electron'), { recursive: true });
const root = await mkdtemp(path.join(repo, '.tmp/electron/preferences-'));
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated preference test\n');
async function launch() { return electron.launch({ executablePath, cwd: packaged ? path.dirname(executablePath) : path.join(repo, 'packages/ui'), args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, 'dist/Phraseback/Phraseback.Engine.exe')], timeout: 10_000 }); }
const first = await launch();
const firstProcess = first.process();
let initialBounds;
try {
  await (await first.firstWindow()).getByRole('heading', { name: 'Your recording studio' }).waitFor({ timeout: 10_000 });
  await first.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0].setBounds({ x: 80, y: 80, width: 1100, height: 720 }));
  initialBounds = await first.evaluate(({ BrowserWindow }) => { const w = BrowserWindow.getAllWindows()[0]; return { bounds: w.getBounds(), normal: w.getNormalBounds(), content: w.getContentBounds() }; });
  const exited = new Promise(resolve => firstProcess.once('exit', resolve));
  await first.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0].close());
  const exit = await Promise.race([
    exited,
    new Promise((_, reject) => setTimeout(() => reject(new Error('Preference save close timed out')), 8_000))
  ]);
  void exit;
} finally { if (firstProcess.exitCode === null) firstProcess.kill(); }
const second = await launch();
let restored;
try {
  await (await second.firstWindow()).getByRole('heading', { name: 'Your recording studio' }).waitFor({ timeout: 10_000 });
  restored = await second.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0].getNormalBounds());
  if (Math.abs(restored.width - 1100) > 2 || restored.height !== 720) throw new Error(`Window size was not restored: ${JSON.stringify({ initialBounds, restored })}`);
} finally { await second.close(); }
const third = await launch();
try {
  await (await third.firstWindow()).getByRole('heading', { name: 'Your recording studio' }).waitFor({ timeout: 10_000 });
  const repeated = await third.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0].getNormalBounds());
  if (repeated.width > restored.width || repeated.height > restored.height) throw new Error(`Window grew across restarts: ${JSON.stringify({ initialBounds, restored, repeated })}`);
  console.log(JSON.stringify({ preferences: 'restored', root, initial: initialBounds.normal, restored, repeated }));
} finally { await third.close(); }
