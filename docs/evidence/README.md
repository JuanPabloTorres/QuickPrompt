# Evidencias locales de validación

Este directorio recibe capturas, grabaciones de QA, logs, exportaciones y resultados generados por las pruebas. Se conservan localmente y se excluyen de Git para evitar añadir datos de usuario, rutas del equipo, exportaciones y grandes cantidades de resultados repetidos.

Las referencias a archivos de este directorio en los informes de aceptación describen ejecuciones locales. En un clon nuevo, esos archivos aparecen al reproducir el recorrido correspondiente; su ausencia inicial no equivale a una prueba fallida. El resumen portable de validación está en [validation-summary.json](validation-summary.json).

Se versionan los tests y fixtures revisados bajo `Tests/`, los informes de `docs/` y los cuatro tutoriales de producto con datos ficticios en `apps/web/public/tutoriales/`. Estos tutoriales son recursos de la aplicación y permanecen disponibles en una instalación nueva.

La aplicación guarda su biblioteca fuera del árbol versionado. No añadir archivos `.db`, `.db3`, `.sqlite`, respaldos personales, credenciales de Firebase ni perfiles del navegador a un commit. Las copias locales existentes se conservan; quitar el seguimiento de Git no reescribe el historial anterior.

Comprobaciones habituales, con las dependencias ya instaladas:

```powershell
$validationOutput = Join-Path (Get-Location) '.playwright/merge-validation/'
dotnet test src/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore "-p:BaseOutputPath=$validationOutput"
dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore
npm test --prefix apps/web
npm run build --prefix apps/web
node --test Tests/e2e/automation-contracts.cjs
```

Los recorridos de NOVA requieren compilar la API con la salida documentada en cada test. [Comandos locales y Playwright visible](../archive/NOVA_LOCAL_COMMANDS.md), [modal de resultado](../archive/NOVA_RESULT_MODAL.md), [biblioteca y videos](../LEARNING_ACCEPTANCE.md).
