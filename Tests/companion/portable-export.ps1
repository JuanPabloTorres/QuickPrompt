#requires -Version 7.2
param(
    [Parameter(Mandatory)][string]$PackageZip,
    [Parameter(Mandatory)][string]$Python,
    [Parameter(Mandatory)][string]$Dotnet,
    [Parameter(Mandatory)][string]$EvidenceDirectory
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('QuickPromptPortableExport-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $temporary
$results = [Collections.Generic.List[object]]::new()
function Check([string]$Name,[scriptblock]$Body) { & $Body; $results.Add([pscustomobject]@{name=$Name;status='PASS'}) }
function Reject([scriptblock]$Body) { $rejected=$false;try { & $Body | Out-Null } catch {$rejected=$true};if(-not $rejected){throw 'Expected package rejection.'} }
try {
    $export=Join-Path $temporary 'QuickPrompt Companion relocated'
    [IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($PackageZip),$export)
    . (Join-Path $export 'Companion.Package.ps1')
    Check 'Clean ZIP extracted outside the repository verifies all captured files' { Assert-CompanionPackage $export }
    $packageManifest=Get-Content -LiteralPath (Join-Path $export 'package-manifest.json') -Raw | ConvertFrom-Json
    Check 'Package and provenance retain the same explicit application RC version' {
        $provenance=Get-Content -LiteralPath (Join-Path $export 'provenance.json') -Raw | ConvertFrom-Json
        $appVersion=(Get-Content -LiteralPath (Join-Path $root 'apps/web/package.json') -Raw | ConvertFrom-Json).version
        if($packageManifest.packageVersion -ne $appVersion -or $provenance.packageVersion -ne $appVersion){throw 'Package version does not match the reviewed application candidate.'}
    }
    Check 'Export contains no credentials, user settings, venv, databases or build debug symbols' {
        foreach($file in (Get-ChildItem -LiteralPath $export -Recurse -File)) {
            $relative=[IO.Path]::GetRelativePath($export,$file.FullName).Replace('\','/')
            if($relative -match '(^|/)(\.venv|__pycache__|node_modules|\.git|companion\.json|session\.dpapi|active\.lock|\.env)(/|$)|\.(pfx|pem|sqlite|db|pdb|pyc)$'){throw 'Forbidden export content.'}
            if($file.Extension -in @('.json','.py','.ps1','.txt','.md','.toml')) {
                $content=Get-Content -LiteralPath $file.FullName -Raw
                if($content.Contains($root) -or $content.Contains($env:USERPROFILE)){throw 'Developer path in export text.'}
            }
            if($file.Extension -eq '.dll') {
                $bytes=[IO.File]::ReadAllBytes($file.FullName)
                foreach($encoding in @([Text.Encoding]::UTF8,[Text.Encoding]::Unicode)) {
                    $content=$encoding.GetString($bytes)
                    if($content.Contains($root) -or $content.Contains($env:USERPROFILE)){throw 'Developer path embedded in export binary.'}
                }
            }
        }
    }
    $configPath=Join-Path $export 'companion.json'
    $config=Get-Content -LiteralPath (Join-Path $export 'companion.example.json') -Raw | ConvertFrom-Json -AsHashtable
    $config.python='missing-python.exe';$config.dotnet=$Dotnet
    $config | ConvertTo-Json | Set-Content -LiteralPath $configPath
    Check 'Missing explicit Python blocks doctor without service startup or authorization' {
        $raw=& (Get-Process -Id $PID).Path -NoProfile -File (Join-Path $export 'Companion.ps1') -Command doctor
        $code=$LASTEXITCODE;$doctor=$raw | ConvertFrom-Json
        if($code -ne 2 -or $doctor.status -ne 'BLOCKED' -or $doctor.workerStarted -or $doctor.servicesStarted -or $doctor.providerChecked -or $doctor.authorizationVerified){throw 'Missing prerequisite did not fail closed.'}
    }
    Check 'Shared development entry point preserves the portable doctor failure exit code' {
        $raw=& (Get-Process -Id $PID).Path -NoProfile -File (Join-Path $root 'scripts/Start-QuickPromptCompanion.ps1') -Command doctor -PackageDirectory $export
        $code=$LASTEXITCODE;$doctor=$raw | ConvertFrom-Json
        if($code -ne 2 -or $doctor.status -ne 'BLOCKED'){throw 'Wrapper lost the doctor failure exit code.'}
    }
    $config.python=$Python;$config | ConvertTo-Json | Set-Content -LiteralPath $configPath
    Check 'Relocated package imports actual SDK and pinned Python dependencies without inference' {
        $raw=& $Python -I -B (Join-Path $export 'Test-PythonRequirements.py') 2>$null
        $code=$LASTEXITCODE;$probe=$raw | ConvertFrom-Json
        if($code -ne 0 -or -not $probe.ready -or $probe.inferenceVerified){throw ('Actual isolated import failed: ' + ($probe.codes -join ','))}
    }
    Check 'Relocated doctor uses explicit existing runtimes and makes no provider or account request' {
        $raw=& (Get-Process -Id $PID).Path -NoProfile -File (Join-Path $export 'Companion.ps1') -Command doctor
        $code=$LASTEXITCODE;$doctor=$raw | ConvertFrom-Json
        if($code -ne 0 -or $doctor.status -ne 'PREREQUISITES_READY' -or $doctor.providerChecked -or $doctor.inferenceVerified -or $doctor.authorizationVerified -or $doctor.workerStarted -or $doctor.servicesStarted){throw 'Doctor contract failed.'}
    }
    Check 'Changing a bundled source fails package verification before invocation' {
        $worker=Join-Path $export 'runtime/nova_worker.py';$bytes=[IO.File]::ReadAllBytes($worker)
        try { [IO.File]::AppendAllText($worker,"`n# synthetic tamper`n");Reject {Assert-CompanionPackage $export} }
        finally {[IO.File]::WriteAllBytes($worker,$bytes)}
    }
    Check 'Unlisted executable input is rejected instead of silently joining imports' {
        $extra=Join-Path $export 'runtime/sdk/adaptive_core/unlisted.py'
        try { [IO.File]::WriteAllText($extra,'raise RuntimeError("synthetic: never execute")');Reject {Assert-CompanionPackage $export} }
        finally {Remove-Item -LiteralPath $extra -Force}
    }
    Check 'Package and source payload remain byte-identical after doctor and failed checks' { Assert-CompanionPackage $export }
    $receipt=[pscustomobject]@{schema=1;scope='Real clean ZIP relocation and offline imports; existing Windows prerequisites, not a clean OS';
        packageVersion=$packageManifest.packageVersion;zipSha256=(Get-FileHash -LiteralPath $PackageZip -Algorithm SHA256).Hash.ToLowerInvariant();passed=$results.Count;failed=0;checks=$results.ToArray();
        modelCalls=0;workerStarts=0;servicesStarted=0;accountRequests=0;providerRequests=0;cleanMachineVerified=$false;inferenceVerified=$false}
    $null=New-Item -ItemType Directory -Path $EvidenceDirectory -Force
    $receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'portable-export.json')
    $receipt | ConvertTo-Json -Depth 5
} finally {
    $resolved=[IO.Path]::GetFullPath($temporary)
    $parent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    if(-not $resolved.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected cleanup target.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
