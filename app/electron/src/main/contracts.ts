import Ajv2020 from 'ajv/dist/2020.js';
import schema from '../../../contracts/commands.schema.json';

type Method = { request: string; response: string };
const methods = schema['x-methods'] as Record<string, Method>;
const ajv = new Ajv2020({ allErrors: true, strict: false });
ajv.addSchema(schema);
const validators = new Map<string, ReturnType<typeof ajv.compile>>();

function validate(name: string, shape: string, value: unknown): void {
  let validator = validators.get(shape);
  if (!validator) {
    const definition = (schema.$defs as Record<string, unknown>)[shape];
    if (!definition) throw new Error(`Unknown ${name} contract: ${shape}`);
    validator = ajv.compile({ $ref: `${schema.$id}#/$defs/${shape}` });
    validators.set(shape, validator);
  }
  if (!validator(value)) throw new Error(`Invalid ${name}: ${ajv.errorsText(validator.errors)}`);
}

export function validateRequest(method: string, params: unknown): void {
  const entry = methods[method];
  if (!entry) throw new Error(`Unknown engine command: ${method}`);
  validate('request', entry.request, params);
}

export function validateResponse(method: string, result: unknown): void {
  const entry = methods[method];
  if (!entry) throw new Error(`Unknown engine command: ${method}`);
  if (entry.response.startsWith('Option<')) {
    if (result === null) return;
    return validate('response', entry.response.slice(7, -1), result);
  }
  if (entry.response.startsWith('Vec<')) {
    if (!Array.isArray(result)) throw new Error('Invalid response: expected array');
    for (const item of result) validate('response', entry.response.slice(4, -1), item);
    return;
  }
  // Worker results have extension points; their fixed identity is checked by the envelope.
  if (entry.response === 'MetadataPage') return;
  validate('response', entry.response, result);
}
