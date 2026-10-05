import { _electron as electron } from 'playwright-core';
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { cp, mkdtemp, writeFile } from 'node:fs/promises';

const repo = path.resolve(import.meta.dirname, '../../..');
const packagedIndex = process.argv.indexOf('--packaged');
const packaged = packagedIndex < 0 ? null : path.resolve(process.argv[packagedIndex + 1]);
const root = await mkdtemp(path.join(repo, '.tmp/electron/reconnect-'));
await cp(path.join(repo, '.tmp/electron/prototype-data/sessions'), path.join(root, 'sessions'), { recursive: true });
await writeFile(path.join(root, '.flow-recorder-development'), 'isolated reconnect test\n');
const application = await electron.launch({
  executablePath: packaged || path.join(repo, 'app/electron/node_modules/electron/dist/electron.exe'),
  cwd: packaged ? path.dirname(packaged) : path.join(repo, 'app/electron'),
  args: packaged ? ['--data-root', root] : ['.', '--data-root', root, '--engine', path.join(repo, '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe')],
  timeout: 15_000
});
try {
  const page = await application.firstWindow();
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor();
  await page.setViewportSize({ width: 1024, height: 700 });
  const script = `$p = @(Get-CimInstance Win32_Process -Filter "Name = 'Phraseback.Engine.exe'" | Where-Object { $_.CommandLine -like '*${root}*' }); if ($p.Count -ne 1) { throw "Expected one isolated engine child; found $($p.Count)" }; Stop-Process -Id $p[0].ProcessId -Force`;
  execFileSync('powershell.exe', ['-NoProfile', '-Command', script], { stdio: 'pipe', timeout: 8_000 });
  await page.getByRole('button', { name: 'Reconnect', exact: true }).waitFor({ timeout: 8_000 });
  await page.screenshot({ path: path.join(repo, '.tmp/electron/layout-error-1024x700.png') });
  if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)) throw new Error('Disconnected state overflows horizontally');
  await page.getByRole('button', { name: 'Reconnect', exact: true }).click();
  await page.getByText('Reconnected').waitFor({ timeout: 8_000 });
  await page.getByRole('heading', { name: 'A calmer workspace' }).waitFor();
  process.stdout.write('Engine child reconnect restored Review on isolated data\n');
} finally { await application.close(); }
