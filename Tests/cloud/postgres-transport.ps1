#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OperatorFile,
    [Parameter(Mandatory)][string]$ExpectedHost,
    [Parameter(Mandatory)][string]$ExpectedDatabase,
    [string]$ReceiptPath
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Import-Module (Join-Path $repository 'deploy/cloud/PublicPostgres.psm1') -Force
$null = Import-PublicPostgresDriver $repository
$settings = Read-PublicPostgresOperator $OperatorFile $ExpectedHost $ExpectedDatabase $repository
$checks = [Collections.Generic.List[string]]::new()
$observedBackendTls = $false
try {
    foreach ($customCallback in @($false, $true)) {
        $connection = [Npgsql.NpgsqlConnection]::new($settings.ConnectionString)
        try {
            # The negative control is a no-op: it never bypasses the driver's
            # validation. Bootstrap must still refuse any custom TLS callback.
            if ($customCallback) {
                $connection.SslClientAuthenticationOptionsCallback = [Action[System.Net.Security.SslClientAuthenticationOptions]]{ param($options) }
            }
            $connection.Open()
            $transaction = $connection.BeginTransaction()
            try {
                $command = $connection.CreateCommand()
                try { $command.CommandText = 'SET TRANSACTION READ ONLY'; $null = $command.ExecuteNonQuery() }
                finally { $command.Dispose() }
                $capabilities = Get-PublicPostgresCapabilities $connection
                if ($capabilities.database -cne $ExpectedDatabase -or $capabilities.tls -eq $customCallback) {
                    throw 'Transport contract did not match the expected security outcome.'
                }
                if (-not $customCallback) {
                    $observedBackendTls = $capabilities.postgresBackendTls
                    $checks.Add('Default VerifyFull connection passes independently of backend pg_stat_ssl')
                } else {
                    $rejected = $false
                    try { Assert-PublicPostgresEmptyTarget $capabilities $ExpectedDatabase $settings.Username }
                    catch { $rejected = $true }
                    if (-not $rejected) { throw 'Custom TLS callback crossed the bootstrap guard.' }
                    $checks.Add('Custom TLS callback cannot authorize bootstrap')
                }
            } finally { $transaction.Rollback(); $transaction.Dispose() }
        } finally { $connection.Dispose() }
    }
    $receipt = @{ verdict='PASS'; checks=$checks.ToArray(); postgresBackendTls=$observedBackendTls; databaseWrites=0; connections=2 }
    $json = $receipt | ConvertTo-Json -Depth 4
    if ($ReceiptPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($ReceiptPath), $json) }
    $json
} catch {
    # Driver errors can contain connection and statement diagnostics.
    throw 'PostgreSQL transport regression failed. No SQL writes were issued; no credential diagnostics are emitted.'
} finally { $settings.Password=''; $settings=$null }
