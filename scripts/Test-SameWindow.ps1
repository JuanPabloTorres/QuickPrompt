param([string[]]$Cases = @('Q','A','B','C','C2','D','E','F','P','L','R','N','S'), [switch]$SkipChecks, [switch]$HeadlessQa)
$ErrorActionPreference = 'Stop'
$novaRoot = Split-Path -Parent $PSScriptRoot
$novaBuild = Join-Path $novaRoot '.playwright/same-tab'
$novaOldCases = $env:NOVA_SAME_TAB_CASES
$novaOldHeadless = $env:NOVA_QA_HEADLESS
function Checked([scriptblock]$Action) { & $Action; if ($LASTEXITCODE -ne 0) { throw 'Falló una comprobación; no se declara aceptación.' } }
Push-Location $novaRoot
try {
    if (-not $SkipChecks) {
        Checked { dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false "-p:BaseOutputPath=$novaBuild/tests/" -v quiet }
        Checked { dotnet test src/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false "-p:BaseOutputPath=$novaBuild/legacy/" -v quiet }
        Checked { npm.cmd test --prefix apps/web }
    }
    Checked { npm.cmd run build --prefix apps/web }
    Checked { dotnet build backend/QuickPrompt.Api/QuickPrompt.Api.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false "-p:BaseOutputPath=$novaBuild/" -v quiet }
    $env:NOVA_SAME_TAB_CASES = $Cases -join ','
    if ($HeadlessQa) { $env:NOVA_QA_HEADLESS = '1' }
    Checked { node Tests/nova/same-window.cjs }
} finally { $env:NOVA_SAME_TAB_CASES = $novaOldCases; $env:NOVA_QA_HEADLESS = $novaOldHeadless; Pop-Location }
