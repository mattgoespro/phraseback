import { describe, expect, it } from 'vitest';
import { waitUnless } from './status-wait';

describe('capture status wait', () => {
  it('resolves when the event arrives while the waiter is being armed', async () => {
    let ready = false;
    const pending = waitUnless(() => ready, () => { ready = true; });
    await Promise.race([
      pending,
      new Promise((_, reject) => setTimeout(() => reject(new Error('missed terminal capture status')), 50))
    ]);
    expect(ready).toBe(true);
  });
});
