#requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReviewedPlan,[string]$ReceiptPath)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$executor=Join-Path $repository 'deploy/cloud/Invoke-PublicPostgresTransfer.ps1'
Import-Module (Join-Path $repository 'deploy/cloud/PublicPostgresNative.psm1') -Force
Import-Module (Join-Path $repository 'deploy/cloud/PublicPostgres.psm1') -Force
$root=New-TransferPrivateDirectory (Join-Path ([IO.Path]::GetTempPath()) ('qp-transfer-contracts-'+[Guid]::NewGuid().ToString('N'))) $repository
$checks=[Collections.Generic.List[string]]::new()
$planPath=Join-Path $root 'review.json'
$original=[IO.File]::ReadAllText($ReviewedPlan)
function Save-FixturePlan([scriptblock]$Change){
    $fixture=$original|ConvertFrom-Json -AsHashtable
    # Even a broken test may never reach a real credential or database.
    $fixture.source.operatorFile=Join-Path $root 'nonexistent-source.json'
    $fixture.target.operatorFile=Join-Path $root 'nonexistent-target.json'
    if($Change){& $Change $fixture}
    [IO.File]::WriteAllText($planPath,($fixture|ConvertTo-Json -Depth 30))
}
function Assert-Rejected([string]$Label,[scriptblock]$Action,[string]$Expected){
    $observed=$null
    try{& $Action|Out-Null}catch{$observed=$_.Exception.Message}
    if(-not $observed -or $observed -cnotlike ($Expected+'*')){throw ('Transfer guard failed: '+$Label)}
    $checks.Add($Label)
}
Save-FixturePlan
$review=& $executor -ReviewedPlan $planPath -ReviewOnly|ConvertFrom-Json
if($review.verdict -cne 'REVIEW_ONLY' -or $review.credentialReads -ne 0 -or $review.databaseConnections -ne 0 -or $review.databaseWrites -ne 0){throw 'Review crossed its observational boundary.'}
$checks.Add('ReviewOnly accepts pinned inputs without opening nonexistent credentials or databases')
Assert-Rejected 'No execution without exact approval hash' {& $executor -ReviewedPlan $planPath -Execute} 'Execution requires'
$hash=(Get-FileHash -LiteralPath $planPath).Hash.ToLowerInvariant()
Assert-Rejected 'No execution without observed service suspension' {& $executor -ReviewedPlan $planPath -Execute -ApprovedPlanSha256 $hash} 'Execution requires'
Assert-Rejected 'Reject simultaneous review and execution' {& $executor -ReviewedPlan $planPath -ReviewOnly -Execute} 'Select exactly'
Save-FixturePlan {param($p) $p.target.database='other_database'}
Assert-Rejected 'Reject changed database scope' {& $executor -ReviewedPlan $planPath -ReviewOnly} 'Reviewed plan scope differs'
Save-FixturePlan {param($p) $p.inputs[0].sha256='0'*64}
Assert-Rejected 'Reject changed implementation hash before load' {& $executor -ReviewedPlan $planPath -ReviewOnly} 'Reviewed implementation changed'
Save-FixturePlan {param($p) $p.trust.target.sha256='0'*64}
Assert-Rejected 'Reject changed pinned TLS trust input' {& $executor -ReviewedPlan $planPath -ReviewOnly} 'Reviewed native or TLS/migration input changed'
Save-FixturePlan {param($p) $p.native.directory=Join-Path $root 'not-the-runtime'}
Assert-Rejected 'Reject executable outside reviewed native inventory' {& $executor -ReviewedPlan $planPath -ReviewOnly} 'Native executable is outside'
$oldPgPassword=[Environment]::GetEnvironmentVariable('PGPASSWORD')
try{
    [Environment]::SetEnvironmentVariable('PGPASSWORD','SYNTHETIC_NOT_A_CREDENTIAL')
    $start=New-TransferNativeStart 'synthetic.exe' @('--no-password') 'private.pgpass' 'public-root.pem'
    if($start.Environment.ContainsKey('PGPASSWORD') -or $start.Environment['PGSSLMODE'] -cne 'verify-full' -or $start.UseShellExecute -or -not $start.CreateNoWindow){throw 'Native environment isolation failed.'}
    $checks.Add('Native child removes inherited passwords and pins verified TLS without shell execution')
}finally{[Environment]::SetEnvironmentVariable('PGPASSWORD',$oldPgPassword)}
$receipt=@{verdict='PASS';checks=$checks.ToArray();databaseConnections=0;databaseWrites=0;credentialReads=0;scope='Offline guard checks, not a real migration'}
$json=$receipt|ConvertTo-Json -Depth 5
if($ReceiptPath){[IO.File]::WriteAllText([IO.Path]::GetFullPath($ReceiptPath),$json)}
$json
