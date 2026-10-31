#requires -Version 7.2
<#
.SYNOPSIS
Identifies a private QuickPrompt candidate without publishing, building or running tests.
.DESCRIPTION
Begin captures the source inventory before validation. Complete compares it with the
current inventory and references selected artifacts/receipts. Hashes and receipt times
do not attest that a process used those binaries or that the product is ready to release.
#>
[CmdletBinding()]
param(
    [ValidateSet('Begin','Complete')][string]$Mode = 'Begin',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,95}$')][string]$CandidateId = ('quickprompt-' + [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')),
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ArtifactDirectory,
    [string[]]$EvidenceFiles = @()
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
$comparison = if ([OperatingSystem]::IsWindows()) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$rootPrefix = $root + [IO.Path]::DirectorySeparatorChar

function Resolve-CandidatePath([string]$Path, [switch]$MustExist) {
    $absolute = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $root $Path }))
    if (-not $absolute.StartsWith($rootPrefix, $comparison)) { throw 'Candidate paths must remain inside the selected repository.' }
    if ($MustExist -and -not (Test-Path -LiteralPath $absolute)) { throw 'A requested candidate input is missing.' }
    # No source/artifact link can introduce another checkout or a private directory.
    for ($cursor = $absolute; $cursor -ne $root; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
                throw 'Candidate inventory refuses symbolic links and junctions.'
            }
        }
    }
    return $absolute
}

function Relative-CandidatePath([string]$Path) { return [IO.Path]::GetRelativePath($root, $Path).Replace('\','/') }

function Test-PrivateCandidatePath([string]$Relative) {
    $name = [IO.Path]::GetFileName($Relative)
    return $Relative -match '(?i)(^|/)(\.git|node_modules|\.playwright|secrets?|credentials?|private)(/|$)' `
        -or $name -match '(?i)^(\.env($|\.)|\.npmrc$|\.pypirc$|\.netrc$|nuget\.config$|secrets?\.|credentials?\.|.*\.user$)' `
        -or ($name -match '(?i)^appsettings.*\.json$' -and $name -notmatch '(?i)^appsettings\.(template|example)\.json$') `
        -or $name -match '(?i)\.(pem|key|pfx|p12|cer|sqlite|sqlite-wal|sqlite-shm|db|db3|dump|bak|log|binlog)$'
}

function Get-CandidateFile([string]$Path) {
    $file = Get-Item -LiteralPath $Path
    if ($file.PSIsContainer) { throw 'A file was required for this inventory entry.' }
    return [ordered]@{ path = Relative-CandidatePath $Path; bytes = $file.Length; sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
}

function Get-CandidateSources {
    $files = & git -C $root -c core.quotePath=false ls-files --cached --others --exclude-standard -z
    if ($LASTEXITCODE -ne 0) { throw 'The selected directory is not a readable Git worktree.' }
    $inventory = [Collections.Generic.List[object]]::new()
    $excluded = 0
    foreach ($relative in @((($files -join "`n") -split "`0") | Where-Object { $_ } | Sort-Object -Unique -CaseSensitive)) {
        if ($relative -match '[\r\n]' -or [IO.Path]::IsPathRooted($relative)) { throw 'Unsupported source path in Git inventory.' }
        if (Test-PrivateCandidatePath $relative) { $excluded++; continue }
        $path = Resolve-CandidatePath $relative
        # Tracked removals are represented by absence, so they also change the source hash.
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        $inventory.Add((Get-CandidateFile $path))
    }
    if ($inventory.Count -eq 0) { throw 'The allowed source inventory is empty.' }
    $canonical = $inventory | ConvertTo-Json -Depth 5 -Compress
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical))).ToLowerInvariant()
    return [ordered]@{ sha256 = $hash; excludedFiles = $excluded; files = @($inventory.ToArray()) }
}

function Get-CandidateReceipt([string]$Path, [DateTimeOffset]$WindowStart, [DateTimeOffset]$WindowEnd) {
    $absolute = Resolve-CandidatePath $Path -MustExist
    $relative = Relative-CandidatePath $absolute
    if (-not $relative.StartsWith('.playwright/', [StringComparison]::Ordinal) -or [IO.Path]::GetExtension($absolute) -notin @('.trx','.json')) {
        throw 'Only explicit TRX/JSON receipts under .playwright may be referenced.'
    }
    if (Test-PrivateCandidatePath $relative.Substring('.playwright/'.Length)) { throw 'Private configuration is not a validation receipt.' }
    $receipt = Get-CandidateFile $absolute
    $receipt['window'] = 'UNCONFIRMED'
    $receipt['assessment'] = 'REFERENCE_ONLY_NOT_A_PRODUCT_VERDICT'
    if ([IO.Path]::GetExtension($absolute) -eq '.trx') {
        $settings = [Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
        $settings.MaxCharactersInDocument = 32 * 1024 * 1024
        $reader = [Xml.XmlReader]::Create($absolute, $settings)
        try {
            $document = [Xml.XmlDocument]::new(); $document.Load($reader)
            $times = $document.SelectSingleNode("/*[local-name()='TestRun']/*[local-name()='Times']")
            $counters = $document.SelectSingleNode("/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
            if ($null -ne $counters) {
                $receipt['counters'] = [ordered]@{ total = [int]$counters.total; passed = [int]$counters.passed; failed = [int]$counters.failed; executed = [int]$counters.executed }
            }
            if ($null -ne $times) {
                $start = [DateTimeOffset]::Parse($times.start); $finish = [DateTimeOffset]::Parse($times.finish)
                $receipt['window'] = if ($start -ge $WindowStart -and $finish -le $WindowEnd -and $finish -ge $start) { 'WITHIN_BASELINE_WINDOW' } else { 'OUTSIDE_BASELINE_WINDOW' }
            }
        } finally { $reader.Dispose() }
    }
    return $receipt
}

if (-not (Test-Path -LiteralPath (Join-Path $root '.git'))) { throw 'Select the actual QuickPrompt Git worktree.' }
$packagePath = Resolve-CandidatePath 'apps/web/package.json' -MustExist
$version = (Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json).version
if ($version -notmatch '^\d+\.\d+\.\d+([+-][0-9A-Za-z.-]+)?$') { throw 'The visible application version is invalid.' }
$output = Resolve-CandidatePath ('.playwright/candidates/' + $CandidateId)
$file = Join-Path $output $(if ($Mode -eq 'Begin') { 'baseline.json' } else { 'candidate.json' })
if (Test-Path -LiteralPath $file) { throw 'This candidate phase already exists; existing evidence is never overwritten.' }
$head = & git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'The worktree has no readable base commit.' }
$status = & git -C $root status --porcelain=v1 --untracked-files=normal
if ($LASTEXITCODE -ne 0) { throw 'The worktree status is unavailable.' }
$now = [DateTimeOffset]::UtcNow
$sources = Get-CandidateSources
$manifest = [ordered]@{
    schemaVersion = 1; purpose = 'QUICKPROMPT_PRIVATE_CANDIDATE'; candidateId = $CandidateId; phase = $Mode
    createdAtUtc = $now.ToString('O'); version = $version; baseCommit = $head.Trim(); dirty = [bool]$status
    sources = $sources; status = 'BASELINE_CAPTURED'; releaseVerdict = 'NOT_ASSESSED'
    dependencyInputs = @($sources.files | Where-Object { $_.path -match '(^|/)(package(-lock)?\.json|global\.json|packages\.lock\.json|Directory\.Packages\.props|dotnet-tools\.json)$' -or $_.path.EndsWith('.csproj', [StringComparison]::OrdinalIgnoreCase) })
    limitations = @('Metadata only: no source archive, build, model, database, publication or test is performed.',
        'Private/configuration paths are excluded. This is not a scanner for secrets embedded in allowed source files.',
        'Equal beginning/end hashes do not monitor intermediate source modifications.',
        'Receipt timing and file hashes do not attest which artifacts a process executed or certify commercial readiness.')
}
if ($Mode -eq 'Complete') {
    $baselinePath = Resolve-CandidatePath (Join-Path $output 'baseline.json') -MustExist
    $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    if ($baseline.purpose -ne $manifest.purpose -or $baseline.phase -ne 'Begin' -or $baseline.candidateId -ne $CandidateId) { throw 'Candidate baseline identity differs.' }
    $manifest['baselineSha256'] = (Get-FileHash -LiteralPath $baselinePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifest['sourcesUnchanged'] = $baseline.sources.sha256 -eq $sources.sha256 -and $baseline.baseCommit -eq $manifest.baseCommit
    $manifest['status'] = if ($manifest.sourcesUnchanged) { 'SOURCE_STABLE_REVIEW_REQUIRED' } else { 'SOURCE_CHANGED_REVALIDATE' }
    $artifacts = [Collections.Generic.List[object]]::new()
    if ($ArtifactDirectory) {
        $artifactRoot = Resolve-CandidatePath $ArtifactDirectory -MustExist
        if (-not (Test-Path -LiteralPath $artifactRoot -PathType Container)) { throw 'ArtifactDirectory must be a directory.' }
        $artifactRelative = Relative-CandidatePath $artifactRoot
        if ($artifactRelative -notmatch '^(\.playwright/|backend/[^/]+/bin/Release(/|$)|apps/web/dist($|/))') {
            throw 'Artifacts must come from private QA output or the explicit Release/web build output.'
        }
        foreach ($artifact in Get-ChildItem -LiteralPath $artifactRoot -Recurse -Force) {
            $path = Resolve-CandidatePath $artifact.FullName -MustExist
            if ($artifact.PSIsContainer) { continue }
            $relative = Relative-CandidatePath $path
            # Private output lives under .playwright, but names inside it still exclude keys/config/data.
            $withinArtifact = [IO.Path]::GetRelativePath($artifactRoot, $path).Replace('\','/')
            if (Test-PrivateCandidatePath $withinArtifact) { continue }
            if ($artifact.Extension -in @('.dll','.exe','.pdb','.json','.html','.js','.css','.svg','.png','.ico','.woff','.woff2','.webp')) { $artifacts.Add((Get-CandidateFile $path)) }
        }
    }
    $manifest['artifacts'] = @($artifacts.ToArray() | Sort-Object path -CaseSensitive)
    if ($EvidenceFiles.Count -gt 200) { throw 'Reference at most 200 explicit receipts.' }
    $manifest['evidence'] = @($EvidenceFiles | Sort-Object -Unique -CaseSensitive | ForEach-Object { Get-CandidateReceipt $_ ([DateTimeOffset]::Parse($baseline.createdAtUtc)) $now })
}
[IO.Directory]::CreateDirectory($output) | Out-Null
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $file -Encoding utf8
[ordered]@{ candidateId = $CandidateId; phase = $Mode; status = $manifest.status; sourceSha256 = $sources.sha256; files = $sources.files.Count; manifest = Relative-CandidatePath $file; releaseVerdict = $manifest.releaseVerdict } | ConvertTo-Json
if ($Mode -eq 'Complete' -and -not $manifest.sourcesUnchanged) { exit 2 }
