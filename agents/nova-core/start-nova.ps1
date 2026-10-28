param(
    [int]$Port = 5261,
    [string]$Python,
    [string]$SdkRoot = "$PSScriptRoot/../../../Adaptive-AI-Core",
    [string]$Worker = "$PSScriptRoot/nova_worker.py",
    [string]$ApiPath = "$PSScriptRoot/../../.playwright/nova-debug/api/QuickPrompt.Api.dll",
    [string]$WebRoot = "$PSScriptRoot/../../.playwright/nova-debug/web",
    [string]$DataPath = "$PSScriptRoot/../../.playwright/nova-core/synthetic.sqlite",
    [string]$Endpoint = 'http://127.0.0.1:11434'
)
$ErrorActionPreference = 'Stop'
if (-not $Python) {
    $Python = @("$PSScriptRoot/../../.venv/Scripts/python.exe", "$env:USERPROFILE/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe") |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if (-not $Python) { throw 'Configura -Python con un intérprete existente. No se instalan dependencias automáticamente.' }
foreach ($file in @($Python, $ApiPath, "$SdkRoot/adaptive_core/runtime.py", $Worker, "$WebRoot/index.html")) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Falta $file. Ejecuta la tarea de VS Code NOVA: preparar depuración sintética o configura sus rutas." }
}
$endpointUri = [uri]$Endpoint
if (-not $endpointUri.IsAbsoluteUri -or -not $endpointUri.IsLoopback -or $endpointUri.Scheme -ne 'http') { throw 'Ollama debe ser un endpoint HTTP local.' }
if ($Port -lt 1024 -or $Port -gt 65535) { throw 'Puerto inválido.' }
$arguments = @([IO.Path]::GetFullPath($ApiPath), "--QuickPrompt:Port=$Port",
    "--QuickPrompt:DataPath=$([IO.Path]::GetFullPath($DataPath))", "--QuickPrompt:WebRoot=$([IO.Path]::GetFullPath($WebRoot))",
    '--QuickPrompt:Demo=true', '--QuickPrompt:Agent:Runtime=adaptive-core', "--QuickPrompt:Ollama=$Endpoint",
    "--QuickPrompt:AdaptiveCore:Python=$([IO.Path]::GetFullPath($Python))", "--QuickPrompt:AdaptiveCore:SdkRoot=$([IO.Path]::GetFullPath($SdkRoot))",
    "--QuickPrompt:AdaptiveCore:Worker=$([IO.Path]::GetFullPath($Worker))")
Write-Host "NOVA con Adaptive Core: http://127.0.0.1:$Port/ | Ollama local: $Endpoint"
& dotnet @arguments
exit $LASTEXITCODE
