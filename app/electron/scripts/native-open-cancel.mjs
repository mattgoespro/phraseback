import { _electron as electron } from 'playwright-core';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { cp, mkdir, mkdtemp, readFile, readdir, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const sourceIndex = process.argv.indexOf('--root');
if (sourceIndex < 0) throw new Error('Use --root <isolated capture root> and optionally --packaged <exe>');
const sourceRoot = path.resolve(process.argv[sourceIndex + 1]);
if (!sourceRoot.startsWith(path.join(repo, '.tmp') + path.sep)) throw new Error('Open cancellation requires isolated .tmp evidence');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const sourceId = (await readdir(path.join(sourceRoot, 'sessions')))[0];
const root = await mkdtemp(path.join(repo, '.tmp/electron/open-cancel-'));
await mkdir(path.join(root, 'sessions'));
await cp(path.join(sourceRoot, 'sessions', sourceId), path.join(root, 'sessions', sourceId), { recursive: true });
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated open cancellation\n');
const directory = path.join(root, 'sessions', sourceId);
const projectFile = path.join(directory, 'project.json');
const journalFile = path.join(directory, 'frames.jsonl');
const firstFrame = path.join(directory, 'frames', '0000000.png');
const originalHash = createHash('sha256').update(await readFile(firstFrame)).digest('hex');
const project = JSON.parse(await readFile(projectFile, 'utf8'));
project.state = 'recording';
await writeFile(projectFile, JSON.stringify(project));
const interrupted = await readFile(projectFile);
const journal = await readFile(journalFile, 'utf8');
const last = project.frames.at(-1).time_ms;
const longTail = Array.from({ length: 100_000 }, (_, index) => JSON.stringify({ file: 'frames/0000000.png', time_ms: last + (index + 1) * 125 })).join('\n');
await writeFile(journalFile, journal.trimEnd() + '\n' + longTail + '\n');
const application = await electron.launch({
  executablePath: packaged || path.join(repo, 'app/electron/node_modules/electron/dist/electron.exe'),
  cwd: packaged ? path.dirname(packaged) : path.join(repo, 'app/electron'),
  args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe')], timeout: 10_000
});
try {
  const page = await application.firstWindow();
  const cancel = page.getByRole('button', { name: 'Cancel opening' });
  await cancel.waitFor({ timeout: 10_000 });
  await cancel.click();
  await cancel.waitFor({ state: 'hidden', timeout: 10_000 });
  if (!interrupted.equals(await readFile(projectFile))) throw new Error('Cancelled recovery published partial metadata');
  await writeFile(journalFile, journal);
  await page.getByRole('button', { name: 'Library', exact: true }).click();
  await page.locator('.library-list').getByRole('button').first().click();
  await page.getByRole('heading', { name: 'Desktop workflow' }).waitFor({ timeout: 15_000 });
  const recovered = JSON.parse(await readFile(projectFile, 'utf8'));
  if (recovered.state !== 'ready' || recovered.frames.length !== project.frames.length) throw new Error('Recording did not recover after cancelled open');
  if (createHash('sha256').update(await readFile(firstFrame)).digest('hex') !== originalHash) throw new Error('Recovery changed original evidence');
  console.log(JSON.stringify({ open_cancellation: 'passed', root, recovered_frames: recovered.frames.length, original_png_unchanged: true }));
} finally { await application.close(); }
