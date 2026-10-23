param(
    [Parameter(Mandatory=$true)][string]$Goal,
    [Parameter(Mandatory=$true)][string]$LabRoot,
    [Parameter(Mandatory=$true)][string]$Python,
    [Parameter(Mandatory=$true)][string]$Probe,
    [Parameter(Mandatory=$true)][string]$Output,
    [Parameter(Mandatory=$true)][string[]]$Reports,
    [switch]$CaptureLive
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location -LiteralPath $repo
$reference=Join-Path $LabRoot 'artifacts/closure-20261021/candidate-reference.json'
$candidate=(Get-Content -LiteralPath $reference -Raw|ConvertFrom-Json).candidate
if($candidate -notmatch '^[a-z0-9-]+$'){throw 'Invalid candidate identity'}
$captureLease=$null
try {
    if($CaptureLive){
        # Preserve foreign diagnostics: fail on a busy lease, never stop them.
        $captureLease=[IO.File]::Open((Join-Path ([IO.Path]::GetTempPath()) 'QuickPrompt.Nova.SemanticModel.lease'),
            [IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        $env:NOVA_SEMANTIC_CAPTURE_EXCLUSIVE_LEASE='1'
    }
    $parameters=@('-X','utf8','-B',(Join-Path $PSScriptRoot 'audit-semantic-boundary.py'),
        '--goal',$Goal,'--reference',$reference,'--frozen',(Join-Path $repo ('.playwright/closure-lab-20261021/'+$candidate)),
        '--dataset',(Join-Path $PSScriptRoot 'datasets/recovery-v1/validation.json'),'--probe',$Probe,
        '--journal',(Join-Path $LabRoot 'artifacts/visual/events.jsonl'),'--output',$Output)
    if($CaptureLive){$parameters+='--capture-live'}
    $parameters+='--reports';$parameters+=$Reports
    & $Python @parameters
    $auditExit=$LASTEXITCODE
    if($auditExit -ne 0){throw 'Boundary audit incomplete; preserve receipts'}
    # A completed diagnosis is not a product PASS. Keep this distinction in CLI
    # status too; the report contains syntax and user-effect results separately.
    $report=Get-Content -LiteralPath (Join-Path $Output 'audit.json') -Raw|ConvertFrom-Json
    if($report.status -ne 'PASS'){exit 1}
} finally {
    Remove-Item Env:NOVA_SEMANTIC_CAPTURE_EXCLUSIVE_LEASE -ErrorAction SilentlyContinue
    if($captureLease){$captureLease.Dispose()}
}
