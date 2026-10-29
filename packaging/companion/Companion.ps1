#requires -Version 7.2
[CmdletBinding()]
param(
    [ValidateSet('doctor','pair','run','status','disconnect')][string]$Command = 'doctor',
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'companion.json'),
    [switch]$CheckOllama
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Companion.Package.ps1')
$checks = [Collections.Generic.List[object]]::new()
function Add-Check([string]$Id, [bool]$Passed, [string]$Code) { $checks.Add([pscustomobject]@{id=$Id;passed=$Passed;code=$Code}) }
try {
    Assert-CompanionPackage $PSScriptRoot
    $config = Read-CompanionConfiguration $ConfigPath
    Add-Check 'package-and-config' $true 'VALID'
    Add-Check 'platform' $IsWindows $(if ($IsWindows) { 'WINDOWS_READY' } else { 'WINDOWS_REQUIRED_FOR_DPAPI' })
} catch { [Console]::Error.WriteLine('Paquete o configuración inválidos. Usa companion.example.json y verifica el ZIP original.'); exit 2 }
$dotnet = $null; $python = $null
try {
    $dotnet = Resolve-CompanionExecutable $config.dotnet 'dotnet' (Split-Path -Parent ([IO.Path]::GetFullPath($ConfigPath)))
    $dotnet = & (Join-Path $PSScriptRoot 'Resolve-NovaDotnet.ps1') -DotnetPath $dotnet -RuntimeConfigPath (Join-Path $PSScriptRoot 'app/QuickPrompt.Companion.runtimeconfig.json')
    Add-Check 'dotnet' $true 'NETCORE_AND_ASPNETCORE_READY'
} catch { Add-Check 'dotnet' $false 'INSTALL_NET10_ASPNETCORE_RUNTIME_OR_CONFIGURE_DOTNET' }
if ($Command -in @('doctor','run')) {
    try {
        $python = Resolve-CompanionExecutable $config.python 'python' (Split-Path -Parent ([IO.Path]::GetFullPath($ConfigPath)))
        $probe = & $python -I -B (Join-Path $PSScriptRoot 'Test-PythonRequirements.py') 2>$null | ConvertFrom-Json
        $probeExit = $LASTEXITCODE
        if ($probeExit -ne 0 -or $probe.schema -ne 1 -or $probe.ready -ne $true) { throw 'PYTHON_REQUIREMENTS_NOT_READY' }
        Add-Check 'python' $true 'PINNED_DEPENDENCIES_AND_IMPORTS_READY'
    } catch { Add-Check 'python' $false 'INSTALL_LOCKED_PYTHON_REQUIREMENTS_OR_CONFIGURE_PYTHON' }
}
$providerChecked = $CheckOllama -or $Command -eq 'run'
if ($providerChecked) {
    $code = Test-CompanionOllama ([uri]$config.endpoint)
    Add-Check 'ollama' ($code -eq 'MODEL_LISTED_NOT_INFERENCE_VERIFIED') $code
}
$ready = @($checks | Where-Object { -not $_.passed }).Count -eq 0
if ($Command -eq 'doctor') {
    [pscustomobject]@{schema=1;status=$(if($ready){'PREREQUISITES_READY'}else{'BLOCKED'});checks=$checks.ToArray();providerChecked=[bool]$providerChecked;
        inferenceVerified=$false;authorizationVerified=$false;workerStarted=$false;servicesStarted=$false} | ConvertTo-Json -Depth 5
    if (-not $ready) { exit 2 }; exit 0
}
if (-not $ready) { throw 'Requisitos incompletos. Ejecuta doctor; no se instalaron ni iniciaron servicios.' }
$arguments = @((Join-Path $PSScriptRoot 'app/QuickPrompt.Companion.dll'),$Command,'--server',$config.server,'--slot',$config.slot,
    '--name',$config.name,'--capability',$config.capability)
if ($Command -eq 'run') {
    $arguments += @('--python',$python,'--sdk-root',(Join-Path $PSScriptRoot 'runtime/sdk'),'--worker',(Join-Path $PSScriptRoot 'runtime/nova_worker.py'),
        '--endpoint',$config.endpoint,'--max-jobs',[string]$config.maximumJobs)
}
try { & $dotnet @arguments; $code = $LASTEXITCODE }
finally { if ($Command -eq 'run') { Write-Host 'DETENIDO: esta consola ya no ejecuta trabajos. No se reinició automáticamente.' } }
exit $code
