param([switch]$RealQwen, [switch]$Regressions)
$ErrorActionPreference = 'Stop'
$novaRoot = Split-Path -Parent $PSScriptRoot
$novaStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$novaOutput = Join-Path $novaRoot ".playwright/visible-$novaStamp"
$novaPreviousBuild = $env:NOVA_TEST_BUILD
$novaPreviousEvidence = $env:NOVA_EVIDENCE
function Checked([scriptblock]$Action) { & $Action; if ($LASTEXITCODE -ne 0) { throw "Falló la comprobación con código $LASTEXITCODE. Las evidencias anteriores se conservan." } }
Push-Location $novaRoot
try {
    # Installed toolchain only. No npm install/ci, restore, model download, personal database or publication.
    Checked { node apps/web/node_modules/typescript/bin/tsc -p automation/browser-worker/tsconfig.json }
    Checked { npm test --prefix apps/web }
    Checked { npm run build --prefix apps/web }
    Checked { dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore "-p:BaseOutputPath=$novaOutput/" }
    Checked { dotnet build backend/QuickPrompt.Api/QuickPrompt.Api.csproj --no-restore "-p:BaseOutputPath=$novaOutput/" }
    $env:NOVA_TEST_BUILD = Join-Path $novaOutput 'Debug/net8.0/QuickPrompt.Api.dll'
    Checked { node Tests/nova/visible-faults.cjs }
    if ($RealQwen) {
        $novaModels = Invoke-RestMethod http://127.0.0.1:11434/api/tags -TimeoutSec 5
        if (-not ($novaModels.models | Where-Object name -eq 'qwen3:8b')) { throw 'Activa Ollama con Qwen3:8b ya instalado. Este script no descarga modelos.' }
        # This acceptance always launches actual headed Edge in an interactive desktop session.
        Checked { node Tests/nova/visible-agent.cjs }
        Checked { node Tests/nova/visible-agent.cjs --manual-only }
    }
    if ($Regressions) {
        Checked { dotnet test src/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore "-p:BaseOutputPath=$novaOutput/legacy/" }
        Checked { node --test Tests/e2e/automation-contracts.cjs }
        Checked { node --test Tests/e2e/bridge-contracts.mjs }
        Checked { node Tests/nova/agentic-2-faults.cjs }
        if ($RealQwen) {
            $env:NOVA_EVIDENCE = "docs/evidence/goal08/regression-commands-$novaStamp"
            Checked { node Tests/nova/local-commands.cjs --headed }
            Checked { node Tests/nova/agentic-2.cjs --headed --chain }
        }
        $env:NOVA_EVIDENCE = "docs/evidence/goal08/regression-modal-$novaStamp"
        Checked { node Tests/nova/preparation-modal.cjs }
        $env:NOVA_EVIDENCE = "docs/evidence/goal08/regression-learning-$novaStamp"
        Checked { node Tests/nova/learning.cjs }
    }
} finally {
    if ($novaPreviousBuild) { $env:NOVA_TEST_BUILD = $novaPreviousBuild } else { Remove-Item Env:NOVA_TEST_BUILD -ErrorAction SilentlyContinue }
    if ($novaPreviousEvidence) { $env:NOVA_EVIDENCE = $novaPreviousEvidence } else { Remove-Item Env:NOVA_EVIDENCE -ErrorAction SilentlyContinue }
    Pop-Location
}
