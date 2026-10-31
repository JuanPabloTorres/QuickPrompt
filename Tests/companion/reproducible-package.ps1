#requires -Version 7.2
<#
.SYNOPSIS
Compares two independently built Companion archives without extracting or executing them.
.DESCRIPTION
Requires identical entry names, uncompressed payload and complete ZIP bytes. The
caller supplies separate-build receipts; equal files alone do not prove those builds ran.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$FirstZip,
    [Parameter(Mandatory)][string]$SecondZip
)
$ErrorActionPreference = 'Stop'
$first = (Get-Item -LiteralPath $FirstZip).FullName
$second = (Get-Item -LiteralPath $SecondZip).FullName
if ($first -eq $second) { throw 'Select archives from two separate builds.' }

function Get-PayloadHashes([string]$Path) {
    $result = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        foreach ($entry in $archive.Entries) {
            if (-not $entry.Name -or -not $result.TryAdd($entry.FullName,'')) {
                throw 'The package must contain unique file entries, without directory entries.'
            }
            $stream = $entry.Open()
            try {
                $result[$entry.FullName] = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
            } finally { $stream.Dispose() }
        }
    } finally { $archive.Dispose() }
    return ,$result
}

$a = Get-PayloadHashes $first
$b = Get-PayloadHashes $second
$names = [Collections.Generic.HashSet[string]]::new($a.Keys,[StringComparer]::Ordinal)
$names.UnionWith($b.Keys)
$differences = @($names | Sort-Object -CaseSensitive | Where-Object {
    -not $a.ContainsKey($_) -or -not $b.ContainsKey($_) -or $a[$_] -cne $b[$_]
})
$hashA = (Get-FileHash -LiteralPath $first -Algorithm SHA256).Hash.ToLowerInvariant()
$hashB = (Get-FileHash -LiteralPath $second -Algorithm SHA256).Hash.ToLowerInvariant()
$passed = $a.Count -gt 0 -and $differences.Count -eq 0 -and $hashA -ceq $hashB
[ordered]@{
    schema = 'quickprompt-reproducible-package-check/1'
    status = $(if ($passed) { 'PASS' } else { 'FAIL' })
    filesA = $a.Count; filesB = $b.Count
    payloadDifferences = $differences
    firstZipSha256 = $hashA; secondZipSha256 = $hashB
    identicalZipBytes = $hashA -ceq $hashB
    execution = 'Read-only ZIP hashing; no extraction, installation, model or Companion start'
} | ConvertTo-Json -Depth 4
if (-not $passed) { exit 1 }
