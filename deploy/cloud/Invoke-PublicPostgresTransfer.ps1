#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReviewedPlan,
    [switch]$ReviewOnly,
    [switch]$Execute,
    [string]$ApprovedPlanSha256,
    [switch]$SourceServiceSuspended
)
$ErrorActionPreference='Stop'
if($ReviewOnly -eq $Execute){throw 'Select exactly ReviewOnly or Execute.'}
if(-not [IO.File]::Exists($ReviewedPlan) -or [IO.FileInfo]::new($ReviewedPlan).Length -gt 1048576){throw 'A bounded reviewed plan is required.'}
$hash=(Get-FileHash -LiteralPath $ReviewedPlan).Hash.ToLowerInvariant()
if($Execute -and ($ApprovedPlanSha256 -cnotmatch '^[a-f0-9]{64}$' -or $hash -cne $ApprovedPlanSha256 -or -not $SourceServiceSuspended)){
    throw 'Execution requires the explicitly approved plan hash and observed Render suspension; no credentials were read.'
}
$plan=[IO.File]::ReadAllText($ReviewedPlan)|ConvertFrom-Json -AsHashtable
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$expectedInputs=@($PSCommandPath,(Join-Path $PSScriptRoot 'PublicPostgres.psm1'),(Join-Path $PSScriptRoot 'PublicPostgresTransfer.psm1'),
    (Join-Path $PSScriptRoot 'PublicPostgresNative.psm1'),(Join-Path $PSScriptRoot 'sql/transfer-catalog.sql'),(Join-Path $repository 'scripts/Protect-QuickPromptSecret.ps1'))
if($plan.schema -cne 'quickprompt-render-neon-transfer/1' -or $plan.planId -cnotmatch '^[a-f0-9]{32}$' -or
    $plan.inputs.Count -ne $expectedInputs.Count -or $plan.target.stagingDatabase -cne ('qp_import_'+$plan.planId) -or
    $plan.target.preservedEmptyDatabase -cne ('qp_before_'+$plan.planId) -or $plan.source.database -cne 'quickprompt_cloud' -or
    $plan.target.database -cne 'quickprompt_cloud' -or $plan.source.host -ceq $plan.target.host -or
    $plan.temporaryRole -cne ('qp_transfer_'+$plan.planId) -or $plan.temporaryPolicy -cne $plan.temporaryRole -or
    $plan.limits.archiveBytes -ne 67108864 -or $plan.limits.nativeTimeoutSeconds -ne 180 -or $plan.limits.automaticRetries -ne 0){throw 'Reviewed plan scope differs.'}
foreach($path in $expectedInputs){
    $inputEntry=@($plan.inputs|Where-Object path -CEQ $path)
    if($inputEntry.Count -ne 1 -or (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -cne $inputEntry[0].sha256){throw 'Reviewed implementation changed before load.'}
}
foreach($file in @($plan.native.files)+@($plan.trust.source,$plan.trust.target,$plan.migration)){
    if((Get-FileHash -LiteralPath $file.path).Hash.ToLowerInvariant() -cne $file.sha256){throw 'Reviewed native or TLS/migration input changed.'}
}
foreach($name in @('pg_dump.exe','pg_restore.exe')){
    $executable=Join-Path $plan.native.directory $name
    if(@($plan.native.files|Where-Object path -CEQ $executable).Count -ne 1){throw 'Native executable is outside the reviewed file inventory.'}
}
if($ReviewOnly){@{verdict='REVIEW_ONLY';planSha256=$hash;credentialReads=0;databaseConnections=0;databaseWrites=0}|ConvertTo-Json -Compress;return}

Import-Module (Join-Path $PSScriptRoot 'PublicPostgres.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'PublicPostgresTransfer.psm1') -Force -DisableNameChecking
Import-Module (Join-Path $PSScriptRoot 'PublicPostgresNative.psm1') -Force
$null=Import-PublicPostgresDriver $repository
$private=Assert-PublicPostgresPrivatePath $plan.outputRoot $repository
if(-not [string]::Equals([IO.Path]::GetFullPath($ReviewedPlan),(Join-Path $private 'transfer-plan.json'),[StringComparison]::OrdinalIgnoreCase)){throw 'Plan destination differs.'}
$started=New-TransferPrivateFile (Join-Path $private 'execution.started') $repository
[IO.File]::WriteAllText($started,$hash)
$receipt=@{verdict='IN_PROGRESS';phase='PREFLIGHT';planSha256=$hash;sourceCleanup='NOT_CREATED';targetCleanup='NOT_CREATED';promotion='NOT_STARTED';productionConfigurationChanged=$false}
$receiptFile=New-TransferPrivateFile (Join-Path $private 'execution.json') $repository
$owned=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$source=$null;$target=$null;$sourceConnection=$null;$baseline=$null;$staging=$null;$snapshotTransaction=$null
$sourceAttempted=$false;$targetAttempted=$false;$restoreCompleted=$false;$failed=$false
$readerRole=Quote-TransferIdentifier $plan.temporaryRole
$marker='QuickPromptTransfer:'+ $plan.planId
$sourceSql=$plan.readerSql
$catalogSql=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'sql/transfer-catalog.sql'))
$sessionSql="SET LOCAL search_path=pg_catalog,public; SET LOCAL TimeZone='UTC'; SET LOCAL DateStyle='ISO, YMD'; SET LOCAL extra_float_digits=1;"

function Open-TargetDatabase([string]$Database){
    $settings=[Npgsql.NpgsqlConnectionStringBuilder]::new($target.ConnectionString);$settings.Database=$Database
    $connection=[Npgsql.NpgsqlConnection]::new($settings.ConnectionString);$connection.Open();return $connection
}
function Invoke-Transactional($Connection,[string]$Sql){
    $transaction=$Connection.BeginTransaction()
    try{Invoke-TransferSql $Connection $Sql $transaction;$transaction.Commit()}catch{try{$transaction.Rollback()}catch{};throw}finally{$transaction.Dispose()}
}
function Read-CatalogHash($Connection){
    $transaction=$Connection.BeginTransaction()
    try{Invoke-TransferSql $Connection ('SET TRANSACTION READ ONLY;'+$sessionSql) $transaction;return Get-TransferTextHash (Invoke-TransferScalar $Connection $catalogSql $transaction)}
    finally{$transaction.Rollback();$transaction.Dispose()}
}
function Assert-EmptyBaseline($Connection){
    if((Read-CatalogHash $Connection) -cne $plan.target.catalogSha256){throw 'Target baseline schema changed.'}
    $rows=Invoke-TransferScalar $Connection (Get-TransferRowsSql $plan.target.inventory)|ConvertFrom-Json -AsHashtable
    if(@($rows|Where-Object{$_.relation -cne '"qp_migrations"."history"' -and $_.rows -ne 0}).Count){throw 'Target baseline is no longer empty.'}
}
function Remove-OwnedReader($Connection,[bool]$Restored){
    $roleText=Invoke-TransferScalar $Connection ("SELECT jsonb_build_object('marker',shobj_description(oid,'pg_authid'),'login',rolcanlogin,'bypass',rolbypassrls,'super',rolsuper,'createRole',rolcreaterole)::text FROM pg_roles WHERE rolname='"+$plan.temporaryRole+"';")
    if(-not $roleText){return 'NOT_PRESENT'}
    $roleState=$roleText|ConvertFrom-Json -AsHashtable
    if($roleState.marker -cne $marker -or $roleState.login -or $roleState.bypass -or $roleState.super -or $roleState.createRole){throw 'Reader ownership or authority differs; no cleanup.'}
    if($Restored){Invoke-Transactional $Connection $sourceSql.cleanup}
    else{Invoke-Transactional $Connection ('DROP ROLE '+$readerRole+';')}
    return 'REMOVED'
}
function Invoke-NativeStep([string]$Name,[string]$Executable,[string[]]$Arguments,[string]$PassFile,[string]$Ca,[string]$Output){
    $receipt.phase=$Name
    $start=New-TransferNativeStart (Join-Path $plan.native.directory $Executable) $Arguments $PassFile $Ca
    Invoke-TransferNative $start $Output (Join-Path $private ($Name+'.stderr')) $repository 67108864 $owned
}
try{
    foreach($side in @('source','target')){
        if((Get-FileHash -LiteralPath $plan[$side].operatorFile).Hash.ToLowerInvariant() -cne $plan[$side].operatorFileSha256){throw 'Operator configuration changed.'}
    }
    $source=Read-PublicPostgresOperator $plan.source.operatorFile $plan.source.host $plan.source.database $repository
    $target=Read-PublicPostgresOperator $plan.target.operatorFile $plan.target.host $plan.target.database $repository
    $sourceConnection=[Npgsql.NpgsqlConnection]::new($source.ConnectionString);$sourceConnection.Open()
    $baseline=Open-TargetDatabase $target.Database
    if(-not (Get-PublicPostgresCapabilities $sourceConnection).tls -or -not (Get-PublicPostgresCapabilities $baseline).tls){throw 'Verified client TLS required.'}
    $sourceInventory=Get-TransferInventory $sourceConnection;$targetInventory=Get-TransferInventory $baseline
    Assert-TransferInventory $sourceInventory $source.Database 35 22
    Assert-TransferInventory $targetInventory $target.Database 36 23 -AllowedLocaleProviders @('b','c','i')
    foreach($side in @('source','target')){
        $current=if($side -eq 'source'){$sourceInventory}else{$targetInventory}
        foreach($key in @('schemas','tables','sequences','locale')){
            if((ConvertTo-Json -InputObject $current[$key] -Depth 12 -Compress) -cne (ConvertTo-Json -InputObject $plan[$side].inventory[$key] -Depth 12 -Compress)){throw 'Reviewed inventory changed.'}
        }
    }
    $computedReader=Get-TransferReaderSql $sourceInventory $plan.temporaryRole $plan.temporaryPolicy $source.Username
    foreach($key in @('prepare','cleanup','createRole')){if($computedReader[$key] -cne $sourceSql[$key]){throw 'Reader SQL differs from its reviewed capability.'}}
    if((Read-CatalogHash $sourceConnection) -cne $plan.source.catalogSha256){throw 'Source schema changed.'}
    Assert-EmptyBaseline $baseline
    if((Invoke-TransferScalar $sourceConnection "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND usename IN ('quickprompt_app','quickprompt_execution');") -ne '0'){
        throw 'Source runtime sessions remain; suspend and drain before transfer.'
    }
    foreach($connection in @($sourceConnection,$baseline)){
        if((Invoke-TransferScalar $connection ("SELECT count(*) FROM pg_roles WHERE rolname='"+$plan.temporaryRole+"';")) -ne '0'){throw 'Temporary reader already exists; no adoption.'}
    }
    foreach($name in @($plan.target.stagingDatabase,$plan.target.preservedEmptyDatabase)){
        if((Invoke-TransferScalar $baseline ("SELECT count(*) FROM pg_database WHERE datname='"+$name+"';")) -ne '0'){throw 'A transfer database already exists; no adoption.'}
    }
    $receipt.phase='SOURCE_READER';$sourceAttempted=$true
    Invoke-Transactional $sourceConnection ($sourceSql.prepare+"COMMENT ON ROLE $readerRole IS '$marker';")
    $snapshotTransaction=$sourceConnection.BeginTransaction([Data.IsolationLevel]::RepeatableRead)
    Invoke-TransferSql $sourceConnection ('SET TRANSACTION READ ONLY; SET LOCAL ROLE '+$readerRole+';'+$sessionSql) $snapshotTransaction
    if((Invoke-TransferScalar $sourceConnection (Get-TransferPendingExecutionSql) $snapshotTransaction) -ne '0'){throw 'Pending/running executions prevent this cutover.'}
    $snapshot=Invoke-TransferScalar $sourceConnection 'SELECT pg_export_snapshot();' $snapshotTransaction
    if($snapshot -cnotmatch '^[A-F0-9-]{1,80}$'){throw 'Invalid exported snapshot.'}
    $rows=Invoke-TransferScalar $sourceConnection (Get-TransferRowsSql $plan.source.inventory) $snapshotTransaction
    $sequences=Invoke-TransferScalar $sourceConnection (Get-TransferSequenceSql $plan.source.inventory) $snapshotTransaction
    $snapshotMeta=New-TransferPrivateFile (Join-Path $private 'snapshot-reference.json') $repository
    [IO.File]::WriteAllText($snapshotMeta,(@{rows=$rows;sequences=$sequences}|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
    $sourcePass=New-TransferPrivateFile (Join-Path $private 'source.pgpass') $repository;$null=$owned.Add($sourcePass)
    [IO.File]::WriteAllText($sourcePass,(Get-TransferPassLine $source),[Text.UTF8Encoding]::new($false))
    $dump=Join-Path $private 'database.dump'
    $arguments=@('--host',$source.Host,'--port','5432','--username',$source.Username,'--dbname',$source.Database,'--role',$plan.temporaryRole,
        '--snapshot',$snapshot,'--enable-row-security','--inserts','--format=custom','--extra-float-digits=1','--no-password')
    foreach($schema in $plan.source.inventory.schemas){$arguments+=@('--schema',$schema.name)}
    Invoke-NativeStep 'DUMP' 'pg_dump.exe' $arguments $sourcePass $plan.trust.source.path $dump
    if((Invoke-TransferScalar $sourceConnection (Get-TransferSequenceSql $plan.source.inventory) $snapshotTransaction) -cne $sequences){throw 'Source sequences changed during snapshot.'}
    $snapshotTransaction.Rollback();$snapshotTransaction.Dispose();$snapshotTransaction=$null
    $receipt.sourceCleanup=Remove-OwnedReader $sourceConnection $true;$sourceAttempted=$false
    if((Read-CatalogHash $sourceConnection) -cne $plan.source.catalogSha256){throw 'Source permissions were not restored exactly.'}
    $receipt.archive=Protect-TransferArchive $dump (Join-Path $private 'database.dump.dpapi') $plan.planId $repository
    [IO.File]::Delete($dump);$null=$owned.Remove($dump)
    $receipt.phase='ISOLATED_DATABASE'
    $locale=$plan.source.inventory.locale
    foreach($value in @($locale.collate,$locale.ctype)){if($value -cnotmatch '^[A-Za-z0-9_.@-]+$'){throw 'Unexpected locale literal.'}}
    # Keep database ownership with the CREATEDB operator. SET-only membership in
    # the migrator does not authorize renaming a database owned by that role.
    Invoke-TransferSql $baseline ('CREATE DATABASE '+(Quote-TransferIdentifier $plan.target.stagingDatabase)+' OWNER '+(Quote-TransferIdentifier $target.Username)+" TEMPLATE template0 ENCODING 'UTF8' LOCALE_PROVIDER libc LC_COLLATE '"+$locale.collate+"' LC_CTYPE '"+$locale.ctype+"';")
    $staging=Open-TargetDatabase $plan.target.stagingDatabase
    Invoke-Transactional $staging ('REVOKE CONNECT ON DATABASE '+(Quote-TransferIdentifier $plan.target.stagingDatabase)+' FROM PUBLIC;GRANT CONNECT,CREATE ON DATABASE '+(Quote-TransferIdentifier $plan.target.stagingDatabase)+' TO quickprompt_migrator;')
    $targetAttempted=$true
    Invoke-Transactional $staging ($sourceSql.createRole+"COMMENT ON ROLE $readerRole IS '$marker';")
    $targetPass=New-TransferPrivateFile (Join-Path $private 'target.pgpass') $repository;$null=$owned.Add($targetPass)
    $stagingSettings=[Npgsql.NpgsqlConnectionStringBuilder]::new($target.ConnectionString);$stagingSettings.Database=$plan.target.stagingDatabase
    [IO.File]::WriteAllText($targetPass,(Get-TransferPassLine $stagingSettings),[Text.UTF8Encoding]::new($false))
    $restoreInput=New-TransferPrivateFile (Join-Path $private 'restore-input.dump') $repository;$null=$owned.Add($restoreInput)
    $clear=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($receipt.archive.path),[Text.Encoding]::UTF8.GetBytes($plan.planId),[Security.Cryptography.DataProtectionScope]::CurrentUser)
    try{[IO.File]::WriteAllBytes($restoreInput,$clear)}finally{[Array]::Clear($clear)}
    $arguments=@('--host',$target.Host,'--port','5432','--username',$target.Username,'--dbname',$plan.target.stagingDatabase,'--role','quickprompt_migrator',
        '--single-transaction','--exit-on-error','--no-owner','--enable-row-security','--no-password',$restoreInput)
    Invoke-NativeStep 'RESTORE' 'pg_restore.exe' $arguments $targetPass $plan.trust.target.path (Join-Path $private 'restore.stdout')
    $restoreCompleted=$true
    Invoke-Transactional $staging ('REVOKE CREATE ON DATABASE '+(Quote-TransferIdentifier $plan.target.stagingDatabase)+' FROM quickprompt_migrator;')
    $transaction=$staging.BeginTransaction()
    try{
        Invoke-TransferSql $staging ('SET TRANSACTION READ ONLY;'+$sessionSql) $transaction
        if((Invoke-TransferScalar $staging (Get-TransferRowsSql $plan.source.inventory) $transaction) -cne $rows -or
            (Invoke-TransferScalar $staging (Get-TransferSequenceSql $plan.source.inventory) $transaction) -cne $sequences){throw 'Restored data or sequence fingerprints differ.'}
    }finally{$transaction.Rollback();$transaction.Dispose()}
    $receipt.targetCleanup=Remove-OwnedReader $staging $true;$targetAttempted=$false
    if((Read-CatalogHash $staging) -cne $plan.source.catalogSha256){throw 'Restored catalog or permissions differ.'}
    $receipt.phase='ADDITIVE_MIGRATION'
    Invoke-TransferSql $staging ('SET ROLE quickprompt_migrator;'+[IO.File]::ReadAllText($plan.migration.path)+'RESET ROLE;')
    if((Read-CatalogHash $staging) -cne $plan.target.catalogSha256){throw 'Final catalog differs from the approved migration baseline.'}
    $transaction=$staging.BeginTransaction()
    try{Invoke-TransferSql $staging ('SET TRANSACTION READ ONLY;'+$sessionSql) $transaction;$after=Invoke-TransferScalar $staging (Get-TransferRowsSql $plan.source.inventory) $transaction|ConvertFrom-Json -AsHashtable}
    finally{$transaction.Rollback();$transaction.Dispose()}
    $before=$rows|ConvertFrom-Json -AsHashtable
    if((ConvertTo-Json -InputObject @($after|Where-Object relation -CNE '"qp_migrations"."history"') -Depth 8 -Compress) -cne
       (ConvertTo-Json -InputObject @($before|Where-Object relation -CNE '"qp_migrations"."history"') -Depth 8 -Compress)){throw 'Additive migration altered inherited user data.'}
    Test-PublicPostgresMigratedBoundary $staging
    Assert-EmptyBaseline $baseline
    $receipt.phase='PROMOTION';$receipt.promotion='STARTED'
    $baseline.Dispose();$baseline=$null
    Invoke-TransferSql $staging ('ALTER DATABASE '+(Quote-TransferIdentifier $target.Database)+' ALLOW_CONNECTIONS false;')
    $null=Invoke-TransferScalar $staging ("SELECT count(*) FROM (SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='"+$target.Database+"') s;")
    Invoke-TransferSql $staging ('ALTER DATABASE '+(Quote-TransferIdentifier $target.Database)+' RENAME TO '+(Quote-TransferIdentifier $plan.target.preservedEmptyDatabase)+';')
    Invoke-TransferSql $staging ('ALTER DATABASE '+(Quote-TransferIdentifier $plan.target.preservedEmptyDatabase)+' ALLOW_CONNECTIONS true;')
    $baseline=Open-TargetDatabase $plan.target.preservedEmptyDatabase
    Invoke-Transactional $baseline ('REVOKE CONNECT ON DATABASE '+(Quote-TransferIdentifier $plan.target.preservedEmptyDatabase)+' FROM quickprompt_app,quickprompt_execution;')
    Invoke-Transactional $staging ('GRANT CONNECT ON DATABASE '+(Quote-TransferIdentifier $plan.target.stagingDatabase)+' TO quickprompt_app,quickprompt_execution;')
    $staging.Dispose();$staging=$null
    $null=Invoke-TransferScalar $baseline ("SELECT count(*) FROM (SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='"+$plan.target.stagingDatabase+"') s;")
    Invoke-TransferSql $baseline ('ALTER DATABASE '+(Quote-TransferIdentifier $plan.target.stagingDatabase)+' RENAME TO '+(Quote-TransferIdentifier $target.Database)+';')
    $staging=Open-TargetDatabase $target.Database
    Test-PublicPostgresMigratedBoundary $staging
    $receipt.promotion='VERIFIED';$receipt.verdict='COPIED_VERIFIED_NOT_PUBLISHED';$receipt.tables=35;$receipt.migrations=32
}catch{
    $failed=$true;$receipt.verdict='FAILED';$receipt.failedPhase=$receipt.phase
    $cause=$_.Exception;while($cause.InnerException -and $cause -isnot [Npgsql.PostgresException]){$cause=$cause.InnerException}
    if($cause -is [Npgsql.PostgresException]){$receipt.sqlState=$cause.SqlState}
}finally{
    if($snapshotTransaction){try{$snapshotTransaction.Rollback()}catch{};$snapshotTransaction.Dispose()}
    if($sourceAttempted -and $sourceConnection){try{$receipt.sourceCleanup=Remove-OwnedReader $sourceConnection $true}catch{$receipt.sourceCleanup='FAILED';$failed=$true;$receipt.verdict='FAILED'}}
    if($targetAttempted -and $staging){try{$receipt.targetCleanup=Remove-OwnedReader $staging $restoreCompleted}catch{$receipt.targetCleanup='FAILED';$failed=$true;$receipt.verdict='FAILED'}}
    foreach($connection in @($sourceConnection,$baseline,$staging)){if($connection){$connection.Dispose()}}
    foreach($name in @('source.pgpass','target.pgpass','database.dump','restore-input.dump')){
        $path=Join-Path $private $name
        if($owned.Contains($path) -and [IO.File]::Exists($path)){try{$null=Assert-PublicPostgresPrivatePath $path $repository;[IO.File]::Delete($path)}catch{$failed=$true;$receipt.verdict='FAILED';$receipt.plaintextCleanup='FAILED'}}
    }
    if($source){$source.Password=''};if($target){$target.Password=''}
    $receipt.endedAtUtc=[DateTime]::UtcNow.ToString('o')
    [IO.File]::WriteAllText($receiptFile,($receipt|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
}
if($failed){throw 'Transfer halted. Production configuration was not changed. Inspect the private stage receipt; do not retry or reopen traffic automatically.'}
@{verdict=$receipt.verdict;sourceCleanup=$receipt.sourceCleanup;targetCleanup=$receipt.targetCleanup;promotion=$receipt.promotion;productionConfigurationChanged=$false}|ConvertTo-Json -Compress
