import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { mkdir, mkdtemp, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
await mkdir(path.join(repo, '.tmp/electron'), { recursive: true });
const root = await mkdtemp(path.join(repo, '.tmp/electron/region-'));
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated region test\n');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const application = await electron.launch({ executablePath: packaged || path.join(repo, 'app/electron/node_modules/electron/dist/electron.exe'), cwd: packaged ? path.dirname(packaged) : path.join(repo, 'app/electron'), args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe')], timeout: 10_000 });
try {
  const page = await application.firstWindow();
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await page.getByRole('banner').getByRole('button', { name: 'New recording' }).click();
  await page.getByRole('button', { name: 'Choose rectangle…' }).click();
  let overlay;
  for (let attempt = 0; attempt < 30; attempt++) {
    for (const candidate of application.windows()) if (candidate !== page && candidate.url().startsWith('data:text/html')) overlay = candidate;
    if (overlay) break;
    await page.waitForTimeout(100);
  }
  if (!overlay) throw new Error('Region selector did not open');
  await overlay.mouse.move(80, 80);
  await overlay.mouse.down();
  await overlay.mouse.move(480, 380, { steps: 5 });
  await overlay.mouse.up();
  await page.getByLabel('Width').waitFor({ timeout: 5_000 });
  const width = Number(await page.getByLabel('Width').inputValue());
  if (width < 300) throw new Error(`Region width not mapped: ${width}`);
  console.log(JSON.stringify({ regionSelected: true, width }));
} finally { await application.close(); }
