#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceOperatorFile,
    [Parameter(Mandatory)][string]$SourceHost,
    [Parameter(Mandatory)][string]$TargetOperatorFile,
    [Parameter(Mandatory)][string]$TargetHost,
    [Parameter(Mandatory)][string]$ReviewRoot,
    [Parameter(Mandatory)][string]$PlanId,
    [Parameter(Mandatory)][string]$NativeClientDirectory
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Import-Module (Join-Path $PSScriptRoot 'PublicPostgres.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'PublicPostgresTransfer.psm1') -Force -DisableNameChecking
Import-Module (Join-Path $PSScriptRoot 'PublicPostgresNative.psm1') -Force
$null=Import-PublicPostgresDriver $repository
if($PlanId -cnotmatch '^[a-f0-9]{32}$'){throw 'A fixed unique plan ID is required.'}
$private=Assert-PublicPostgresPrivatePath $ReviewRoot $repository
$source=Read-PublicPostgresOperator $SourceOperatorFile $SourceHost 'quickprompt_cloud' $repository
$target=Read-PublicPostgresOperator $TargetOperatorFile $TargetHost 'quickprompt_cloud' $repository
$states=@{}
try {
    if($source.Host -ceq $target.Host){throw 'Source and destination must differ.'}
    foreach($side in @('source','target')) {
        $settings=if($side -eq 'source'){$source}else{$target}
        $connection=[Npgsql.NpgsqlConnection]::new($settings.ConnectionString)
        try {
            $connection.Open(); $transaction=$connection.BeginTransaction()
            try {
                Invoke-TransferSql $connection 'SET TRANSACTION READ ONLY;' $transaction
                if(-not (Get-PublicPostgresCapabilities $connection).tls){throw 'Client TLS verification required.'}
                $inventory=Get-TransferInventory $connection
                if($side -eq 'source'){Assert-TransferInventory $inventory 'quickprompt_cloud' 35 22}
                else{Assert-TransferInventory $inventory 'quickprompt_cloud' 36 23 -AllowedLocaleProviders @('b','c','i');if(-not $inventory.canCreateDatabase){throw 'The target operator cannot create an isolated restore database.'}}
                Invoke-TransferSql $connection 'SET LOCAL ROLE quickprompt_migrator; SET LOCAL search_path=pg_catalog,public;' $transaction
                $catalog=Invoke-TransferScalar $connection ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'sql/transfer-catalog.sql'))) $transaction
                $migrations=Invoke-TransferScalar $connection 'SELECT jsonb_agg("MigrationId" ORDER BY "MigrationId" COLLATE pg_catalog."C")::text FROM qp_migrations.history;' $transaction
                Invoke-TransferSql $connection 'RESET ROLE;' $transaction
                if($side -eq 'target') {
                    $rows=Invoke-TransferScalar $connection (Get-TransferRowsSql $inventory) $transaction | ConvertFrom-Json -AsHashtable
                    if(@($rows|Where-Object {$_.relation -cne '"qp_migrations"."history"' -and $_.rows -ne 0}).Count){throw 'Destination contains user data; no preparation.'}
                }
                $states[$side]=@{inventory=$inventory;catalog=$catalog;migrations=@($migrations|ConvertFrom-Json);catalogSha256=Get-TransferTextHash $catalog}
            } finally {$transaction.Rollback();$transaction.Dispose()}
        } finally {$connection.Dispose()}
    }
    if($states.source.migrations.Count -ne 31 -or $states.target.migrations.Count -ne 32 -or
        ($states.target.migrations[0..30] -join '|') -cne ($states.source.migrations -join '|') -or
        $states.target.migrations[-1] -cne '20261029151628_CompanionOperationalPresence'){throw 'The exact migration prefix differs.'}
    $sourceItems=@(foreach($item in (ConvertFrom-Json -InputObject $states.source.catalog -AsHashtable -NoEnumerate)){ConvertTo-Json -InputObject $item -Depth 25 -Compress})
    $targetItems=@(foreach($item in (ConvertFrom-Json -InputObject $states.target.catalog -AsHashtable -NoEnumerate)){ConvertTo-Json -InputObject $item -Depth 25 -Compress})
    if(@(Compare-Object $sourceItems $targetItems|Where-Object SideIndicator -EQ '<=').Count){throw 'A source schema definition is missing or changed in destination.'}
    $role='qp_transfer_'+$PlanId
    $reader=Get-TransferReaderSql $states.source.inventory $role $role $source.Username
    $inputs=@('Invoke-PublicPostgresTransfer.ps1','PublicPostgres.psm1','PublicPostgresTransfer.psm1','PublicPostgresNative.psm1','sql/transfer-catalog.sql')|ForEach-Object{
        $path=Join-Path $PSScriptRoot $_
        @{path=$path;sha256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()}
    }
    $inputs+=@{path=(Join-Path $repository 'scripts/Protect-QuickPromptSecret.ps1');sha256=(Get-FileHash -LiteralPath (Join-Path $repository 'scripts/Protect-QuickPromptSecret.ps1')).Hash.ToLowerInvariant()}
    $trust=@{}
    foreach($side in @('source','target')){$path=Join-Path $private ($side+'-root-ca.pem');$trust[$side]=@{path=$path;sha256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()}}
    $migrationPath=Join-Path $private 'companion-presence-migration.sql'
    $plan=[ordered]@{
        schema='quickprompt-render-neon-transfer/1';planId=$PlanId;state='PREPARED_NOT_AUTHORIZED';scope='Complete database, preserving account/library IDs, hashes, histories, memory, permissions and protected keys'
        source=@{operatorFile=$SourceOperatorFile;operatorFileSha256=(Get-FileHash -LiteralPath $SourceOperatorFile).Hash.ToLowerInvariant();host=$source.Host;database=$source.Database;operator=$source.Username;inventory=$states.source.inventory;catalogSha256=$states.source.catalogSha256;migrations=$states.source.migrations}
        target=@{operatorFile=$TargetOperatorFile;operatorFileSha256=(Get-FileHash -LiteralPath $TargetOperatorFile).Hash.ToLowerInvariant();host=$target.Host;database=$target.Database;operator=$target.Username;inventory=$states.target.inventory;catalogSha256=$states.target.catalogSha256;migrations=$states.target.migrations;stagingDatabase=('qp_import_'+$PlanId);preservedEmptyDatabase=('qp_before_'+$PlanId)}
        outputRoot=$private;temporaryRole=$role;temporaryPolicy=$role;readerSql=$reader
        trust=$trust;native=Get-TransferNativeRuntime $NativeClientDirectory;inputs=@($inputs)
        migration=@{path=$migrationPath;sha256=(Get-FileHash -LiteralPath $migrationPath).Hash.ToLowerInvariant()}
        limits=@{archiveBytes=67108864;nativeTimeoutSeconds=180;automaticRetries=0}
        archiveProtection='Windows DPAPI CurrentUser; 64 MiB; private plaintext only during native dump/restore, removed after verified encryption/child exit; not portable disaster recovery'
        requiredBeforeExecute=@('Approve this exact plan SHA-256','Suspend the current Render service and verify no application/dispatcher sessions','Keep current HTTPS origin, Google/MCP settings and cryptographic keys unchanged')
        cutover='Copy/verify into isolated Neon staging; clean temporary access; apply pinned additive migration; retain existing empty Neon DB under qp_before name; rename verified import to quickprompt_cloud. Production configuration is a separate explicit action.'
        rollback='Before reopening traffic, retain original Render database/configuration and restore its connection if needed. Never point traffic to a stale source after new Neon writes.'
        productionConfigurationChanged=$false
    }
    $path=New-TransferPrivateFile (Join-Path $private 'transfer-plan.json') $repository
    [IO.File]::WriteAllText($path,($plan|ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false))
    @{state=$plan.state;plan=$path;sha256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant();sourceTables=35;sourceMigrations=31;targetTables=36;targetMigrations=32;privateRowsCopied=0;sqlWrites=0}|ConvertTo-Json -Compress
} catch {
    $cause=$_.Exception
    while($cause.InnerException){$cause=$cause.InnerException}
    $sqlState=if($cause -is [Npgsql.PostgresException]){$cause.SqlState}else{'none'}
    throw ('Transfer plan preparation failed at line {0}, type {1}, SQLSTATE {2}, error ID {3}; no database writes or private data transfer performed.' -f $_.InvocationInfo.ScriptLineNumber,$cause.GetType().Name,$sqlState,$_.FullyQualifiedErrorId)
}
finally {$source.Password='';$target.Password='';$source=$null;$target=$null}
