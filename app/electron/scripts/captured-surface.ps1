param([Parameter(Mandatory = $true)][string]$Path, [switch]$CheckCenter)
Add-Type -AssemblyName System.Drawing
$image = [System.Drawing.Bitmap]::new($Path)
try { $marker = $image.GetPixel(10, 10) }
finally { $image.Dispose() }
if ($marker.R -lt 250 -or $marker.G -gt 5 -or $marker.B -lt 250) {
    throw 'Captured frame does not contain the synthetic surface marker'
}
if ($CheckCenter) {
    $image = [System.Drawing.Bitmap]::new($Path)
    try { $center = $image.GetPixel(1300, 1070) }
    finally { $image.Dispose() }
    if ($center.R -gt 5 -or $center.G -lt 250 -or $center.B -lt 250) {
        throw "Capture exclusion failed to reveal the covered synthetic marker: $($center.R),$($center.G),$($center.B)"
    }
}
