param([int]$X = 1040, [int]$Y = 856)
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class WindowAtPoint {
  [StructLayout(LayoutKind.Sequential)] public struct Point { public int X; public int Y; }
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point point);
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr window, uint flags);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
'@
$point = [WindowAtPoint+Point]::new()
$point.X = $X; $point.Y = $Y
$window = [WindowAtPoint]::WindowFromPoint($point)
$root = [WindowAtPoint]::GetAncestor($window, 2)
$processId = [uint32]0
[void][WindowAtPoint]::GetWindowThreadProcessId($root, [ref]$processId)
@{ handle = $root.ToInt64(); pid = $processId; name = (Get-Process -Id $processId -ErrorAction SilentlyContinue).ProcessName } | ConvertTo-Json -Compress
