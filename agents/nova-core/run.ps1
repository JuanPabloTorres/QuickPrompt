param(
    [Parameter(Mandatory=$true)][string]$Goal,
    [string]$Config = "$PSScriptRoot/config.example.json",
    [string]$State = "$PSScriptRoot/.state/runs.sqlite",
    [string]$Python,
    [string]$ApplicationUrl
)
$ErrorActionPreference = 'Stop'
if (-not $Python) {
    $candidates = @(
        "$PSScriptRoot/../../.venv/Scripts/python.exe",
        "$env:USERPROFILE/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe"
    )
    $Python = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $Python) { $Python = (Get-Command python -ErrorAction Stop).Source }
$runnerArgs = @("$PSScriptRoot/run.py", '--config', $Config, '--goal', $Goal, '--state', $State)
if ($ApplicationUrl) { $runnerArgs += @('--application-url', $ApplicationUrl) }
& $Python @runnerArgs
exit $LASTEXITCODE
