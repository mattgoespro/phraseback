import { _electron as electron } from 'playwright-core';
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { readFile, readdir, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const rootIndex = process.argv.indexOf('--root');
if (rootIndex < 0) throw new Error('Use --root <isolated recording root> and optionally --packaged <exe>');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const executablePath = packaged || path.join(repo, 'packages/ui/node_modules/electron/dist/electron.exe');
const root = path.resolve(process.argv[rootIndex + 1]);
if (!root.startsWith(path.join(repo, '.tmp') + path.sep)) throw new Error('Review endurance requires isolated .tmp evidence');
const sessions = await readdir(path.join(root, 'sessions'));
if (!sessions.length) throw new Error('Review endurance requires isolated .tmp evidence');
const project = JSON.parse(await readFile(path.join(root, 'sessions', sessions[0], 'project.json'), 'utf8'));
const frameCount = project.frames?.length;
if (!Number.isInteger(frameCount) || frameCount < 1) throw new Error('Review endurance requires frames');
const application = await electron.launch({ executablePath, cwd: packaged ? path.dirname(executablePath) : path.join(repo, 'packages/ui'),
  args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, 'dist/Phraseback/Phraseback.Engine.exe')], timeout: 10_000 });
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'Desktop workflow' }).waitFor({ timeout: 10_000 });
  const memory = () => JSON.parse(execFileSync('powershell.exe', ['-NoProfile', '-File', path.join(import.meta.dirname, 'tree-memory.ps1'), '-Root', root, '-Name', packaged ? 'Phraseback.exe' : 'electron.exe'], { encoding: 'utf8', timeout: 8_000 }));
  const samples = [];
  for (let batch = 0; batch < 5; batch++) {
    await page.evaluate(async ({ batch, frameCount }) => {
      const { items } = await window.phraseback.request('library_page', { offset: 0, limit: 10 });
      for (let n = 0; n < 160; n++) {
        const index = (batch * 160 + n) % frameCount;
        const frame = await window.phraseback.request('frame', { recording_id: items[0].id, index, preview_width: 1440 });
        const image = new Image(); image.src = frame.url; await image.decode();
      }
    }, { batch, frameCount });
    samples.push({ seeks: (batch + 1) * 160, ...memory() });
  }
  const growth = samples.at(-1).private_bytes - samples[1].private_bytes;
  const report = { review_endurance: 'measured', root, samples, post_warmup_private_growth_bytes: growth };
  await writeFile(path.join(root, `electron-review-endurance-${packaged ? 'packaged' : 'dev'}.json`), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report));
} finally { await application.close(); }
