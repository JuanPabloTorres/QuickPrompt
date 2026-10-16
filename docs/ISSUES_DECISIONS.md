# GOAL 02 — problemas y decisiones

Se preserva PASS_MVP_LOCAL como evidencia histórica; no se reescribe su informe ni sus fixtures. Las evidencias nuevas van a docs/evidence/goal02. El estado de cada puerta se consolida en PRODUCT_READINESS.

| ID | Severidad / estado | Evidencia y decisión |
|---|---|---|
| DATA-01 | Alta, corregido | Constructor creaba tablas/versiones antes de rechazar DB futura. Regresión reprodujo cambio de bytes; comprobación ahora precede DDL dentro de transacción |
| DATA-02 | Alta, corregido | Restaurar aceptaba un JSON legado válido y reemplazaba toda la biblioteca. Ahora requiere respaldo completo versionado; importar legado sigue disponible y aditivo |
| DATA-03 | Media, corregido | Restore persistía preferencias pero UI conservaba idioma/tema/modelo/opt-in antiguos. Reproducción Edge es/en; recarga de preferencias y limpieza de estado temporal tras éxito |
| DATA-04 | Verificado / reforzado | Trigger SQLite aborta una inserción después de DELETE: rollback conserva todo. Exportación usa snapshot transaccional explícito entre colecciones, también frente a otros escritores |
| UX-01 | Media, corregido | Buscar revision no encontraba Revisión. Normalización solo del índice de comparación; texto/variables/DB no se normalizan ni alteran |
| UX-02 | Media, corregido | En móvil seleccionar dejaba foco en la tarjeta y campos debajo. Foco tras render al primer campo; desplazamiento del panel en vista apilada. Crear/editar lleva a título |
| UX-03 | Media, corregido | Edición rápida tras aplicar sugerencia produjo SQLiteSQLite. Se reemplaza value+oninput manual por @bind:get/@bind:set; el harness espera la aplicación de la sugerencia y compara valores exactos antes de compilar |
| A11Y-01 | Media, corregido | aria-pressed booleano se renderizaba vacío; favorito persistía. Se renderizan strings true/false para favoritos y modo visual, con regresión Edge |
| SEC-01 | Alta, corregido | Kestrel:Endpoints sobreescribía UseUrls (reproducido solo en 127.0.0.1:5190). Se rechaza esa configuración antes de construir el host; solo QuickPrompt:Port cambia puerto. Nunca se abrió LAN |
| AI-01 | Media, corrección parcial | Primera evaluación v2: contratos 18/18, pero creación Qwen3 debilitaba una prohibición y Qwen2.5 producía metaplantilla. Instrucción de creación ahora exige preservar prohibiciones y distinguir entradas/salidas. Se conserva corrida inicial |
| AI-02 | Limitación abierta | Qwen2.5 perdió una variable nombrada en una corrida y omitió una prohibición en la última. No se rebajan criterios ni se reintenta hasta PASS. Alternativa configurable con limitación visible; Qwen3 sigue principal |
| AI-03 | Media, corregido | Respuesta JSON sintácticamente válida pero con modelos/message/done de tipo incorrecto escapaba como KeyNotFoundException/InvalidOperationException. Cinco reproducciones fallaron; ahora devuelve indisponible o error controlado, sin cerrar el circuito |
| AI-04 | Media, corregido | E2E detectó análisis truncado. Se separa el contrato interno: hasta cuatro razones/advertencias de 200 caracteres; el código conserva fuente y variables. DTO público de propuesta y formato v1 se mantienen |
| AI-05 | Limitación y ajuste | Consulta abierta agotó 1800 tokens; fue rechazada sin guardar parcial. Se solicita concisión cuando el usuario no pide detalle, sin subir el límite ni imponer un límite de palabras que contradiga al usuario. El truncamiento sigue siendo un error controlado, no una respuesta válida |
| NET10-01 | Bloqueado | Únicamente SDK 8.0.425. Plan incremental con rollback, sin instalar ni retargetear sin ejecución |
| PRIV-01 | Bloqueado por alcance autorizado | Usuario eligió continuar sin datos privados. No leer QuickPrompt.db3 ni buscar otras DB privadas. Importación real solo cuando aporte copia y autorización explícitas |
| DEPS-01 | Ejecutado | Consulta de vulnerabilidades de dependencias directas/transitivas de la solución a fuente oficial NuGet; sin descargas de paquetes. No equivale a garantía de ausencia de vulnerabilidades |

No se añaden pantallas ni dependencias. No se cambia el contrato de plantilla/JSON v1. Los controles de restauración se ajustan al formato de respaldo ya documentado. No se migra a otro diseño, otro frontend ni servicios externos. El perfil normal conserva una plantilla nueva de trabajo creada durante esta verificación; las pruebas destructivas de restore usan DB aislada y datos propios.

## Fase 03 (el párrafo anterior corresponde al alcance histórico GOAL 02)

| ID | Estado | Evidencia / decisión |
|---|---|---|
| P03-BASE-01 | Corregido | Baseline E2E leyó selección vacía al pulsar chip. OnAfterRender aplica selección después del DOM nuevo; harness espera el mismo contenido exacto, sin cambiar criterio |
| P03-SEC-01 | Implementado/probado local | Contrato duplicado .NET/SW, origen exacto, gesto confiable, frame principal, destinos sin URL caller, TTL, 20k, reserva durable/replay y metadatos sin texto |
| P03-SEND-01 | Implementado/probado fixture | Doble fase preparar/verificar/commit, botón inequívoco, pestaña/ventana/foco, un intento, transición UI. Cancelación tardía no afirma deshacer un clic |
| P03-CLAUDE-01 | BLOQUEADO POR PROVEEDOR | Términos 3.7 restringen acceso automatizado sin permiso; SW bloquea antes de abrir/injectar. Copiar/abrir manual permanece. Sin bypass |
| P03-CHATGPT-01 | BLOQUEADO | Landing real aislada 403, cero editor, sin sesión. No automatizar login ni contar fixture como éxito externo |
| P03-TOOL-01 | Resuelto tras autorización | Usuario autorizó Tailwind/CLI v4; versiones finales 4.3.0 fijadas con lockfile real, CSS compilado servido por Blazor, css:check reproducible tras npm ci offline. Sin Node/npm en runtime |
| P03-A11Y-01 | Corregido | Teal textual nuevo medido 4.33:1; derivado más oscuro para texto. Transición de fondo de tema provocaba contraste bajo transitorio; tema atómico y motion de borde/sombra, criterios AA intactos |
| P03-FIXTURE-01 | Corregido en harness | Edge no enrutó primera navegación de pestaña creada por extensión al fixture. Preabrir fixture explícito permite probar adaptador real en página conocida; nuevo tab externo permanece sin aprobación |
| P03-FOCUS-01 | Límite declarado | Headless no demostró pérdida real de foco al cambiar páginas; guard DOM probado con hasFocus=false simulado y SW verifica active/focused. Prueba humana de ventana pendiente |
| P03-LICENSE-01 | Verificado | Lucide 1.8.0 ya instalado con licencia ISC y derivados Feather/MIT; subconjunto SVG y avisos copiados localmente, sin logos de proveedores ni runtime externo |
| P03-CLIPBOARD-01 | Corregido y reproducido | Sin clipboard-write preconcedido, abrir la pestaña antes de la ida/vuelta de Blazor hizo fallar la copia. Se inicia escritura dentro del clic confiable antes de perder foco y se navega al destino fijo después. Fallo preservado en fallback-reproduction; UI final PASS con permisos por defecto |
| P03-RACE-01 | Corregido / fixtures | Revalidar editor conectado/único tras render; no confirmar Prepared sobre un nodo sustituido. El fixture inicial se hizo determinista disparando el reemplazo desde input, sin cambiar la condición esperada |
| P03-RACE-02 | Corregido / fixtures | Auth que aparece antes o intercepta el clic devuelve LoginRequired; generación previa no se toma como confirmación del nuevo envío ni se introduce otro prompt |
| P03-DEADLINE-01 | Corregido / fixtures | Observadores sin mutaciones y frames pausados podían seguir pendientes al caducar. Deadlines/cancelación explícitos también en espera de render, sin sleeps de carga |
| P03-CANCEL-01 | Corregido / 2 contratos .NET | Cancelación de JS interop no demuestra ausencia de un clic. Resultado incierto manual; worker también conserva incertidumbre si falla el RPC tras solicitar commit |
| P03-AUDIT-01 | Corregido / fallo API inyectado | Escritura final del registro rechazada dejaba operación sin respuesta. Ahora devuelve estado realmente observado con advertencia de auditoría; si falla reservar antes, no toca editor |
| P03-NPM-01 | Corregido / auditoría ejecutada | CLI 4.3.3 fijaba watcher 2.5.1 con braces vulnerable. CLI 4.3.0 permite watcher 2.6.0 sin esa cadena; lockfile actualizado dentro del rango admitido, sin overrides/force. Auditoría inicial preservada y final cero hallazgos en tailwind/npm-audit-final.json |

[Aceptación Fase 03](PHASE03_ACCEPTANCE.md). Sin alteración de DB/esquema/JSON v1, datos de origen privados, modelos o thresholds. No commits/publicación. Los cambios aprobados de GOAL 02 se conservan; el rediseño e integración amplían solo el alcance solicitado en Fase 03.

## GOAL 04 — recuperación funcional y organización

Las afirmaciones anteriores conservan su alcance histórico. GOAL 04 sí amplía esquema/exportación mediante migración explícita. [Aceptación y evidencia actuales](GOAL04_ACCEPTANCE.md): PARTIAL_PROVIDER_BLOCKED.

| ID | Estado | Causa / decisión y comprobación |
|---|---|---|
| G04-UX-01 | Corregido | Sidebar 100vh dejaba nav fuera del viewport; solo nav posee scroll, marca/búsqueda permanecen visibles. Hit testing en altura 400 y cinco anchos |
| G04-UX-02 | Corregido | Copia sticky cubría controles posteriores; acción en flujo normal. OverlayHost coordina diálogos nativos, sin subir z-index |
| G04-STATE-01 | Corregido | Reselección borraba inputs y DOM reutilizado mostraba datos discordantes al compilar. Conserva misma identidad/revisión, key en campos, callbacks capturan/validan contexto. Reproducciones fallidas y regresión específica de plantilla/grupo/perfil |
| G04-AI-01 | Corregido | Error SQLite de aceptar aparecía tras modal inert; alert dentro del diálogo, original/versiones intactos, rechazo/reintento. Fallo real inyectado en DB propia |
| G04-AI-02 | Corregido | Contexto anterior podía competir tras navegar/cancelar. Token/contexto capturados, invalidación y exclusión IA/transferencia; respuesta completa sin guardar sobrevive a navegación dentro del circuito; nunca autosave |
| G04-AI-03 | Implementado/probado | Roles system/user con snapshot de instrucciones, presupuesto conservador UTF-8 sin truncamiento; errores vacío/red/HTTP/timeout controlados. Timeout configurable 5..180, default180 intacto |
| G04-AI-04 | Limitación abierta | Qwen3 compuesto: formato correcto pero ruta de DB contextual incorrecta. Funcionalidad PASS no certifica exactitud factual; respuesta solo se guarda por decisión explícita. Datasets v1/v2 intactos |
| G04-DATA-01 | Implementado/probado | SQLite v2 aditivo transaccional, copia previa consistente v1, FK/índices/unicidad, idempotencia, DB futura rechazada antes de DDL. Rollback exige copia/código previo; nuevos cambios v2 no existen en copia v1 |
| G04-DATA-02 | Implementado/probado | Grupos muchos-a-muchos y perfiles/versiones/combinaciones; JSON v2 con lectura v1; import aditivo y restore transaccional completo, sin huérfanos. ID de perfil exige revisión, incluida validación de null |
| G04-DATA-03 | Corregido | Respaldo propio >4 MB no restaurable; mismo límite JSON32MiB en export/import. Export demasiado grande falla controladamente. UI 450 plantillas/5 MB exactos, source intacto; SQLite legado mantiene4MB |
| G04-TEST-01 | Corrección explícita de pruebas | DB futura usa CurrentSchemaVersion+1 en lugar de2 ahora soportado; misma condición de rechazo/bytes. Dos expectativas UI cambian a disabled por falta de extensión/Claude. Contratos de texto/envío y criterios intactos |
| G04-TEST-02 | Corrección de harness | Esperar acknowledgement Blazor antes de clipboard; comparar entidades de archivos por ID sin exigir orden de arrays, preservando todos los valores. Fallos originales conservados, no ocultados |
| G04-WEB-01 | Implementado/probado fixture | Máquina tipada CREATED/PREPARED/TRANSFERRING/INSERTED y resultados; SUBMITTED_UI no equivale a entrega. UNKNOWN conserva incertidumbre de desconexión. Historial local200 metadatos sin prompt, incluido en backup |
| G04-WEB-02 | Corregido/probado | Capacidad/consentimiento/exclusión determinan disabled y razón; copiar/abrir manual no exige consentimiento de inserción y persiste COPIED_OPENED. Sin ampliar permisos MV3 |
| G04-WEB-03 | BLOCKED_REAL_PROVIDER | Usuario: continuar sin sesión. No se prueba ChatGPT auténtico; 403 es histórico Fase03. Claude bloqueado por proveedor. Fixtures nunca se cuentan como éxito externo |
| G04-PRIV-01 | BLOCKED | Sin datos privados por decisión previa. No leer/hash/importar QuickPrompt.db3; toda restauración destructiva solo sobre datos propios autorizados |
| G04-OPS-01 | Verificado en alcance | Loopback y guard Kestrel; npm/NuGet públicos sin hallazgos reportados; no instalación/modelos/publicación/commits. SDK10 sigue ausente; no migración |
| G04-OPS-02 | Corregido al cierre | Listener Ollama estaba en ::11434. Reinicio del mismo ejecutable con bind127.0.0.1 y nube desactivada; OLLAMA_HOST/OLLAMA_NO_CLOUD fijados en ámbito User. No modelos nuevos. Reinicio de Windows/tray no probado; reversión y alcance documentados en LOCAL_AI_WORKFLOWS |

Evidencia histórica preservada mediante hashes; logs fallidos nuevos conservados y alcance vigente identificado en verified. 109/109 y flujos reales Edge pasan; lector de pantalla, uso humano prolongado y producción remota no probados.

## GOAL 05 — simplificación y fricción

[Aceptación vigente](GOAL05_ACCEPTANCE.md), [auditoría UX](GOAL05_UX_AUDIT.md) y [alineación de producto](VISION_ALIGNMENT.md). Los informes anteriores mantienen su alcance histórico.

| ID | Estado | Decisión/causa y comprobación |
|---|---|---|
| G05-UX-01 | PASS | Una biblioteca, header de cuatro responsabilidades, lista/detalle y móvil con Volver; Generar se integra en Copiar, preview expandible. A–F/I/J sin cambiar sección |
| G05-UX-02 | PASS | Funciones avanzadas preservadas mediante disclosure/gestores contextuales; no hay dos interfaces simultáneas ni nuevas rutas. Conteo seleccionado51→16 controles, sin convertirlo en métrica humana |
| G05-STATE-01 | PASS | Borradores de completado por ID/revisión en memoria del circuito; selección no resetea valores, edición confirma descarte; no activa persistencia de valores |
| G05-FOCUS-01 | PASS | Escape podía dejar foco en BODY tras navegación. Restaurar origen conectado o búsqueda y respetar otro modal/foco válido; reproducción/contraste/teclado Edge |
| G05-GEOM-01 | PASS | Checkbox y estrella absolutos se superponían en filas breves; reserva horizontal para ambos. FAIL antes en cinco anchos y PASS después |
| G05-PROFILE-01 | PASS | Borrar perfil dejaba ID/composición C# aunque selector mostrase vacío; limpiar selección y snapshot vivo, preservar reutilización histórica explícita. Clipboard antes incorrecto/ahora exacto |
| G05-EDITOR-01 | PASS | DOM del editor podía reutilizar otra entidad; key y captura del formulario/ID. Creación IA actualiza borrador y no controles de otro ID |
| G05-AI-01 | PASS | Mejora de editor invocaba DraftProposal(create-only) después de mutar UI. Staging nuevo consulta propuesta guardada, valida pending/revisión/variables/candidato, marca drafted transaccionalmente y retorna clon solo tras éxito; sin guardar plantilla/versiones. Replay/invalidación sin escrituras y E2E real cubiertos |
| G05-DATA-01 | PASS | Preview solo lectura comparte composición con Compile; reciente es filtro query opcional; crear/asignar grupo atómico. No cambia esquema ni backup ni CompiledPrompt |
| G05-TEST-01 | PASS | Harness histórico adapta únicamente rutas/disclosures/labels y espera acknowledgements. Permisos de escritura predeterminados en copia diaria; lectura concedida después. Datasets y109 tests originales intactos, seis regresiones añadidas |
| G05-PRESERVE-01 | PASS | 724 evidencias históricas y32 fuentes protegidas verificadas por hash; package/lock/compilador/migraciones/backend de puente/permisos sin cambios |
| G05-WEB-01 | BLOCKED | ChatGPT auténtico sin sesión autorizada por decisión del usuario; Claude automatizado bloqueado por distribución. Fixtures locales no certifican servicios |
| G05-PRIVATE-01 | BLOCKED | Usuario pidió continuar sin datos privados. No se lee/hash/importa DB privada; restore destructivo solo en bases propias |
| G05-PILOT-01 | NOT_TESTED | Cinco personas/<10s/<20s son metas propuestas. Automatización n=1 no prueba intuición, satisfacción o uso prolongado |
| G05-SCOPE-01 | Decisión | QuickPrompt Simple primero; panel lateral/API/nuevos destinos/búsqueda semántica requieren utilidad observada y alcance futuro. No acumular funciones IA |
| G05-DOC-01 | FAIL heredado | Dos enlaces LICENSE de la sección histórica MAUI apuntan a archivo ausente; no fabricar licencia. Enlaces vigentes PASS, inspección completa en evidence/goal05/documentation-links.json. Resolver procedencia antes de distribuir |

Sin instalaciones, descargas de modelos, publicaciones ni commits. Los logs FAIL de iteraciones quedan junto a los PASS identificados por el informe final; no se alteran evidencias previas para fabricar el resultado.

## Seguimiento de uso — 16 de octubre de 2026

| ID | Estado | Causa y corrección |
|---|---|---|
| DU-001 | PASS técnico | Buscar dos palabras separadas/reordenadas no encontraba el prompt. PromptSearch y SQL comparaban la frase completa. Coincidencia determinista por todos los términos normalizados, literales y parametrizados; filtros/paginación intactos. Doce regresiones añadidas,127/127 y E2E Edge con copia/reutilización/móvil/reinicio PASS |
| DU-PILOT | NOT_TESTED | Usuario eligió tareas reales; solicitada su primera tarea concreta. DU-001 usa un escenario técnico sintético, sin participantes ni datos privados |

[Informe y evidencia](DAILY_USE_FINDINGS.md). No se reconstruye la aplicación, no se incorporan capacidades IA ni se modifican los criterios/datasets previos.
