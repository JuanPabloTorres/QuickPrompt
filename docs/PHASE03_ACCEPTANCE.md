# QuickPrompt Web — aceptación Fase 03

**Estado: PARTIAL_PROVIDER_BLOCKED.** Web local y UI Tailwind verificadas; puente MV3 probado sobre fixtures con navegadores reales. ChatGPT real no pudo validarse (HTTP 403/sin sesión) y Claude automatizado permanece bloqueado por restricciones del proveedor. No se declara PASS_LOCAL_PHASE03, publicación ni disponibilidad comercial/remota.

## Resultado por puerta

| Puerta | Resultado observado |
|---|---|
| A — base | **PASS**: build sin errores/advertencias, 81/81 pruebas, E2E Edge integral repetido con Qwen3 real y reinicio tras la auditoría de carreras. SQLite/JSON v1 sin cambios, pruebas de datos sobre copias sintéticas |
| B — UI/Tailwind | **PASS local**: Tailwind/CLI 4.3.0 instalados tras autorización expresa, lockfile real, build y css:check reproducibles tras npm ci offline. Blazor sirve quickprompt.css; Edge comprueba bytes/token y cinco anchos/temas/idiomas/teclado |
| C — bridge | **Fixtures PASS; sitios reales BLOQUEADOS**. MV3 ejecutado en Edge/Chrome, ambos adaptadores en DOM simulado; ChatGPT real HTTP 403; Claude bloqueado por términos vigentes |
| D — privacidad/seguridad | **PASS en controles ejecutados**, con pruebas manuales pendientes explícitas en QA. Origen/destinos exactos, clic confiable, validación duplicada, replay durable, cancelación, borrador ajeno conservado, metadatos sin texto, sin scraping/cookies/output |
| E — evidencia | **Entregada**, sin convertir controles no ejecutados en PASS. Reportes, capturas antes/después, comandos y límites separados |

Se leyó completamente el CODEX_GOAL.md de Downloads, guardado en [PHASE03_GOAL](PHASE03_GOAL.md), y START_CODEX.md como guía de inicio. Se conserva el objetivo histórico de la raíz. Rama local creada sin commit: `feature/provider-bridge-tailwind`; los cambios sin commit previos permanecen. .NET 8.0.425/8.0.31; Node 22.19/npm 10.9; modelos instalados conservados. Solo se instalaron las dependencias de desarrollo Tailwind/CLI y sus transitivas autorizadas; ningún SDK, modelo ni servicio externo.

## Implementado y comprobado

- `PromptTransferRequest`/estados/acciones tipados; IWebPromptTransfer y módulo JS desacoplados. Sin llamadas del backend a proveedores ni credenciales.
- Extensión local MV3 con contrato/origen/host/TTL/size/replay, adaptadores separados, espera por MutationObserver, preparación literal, no sobrescritura, confirmación de UI y un único intento de envío. Consentimiento por texto/operación y opción Enviar desactivada después de cada acción.
- Fallback con clipboard real y apertura explícita sin texto en URL; no marca lo copiado como enviado. Registro local de 200 metadatos en extensión; sesión UI separada de historial Qwen.
- Se reprodujo fallo de clipboard sin permiso preconcedido al abrir primero otra pestaña; el clic ahora inicia la copia antes de perder foco. La regresión final retira permisos preconcedidos y aprueba con el comportamiento normal de Edge.
- AppIcon, PromptCard, Toast, LoadingSkeleton y ProviderActionMenu; navegación SVG local coherente, tipografía, superficies, temas/modales, estados y móvil. Se preservan biblioteca/editor/variables/categorías/favoritos/historial/importación/restore.
- Baseline descubrió selección asíncrona del editor; se corrigió usando OnAfterRender y espera de condición exacta en el harness. Nuevas pruebas detectaron y corrigieron teal textual insuficiente y transición de tema con contraste transitorio bajo; sin bajar umbrales.

## Evidencia real

[Matriz completa](QA_MATRIX_PHASE03.md), [contrato/permisos/riesgos](PROVIDER_BRIDGE.md), [UX y Tailwind verificado](UI_UX_AUDIT_V2.md), [guía local](../extensions/quickprompt-browser/README.md). Evidencia bajo `evidence/phase03`:

- baseline/build.log y tests.log: 70/70; baseline-after-fix: primera regresión integral aprobada después de corregir la selección.
- final/tests.log: primera suite 79/79 conservada. Suite actual 81/81 en races-final/tests.log y unit-tests.json. Regresión integral actual: races-final/local-regression/browser-workflows.json, browser-offline.json, browser-ai.json, browser-restart.json, accessibility.json, http-security.json; los resultados anteriores en final/ también se conservan.
- extension-runtime.json / chrome/extension-runtime.json: **fixtures exclusivamente**, con manifiesto/SW reales. adapter-fixtures.json: 30 casos DOM. real-providers.json: bloqueos reales, sin envío o extracción.
- Revisión posterior: races-final/bridge-races.json (14/14), worker-failures.json (3/3 APIs inyectadas), adapter-fixtures.json (30/30), extension-runtime.json y chrome/extension-runtime.json. Se corrigen sesión/render/generación en curso, deadlines y confirmación incierta de cancelación; fallos anteriores e intermedios quedan separados.
- ui-regression.json y capturas UI a 320/390/768/1024/1440, oscuro y carga lenta. Las métricas pertenecen a esa corrida automatizada, no a tiempo humano ni a proveedores reales.
- preserved-evidence.json: hashes de evidencia anterior sin sobrescribir. Los fixtures v1/v2 y criterios originales no se alteran.
- **Corrida final con Tailwind**, en tailwind/: solution-build.log y tests.log (81/81); local-regression/ con flujo integral Edge/Qwen3/reinicio, controles HTTP y accesibilidad (57 textos claros/41 oscuros sin fallos detectados). ui/ui-regression.json valida la hoja realmente servida y el flujo sin Ollama; capturas reales en cinco anchos. contracts.log (6), adapter-fixtures.json (30), bridge-races.json (14), worker-failures.json (3) y extension-runtime.json / chrome/extension-runtime.json repiten los controles del puente sin afirmar éxito externo.
- tailwind/css-build.log, css-check-after-ci.log, npm-ci-offline.log y package-lock.json: compilación/reinstalación reproducibles. npm-audit.json conserva los cuatro hallazgos iniciales de la cadena braces; npm-audit-final.json reporta cero tras resolver watcher 2.6.0 mediante CLI 4.3.0, sin force/overrides.

[Resumen final y hashes de código](evidence/phase03/final-summary.json). Se conservaron 64 archivos de evidencia anteriores sin cambios; LocalStore y OllamaClient también mantienen sus hashes de GOAL 02. La comprobación adicional de retención del campo tras compilar/copiar está en current-ui/ui-regression.json (otra muestra automatizada, no sustitución del benchmark citado).

La consulta Qwen3 de la corrida final Tailwind duró **5 410 ms** (n=1; operación abierta distinta del benchmark de calidad GOAL 02; corrida previa 17 786 ms conservada). No se reevalúa exactitud general ni se borran limitaciones Qwen2.5. Seleccionar/completar/compilar/copiar: n=5, p50 **126 ms**, p95 **178 ms**; readiness de servidor con DB nueva: n=1, **992 ms**. CSS servido: **24 849 bytes**, sin gzip. Son mediciones automatizadas pequeñas, no tiempo humano ni mejora estadística demostrada.

Preparar en fixture: Edge n=1 **86 ms**, Chrome n=1 **50 ms**; 1/1 inserción literal y 1/1 confirmación UI por navegador. Inserción/envío en sitios reales: **n=0, tasa no disponible**. No presentar esos tiempos/tasas del fixture como rendimiento del proveedor.

## Comandos reproducibles sin instalaciones implícitas

```powershell
dotnet build src/QuickPrompt.Web.sln --no-restore
dotnet test src/QuickPrompt.Tests --no-restore --logger 'trx;LogFileName=phase03.trx'
npm run css:build
npm run css:check
$env:QP_EVIDENCE_DIR='docs/evidence/phase03-rerun'
node --test Tests/e2e/bridge-contracts.mjs
node Tests/e2e/provider-adapters.cjs
node Tests/e2e/bridge-races.cjs
node Tests/e2e/worker-failures.cjs
node Tests/e2e/extension-fixture.cjs
node Tests/e2e/phase03-ui.cjs
node Tests/e2e/run.cjs
node Tests/e2e/goal02.cjs
node Tests/e2e/endpoint-guard.cjs
node Tests/e2e/provider-readiness.cjs # solo landing aislada; sin autenticación/envío
```

5188 debe estar libre; ejecutar secuencialmente. Los procesos/DB/perfiles son propios/desechables; no usar DB o perfil privado como fixture. Playwright ya existente; no instalar navegadores con npx. Chrome usa QP_BROWSER_CHANNEL=chrome en el harness de extensión y evidencia en otra carpeta.

## Bloqueos restantes y operación local

1. **ChatGPT**: navegación real aislada HTTP 403. Validación manual supervisada con sesión autorizada cuando el sitio lo permita; no automatizar login ni sortear controles. No contar fixtures en esta puerta. La autorización de Tailwind no aporta una sesión externa nueva; no se repitió ni forzó el acceso.
2. **Claude**: sus términos 3.7 restringen automatización sin permiso de Anthropic. No habilitar adaptador a producción ni añadir un bypass. Copiar/abrir manual está disponible.

Tailwind dejó de ser bloqueo con la autorización explícita del usuario. Para reconstruir dependencias en otro checkout hace falta autorización de instalación: `npm ci --ignore-scripts`; en este equipo se comprobó también `--offline`. El CSS compilado está entregado y arrancar/usar la web no requiere npm. Revocación manual de permisos durante un envío, cambio de ventana por una persona y lector de pantalla humano siguen pendientes, identificados en QA. Importación de datos privados reales permanece bloqueada por decisión previa del usuario, sin leer QuickPrompt.db3.

La aplicación queda iniciada en **http://127.0.0.1:5188**, sin Demo ni DataPath: HTTP 200 para aplicación y CSS, listener únicamente 127.0.0.1. [Comprobación de arranque normal](evidence/phase03/tailwind/normal-start.json), sin inspeccionar datos privados. Para iniciarla de nuevo: `dotnet run --project src/QuickPrompt.Web`. Deshabilitar extensión no afecta SQLite o Qwen local. No hubo commits, push, PR, publicación ni acceso a datos privados de origen.
