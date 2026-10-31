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
    [switch]$CheckOllama,
    [switch]$CheckProvider,
    [ValidateSet('ollama','docker-model-runner')][string]$Provider = 'ollama',
    [string]$PhysicalModel,
    [ValidateSet('bounded','connected')][string]$SessionMode = 'bounded',
    [bool]$Thinking = $false
)
$ErrorActionPreference = 'Stop'
if ($PackageDirectory) {
    foreach ($conflicting in @('Server','Slot','Name','Capability','CorePython','CoreSdkRoot','CoreWorker','Endpoint','MaximumJobs','NoBuild','Provider','PhysicalModel','SessionMode','Thinking')) {
        if ($PSBoundParameters.ContainsKey($conflicting)) { throw 'El paquete usa sólo su JSON explícito; no se mezclan opciones del launcher de desarrollo.' }
    }
    $portable = Join-Path ([IO.Path]::GetFullPath($PackageDirectory)) 'Companion.ps1'
    if (-not (Test-Path -LiteralPath $portable -PathType Leaf)) { throw 'Falta Companion.ps1 en la distribución indicada.' }
    $portableArguments = @{ Command=$Command; CheckOllama=$CheckOllama; CheckProvider=$CheckProvider }
    if ($ConfigPath) { $portableArguments.ConfigPath = $ConfigPath }
    & $portable @portableArguments
    exit $LASTEXITCODE
}
if ($Command -eq 'doctor' -or $ConfigPath -or $CheckOllama -or $CheckProvider) { throw 'doctor y configuración portable requieren -PackageDirectory. Consulta la guía de Companion.' }
if (($Command -ne 'run' -and $PSBoundParameters.ContainsKey('SessionMode')) -or
    ($SessionMode -eq 'connected' -and $PSBoundParameters.ContainsKey('MaximumJobs'))) { throw 'El modo connected sólo pertenece a run y no se combina con MaximumJobs.' }
if ($Command -ne 'run' -and $PSBoundParameters.ContainsKey('Thinking')) { throw 'Thinking sólo pertenece al comando run.' }
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
if ($Provider -eq 'docker-model-runner') {
    if (-not $PhysicalModel -or $Endpoint.Port -ne 12434) { throw 'DMR exige un modelo físico fijado y -Endpoint loopback 12434 explícito.' }
    $companionArguments += @('--provider',$Provider,'--physical-model',$PhysicalModel,'--endpoint',$Endpoint.AbsoluteUri)
} elseif ($PSBoundParameters.ContainsKey('PhysicalModel')) { throw 'PhysicalModel sólo pertenece al proveedor DMR explícito.' }
if ($Command -eq 'run') {
    if (-not $CorePython) { throw 'Configura -CorePython con el Python existente de Adaptive Core; no se instalarán dependencias ni cambiará el proveedor.' }
    if (-not $CoreSdkRoot) { $CoreSdkRoot = Join-Path (Split-Path $companionRoot -Parent) 'Adaptive-AI-Core' }
    if (-not $CoreWorker) { $CoreWorker = Join-Path $companionRoot 'agents/nova-core/nova_worker.py' }
    $companionArguments += @('--python', $CorePython, '--sdk-root', $CoreSdkRoot, '--worker', $CoreWorker)
    if ($SessionMode -eq 'bounded') { $companionArguments += @('--max-jobs',$MaximumJobs.ToString()) }
    else { $companionArguments += @('--session-mode','connected') }
    $companionArguments += @('--thinking',$Thinking.ToString().ToLowerInvariant())
    if ($Provider -eq 'ollama') { $companionArguments += @('--endpoint',$Endpoint.AbsoluteUri) }
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
