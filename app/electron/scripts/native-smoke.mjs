import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { cp, mkdir, mkdtemp, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const root = await mkdtemp(path.join(repo, '.tmp/electron/smoke-'));
await cp(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated native test\n');
const engine = path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const output = path.join(repo, '.tmp/electron/native-smoke.png');
await mkdir(path.dirname(output), { recursive: true });
const application = await electron.launch({
  executablePath: packaged || path.join(repo, 'app/electron/node_modules/electron/dist/electron.exe'),
  cwd: packaged ? path.dirname(packaged) : path.join(repo, 'app/electron'),
  args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', engine],
  timeout: 15_000
});
let page;
try {
  page = await application.firstWindow({ timeout: 10_000 });
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor({ timeout: 10_000 });
  const preview = page.getByAltText('Recorded screen evidence');
  await page.waitForFunction(() => { const image = document.querySelector('img[alt="Recorded screen evidence"]'); return image instanceof HTMLImageElement && image.complete && image.naturalWidth > 0; });
  const loaded = await preview.evaluate(image => image instanceof HTMLImageElement && image.complete && image.naturalWidth > 0);
  if (!loaded) throw new Error('Evidence preview did not load');
  await page.screenshot({ path: output });
  await page.getByRole('button', { name: 'More actions' }).click();
  await page.getByRole('button', { name: 'Show description details' }).click();
  const title = page.getByLabel('Title');
  const original = await title.inputValue();
  await title.fill(`${original} test`);
  await page.getByText('Saved locally').waitFor({ timeout: 8_000 });
  await page.reload();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor({ timeout: 10_000 });
  await page.getByRole('button', { name: 'More actions' }).click();
  await page.getByRole('button', { name: 'Show description details' }).click();
  if (await page.getByLabel('Title').inputValue() !== `${original} test`) throw new Error('Description did not persist');
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await page.getByRole('button', { name: 'Get prompt' }).click();
  await page.getByRole('heading', { name: 'Get prompt' }).waitFor();
  if (!(await page.locator('pre').textContent())?.includes('A calmer workspace')) throw new Error('Prompt did not render');
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await page.getByRole('heading', { name: 'Settings' }).waitFor();
  await page.getByRole('heading', { name: 'Prompt format' }).waitFor();
  console.log(JSON.stringify({ title: await page.title(), previewLoaded: loaded, draftPersisted: true, promptRendered: true, settingsOpened: true, screenshot: output }));
} finally {
  try { if (page && !page.isClosed()) await application.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0]?.close()); await Promise.race([application.close(), new Promise((_, reject) => { const timer = setTimeout(() => reject(new Error('Electron did not close within 10 seconds')), 10_000); timer.unref(); })]); }
  catch (error) { application.process().kill(); throw error; }
}
