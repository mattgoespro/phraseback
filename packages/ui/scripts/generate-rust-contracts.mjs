import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import Ajv2020 from 'ajv/dist/2020.js';

// Same schema-driven Rust output as the retired tool; no C# toolchain required.
export async function generateRustContracts(engine, protocol, commands, check) {
  const ajv = new Ajv2020({ allErrors: true, strict: false });
  ajv.addSchema(commands);
  const validate = (shape, value) => {
    const validator = ajv.getSchema(`${commands.$id}#/$defs/${shape}`);
    if (!validator?.(value)) throw new Error(`Invalid ${shape} golden: ${ajv.errorsText(validator?.errors)}`);
  };
  const golden = JSON.parse(await readFile(path.join(engine, 'contracts/commands.golden.json'), 'utf8'));
  const covered = new Set();
  for (const example of golden.valid) {
    const method = commands['x-methods'][example.method];
    if (!method) throw new Error(`Unknown golden command: ${example.method}`);
    covered.add(example.method);
    validate(method.request, example.request);
    let shape = method.response;
    if (shape === 'MetadataPage') shape = example.request.kind === 'frames' ? 'FramePage' : 'StepPage';
    if (shape.startsWith('Option<')) {
      if (example.response === null) continue;
      shape = shape.slice(7, -1);
    }
    if (shape.startsWith('Vec<')) {
      if (!Array.isArray(example.response)) throw new Error('Expected golden response array');
      for (const value of example.response) validate(shape.slice(4, -1), value);
    } else validate(shape, example.response);
  }
  const methods = Object.keys(commands['x-methods']);
  if (methods.length !== covered.size || methods.some(method => !covered.has(method))) throw new Error('Missing command golden');
  const dispatch = await readFile(path.join(engine, 'crates/flow-engine/src/main.rs'), 'utf8');
  const implemented = new Set(['hello']);
  for (const arm of dispatch.matchAll(/^ {12}((?:"[a-z_]+"\s*\|\s*)*"[a-z_]+")\s*=>/gm))
    for (const name of arm[1].matchAll(/"([a-z_]+)"/g)) implemented.add(name[1]);
  if (implemented.size !== methods.length || methods.some(method => !implemented.has(method))) throw new Error('Engine command surface differs from schema');

  const envelopes = ['// Generated from contracts/protocol.schema.json. Do not edit by hand.', ''];
  for (const [name, shape] of Object.entries(protocol.$defs)) {
    envelopes.push('#[derive(Debug, Deserialize, Serialize)]', '#[serde(deny_unknown_fields)]', `pub struct ${name} {`);
    for (const [field, definition] of Object.entries(shape.properties)) envelopes.push(`    pub ${field}: ${definition['x-rust']},`);
    envelopes.push('}', '');
  }
  const payloads = ['// Generated from contracts/commands.schema.json. Do not edit by hand.', 'use serde::{Deserialize, Serialize};', 'use serde_json::Value;', ''];
  for (const [name, shape] of Object.entries(commands.$defs)) {
    payloads.push('#[derive(Debug, Deserialize, Serialize)]', '#[serde(deny_unknown_fields)]', `pub struct ${name} {`);
    const helpers = [];
    for (const [field, definition] of Object.entries(shape.properties)) {
      const type = definition['x-rust'];
      if (!shape.required.includes(field)) {
        let value = JSON.stringify(definition.default);
        if (type === 'String') value += '.into()';
        if (!['0', 'false', 'null'].includes(value)) {
          const helper = `default_${name.toLowerCase()}_${field}`;
          payloads.push(`    #[serde(default = "${helper}")]`);
          helpers.push(`fn ${helper}() -> ${type} {`, `    ${value}`, '}', '');
        } else payloads.push('    #[serde(default)]');
      } else if (type.startsWith('Option<')) payloads.push('    #[serde(deserialize_with = "required_nullable")]');
      payloads.push(`    pub ${field}: ${type},`);
    }
    payloads.push('}', '', ...helpers);
  }
  payloads.push("fn required_nullable<'de, D, T>(deserializer: D) -> Result<Option<T>, D::Error>", 'where', "    D: serde::Deserializer<'de>,", "    T: Deserialize<'de>,", '{', '    Option::<T>::deserialize(deserializer)', '}', '',
    'fn check<T: serde::de::DeserializeOwned>(value: &Value) -> Result<(), String> {', '    serde_json::from_value::<T>(value.clone())', '        .map(|_| ())', '        .map_err(|_| "Invalid command payload".into())', '}', '',
    '#[rustfmt::skip]', 'pub fn validate_request(method: &str, value: &Value) -> Result<(), String> {', '    match method {');
  for (const [method, definition] of Object.entries(commands['x-methods'])) payloads.push(`        "${method}" => check::<${definition.request}>(value),`);
  payloads.push('        _ => Err("Unknown command".into()),', '    }', '}', '', '#[rustfmt::skip]', 'pub fn validate_response(method: &str, request: &Value, value: &Value) -> Result<(), String> {', '    match method {');
  for (const [method, definition] of Object.entries(commands['x-methods'])) payloads.push(definition.response === 'MetadataPage'
    ? '        "metadata_page" => if request["kind"] == "frames" { check::<FramePage>(value) } else { check::<StepPage>(value) },'
    : `        "${method}" => check::<${definition.response}>(value),`);
  payloads.push('        _ => Err("Unknown command".into()),', '    }', '}', '');
  for (const [name, lines] of [['protocol', envelopes], ['commands', payloads]]) {
    const file = path.join(engine, `crates/flow-core/src/${name}_generated.rs`);
    const expected = lines.join('\n');
    if (check) {
      if ((await readFile(file, 'utf8')).replaceAll('\r\n', '\n') !== expected) throw new Error(`Rust ${name} contracts are stale. Run npm run generate:contracts.`);
    } else await writeFile(file, expected);
  }
}
