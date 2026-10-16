param(
    [ValidateRange(1024, 65535)][int]$Port = 5190,
    [string]$DataPath,
    [switch]$NoBuild,
    [switch]$NoBrowser,
    [switch]$Background
)
$ErrorActionPreference = 'Stop'
$novaRoot = Split-Path -Parent $PSScriptRoot
$novaWeb = Join-Path $novaRoot 'apps/web'
$novaApi = Join-Path $novaRoot 'backend/QuickPrompt.Api'
if (-not $NoBuild) {
    if (-not (Test-Path -LiteralPath (Join-Path $novaWeb 'node_modules/react/package.json'))) {
        throw 'Faltan paquetes aprobados. Instala explícitamente con npm ci --ignore-scripts desde apps/web. Este script no instala software.'
    }
    & npm run build --prefix $novaWeb
    if ($LASTEXITCODE -ne 0) { throw 'Falló el build React.' }
    & dotnet build (Join-Path $novaApi 'QuickPrompt.Api.csproj') --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Falló el build API. Si faltan assets, restaura explícitamente los paquetes .NET documentados.' }
}
$novaDll = Join-Path $novaApi 'bin/Debug/net8.0/QuickPrompt.Api.dll'
$novaDist = Join-Path $novaWeb 'dist'
if (-not (Test-Path -LiteralPath $novaDll) -or -not (Test-Path -LiteralPath (Join-Path $novaDist 'index.html'))) { throw 'Falta la compilación de NOVA.' }
$novaLogs = Join-Path $novaRoot '.nova-build/logs'
New-Item -ItemType Directory -Path $novaLogs -Force | Out-Null
$novaStamp = [Guid]::NewGuid().ToString('N')
$novaArguments = @(('"' + $novaDll + '"'), ('--QuickPrompt:Port=' + $Port), ('"--QuickPrompt:WebRoot=' + $novaDist + '"'))
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
