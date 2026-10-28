#requires -Version 7.2
param([string]$EvidenceDirectory = '.playwright/postgres-restore-contract')

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$restoreScript = Join-Path $repository 'scripts/Test-QuickPromptPostgresRestore.ps1'
$evidence = [IO.Path]::GetFullPath((Join-Path $repository $EvidenceDirectory))
$qaRoot = [IO.Path]::GetFullPath((Join-Path $repository '.playwright')) + [IO.Path]::DirectorySeparatorChar
if (-not $evidence.StartsWith($qaRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Restore-contract evidence must stay inside this repository .playwright directory.'
}
if (Test-Path -LiteralPath (Join-Path $evidence 'result.json')) {
    throw 'This contrast already has a receipt. Use another evidence directory; do not overwrite a result.'
}

$container = 'quickprompt-saas-postgres'
$labelsText = & docker inspect --format '{{json .Config.Labels}}' $container 2>$null
if ($LASTEXITCODE -ne 0) { throw 'The development PostgreSQL container is unavailable.' }
$labels = $labelsText | ConvertFrom-Json
if ($labels.'com.docker.compose.project' -ne 'quickprompt-saas-development' -or $labels.'io.quickprompt.scope' -ne 'saas-development') {
    throw 'The PostgreSQL container is outside the approved synthetic development scope.'
}

# Exercise the SQL used by the owning restore script, rather than reimplementing
# its comparator. Both databases are created here and contain only these fixtures.
$ownerSource = Get-Content -LiteralPath $restoreScript -Raw
$assignment = [regex]::Match($ownerSource, '(?ms)^\$pgErasureContractSql = @''\r?\n(.*?)\r?\n''@')
if (-not $assignment.Success) { throw 'The restore script no longer exposes its expected catalog query.' }
$query = $assignment.Groups[1].Value
$oldQuery = $query.Replace("'erasure_collection_change'", "'erasure_collection_update'")
if ($oldQuery -eq $query) { throw 'The real collection-erasure policy is absent from the restore contract.' }

$fixtureId = [Guid]::NewGuid().ToString('N')
$sourceDatabase = 'qp_restore_contract_source_' + $fixtureId
$targetDatabase = 'qp_restore_contract_target_' + $fixtureId
$archive = '/tmp/quickprompt_restore_contract_' + $fixtureId + '.dump'
$created = [Collections.Generic.List[string]]::new()
$checks = [ordered]@{}
$started = [DateTimeOffset]::UtcNow
$failure = $null
[IO.Directory]::CreateDirectory($evidence) | Out-Null

function Invoke-FixtureSql([string]$Database, [string]$Sql) {
    if ($Database -notin $created) { throw 'The database was not created by this contrast.' }
    $rows = $Sql | & docker exec -i $container psql -X -q -v ON_ERROR_STOP=1 -U quickprompt_bootstrap -d $Database -At 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'The isolated PostgreSQL contrast failed; no database output was printed.' }
    return ($rows -join "`n").Trim()
}

function Assert-RestoreContract([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

try {
    foreach ($database in @($sourceDatabase, $targetDatabase)) {
        & docker exec $container createdb -U quickprompt_bootstrap -O quickprompt_migrator $database
        if ($LASTEXITCODE -ne 0) { throw 'Could not create a fresh restore-contract fixture.' }
        $created.Add($database)
    }
    $null = Invoke-FixtureSql $sourceDatabase @'
SET ROLE quickprompt_migrator;
CREATE SCHEMA qp_library;
CREATE TABLE qp_library.prompts (id integer PRIMARY KEY);
CREATE TABLE qp_library.collections (id integer PRIMARY KEY);
ALTER TABLE qp_library.collections ENABLE ROW LEVEL SECURITY;
ALTER TABLE qp_library.collections FORCE ROW LEVEL SECURITY;
CREATE POLICY erasure_collection_change ON qp_library.collections FOR UPDATE
  TO quickprompt_migrator USING (false) WITH CHECK (false);
'@
    $baseline = Invoke-FixtureSql $sourceDatabase $query
    $oldBaseline = Invoke-FixtureSql $sourceDatabase $oldQuery
    Assert-RestoreContract ($baseline -match '^[a-f0-9]{32}$') 'The owner comparator did not produce a catalog fingerprint.'

    $null = Invoke-FixtureSql $sourceDatabase 'SET ROLE quickprompt_migrator; ALTER POLICY erasure_collection_change ON qp_library.collections USING (true);'
    $changed = Invoke-FixtureSql $sourceDatabase $query
    $oldChanged = Invoke-FixtureSql $sourceDatabase $oldQuery
    Assert-RestoreContract ($oldBaseline -eq $oldChanged) 'The historical comparator no longer reproduces the omitted-policy defect.'
    Assert-RestoreContract ($baseline -ne $changed) 'Changing the collection-erasure policy escaped the current comparator.'
    $checks.historicalComparatorMissedChangedPolicy = $true
    $checks.ownerComparatorDetectedChangedPolicy = $true

    $null = Invoke-FixtureSql $sourceDatabase 'SET ROLE quickprompt_migrator; ALTER POLICY erasure_collection_change ON qp_library.collections USING (false);'
    Assert-RestoreContract ($baseline -eq (Invoke-FixtureSql $sourceDatabase $query)) 'Restoring the policy did not restore the original contract.'
    $checks.restoredPolicyMatchesBaseline = $true

    & docker exec $container pg_dump -U quickprompt_bootstrap -d $sourceDatabase -Fc -f $archive
    if ($LASTEXITCODE -ne 0) { throw 'Could not dump the synthetic catalog fixture.' }
    & docker exec $container pg_restore -U quickprompt_bootstrap --role=quickprompt_migrator -d $targetDatabase --exit-on-error $archive
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore the synthetic catalog fixture.' }
    Assert-RestoreContract ($baseline -eq (Invoke-FixtureSql $targetDatabase $query)) 'The restored policy differs from the source contract.'
    $checks.pgDumpRestorePreservedContract = $true

    $null = Invoke-FixtureSql $targetDatabase 'SET ROLE quickprompt_migrator; ALTER POLICY erasure_collection_change ON qp_library.collections WITH CHECK (true);'
    Assert-RestoreContract ($baseline -ne (Invoke-FixtureSql $targetDatabase $query)) 'A policy change after restoration escaped the owner comparator.'
    $checks.postRestoreMutationDetected = $true
}
catch { $failure = $_.Exception.Message }
finally {
    $cleanup = $true
    foreach ($database in $created) {
        if ($database -notmatch '^qp_restore_contract_(source|target)_[a-f0-9]{32}$' -or -not $database.EndsWith($fixtureId, [StringComparison]::Ordinal)) {
            $cleanup = $false
            continue
        }
        & docker exec $container dropdb -U quickprompt_bootstrap --if-exists $database 2>$null
        if ($LASTEXITCODE -ne 0) { $cleanup = $false }
    }
    & docker exec $container rm -f -- $archive 2>$null
    if ($LASTEXITCODE -ne 0) { $cleanup = $false }
    $receipt = [ordered]@{
        kind = 'SYNTHETIC_RESTORE_POLICY_CONTRAST'
        status = if ($failure -or -not $cleanup) { 'failed' } else { 'passed' }
        ownerScriptSha256 = (Get-FileHash -LiteralPath $restoreScript -Algorithm SHA256).Hash
        checks = $checks
        createdDatabases = @($created)
        cleanupVerified = $cleanup
        privateDataRead = $false
        remoteDatabaseUsed = $false
        startedUtc = $started.ToString('o')
        finishedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        limitation = 'Catalog-policy comparator only; not a production-data restore, recovery-time objective, or provider backup certification.'
        failure = $failure
    }
    $receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'result.json') -Encoding utf8
}
if ($failure -or -not $cleanup) { throw 'The synthetic restore-policy contrast failed. Inspect its preserved receipt.' }
$receipt | ConvertTo-Json -Depth 5
