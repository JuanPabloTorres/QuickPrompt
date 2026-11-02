param([string]$RuntimeConfigPath, [switch]$ForBuild, [string]$DotnetPath)
$ErrorActionPreference = 'Stop'
# Trusted launcher configuration only. HTTP requests never select an executable.
$novaRequiredVersion = [Version]'10.0.403'
$novaMajor = 10
$novaRequiredFrameworks = @('Microsoft.NETCore.App','Microsoft.AspNetCore.App')
$novaMinimumRuntime = [Version]'10.0.0'
if ($RuntimeConfigPath -and (Test-Path -LiteralPath $RuntimeConfigPath)) {
    $novaRuntimeConfig = Get-Content -LiteralPath $RuntimeConfigPath -Raw | ConvertFrom-Json -AsHashtable
    $novaFrameworks = @($novaRuntimeConfig.runtimeOptions['framework']) + @($novaRuntimeConfig.runtimeOptions['frameworks'])
    $novaVersions = @($novaFrameworks | Where-Object { $_ -and $_.version } | ForEach-Object { [Version]$_.version })
    if ($novaVersions.Count) {
        $novaMinimumRuntime = $novaVersions | Sort-Object -Descending | Select-Object -First 1
        $novaMajor = $novaMinimumRuntime.Major
    }
    $novaRequiredFrameworks = @($novaFrameworks | Where-Object { $_ -and $_.name } | ForEach-Object { $_.name } | Select-Object -Unique)
}
$novaSystemDotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
$novaCandidates = @()
if ($DotnetPath) { $novaCandidates += $DotnetPath }
elseif ($env:QUICKPROMPT_DOTNET) { $novaCandidates += $env:QUICKPROMPT_DOTNET }
else {
    if ($novaSystemDotnet) { $novaCandidates += $novaSystemDotnet.Source }
    if ($env:LOCALAPPDATA) { $novaCandidates += Join-Path $env:LOCALAPPDATA 'QuickPromptNova/toolchains/dotnet10/dotnet.exe' }
}
foreach ($novaCandidate in ($novaCandidates | Select-Object -Unique)) {
    if (-not (Test-Path -LiteralPath $novaCandidate -PathType Leaf)) { continue }
    if ($ForBuild) {
        $novaSdkVersions = @(& $novaCandidate --list-sdks | ForEach-Object {
            if ($_ -match '^(\d+\.\d+\.\d+)\s') { [Version]$Matches[1] }
        })
        if ($novaSdkVersions | Where-Object { $_.Major -eq $novaRequiredVersion.Major -and $_ -ge $novaRequiredVersion }) {
            return [IO.Path]::GetFullPath($novaCandidate)
        }
    } else {
        $novaRuntimes = @(& $novaCandidate --list-runtimes)
        $novaMissing = @($novaRequiredFrameworks | Where-Object {
            $novaFrameworkName = [regex]::Escape($_)
            -not ($novaRuntimes | Where-Object {
                $_ -match "^$novaFrameworkName ($novaMajor\.\d+\.\d+)\s" -and [Version]$Matches[1] -ge $novaMinimumRuntime
            })
        })
        if ($LASTEXITCODE -eq 0 -and $novaMissing.Count -eq 0) {
            return [IO.Path]::GetFullPath($novaCandidate)
        }
    }
}
throw "Falta el SDK/runtime .NET requerido (major $novaMajor). Instala el SDK soportado o configura QUICKPROMPT_DOTNET con su ejecutable existente."
