import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const app = path.resolve(import.meta.dirname, '..');
const payload = path.resolve(process.argv[2] || '');
if (!process.argv[2] || !existsSync(payload)) throw new Error('Pass an existing candidate payload');
const lock = JSON.parse(readFileSync(path.join(app, 'package-lock.json'), 'utf8'));
const notices = path.join(payload, 'licenses/npm');
mkdirSync(notices, { recursive: true });
const components = [];
for (const [relative, entry] of Object.entries(lock.packages).sort(([a], [b]) => a.localeCompare(b))) {
  if (!relative.startsWith('node_modules/')) continue;
  const directory = path.join(app, relative);
  if (!existsSync(directory)) continue; // Platform-specific optional dependency.
  const manifest = JSON.parse(readFileSync(path.join(directory, 'package.json'), 'utf8'));
  const name = manifest.name || relative.slice('node_modules/'.length);
  const version = manifest.version || entry.version;
  const files = [];
  for (const filename of readdirSync(directory).sort()) {
    if (!/^(licen[cs]e|copying|notice)([._-]|$)/i.test(filename)) continue;
    const source = path.join(directory, filename);
    if (!statSync(source).isFile()) continue;
    const target = path.join(notices, `${name.replaceAll(/[\\/]/g, '__')}@${version}`, filename);
    mkdirSync(path.dirname(target), { recursive: true });
    cpSync(source, target);
    files.push({ path: path.relative(payload, target).replaceAll('\\', '/'), sha256: createHash('sha256').update(readFileSync(target)).digest('hex') });
  }
  components.push({ name, version, declared_license: manifest.license || null, files });
}
writeFileSync(path.join(notices, 'components.json'), JSON.stringify({ scope: 'Conservative installed npm lockfile inventory, including build-only dependencies', components, missing_text: components.filter(component => !component.files.length).map(component => `${component.name}@${component.version}`) }, null, 2));
process.stdout.write(`Collected npm notices for ${components.length} installed packages; ${components.filter(component => !component.files.length).length} lack bundled text\n`);
