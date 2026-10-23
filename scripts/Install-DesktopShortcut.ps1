param(
    [Parameter(Mandatory)][string]$DataPath,
    [Parameter(Mandatory)][string]$CorePython,
    [Parameter(Mandatory)][string]$CoreSdkRoot,
    [Parameter(Mandatory)][string]$ApiDllPath,
    [Parameter(Mandatory)][string]$WebRootPath,
    [Parameter(Mandatory)][string]$CoreWorkerPath,
    [ValidateRange(1024, 65535)][int]$Port = 5277
)
$ErrorActionPreference = 'Stop'
$novaInstallRoot = Join-Path $env:LOCALAPPDATA 'QuickPromptNova'
New-Item -ItemType Directory -Force -Path $novaInstallRoot | Out-Null
$novaPaths = @{}
foreach ($novaName in @('DataPath', 'CorePython', 'CoreSdkRoot', 'ApiDllPath', 'WebRootPath', 'CoreWorkerPath')) {
    $novaValue = [IO.Path]::GetFullPath((Get-Variable -Name $novaName -ValueOnly))
    if (-not (Test-Path -LiteralPath $novaValue)) { throw "No existe $novaName. Selecciona una entrega compilada y una biblioteca explícita." }
    $novaPaths[$novaName] = $novaValue
}
$novaPaths.Port = $Port
$novaPaths.OllamaEndpoint = 'http://127.0.0.1:11434'
$novaProfile = Join-Path $novaInstallRoot 'desktop-launch.json'
if (Test-Path -LiteralPath $novaProfile) { Copy-Item -LiteralPath $novaProfile -Destination ($novaProfile + '.bak-' + (Get-Date -Format 'yyyyMMddHHmmssfff')) }
@{ schemaVersion = 1; launch = $novaPaths } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $novaProfile -Encoding utf8
$novaIcon = Join-Path $novaInstallRoot 'QuickPrompt.ico'
& (Join-Path $PSScriptRoot 'Build-DesktopIcon.ps1') -OutputPath $novaIcon | Out-Null
$novaShell = New-Object -ComObject WScript.Shell
$novaDesktop = $novaShell.SpecialFolders.Item('Desktop')
$novaShortcutPath = Join-Path $novaDesktop 'QuickPrompt NOVA.lnk'
if (Test-Path -LiteralPath $novaShortcutPath) { Copy-Item -LiteralPath $novaShortcutPath -Destination ($novaShortcutPath + '.bak-' + (Get-Date -Format 'yyyyMMddHHmmssfff')) }
$novaShortcut = $novaShell.CreateShortcut($novaShortcutPath)
$novaShortcut.TargetPath = Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
$novaShortcut.Arguments = '-NoProfile -WindowStyle Hidden -File "' + (Join-Path $PSScriptRoot 'Open-NovaDesktop.ps1') + '" -ProfilePath "' + $novaProfile + '"'
$novaShortcut.WorkingDirectory = Split-Path -Parent $PSScriptRoot
$novaShortcut.IconLocation = $novaIcon + ',0'
$novaShortcut.Description = 'QuickPrompt NOVA · Tu agente y biblioteca local de prompts'
$novaShortcut.WindowStyle = 7
$novaShortcut.Save()
Write-Output $novaShortcutPath
