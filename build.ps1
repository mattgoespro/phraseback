param(
    [ValidateSet('Build','Check','Test','Prototype','Release')][string]$Task = 'Build',
    [ValidateSet('Electron')][string]$Ui = 'Electron',
    [string]$Candidate = '',
    [string]$AcceptanceFile = ''
)
$ErrorActionPreference = 'Stop'
$app = Join-Path $PSScriptRoot 'packages/ui'
if ($Task -eq 'Release') {
    if (-not $Candidate -or -not $AcceptanceFile) { throw 'Release needs a validated Electron candidate and its acceptance file.' }
    & (Join-Path $PSScriptRoot 'scripts/electron_release.ps1') -Candidate $Candidate -AcceptanceFile $AcceptanceFile
    return
}

& (Join-Path $PSScriptRoot 'scripts/electron_engine.ps1') -Task $Task
Push-Location $app
try {
    & npm run check
    if ($LASTEXITCODE -ne 0) { throw 'Electron check failed' }
    if ($Task -eq 'Test') {
        $previousTestEngine = [Environment]::GetEnvironmentVariable('PHRASEBACK_TEST_ENGINE')
        try {
            $env:PHRASEBACK_TEST_ENGINE = Join-Path $PSScriptRoot '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe'
            & npm test
            if ($LASTEXITCODE -ne 0) { throw 'Electron tests failed' }
        } finally { [Environment]::SetEnvironmentVariable('PHRASEBACK_TEST_ENGINE', $previousTestEngine) }
    }
    if ($Task -eq 'Build' -or $Task -eq 'Prototype') {
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Electron build failed' }
    }
} finally { Pop-Location }

if ($Task -eq 'Prototype') {
    $fixture = Join-Path $PSScriptRoot '.tmp/electron/prototype-data'
    $engine = Join-Path $PSScriptRoot '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe'
    $prototype = Start-Process -FilePath (Join-Path $app 'node_modules/electron/dist/electron.exe') -ArgumentList @('.', '--data-root', ('"' + $fixture + '"'), '--engine', ('"' + $engine + '"')) -WorkingDirectory $app -PassThru
    Write-Output "Electron prototype started on isolated data (PID $($prototype.Id)): $fixture"
}
