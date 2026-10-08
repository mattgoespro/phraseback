import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { readFile, stat } from 'node:fs/promises';
import { modelFixture } from './native-model-fixture.mjs';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const rootIndex = process.argv.indexOf('--root');
if (packagedIndex < 0 || rootIndex < 0) throw new Error('Use --packaged <exe> --root <isolated verified-model root>');
const executablePath = path.resolve(process.argv[packagedIndex + 1]);
const root = path.resolve(process.argv[rootIndex + 1]);
if (!root.startsWith(path.join(repo, '.tmp') + path.sep)) throw new Error('Model lifecycle requires isolated .tmp data');
const projectFile = path.join(root, 'sessions', 'synthetic-settings', 'project.json');
const original = await readFile(projectFile);
const sourceModel = path.join(modelFixture(repo), 'Qwen3-VL-2B-Instruct-Q8_0.gguf');
const sourceBytes = (await stat(sourceModel)).size;
const application = await electron.launch({ executablePath, cwd: path.dirname(executablePath), args: ['--data-root', root], timeout: 15_000 });
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor({ timeout: 15_000 });
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await page.getByRole('button', { name: 'Install / repair' }).click();
  await page.getByRole('button', { name: 'Cancel operation' }).waitFor({ timeout: 10_000 });
  await page.getByRole('button', { name: 'Cancel operation' }).waitFor({ state: 'hidden', timeout: 90_000 });
  if (await page.locator('.error-banner').count()) throw new Error(`Install/repair failed: ${await page.locator('.error-banner').innerText()}`);
  const installed = await page.locator('.setting-group').first().innerText();
  if (!installed.includes('Verified')) throw new Error(`Model not verified after repair: ${installed}`);
  page.once('dialog', dialog => dialog.accept());
  await page.getByRole('button', { name: 'Remove model files…' }).click();
  await page.getByText('Not installed').waitFor({ timeout: 30_000 });
  await page.getByRole('button', { name: 'Cancel operation' }).waitFor({ state: 'hidden', timeout: 30_000 });
  if (await page.locator('.error-banner').count()) throw new Error(`Model removal failed: ${await page.locator('.error-banner').innerText()}`);
  const removed = await page.locator('.setting-group').first().innerText();
  if (!removed.includes('Not installed')) throw new Error(`Model still present after removal: ${removed}`);
  if (!original.equals(await readFile(projectFile))) throw new Error('Model removal changed the recording');
  if ((await stat(sourceModel)).size !== sourceBytes) throw new Error('Shared source model asset changed');
  console.log(JSON.stringify({ model: 'cached_install_and_remove_passed', root, project_unchanged: true, source_asset_unchanged: true }));
} finally { await application.close(); }
