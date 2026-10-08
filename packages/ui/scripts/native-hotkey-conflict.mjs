import { _electron as electron } from 'playwright-core';
import { spawn } from 'node:child_process';
import path from 'node:path';
import { mkdtemp, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packaged = path.resolve(process.argv[2] || '');
if (!process.argv[2]) throw new Error('Pass the packaged Phraseback.exe');
const root = await mkdtemp(path.join(repo, '.tmp/electron/hotkey-conflict-'));
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated shortcut conflict test\n');
const helper = spawn(path.join(repo, 'packages/ui/node_modules/electron/dist/electron.exe'), [path.join(import.meta.dirname, 'hold-hotkey.cjs')], {
  cwd: path.join(repo, 'packages/ui'), windowsHide: true, stdio: ['ignore', 'pipe', 'pipe']
});
helper.stderr.resume();
let application;
try {
  await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Hotkey holder did not start')), 8_000);
    helper.stdout.on('data', chunk => {
      if (String(chunk).includes('HOTKEY_HELD')) { clearTimeout(timer); resolve(); }
    });
    helper.on('exit', code => { clearTimeout(timer); reject(new Error(`Hotkey holder exited: ${code}`)); });
  });
  application = await electron.launch({ executablePath: packaged, cwd: path.dirname(packaged), args: ['--data-root', root], timeout: 10_000 });
  const page = await application.firstWindow();
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await page.locator('.nav-popover').getByRole('button', { name: 'New recording' }).click();
  await page.getByRole('button', { name: 'Start recording' }).click();
  await page.getByText('Ctrl+Shift+F9 is unavailable', { exact: false }).waitFor({ timeout: 8_000 });
  process.stdout.write('Packaged app rejected capture while the stop hotkey was unavailable\n');
} finally {
  if (application) await application.close();
  helper.kill();
}
