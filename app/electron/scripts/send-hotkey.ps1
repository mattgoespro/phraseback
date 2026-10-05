$signature = @'
using System;
using System.Runtime.InteropServices;
public static class PhrasebackTestKeys {
  [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
'@
Add-Type -TypeDefinition $signature
[PhrasebackTestKeys]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
[PhrasebackTestKeys]::keybd_event(0x10, 0, 0, [UIntPtr]::Zero)
[PhrasebackTestKeys]::keybd_event(0x78, 0, 0, [UIntPtr]::Zero)
[PhrasebackTestKeys]::keybd_event(0x78, 0, 2, [UIntPtr]::Zero)
[PhrasebackTestKeys]::keybd_event(0x10, 0, 2, [UIntPtr]::Zero)
[PhrasebackTestKeys]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
