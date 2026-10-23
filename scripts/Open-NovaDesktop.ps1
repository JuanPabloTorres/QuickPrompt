param([string]$ProfilePath = (Join-Path $env:LOCALAPPDATA 'QuickPromptNova/desktop-launch.json'), [switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
try {
    $novaProfileFile = Get-Item -LiteralPath $ProfilePath
    if ($novaProfileFile.Length -gt 16384) { throw 'El perfil de escritorio supera el límite permitido.' }
    $novaProfile = Get-Content -LiteralPath $ProfilePath -Raw | ConvertFrom-Json
    if ($novaProfile.schemaVersion -ne 1) { throw 'Perfil de escritorio no compatible.' }
    $novaAllowed = @('Port', 'DataPath', 'CorePython', 'CoreSdkRoot', 'ApiDllPath', 'WebRootPath', 'CoreWorkerPath', 'OllamaEndpoint')
    $novaLaunch = @{}
    foreach ($novaProperty in $novaProfile.launch.PSObject.Properties) {
        if ($novaAllowed -notcontains $novaProperty.Name) { throw 'El perfil contiene un parámetro no permitido.' }
        $novaLaunch[$novaProperty.Name] = $novaProperty.Value
    }
    foreach ($novaRequired in @('Port', 'DataPath', 'CorePython', 'CoreSdkRoot', 'ApiDllPath', 'WebRootPath', 'CoreWorkerPath')) {
        if (-not $novaLaunch[$novaRequired]) { throw "Falta $novaRequired en el perfil de escritorio." }
    }
    & (Join-Path $PSScriptRoot 'Start-Nova.ps1') @novaLaunch -NoBuild -Background -Production -ReuseExisting -NoBrowser:$NoBrowser
} catch {
    $novaFailure = $_.Exception.Message
    $novaLogRoot = Join-Path $env:LOCALAPPDATA 'QuickPromptNova'
    New-Item -ItemType Directory -Force -Path $novaLogRoot | Out-Null
    Add-Content -LiteralPath (Join-Path $novaLogRoot 'desktop-errors.log') -Value ((Get-Date -Format o) + ' ' + $novaFailure)
    if (-not $NoBrowser) {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show($novaFailure, 'QuickPrompt NOVA', 'OK', 'Error') | Out-Null
    }
    throw
}
