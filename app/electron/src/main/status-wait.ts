export function waitUnless(ready: () => boolean, arm: (wake: () => void) => void): Promise<void> {
  if (ready()) return Promise.resolve();
  return new Promise<void>(resolve => {
    arm(resolve);
    if (ready()) resolve();
  });
}
