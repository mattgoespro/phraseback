param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Engine,
    [ValidateSet('qwen2-cpu','qwen2-gpu')][string]$Preset = 'qwen2-cpu'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$enginePath = (Resolve-Path -LiteralPath $Engine).Path
$dotnet = Join-Path $env:LOCALAPPDATA 'FlowRecorderDev/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$root = Join-Path $repo ('.tmp/rebuild/model-check-' + [Guid]::NewGuid().ToString('N'))
$settings = @{
    FLOW_REAL_MODEL='1'; FLOW_REAL_MODEL_REPOSITORY=$repo; FLOW_REAL_MODEL_SOURCE=$sourcePath
    FLOW_REAL_MODEL_OUTPUT=(Join-Path $root 'data'); FLOW_REAL_MODEL_PRESET=$Preset; FLOW_REBUILD_ENGINE=$enginePath
}
$previous = @{}
foreach ($name in $settings.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $settings[$name], 'Process')
}
try {
    & $dotnet test (Join-Path $repo 'app/shell/Phraseback.Tests/Phraseback.Tests.csproj') -p:RestoreLockedMode=true --filter FullyQualifiedName~LocalModelGeneratesThenManualEditSurvivesReopen --blame-hang-timeout 17m --logger trx --results-directory (Join-Path $root 'test-reports')
    if ($LASTEXITCODE -ne 0) { throw "Model check failed; evidence retained: $root" }
    Write-Output "Model check passed; evidence: $root"
}
finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
