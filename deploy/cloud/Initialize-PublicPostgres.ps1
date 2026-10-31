#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OperatorFile,
    [Parameter(Mandatory)][string]$ExpectedHost,
    [Parameter(Mandatory)][string]$ExpectedDatabase,
    [switch]$ProbeOnly,
    [string]$OutputDirectory,
    [switch]$ResumeMigrations,
    [ValidateSet('None','ProbeRollback','RetryRolledBack')][string]$BootstrapRecovery = 'None'
)
$ErrorActionPreference = 'Stop'
if (($ProbeOnly -and $ResumeMigrations) -or (($ProbeOnly -or $ResumeMigrations) -and $BootstrapRecovery -ne 'None')) { throw 'Choose one explicit operation.' }
$bootstrapRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Import-Module (Join-Path $PSScriptRoot 'PublicPostgres.psm1') -Force
$bootstrapSdk = Import-PublicPostgresDriver $bootstrapRoot
$bootstrapTarget = Read-PublicPostgresOperator $OperatorFile $ExpectedHost $ExpectedDatabase $bootstrapRoot
$bootstrapConnection = [Npgsql.NpgsqlConnection]::new($bootstrapTarget.ConnectionString)
$bootstrapPhase = 'READ_ONLY_PROBE'; $bootstrapStep = 'NONE'; $bootstrapCommitted = $false
try {
    $bootstrapConnection.Open()
    # Probe executes only SELECTs in a read-only transaction. No roles, secrets or schemas are created.
    $bootstrapTransaction = $bootstrapConnection.BeginTransaction()
    try {
        $bootstrapReadOnly = $bootstrapConnection.CreateCommand()
        $bootstrapReadOnly.CommandText = 'SET TRANSACTION READ ONLY'
        try { $null = $bootstrapReadOnly.ExecuteNonQuery() } finally { $bootstrapReadOnly.Dispose() }
        $bootstrapCapabilities = Get-PublicPostgresCapabilities $bootstrapConnection
    } finally { $bootstrapTransaction.Rollback(); $bootstrapTransaction.Dispose() }
    if ($ProbeOnly) {
        $bootstrapCapabilities | ConvertTo-Json -Compress
        return
    }
    if (-not $OutputDirectory) { throw 'Supply an explicit private output directory for limited credentials.' }
    $bootstrapPhase = 'PREPARE_SECRETS'
    if ($ResumeMigrations) {
        # Explicit recovery consumes the original private credentials. It never creates or changes roles/schemas.
        $bootstrapConnections = Read-PublicPostgresConnections $OutputDirectory $bootstrapTarget $bootstrapRoot
        $bootstrapPhase = 'ROLE_BOUNDARY'
        Test-PublicPostgresRoleConnections $bootstrapConnections
    } else {
        Assert-PublicPostgresEmptyTarget $bootstrapCapabilities $ExpectedDatabase $bootstrapTarget.Username
        if ($BootstrapRecovery -eq 'None') {
            $bootstrapSecrets = New-PublicPostgresSecrets $OutputDirectory $bootstrapTarget $bootstrapRoot
        } else {
            $bootstrapSaved = Read-PublicPostgresConnections $OutputDirectory $bootstrapTarget $bootstrapRoot
            $bootstrapPasswords = @{}
            foreach ($name in @('migrator','application','execution')) { $bootstrapPasswords[$name]=([Npgsql.NpgsqlConnectionStringBuilder]::new($bootstrapSaved[$name])).Password }
            $bootstrapSecrets = @{ Passwords=$bootstrapPasswords; Connections=$bootstrapSaved }
        }
        $bootstrapConnections = $bootstrapSecrets.Connections
        $bootstrapPhase = 'ROLE_DDL'; $bootstrapStep = 'LOCK'
        $bootstrapWrite = $bootstrapConnection.BeginTransaction()
        try {
            $bootstrapCommand = $bootstrapConnection.CreateCommand()
            $bootstrapCommand.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended('QuickPrompt.PublicBootstrap',0))"
            $null = $bootstrapCommand.ExecuteScalar()
            $bootstrapStep = 'EMPTY_TARGET'
            Assert-PublicPostgresEmptyTarget (Get-PublicPostgresCapabilities $bootstrapConnection) $ExpectedDatabase $bootstrapTarget.Username
            foreach ($step in (Get-PublicPostgresBootstrapSteps $bootstrapSecrets.Passwords $ExpectedDatabase $bootstrapTarget.Username).GetEnumerator()) {
                $bootstrapStep = $step.Key; $bootstrapCommand.CommandText = $step.Value
                $null = $bootstrapCommand.ExecuteNonQuery()
            }
            if ($BootstrapRecovery -eq 'ProbeRollback') {
                $bootstrapStep = 'ROLLBACK'; $bootstrapWrite.Rollback()
                @{ verdict='ROLLED_BACK_PROBE'; database=$ExpectedDatabase; ddlCommitted=$false; migrationsRun=$false } | ConvertTo-Json -Compress
                return
            }
            $bootstrapStep = 'COMMIT'; $bootstrapWrite.Commit(); $bootstrapCommitted = $true
        } catch {
            $bootstrapDdlFailure = $_
            try { $bootstrapWrite.Rollback() } catch { } # Preserve the original safe diagnosis if the connection also fails during cleanup.
            throw $bootstrapDdlFailure
        }
        finally { if ($null -ne $bootstrapCommand) { $bootstrapCommand.Dispose() }; $bootstrapWrite.Dispose(); $bootstrapSecrets.Passwords.Clear() }
        $bootstrapPhase = 'ROLE_BOUNDARY'; $bootstrapStep = 'NONE'
        Test-PublicPostgresRoleConnections $bootstrapConnections
    }
    $bootstrapPhase = 'MIGRATIONS'; $bootstrapStep = 'NONE'
    Invoke-PublicPostgresMigrations $bootstrapSdk $bootstrapRoot $bootstrapConnections.migrator
    $bootstrapPhase = 'FINAL_BOUNDARY'
    Test-PublicPostgresRoleConnections $bootstrapConnections
    # The managed operator has NOINHERIT membership: resolving a schema-qualified
    # table as regclass requires USAGE. Verify through the actual limited consumer
    # instead of broadening operator permissions merely for this smoke check.
    $bootstrapConsumer = [Npgsql.NpgsqlConnection]::new($bootstrapConnections.application)
    try {
        $bootstrapConsumer.Open()
        Test-PublicPostgresMigratedBoundary $bootstrapConsumer
    } finally { $bootstrapConsumer.Dispose() }
    @{ verdict='INITIALIZED'; database=$ExpectedDatabase; limitedRoles=3; migrations='applied with the existing migrator'; tls='VerifyFull'; runtimeStarted=$false } | ConvertTo-Json -Compress
} catch {
    # Driver exceptions can include statement/connection diagnostics. Emit a fixed message, never an inner exception.
    $bootstrapFailure = Get-PublicPostgresSafeFailure $_.Exception $bootstrapPhase $bootstrapStep $bootstrapCommitted
    throw ('PublicPostgresFailure:' + ($bootstrapFailure | ConvertTo-Json -Compress))
} finally { $bootstrapConnection.Dispose(); $bootstrapTarget.Password = ''; $bootstrapTarget = $null }
