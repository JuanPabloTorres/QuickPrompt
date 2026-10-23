param(
    [ValidateRange(1024, 65535)][int]$Port = 5190,
    [string]$DataPath,
    [switch]$NoBuild,
    [switch]$NoBrowser,
    [switch]$Background,
    [switch]$QaBrowser,
    [ValidateSet('adaptive-core','native')][string]$AgentRuntime = 'adaptive-core',
    [string]$CorePython,
    [string]$CoreSdkRoot,
    [string]$ApiDllPath,
    [string]$WebRootPath,
    [string]$CoreWorkerPath,
    [string]$OllamaEndpoint = 'http://127.0.0.1:11434',
    [switch]$Production,
    [switch]$ReuseExisting
)
$ErrorActionPreference = 'Stop'
$novaRoot = Split-Path -Parent $PSScriptRoot
$novaWeb = Join-Path $novaRoot 'apps/web'
$novaApi = Join-Path $novaRoot 'backend/QuickPrompt.Api'
$novaDll = if ($ApiDllPath) { [IO.Path]::GetFullPath($ApiDllPath) } else { Join-Path $novaApi 'bin/Debug/net8.0/QuickPrompt.Api.dll' }
$novaDist = if ($WebRootPath) { [IO.Path]::GetFullPath($WebRootPath) } else { Join-Path $novaWeb 'dist' }
if (($ApiDllPath -or $WebRootPath -or $CoreWorkerPath) -and -not $NoBuild) { throw 'Las rutas de una entrega existente requieren -NoBuild. Compila antes y selecciona la entrega explícita.' }
if ($AgentRuntime -eq 'adaptive-core') {
    if (-not $CorePython) {
        $CorePython = @((Join-Path $novaRoot '.venv/Scripts/python.exe'),
            "$env:USERPROFILE/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe") |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
    if (-not $CoreSdkRoot) { $CoreSdkRoot = Join-Path (Split-Path $novaRoot -Parent) 'Adaptive-AI-Core' }
    $novaCoreWorker = if ($CoreWorkerPath) { [IO.Path]::GetFullPath($CoreWorkerPath) } else { Join-Path $novaRoot 'agents/nova-core/nova_worker.py' }
    if (-not $CorePython) { throw 'Configura -CorePython con un intérprete existente. No se instala software ni se cambia de agente automáticamente.' }
    foreach ($novaCoreFile in @($CorePython, $novaCoreWorker, (Join-Path $CoreSdkRoot 'adaptive_core/runtime.py'))) {
        if (-not (Test-Path -LiteralPath $novaCoreFile -PathType Leaf)) { throw "Falta la dependencia Core: $novaCoreFile. Configura -CorePython/-CoreSdkRoot; no hay fallback automático." }
    }
}
if ($ReuseExisting) {
    if (-not $DataPath) { throw 'Para reutilizar una instancia, indica -DataPath explícitamente.' }
    $novaListeners = @(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue)
    if ($novaListeners.Count) {
        $novaOwners = @($novaListeners.OwningProcess | Select-Object -Unique)
        if ($novaOwners.Count -ne 1) { throw 'El puerto tiene propietarios distintos. No se sustituyó ninguna instancia.' }
        $novaProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $($novaOwners[0])"
        $novaCommand = ([string]$novaProcess.CommandLine).Replace('\', '/').ToLowerInvariant()
        $novaExpected = @($novaDll, "--QuickPrompt:Port=$Port", "--QuickPrompt:WebRoot=$novaDist")
        if ($DataPath) { $novaExpected += '--QuickPrompt:DataPath=' + [IO.Path]::GetFullPath($DataPath) }
        foreach ($novaToken in $novaExpected) {
            $novaPattern = '(?:^|["\s])' + [regex]::Escape($novaToken.Replace('\', '/').ToLowerInvariant()) + '(?=["\s]|$)'
            if ($novaCommand -notmatch $novaPattern) { throw 'El puerto está ocupado por otra configuración. No se abrió ni sustituyó esa instancia.' }
        }
        $novaHealth = Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 3
        if ($novaHealth.status -ne 'ok') { throw 'La instancia existe pero su salud no está confirmada. No se arrancó otra.' }
        Write-Host "QuickPrompt NOVA: http://127.0.0.1:$Port. Instancia existente; PID $($novaOwners[0])."
        if (-not $NoBrowser) { Start-Process "http://127.0.0.1:$Port" }
        return
    }
}
if (-not $NoBuild) {
    if (-not (Test-Path -LiteralPath (Join-Path $novaWeb 'node_modules/react/package.json'))) {
        throw 'Faltan paquetes aprobados. Instala explícitamente con npm ci --ignore-scripts desde apps/web. Este script no instala software.'
    }
    & npm.cmd run build --prefix $novaWeb
    if ($LASTEXITCODE -ne 0) { throw 'Falló el build React.' }
    if ($QaBrowser) {
        & node (Join-Path $novaWeb 'node_modules/typescript/bin/tsc') -p (Join-Path $novaRoot 'automation/browser-worker/tsconfig.json')
        if ($LASTEXITCODE -ne 0) { throw 'Falló el build BrowserWorker. No se instalaron dependencias.' }
    }
    # Restored sources can be older than a previous experimental binary.
    & dotnet build (Join-Path $novaApi 'QuickPrompt.Api.csproj') --no-restore --no-incremental
    if ($LASTEXITCODE -ne 0) { throw 'Falló el build API. Si faltan assets, restaura explícitamente los paquetes .NET documentados.' }
}
if (-not (Test-Path -LiteralPath $novaDll) -or -not (Test-Path -LiteralPath (Join-Path $novaDist 'index.html'))) { throw 'Falta la compilación de NOVA.' }
$novaLogs = Join-Path $novaRoot '.nova-build/logs'
New-Item -ItemType Directory -Path $novaLogs -Force | Out-Null
$novaStamp = [Guid]::NewGuid().ToString('N')
$novaArguments = @(('"' + $novaDll + '"'), ('--QuickPrompt:Port=' + $Port), ('"--QuickPrompt:WebRoot=' + $novaDist + '"'))
$novaArguments += '--QuickPrompt:Agent:Runtime=' + $AgentRuntime
if ($Production) { $novaArguments += '--environment=Production' }
$novaArguments += '"--QuickPrompt:Ollama=' + $OllamaEndpoint + '"'
if ($AgentRuntime -eq 'adaptive-core') {
    $novaArguments += '"--QuickPrompt:AdaptiveCore:Python=' + [IO.Path]::GetFullPath($CorePython) + '"'
    $novaArguments += '"--QuickPrompt:AdaptiveCore:SdkRoot=' + [IO.Path]::GetFullPath($CoreSdkRoot) + '"'
    $novaArguments += '"--QuickPrompt:AdaptiveCore:Worker=' + $novaCoreWorker + '"'
}
if ($QaBrowser) { $novaArguments += '--QuickPrompt:Live:AllowQaBrowser=true'; $novaArguments += '"--QuickPrompt:Live:Worker=' + (Join-Path $novaRoot '.playwright/browser-worker-build/worker.cjs') + '"' }
if ($DataPath) { $novaArguments += '"--QuickPrompt:DataPath=' + [System.IO.Path]::GetFullPath($DataPath) + '"' }
$novaServer = Start-Process -FilePath dotnet -ArgumentList $novaArguments -WorkingDirectory $novaApi -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $novaLogs "$novaStamp.out.log") -RedirectStandardError (Join-Path $novaLogs "$novaStamp.err.log")
$novaDetached = $false
try {
    $novaReady = $false
    for ($novaAttempt = 0; $novaAttempt -lt 60; $novaAttempt++) {
        $novaServer.Refresh()
        if ($novaServer.HasExited) { throw 'La API no pudo arrancar. Revisa .nova-build/logs; no se utilizó otra instancia.' }
        try {
            $novaOwnLog = Get-Content -LiteralPath (Join-Path $novaLogs "$novaStamp.out.log") -Raw
            if ($novaOwnLog -and $novaOwnLog.Contains("Now listening on: http://127.0.0.1:$Port")) {
                $novaHealth = Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 1
                if ($novaHealth.status -eq 'ok') { $novaReady = $true; break }
            }
        } catch { }
        Start-Sleep -Milliseconds 200
    }
    if (-not $novaReady) { throw 'La API no estuvo disponible dentro del tiempo de arranque.' }
    if ($Background) {
        Write-Host "QuickPrompt NOVA: http://127.0.0.1:$Port. Segundo plano; PID $($novaServer.Id). Para detener: Stop-Process -Id $($novaServer.Id)."
    } else {
        Write-Host "QuickPrompt NOVA: http://127.0.0.1:$Port. Ctrl+C detiene esta instancia."
    }
    if (-not $NoBrowser) { Start-Process "http://127.0.0.1:$Port" }
    if ($Background) { $novaDetached = $true }
    else { Wait-Process -Id $novaServer.Id }
} finally {
    $novaServer.Refresh()
    if (-not $novaDetached -and -not $novaServer.HasExited) { Stop-Process -Id $novaServer.Id }
}
