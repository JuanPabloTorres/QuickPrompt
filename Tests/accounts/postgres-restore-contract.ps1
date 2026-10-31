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
function Read-OwnerQuery([string]$Name) {
    $match = [regex]::Match($ownerSource, '(?ms)^\$' + [regex]::Escape($Name) + ' = @''\r?\n(.*?)\r?\n''@')
    if (-not $match.Success) { throw 'The owning restore query is missing.' }
    return $match.Groups[1].Value
}
$factualQuery = Read-OwnerQuery 'pgFactualContractSql'
$factualDataQuery = Read-OwnerQuery 'pgFactualDataSql'
$presenceQuery = Read-OwnerQuery 'pgPresenceContractSql'
$presenceDataQuery = Read-OwnerQuery 'pgPresenceDataSql'
$presenceSafetyQuery = Read-OwnerQuery 'pgPresenceSafetySql'
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
CREATE SCHEMA qp_operations;
CREATE TABLE qp_library.prompts (id integer PRIMARY KEY);
CREATE TABLE qp_library.collections (id integer PRIMARY KEY);
CREATE TABLE qp_library.factual_documents (id integer PRIMARY KEY, payload text NOT NULL);
CREATE TABLE qp_operations.factual_reviews (id integer PRIMARY KEY, payload text NOT NULL,
 "Status" character varying(32) NOT NULL DEFAULT 'REVIEW', "Version" integer NOT NULL DEFAULT 1,
 "ChildExecutionId" integer, "ExpiresAt" timestamptz NOT NULL DEFAULT '2099-01-01 00:00:00+00');
CREATE TABLE qp_operations.executions ("Id" integer PRIMARY KEY, "Status" character varying(32) NOT NULL, "CancelRequested" boolean NOT NULL DEFAULT false);
CREATE TABLE qp_operations.connections ("Id" uuid PRIMARY KEY, "OwnerId" uuid NOT NULL, "ProfileId" varchar(64) NOT NULL, "LibraryId" uuid,
 UNIQUE ("Id","OwnerId"));
CREATE TABLE qp_operations.companion_presence (
 "ConnectionId" uuid NOT NULL, "OwnerId" uuid NOT NULL, "ObservationId" uuid NOT NULL, "Signal" varchar(16) NOT NULL,
 "ObservedAt" timestamptz NOT NULL, "ExpiresAt" timestamptz NOT NULL,
 CONSTRAINT "PK_companion_presence" PRIMARY KEY ("ConnectionId"),
 CONSTRAINT "FK_companion_presence_connections_ConnectionId_OwnerId" FOREIGN KEY ("ConnectionId","OwnerId")
 REFERENCES qp_operations.connections("Id","OwnerId") ON DELETE CASCADE,
 CONSTRAINT bounded_companion_presence CHECK ("ObservationId" <> '00000000-0000-0000-0000-000000000000'::uuid
 AND "Signal" IN ('POLLING','WORKING','STOPPED') AND "ExpiresAt">="ObservedAt" AND "ExpiresAt"<="ObservedAt"+interval '20 seconds'));
CREATE INDEX "IX_companion_presence_ConnectionId_OwnerId" ON qp_operations.companion_presence("ConnectionId","OwnerId");
CREATE INDEX "IX_companion_presence_OwnerId_ConnectionId" ON qp_operations.companion_presence("OwnerId","ConnectionId");
ALTER TABLE qp_operations.companion_presence ENABLE ROW LEVEL SECURITY;
ALTER TABLE qp_operations.companion_presence FORCE ROW LEVEL SECURITY;
CREATE POLICY companion_presence_actor ON qp_operations.companion_presence TO quickprompt_app
 USING ("OwnerId"=nullif(current_setting('quickprompt.actor_id',true),'')::uuid)
 WITH CHECK ("OwnerId"=nullif(current_setting('quickprompt.actor_id',true),'')::uuid
 AND EXISTS(SELECT 1 FROM qp_operations.connections c WHERE c."Id"="ConnectionId" AND c."OwnerId"=companion_presence."OwnerId"
 AND c."ProfileId" IN ('ollama-qwen3-8b','ollama-qwen3-8b-proposals') AND c."LibraryId" IS NULL));
CREATE POLICY companion_presence_maintenance ON qp_operations.companion_presence TO quickprompt_migrator USING (true) WITH CHECK (true);
REVOKE ALL ON qp_operations.companion_presence FROM PUBLIC,quickprompt_app,quickprompt_execution;
GRANT SELECT,INSERT ON qp_operations.companion_presence TO quickprompt_app;
GRANT UPDATE ("ObservationId","Signal","ObservedAt","ExpiresAt") ON qp_operations.companion_presence TO quickprompt_app;
ALTER TABLE qp_operations.factual_reviews ADD CONSTRAINT factual_review_shape
 CHECK ("Version">0 AND "Status" IN ('ADMITTED','PROPOSING','REVIEW','COMPOSED','APPROVED','CANCELLED','INVALIDATED','EXPIRED','FAILED'));
ALTER TABLE qp_library.factual_documents ADD CONSTRAINT factual_document_shape CHECK (length(payload)>0);
ALTER TABLE qp_library.factual_documents ENABLE ROW LEVEL SECURITY;
ALTER TABLE qp_library.factual_documents FORCE ROW LEVEL SECURITY;
ALTER TABLE qp_operations.factual_reviews ENABLE ROW LEVEL SECURITY;
ALTER TABLE qp_operations.factual_reviews FORCE ROW LEVEL SECURITY;
CREATE POLICY factual_document_actor ON qp_library.factual_documents TO quickprompt_app USING (true) WITH CHECK (true);
CREATE POLICY factual_review_actor ON qp_operations.factual_reviews TO quickprompt_app USING (true) WITH CHECK (true);
CREATE POLICY factual_expiry_cancel ON qp_operations.executions FOR UPDATE TO quickprompt_migrator
 USING ("Status" IN ('Queued','Running') AND EXISTS(SELECT 1 FROM qp_operations.factual_reviews r WHERE r."ChildExecutionId"=executions."Id" AND r."ExpiresAt"<=clock_timestamp()))
 WITH CHECK ("Status" IN ('Queued','Running') AND "CancelRequested");
CREATE FUNCTION qp_operations.expire_factual_reviews() RETURNS integer LANGUAGE sql SECURITY DEFINER
 SET search_path=pg_catalog,pg_temp AS 'SELECT 1';
ALTER TABLE qp_library.collections ENABLE ROW LEVEL SECURITY;
ALTER TABLE qp_library.collections FORCE ROW LEVEL SECURITY;
CREATE POLICY erasure_collection_change ON qp_library.collections FOR UPDATE
  TO quickprompt_migrator USING (false) WITH CHECK (false);
'@
    $null = Invoke-FixtureSql $sourceDatabase "INSERT INTO qp_library.factual_documents (id,payload) VALUES (1,'synthetic baseline'); INSERT INTO qp_operations.factual_reviews (id,payload) VALUES (1,'synthetic baseline');"
    $baseline = Invoke-FixtureSql $sourceDatabase $query
    $factualBaseline = Invoke-FixtureSql $sourceDatabase $factualQuery
    $factualDataBaseline = Invoke-FixtureSql $sourceDatabase $factualDataQuery
    $null = Invoke-FixtureSql $sourceDatabase @'
INSERT INTO qp_operations.connections VALUES ('10000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001','ollama-qwen3-8b',NULL);
INSERT INTO qp_operations.companion_presence VALUES ('10000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001',
 '30000000-0000-4000-8000-000000000001','POLLING','2099-01-01T00:00:00Z','2099-01-01T00:00:20Z');
'@
    $presenceBaseline = Invoke-FixtureSql $sourceDatabase $presenceQuery
    $presenceDataBaseline = Invoke-FixtureSql $sourceDatabase $presenceDataQuery
    Assert-RestoreContract ((Invoke-FixtureSql $sourceDatabase $presenceSafetyQuery) -eq 't') 'The presence fixture is missing its exact permission boundary.'
    # Run each drift in its own rollback-only transaction over these fresh fixture
    # databases. Strip only the owner's transaction delimiters, never SQL terms.
    $presenceWithinTransaction = [regex]::Replace([regex]::Replace($presenceQuery,'^BEGIN;\s*',''),'ROLLBACK;\s*$','')
    $factualWithinTransaction = [regex]::Replace([regex]::Replace($factualQuery,'^BEGIN;\s*',''),'ROLLBACK;\s*$','')
    $presenceDrifts = @(
        @{ Key='presenceUnexpectedPolicyDetected'; Sql='CREATE POLICY unexpected_presence_access ON qp_operations.companion_presence TO quickprompt_app USING (true) WITH CHECK (true);' },
        @{ Key='presenceActorPolicyDriftDetected'; Sql='ALTER POLICY companion_presence_actor ON qp_operations.companion_presence WITH CHECK (true);' },
        @{ Key='presenceOwnerColumnGrantDetected'; Sql='GRANT UPDATE ("OwnerId") ON qp_operations.companion_presence TO quickprompt_app;' },
        @{ Key='presenceDispatchGrantDetected'; Sql='GRANT SELECT ON qp_operations.companion_presence TO quickprompt_execution;' },
        @{ Key='presencePublicGrantDetected'; Sql='GRANT SELECT ON qp_operations.companion_presence TO PUBLIC;' },
        @{ Key='presenceIndexRemovalDetected'; Sql='DROP INDEX qp_operations."IX_companion_presence_OwnerId_ConnectionId";' },
        @{ Key='presenceConstraintRemovalDetected'; Sql='ALTER TABLE qp_operations.companion_presence DROP CONSTRAINT bounded_companion_presence;' },
        @{ Key='presenceTtlTermLossDetected'; Sql='ALTER TABLE qp_operations.companion_presence DROP CONSTRAINT bounded_companion_presence; ALTER TABLE qp_operations.companion_presence ADD CONSTRAINT bounded_companion_presence CHECK ("ObservationId"<>''00000000-0000-0000-0000-000000000000''::uuid AND "Signal" IN (''POLLING'',''WORKING'',''STOPPED'') AND "ExpiresAt">="ObservedAt");' },
        @{ Key='presenceForeignKeyCascadeLossDetected'; Sql='ALTER TABLE qp_operations.companion_presence DROP CONSTRAINT "FK_companion_presence_connections_ConnectionId_OwnerId"; ALTER TABLE qp_operations.companion_presence ADD CONSTRAINT "FK_companion_presence_connections_ConnectionId_OwnerId" FOREIGN KEY ("ConnectionId","OwnerId") REFERENCES qp_operations.connections("Id","OwnerId");' }
    )
    foreach ($drift in $presenceDrifts) {
        $driftResult = (Invoke-FixtureSql $sourceDatabase "BEGIN; SET LOCAL ROLE quickprompt_migrator; $($drift.Sql) $presenceWithinTransaction $factualWithinTransaction ROLLBACK;") -split "`n"
        Assert-RestoreContract ($driftResult.Count -eq 2 -and $driftResult[0] -ne $presenceBaseline) 'Presence catalog drift escaped the new owner comparator.'
        Assert-RestoreContract ($driftResult[1] -eq $factualBaseline) 'The historical factual comparator did not demonstrate its missing presence scope.'
        Assert-RestoreContract ($presenceBaseline -eq (Invoke-FixtureSql $sourceDatabase $presenceQuery)) 'Presence drift was not rolled back to baseline.'
        $checks[$drift.Key] = $true
    }
    $exactPolicyCount = " AND (SELECT count(*)=2 FROM pg_policies WHERE schemaname='qp_operations' AND tablename='companion_presence')"
    $historicalPresenceSafety = $presenceSafetyQuery.Replace($exactPolicyCount,'')
    Assert-RestoreContract ($historicalPresenceSafety -ne $presenceSafetyQuery) 'The exact policy-count guard is missing from the owner.'
    $extraPolicy = 'CREATE POLICY unexpected_presence_access ON qp_operations.companion_presence TO quickprompt_app USING (true) WITH CHECK (true);'
    $safetyContrast = (Invoke-FixtureSql $sourceDatabase "BEGIN; SET LOCAL ROLE quickprompt_migrator; $extraPolicy $historicalPresenceSafety $presenceSafetyQuery ROLLBACK;") -split "`n"
    Assert-RestoreContract ($safetyContrast.Count -eq 2 -and $safetyContrast[0] -eq 't' -and $safetyContrast[1] -eq 'f') 'The permissive extra-policy baseline was not reproduced and rejected.'
    $checks.presenceHistoricalSafetyMissedExtraPolicyNowRejected = $true
    $changedPresenceData = Invoke-FixtureSql $sourceDatabase "BEGIN; UPDATE qp_operations.companion_presence SET `"Signal`"='WORKING'; $presenceDataQuery ROLLBACK;"
    Assert-RestoreContract ($changedPresenceData -ne $presenceDataBaseline) 'A presence observation change escaped the data fingerprint.'
    Assert-RestoreContract ($presenceDataBaseline -eq (Invoke-FixtureSql $sourceDatabase $presenceDataQuery)) 'The presence row mutation was not rolled back.'
    $checks.presenceDataDriftDetected = $true
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

    foreach ($entry in @(@{ Table='qp_library.factual_documents'; Policy='factual_document_actor'; Key='factualDocument' },
                         @{ Table='qp_operations.factual_reviews'; Policy='factual_review_actor'; Key='factualReview' })) {
        $null = Invoke-FixtureSql $sourceDatabase "UPDATE $($entry.Table) SET payload='synthetic changed' WHERE id=1;"
        Assert-RestoreContract ($factualDataBaseline -ne (Invoke-FixtureSql $sourceDatabase $factualDataQuery)) 'A factual-table row change escaped the owner data fingerprint.'
        $null = Invoke-FixtureSql $sourceDatabase "UPDATE $($entry.Table) SET payload='synthetic baseline' WHERE id=1;"
        Assert-RestoreContract ($factualDataBaseline -eq (Invoke-FixtureSql $sourceDatabase $factualDataQuery)) 'The factual data did not return to baseline.'
        $checks[($entry.Key + 'RowDriftDetected')] = $true
        $null = Invoke-FixtureSql $sourceDatabase "SET ROLE quickprompt_migrator; ALTER POLICY $($entry.Policy) ON $($entry.Table) USING (false);"
        Assert-RestoreContract ($factualBaseline -ne (Invoke-FixtureSql $sourceDatabase $factualQuery)) 'A factual-table policy change escaped the owner catalog fingerprint.'
        $null = Invoke-FixtureSql $sourceDatabase "SET ROLE quickprompt_migrator; ALTER POLICY $($entry.Policy) ON $($entry.Table) USING (true);"
        Assert-RestoreContract ($factualBaseline -eq (Invoke-FixtureSql $sourceDatabase $factualQuery)) 'The factual policy did not return to baseline.'
        $checks[($entry.Key + 'PolicyDriftDetected')] = $true
    }
    $null = Invoke-FixtureSql $sourceDatabase "SET ROLE quickprompt_migrator; CREATE OR REPLACE FUNCTION qp_operations.expire_factual_reviews() RETURNS integer LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS 'SELECT 2';"
    Assert-RestoreContract ($factualBaseline -ne (Invoke-FixtureSql $sourceDatabase $factualQuery)) 'A factual function change escaped the owner catalog fingerprint.'
    $null = Invoke-FixtureSql $sourceDatabase "SET ROLE quickprompt_migrator; CREATE OR REPLACE FUNCTION qp_operations.expire_factual_reviews() RETURNS integer LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS 'SELECT 1';"
    Assert-RestoreContract ($factualBaseline -eq (Invoke-FixtureSql $sourceDatabase $factualQuery)) 'The factual function did not return to baseline.'
    $checks.factualFunctionDriftDetected = $true

    $shape = Invoke-FixtureSql $sourceDatabase "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid='qp_operations.factual_reviews'::regclass AND conname='factual_review_shape';"
    $null = Invoke-FixtureSql $sourceDatabase 'SET ROLE quickprompt_migrator; ALTER TABLE qp_operations.factual_reviews DROP CONSTRAINT factual_review_shape;'
    Assert-RestoreContract ($factualBaseline -ne (Invoke-FixtureSql $sourceDatabase $factualQuery)) 'A removed factual CHECK escaped the comparator.'
    $checks.factualConstraintRemovalDetected = $true
    $null = Invoke-FixtureSql $sourceDatabase 'SET ROLE quickprompt_migrator; ALTER TABLE qp_operations.factual_reviews ADD CONSTRAINT factual_review_shape CHECK ("Status" IN (''ADMITTED'',''PROPOSING'',''REVIEW'',''COMPOSED'',''APPROVED'',''CANCELLED'',''INVALIDATED'',''EXPIRED'',''FAILED''));'
    Assert-RestoreContract ($factualBaseline -ne (Invoke-FixtureSql $sourceDatabase $factualQuery)) 'Dropping a factual CHECK term escaped PostgreSQL canonicalization.'
    $checks.factualConstraintTermLossDetected = $true
    $null = Invoke-FixtureSql $sourceDatabase "SET ROLE quickprompt_migrator; ALTER TABLE qp_operations.factual_reviews DROP CONSTRAINT factual_review_shape; ALTER TABLE qp_operations.factual_reviews ADD CONSTRAINT factual_review_shape $shape;"
    Assert-RestoreContract ($factualBaseline -eq (Invoke-FixtureSql $sourceDatabase $factualQuery)) 'The full factual CHECK did not return to baseline.'
    $expiry = Invoke-FixtureSql $sourceDatabase "SELECT with_check FROM pg_policies WHERE schemaname='qp_operations' AND tablename='executions' AND policyname='factual_expiry_cancel';"
    $null = Invoke-FixtureSql $sourceDatabase 'SET ROLE quickprompt_migrator; ALTER POLICY factual_expiry_cancel ON qp_operations.executions WITH CHECK ("Status" IN (''Queued'',''Running''));'
    Assert-RestoreContract ($factualBaseline -ne (Invoke-FixtureSql $sourceDatabase $factualQuery)) 'Dropping the cancellation term from a factual policy escaped canonicalization.'
    $checks.factualPolicyTermLossDetected = $true
    $null = Invoke-FixtureSql $sourceDatabase "SET ROLE quickprompt_migrator; ALTER POLICY factual_expiry_cancel ON qp_operations.executions WITH CHECK ($expiry);"
    Assert-RestoreContract ($factualBaseline -eq (Invoke-FixtureSql $sourceDatabase $factualQuery)) 'The full expiry policy did not return to baseline.'

    & docker exec $container pg_dump -U quickprompt_bootstrap -d $sourceDatabase -Fc -f $archive
    if ($LASTEXITCODE -ne 0) { throw 'Could not dump the synthetic catalog fixture.' }
    & docker exec $container pg_restore -U quickprompt_bootstrap --role=quickprompt_migrator -d $targetDatabase --exit-on-error $archive
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore the synthetic catalog fixture.' }
    Assert-RestoreContract ($baseline -eq (Invoke-FixtureSql $targetDatabase $query)) 'The restored policy differs from the source contract.'
    $checks.pgDumpRestorePreservedContract = $true
    Assert-RestoreContract ($factualBaseline -eq (Invoke-FixtureSql $targetDatabase $factualQuery)) 'The factual catalog changed during pg_dump/restore.'
    Assert-RestoreContract ($factualDataBaseline -eq (Invoke-FixtureSql $targetDatabase $factualDataQuery)) 'The factual rows changed during pg_dump/restore.'
    $checks.factualDumpRestorePreservedContractAndRows = $true
    Assert-RestoreContract ($presenceBaseline -eq (Invoke-FixtureSql $targetDatabase $presenceQuery)) 'The presence catalog changed during pg_dump/restore.'
    Assert-RestoreContract ($presenceDataBaseline -eq (Invoke-FixtureSql $targetDatabase $presenceDataQuery)) 'The presence observations changed during pg_dump/restore.'
    Assert-RestoreContract ((Invoke-FixtureSql $targetDatabase $presenceSafetyQuery) -eq 't') 'The restored presence boundary is unsafe.'
    $checks.presenceDumpRestorePreservedContractAndRows = $true
    $checks.varcharArrayCheckAndPolicyReparsePreserved = $true

    $null = Invoke-FixtureSql $targetDatabase 'SET ROLE quickprompt_migrator; ALTER POLICY erasure_collection_change ON qp_library.collections WITH CHECK (true);'
    Assert-RestoreContract ($baseline -ne (Invoke-FixtureSql $targetDatabase $query)) 'A policy change after restoration escaped the owner comparator.'
    $checks.postRestoreMutationDetected = $true
    $null = Invoke-FixtureSql $targetDatabase "UPDATE qp_library.factual_documents SET payload='changed after restore' WHERE id=1;"
    Assert-RestoreContract ($factualDataBaseline -ne (Invoke-FixtureSql $targetDatabase $factualDataQuery)) 'A factual row mutation after restore escaped the owner fingerprint.'
    $checks.postRestoreFactualMutationDetected = $true
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
