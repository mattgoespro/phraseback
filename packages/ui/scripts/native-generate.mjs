import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { readFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const rootIndex = process.argv.indexOf('--root');
if (packagedIndex < 0 || rootIndex < 0) throw new Error('Use --packaged <exe> --root <isolated verified-model root>');
const executablePath = path.resolve(process.argv[packagedIndex + 1]);
const root = path.resolve(process.argv[rootIndex + 1]);
if (!root.startsWith(path.join(repo, '.tmp') + path.sep)) throw new Error('Generation requires isolated .tmp data');
const file = path.join(root, 'sessions', 'synthetic-settings', 'project.json');
const before = JSON.parse(await readFile(file, 'utf8'));
const application = await electron.launch({ executablePath, cwd: path.dirname(executablePath), args: ['--data-root', root], timeout: 15_000 });
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor({ timeout: 15_000 });
  await page.getByRole('button', { name: /Notice the validation message/ }).click();
  await page.getByRole('button', { name: 'More actions' }).click();
  await page.getByRole('button', { name: 'Regenerate descriptions…' }).click();
  await page.getByRole('dialog', { name: 'Regenerate descriptions' }).getByRole('button', { name: 'Selected moment' }).click();
  await page.getByRole('button', { name: 'Cancel operation' }).waitFor({ timeout: 10_000 });
  process.stdout.write('Selected-moment generation started on isolated verified assets\n');
  await page.getByRole('button', { name: 'Cancel operation' }).waitFor({ state: 'hidden', timeout: 120_000 });
  const errors = await page.locator('.error-banner').allTextContents();
  if (errors.length) throw new Error(`Generation error: ${errors.join(' | ')}`);
  const after = JSON.parse(await readFile(file, 'utf8'));
  const generated = after.steps.find(step => step.id === 'validation');
  if (!generated || !generated.action || !generated.result || generated.status !== 'generated') throw new Error(`Generated moment was not durably saved: ${JSON.stringify(generated)}`);
  for (const prior of before.steps.filter(step => step.manual)) {
    const current = after.steps.find(step => step.id === prior.id);
    if (current.action !== prior.action || current.result !== prior.result) throw new Error(`Manual description changed: ${prior.id}`);
  }
  console.log(JSON.stringify({ generation: 'saved', root, step_id: generated.id, action_chars: generated.action.length, result_chars: generated.result.length }));
} finally { await application.close(); }
