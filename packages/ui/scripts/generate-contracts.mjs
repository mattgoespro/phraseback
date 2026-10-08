import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { compile } from 'json-schema-to-typescript';
import { generateRustContracts } from './generate-rust-contracts.mjs';

const ui = path.resolve(import.meta.dirname, '..');
const engine = path.resolve(ui, '../engine');
const output = path.join(ui, 'src/shared/contracts.generated.ts');
const commands = JSON.parse(await readFile(path.join(engine, 'contracts/commands.schema.json'), 'utf8'));
const protocol = JSON.parse(await readFile(path.join(engine, 'contracts/protocol.schema.json'), 'utf8'));
await generateRustContracts(engine, protocol, commands, process.argv.includes('--check'));
const definitions = Object.keys(commands.$defs);
const commandsRoot = {
  $schema: commands.$schema,
  title: 'CommandDefinitions',
  type: 'object',
  properties: Object.fromEntries(definitions.map(name => [name, { $ref: `#/$defs/${name}` }])),
  $defs: commands.$defs
};
const options = { bannerComment: '', additionalProperties: false, style: { singleQuote: true } };
const generated = '// Generated from packages/engine/contracts/*.schema.json. Do not edit.\n'
  + await compile(protocol, 'ProtocolEnvelope', options)
  + '\n' + await compile(commandsRoot, 'CommandDefinitions', options);
if (process.argv.includes('--check')) {
  const current = await readFile(output, 'utf8').catch(() => '');
  if (current !== generated) throw new Error('TypeScript contracts are stale. Run npm run generate:contracts.');
} else await writeFile(output, generated);
