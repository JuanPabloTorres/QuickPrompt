#requires -Version 7.2
[CmdletBinding()]
param(
    [ValidateSet('doctor','pair','run','status','disconnect')][string]$Command = 'doctor',
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'companion.json'),
    [switch]$CheckOllama,
    [switch]$CheckProvider,
    [string]$StartupEvidenceDirectory = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'QuickPromptNova/companion/startup-diagnostics')
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Companion.Startup.ps1')
$startupRecord = if($Command -eq 'run'){New-CompanionStartupRecord $StartupEvidenceDirectory $ConfigPath}else{$null}
if($startupRecord){Write-Host ('Diagnóstico de este arranque: '+$startupRecord.Path)}
if($Command -eq 'run'){try{$Host.UI.RawUI.WindowTitle='QuickPrompt · INICIANDO'}catch{}}
$checks = [Collections.Generic.List[object]]::new()
function Add-Check([string]$Id, [bool]$Passed, [string]$Code) { $checks.Add([pscustomobject]@{id=$Id;passed=$Passed;code=$Code}) }
try {
    . (Join-Path $PSScriptRoot 'Companion.Package.ps1')
    Assert-CompanionPackage $PSScriptRoot
    $config = Read-CompanionConfiguration $ConfigPath
    $provider = if ($config.version -eq 1) { 'ollama' } else { $config.provider }
    if (($CheckOllama -and $provider -ne 'ollama') -or ($CheckOllama -and $CheckProvider)) { throw 'Use one provider check matching the explicit configuration.' }
    Add-Check 'package-and-config' $true 'VALID'
    Add-Check 'platform' $IsWindows $(if ($IsWindows) { 'WINDOWS_READY' } else { 'WINDOWS_REQUIRED_FOR_DPAPI' })
} catch {
    Update-CompanionStartupRecord $startupRecord 'INVALID_PACKAGE_OR_CONFIGURATION' @() 2
    if($Command -eq 'run'){try{$Host.UI.RawUI.WindowTitle='QuickPrompt · DETENIDO'}catch{}}
    [Console]::Error.WriteLine('Paquete o configuración inválidos. Usa companion.example.json y verifica el ZIP original.'); exit 2
}
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
$providerChecked = $CheckOllama -or $CheckProvider -or $Command -eq 'run'
if ($providerChecked) {
    $code = if ($provider -eq 'ollama') { Test-CompanionOllama ([uri]$config.endpoint) }
        else { Test-CompanionDmr ([uri]$config.endpoint) $config.physicalModel $config.thinking }
    Add-Check $provider ($code -in @('MODEL_LISTED_NOT_INFERENCE_VERIFIED','DMR_MODEL_CONFIGURED_NOT_INFERENCE_VERIFIED')) $code
}
$ready = @($checks | Where-Object { -not $_.passed }).Count -eq 0
if ($Command -eq 'doctor') {
    [pscustomobject]@{schema=1;status=$(if($ready){'PREREQUISITES_READY'}else{'BLOCKED'});checks=$checks.ToArray();provider=$provider;providerChecked=[bool]$providerChecked;
        inferenceVerified=$false;authorizationVerified=$false;workerStarted=$false;servicesStarted=$false} | ConvertTo-Json -Depth 5
    if (-not $ready) { exit 2 }; exit 0
}
if (-not $ready) {
    Update-CompanionStartupRecord $startupRecord 'PREREQUISITES_BLOCKED' $checks.ToArray() 2
    [pscustomobject]@{schema=1;status='BLOCKED';checks=$checks.ToArray();provider=$provider;
        inferenceVerified=$false;authorizationVerified=$false;workerStarted=$false;servicesStarted=$false} | ConvertTo-Json -Depth 5
    [Console]::Error.WriteLine('Requisitos incompletos. Revisa los códigos de arriba; no se inició el componente.')
    if($Command -eq 'run'){try{$Host.UI.RawUI.WindowTitle='QuickPrompt · DETENIDO'}catch{}}
    exit 2
}
$arguments = @((Join-Path $PSScriptRoot 'app/QuickPrompt.Companion.dll'),$Command,'--server',$config.server,'--slot',$config.slot,
    '--name',$config.name,'--capability',$config.capability)
if ($provider -eq 'docker-model-runner') {
    $arguments += @('--provider',$provider,'--physical-model',$config.physicalModel,'--endpoint',$config.endpoint)
}
if ($Command -eq 'run') {
    $arguments += @('--python',$python,'--sdk-root',(Join-Path $PSScriptRoot 'runtime/sdk'),'--worker',(Join-Path $PSScriptRoot 'runtime/nova_worker.py'))
    $arguments += Get-CompanionSessionArguments $config $Command
    $arguments += Get-CompanionThinkingArguments $config $Command
    if ($provider -eq 'ollama') { $arguments += @('--endpoint',$config.endpoint) }
}
$code=$null
Update-CompanionStartupRecord $startupRecord 'INVOKING_RUNTIME' $checks.ToArray()
try { & $dotnet @arguments; $code = $LASTEXITCODE }
finally {
    Update-CompanionStartupRecord $startupRecord $(if($null -eq $code){'RUNTIME_EXIT_UNCONFIRMED'}else{'RUNTIME_EXITED'}) $checks.ToArray() $code
    if ($Command -eq 'run') {
        try{$Host.UI.RawUI.WindowTitle='QuickPrompt · DETENIDO'}catch{}
        Write-Host 'DETENIDO: esta consola ya no ejecuta trabajos. No se reinició automáticamente.'
    }
}
exit $code
