import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const rootIndex = process.argv.indexOf('--root');
if (packagedIndex < 0 || rootIndex < 0) throw new Error('Use --packaged <exe> --root <isolated capture root>');
const executablePath = path.resolve(process.argv[packagedIndex + 1]);
const root = path.resolve(process.argv[rootIndex + 1]);
if (!root.startsWith(path.join(repo, '.tmp') + path.sep)) throw new Error('Review performance requires isolated .tmp data');
const application = await electron.launch({ executablePath, cwd: path.dirname(executablePath), args: ['--data-root', root], timeout: 10_000 });
try {
  const page = await application.firstWindow({ timeout: 10_000 });
  await page.getByRole('heading', { name: 'Desktop workflow' }).waitFor({ timeout: 10_000 });
  const result = await page.evaluate(async () => {
    const { items } = await window.phraseback.request('library_page', { offset: 0, limit: 10 });
    const id = items[0].id;
    const p95 = values => { const sorted = [...values].sort((a, b) => a - b); return sorted[Math.ceil(sorted.length * .95) - 1]; };
    const latency = [];
    let last = performance.now();
    const timer = setInterval(() => { const now = performance.now(); latency.push(Math.max(0, now - last - 20)); last = now; }, 20);
    async function seek(index) {
      const start = performance.now();
      const reference = await window.phraseback.request('frame', { recording_id: id, index, preview_width: 1440 });
      const image = new Image(); image.src = reference.url; await image.decode();
      return performance.now() - start;
    }
    const first = [];
    const repeated = [];
    try {
      for (let index = 1; index <= 20; index++) first.push(await seek(index));
      for (let index = 0; index < 20; index++) repeated.push(await seek(index % 2 + 1));
    } finally { clearInterval(timer); }
    return { uncached_seek_ms: first, cached_seek_ms: repeated,
      uncached_p95_ms: p95(first), cached_p95_ms: p95(repeated),
      renderer_dispatch_p95_ms: p95(latency), renderer_dispatch_max_ms: Math.max(...latency), dispatch_samples: latency.length };
  });
  const report = { ...result, packaged: executablePath, root, recorded_at: new Date().toISOString() };
  await writeFile(path.join(root, 'electron-review-performance.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report));
} finally { await application.close(); }
