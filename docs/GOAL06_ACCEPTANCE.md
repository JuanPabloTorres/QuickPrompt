# GOAL 06 — QuickPrompt Next

**PASS del flujo local en el alcance probado. BLOCKED para integración con proveedores auténticos. NOT_TESTED para aceptación humana y docking visual del panel.** Fecha: 2026-10-16, America/La_Paz.

[Solicitud](GOAL06.md), [auditoría previa, decisiones y riesgos](GOAL06_AUDIT.md), [resumen verificable](evidence/goal06/final-summary.json), [build sin advertencias ni errores](evidence/goal06/build.log). No se declara completada la validación comercial/humana ni todos los recorridos de sitios externos.

## Entrega

- Editor con nombre propuesto, crear/deshacer variable, renombrar todas las apariciones, defaults consistentes y Undo/Redo por botones o Ctrl/Cmd+Z/Shift+Z/Y. Historia limitada a100 estados, en memoria del editor. Recupera el texto original durante esa sesión; tras cerrar/reabrir un marcador vuelve a su nombre. Selecciones completas de marcador o de su nombre son válidas; selecciones parciales, escapes incompletos y colisiones se rechazan.
- Un `CompiledPrompt` con copia defensiva de Values y texto determinista para copiar, Ollama y puente. Values ahora expone `IReadOnlyDictionary<string,string>`; cambio de contrato C# deliberado para impedir mutación, sin cambiar la forma JSON ni SQLite. Se actualizaron los constructores/test fixtures afectados. `Compile` acepta una revisión esperada opcional; el panel la verifica dentro de la transacción.
- Compartir plantilla mediante texto, Web Share cuando exista, o `quickprompt-template-v1.json`. Proyección mínima sin defaults/historial/IDs/relaciones/preferencias. Respaldo completo v2 sigue independiente y privado; v1 y JSON históricos siguen admitidos.
- Importar/restaurar primero muestra preview. Cancelar no escribe; confirmar revalida toda la estructura y usa la transacción existente. Duplicados y conflictos de identidad se muestran y omiten en importación aditiva; restaurar reemplaza con una segunda confirmación explícita. No se añade resolución destructiva automática.
- Búsqueda por título, descripción, contenido, categoría enum y nombre de grupo; favoritos, recientes, filtros y organización previos conservados. Settings ofrece General, Modelos y proveedores, Mis datos, Privacidad y automatización; opciones técnicas siguen plegadas.
- Quick Assist compacto con comandos locales (`buscar`, `favoritos`, `recientes`, `grupo`, `abrir`) y Qwen para interpretar solicitudes naturales, crear/mejorar, proponer variables y organización. Planes de hasta4 pasos revisados antes de aplicar; no envío, borrado, scripts ni autenticación. Sugerir variables prepara una plantilla nueva para revisión, conservando la original.
- Panel lateral MV3 con búsqueda, favoritos, paginación, variables y compilación en C#. Conectar consulta solo la pestaña local registrada. Copiar/abrir ChatGPT y Claude usa URLs fijas; pegar/enviar sigue siendo manual. Documento y runtime probados en perfil aislado; el docking del navegador no se comprobó.
- Intérprete Playwright separado: QA local y supervisión por allowlist, acciones accesibles tipadas, límites, capturas solicitadas, cancelación, resultados y confirmación interactiva externa. Política entregada únicamente local. No está conectado como herramienta libre del LLM.

## Aceptación por criterio

| # | Criterio solicitado | Estado y evidencia |
|---|---|---|
| 1 | Crear/guardar sin variables | **PASS** [core final](evidence/goal06/final-core/core.json): título/texto, cero campos variables, clipboard exacto |
| 2 | Crear/deshacer variables | **PASS** selección con teclas reales en Edge, renombrar, CtrlZ/ShiftZ y recuperación original; unitarias de escapes/colisiones/multilínea |
| 3 | Reutilizar con otros valores | **PASS** [A–J finales](evidence/goal06/final-layout/users.json), Unicode, repetidos, multilínea y plantilla intacta |
| 4 | Buscar título/texto/categoría/grupo | **PASS** pruebas SQLite cruzando campos, filtros y [organización](evidence/goal06/organization/organization.json) |
| 5 | Favoritos/recientes | **PASS** suite integral, A–J, Quick Assist y contadores/usos transaccionales |
| 6 | Importar/compartir JSON | **PASS** proyección mínima, cancelación/confirmación, roundtrip histórico; [respaldo450 plantillas](evidence/goal06/large-backup/large-backup.json), 5,003,505bytes, fuente intacta |
| 7 | Qwen3 streaming | **PASS real local** [G/H](evidence/goal06/real-qwen/ai.json), tokens visibles, confirmar, copiar, guardar y reiniciar |
| 8 | Mejorar/aprobar/rechazar | **PASS real local** G/H y creación/mejora de borrador; [plan natural Quick Assist](evidence/goal06/assist/assist.json), revisión antes del favorito |
| 9 | Copiar/abrir externo | **PASS** clipboard y URLs sobre destinos enrutados localmente. Inserción/envío en ChatGPT auténtico: **BLOCKED**, sin sesión autorizada. Claude automatizado: **BLOCKED** por política conservada |
| 10 | Playwright en aplicación propia | **PASS** suite Edge y [runner tipado](evidence/goal06/assist/automation/result.json). [Cancelación/timeout/fallo/recuperación](evidence/goal06/automation/automation.json) PASS en servidor de prueba propio |
| 11 | Persistencia/reinicio | **PASS** cierre y nuevo proceso real; SQLitev2, integrity y foreign keys válidas en DB ficticia propia |
| 12 | Ollama ausente | **PASS** core/A–J/robustez con endpoint local inaccesible; no se detuvo el servicio del usuario |
| 13 | Cinco tamaños/dos temas | **PASS** [biblioteca/uso](evidence/goal06/final-layout/users.json) y [30 combinaciones editor/asistente/importación](evidence/goal06/new-ui/layout.json), 320/390/768/1024/1440px, sin overflow ni controles cubiertos en alcance evaluado |
| 14 | Teclado/accesibilidad | **PASS acotado** [contraste muestreado, labels y foco](evidence/goal06/regression/accessibility.json), selección, launcher, modales y campos requeridos. Lector de pantalla/certificación WCAG: **NOT_TESTED** |

## Pruebas y resultados

El baseline observado fue **127/127**; resultado actual **150/150**, cero omitidas, [log completo](evidence/goal06/tests.log). Se añadieron23 casos de motor/editor, privacidad de intercambio, preview, inmutabilidad, planes, rollback, panel y búsqueda. No se instalaron dependencias ni se redujeron umbrales.

| Comprobación | Resultado |
|---|---|
| [Regresión integral](evidence/goal06/regression.log) | **PASS** CRUD/chips/favoritos/archivo/eliminar/JSON/SQLite elegido/backup/ES-EN/temas/seguridad/a11y/IA/reinicio; preview se confirma con clics explícitos en el harness |
| [Robustez](evidence/goal06/robustness/robustness.json) | **PASS**60 campos, Unicode largo, valores no recursivos/defaults, clipboard denegado, atajos, privacidad opt-in y reinicio |
| [Quick Assist](evidence/goal06/assist/assist.json) | **PASS Qwen3 real**; comando léxico no usa el modelo; plan natural y aprobación de favorito; cancelar grupo no escribe |
| [MV3 panel](evidence/goal06/panel/panel.json) | **PASS documento de extensión real**, compilación/clipboard, light-dark390, contenido fuera de storage y resultado tardío descartado; docking **NOT_TESTED** |
| [MV3 Edge](evidence/goal06/bridge-edge/extension-runtime.json), [Chrome](evidence/goal06/bridge-chrome/extension-runtime.json) | **PASS fixtures locales**, inserción exacta y envío separado una vez; no son proveedores auténticos |
| [Fallos de worker](evidence/goal06/worker-failures/worker-failures.json) | **PASS3/3** denegación de almacenamiento/scripting y fallo del audit; sin falsa confirmación |
| [Estado de entrada](evidence/goal06/input-state/input-state.json), [fallos IA](evidence/goal06/ai-faults/ai-faults.json) | **PASS** aislamiento de formularios/selección y recuperación de modelo ausente, respuesta vacía, timeout y stream interrumpido en fixtures |
| CSS | `npm run css:check` **PASS**; Tailwind4.3.0 existente y artefacto compilado reproducible |
| Contratos de bridge/automatización | **PASS8/8** Node: allowlist, identidad, replay, Claude bloqueado, permisos mínimos, acciones tipadas/límites/aprobación |

Las pruebas usan puertos y DB/perfiles propios. Las de extensión copian la extensión a `.playwright` y cambian únicamente el puerto local5188 por5195/5199; el puente web de los fixtures se sirve con ese mismo cambio de origen. No se amplían hosts ni se eliminan controles de seguridad en producción. El usuario no recibió solicitudes externas de prueba: proveedores están enrutados a HTML ficticio. No se importó, leyó ni modificó `QuickPrompt.db3` ni la biblioteca privada normal.

Métricas automatizadas finales n=1: encontrar/copiar sin variables122ms; completar dos variables/copiar174ms; crear/copiar544ms. G/H real: mejora/revisión18885ms; ejecución local1353ms. Incluyen el script y roundtrips, excluyen lectura/escritura humana; no demuestran metas humanas <10s/<20s. Piloto y ausencia de pérdidas en datos privados reales: **NOT_TESTED**; la integridad se valida sobre datos propios.

## Reproducción sin modificar la instancia normal

Herramientas ya instaladas: SDK8.0.425, Node/Playwright del runtime existente, Edge/Chrome y Ollama con Qwen3:8b. Si falta alguna, registrar BLOCKED; no instalar automáticamente. La decisión histórica .NET8 se conserva en [ADR0001](ADR/0001-web-stack.md).

```powershell
New-Item -ItemType Directory -Force .playwright/empty-nuget | Out-Null
dotnet restore src/QuickPrompt.Tests/QuickPrompt.Tests.csproj --artifacts-path .playwright/goal06-build --source .playwright/empty-nuget --ignore-failed-sources
dotnet test src/QuickPrompt.Tests/QuickPrompt.Tests.csproj --artifacts-path .playwright/goal06-build --no-restore
npm run css:check
node --test Tests/e2e/bridge-contracts.mjs Tests/e2e/automation-contracts.cjs
node Tests/e2e/goal06-core.cjs
node Tests/e2e/goal06-assist.cjs
node Tests/e2e/goal06-panel.cjs
node Tests/e2e/goal06-layout.cjs
node Tests/e2e/goal06-automation.cjs
```

Los escenarios nuevos arrancan/cierran su propio servidor, sin Demo. No ejecutar simultáneamente dos harness que usen el mismo puerto. Para suites históricas:

```powershell
$env:QP_WEB_DLL=(Resolve-Path '.playwright/goal06-build/bin/QuickPrompt.Web/debug/QuickPrompt.Web.dll').Path
$env:QP_BASE_URL='http://127.0.0.1:5197'
$env:QP_TEST_PORT='5197'
$env:QP_EVIDENCE_DIR='docs/evidence/goal06/rerun'
node Tests/e2e/run.cjs
```

La instancia normal ya abierta conserva el binario anterior. Guardar trabajo en curso y reiniciarla para cargar este código antes de `dotnet run --project src/QuickPrompt.Web --no-restore`. No se detuvo automáticamente una sesión del usuario ni se cambió su ubicación de datos. La extensión debe recargarse manualmente en el perfil donde se quiera usar; no se instaló allí.

## Bloqueos y trabajo no probado

- **BLOCKED:** runtime MAUI original por SDK/workloads ausentes; no se instalaron. Se auditó su código efectivo.
- **BLOCKED:** validación de ChatGPT auténtico sin sesión/autorización. Claude directo permanece bloqueado; manual disponible. APIs oficiales y sus credenciales/costos no fueron autorizados ni implementados.
- **NOT_TESTED:** docking visual del Side Panel, selector nativo de Web Share, navegador supervisado sobre terceros, evaluación con usuarios y lector de pantalla. Solo se entregó una allowlist local; no hay automatización externa habilitada.
- **NOT_TESTED:** Gemini/Grok/Copilot como adaptadores web nuevos; se usa copia universal y no se declaran capacidades verificadas inexistentes. No se añadió instalación de modelos ni búsqueda vectorial.
- Los FAIL intermedios se conservaron como diagnóstico: nombres/quotes del primer build, esperas del harness, origen fijo del fixture y [overflow320](evidence/goal06/new-ui/failure.png) fueron corregidos. Un screenshot `failure.png` histórico no representa el resultado de los JSON finales enlazados.
