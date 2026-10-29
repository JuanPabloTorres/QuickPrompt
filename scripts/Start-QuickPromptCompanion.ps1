#requires -Version 7.2
param(
    [Parameter(Mandatory)][ValidateSet('doctor','pair','run','status','disconnect')][string]$Command,
    [uri]$Server,
    [ValidatePattern('^[A-Za-z0-9-]{1,40}$')][string]$Slot = 'default',
    [string]$Name = $env:COMPUTERNAME,
    [ValidateSet('prompts','nova')][string]$Capability = 'prompts',
    [string]$CorePython,
    [string]$CoreSdkRoot,
    [string]$CoreWorker,
    [uri]$Endpoint = 'http://127.0.0.1:11434/',
    [ValidateRange(1,50)][int]$MaximumJobs = 1,
    [switch]$NoBuild,
    [string]$PackageDirectory,
    [string]$ConfigPath,
    [switch]$CheckOllama
)
$ErrorActionPreference = 'Stop'
if ($PackageDirectory) {
    foreach ($conflicting in @('Server','Slot','Name','Capability','CorePython','CoreSdkRoot','CoreWorker','Endpoint','MaximumJobs','NoBuild')) {
        if ($PSBoundParameters.ContainsKey($conflicting)) { throw 'El paquete usa sólo su JSON explícito; no se mezclan opciones del launcher de desarrollo.' }
    }
    $portable = Join-Path ([IO.Path]::GetFullPath($PackageDirectory)) 'Companion.ps1'
    if (-not (Test-Path -LiteralPath $portable -PathType Leaf)) { throw 'Falta Companion.ps1 en la distribución indicada.' }
    $portableArguments = @{ Command=$Command; CheckOllama=$CheckOllama }
    if ($ConfigPath) { $portableArguments.ConfigPath = $ConfigPath }
    & $portable @portableArguments
    exit $LASTEXITCODE
}
if ($Command -eq 'doctor' -or $ConfigPath -or $CheckOllama) { throw 'doctor y configuración portable requieren -PackageDirectory. Consulta la guía de Companion.' }
if (-not $Server) { throw 'Configura -Server HTTPS para desarrollo o usa -PackageDirectory con su configuración.' }
$companionRoot = Split-Path -Parent $PSScriptRoot
$companionSdk = & (Join-Path $PSScriptRoot 'Resolve-NovaDotnet.ps1') -ForBuild
$companionProject = Join-Path $companionRoot 'backend/QuickPrompt.Companion'
if (-not $NoBuild) {
    & $companionSdk build $companionProject -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'No compiló el componente local.' }
}
$companionDll = Join-Path $companionProject 'bin/Release/net10.0-windows/QuickPrompt.Companion.dll'
if (-not (Test-Path -LiteralPath $companionDll -PathType Leaf)) { throw 'Compila el componente antes de usar -NoBuild.' }
$companionArguments = @($companionDll, $Command, '--server', $Server.AbsoluteUri, '--slot', $Slot, '--name', $Name, '--capability', $Capability)
if ($Command -eq 'run') {
    if (-not $CorePython) { throw 'Configura -CorePython con el Python existente de Adaptive Core; no se instalarán dependencias ni cambiará el proveedor.' }
    if (-not $CoreSdkRoot) { $CoreSdkRoot = Join-Path (Split-Path $companionRoot -Parent) 'Adaptive-AI-Core' }
    if (-not $CoreWorker) { $CoreWorker = Join-Path $companionRoot 'agents/nova-core/nova_worker.py' }
    $companionArguments += @('--python', $CorePython, '--sdk-root', $CoreSdkRoot, '--worker', $CoreWorker,
        '--endpoint', $Endpoint.AbsoluteUri, '--max-jobs', $MaximumJobs.ToString())
}
# Foreground is intentional: this command displays a short pairing code and allows Ctrl+C.
if ($Command -eq 'run') {
    # A retained -NoExit shell is not evidence of a live worker. Title decoration
    # is best effort because some PowerShell hosts do not implement RawUI.
    try { $Host.UI.RawUI.WindowTitle = "QuickPrompt $Capability · SESIÓN LOCAL" } catch { }
}
$companionExitCode = $null
try {
    & $companionSdk @companionArguments
    $companionExitCode = $LASTEXITCODE
} finally {
    if ($Command -eq 'run') {
        try { $Host.UI.RawUI.WindowTitle = "QuickPrompt $Capability · DETENIDO" } catch { }
        Write-Host 'DETENIDO: esta consola ya no ejecuta trabajos. Un nuevo inicio requiere volver a ejecutar el acceso directo.'
    }
}
if ($companionExitCode -ne 0) { throw 'El componente terminó sin confirmar la operación. Revisa su mensaje y el estado en QuickPrompt.' }
