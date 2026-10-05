import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { cp, link, mkdir, mkdtemp, writeFile } from 'node:fs/promises';
import { modelFixture } from './native-model-fixture.mjs';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const source = modelFixture(repo);
const root = await mkdtemp(path.join(repo, '.tmp/electron/model-verify-'));
const model = path.join(root, 'model');
await mkdir(model);
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated model verification\n');
await cp(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
for (const name of ['Qwen3-VL-2B-Instruct-Q8_0.gguf', 'mmproj-Qwen3-VL-2B-Instruct-Q8_0.gguf']) {
  await link(path.join(source, name), path.join(model, name));
}
await cp(path.join(source, 'runtime.zip'), path.join(model, 'runtime.zip'));
const application = await electron.launch({
  executablePath: packaged || path.join(repo, 'app/electron/node_modules/electron/dist/electron.exe'),
  cwd: packaged ? path.dirname(packaged) : path.join(repo, 'app/electron'),
  args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe')],
  timeout: 15_000
});
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor();
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await page.getByRole('button', { name: 'Verify existing files' }).click();
  await page.getByRole('button', { name: 'Cancel operation' }).waitFor({ timeout: 5_000 });
  await page.getByRole('button', { name: 'Cancel operation' }).click();
  try {
    await page.getByText('Operation cancelled · saved progress retained').waitFor({ timeout: 7_000 });
    process.stdout.write('Model verification cancellation passed on isolated assets\n');
  } catch (error) {
    process.stdout.write(JSON.stringify({ error: await page.locator('.error-banner').allTextContents(), status: await page.locator('.statusbar').allTextContents() }) + '\n');
    throw error;
  }
} finally { await application.close(); }
