#requires -Version 7.2
param([string]$SecretDirectory,
    [ValidateSet('quickprompt_development','quickprompt_accounts_local')][string]$DatabaseName = 'quickprompt_development')
$ErrorActionPreference = 'Stop'
$pgProject = 'quickprompt-saas-development'
$pgContainer = 'quickprompt-saas-postgres'
$pgDatabase = $DatabaseName
$pgRoot = Split-Path -Parent $PSScriptRoot
if (-not $SecretDirectory) {
    $pgLocalData = [Environment]::GetFolderPath('LocalApplicationData')
    if (-not $pgLocalData) { throw 'Indica un directorio privado para los secretos de desarrollo.' }
    $SecretDirectory = Join-Path $pgLocalData 'QuickPromptNova/saas-development/secrets'
}
$SecretDirectory = [IO.Path]::GetFullPath($SecretDirectory)
[IO.Directory]::CreateDirectory($SecretDirectory) | Out-Null

$pgSecrets = @{}
foreach ($pgName in @('bootstrap', 'migrator', 'application', 'execution')) {
    $pgPath = Join-Path $SecretDirectory "postgres-$pgName.txt"
    if (-not (Test-Path -LiteralPath $pgPath)) {
        [IO.File]::WriteAllText($pgPath, [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)))
    }
    & (Join-Path $PSScriptRoot 'Protect-QuickPromptSecret.ps1') -Path $pgPath
    $pgValue = [IO.File]::ReadAllText($pgPath).Trim()
    if ($pgValue -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Formato de secreto de desarrollo inesperado; no se sustituirá.' }
    $pgSecrets[$pgName] = $pgValue
}
$pgExisting = & docker ps -a --filter "name=^/$pgContainer$" --format '{{.Names}}'
if ($LASTEXITCODE -ne 0) { throw 'Docker no está disponible.' }
if ($pgExisting) {
    $pgOwner = & docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' $pgContainer
    $pgScope = & docker inspect --format '{{index .Config.Labels "io.quickprompt.scope"}}' $pgContainer
    if ($pgOwner -ne $pgProject -or $pgScope -ne 'saas-development') { throw 'El nombre pertenece a otro contenedor; no se modificó.' }
} else {
    $pgPortProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 55432)
    try { $pgPortProbe.Start() }
    catch { throw 'El puerto de desarrollo está ocupado por otra instancia.' }
    finally { $pgPortProbe.Stop() }
}
$pgPreviousSecret = $env:QUICKPROMPT_POSTGRES_SECRET_FILE
try {
    $env:QUICKPROMPT_POSTGRES_SECRET_FILE = Join-Path $SecretDirectory 'postgres-bootstrap.txt'
    & docker compose -p $pgProject -f (Join-Path $pgRoot 'deploy/postgres/compose.yml') up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar el PostgreSQL de desarrollo.' }
} finally { $env:QUICKPROMPT_POSTGRES_SECRET_FILE = $pgPreviousSecret }

# Keep human development sign-in separate from the synthetic integration database.
if ($pgDatabase -eq 'quickprompt_accounts_local') {
    $pgExists = & docker exec $pgContainer psql -X -U quickprompt_bootstrap -d quickprompt_development -Atc "SELECT 1 FROM pg_database WHERE datname='quickprompt_accounts_local';"
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo comprobar la base de desarrollo de cuentas.' }
    if ($pgExists -ne '1') {
        & docker exec $pgContainer createdb -U quickprompt_bootstrap -O quickprompt_bootstrap quickprompt_accounts_local
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear la base separada de cuentas.' }
    }
}

# Only generated hex secrets are interpolated into this fixed operator-owned SQL.
# Do not echo SQL or database errors: a rejected statement could include a secret.
$pgSql = @'
DO $roles$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'quickprompt_migrator') THEN
    CREATE ROLE quickprompt_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '__MIGRATOR__';
  END IF;
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'quickprompt_app') THEN
    CREATE ROLE quickprompt_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '__APPLICATION__';
  END IF;
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'quickprompt_execution') THEN
    CREATE ROLE quickprompt_execution LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '__EXECUTION__';
  END IF;
END
$roles$;
GRANT CONNECT ON DATABASE __DATABASE__ TO quickprompt_migrator, quickprompt_app, quickprompt_execution;
CREATE SCHEMA IF NOT EXISTS qp_identity AUTHORIZATION quickprompt_migrator;
CREATE SCHEMA IF NOT EXISTS qp_library AUTHORIZATION quickprompt_migrator;
CREATE SCHEMA IF NOT EXISTS qp_operations AUTHORIZATION quickprompt_migrator;
CREATE SCHEMA IF NOT EXISTS qp_migrations AUTHORIZATION quickprompt_migrator;
CREATE SCHEMA IF NOT EXISTS qp_host AUTHORIZATION quickprompt_migrator;
REVOKE ALL ON SCHEMA qp_host FROM PUBLIC, quickprompt_execution;
REVOKE ALL ON SCHEMA qp_identity, qp_library, qp_operations, qp_migrations FROM PUBLIC;
GRANT USAGE ON SCHEMA qp_identity, qp_library, qp_operations TO quickprompt_app;
GRANT USAGE ON SCHEMA qp_operations TO quickprompt_execution;
ALTER DEFAULT PRIVILEGES FOR ROLE quickprompt_migrator IN SCHEMA qp_identity, qp_library, qp_operations
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO quickprompt_app;
ALTER DEFAULT PRIVILEGES FOR ROLE quickprompt_migrator IN SCHEMA qp_identity, qp_library, qp_operations
  GRANT USAGE, SELECT ON SEQUENCES TO quickprompt_app;
'@
$pgSql = $pgSql.Replace('__MIGRATOR__', $pgSecrets.migrator).Replace('__APPLICATION__', $pgSecrets.application).Replace('__EXECUTION__', $pgSecrets.execution).Replace('__DATABASE__', $pgDatabase)
$pgResult = $pgSql | & docker exec -i $pgContainer psql -X -q -v ON_ERROR_STOP=1 -U quickprompt_bootstrap -d $pgDatabase 2>&1
if ($LASTEXITCODE -ne 0) { throw 'La inicialización de roles/esquemas falló. No se imprimieron SQL ni credenciales.' }

foreach ($pgRole in @('migrator', 'application', 'execution')) {
    $pgPreviousPassword = $env:PGPASSWORD
    try {
        $env:PGPASSWORD = $pgSecrets[$pgRole]
        $pgUser = switch ($pgRole) { 'application' { 'quickprompt_app' }; 'execution' { 'quickprompt_execution' }; default { 'quickprompt_migrator' } }
        $pgAuth = & docker exec --env PGPASSWORD $pgContainer psql -X -h 127.0.0.1 -U $pgUser -d $pgDatabase -Atc 'SELECT current_user;' 2>&1
        if ($LASTEXITCODE -ne 0 -or $pgAuth -ne $pgUser) { throw 'El secreto local no corresponde al rol existente; no se rotó automáticamente.' }
    } finally { $env:PGPASSWORD = $pgPreviousPassword }
}
$pgMetadata = @{ container = $pgContainer; host = '127.0.0.1'; port = 55432; database = $pgDatabase; migratorUser = 'quickprompt_migrator'; applicationUser = 'quickprompt_app'; executionUser = 'quickprompt_execution'; migratorSecretFile = 'postgres-migrator.txt'; applicationSecretFile = 'postgres-application.txt'; executionSecretFile = 'postgres-execution.txt' }
$pgMetadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $SecretDirectory 'postgres-development.json')
Write-Host 'PostgreSQL de desarrollo listo en 127.0.0.1:55432; roles migrator, application y execution verificados por TCP. Sin superuser/BYPASSRLS para aplicación o dispatcher.'
