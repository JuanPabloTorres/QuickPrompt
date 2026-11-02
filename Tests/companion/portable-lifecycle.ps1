#requires -Version 7.2
param(
    [Parameter(Mandatory)][string]$PreviousZip,
    [Parameter(Mandatory)][string]$CandidateZip,
    [Parameter(Mandatory)][string]$Python,
    [Parameter(Mandatory)][string]$Dotnet,
    [Parameter(Mandatory)][string]$EvidenceDirectory
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This lifecycle check requires Windows and DPAPI.' }
foreach ($inputFile in @($PreviousZip, $CandidateZip, $Python, $Dotnet)) {
    if (-not (Test-Path -LiteralPath $inputFile -PathType Leaf)) { throw 'An explicit lifecycle input is missing.' }
}
if (Test-Path -LiteralPath $EvidenceDirectory) { throw 'Use a new evidence directory; prior receipts are not overwritten.' }
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('QuickPromptPortableLifecycle-' + [guid]::NewGuid().ToString('N'))
$versions = Join-Path $scratch 'versions'
$settings = Join-Path $scratch 'settings'
$private = Join-Path $scratch 'synthetic-private'
$null = New-Item -ItemType Directory -Path $versions, $settings, $private
$previous = Join-Path $versions 'previous'
$candidate = Join-Path $versions 'candidate'
$configPath = Join-Path $settings 'companion.json'
$sentinelPath = Join-Path $private 'synthetic-not-a-grant.dpapi'
$checks = [Collections.Generic.List[object]]::new()
$shell = (Get-Process -Id $PID).Path

function Check([string]$Name, [scriptblock]$Body) {
    & $Body
    $checks.Add([pscustomobject]@{ name=$Name; status='PASS' })
}

function Assert-PreservedSettings {
    if ((Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash -cne $configurationHash -or
        (Get-FileHash -LiteralPath $sentinelPath -Algorithm SHA256).Hash -cne $sentinelHash -or
        (Get-Acl -LiteralPath $configPath).Sddl -cne $configurationAcl -or
        (Get-Acl -LiteralPath $sentinelPath).Sddl -cne $sentinelAcl) {
        throw 'Portable lifecycle changed the external configuration or synthetic private fixture.'
    }
}

function Invoke-OfflineDoctor([string]$Payload) {
    $raw = & $shell -NoProfile -File (Join-Path $Payload 'Companion.ps1') -Command doctor -ConfigPath $configPath
    $exitCode = $LASTEXITCODE
    $doctor = $raw | ConvertFrom-Json
    if ($exitCode -ne 0 -or $doctor.status -cne 'PREREQUISITES_READY' -or
        $doctor.provider -cne 'docker-model-runner' -or $doctor.providerChecked -or
        $doctor.authorizationVerified -or $doctor.inferenceVerified -or
        $doctor.workerStarted -or $doctor.servicesStarted) {
        throw 'The offline doctor failed or claimed an unauthorized runtime/network operation.'
    }
    Assert-PreservedSettings
}

function Remove-OwnedPayload([string]$Payload) {
    # Deletion is limited to one of the two exact directories this invocation
    # created. Reject links before recursive deletion; never touch account stores.
    $resolved = (Resolve-Path -LiteralPath $Payload).Path
    $allowed = @([IO.Path]::GetFullPath($previous), [IO.Path]::GetFullPath($candidate))
    $boundary = [IO.Path]::GetFullPath($versions).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($resolved -notin $allowed -or -not $resolved.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase) -or
        (Get-Item -LiteralPath $scratch).LinkTarget -or (Get-Item -LiteralPath $versions).LinkTarget -or
        (Get-Item -LiteralPath $resolved).LinkTarget -or
        @(Get-ChildItem -LiteralPath $resolved -Recurse -Force | Where-Object LinkTarget).Count -gt 0) {
        throw 'Refusing an unexpected or linked payload removal target.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    if (Test-Path -LiteralPath $resolved) { throw 'The owned payload was not removed.' }
}

Check 'Both exported payloads extract into new separate directories and pass their own manifest' {
    [IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($PreviousZip), $previous)
    [IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($CandidateZip), $candidate)
    foreach ($payload in @($previous, $candidate)) {
        . (Join-Path $payload 'Companion.Package.ps1')
        Assert-CompanionPackage $payload
    }
}

Check 'One external configuration resolves runtime paths independently of either payload' {
    $config = Get-Content -LiteralPath (Join-Path $candidate 'companion.dmr.connected.example.json') -Raw | ConvertFrom-Json -AsHashtable
    $config.server = 'https://portable-lifecycle.example.invalid/'
    $config.slot = 'qa-lifecycle-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $config.python = [IO.Path]::GetRelativePath($settings, [IO.Path]::GetFullPath($Python))
    $config.dotnet = [IO.Path]::GetRelativePath($settings, [IO.Path]::GetFullPath($Dotnet))
    $config | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding utf8
    . (Join-Path $candidate 'Companion.Package.ps1')
    $parsed = Read-CompanionConfiguration $configPath
    if ((Resolve-CompanionExecutable $parsed.python 'python' $settings) -cne [IO.Path]::GetFullPath($Python) -or
        (Resolve-CompanionExecutable $parsed.dotnet 'dotnet' $settings) -cne [IO.Path]::GetFullPath($Dotnet)) {
        throw 'Runtime paths were not resolved against the configuration directory.'
    }
}

# This is inert fixture data with no account, refresh token or access grant.
# It proves file/DPAPI preservation, not compatibility of a live authorization.
$clear = [Text.Encoding]::UTF8.GetBytes('SYNTHETIC_NOT_AN_AUTHORIZATION')
$purpose = [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
try {
    $encrypted = [Security.Cryptography.ProtectedData]::Protect($clear, $purpose, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    [IO.File]::WriteAllBytes($sentinelPath, $encrypted)
} finally { [Security.Cryptography.CryptographicOperations]::ZeroMemory($clear) }
$configurationHash = (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash
$sentinelHash = (Get-FileHash -LiteralPath $sentinelPath -Algorithm SHA256).Hash
$configurationAcl = (Get-Acl -LiteralPath $configPath).Sddl
$sentinelAcl = (Get-Acl -LiteralPath $sentinelPath).Sddl

Check 'Payload removal refuses the external settings directory before deleting anything' {
    $rejected = $false
    try { Remove-OwnedPayload $settings } catch { $rejected = $true }
    if (-not $rejected) { throw 'The removal guard accepted the external settings directory.' }
    Assert-PreservedSettings
}
Check 'Previous payload accepts the external configuration without account or provider requests' { Invoke-OfflineDoctor $previous }
Check 'Candidate accepts the same configuration unchanged without pairing or inference' { Invoke-OfflineDoctor $candidate }
Check 'Rollback to the previous payload preserves the same configuration and private fixture' { Invoke-OfflineDoctor $previous }
Check 'Removing only the candidate payload preserves the external configuration and private fixture' {
    Remove-OwnedPayload $candidate
    Assert-PreservedSettings
}
Check 'Removing only the previous payload preserves the external configuration and private fixture' {
    Remove-OwnedPayload $previous
    Assert-PreservedSettings
}
Check 'The synthetic fixture remains decryptable by the same Windows user after payload removal' {
    $restored = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($sentinelPath), $purpose,
        [Security.Cryptography.DataProtectionScope]::CurrentUser)
    try {
        if ([Text.Encoding]::UTF8.GetString($restored) -cne 'SYNTHETIC_NOT_AN_AUTHORIZATION') { throw 'Synthetic DPAPI fixture changed.' }
    } finally { [Security.Cryptography.CryptographicOperations]::ZeroMemory($restored) }
}

$receipt = [ordered]@{
    schema=1; scope='Actual ZIP extraction, offline doctors, filesystem update/rollback/removal on existing Windows prerequisites; not a clean OS or live account acceptance'
    scriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
    previousZipSha256=(Get-FileHash -LiteralPath $PreviousZip -Algorithm SHA256).Hash.ToLowerInvariant()
    candidateZipSha256=(Get-FileHash -LiteralPath $CandidateZip -Algorithm SHA256).Hash.ToLowerInvariant()
    passed=$checks.Count; failed=0; checks=$checks.ToArray(); fixtureDirectory=$scratch
    authenticatedWorkerStarts=0; providerRequests=0; accountRequests=0; modelCalls=0
    realCredentialsRead=$false; authorizationRevocationVerified=$false; activeProcessUpgradeVerified=$false
    externalConfigurationPreserved=$true; syntheticEncryptedFixturePreserved=$true
    externalFilePermissionsPreserved=$true
}
$null = New-Item -ItemType Directory -Path $EvidenceDirectory
$receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'portable-lifecycle.json') -Encoding utf8
$receipt | ConvertTo-Json -Depth 5
$global:LASTEXITCODE = 0
