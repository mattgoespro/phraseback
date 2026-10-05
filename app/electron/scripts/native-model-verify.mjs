import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { cp, link, mkdir, mkdtemp, writeFile } from 'node:fs/promises';
import { modelFixture } from './native-model-fixture.mjs';

const repo = path.resolve(import.meta.dirname, '../../..');
const source = modelFixture(repo);
const root = await mkdtemp(path.join(repo, '.tmp/electron/model-verify-complete-'));
const model = path.join(root, 'model');
await mkdir(model);
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated model verification\n');
await cp(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
for (const name of ['Qwen3-VL-2B-Instruct-Q8_0.gguf', 'mmproj-Qwen3-VL-2B-Instruct-Q8_0.gguf']) await link(path.join(source, name), path.join(model, name));
await cp(path.join(source, 'runtime.zip'), path.join(model, 'runtime.zip'));
const packagedIndex = process.argv.indexOf('--packaged');
if (packagedIndex < 0) throw new Error('Use --packaged <exe>');
const executablePath = path.resolve(process.argv[packagedIndex + 1]);
const application = await electron.launch({ executablePath, cwd: path.dirname(executablePath), args: ['--data-root', root], timeout: 15_000 });
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor();
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await page.getByRole('button', { name: 'Verify existing files' }).click();
  await page.getByText(/Verified/).waitFor({ timeout: 90_000 });
  const status = await page.locator('.setting-group').first().innerText();
  if (!status.includes('Verified')) throw new Error(`Model did not reach verified state: ${status}`);
  console.log(JSON.stringify({ model: 'verified', root, status }));
} finally { await application.close(); }
