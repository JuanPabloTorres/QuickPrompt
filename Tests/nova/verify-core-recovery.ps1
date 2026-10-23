param(
    [Parameter(Mandatory=$true)][string]$Python,
    [Parameter(Mandatory=$true)][string]$SdkRoot,
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9-]+$')][string]$Candidate,
    [ValidateSet('development','validation','holdout')][string]$Split='development',
    [string[]]$DiagnosticCases=@(),
    [string]$DiagnosticDataset='',
    [ValidatePattern('^[a-z0-9-]+$')][string]$FrozenCandidate='',
    [string]$Events='',
    [switch]$PrepareOnly
)
$ErrorActionPreference='Stop'
if($PrepareOnly -and $FrozenCandidate){throw 'PrepareOnly creates a new snapshot; it cannot reuse a frozen candidate.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location -LiteralPath $repo
$base=Join-Path $repo ('.playwright/closure-lab-20261021/'+$Candidate)
$evidence=Join-Path $repo ('docs/evidence/closure-lab-20261021/'+$Candidate)
if((Test-Path -LiteralPath $base) -or (Test-Path -LiteralPath $evidence)){throw 'Preserve frozen candidates; choose a new candidate name.'}
foreach($file in @($Python,(Join-Path $SdkRoot 'adaptive_core/host.py'))) {if(-not(Test-Path -LiteralPath $file)){throw 'Configure existing Python/SDK.'}}
New-Item -ItemType Directory -Path (Join-Path $base 'sdk/adaptive_core'),$evidence -Force | Out-Null
$runtimeBase=$base;$runtimeManifest=$null;$compilation=$null
if($FrozenCandidate){
    $runtimeBase=Join-Path $repo ('.playwright/closure-lab-20261021/'+$FrozenCandidate)
    $manifestPath=Join-Path $repo ('docs/evidence/closure-lab-20261021/'+$FrozenCandidate+'/manifest.json')
    $frozen=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
    foreach($entry in $frozen.files){
        $path=[IO.Path]::GetFullPath((Join-Path $runtimeBase $entry.path))
        if(-not $path.StartsWith($runtimeBase+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256){throw 'Frozen runtime integrity mismatch; no inference started.'}
    }
    $sourceSnapshot=$frozen.sourceSnapshot;$files=$frozen.files
    $compilation=$frozen.compilation
    $runtimeManifest=@{path=$manifestPath;sha256=(Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash}
}else{
$sourceSnapshot=Join-Path $base 'source'
$sourceFiles=@(Get-ChildItem -LiteralPath (Join-Path $repo 'backend'),(Join-Path $repo 'src') -Recurse -File |
    Where-Object {$_.Extension -in @('.cs','.csproj') -and -not ($_.FullName.Split([IO.Path]::DirectorySeparatorChar) | Where-Object {$_ -in @('bin','obj')})})
$sourceHashes=@{}
foreach($file in $sourceFiles){
    $relative=[IO.Path]::GetRelativePath($repo,$file.FullName);$destination=Join-Path $sourceSnapshot $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
    $sourceHashes[$file.FullName]=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
}
# CallerFilePath points to this candidate's source, not a later edited checkout.
dotnet build backend/QuickPrompt.Evaluation --no-restore --no-incremental -c Release "-p:PathMap=$repo=$sourceSnapshot" --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Fresh evaluator compilation failed; no candidate can be accepted'}
dotnet publish backend/QuickPrompt.Evaluation --no-build --no-restore -c Release -o (Join-Path $base 'eval') "-p:PathMap=$repo=$sourceSnapshot" --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Evaluator publish failed'}
$compilation=@{mode='REBUILD_NO_INCREMENTAL';configuration='Release';publish='NO_BUILD';sourceIntegrityChecked=$true}
foreach($file in $sourceFiles){if((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $sourceHashes[$file.FullName]){throw 'Source changed while publishing; preserve this attempt and freeze another candidate.'}}
Get-ChildItem -LiteralPath (Join-Path $SdkRoot 'adaptive_core') -File | Where-Object {$_.Extension -eq '.py' -or $_.Name.EndsWith('.schema.json')} | Copy-Item -Destination (Join-Path $base 'sdk/adaptive_core')
Copy-Item -LiteralPath agents/nova-core/nova_worker.py -Destination $base
$files=@(Get-ChildItem -LiteralPath $base -Recurse -File | ForEach-Object {@{path=[IO.Path]::GetRelativePath($base,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
}
$dataset=Join-Path $repo "Tests/nova/datasets/recovery-v1/$Split.json"
if($DiagnosticDataset){
    $dataset=[IO.Path]::GetFullPath($DiagnosticDataset)
    $ownedRoot=[IO.Path]::GetFullPath((Join-Path $repo '.playwright/closure-lab-20261021'))+[IO.Path]::DirectorySeparatorChar
    if(-not $DiagnosticCases.Length -or -not $dataset.StartsWith($ownedRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'An explicit diagnostic case and owned synthetic dataset are required.'}
    if((Get-Content -LiteralPath $dataset -Raw|ConvertFrom-Json).syntheticOnly -ne $true){throw 'Synthetic diagnostic fixtures required.'}
}
@{candidate=$Candidate;createdUtc=[DateTime]::UtcNow.ToString('o');profile=$(if($PrepareOnly){'ACTION_WORKBENCH_RUNTIME_NO_INFERENCE'}else{'RETIRED_REQUEST_DECODER_DIAGNOSTIC_NO_SCOPE_ORACLE'});split=$Split;files=$files;
 runtimeBase=$runtimeBase;runtimeManifest=$runtimeManifest;sourceSnapshot=$sourceSnapshot;compilation=$compilation;datasetSha256=(Get-FileHash -LiteralPath $dataset -Algorithm SHA256).Hash;thresholdsSha256=(Get-FileHash -LiteralPath Tests/nova/datasets/recovery-v1/thresholds.json -Algorithm SHA256).Hash;deployment='not deployed'} |
 ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'manifest.json') -Encoding utf8
if($PrepareOnly){
    @{schema='ai-delivery-tests/1';status='PASS';count=1;skipped=0;profile=@{semanticCore='RUNTIME_PREPARATION_NO_INFERENCE'};
      rows=@(@{id='frozen-runtime';title='Candidato publicado y fuentes congeladas; sin inferencia';status='PASS';
        expected=@{sourceUnchangedDuringPublish=$true};observed=@{candidate=$Candidate;files=$files.Count;sourceSnapshot=$sourceSnapshot;
          manifestSha256=(Get-FileHash -LiteralPath (Join-Path $evidence 'manifest.json') -Algorithm SHA256).Hash}})} |
      ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'preparation.json') -Encoding utf8
    Write-Output 'CANDIDATE_FROZEN_NO_INFERENCE';exit 0
}
$env:NOVA_EVAL_RUNTIME='adaptive-core'
Remove-Item Env:NOVA_EVAL_LAB_ROOT -ErrorAction SilentlyContinue
$env:NOVA_CORE_PYTHON=$Python;$env:NOVA_CORE_SDK=Join-Path $runtimeBase 'sdk';$env:NOVA_CORE_WORKER=Join-Path $runtimeBase 'nova_worker.py'
$env:NOVA_EVAL_LAB_PUBLIC_INPUTS='1';$env:NOVA_EVAL_LAB_CAMPAIGN=$Candidate
if($Events){$env:NOVA_EVAL_LAB_EVENTS=$Events}else{Remove-Item Env:NOVA_EVAL_LAB_EVENTS -ErrorAction SilentlyContinue}
$caseIds=@($DiagnosticCases | ForEach-Object {$_.Split(',')})
$available=@((Get-Content -LiteralPath $dataset -Raw | ConvertFrom-Json).scenarios.id)
if($caseIds.Count -and ($caseIds | Where-Object {$_ -notin $available})){throw 'Unknown diagnostic case; no inference started.'}
if(@($caseIds | Select-Object -Unique).Count -ne $caseIds.Count){throw 'Duplicate diagnostic case; no inference started.'}
# Evaluation accepts one comma-separated filter argument. Passing an array as
# separate argv entries silently evaluated only its first case.
$caseArguments=@();if($caseIds.Count){$caseArguments=@($caseIds -join ',')}
$report=Join-Path $evidence "$Split.json"
dotnet (Join-Path $runtimeBase 'eval/QuickPrompt.Evaluation.dll') --recovery $dataset $report @caseArguments
$result=$LASTEXITCODE
if(Test-Path -LiteralPath $report){
    $expectedCount=if($caseIds.Count){$caseIds.Count}else{$available.Count}
    if((Get-Content -LiteralPath $report -Raw | ConvertFrom-Json).count -ne $expectedCount){throw 'Incomplete evaluation; preserve this attempt as failed.'}
}
exit $result
