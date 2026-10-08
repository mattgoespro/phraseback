const { app, globalShortcut } = require('electron');
app.whenReady().then(() => {
  if (!globalShortcut.register('CommandOrControl+Shift+F9', () => {})) process.exit(2);
  process.stdout.write('HOTKEY_HELD\n');
  setInterval(() => {}, 1000);
});
app.on('will-quit', () => globalShortcut.unregisterAll());
