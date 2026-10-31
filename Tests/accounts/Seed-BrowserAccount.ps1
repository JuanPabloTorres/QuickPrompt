#requires -Version 7.2
param([ValidateRange(1024,65535)][int]$Port = 5300)
$ErrorActionPreference = 'Stop'
$qaRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$qaOrigin = "http://127.0.0.1:$Port"
$qaRuntime = Get-Content -LiteralPath (Join-Path $qaRoot ".playwright/accounts-runtime/runtime-$Port.json") -Raw | ConvertFrom-Json
if ($qaRuntime.origin -ne $qaOrigin -or $qaRuntime.database -ne 'quickprompt_development' -or $qaRuntime.googleConfigured) {
    throw 'Este arnés sólo crea cuentas sintéticas en la instancia HTTP de QA identificada; nunca en la biblioteca personal.'
}
$qaHealth = Invoke-RestMethod "$qaOrigin/health"
$qaOptions = Invoke-RestMethod "$qaOrigin/account/options"
if ($qaHealth.mode -ne 'accounts-candidate' -or $qaOptions.mailDelivery -ne 'development-pickup') { throw 'No se verificó el perfil sintético con buzón local.' }
$qaSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$qaEmail = 'qa-browser-' + [Guid]::NewGuid().ToString('N') + '@example.invalid'
# Public, disposable fixture password. It must never be used for a personal account.
$qaRegistration = @{ email=$qaEmail; password='Synthetic-Account!79' } | ConvertTo-Json -Compress
$qaToken = (Invoke-RestMethod "$qaOrigin/account/csrf" -WebSession $qaSession).token
$null = Invoke-RestMethod "$qaOrigin/account/register" -Method Post -ContentType application/json -Headers @{'X-QP-CSRF'=$qaToken} -WebSession $qaSession -Body $qaRegistration
$qaMailboxId = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($qaEmail.ToUpperInvariant()))).ToLowerInvariant()
$qaMailbox = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('QuickPromptNova/saas-development/mail/' + $qaMailboxId)
$qaOwnMessages = @(Get-ChildItem -LiteralPath $qaMailbox -Filter '*.json')
if ($qaOwnMessages.Count -ne 1) { throw 'No se identificó exactamente un mensaje para esta cuenta sintética.' }
$qaMessage = [IO.File]::ReadAllText($qaOwnMessages[0].FullName) | ConvertFrom-Json
if ($qaMessage.Email -ne $qaEmail -or $qaMessage.Purpose -ne 'confirm') { throw 'El mensaje no corresponde a la cuenta sintética.' }
$qaQuery = ([Uri]$qaMessage.Link).Query.TrimStart('?').Split('&')
$qaCode = [Uri]::UnescapeDataString(($qaQuery | Where-Object { $_.StartsWith('code=') }).Substring(5))
$qaUserId = [Uri]::UnescapeDataString(($qaQuery | Where-Object { $_.StartsWith('userId=') }).Substring(7))
$qaToken = (Invoke-RestMethod "$qaOrigin/account/csrf" -WebSession $qaSession).token
$null = Invoke-RestMethod "$qaOrigin/account/confirm" -Method Post -ContentType application/json -Headers @{'X-QP-CSRF'=$qaToken} -WebSession $qaSession -Body (@{userId=$qaUserId;code=$qaCode} | ConvertTo-Json -Compress)
# No password, token, cookie or unrelated mailbox is returned or persisted.
[pscustomobject]@{ email=$qaEmail; confirmed=$true; database='quickprompt_development'; port=$Port; mailProvider='private-development-pickup' }
