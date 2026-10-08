import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { cp, mkdir, mkdtemp, readdir, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
await mkdir(path.join(repo, '.tmp/electron'), { recursive: true });
const root = await mkdtemp(path.join(repo, '.tmp/electron/export-'));
await cp(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated export test\n');
const destination = path.join(root, 'exports');
await mkdir(destination);
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const application = await electron.launch({ executablePath: packaged || path.join(repo, 'packages/ui/node_modules/electron/dist/electron.exe'), cwd: packaged ? path.dirname(packaged) : path.join(repo, 'packages/ui'), args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe')], timeout: 10_000 });
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor();
  await application.evaluate(({ dialog }, folder) => { dialog.showOpenDialog = async () => ({ canceled: false, filePaths: [folder] }); }, destination);
  await page.getByRole('button', { name: 'More actions' }).click();
  await page.getByRole('button', { name: 'Export', exact: true }).click();
  let entries = [];
  for (let attempt = 0; attempt < 100; attempt++) {
    entries = await readdir(destination);
    if (entries.length && !(await page.locator('.busy-dot').count())) break;
    await page.waitForTimeout(100);
  }
  if (!entries.length) throw new Error(`Export produced no files: ${await page.locator('.error-banner').allTextContents()}`);
  console.log(JSON.stringify({ exported: true, entries, destination }));
} finally { await application.close(); }
