import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { mkdir, readFile, readdir } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const rootIndex = process.argv.indexOf('--root');
if (packagedIndex < 0 || rootIndex < 0) throw new Error('Use --packaged <exe> --root <isolated full-display recording root>');
const executablePath = path.resolve(process.argv[packagedIndex + 1]);
const root = path.resolve(process.argv[rootIndex + 1]);
if (!root.startsWith(path.join(repo, '.tmp') + path.sep)) throw new Error('Export cancellation requires isolated .tmp data');
const recording = (await readdir(path.join(root, 'sessions')))[0];
const projectFile = path.join(root, 'sessions', recording, 'project.json');
const original = await readFile(projectFile);
const destination = path.join(root, 'cancelled-export');
await mkdir(destination);
const application = await electron.launch({ executablePath, cwd: path.dirname(executablePath), args: ['--data-root', root], timeout: 10_000 });
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'Desktop workflow' }).waitFor({ timeout: 10_000 });
  await application.evaluate(({ dialog }, folder) => { dialog.showOpenDialog = async () => ({ canceled: false, filePaths: [folder] }); }, destination);
  await page.getByRole('button', { name: 'More actions' }).click();
  await page.getByRole('button', { name: 'Export', exact: true }).click();
  const cancel = page.getByRole('button', { name: 'Cancel operation' });
  await cancel.waitFor({ timeout: 10_000 });
  await page.waitForTimeout(1_000);
  await cancel.click();
  await page.getByText('Operation cancelled · saved progress retained').waitFor({ timeout: 15_000 });
  if (!original.equals(await readFile(projectFile))) throw new Error('Export cancellation changed the project');
  const entries = await readdir(destination);
  if (entries.length) throw new Error(`Cancelled export left published files: ${entries.join(', ')}`);
  console.log(JSON.stringify({ export_cancellation: 'passed', root, project_unchanged: true, no_published_output: true }));
} finally { await application.close(); }
