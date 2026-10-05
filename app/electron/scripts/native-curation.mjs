import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { cp, mkdir, mkdtemp, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
await mkdir(path.join(repo, '.tmp/electron'), { recursive: true });
const root = await mkdtemp(path.join(repo, '.tmp/electron/curation-'));
await cp(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated curation test\n');
const application = await electron.launch({ executablePath: packaged || path.join(repo, 'app/electron/node_modules/electron/dist/electron.exe'), cwd: packaged ? path.dirname(packaged) : path.join(repo, 'app/electron'), args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe')], timeout: 10_000 });
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor();
  const initial = await page.locator('.moment').count();
  await page.locator('.moment.active').click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Remove moment' }).click();
  await page.waitForFunction(count => document.querySelectorAll('.moment').length === count - 1, initial, { timeout: 8_000 });
  await page.getByRole('slider', { name: 'Recording timeline' }).fill('0');
  await page.locator('.evidence-stage').click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Select current frame' }).click();
  await page.waitForFunction(count => document.querySelectorAll('.moment').length === count, initial, { timeout: 8_000 });
  console.log(JSON.stringify({ removedAndAdded: true, initial }));
} finally { await application.close(); }
