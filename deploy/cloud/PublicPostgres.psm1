#requires -Version 7.2
Set-StrictMode -Version Latest

function Assert-PublicPostgresPrivatePath([string]$Path, [string]$RepositoryRoot) {
    if (-not [IO.Path]::IsPathFullyQualified($Path)) { throw 'Use an absolute physical private path outside the repository.' }
    $full = [IO.Path]::GetFullPath($Path)
    $repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ($full -eq $repository -or $full.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'PostgreSQL credentials must remain outside the repository.'
    }
    $item = [IO.FileInfo]::new($full)
    if ($item.Exists -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Private paths cannot contain links or junctions.' }
    $ancestor = [IO.DirectoryInfo]::new($full)
    while ($null -ne $ancestor) {
        if ($ancestor.Exists -and ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Private paths cannot contain links or junctions.' }
        $ancestor = $ancestor.Parent
    }
    return $full
}

function Import-PublicPostgresDriver([string]$RepositoryRoot) {
    $sdk = & (Join-Path $RepositoryRoot 'scripts/Resolve-NovaDotnet.ps1') -ForBuild
    $framework = Join-Path (Split-Path -Parent $sdk) 'shared/Microsoft.AspNetCore.App/10.0.14'
    $logging = Join-Path $framework 'Microsoft.Extensions.Logging.Abstractions.dll'
    $driver = Join-Path $RepositoryRoot 'backend/QuickPrompt.Cloud/bin/Release/net10.0/Npgsql.dll'
    if (-not (Test-Path -LiteralPath $logging) -or -not (Test-Path -LiteralPath $driver)) { throw 'Build the identified Cloud Release candidate with the pinned .NET 10 runtime first.' }
    Add-Type -Path $logging
    Add-Type -Path $driver
    return $sdk
}

function Read-PublicPostgresOperator([string]$OperatorFile, [string]$ExpectedHost, [string]$ExpectedDatabase, [string]$RepositoryRoot) {
    $path = Assert-PublicPostgresPrivatePath $OperatorFile $RepositoryRoot
    if (-not [IO.File]::Exists($path)) { throw 'The private operator configuration does not exist.' }
    & (Join-Path $RepositoryRoot 'scripts/Protect-QuickPromptSecret.ps1') -Path $path
    try {
        $metadata = [IO.File]::ReadAllText($path) | ConvertFrom-Json -AsHashtable
        if (@($metadata.Keys | Where-Object { $_ -notin @('provider','serviceId','externalDatabaseUrl') }).Count -gt 0 -or $metadata.provider -ne 'Render' -or $metadata.serviceId -notmatch '^dpg-[a-z0-9]+-a$') { throw 'Invalid metadata.' }
        $uri = [Uri]::new([string]$metadata.externalDatabaseUrl)
        if (-not $uri.IsAbsoluteUri -or $uri.Scheme -notin @('postgresql','postgres') -or $uri.IsLoopback -or $uri.HostNameType -ne [UriHostNameType]::Dns -or $uri.Host -notmatch '^[a-z0-9-]+\.[a-z0-9.-]+\.render\.com$' -or -not $uri.Host.StartsWith($metadata.serviceId + '.', [StringComparison]::Ordinal) -or $uri.Query.Length -gt 0 -or $uri.Fragment.Length -gt 0) { throw 'Invalid target.' }
        $hostName = $uri.DnsSafeHost
        $database = [Uri]::UnescapeDataString($uri.AbsolutePath.TrimStart('/'))
        if ($ExpectedDatabase -notmatch '^[a-z_][a-z0-9_]{0,62}$' -or $database -cne $ExpectedDatabase -or -not [string]::Equals($hostName, $ExpectedHost, [StringComparison]::OrdinalIgnoreCase)) { throw 'Target mismatch.' }
        $separator = $uri.UserInfo.IndexOf(':')
        if ($separator -lt 1 -or $separator -eq $uri.UserInfo.Length - 1) { throw 'Missing credentials.' }
        $builder = [Npgsql.NpgsqlConnectionStringBuilder]::new()
        $builder.Host = $hostName; $builder.Port = if ($uri.Port -eq -1) { 5432 } else { $uri.Port }
        if ($builder.Port -ne 5432) { throw 'Unexpected remote port.' }
        $builder.Database = $database
        $builder.Username = [Uri]::UnescapeDataString($uri.UserInfo.Substring(0, $separator))
        $builder.Password = [Uri]::UnescapeDataString($uri.UserInfo.Substring($separator + 1))
        $builder.SslMode = [Npgsql.SslMode]::VerifyFull; $builder.IncludeErrorDetail = $false
        $builder.LogParameters = $false; $builder.Pooling = $false; $builder.Timeout = 15; $builder.CommandTimeout = 30
        $builder.ApplicationName = 'QuickPrompt.PublicBootstrap'
        return $builder
    } catch { throw 'Invalid private operator configuration or explicit target mismatch. No connection was attempted.' }
}

function Get-PublicPostgresCapabilities($Connection) {
    $command = $Connection.CreateCommand()
    $command.CommandText = @'
SELECT json_build_object(
 'database',current_database(),'operator',current_user,
 'serverVersion',current_setting('server_version_num')::int,
 'tls',COALESCE((SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid()),false),
 'canCreateRole',r.rolcreaterole,'isSuperuser',r.rolsuper,
 'isDatabaseOwner',d.datdba=r.oid,'canCreateSchema',has_database_privilege(current_user,current_database(),'CREATE'),
 'existingTargetRoles',(SELECT count(*) FROM pg_roles WHERE rolname IN ('quickprompt_migrator','quickprompt_app','quickprompt_execution')),
 'existingTargetSchemas',(SELECT count(*) FROM pg_namespace WHERE nspname IN ('qp_identity','qp_library','qp_operations','qp_migrations','qp_host')),
 'nonSystemRelations',(SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE c.relkind IN ('r','p','m','S') AND n.nspname NOT IN ('information_schema') AND n.nspname NOT LIKE 'pg_%')
)::text FROM pg_roles r JOIN pg_database d ON d.datname=current_database() WHERE r.rolname=current_user;
'@
    try { return ($command.ExecuteScalar() | ConvertFrom-Json -AsHashtable) }
    finally { $command.Dispose() }
}

function Assert-PublicPostgresEmptyTarget($Capabilities, [string]$ExpectedDatabase, [string]$ExpectedOperator) {
    if ($Capabilities.database -cne $ExpectedDatabase -or $Capabilities.operator -cne $ExpectedOperator -or -not $Capabilities.tls -or $Capabilities.serverVersion -lt 160000 -or -not ($Capabilities.canCreateRole -or $Capabilities.isSuperuser) -or -not $Capabilities.isDatabaseOwner -or -not $Capabilities.canCreateSchema -or $Capabilities.existingTargetRoles -ne 0 -or $Capabilities.existingTargetSchemas -ne 0 -or $Capabilities.nonSystemRelations -ne 0) {
        throw 'Bootstrap requires the expected empty PostgreSQL 16+ database, TLS, owner/CREATEROLE authority and no existing QuickPrompt roles or schemas.'
    }
}

function New-PublicPostgresSecrets([string]$Directory, $Operator, [string]$RepositoryRoot) {
    $private = Assert-PublicPostgresPrivatePath $Directory $RepositoryRoot
    if (Test-Path -LiteralPath $private) { throw 'Choose a new private output directory. Existing credentials are never overwritten.' }
    [IO.Directory]::CreateDirectory($private) | Out-Null
    $passwords = @{}; $connections = @{}
    foreach ($entry in @{ migrator='quickprompt_migrator'; application='quickprompt_app'; execution='quickprompt_execution' }.GetEnumerator()) {
        $password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        $passwords[$entry.Key] = $password
        $builder = [Npgsql.NpgsqlConnectionStringBuilder]::new($Operator.ConnectionString)
        $builder.Username = $entry.Value; $builder.Password = $password
        $builder.Pooling = $true; $builder.ApplicationName = 'QuickPrompt.' + $entry.Key
        $connections[$entry.Key] = $builder.ConnectionString
        $path = Join-Path $private ('postgres-' + $entry.Key + '.txt')
        $stream = [IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None); $stream.Dispose()
        & (Join-Path $RepositoryRoot 'scripts/Protect-QuickPromptSecret.ps1') -Path $path
        [IO.File]::WriteAllText($path,$password)
    }
    $path = Join-Path $private 'postgres-connections.json'
    $stream = [IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None); $stream.Dispose()
    & (Join-Path $RepositoryRoot 'scripts/Protect-QuickPromptSecret.ps1') -Path $path
    [IO.File]::WriteAllText($path,($connections | ConvertTo-Json))
    return @{ Directory=$private; Passwords=$passwords; Connections=$connections }
}

function Get-PublicPostgresBootstrapSteps($Passwords, [string]$Database, [string]$Operator) {
    foreach ($name in @('migrator','application','execution')) {
        if ($Passwords[$name] -notmatch '^[A-F0-9]{64}$') { throw 'Only generated hexadecimal role credentials are accepted.' }
    }
    if ($Database -notmatch '^[a-z_][a-z0-9_]{0,62}$' -or [string]::IsNullOrWhiteSpace($Operator)) { throw 'Invalid bootstrap identifiers.' }
    $quote = [Npgsql.NpgsqlCommandBuilder]::new()
    try { $databaseIdentifier=$quote.QuoteIdentifier($Database); $operatorIdentifier=$quote.QuoteIdentifier($Operator) }
    finally { $quote.Dispose() }
    # This fixed SQL is never printed or written to disk. Only generated hex passwords are inserted.
    # A managed operator needs SET membership to assign schemas to its newly created migrator on PostgreSQL 16+.
    $steps = [ordered]@{ CREATE_ROLES = @'
CREATE ROLE quickprompt_migrator LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '__MIGRATOR__';
CREATE ROLE quickprompt_app LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '__APPLICATION__';
CREATE ROLE quickprompt_execution LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '__EXECUTION__';
'@
    MIGRATOR_MEMBERSHIP = @'
GRANT quickprompt_migrator TO __OPERATOR__ WITH SET TRUE, INHERIT FALSE;
'@
    DATABASE_CONNECT = @'
GRANT CONNECT ON DATABASE __DATABASE__ TO quickprompt_migrator,quickprompt_app,quickprompt_execution;
'@
    CREATE_SCHEMAS = @'
CREATE SCHEMA qp_identity AUTHORIZATION quickprompt_migrator;
CREATE SCHEMA qp_library AUTHORIZATION quickprompt_migrator;
CREATE SCHEMA qp_operations AUTHORIZATION quickprompt_migrator;
CREATE SCHEMA qp_migrations AUTHORIZATION quickprompt_migrator;
CREATE SCHEMA qp_host AUTHORIZATION quickprompt_migrator;
'@
    SCHEMA_PERMISSIONS = @'
SET LOCAL ROLE quickprompt_migrator;
REVOKE ALL ON SCHEMA qp_identity,qp_library,qp_operations,qp_migrations,qp_host FROM PUBLIC;
REVOKE ALL ON SCHEMA qp_host FROM quickprompt_execution;
GRANT USAGE ON SCHEMA qp_identity,qp_library,qp_operations TO quickprompt_app;
GRANT USAGE ON SCHEMA qp_operations TO quickprompt_execution;
'@
    DEFAULT_PRIVILEGES = @'
ALTER DEFAULT PRIVILEGES IN SCHEMA qp_identity,qp_library,qp_operations GRANT SELECT,INSERT,UPDATE,DELETE ON TABLES TO quickprompt_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA qp_identity,qp_library,qp_operations GRANT USAGE,SELECT ON SEQUENCES TO quickprompt_app;
RESET ROLE;
'@
    }
    foreach ($key in @($steps.Keys)) {
        $steps[$key] = $steps[$key].Replace('__MIGRATOR__',$Passwords.migrator).Replace('__APPLICATION__',$Passwords.application).Replace('__EXECUTION__',$Passwords.execution).Replace('__DATABASE__',$databaseIdentifier).Replace('__OPERATOR__',$operatorIdentifier)
    }
    return $steps
}

function Get-PublicPostgresBootstrapSql($Passwords, [string]$Database, [string]$Operator) {
    return (Get-PublicPostgresBootstrapSteps $Passwords $Database $Operator).Values -join "`n"
}

function Read-PublicPostgresConnections([string]$Directory, $Operator, [string]$RepositoryRoot) {
    $file = Assert-PublicPostgresPrivatePath (Join-Path $Directory 'postgres-connections.json') $RepositoryRoot
    & (Join-Path $RepositoryRoot 'scripts/Protect-QuickPromptSecret.ps1') -Path $file
    try {
        $connections = [IO.File]::ReadAllText($file) | ConvertFrom-Json -AsHashtable
        if ($connections.Count -ne 3) { throw 'Unexpected connection set.' }
        foreach ($entry in @{ migrator='quickprompt_migrator'; application='quickprompt_app'; execution='quickprompt_execution' }.GetEnumerator()) {
            $builder = [Npgsql.NpgsqlConnectionStringBuilder]::new([string]$connections[$entry.Key])
            if ($builder.Host -cne $Operator.Host -or $builder.Port -ne $Operator.Port -or $builder.Database -cne $Operator.Database -or $builder.Username -cne $entry.Value -or $builder.SslMode -ne [Npgsql.SslMode]::VerifyFull -or $builder.IncludeErrorDetail -or $builder.LogParameters -or $builder.Password -notmatch '^[A-F0-9]{64}$') { throw 'Unsafe saved connection.' }
        }
        return $connections
    } catch { throw 'Saved limited connections do not match the explicit database or security contract.' }
}

function Test-PublicPostgresRoleConnections($Connections) {
    foreach ($entry in @{ migrator='quickprompt_migrator'; application='quickprompt_app'; execution='quickprompt_execution' }.GetEnumerator()) {
        $connection = [Npgsql.NpgsqlConnection]::new($Connections[$entry.Key])
        try {
            $connection.Open(); $command = $connection.CreateCommand()
            $command.CommandText = "SELECT current_user=@role AND NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls) AND NOT EXISTS (SELECT FROM pg_auth_members WHERE member=pg_roles.oid) FROM pg_roles WHERE rolname=current_user"
            $null = $command.Parameters.AddWithValue('role',$entry.Value)
            try { if (-not $command.ExecuteScalar()) { throw 'Role authority is broader than allowed.' } } finally { $command.Dispose() }
        } finally { $connection.Dispose() }
    }
}

function Invoke-PublicPostgresMigrations([string]$Sdk, [string]$RepositoryRoot, [string]$MigratorConnection) {
    $previousConnection = $env:QUICKPROMPT_MIGRATION_CONNECTION
    $previousRoot = $env:DOTNET_ROOT; $previousPath = $env:PATH
    Push-Location $RepositoryRoot
    try {
        $env:QUICKPROMPT_MIGRATION_CONNECTION = $MigratorConnection
        $env:DOTNET_ROOT = Split-Path -Parent $Sdk; $env:PATH = "$env:DOTNET_ROOT$([IO.Path]::PathSeparator)$env:PATH"
        # Capture diagnostics because a provider/SQL failure may include sensitive connection or statement data.
        $migrationDiagnostics = & $Sdk ef database update --project backend/QuickPrompt.Accounts --context AccountDatabase --configuration Release --no-build --no-color 2>&1
        if ($LASTEXITCODE -ne 0) {
            $migrationFailure = [InvalidOperationException]::new('Existing EF migrations failed. Keep private credentials; no automatic reset or retry was performed.')
            $migrationFailure.Data['QuickPromptMigrationExitCode'] = $LASTEXITCODE
            throw $migrationFailure
        }
    } finally {
        $migrationDiagnostics = $null; $env:QUICKPROMPT_MIGRATION_CONNECTION = $previousConnection
        $env:DOTNET_ROOT = $previousRoot; $env:PATH = $previousPath; Pop-Location
    }
}

function Test-PublicPostgresMigratedBoundary($Connection) {
    $command = $Connection.CreateCommand()
    $command.CommandText = @'
SELECT
 (SELECT count(*)=5 AND bool_and(pg_get_userbyid(nspowner)='quickprompt_migrator') FROM pg_namespace
  WHERE nspname IN ('qp_identity','qp_library','qp_operations','qp_migrations','qp_host'))
 AND has_table_privilege('quickprompt_app','qp_host.data_protection_keys','SELECT')
 AND has_table_privilege('quickprompt_app','qp_host.data_protection_keys','INSERT')
 AND NOT has_table_privilege('quickprompt_app','qp_host.data_protection_keys','UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
 AND NOT has_schema_privilege('quickprompt_app','qp_host','CREATE')
 AND NOT has_schema_privilege('quickprompt_execution','qp_host','USAGE')
 AND NOT EXISTS (SELECT FROM pg_namespace n,LATERAL aclexplode(COALESCE(n.nspacl,acldefault('n',n.nspowner))) p
                 WHERE n.nspname='qp_host' AND p.grantee=0)
 AND (SELECT count(*)>=20 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
      WHERE n.nspname IN ('qp_identity','qp_library','qp_operations') AND c.relkind='r' AND c.relrowsecurity AND c.relforcerowsecurity);
'@
    try { if (-not $command.ExecuteScalar()) { throw 'Migrated database ownership, tenant RLS or host key boundaries did not pass.' } }
    finally { $command.Dispose() }
}

function Get-PublicPostgresSafeFailure([Exception]$Failure, [string]$Phase, [string]$Step, [bool]$Committed) {
    $phases = @('READ_ONLY_PROBE','PREPARE_SECRETS','ROLE_DDL','ROLE_BOUNDARY','MIGRATIONS','FINAL_BOUNDARY')
    $steps = @('NONE','LOCK','EMPTY_TARGET','CREATE_ROLES','MIGRATOR_MEMBERSHIP','DATABASE_CONNECT','CREATE_SCHEMAS','SCHEMA_PERMISSIONS','DEFAULT_PRIVILEGES','COMMIT','ROLLBACK')
    $states = @('42501','42601','42704','42P01','42P07','42710','42P06','28P01','28000','08001','08004','08006','53300','55P03','57014','40001','40P01','3F000','3D000','0A000')
    $receipt = @{ verdict='FAILED'; phase=if ($Phase -in $phases) { $Phase } else { 'UNKNOWN' };
        step=if ($Step -in $steps) { $Step } else { 'NONE' }; sqlState='NOT_AVAILABLE'; ddlCommitted=$Committed; migrationExitCode=$null }
    $current = $Failure
    for ($depth=0; $null -ne $current -and $depth -lt 12; $depth++) {
        if ($current -is [Npgsql.PostgresException]) { $receipt.sqlState = if ($current.SqlState -in $states) { $current.SqlState } else { 'OTHER' } }
        if ($current.Data.Contains('QuickPromptMigrationExitCode')) {
            $exitCode = $current.Data['QuickPromptMigrationExitCode']
            if ($exitCode -is [int]) { $receipt.migrationExitCode = $exitCode }
        }
        $current = $current.InnerException
    }
    return $receipt
}

Export-ModuleMember -Function Assert-PublicPostgresPrivatePath, Import-PublicPostgresDriver, Read-PublicPostgresOperator, Get-PublicPostgresCapabilities, Assert-PublicPostgresEmptyTarget, New-PublicPostgresSecrets, Get-PublicPostgresBootstrapSql, Get-PublicPostgresBootstrapSteps, Read-PublicPostgresConnections, Test-PublicPostgresRoleConnections, Invoke-PublicPostgresMigrations, Test-PublicPostgresMigratedBoundary, Get-PublicPostgresSafeFailure
