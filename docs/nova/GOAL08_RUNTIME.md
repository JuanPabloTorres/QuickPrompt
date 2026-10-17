# NOVA en vivo — uso y contratos

## Cómo usarlo

En la entrada habitual, elige **Modo de NOVA**:

- **En este lienzo** conserva la preparación normal y muestra su actividad real. Puedes seguir usando biblioteca, editor, variables y destinos manualmente.
- **Navegador visible · Edge** abre una ventana independiente. Qwen elige una acción, QuickPrompt comprueba sus permisos, Playwright utiliza los controles React y el sistema verifica el resultado antes de consultar la siguiente decisión.

Con la biblioteca práctica instalada, prueba:

> NOVA, busca el prompt Escribir un correo profesional, ábrelo y prepara una versión más breve.

Observa la búsqueda, la apertura y el borrador en Edge. **Ver navegador** activa esa ventana incluso mientras una operación espera al modelo. El panel muestra el prompt seleccionado, la operación y unos pocos pasos; **Ver actividad y procedencia** amplía los nombres técnicos y duraciones.

**Pausar** impide comenzar la siguiente operación. La operación que ya estaba en curso puede terminar. **Continuar** comprueba la revisión y la sesión antes de avanzar. **Detener NOVA** cancela las llamadas pendientes y cierra el navegador del run.

También puedes escribir **Cancela la modificación**, **Pausa**, **Continúa** o **Guarda versión** durante una ejecución. Son controles seleccionados por el usuario y resueltos por reglas. Guardar versión abre la revisión; hace falta confirmar su contenido. **Cancelar modificación** o **Rechazar propuesta** conserva el registro original.

Puedes modificar el borrador en el editor de la ventana visible y pulsar **Guardar versión** para revisar tus cambios. Se genera una nueva identidad de propuesta y la confirmación anterior deja de ser válida. Cada confirmación corresponde a un run, prompt, contenido y revisión concretos.

Para utilizar valores del resultado en otra petición, pulsa **Revisar en este lienzo**. Con el prompt seleccionado, por ejemplo:

```text
Completa sus variables con la información de mi petición.
proyecto: API .NET para una biblioteca de barrio
```

Después puedes pedir **Utilízalo con Qwen local**. Los valores etiquetados de la petición actual prevalecen sobre el contexto anterior. La respuesta aparece en streaming en la ventana y queda disponible temporalmente en el lienzo, con la acción de copiar. Ejecutar no guarda el prompt.

El modo navegador admite una ejecución activa a la vez. Termina o detén la revisión pendiente antes de iniciar otra solicitud. Para crear un prompt nuevo utiliza **En este lienzo**: el catálogo de control de navegador cubre buscar, abrir, editar, completar, compilar y utilizar instrucciones existentes.

En preparación directa, si haces referencia a datos que supuestamente ya describiste y Qwen pregunta por ellos, el orquestador contrasta los campos del origen con los valores autorizados. Si todavía faltan, muestra esa pregunta antes de intentar adaptar el texto. Esto no impide preparar una plantilla reutilizable sin rellenar sus variables ni detiene una adaptación cuyos datos ya están disponibles.

## Procedencia observable

| Actor persistido | Explicación visible |
| --- | --- |
| `MODEL_SELECTED` | Decisión de Qwen: nombre y argumentos de una acción realmente recibida del modelo |
| `RULE_SELECTED` | Regla de QuickPrompt: arranque, comprobaciones de sesión y flujo directo de preparación |
| `USER_SELECTED` | Decisión tuya: solicitud, pausa, continuación, cancelación o aprobación |
| `SYSTEM_VALIDATION` | Verificación del sistema: esquema, permisos, identidad, compilación y efectos |

La preparación directa usa un workflow guiado por reglas y consulta el modelo en sus etapas de interpretación/generación. Su orden de herramientas se identifica como selección por reglas. El modo navegador consulta Qwen después de observar los resultados de cada acción. No se registra ni muestra razonamiento interno del modelo.

Una respuesta sin tool call, o argumentos que incumplen el esquema de una herramienta **disponible**, permite **un único reintento de decisión con JSON Schema**, anunciado en la actividad y registrado con `selectionFormat=json_schema_retry`. La propuesta con argumentos inválidos se registra como decisión del modelo y rechazo de validación; nunca se ejecuta ni se corrige por coerción. La siguiente selección procede de una nueva respuesta real de Qwen. Si vuelve a ser inválida, el run se bloquea. Herramientas desconocidas o no disponibles, IDs inventados y permisos insuficientes se rechazan directamente, sin activar ese reintento ni un guion de sustitución.

## Herramientas y comprobaciones

Los argumentos son contratos C# y JSON Schema cerrados. El modelo recibe únicamente acciones disponibles según el estado actual. Todos los objetivos deben ser IDs reales recuperados y vinculados a su revisión.

Un origen que ya seleccionaste y que está visible en la biblioteca se puede abrir directamente: no se obliga al modelo a buscarlo otra vez. Debe inspeccionarlo antes de preguntar por su propósito o sus campos. Si no aparece después de buscar, puede solicitar aclaración. Sin un origen seleccionado se conserva la búsqueda previa y la aclaración ante candidatos ambiguos.

| Acción | Argumentos lógicos / comprobación |
| --- | --- |
| `ui_search_prompt` | `query`; rellena Buscar en la biblioteca y espera la respuesta de esa consulta |
| `ui_open_prompt` | `promptId` visible; hace clic y verifica el ID de Focus View |
| `ui_open_editor` | `promptId`; comprueba identidad y editor real |
| `ui_fill_variable` | `promptId`, `name`, `value`, `evidence`; valida datos etiquetados/contexto autorizado y lee el valor efectivo del textarea |
| `ui_prepare_update` | `promptId`; rellena el cambio con la **petición original del usuario**, consulta el editor Qwen existente y verifica un borrador distinto sin nueva revisión persistida |
| `ui_show_compiled_prompt` | `promptId`; muestra la vista previa real y contrasta su texto con el compilador determinista |
| `ui_open_provider_picker` | `promptId`; abre el selector existente, sin copiar, navegar o enviar externamente |
| `ui_read_visible_state` | Sin argumentos; observa controles y metadatos React |
| `ui_verify_prompt_state` | `promptId`; contrasta identidad, revisión, contenido y compilación con el dominio |
| `ui_execute_local` | `promptId`; exige petición explícita, datos aplicados, compilación válida y respuesta SSE completa en React |
| `ui_request_save_version` | `promptId`; presenta una propuesta para revisión, sin guardarla |
| `finish` / `ask_clarification` | Terminan con resultado verificado o una pregunta concreta |

La descripción del cambio no puede sustituir la petición original mediante un argumento inventado por el modelo. Pedir una versión más breve se verifica también por reducción real de longitud. Las prohibiciones y órdenes explícitas de conservación del origen reconocidas al inicio de una cláusula se conservan completas y se contrastan independientemente dentro del cuerpo propuesto; por ejemplo, «Conserva nombres, fechas y compromisos tal como se proporcionan». Esta comprobación conservadora no constituye una evaluación semántica universal de todas las restricciones posibles. Los campos no se rellenan con saludos como «NOVA», títulos ni palabras del workflow. Una plantilla puede prepararse manteniendo sus variables vacías; la ejecución necesita sus datos.

Abrir, rellenar y ejecutar son efectos distintos. Una operación local no está disponible mientras los valores nuevos autorizados sigan pendientes, aunque los defaults del origen ya permitan compilar. El endpoint de ejecución comprueba además el texto revisado, el borrador, el origen y los permisos del run visible.

## Worker, límites y escritorio

`automation/browser-worker/worker.cts` es un intérprete de acciones lógicas mediante stdin/stdout UTF-8. Usa Edge instalado con **`headless=false`**, un contexto nuevo y el origen exacto de QuickPrompt. El host Windows exige sesión interactiva y bloquea la sesión 0; no recurre a un navegador oculto.

No utiliza perfiles personales. Su política de red bloquea otros orígenes, popups, descargas y escrituras ajenas al catálogo. Los locators son roles, labels y contratos `data-testid` de los controles reales. No hay coordenadas ni pausas cosméticas. El LLM no recibe selectores, JavaScript, `page.evaluate`, CDP, shell ni SQL.

La supervisión de proceso correlaciona respuestas por ID. Activar la ventana tiene un canal de control que puede responder mientras otra acción espera. Cancelar termina el árbol de procesos propio; cerrar el navegador comunica la desconexión al runtime e impide nuevas acciones. Los fallos de locator, modelo o transporte no se registran como éxito.

- Preparación directa: conserva el límite inicial de cinco herramientas y su presupuesto existente.
- Navegador: doce decisiones por defecto, configurable entre 3 y 16 mediante `QuickPrompt:Live:MaxSteps`; ocho minutos por run, incluida la revisión humana.
- Interpretación de acciones: Qwen3:8b, temperatura 0, contexto 12288, hasta 1000 tokens de salida; una alternativa estructurada máxima por decisión cuando falta el tool call o una herramienta disponible lleva argumentos incompatibles. Ambas llamadas comparten el timeout de la etapa.
- Editor de origen: adaptador existente, temperatura 0.2, contexto 8192, hasta 1800 tokens; una corrección máxima si una abreviación no reduce la longitud o falta una cláusula explícita de prohibición o conservación del origen. En preparación directa esa corrección pertenece al pipeline existente, sin añadir otro presupuesto de revisión. Una salida truncada se rechaza y conserva el original.
- Cambios desde el editor visible: petición de hasta 4000 caracteres. Se aplican también los límites existentes de contexto, plantillas, variables y ejecución; no se truncan entradas para aparentar éxito.

## Eventos y recuperación

El [OpenAPI](../../contracts/openapi/nova-v1.json) documenta los endpoints. Los principales son:

```text
POST /api/v1/nova/live                 iniciar navegador o recuperar identidad sin repetir efectos
GET  /api/v1/nova/live/{runId}         estado persistido y vista temporal autorizada
GET  /api/v1/nova/live/{runId}/events  SSE con Last-Event-ID o ?after=secuencia
POST /api/v1/nova/live/{runId}/pause
POST /api/v1/nova/live/{runId}/resume
POST /api/v1/nova/live/{runId}/cancel
POST /api/v1/nova/live/{runId}/approve
POST /api/v1/nova/live/{runId}/proposal
POST /api/v1/nova/live/{runId}/commit  consumo de aprobación desde el control React
POST /api/v1/nova/live/{runId}/focus
```

Cada evento lleva RunId, EventId estable, secuencia monotónica, timestamp UTC, etapa, actor, herramienta/objetivo cuando corresponden, estado, duración/error cuando disponibles y un mensaje seguro de catálogo. Los comentarios keep-alive mantienen el transporte; no representan progreso.

SQLite conserva `nova-live`, `nova-live-event` y recibos `nova-live-receipt`, usando la tabla Documents existente. No se cambia la versión de esquema. Los eventos y checkpoints contienen metadatos, sin consultas, valores, borradores, cookies ni respuestas privadas. El contenido de la vista vive temporalmente en memoria, con límite de cantidad y dos horas de retención. El modo navegador no ofrece el consentimiento de recuperación de contenido del flujo directo.

Un observador SSE puede desconectarse y volver a solicitar eventos. No vuelve a lanzar herramientas. React descarta eventos repetidos y recupera huecos desde la última secuencia confirmada. La pestaña conserva únicamente el RunId aleatorio en sessionStorage para recuperar el panel y sus controles al recargar; no persiste allí peticiones, campos ni borradores. El estado persistido decide los controles y la finalización. En el modo directo, React confirma además la identidad y el texto del resultado realmente renderizado.

Después de reiniciar el proceso, los runs interrumpidos quedan **BLOCKED / INTERRUPTED_NO_REPLAY**. Sus eventos se pueden revisar; para operar se necesita una nueva petición. Los recibos evitan repetir efectos incluso tras una restauración de biblioteca. Restaurar se bloquea mientras existe una ejecución activa y, después, permite iniciar runs nuevos sin reutilizar identidades anteriores.

## Reproducir la verificación

Con dependencias, Edge, Ollama y Qwen3:8b ya instalados:

```powershell
.\scripts\Test-VisibleAgent.ps1 -RealQwen -Regressions
```

Compila TypeScript/React/API en salidas aisladas y ejecuta los recorridos reales y los fallos controlados por separado. No instala software, restaura paquetes, descarga modelos ni abre bases personales. `PLAYWRIGHT_MODULE` permite indicar otra instalación previamente autorizada de Playwright.

Las capturas y WebM del worker solo se habilitan en la batería con `NOVA_UI_QA_OUT` y `NOVA_UI_QA_APPROVED=synthetic-only`, sobre datos públicos de ejemplo. En uso normal no se crean trazas con contenido. Los artefactos generados permanecen locales y excluidos de Git.

La validación corresponde a Windows y Edge en este equipo. No certifica Chrome, MAUI, uso como servicio, bibliotecas grandes, calidad semántica universal ni automatización de envíos externos. Los destinos externos conservan su preparación manual y las políticas de la extensión.
