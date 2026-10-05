import { describe, expect, it } from 'vitest';
import { validateRequest, validateResponse } from './contracts';

describe('shared command contracts', () => {
  it('accepts a valid request and rejects unknown or malformed commands', () => {
    expect(() => validateRequest('hello', { notifications: true })).not.toThrow();
    expect(() => validateRequest('edit_step', { recording_id: 'x' })).toThrow();
    expect(() => validateRequest('not_a_command', {})).toThrow();
  });

  it('validates required response fields', () => {
    expect(() => validateResponse('hello', { engine_version: 'x', protocol_minor: 2, notifications: true })).not.toThrow();
    expect(() => validateResponse('hello', { protocol_minor: 2 })).toThrow();
  });
});
