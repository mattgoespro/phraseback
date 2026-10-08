import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { cp, mkdtemp, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const root = await mkdtemp(path.join(repo, '.tmp/electron/model-controls-'));
await cp(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated model-control test\n');
const application = await electron.launch({
  executablePath: packaged || path.join(repo, 'packages/ui/node_modules/electron/dist/electron.exe'),
  cwd: packaged ? path.dirname(packaged) : path.join(repo, 'packages/ui'),
  args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe')],
  timeout: 15_000
});
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor();
  await page.getByRole('button', { name: 'More actions' }).click();
  await page.getByRole('button', { name: 'Regenerate descriptions…' }).click();
  const dialog = page.getByRole('dialog', { name: 'Regenerate descriptions' });
  await dialog.waitFor();
  if (await dialog.getByRole('checkbox', { name: 'Also replace my manual descriptions' }).isChecked()) throw new Error('Manual descriptions were not protected by default');
  await dialog.getByRole('button', { name: 'Close regenerate descriptions' }).click();
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await page.getByRole('heading', { name: 'Settings' }).waitFor();
  await page.getByRole('button', { name: 'Release model memory' }).click();
  await page.getByText('Model memory released').waitFor();
  const template = page.getByLabel('Template');
  const original = await template.inputValue();
  await template.fill(`${original}\nLocal test marker`);
  await page.getByRole('button', { name: 'Save format' }).click();
  await page.getByText('Prompt format saved').waitFor();
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  if (!(await page.getByLabel('Template').inputValue()).endsWith('Local test marker')) throw new Error('Prompt template did not persist');
  await page.getByRole('button', { name: 'Reset default' }).click();
  await page.getByText('Prompt format reset').waitFor();
  process.stdout.write('Regeneration guard, model release, and prompt template save/reset passed on isolated data\n');
} finally { await application.close(); }
