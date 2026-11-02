#requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$cloudRepository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd([IO.Path]::DirectorySeparatorChar)
if (-not [IO.Path]::IsPathFullyQualified($OutputDirectory)) { throw 'Use an absolute private directory outside the repository.' }
$cloudPrivate = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
function Assert-PhysicalAncestors([string]$Path) {
    $ancestor = [IO.DirectoryInfo]::new($Path)
    while ($null -ne $ancestor) {
        if ($ancestor.Exists -and ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Use physical local paths without symbolic links or junctions for hosting secrets.'
        }
        $ancestor = $ancestor.Parent
    }
}
# Lexical containment alone does not prevent an outside junction from targeting this repository.
Assert-PhysicalAncestors $cloudRepository
Assert-PhysicalAncestors $cloudPrivate
if ($cloudPrivate -eq $cloudRepository -or $cloudPrivate.StartsWith($cloudRepository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Hosting secrets must stay outside the repository.'
}
if (Test-Path -LiteralPath $cloudPrivate) { throw 'Choose a new private directory. Existing keys are never overwritten or rotated implicitly.' }
[IO.Directory]::CreateDirectory($cloudPrivate) | Out-Null
$cloudProtector = Join-Path $cloudRepository 'scripts/Protect-QuickPromptSecret.ps1'
function New-PrivateFile([string]$Name) {
    $file = Join-Path $cloudPrivate $Name
    $stream = [IO.File]::Open($file, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $stream.Dispose()
    & $cloudProtector -Path $file
    return $file
}
foreach ($cloudName in @('signing.pem','encryption.pem')) {
    $cloudPath = New-PrivateFile $cloudName
    $cloudKey = [Security.Cryptography.RSA]::Create(3072)
    try { [IO.File]::WriteAllText($cloudPath, $cloudKey.ExportPkcs8PrivateKeyPem()) }
    finally { $cloudKey.Dispose() }
}
$cloudPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$cloudPasswordPath = New-PrivateFile 'key-ring-password.txt'
[IO.File]::WriteAllText($cloudPasswordPath, $cloudPassword)
$cloudCertificatePath = New-PrivateFile 'key-ring.pfx'
$cloudBase64Path = New-PrivateFile 'key-ring-base64.txt'
$cloudCertificateKey = [Security.Cryptography.RSA]::Create(3072)
try {
    $cloudRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=QuickPrompt host key encryption', $cloudCertificateKey,
        [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $cloudCertificate = $cloudRequest.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddYears(2))
    try {
        $cloudPfx = $cloudCertificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $cloudPassword)
        [IO.File]::WriteAllBytes($cloudCertificatePath, $cloudPfx)
        [IO.File]::WriteAllText($cloudBase64Path, [Convert]::ToBase64String($cloudPfx))
    } finally { $cloudCertificate.Dispose() }
} finally { $cloudCertificateKey.Dispose(); $cloudPassword = $null; $cloudPfx = $null }
# Only the directory is returned. No certificate trust, cloud upload or secret-bearing process argument is created.
Write-Output $cloudPrivate
