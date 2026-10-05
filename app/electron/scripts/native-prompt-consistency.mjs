import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { cp, mkdtemp, readFile, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
if (packagedIndex < 0) throw new Error('Use --packaged <exe>');
const executablePath = path.resolve(process.argv[packagedIndex + 1]);
const root = await mkdtemp(path.join(repo, '.tmp/electron/prompt-consistency-'));
await cp(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated prompt consistency test\n');
const changedAction = `Prompt reflects saved Review edit ${Date.now()}`;
const customTemplate = 'Custom prompt marker\n{{#moments}}{{action}}{{/moments}}';
const application = await electron.launch({ executablePath, cwd: path.dirname(executablePath), args: ['--data-root', root], timeout: 15_000 });
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor({ timeout: 15_000 });
  await page.getByRole('button', { name: 'More actions' }).click();
  await page.getByRole('button', { name: 'Show description details' }).click();
  await page.getByRole('textbox', { name: 'Action', exact: true }).fill(changedAction);
  const navigate = async name => {
    await page.getByRole('button', { name: 'Open navigation' }).click();
    await page.getByRole('button', { name, exact: true }).click();
  };
  await navigate('Get prompt');
  await page.getByRole('heading', { name: 'Get prompt' }).waitFor({ timeout: 12_000 });
  const rendered = await page.locator('.prompt-view pre').innerText();
  if (!rendered.includes(changedAction)) throw new Error('Rendered prompt omitted the saved Review edit');
  await navigate('Settings');
  await page.getByLabel('Template').fill(customTemplate);
  await page.getByRole('button', { name: 'Save format' }).click();
  await page.getByText('Prompt format saved').waitFor({ timeout: 8_000 });
  await navigate('Get prompt');
  const customPrompt = await page.locator('.prompt-view pre').innerText();
  if (!customPrompt.includes('Custom prompt marker') || !customPrompt.includes(changedAction)) throw new Error('Custom format did not render the saved edit');
  await navigate('Settings');
  if (await page.getByLabel('Template').inputValue() !== customTemplate) throw new Error('Saved prompt format was not restored');
  await page.getByRole('button', { name: 'Reset default' }).click();
  await page.getByText('Prompt format reset').waitFor({ timeout: 8_000 });
  await navigate('Get prompt');
  const resetPrompt = await page.locator('.prompt-view pre').innerText();
  if (resetPrompt.includes('Custom prompt marker') || !resetPrompt.includes(changedAction)) throw new Error('Default format did not restore the saved edit');
  const project = JSON.parse(await readFile(path.join(root, 'sessions', 'synthetic-settings', 'project.json'), 'utf8'));
  if (!project.steps.some(step => step.action === changedAction)) throw new Error('Prompt reflected an edit that was not durable');
  console.log(JSON.stringify({ prompt_consistency: 'passed', root, saved_review_edit: true, custom_template_and_reset: true }));
} finally { await application.close(); }
