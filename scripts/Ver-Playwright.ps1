$ErrorActionPreference = 'Stop'
$novaDemoRoot = Split-Path -Parent $PSScriptRoot
Write-Host 'Se abrirá Edge con una biblioteca de prueba. Tu pestaña y tus prompts no se modifican.'
& dotnet build (Join-Path $novaDemoRoot 'backend/QuickPrompt.Api/QuickPrompt.Api.csproj') --no-restore ("-p:BaseOutputPath=" + (Join-Path $novaDemoRoot '.playwright/learning-build/'))
if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar la demostración.' }
& node (Join-Path $PSScriptRoot 'Record-UsageVideos.cjs') --visible --demo-only
if ($LASTEXITCODE -ne 0) { throw 'La demostración no se completó. Revisa docs/evidence/learning.' }
