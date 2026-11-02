#requires -Version 7.2
$ErrorActionPreference='Stop'
$snapshotRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../packaging/companion/core-sdk'))
$manifest=Get-Content -LiteralPath (Join-Path $snapshotRoot 'snapshot.json') -Raw | ConvertFrom-Json
if($manifest.schema -cne 'quickprompt-core-source-snapshot/1'){throw 'Unknown Core source snapshot.'}
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach($entry in $manifest.files){
    $path=[IO.Path]::GetFullPath((Join-Path $snapshotRoot $entry.path))
    if(-not $path.StartsWith($snapshotRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
        -not $seen.Add($entry.path) -or (Get-Item -LiteralPath $path).LinkTarget -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $entry.sha256){throw 'Core source snapshot changed.'}
}
$actual=@(Get-ChildItem -LiteralPath $snapshotRoot -Recurse -File | Where-Object Name -NE 'snapshot.json')
if($actual.Count -ne $seen.Count){throw 'Unlisted Core snapshot input.'}
[pscustomobject]@{status='PASS';files=$seen.Count;scope='Source identity only; not provider inference'} | ConvertTo-Json
