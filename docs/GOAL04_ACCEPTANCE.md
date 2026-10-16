# QuickPrompt Web — GOAL 04

**Estado global: PARTIAL_PROVIDER_BLOCKED.** La aplicación local y los flujos nuevos pasan las pruebas descritas abajo. No hay aprobación de inserción ni envío en ChatGPT/Claude auténticos. Fecha: 2026-10-15. [Objetivo completo](GOAL04.md).

| Subsistema | Estado | Evidencia actual |
|---|---|---|
| Build y regresión .NET | PASS | Baseline ejecutado 81/81; cierre **109/109**, cero omitidas. Build sin advertencias/errores. [Build](evidence/goal04/verified/build.log), [pruebas](evidence/goal04/verified/closure/tests.log) |
| Biblioteca y flujos anteriores | PASS | Crear/editar/duplicar/archivar/restaurar/borrar, favoritos, variables, editor visual, búsqueda, copiar, historial, respaldo, ES/EN, temas, seguridad y reinicio. [Regresión integral Edge](evidence/goal04/verified/regression.log) |
| Layout, overlays y teclado | PASS en escenarios ejecutados | Cinco anchos, ambos temas, ventana de 400 px de alto, hit testing durante scroll, foco/Escape, un diálogo activo y reduced motion. [Layout](evidence/goal04/verified/closure/layout/layout.json), [30 escenarios](evidence/goal04/verified/closure/experience/experience.json), [captura](evidence/goal04/verified/closure/experience/Biblioteca-1440-light.png) |
| Borradores e identidad de controles | PASS | Reseleccionar conserva borrador; cambiar entidad recrea controles; eventos anteriores no escriben en otra plantilla. También grupos/perfiles. [Regresión](evidence/goal04/verified/editors/input-state.json) |
| Grupos y perfiles | PASS | CRUD, muchos-a-muchos, asignación múltiple, filtros, fijación/orden, contadores, versiones, combinaciones, composición/copiar y reinicio. [E2E normal sin Ollama](evidence/goal04/verified/organization-after-editor-fix/organization.json) |
| SQLite y respaldos | PASS sobre datos propios | Migración aditiva 1→2, copia previa consistente, idempotencia, rechazo de versión futura, restricciones y rollback DDL/restore real; JSON v1/v2. 1000 plantillas en regresión de consultas; restore/export UI de 450 plantillas y 5 MB. [Pruebas](evidence/goal04/verified/closure/tests.log), [respaldo grande](evidence/goal04/stage06/large-backup/large-backup.json), [preferencias restore](evidence/goal04/verified/closure/restore-preferences-after.json) |
| Qwen3: flujo local completo | PASS funcional | Biblioteca → variables → perfil → ajustes → compilar/revisar → streaming real → guardar explícitamente → reiniciar → recuperar respuesta exacta. Cancelación real y navegación invalidan operaciones anteriores. [Qwen3](evidence/goal04/verified/local-ai/local-ai.json), [captura](evidence/goal04/verified/local-ai/qwen3-composition-result.png) |
| Propuestas y fallos IA | PASS funcional | Mejorar/crear/sugerir/analizar conservan revisión, variables y versiones. Error real de escritura visible dentro del modal. Ausente/vacío/timeout/corte HTTP probados con fixture. [IA real](evidence/goal04/verified/regression/browser-ai.json), [error SQLite](evidence/goal04/stage04/proposal-error/observed.json), [fallos HTTP](evidence/goal04/stage04/faults-after/ai-faults.json) |
| Calidad semántica/factual | FAIL limitado / sin garantía general | Corrida previa: ruta SQLite incorrecta. Tras reinicio: sin ese error, pero prueba sugerida poco concreta. Revisión textual del agente, no humana independiente. [Informe](LOCAL_AI_WORKFLOWS.md) |
| Puente MV3 y estados | PASS local/fixtures | Contratos 6, adaptadores DOM 30, carreras 14, fallos worker 3; extensión real en Edge/Chrome con manifiesto de producción sobre destinos enrutados. Texto literal, borrador intacto, envío UI único, cancelación. [Edge](evidence/goal04/verified/closure/edge/extension-runtime.json), [Chrome](evidence/goal04/verified/closure/chrome/extension-runtime.json) |
| ChatGPT auténtico | BLOCKED | Usuario eligió continuar sin sesión. No se probó sitio auténtico en GOAL 04. HTTP 403 corresponde a Fase 03 |
| Claude automatizado | BLOCKED | Restricción del proveedor conservada, sin bypass. El adaptador fixture no demuestra funcionamiento del servicio |
| Fallback manual | PASS | Clipboard real exacto y URL fija con destinos enrutados/permisos normales. COPIED_OPENED persiste; nunca SENT. [Evidencia](evidence/goal04/verified/closure/fallback/fallback.json) |
| Red y dependencias | PASS en alcance ejecutado | Guard de Kestrel conserva loopback; auditorías públicas npm/NuGet sin instalar paquetes: cero hallazgos reportados. [Guard](evidence/goal04/verified/closure/endpoint-guard.log), [npm](evidence/goal04/final/npm-audit.json), [NuGet](evidence/goal04/final/nuget-audit.json) |
| Confinamiento Ollama | PASS del proceso actual | Listener :: detectado/corregido a127.0.0.1:11434; nube desactivada y dos variables de usuario fijadas. Reinicio Windows/tray NOT_TESTED. [Evidencia](evidence/goal04/ollama-confinement.json), [operación/reversión](LOCAL_AI_WORKFLOWS.md) |
| Importación privada / .NET 10 | BLOCKED | Decisión de no usar datos privados; QuickPrompt.db3 no se leyó. SDK 10 ausente, sin migración ni compatibilidad declarada |
| Lector de pantalla, uso humano prolongado, producción remota | NOT_TESTED | Teclado/geometría automatizados no equivalen a esas validaciones |

Se amplió la aplicación existente ASP.NET Core / Blazor / SQLite sin reconstruirla ni sustituir la identidad original. Tailwind/CLI siguen en 4.3.0, con CSS local compilado. No se instalaron dependencias, SDK ni modelos ni se publicaron cambios o crearon commits en GOAL 04. Las pruebas destructivas usan DB/perfiles propios. No se declara preparación comercial ni disponibilidad remota; bloqueos externos y exactitud factual no se convierten en PASS mediante fixtures.

## Reparaciones y límites

[Auditoría UX](UI_UX_AUDIT.md) contiene la matriz defecto → causa → corrección → prueba. Se reprodujeron navegación fuera del viewport, copia móvil sobre otro control, error oculto por modal, borradores reiniciados/controles reutilizados y respaldo propio que excedía el límite de importación. Las correcciones del producto tienen regresiones; se conservan los intentos fallidos.

OverlayHost coordina top layer nativo, sin elevar z-index. Solo la navegación lateral posee scroll en escritorio. Grupos/perfiles usan consultas paginadas y composición determinista; el LLM no tiene autoridad CRUD. El puente mantiene permisos mínimos y distingue INSERTED, SUBMITTED_UI, UNKNOWN y COPIED_OPENED. [Grupos y rollback](PROMPT_GROUPS.md), [perfiles](INSTRUCTION_PROFILES.md), [IA](LOCAL_AI_WORKFLOWS.md), [proveedores](PROVIDER_INTEGRATION_STATUS.md), [instalación](../extensions/quickprompt-browser/README.md).

Cambio explícito: SQLite v2 y exportación JSON v2, con lectura v1/legado. Antes de migrar v1 se crea una copia SQLite junto al archivo; rollback exige detener y restaurar esa copia con código anterior. Restaurar reemplaza el conjunto; importar es aditivo. JSON tiene límite simétrico **32 MiB** al exportar/importar; SQLite legado seleccionado conserva 4 MB. No se prometen respaldos ilimitados.

## Métricas observadas

| Medición | Resultado | Alcance |
|---|---|---|
| Seleccionar/completar/compilar/copiar | p50 136 ms, p95 210 ms, n=5 | Edge automatizado, incluye roundtrips; no tiempo humano ni benchmark general |
| Arranque con DB propia nueva | 436 ms, n=1 | Hasta readiness del proceso propio |
| CSS servido | 28 831 bytes | Igual al asset compilado, css:check PASS |
| Consulta compuesta Qwen3 antes del reinicio Ollama | 2415 ms, n=1 | Streaming observado; corridas anteriores conservadas, no mezcladas como benchmark |
| Mismo caso después de reiniciar Ollama | 9549 ms, n=1 | Bind loopback/nube desactivada; incluye arranque del modelo; prueba real Edge completa otra vez |
| Restore UI de 450 plantillas | 860 ms, n=1; 5 003 505 bytes | 12 tarjetas materializadas, búsqueda devuelve 1, todos los datos comparados |

[Métricas UI](evidence/goal04/verified/closure/ui-final/ui-regression.json). El comparador de respaldo ordena arrays por ID antes de comparar: orden de array no es contrato; cada dato/ID/fecha/versión sí se compara estrictamente. El error inicial del harness se conserva comprimido. Datasets IA previos y criterios sin alterar.

[Regresión real después del confinamiento Ollama](evidence/goal04/verified/loopback-local-ai/local-ai.json): funcional PASS con guardado/reinicio/cancelación. Su respuesta ya no contiene la ruta incorrecta, pero «Prueba: Funciona bien…» no propone una comprobación concreta. Se registra calidad semántica limitada, sin ocultar el fallo factual de la corrida anterior ni cambiar instrucciones para obtener PASS.

## Reproducción local

Desde la raíz, con tooling **ya instalado**, sin descargar paquetes/navegadores. Detener primero la instancia propia de 5188: los harnesses usan 5188/5189 y DB/perfiles aislados. No apuntarlos a datos del usuario. Todos exigen readiness de su propio proceso.

```powershell
dotnet build src/QuickPrompt.Web.sln --no-restore
dotnet test src/QuickPrompt.Web.sln --no-build --no-restore
npm run css:check
$rerun = 'docs/evidence/goal04-rerun-' + (Get-Date -Format yyyyMMdd-HHmmss)
$env:QP_EVIDENCE_DIR = "$rerun/contracts"
node --test Tests/e2e/bridge-contracts.mjs
node Tests/e2e/provider-adapters.cjs
node Tests/e2e/bridge-races.cjs

$env:QP_EVIDENCE_DIR = "$rerun/regression"
node Tests/e2e/run.cjs
$env:QP_EVIDENCE_DIR = "$rerun/organization"
node Tests/e2e/goal04-organization.cjs
$env:QP_EVIDENCE_DIR = "$rerun/input-state"
node Tests/e2e/goal04-input-state.cjs
$env:QP_EVIDENCE_DIR = "$rerun/layout"
node Tests/e2e/goal04-layout.cjs
$env:QP_EVIDENCE_DIR = "$rerun/experience"
node Tests/e2e/goal04-experience.cjs
$env:QP_EVIDENCE_DIR = "$rerun/ui"
node Tests/e2e/phase03-ui.cjs
$env:QP_EVIDENCE_DIR = "$rerun/restore"
node Tests/e2e/goal02.cjs
$env:QP_EVIDENCE_DIR = "$rerun/endpoint-guard"
node Tests/e2e/endpoint-guard.cjs
$env:QP_EVIDENCE_DIR = "$rerun/large-backup"
node Tests/e2e/goal04-large-backup.cjs
$env:QP_EVIDENCE_DIR = "$rerun/local-ai"
node Tests/e2e/goal04-local-ai.cjs
$env:QP_EVIDENCE_DIR = "$rerun/faults"
node Tests/e2e/goal04-ai-faults.cjs
$env:QP_EVIDENCE_DIR = "$rerun/proposal-error"
node Tests/e2e/goal04-proposal-error.cjs
$env:QP_EVIDENCE_DIR = "$rerun/fallback"
node Tests/e2e/goal04-provider-fallback.cjs
$env:QP_EVIDENCE_DIR = "$rerun/worker"
node Tests/e2e/worker-failures.cjs
$env:QP_EVIDENCE_DIR = "$rerun/edge"
$env:QP_BROWSER_CHANNEL = 'msedge'
node Tests/e2e/extension-fixture.cjs
$env:QP_EVIDENCE_DIR = "$rerun/chrome"
$env:QP_BROWSER_CHANNEL = 'chrome'
node Tests/e2e/extension-fixture.cjs
Remove-Item Env:QP_BROWSER_CHANNEL
Remove-Item Env:QP_EVIDENCE_DIR
```

Playwright usa la instalación existente del equipo; en otro entorno proporcionar PLAYWRIGHT_MODULE autorizado. IA real exige Ollama loopback y qwen3:8b ya instalado. local-ai y experience leen el respaldo **sintético entregado** `docs/evidence/goal04/stage03/authored-backup-v2.json`; no datos privados. Casos HTTP adversos son simulados explícitamente. Los scripts finalizan sus procesos propios incluso ante fallo.

Uso cotidiano: `dotnet run --project src/QuickPrompt.Web --no-build --no-restore`, abrir **http://127.0.0.1:5188**, sin Demo ni DataPath alternativo. AiTimeoutSeconds admite 5..180, predeterminado 180. [Arranque entregado](evidence/goal04/normal-start.json).

## Integridad y evidencia

baseline reproduce defectos aun con 81/81 PASS. stage02…stage06 y final conservan intentos, incluidos FAIL. **No todos los archivos llamados final son PASS**: una regresión posterior de borrador motivó otra corrección. Para estado vigente usar las rutas de **verified** citadas, con su alcance específico. Aceptaciones MVP/GOAL02/Fase03 permanecen históricas.

[Preservación y hashes](evidence/goal04/preservation.json) verifica los 221 archivos de evidencia anteriores, datasets IA y package/lockfile. [Manifest de fuentes](evidence/goal04/source-manifest.json) identifica cambios sin DB/configuración privada. Copia inicial de 74 fuentes en `.playwright/goal04-source` para revisión/rollback; no equivale a commit. [Problemas y decisiones](ISSUES_DECISIONS.md), [paridad actualizada](FEATURE_PARITY.md).
