#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$CoreSdkRoot,
    [string]$NugetCache = $(if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }),
    [string]$BuildDirectory,
    [string]$SourceLabel = 'working-tree',
    [string]$PackageVersion
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$versionSource = Join-Path $root 'apps/web/package.json'
if (-not $PackageVersion) { $PackageVersion = (Get-Content -LiteralPath $versionSource -Raw | ConvertFrom-Json).version }
if ($packageVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Falta una versión de aplicación explícita y válida.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'El destino debe ser nuevo; no se reemplazan paquetes ni configuraciones.' }
if ($SourceLabel -notmatch '^[A-Za-z0-9._-]{1,100}$') { throw 'Etiqueta de fuente inválida.' }
$cache = [IO.Path]::GetFullPath($NugetCache)
if (-not (Test-Path -LiteralPath $cache -PathType Container)) { throw 'Falta la caché local NuGet: no se descargan dependencias.' }
$build = if ($BuildDirectory) { [IO.Path]::GetFullPath($BuildDirectory) } else { Join-Path ([IO.Path]::GetTempPath()) ('QuickPromptCompanionBuild-' + [guid]::NewGuid().ToString('N')) }
if (Test-Path -LiteralPath $build) { throw 'El directorio de compilación debe ser nuevo y separado de binarios activos.' }
if ($build.StartsWith($output + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($build + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or $build -eq $output) {
    throw 'Los directorios de compilación y exportación deben estar separados.'
}
$sdk = & (Join-Path $PSScriptRoot 'Resolve-NovaDotnet.ps1') -ForBuild
$null = New-Item -ItemType Directory -Path $build
$source = Join-Path $build 'source'
$sourceHashes = [Collections.Generic.List[object]]::new()
$sourceHashes.Add([pscustomobject]@{path='apps/web/package.json';sha256=(Get-FileHash -LiteralPath $versionSource -Algorithm SHA256).Hash.ToLowerInvariant()})
function Copy-Source([string]$From, [string]$Relative, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $From -PathType Leaf) -or (Get-Item -LiteralPath $From).LinkTarget) { throw 'Falta un archivo regular de la fuente permitida.' }
    $target = Join-Path $Destination $Relative
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force
    Copy-Item -LiteralPath $From -Destination $target
    $sourceHashes.Add([pscustomobject]@{path=$Relative.Replace('\','/');sha256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()})
}
# Only compile inputs and locks. No bin/obj, user configuration, credential stores or repository-wide copy.
$projects = @('backend/QuickPrompt.Companion','backend/QuickPrompt.Inference','backend/QuickPrompt.Application','src/QuickPrompt.Domain')
foreach ($project in $projects) {
    Get-ChildItem -LiteralPath (Join-Path $root $project) -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and ($_.Extension -in @('.cs','.csproj') -or $_.Name -eq 'packages.lock.json') } |
        ForEach-Object { Copy-Source $_.FullName ([IO.Path]::GetRelativePath($root,$_.FullName)) $source }
}
foreach ($folder in @('apps/mobile/Domain/Entities','apps/mobile/Domain/Enums')) {
    Get-ChildItem -LiteralPath (Join-Path $root $folder) -Filter '*.cs' -File |
        ForEach-Object { Copy-Source $_.FullName ([IO.Path]::GetRelativePath($root,$_.FullName)) $source }
}
Copy-Source (Join-Path $root 'global.json') 'global.json' $source
$projectFile = Join-Path $source 'backend/QuickPrompt.Companion/QuickPrompt.Companion.csproj'
if (-not (Test-Path -LiteralPath (Join-Path (Split-Path $projectFile -Parent) 'packages.lock.json'))) { throw 'Falta el lock revisado de Companion.' }
$nugetConfig = Join-Path $build 'offline.nuget.config'
$escapedCache = [Security.SecurityElement]::Escape($cache)
[IO.File]::WriteAllText($nugetConfig, "<configuration><packageSources><clear/><add key=`"existing-local-cache`" value=`"$escapedCache`"/></packageSources></configuration>")
$artifacts = Join-Path $build 'artifacts'
& $sdk restore $projectFile --locked-mode --configfile $nugetConfig --packages $cache --artifacts-path $artifacts -p:NuGetAudit=false --nologo
if ($LASTEXITCODE -ne 0) { throw 'Restore offline bloqueado: faltan paquetes o difiere el lock. No se usó una fuente remota.' }
$published = Join-Path $build 'published'
# GeneratedRegex file-local identities also hash generated paths under artifacts/obj.
# Map the entire isolated build, not only source, so separate exports have identical IL metadata.
& $sdk publish $projectFile -c Release --no-restore --artifacts-path $artifacts -o $published -p:UseAppHost=false -p:DebugType=None -p:DebugSymbols=false "-p:PathMap=$build=/_/build" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Falló publicación aislada; se conservó el directorio de diagnóstico.' }
$null = New-Item -ItemType Directory -Path $output
foreach ($file in (Get-ChildItem -LiteralPath $published -Recurse -File)) {
    if ($file.Extension -notin @('.dll','.json')) { throw 'Archivo publicado inesperado: revisar la allowlist antes de distribuir.' }
    $relative = 'app/' + [IO.Path]::GetRelativePath($published,$file.FullName).Replace('\','/')
    $destination = Join-Path $output $relative
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}
foreach ($name in @('Companion.ps1','Companion.Package.ps1','Companion.Startup.ps1','companion.example.json','companion.prompts.example.json','companion.dmr.example.json','companion.dmr.connected.example.json','Test-PythonRequirements.py','README.md')) {
    Copy-Source (Join-Path $root "packaging/companion/$name") $name $output
}
Copy-Source (Join-Path $PSScriptRoot 'Resolve-NovaDotnet.ps1') 'Resolve-NovaDotnet.ps1' $output
Copy-Source (Join-Path $root 'agents/nova-core/nova_worker.py') 'runtime/nova_worker.py' $output
$coreFiles = @('__init__.py','capabilities.py','host.py','inference.py','observation.py','ollama_framework.py','dmr_framework.py','provider.py','runtime.py','store.py',
    'inference-reply.schema.json','inference-request.schema.json')
foreach ($name in $coreFiles) { Copy-Source (Join-Path $CoreSdkRoot "adaptive_core/$name") "runtime/sdk/adaptive_core/$name" $output }
Copy-Source (Join-Path $CoreSdkRoot 'requirements-inference.lock.txt') 'runtime/requirements-inference.lock.txt' $output
Copy-Source (Join-Path $CoreSdkRoot 'pyproject.toml') 'notices/Adaptive-Core-pyproject.toml' $output
$coreLicense = Get-ChildItem -LiteralPath $CoreSdkRoot -File | Where-Object { $_.Name -match '^(LICENSE|NOTICE|COPYING)(\..*)?$' }
foreach ($file in $coreLicense) { Copy-Source $file.FullName ('notices/Adaptive-Core-' + $file.Name) $output }
$coreNotice = @'
Adaptive Core: first-party source snapshot supplied explicitly to the packager.
Only the allowlisted SDK sources and schemas are included, never its data, venv or configuration.
No third-party Python implementation is vendored; requirements-inference.lock.txt pins separately installed distributions.
See provenance.json for source hashes and Adaptive-Core-pyproject.toml for its declared package version.
Any existing source LICENSE/NOTICE/COPYING files are copied alongside this notice. If none exist,
this notice does not invent a license or grant onward redistribution rights: resolve first-party licensing before public distribution.
'@
[IO.File]::WriteAllText((Join-Path $output 'notices/Adaptive-Core-NOTICE.txt'),$coreNotice)
$notices = [Collections.Generic.List[object]]::new()
foreach ($project in $projects) {
    $lockFile = Join-Path $source "$project/packages.lock.json"
    if (-not (Test-Path -LiteralPath $lockFile)) { continue }
    $destination = Join-Path $output ('locks/' + ($project.Replace('/','-')) + '.lock.json')
    $null = New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force
    Copy-Item -LiteralPath $lockFile -Destination $destination
    $lock = Get-Content -LiteralPath $lockFile -Raw | ConvertFrom-Json -AsHashtable
    foreach ($framework in $lock.dependencies.Values) { foreach ($id in $framework.Keys) {
        $dependency = $framework[$id]
        if ($dependency.type -eq 'Project') { continue }
        $version = [string]$dependency.resolved
        if ($notices | Where-Object { $_.id -ieq $id -and $_.version -eq $version }) { continue }
        $packageDirectory = Join-Path $cache ($id.ToLowerInvariant() + '/' + $version)
        $nuspecPath = Join-Path $packageDirectory ($id.ToLowerInvariant() + '.nuspec')
        if (-not (Test-Path -LiteralPath $nuspecPath)) { throw 'Falta el aviso NuGet en la caché local.' }
        [xml]$nuspec = Get-Content -LiteralPath $nuspecPath -Raw
        $metadata = $nuspec.package.metadata
        $license = $metadata.SelectSingleNode("*[local-name()='license']")
        $licenseUrl = $metadata.SelectSingleNode("*[local-name()='licenseUrl']")
        $record = [ordered]@{id=$id;version=$version;licenseType=$(if($license){$license.type}else{$null});license=$(if($license){$license.InnerText}else{$null});licenseUrl=$(if($licenseUrl){$licenseUrl.InnerText}else{$null})}
        foreach ($field in @('authors','copyright','projectUrl')) {
            $node = $metadata.SelectSingleNode("*[local-name()='$field']")
            $record[$field] = if ($node) { $node.InnerText } else { $null }
        }
        foreach ($notice in (Get-ChildItem -LiteralPath $packageDirectory -File | Where-Object { $_.Name -match '(LICENSE|NOTICE|COPYING)' -and $_.Extension -in @('.txt','.md','') })) {
            $target = Join-Path $output ('notices/nuget/' + $id + '-' + $version + '-' + $notice.Name)
            $null = New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force
            Copy-Item -LiteralPath $notice.FullName -Destination $target
        }
        if ($license -and $license.type -eq 'file') {
            $licensePath = [IO.Path]::GetFullPath((Join-Path $packageDirectory $license.InnerText))
            if (-not $licensePath.StartsWith([IO.Path]::GetFullPath($packageDirectory) + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Ruta de licencia fuera del paquete.' }
            $target = Join-Path $output ('notices/nuget/' + $id + '-' + $version + '-LICENSE.txt')
            $null = New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force
            Copy-Item -LiteralPath $licensePath -Destination $target
        }
        $notices.Add([pscustomobject]$record)
    } }
}
$notices | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'notices/nuget-packages.json') -Encoding utf8
$sdkVersion = (& $sdk --version | Select-Object -First 1)
[pscustomobject]@{schema=2;packageVersion=$packageVersion;sourceLabel=$SourceLabel;sdkVersion=$sdkVersion;configuration='Release';deployment='framework-dependent';
    restore='locked-local-cache-only';providers=@('ollama','docker-model-runner');model='qwen3:8b';sourceFiles=$sourceHashes.ToArray();inferenceVerified=$false;
    cleanMachineVerified=$false;thirdPartyPythonVendored=$false} | ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath (Join-Path $output 'provenance.json') -Encoding utf8
$files = @(Get-ChildItem -LiteralPath $output -Recurse -File | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{path=[IO.Path]::GetRelativePath($output,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
[pscustomobject]@{schema=2;packageVersion=$packageVersion;providers=@('ollama','docker-model-runner');model='qwen3:8b';files=$files} | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $output 'package-manifest.json') -Encoding utf8
. (Join-Path $output 'Companion.Package.ps1')
Assert-CompanionPackage $output
$zip = $output + '.zip'
if (Test-Path -LiteralPath $zip) { throw 'El ZIP destino ya existe; no se reemplaza.' }
# ZIP metadata is part of the delivery hash. Use ordinal entry order and the
# format's fixed minimum date; this is canonical metadata, not a claimed build time.
$zipPaths = [string[]]@(Get-ChildItem -LiteralPath $output -Recurse -File | ForEach-Object {
    [IO.Path]::GetRelativePath($output,$_.FullName).Replace('\','/')
})
[Array]::Sort($zipPaths,[StringComparer]::Ordinal)
$zipStream = [IO.File]::Open($zip,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try {
    $archive = [IO.Compression.ZipArchive]::new($zipStream,[IO.Compression.ZipArchiveMode]::Create,$true)
    try {
        foreach ($relative in $zipPaths) {
            $entry = $archive.CreateEntry($relative,[IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]::new(1980,1,1,0,0,0,[TimeSpan]::Zero)
            $entryStream = $entry.Open()
            try {
                $sourceStream = [IO.File]::OpenRead((Join-Path $output $relative))
                try { $sourceStream.CopyTo($entryStream) }
                finally { $sourceStream.Dispose() }
            } finally { $entryStream.Dispose() }
        }
    } finally { $archive.Dispose() }
} finally { $zipStream.Dispose() }
[pscustomobject]@{packageVersion=$packageVersion;directory=$output;zip=$zip;sha256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant();files=$files.Count;
    status='PACKAGE_EXPORTED_NOT_AGENT_CERTIFIED';buildDirectory=$build} | ConvertTo-Json
