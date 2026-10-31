#requires -Version 7.2
[CmdletBinding()]
param([string]$ReceiptPath)
$ErrorActionPreference = 'Stop'
$testRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Import-Module (Join-Path $PSScriptRoot 'PublicPostgres.psm1') -Force
$null = Import-PublicPostgresDriver $testRoot
$fixtureDirectory = Join-Path ([IO.Path]::GetTempPath()) ('quickprompt-bootstrap-synthetic-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtureDirectory) | Out-Null
$fixtureFile = Join-Path $fixtureDirectory 'operator.json'
$fixturePassword = 'Synthetic!' + [Guid]::NewGuid().ToString('N') + ';value'
$fixtureHost = 'dpg-fixture-a.oregon-postgres.render.com'
$fixtureMetadata = @{ provider='Render'; serviceId='dpg-fixture-a'; externalDatabaseUrl="postgresql://fixture:$([Uri]::EscapeDataString($fixturePassword))@$fixtureHost/quickprompt_cloud" }
$passed = [Collections.Generic.List[string]]::new()
function Write-Fixture($Metadata) { [IO.File]::WriteAllText($fixtureFile, ($Metadata | ConvertTo-Json -Compress)) }
function Assert-Rejected([string]$Name, [scriptblock]$Operation) {
    $rejected = $false
    try { $null = & $Operation } catch {
        if ($_.Exception.Message.Contains($fixturePassword)) { throw 'A private value escaped into diagnostics.' }
        $rejected = $true
    }
    if (-not $rejected) { throw "Expected rejection: $Name" }
    $passed.Add($Name)
}
Write-Fixture $fixtureMetadata
$target = Read-PublicPostgresOperator $fixtureFile $fixtureHost 'quickprompt_cloud' $testRoot
if ($target.Password -cne $fixturePassword -or $target.Username -ne 'fixture' -or $target.SslMode -ne 'VerifyFull' -or $target.IncludeErrorDetail -or $target.LogParameters -or $target.Pooling) { throw 'The safe parser contract failed.' }
$passed.Add('encoded password, VerifyFull and safe driver settings')
Assert-Rejected 'mismatched host' { Read-PublicPostgresOperator $fixtureFile 'other.render.com' 'quickprompt_cloud' $testRoot }
Assert-Rejected 'mismatched database' { Read-PublicPostgresOperator $fixtureFile $fixtureHost 'different_database' $testRoot }
Assert-Rejected 'repository secret path' { Assert-PublicPostgresPrivatePath (Join-Path $testRoot 'unsafe.json') $testRoot }
Assert-Rejected 'relative secret path' { Assert-PublicPostgresPrivatePath 'unsafe.json' $testRoot }
$savedSecrets = New-PublicPostgresSecrets (Join-Path $fixtureDirectory 'new-limited-credentials') $target $testRoot
$saved = Read-PublicPostgresConnections $savedSecrets.Directory $target $testRoot
if ($saved.Count -ne 3 -or @($savedSecrets.Passwords.Values | Select-Object -Unique).Count -ne 3) { throw 'Limited credentials were not distinct.' }
$passed.Add('three distinct limited credentials saved with verified private file permissions')
Assert-Rejected 'no overwrite of existing private credentials' { New-PublicPostgresSecrets $savedSecrets.Directory $target $testRoot }
$unsafeConnections = $saved.Clone()
$unsafeBuilder = [Npgsql.NpgsqlConnectionStringBuilder]::new($saved.application); $unsafeBuilder.SslMode='Require'
$unsafeConnections.application = $unsafeBuilder.ConnectionString
[IO.File]::WriteAllText((Join-Path $savedSecrets.Directory 'postgres-connections.json'),($unsafeConnections | ConvertTo-Json))
Assert-Rejected 'saved connections cannot downgrade TLS' { Read-PublicPostgresConnections $savedSecrets.Directory $target $testRoot }
[IO.File]::WriteAllText((Join-Path $savedSecrets.Directory 'postgres-connections.json'),($saved | ConvertTo-Json))
$sql = Get-PublicPostgresBootstrapSql $savedSecrets.Passwords 'quickprompt_cloud' 'operator";unsafe'
if (-not $sql.Contains('TO "operator"";unsafe" WITH SET TRUE, INHERIT FALSE') -or $sql.Contains('IF NOT EXISTS') -or $sql.Contains('ALTER ROLE')) { throw 'Bootstrap cannot quote identifiers or silently reuse/alter existing objects.' }
$passed.Add('operator identifier quoted and no existing-role reset or silent reuse')
$steps = Get-PublicPostgresBootstrapSteps $savedSecrets.Passwords 'quickprompt_cloud' 'fixture'
if (-not $steps.SCHEMA_PERMISSIONS.StartsWith('SET LOCAL ROLE quickprompt_migrator;') -or -not $steps.DEFAULT_PRIVILEGES.EndsWith('RESET ROLE;')) { throw 'Schema grants must execute with their owner and restore the operator.' }
$passed.Add('owner role explicitly controls schema grants without inheriting or broadening database permissions')
$invalidPasswords = $savedSecrets.Passwords.Clone(); $invalidPasswords.migrator = "unsafe'input"
Assert-Rejected 'only generated hex enters role SQL' { Get-PublicPostgresBootstrapSql $invalidPasswords 'quickprompt_cloud' 'fixture' }
$linked = Join-Path $fixtureDirectory 'linked'
$null = [IO.Directory]::CreateSymbolicLink($linked,$savedSecrets.Directory)
try { Assert-Rejected 'linked private path' { Assert-PublicPostgresPrivatePath (Join-Path $linked 'postgres-connections.json') $testRoot } }
finally { [IO.Directory]::Delete($linked) }
foreach ($invalid in @('postgresql://fixture:dummy@localhost/quickprompt_cloud',
    'postgresql://fixture:dummy@127.0.0.1/quickprompt_cloud',
    'postgresql://fixture:dummy@dpg-other-a.oregon-postgres.render.com/quickprompt_cloud',
    "postgresql://fixture:dummy@$fixtureHost/quickprompt_cloud?sslmode=disable", "https://$fixtureHost/quickprompt_cloud")) {
    $altered = $fixtureMetadata.Clone(); $altered.externalDatabaseUrl = $invalid; Write-Fixture $altered
    Assert-Rejected 'untrusted URI shape or target' { Read-PublicPostgresOperator $fixtureFile $fixtureHost 'quickprompt_cloud' $testRoot }
}
$capabilities = @{ database='quickprompt_cloud'; operator='fixture'; tls=$true; serverVersion=180000;
    canCreateRole=$true; isSuperuser=$false; isDatabaseOwner=$true; canCreateSchema=$true;
    existingTargetRoles=0; existingTargetSchemas=0; nonSystemRelations=0 }
Assert-PublicPostgresEmptyTarget $capabilities 'quickprompt_cloud' 'fixture'
$passed.Add('limited managed operator accepted without superuser')
foreach ($key in @('tls','canCreateRole','isDatabaseOwner','canCreateSchema')) {
    $altered = $capabilities.Clone(); $altered[$key] = $false
    Assert-Rejected "missing $key" { Assert-PublicPostgresEmptyTarget $altered 'quickprompt_cloud' 'fixture' }
}
foreach ($key in @('existingTargetRoles','existingTargetSchemas','nonSystemRelations')) {
    $altered = $capabilities.Clone(); $altered[$key] = 1
    Assert-Rejected "nonempty $key" { Assert-PublicPostgresEmptyTarget $altered 'quickprompt_cloud' 'fixture' }
}
$altered = $capabilities.Clone(); $altered.serverVersion = 150000
Assert-Rejected 'unsupported old server' { Assert-PublicPostgresEmptyTarget $altered 'quickprompt_cloud' 'fixture' }
foreach ($file in @('PublicPostgres.psm1','Initialize-PublicPostgres.ps1','Test-PublicPostgresBootstrap.ps1')) {
    $tokens = $null; $syntaxErrors = $null
    $null = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $file),[ref]$tokens,[ref]$syntaxErrors)
    if ($syntaxErrors.Count) { throw "PowerShell parse failed: $file" }
}
$passed.Add('all helper scripts parse')
$privateFailure = [Npgsql.PostgresException]::new($fixturePassword,'ERROR','ERROR','42501')
$wrappedFailure = [InvalidOperationException]::new('private details must not be copied',$privateFailure)
$failure = Get-PublicPostgresSafeFailure $wrappedFailure 'ROLE_DDL' 'CREATE_SCHEMAS' $false
$failureJson = $failure | ConvertTo-Json -Compress
if ($failure.sqlState -ne '42501' -or $failure.phase -ne 'ROLE_DDL' -or $failure.ddlCommitted -or $failureJson.Contains($fixturePassword) -or $failureJson.Contains('private details')) { throw 'Safe failure projection violated its contract.' }
$passed.Add('nested PostgreSQL errors expose only allowlisted state and stage')
$unknownFailure = [Npgsql.PostgresException]::new($fixturePassword,'ERROR','ERROR','ZZZZZ')
if ((Get-PublicPostgresSafeFailure $unknownFailure 'UntrustedPhase' 'UntrustedStep' $false).sqlState -ne 'OTHER') { throw 'Unknown SQLSTATE escaped the allowlist.' }
$passed.Add('unknown diagnostic metadata cannot escape the allowlist')
$migrationFailure = [InvalidOperationException]::new($fixturePassword); $migrationFailure.Data['QuickPromptMigrationExitCode']=1
if ((Get-PublicPostgresSafeFailure $migrationFailure 'MIGRATIONS' 'NONE' $true).migrationExitCode -ne 1) { throw 'Migration exit code was lost.' }
$passed.Add('migration failure retains safe exit code without text')
$receipt = @{ verdict='PASS'; passed=$passed.Count; checks=$passed.ToArray(); remoteConnections=0; databaseWrites=0; fixture='synthetic private temporary files only' }
$json = $receipt | ConvertTo-Json -Depth 4
if ($ReceiptPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($ReceiptPath), $json) }
$json
