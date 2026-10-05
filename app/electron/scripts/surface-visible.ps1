Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SurfacePixel {
  [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
  [DllImport("gdi32.dll")] public static extern uint GetPixel(IntPtr dc, int x, int y);
}
'@
$dc = [SurfacePixel]::GetDC([IntPtr]::Zero)
try { $pixel = [SurfacePixel]::GetPixel($dc, 10, 10) }
finally { [void][SurfacePixel]::ReleaseDC([IntPtr]::Zero, $dc) }
if ($pixel -ne 0x00ff00ff) { throw ('Synthetic surface is not visible; desktop pixel was 0x{0:x8}' -f $pixel) }
