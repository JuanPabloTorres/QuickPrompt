# GOAL 05 — QuickPrompt Simple

Resultado local: **PASS**. Proveedores auténticos: **BLOCKED**. Piloto humano: **NOT_TESTED**.

[Resumen de evidencias](evidence/goal05/final-summary.json). Aplicación normal disponible en **http://127.0.0.1:5188**; [proceso, cabeceras y listeners observados](evidence/goal05/normal-start.json). Ollama también escucha únicamente en127.0.0.1 en la comprobación final.

Se continuó la aplicación Blazor/SQLite existente. La biblioteca, los campos y **Copiar prompt** forman el recorrido principal en una sola superficie. Crear requiere título y texto; **Usar en…** abre únicamente el destino solicitado. No se añadieron proveedores, dependencias, modelos, rutas, APIs remotas ni panel lateral. [Solicitud GOAL05](GOAL05.md), [dirección de producto recibida](PRODUCT_DIRECTION.md), [alineación y siguiente etapa](VISION_ALIGNMENT.md).

## Evidencia y alcance

Pruebas ejecutadas en este equipo con SDK8.0.425, Edge155.0.4283.45, Chrome145.0.7632.6 para el puente, Tailwind/CLI4.3.0 ya instalados y Qwen3:8b local. Baseline **109/109** comprobado antes de modificar; entrega **115/115**, cero omitidas. [Baseline](evidence/goal05/baseline/tests.log), [pruebas finales](evidence/goal05/tests-accepted.log), [build: cero advertencias/errores](evidence/goal05/build-accepted.log), [CSS reproducible](evidence/goal05/css-accepted.log).

Toda automatización usa perfiles limpios y bases ficticias propias. Las pruebas nuevas A–J y de organización arrancan **sin Demo**, crean datos y reinician realmente el proceso. La suite histórica integral conserva su siembra Demo aislada. No se ha leído, importado ni calculado hash de ninguna DB privada o QuickPrompt.db3. El arranque normal final y sus listeners se registran en [normal-start.json](evidence/goal05/normal-start.json); su comprobación HTTP solo lee cabeceras, sin inspeccionar la biblioteca del usuario.

## Criterios de aceptación

| Criterio GOAL05 | Estado | Comprobación |
|---|---|---|
| 1–3 Biblioteca única, copia sin editor, campos inmediatos | PASS | [A–F/I/J Edge](evidence/goal05/users-final/users.json); no sidebar ni navegación primaria entre secciones |
| 4 Acción principal identificable | PASS de inspección/automatización | Copiar destacado; capturas comparables y controles visibles. Intuición de personas: NOT_TESTED |
| 5 Crear con solo título y texto | PASS | F y [robustez](evidence/goal05/verified/goal05-robustness/robustness.json), sin metadatos obligatorios |
| 6–7 Organización, perfiles e IA opcionales | PASS | A–F/J con Ollama inaccesible; controles avanzados fuera del recorrido básico |
| 8 Geometría, teclado y móvil | PASS en alcance probado | Diez combinaciones básicas y [30 avanzadas](evidence/goal05/accepted/goal04-experience/experience.json), cinco anchos, dos temas, ES/EN, altura400 y reduced motion |
| 9 Datos/relaciones conservados | PASS sobre fixtures propios y archivos protegidos | SQLitev2, FK, migraciones, respaldo/restauración exactos; [preservación de 724 evidencias y 32 fuentes](evidence/goal05/preservation.json). Importación privada: BLOCKED |
| 10 Qwen y fallback | PASS local | [G/H con Qwen3 real](evidence/goal05/ai-accepted/ai.json); [fallback exacto y reinicio](evidence/goal05/fallback-accepted/fallback.json). Sitios auténticos: BLOCKED |
| 11 Regresión previa/nueva sin reducir cobertura | PASS | [Suite integral Edge](evidence/goal05/regression-accepted.log), 115/115 y pruebas específicas; datasets originales sin cambios |
| 12 Interfaz más simple | PASS de inspección | Misma plantilla/valores: 51→16 controles en el documento seleccionado; ocho entradas de sidebar→cero. Es un recuento reproducible, no evaluación humana |

## Flujos A–J y métricas

Una acción es un clic, relleno, selección o confirmación explícita del escenario. Navegación cuenta cambios de sección principal; los despliegues contextuales cuentan como acciones. La apertura inicial y preparación de fixtures no se incluyen. J incluye crear/reutilizar y Todos; I incluye elegir ambos proveedores, con el prompt ya seleccionado/completado. La confirmación de borrado sí se cuenta en E. El scroll automático y las verificaciones no son acciones de usuario.

Cada fila es **n=1 automatizado**; tiempos en ms, incluyen roundtrips Blazor y verificación de clipboard, excluyen lectura/escritura humanas. Baseline concedía permisos antes de copiar; la prueba final escribe con permiso predeterminado y concede lectura después. No son medidas de rapidez humana ni fundamento para porcentajes. E/G/H/I/J carecen de baseline comparable. [Antes](evidence/goal05/baseline/ux-baseline.json), [después A–F/I/J](evidence/goal05/users-final/users.json), [G/H](evidence/goal05/ai-accepted/ai.json).

| Caso | Antes acciones/nav/ms | Después acciones/nav/ms | Resultado |
|---|---|---|---|
| A Encontrar/copiar sin variables | 4/0/160 | 3/0/116 | PASS texto exacto |
| B Dos variables, repetición/Unicode/multilínea | 6/0/207 | 5/0/223 | PASS texto exacto |
| C Reutilizar con valores diferentes | 4/0/157 | 3/0/73 | PASS original intacto |
| D Favorito, localizar y copiar | 5/1/221 | 4/0/123 | PASS filtro inmediato |
| E Crear/asignar/filtrar/borrar grupo | No medido | 13/0/431 | PASS conserva plantilla |
| F Crear y copiar inmediatamente | 6/0/587 | 5/0/521 | PASS solo dos campos |
| G Mejorar, revisar, rechazar y aceptar | No medido | 6/0/15847 | PASS Qwen3 real y versión nueva |
| H Completar, ejecutar, copiar/guardar/cerrar | No medido | 9/0/2196 | PASS Qwen3 real; ejecución1841ms |
| I ChatGPT y Claude, fallback | No medido | 5/0/382 | PASS clipboard/URL fija; destinos enrutados localmente |
| J Crear/completar/copiar sin Ollama | No medido | 7/0/197 | PASS endpoint IA inaccesible |

La menor cantidad de acciones proviene principalmente de unir Generar y Copiar. B fue más lento en esta observación; no se oculta esa variación. Las metas propuestas de <10s/<20s y 4/5 personas siguen pendientes de un piloto humano.

## UI y funcionalidades preservadas

[Auditoría antes/después](GOAL05_UX_AUDIT.md) y [matriz de paridad](FEATURE_PARITY.md). Se conservaron categorías, favoritos, búsqueda paginada, grupos muchos-a-muchos, perfiles, combinaciones, editor visual, defaults, versiones, archivo, historial, import/export, migraciones, Ollama y MV3. La previsualización usa la misma composición determinista de Compile, sin escribir historial. Un único CompiledPrompt conserva el texto utilizado por copia, Ollama y puente. Los valores transitorios por plantilla/revisión viven en el circuito; no se activa silenciosamente RememberValues. El historial conserva sus snapshots incluso tras borrar un perfil.

Comparación visual con **Mensaje breve**, mismos valores y ancho:

| Pantalla | Antes | Después |
|---|---|---|
| Escritorio1440, claro | [Captura](evidence/goal05/baseline/selected-1440-light.png) | [Captura](evidence/goal05/users-final/comparable-1440-light.png) |
| Móvil390, claro | [Captura](evidence/goal05/baseline/selected-390-light.png) | [Captura](evidence/goal05/users-final/comparable-390-light.png) |
| Móvil320, oscuro | Sin comparación equivalente | [Captura](evidence/goal05/users-final/selected-320-dark.png) |

Capturas inspeccionadas visualmente además de las aserciones. Móvil alterna lista/detalle con Volver; escritorio mantiene dos columnas. Los controles se prueban con hit testing durante scroll, no solo con screenshots. [Accesibilidad](evidence/goal05/regression-accepted/accessibility.json): contraste muestreado, labels, foco visible y retorno de modal. No equivale a certificación WCAG ni a prueba con lector de pantalla.

## Regresiones y causas corregidas

| Defecto reproducido | Corrección y evidencia |
|---|---|
| Escape podía dejar foco en BODY tras cambiar de superficie | Retorno a control conectado o búsqueda estable, sin desplazar un modal más reciente; [reproducción](evidence/goal05/focus-reproduction.log) y suite integral final |
| Selección múltiple tapaba estrella en filas cortas | Espacio horizontal reservado para checkbox y favorito; [FAIL antes](evidence/goal05/bulk-before/bulk.json), [PASS final](evidence/goal05/accepted/goal05-bulk-geometry/bulk.json) |
| Borrar perfil dejaba composición anterior activa pese a selector vacío | Invalidar selección/composición viva; mantener snapshot histórico explícito; [FAIL](evidence/goal05/profile-before.log), [PASS](evidence/goal05/accepted/goal05-optional-profile/profile.json) |
| Editor reutilizaba controles de otra identidad | Key por ID, bindings capturan formulario y Save valida identidad; suite integral de creación IA y regresión de estado |
| Aceptar mejora dentro del editor modificaba texto antes de fallar DraftProposal | Nuevo staging tipado transaccional consulta propuesta pendiente autoritativa, valida revisión/variables/candidato y devuelve clon solo tras commit; no escribe plantilla/versiones. Contrato create-only de DraftProposal intacto. [Fallo anterior](evidence/goal05/acceptance-ai/failure.png), [115 pruebas](evidence/goal05/tests-accepted.log), [E2E editor real](evidence/goal05/ai-accepted/ai.json) |
| Clipboard denegado dejaba preview plegada | Abrir disclosures ancestros y enfocar texto exacto para copia manual; robustez PASS |

Se corrigieron también esperas del harness: confirmar respuesta renderizada antes de leer clipboard, abrir disclosures antes de probar controles avanzados y esperar cierre de gestores. `ui-actions.cjs` solo navega/clica interfaces reales. Los nuevos A–J usan interacciones directas. No se inyectan resultados DOM en la aplicación. Los fallos de iteraciones permanecen; archivos `first`, `before`, `final` antiguos o `verified-runs.json` no representan todos el resultado vigente. Las referencias de este informe identifican los PASS finales; los fallos de causa corregida permanecen como evidencia.

## Otras regresiones ejecutadas

| Prueba | Resultado / alcance |
|---|---|
| [Suite integral](evidence/goal05/regression-accepted.log) | PASS CRUD, archivo/restauración, import JSON/SQLite autorizado sintético, integridad, temas/idiomas, seguridad HTTP, a11y, propuestas/sugerencias/análisis/creación, Qwen3, cancelación y reinicio |
| [Organización](evidence/goal05/accepted/goal04-organization/organization.json) | PASS grupos, bulk, filtros, perfiles/versiones/duplicar/borrar, composición, combinaciones, backupv2 y reinicio |
| [Robustez](evidence/goal05/verified/goal05-robustness/robustness.json) | PASS 60 campos, defaults, escapes, Unicode/multilínea, no sustitución recursiva, CtrlK+Enter, CtrlEnter copia sin enviar, salida de editor confirmada, clipboard denegado, reinicio; SQLite2 integrity=ok/FKvacías |
| [Respaldo grande](evidence/goal05/verified/goal04-large-backup/large-backup.json) | PASS 450 plantillas, 5,003,505bytes, restore808ms n=1, roundtrip exacto por ID/valores, fuente intacta |
| [Composición IA real](evidence/goal05/verified/goal04-local-ai/local-ai.json) | PASS Qwen3, streaming, respuesta sin guardar tras navegación, guardar/reinicio y cancelación/contexto; 10090ms n=1 |
| [Fallos IA](evidence/goal05/verified/goal04-ai-faults/ai-faults.json) | PASS HTTP simulado: modelo ausente, vacío, timeout/drop y recuperación; no es calidad real del modelo |
| [Error SQLite de propuesta](evidence/goal05/verified/goal04-proposal-error/observed.json) | PASS rollback real sobre DB propia con propuesta HTTP simulada; error dentro del modal, original/versiones intactos, rechazo y reintento |
| [Fase03 UI](evidence/goal05/phase03-ui/ui-regression.json) | PASS CSS servido exacto, ES/EN, fallback, cinco anchos, modal oscuro, reduced motion y recuperación de startup lento |
| [Preferencias tras restore](evidence/goal05/restore-preferences-accepted/restore-preferences-after.json) | PASS idioma/tema/modelo restaurados, advertencia Qwen2.5 visible mediante opciones avanzadas |
| [Guard Kestrel](evidence/goal05/restore-preferences/kestrel-override-after.json) | PASS overrides rechazados antes del arranque |
| [Contratos6](evidence/goal05/contracts/node-contracts.log), [adaptadores30](evidence/goal05/contracts/adapter-fixtures.json), [carreras14](evidence/goal05/contracts/bridge-races.json) | PASS contratos y DOM local simulado, no proveedores auténticos |
| [MV3 Edge](evidence/goal05/bridge-edge-stage2/extension-runtime.json), [Chrome](evidence/goal05/bridge-chrome/extension-runtime.json), [worker3](evidence/goal05/worker-stage2/worker-failures.json) | PASS manifiesto real/perfil aislado/targets locales y fallos API inyectados; inserción exacta, envío explícito una vez, borrador ajeno intacto, cancelación y metadatos sin texto |

## Bloqueos y límites

- **BLOCKED** ChatGPT auténtico: decisión previa de continuar sin sesión autorizada. No se accede a conversaciones ni se automatiza autenticación. El HTTP403 referido en documentos previos es histórico de Fase03, no una nueva prueba.
- **BLOCKED** Claude automatizado: restricción de distribución existente conservada, sin opción de evasión. Copiar/abrir manual funciona sobre target local enrutado.
- **BLOCKED** importación privada: el usuario pidió continuar sin datos privados. **BLOCKED** migración .NET10: SDK no instalado/autorizado; no se declara compatibilidad.
- **NOT_TESTED** cinco participantes, uso prolongado humano, lector de pantalla y reinicio de Windows. No se certifica producto comercial ni disponibilidad LAN/Internet.
- **NOT_TESTED** nueva auditoría de advisories externos en esta fase. No cambian lockfile/dependencias; la auditoría GOAL04 conserva su fecha/alcance y no se presenta como actualizada.
- **FAIL heredado de documentación**: dos enlaces LICENSE del bloque histórico MAUI apuntan a un archivo ausente en esta copia. No se inventó ni sustituyó una licencia. Los enlaces locales de documentación vigente pasan; [inspección completa](evidence/goal05/documentation-links.json). No impide el flujo local, pero requiere resolver procedencia antes de una distribución.
- **NOT_TESTED** semántica/factualidad independiente en estos casos nuevos. Los flujos prueban contratos, revisión, variables, guardado y respuesta real. [Limitaciones históricas IA](AI_QUALITY.md) siguen vigentes: Qwen3 puede equivocarse y Qwen2.5 sigue siendo alternativa con limitaciones observadas. No se tocaron los datasets v1/v2 ni los criterios.
- Panel lateral, nuevos destinos, búsqueda semántica y APIs oficiales son dirección futura, **no implementados ni certificados** en esta entrega. No se guardan credenciales ni se activan conexiones externas nuevas.

## Reproducción y rollback

No ejecutar instalaciones. Usar tooling/modelos ya disponibles y puertos5188/5189 libres para E2E. Cada harness es dueño de su proceso y DB; no apuntar a una DB privada. Ejecutar desde la raíz:

```powershell
dotnet build src/QuickPrompt.Web.sln --no-restore
dotnet test src/QuickPrompt.Web.sln --no-restore
npm run css:check
$run = 'docs/evidence/goal05/rerun-' + (Get-Date -Format yyyyMMdd-HHmmss)
$env:QP_EVIDENCE_DIR = "$run/daily"
node Tests/e2e/goal05-users.cjs
$env:QP_EVIDENCE_DIR = "$run/ai"
node Tests/e2e/goal05-ai.cjs
$env:QP_EVIDENCE_DIR = "$run/full"
node Tests/e2e/run.cjs
foreach ($case in @('goal04-organization','goal04-experience','goal05-robustness',
 'goal05-optional-profile','goal05-bulk-geometry','goal04-large-backup',
 'goal04-local-ai','goal04-ai-faults','goal04-proposal-error','goal04-layout',
 'goal04-input-state','goal04-provider-fallback','phase03-ui','goal02','endpoint-guard')) {
 $env:QP_EVIDENCE_DIR = "$run/$case"
 node "Tests/e2e/$case.cjs"
 if ($LASTEXITCODE -ne 0) { throw "Falló $case" }
}
$env:QP_EVIDENCE_DIR = "$run/contracts"
node --test Tests/e2e/bridge-contracts.mjs
node Tests/e2e/provider-adapters.cjs
node Tests/e2e/bridge-races.cjs
$env:QP_EVIDENCE_DIR = "$run/worker"
node Tests/e2e/worker-failures.cjs
$env:QP_EVIDENCE_DIR = "$run/edge"
node Tests/e2e/extension-fixture.cjs
$env:QP_BROWSER_CHANNEL = 'chrome'
$env:QP_EVIDENCE_DIR = "$run/chrome"
node Tests/e2e/extension-fixture.cjs
Remove-Item Env:QP_BROWSER_CHANNEL
$env:QP_EVIDENCE_DIR = $run
node tools/goal05-preservation.cjs
Remove-Item Env:QP_EVIDENCE_DIR
# Uso normal después de cerrar los harness:
dotnet run --project src/QuickPrompt.Web --no-restore
```

`PLAYWRIGHT_MODULE` permite señalar un módulo existente; el fallback local del equipo está en los scripts. Ningún comando descarga navegadores/modelos. `normal-baseline.cjs` y `goal05-baseline.cjs` son reproducciones históricas del UI anterior, no comandos para validar el diseño actual. No ejecutarlos contra código actual ni sobrescribir evidence histórica.

Para rollback de código, detener el proceso y recuperar los archivos públicos previos desde `.playwright/goal05-source` según el inventario [source-hashes](evidence/goal05/baseline/source-hashes.json); los archivos nuevos requieren revisión manual. Es una copia local no versionada, no un respaldo de datos ni un mecanismo de despliegue. GOAL05 no exige rollback de DB: conserva SQLite/JSONv2. Conservar respaldo autorizado antes de cualquier restauración de datos. No hubo commit, push, publicación ni acceso privado por las herramientas.
