param([switch]$RealQwen, [switch]$Headed)
$ErrorActionPreference = 'Stop'
$novaRoot = Split-Path -Parent $PSScriptRoot
$novaStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$novaOutput = Join-Path $novaRoot ".playwright/agentic-$novaStamp"
$novaEvidence = Join-Path $novaRoot "docs/evidence/agentic-2/reproduce-$novaStamp"
$novaPreviousTestBuild = $env:NOVA_TEST_BUILD
function Checked([scriptblock]$Action) { & $Action; if ($LASTEXITCODE -ne 0) { throw "La comprobación falló con código $LASTEXITCODE. Se conserva la evidencia en $novaEvidence" } }
Push-Location $novaRoot
try {
    # Existing dependencies only. No install, restore, model download, normal database or publish step.
    Checked { npm test --prefix apps/web }
    Checked { npm run build --prefix apps/web }
    Checked { dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore "-p:BaseOutputPath=$novaOutput/" }
    Checked { dotnet build backend/QuickPrompt.Api/QuickPrompt.Api.csproj --no-restore "-p:BaseOutputPath=$novaOutput/" }
    $env:NOVA_TEST_BUILD = Join-Path $novaOutput 'Debug/net8.0/QuickPrompt.Api.dll'
    Checked { node Tests/nova/agentic-2-faults.cjs }
    if ($RealQwen) {
        $novaTags = Invoke-RestMethod http://127.0.0.1:11434/api/tags -TimeoutSec 5
        if (-not ($novaTags.models | Where-Object name -eq 'qwen3:8b')) { throw 'Qwen3:8b debe estar instalado y Ollama activo. Este script no descarga modelos.' }
        Checked { dotnet build backend/QuickPrompt.Evaluation/QuickPrompt.Evaluation.csproj --no-restore "-p:BaseOutputPath=$novaOutput/" }
        $novaEvaluation = Join-Path $novaOutput 'Debug/net8.0/QuickPrompt.Evaluation.dll'
        foreach ($novaSplit in @('development', 'validation')) {
            Checked { dotnet $novaEvaluation --agentic Tests/evaluation/agentic-2-v2.json "$novaEvidence/$novaSplit.json" $novaSplit }
        }
        Checked { node scripts/Check-AgenticEvaluation.cjs $novaEvidence --v2 --pre-holdout }
        # Holdout runs only after the frozen development/validation gates, never to tune prompts.
        Checked { dotnet $novaEvaluation --agentic Tests/evaluation/agentic-2-v2.json "$novaEvidence/holdout.json" holdout }
        Checked { node scripts/Check-AgenticEvaluation.cjs $novaEvidence --v2 }
        if ($Headed) { Checked { node Tests/nova/agentic-2.cjs --headed --chain } }
        else { Checked { node Tests/nova/agentic-2.cjs --chain } }
    }
} finally {
    if ($novaPreviousTestBuild) { $env:NOVA_TEST_BUILD = $novaPreviousTestBuild }
    else { Remove-Item Env:NOVA_TEST_BUILD -ErrorAction SilentlyContinue }
    Pop-Location
}
