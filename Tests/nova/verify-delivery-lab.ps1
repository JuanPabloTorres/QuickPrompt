param(
    [Parameter(Mandatory=$true)][string]$Python,
    [Parameter(Mandatory=$true)][string]$SdkRoot,
    [Parameter(Mandatory=$true)][string]$LabRoot,
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9-]+$')][string]$Candidate,
    [string]$Dataset='Tests/nova/datasets/recovery-v1/development.json',
    [string]$DiagnosticCase=''
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location -LiteralPath $repo
$base=Join-Path $repo ('.playwright/skill-audit-20261021/'+$Candidate)
$evidence=Join-Path $repo ('docs/evidence/skill-audit-20261021/'+$Candidate)
if((Test-Path -LiteralPath $base) -or (Test-Path -LiteralPath $evidence)){throw 'Preserve frozen candidates; use a new candidate name'}
foreach($item in @($Python,(Join-Path $SdkRoot 'adaptive_core/inference.py'),(Join-Path $LabRoot 'ai_delivery_lab/core_bridge.py'))){
    if(-not(Test-Path -LiteralPath $item -PathType Leaf)){throw 'Configure the existing Python, SDK and Lab explicitly'}
}
New-Item -ItemType Directory -Path (Join-Path $base 'sdk/adaptive_core'),(Join-Path $base 'lab/ai_delivery_lab'),$evidence -Force | Out-Null
dotnet publish backend/QuickPrompt.Evaluation/QuickPrompt.Evaluation.csproj --no-restore -o (Join-Path $base 'eval') --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Evaluator publish failed'}
Get-ChildItem -LiteralPath (Join-Path $SdkRoot 'adaptive_core') -File | Where-Object {$_.Extension -eq '.py' -or $_.Name.EndsWith('.schema.json')} | Copy-Item -Destination (Join-Path $base 'sdk/adaptive_core')
Get-ChildItem -LiteralPath (Join-Path $LabRoot 'ai_delivery_lab') -File -Filter '*.py' | Copy-Item -Destination (Join-Path $base 'lab/ai_delivery_lab')
Copy-Item -LiteralPath agents/nova-core/nova_worker.py -Destination $base
$files=@(Get-ChildItem -LiteralPath $base -Recurse -File | ForEach-Object {@{path=[IO.Path]::GetRelativePath($base,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
@{candidate=$Candidate;createdUtc=[DateTime]::UtcNow.ToString('o');profile='QA_ORACLE_ASSISTED_NOT_BLIND_QUALIFICATION';files=$files;
    datasetSha256=(Get-FileHash -LiteralPath $Dataset -Algorithm SHA256).Hash;
    thresholdsSha256=(Get-FileHash -LiteralPath Tests/nova/datasets/recovery-v1/thresholds.json -Algorithm SHA256).Hash;
    deployment='not deployed'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'manifest.json') -Encoding utf8
$env:NOVA_EVAL_RUNTIME='adaptive-core'
$env:NOVA_EVAL_LAB_ROOT=Join-Path $base 'lab'
$env:NOVA_CORE_PYTHON=$Python
$env:NOVA_CORE_SDK=Join-Path $base 'sdk'
$env:NOVA_CORE_WORKER=Join-Path $base 'nova_worker.py'
if($DiagnosticCase){
    dotnet (Join-Path $base 'eval/QuickPrompt.Evaluation.dll') --recovery $Dataset (Join-Path $evidence 'assisted-development.json') $DiagnosticCase
}else{
    dotnet (Join-Path $base 'eval/QuickPrompt.Evaluation.dll') --recovery $Dataset (Join-Path $evidence 'assisted-development.json')
}
$runnerExit=$LASTEXITCODE
$report=Get-Content -Raw -LiteralPath (Join-Path $evidence 'assisted-development.json') | ConvertFrom-Json
$passed=@($report.rows | Where-Object {$_.correct -and $_.effectScopePass}).Count
$expectedCount=@((Get-Content -Raw -LiteralPath $Dataset | ConvertFrom-Json).scenarios).Count
@{profile=$report.profile.semanticCore;passed=$passed;count=$report.count;fixtureUnchanged=$report.fixtureUnchanged;
    runnerExit=$runnerExit;acceptanceComplete=$false;blindSemanticQualification='NOT_RUN';uiExecution='NOT_RUN';
    status=$(if($passed -eq $expectedCount -and $report.fixtureUnchanged){'ASSISTED_SLICE_PASS'}else{'ASSISTED_SLICE_FAIL'})} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'summary.json') -Encoding utf8
if($runnerExit -ne 0 -or $passed -ne $expectedCount -or -not $report.fixtureUnchanged){exit 1}
