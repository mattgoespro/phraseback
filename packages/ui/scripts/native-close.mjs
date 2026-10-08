import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { mkdir, mkdtemp, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
await mkdir(path.join(repo, '.tmp/electron'), { recursive: true });
const root = await mkdtemp(path.join(repo, '.tmp/electron/close-'));
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated close test\n');
const application = await electron.launch({ executablePath: packaged || path.join(repo, 'packages/ui/node_modules/electron/dist/electron.exe'), cwd: packaged ? path.dirname(packaged) : path.join(repo, 'packages/ui'), args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe')], timeout: 10_000 });
const processHandle = application.process();
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'Your recording studio' }).waitFor();
  const exit = new Promise(resolve => processHandle.once('exit', resolve));
  await application.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0].close());
  await Promise.race([exit, new Promise((_, reject) => { const timer = setTimeout(() => reject(new Error('Native window close exceeded 8 seconds')), 8000); timer.unref(); })]);
  console.log(JSON.stringify({ closed: true, root }));
} finally { if (processHandle.exitCode === null) processHandle.kill(); }
