import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { cp, mkdtemp, readFile, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
if (packagedIndex < 0) throw new Error('Use --packaged <exe> and optionally --edit-during');
const executablePath = path.resolve(process.argv[packagedIndex + 1]);
const editDuring = process.argv.includes('--edit-during');
const root = await mkdtemp(path.join(repo, '.tmp/electron/organization-lifecycle-'));
await cp(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated organization lifecycle test\n');
const directory = path.join(root, 'sessions', 'synthetic-settings');
const projectFile = path.join(directory, 'project.json');
const frameFile = path.join(directory, 'frames', '0000000.png');
const source = JSON.parse(await readFile(projectFile, 'utf8'));
source.frames = Array.from({ length: 4000 }, (_, index) => ({ ...source.frames[0], time_ms: index * 125 }));
source.duration_ms = 500000;
await writeFile(projectFile, JSON.stringify(source));
const originalProject = await readFile(projectFile);
const originalFrameHash = createHash('sha256').update(await readFile(frameFile)).digest('hex');
const application = await electron.launch({ executablePath, cwd: path.dirname(executablePath), args: ['--data-root', root], timeout: 15_000 });
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor({ timeout: 15_000 });
  if (editDuring) {
    await page.getByRole('button', { name: 'More actions' }).click();
    await page.getByRole('button', { name: 'Show description details' }).click();
  }
  await page.getByRole('button', { name: 'More actions' }).click();
  await page.getByRole('button', { name: 'Organize', exact: true }).click();
  if (editDuring) {
    const action = 'Manual edit made while organization is running';
    await page.getByRole('textbox', { name: 'Action', exact: true }).fill(action);
    await page.getByRole('button', { name: 'Save description' }).click();
    await page.getByText('Recording changed; organization suggestions were discarded').waitFor({ timeout: 30_000 });
    const saved = JSON.parse(await readFile(projectFile, 'utf8'));
    if (!saved.steps.some(step => step.action === action)) throw new Error('Concurrent manual description was lost');
    if (saved.steps.length !== source.steps.length) throw new Error('Stale organization changed moment selection');
  } else {
    await page.getByRole('button', { name: 'More actions' }).click();
    await page.getByRole('button', { name: 'Cancel organizing' }).click();
    await page.getByText('Organization cancelled').waitFor({ timeout: 15_000 });
    if (!originalProject.equals(await readFile(projectFile))) throw new Error('Cancelled organization changed project bytes');
  }
  if (createHash('sha256').update(await readFile(frameFile)).digest('hex') !== originalFrameHash) throw new Error('Organization changed original evidence');
  console.log(JSON.stringify({ organization_lifecycle: editDuring ? 'concurrent_edit_preserved' : 'cancelled_without_save', root, original_png_unchanged: true }));
} finally { await application.close(); }
