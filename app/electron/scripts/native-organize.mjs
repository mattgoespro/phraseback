import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { cp, mkdtemp, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const root = await mkdtemp(path.join(repo, '.tmp/electron/organize-'));
await cp(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated organization test\n');
const application = await electron.launch({
  executablePath: packaged || path.join(repo, 'app/electron/node_modules/electron/dist/electron.exe'),
  cwd: packaged ? path.dirname(packaged) : path.join(repo, 'app/electron'),
  args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe')],
  timeout: 15_000
});
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor();
  await page.getByRole('button', { name: 'More actions' }).click();
  await page.getByRole('button', { name: 'Organize', exact: true }).click();
  await page.getByText('Screenshots organized · ready for review').waitFor({ timeout: 12_000 });
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor();
  process.stdout.write('Background organization applied on isolated recording\n');
} finally { await application.close(); }
