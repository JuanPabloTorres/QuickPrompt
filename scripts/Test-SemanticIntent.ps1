param([switch]$RealQwen, [switch]$Headed, [switch]$Regressions)
$ErrorActionPreference = 'Stop'
$novaRoot = Split-Path -Parent $PSScriptRoot
$novaStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$novaOutput = Join-Path $novaRoot ".playwright/semantic-$novaStamp"
$novaEvidence = Join-Path $novaRoot "docs/evidence/semantic/acceptance-$novaStamp"
$novaPreviousBuild = $env:NOVA_TEST_BUILD
$novaPreviousEvidence = $env:NOVA_EVIDENCE
function Checked([scriptblock]$Action) { & $Action; if ($LASTEXITCODE -ne 0) { throw "Comprobación fallida ($LASTEXITCODE). Revisa la evidencia; no se declara aceptación." } }
Push-Location $novaRoot
try {
    # Installed toolchain only; no restore/install/download/publish/private data.
    Checked { dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false "-p:BaseOutputPath=$novaOutput/" -v quiet }
    Checked { dotnet build backend/QuickPrompt.Api/QuickPrompt.Api.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false "-p:BaseOutputPath=$novaOutput/" -v quiet }
    Checked { dotnet build backend/QuickPrompt.Evaluation/QuickPrompt.Evaluation.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false "-p:BaseOutputPath=$novaOutput/" -v quiet }
    Checked { node apps/web/node_modules/typescript/bin/tsc -p automation/browser-worker/tsconfig.json }
    Checked { npm test --prefix apps/web }
    Checked { npm run build --prefix apps/web }
    $env:NOVA_TEST_BUILD = Join-Path $novaOutput 'Debug/net8.0/QuickPrompt.Api.dll'
    Checked { node Tests/nova/visible-faults.cjs }
    Checked { node Tests/nova/agentic-2-faults.cjs }
    if ($Regressions) {
        Checked { dotnet test src/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false "-p:BaseOutputPath=$novaOutput/legacy/" -v quiet }
        Checked { node --test Tests/e2e/automation-contracts.cjs Tests/e2e/bridge-contracts.mjs }
    }
    if ($RealQwen) {
        $novaModels = Invoke-RestMethod http://127.0.0.1:11434/api/tags -TimeoutSec 5
        if (-not ($novaModels.models | Where-Object name -eq 'qwen3:8b')) { throw 'Se requiere Qwen3:8b ya instalado y activo; no se descargará.' }
        $novaEvaluator = Join-Path $novaOutput 'Debug/net8.0/QuickPrompt.Evaluation.dll'
        foreach ($novaSplit in @('development','validation')) {
            Checked { dotnet $novaEvaluator --semantic Tests/evaluation/semantic-v1.json "$novaEvidence/$novaSplit.json" $novaSplit }
            if ($novaSplit -eq 'development') { Checked { node scripts/Check-SemanticEvaluation.cjs $novaEvidence --development } }
            if ($novaSplit -eq 'validation') { Checked { node scripts/Check-SemanticEvaluation.cjs $novaEvidence --validation } }
        }
    }
    if ($Headed) {
        $env:NOVA_EVIDENCE = "$novaEvidence/headed"
        Checked { node Tests/nova/semantic-intent.cjs }
    }
    if ($RealQwen) {
        # With -Headed, functional browser evidence must pass before opening the holdout.
        Checked { dotnet $novaEvaluator --semantic Tests/evaluation/semantic-v1.json "$novaEvidence/holdout.json" holdout }
        Checked { node scripts/Check-SemanticEvaluation.cjs $novaEvidence }
    }
    if ($RealQwen -and $Headed) {
        Checked { node scripts/Summarize-SemanticValidation.cjs $novaEvidence "$novaEvidence/headed" }
    }
} finally {
    if ($novaPreviousBuild) { $env:NOVA_TEST_BUILD = $novaPreviousBuild } else { Remove-Item Env:NOVA_TEST_BUILD -ErrorAction SilentlyContinue }
    if ($novaPreviousEvidence) { $env:NOVA_EVIDENCE = $novaPreviousEvidence } else { Remove-Item Env:NOVA_EVIDENCE -ErrorAction SilentlyContinue }
    Pop-Location
}
