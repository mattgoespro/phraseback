import { _electron as electron } from 'playwright-core';
import path from 'node:path';
import { cp, mkdtemp, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const root = await mkdtemp(path.join(repo, '.tmp/electron/layout-'));
await cp(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated layout test\n');
const application = await electron.launch({
  executablePath: packaged || path.join(repo, 'app/electron/node_modules/electron/dist/electron.exe'),
  cwd: packaged ? path.dirname(packaged) : path.join(repo, 'app/electron'),
  args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe')],
  timeout: 15_000
});
try {
  const page = await application.firstWindow({ timeout: 10_000 });
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor({ timeout: 10_000 });
  async function captureLayout(name, width, height) {
    await page.screenshot({ path: path.join(repo, `.tmp/electron/layout-${name}-${width}x${height}.png`) });
    const overflowing = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth);
    if (overflowing) throw new Error(`Horizontal overflow in ${name} at ${width}x${height}`);
  }
  async function captureMenu(name, trigger, selector, width, height) {
    await trigger.click();
    const popover = page.locator(selector);
    await popover.waitFor();
    const box = await popover.boundingBox();
    if (!box || box.x < 0 || box.y < 0 || box.x + box.width > width || box.y + box.height > height) {
      throw new Error(`${name} menu escapes ${width}x${height}`);
    }
    await captureLayout(name, width, height);
    await page.keyboard.press('Escape');
  }
  async function navigate(name) {
    await page.getByRole('button', { name: 'Open navigation' }).click();
    await page.locator('.nav-popover').getByRole('button', { name, exact: true }).click();
  }
  for (const [width, height] of [[1280, 800], [1024, 700]]) {
    await page.setViewportSize({ width, height });
    await page.reload();
    await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor();
    await captureLayout('review', width, height);
    await captureMenu('recording-menu', page.locator('.recording-crumb'), '.recording-popover', width, height);
    await captureMenu('actions-menu', page.getByRole('button', { name: 'More actions' }), '.more-popover', width, height);
    await captureMenu('navigation-menu', page.getByRole('button', { name: 'Open navigation' }), '.nav-popover', width, height);
    await page.getByRole('button', { name: 'Open navigation' }).focus();
    await page.keyboard.press('Tab');
    const focus = await page.evaluate(() => ({ label: document.activeElement?.textContent?.trim(),
      visible: document.activeElement?.matches(':focus-visible'), outline: getComputedStyle(document.activeElement).outlineStyle }));
    if (focus.label !== 'Library' || !focus.visible || focus.outline !== 'solid') throw new Error(`Keyboard focus is not visible: ${JSON.stringify(focus)}`);
    await captureLayout('keyboard-focus', width, height);
    await navigate('Library');
    await page.getByRole('heading', { name: 'Library' }).waitFor();
    await captureLayout('library', width, height);
    await navigate('New recording');
    await page.getByRole('heading', { name: 'New recording' }).waitFor();
    await captureLayout('capture', width, height);
    await navigate('Settings');
    await page.getByRole('heading', { name: 'Settings' }).waitFor();
    await captureLayout('settings', width, height);
    await navigate('Get prompt');
    await page.getByRole('heading', { name: 'Get prompt' }).waitFor();
    await captureLayout('prompt', width, height);
  }
  process.stdout.write('Review, Library, capture, Settings, and prompt rendered at both sizes without horizontal overflow\n');
} finally {
  await application.close();
}

const emptyRoot = await mkdtemp(path.join(repo, '.tmp/electron/layout-empty-'));
await writeFile(path.join(emptyRoot, '.flow-recorder-development'), 'isolated empty-state test\n');
const emptyApp = await electron.launch({
  executablePath: packaged || path.join(repo, 'app/electron/node_modules/electron/dist/electron.exe'),
  cwd: packaged ? path.dirname(packaged) : path.join(repo, 'app/electron'),
  args: packaged ? ['--data-root', emptyRoot] : ['.', '--data-root', emptyRoot, '--engine', path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe')],
  timeout: 15_000
});
try {
  const page = await emptyApp.firstWindow({ timeout: 10_000 });
  await page.getByText('No recording open').waitFor({ timeout: 10_000 });
  for (const [width, height] of [[1280, 800], [1024, 700]]) {
    await page.setViewportSize({ width, height });
    await page.reload();
    await page.getByText('No recording open').waitFor();
    await page.screenshot({ path: path.join(repo, `.tmp/electron/layout-empty-review-${width}x${height}.png`) });
    await page.locator('.topbar .crumb').first().click();
    await page.getByRole('heading', { name: 'Library' }).waitFor();
    if (await page.locator('.library-list button').count()) throw new Error('Empty library unexpectedly has recordings');
    await page.screenshot({ path: path.join(repo, `.tmp/electron/layout-empty-library-${width}x${height}.png`) });
    if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)) throw new Error('Empty library overflows horizontally');
  }
} finally {
  await emptyApp.close();
}
