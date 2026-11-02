# Herramientas de NOVA

Guía del catálogo existente y su contrato con el agente. El checklist de cierre sigue siendo [R1–R6](GO_TO_MARKET.md); esta guía no acredita la aceptación semántica ni la publicación de R2.

## Acceso completo por tarea

NOVA debe conocer las capacidades del producto y recibir el contrato completo de las herramientas que puede utilizar en el estado actual. El catálogo se obtiene de declaraciones del código de la aplicación. Los datos de una plantilla, una conversación o una respuesta de IA no registran herramientas ni conceden permisos.

Cada herramienta ofrecida incluye nombre, descripción funcional, esquema exacto de argumentos, permiso y tiempo límite. El mismo conjunto se utiliza para explicar las herramientas en los mensajes, restringir la propuesta del modelo y validar la llamada antes de ejecutarla. El esquema de salida de NativeOutput restringe la forma de una respuesta; por sí solo no entrega al modelo un manual semántico en sus mensajes.

El agente observa el estado actual y los recibos de acciones anteriores. Después propone una herramienta elegible. El ejecutor valida objetivo, identidad, revisión, valores, permisos y presupuesto; realiza la operación y vuelve a observar su resultado. `finish` se acepta sólo cuando las postcondiciones solicitadas están comprobadas.

## Catálogo UI declarado

Hay 23 declaraciones en `UiCapabilities`. El catálogo predeterminado omite las dos capacidades de representación TEMPLATE, que se habilitan mediante la elección explícita correspondiente.

| Responsabilidad | Herramientas | Entrada y resultado |
| --- | --- | --- |
| Observar y comprobar | `ui_read_visible_state`, `ui_verify_prompt_state` | Estado React actual; comparación independiente de identidad, revisión y valores |
| Encontrar y abrir | `ui_navigate_library`, `ui_search_prompt`, `ui_open_prompt` | Consulta acotada y UUID real devuelto/seleccionado; identidad visible comprobada |
| Recuperar trabajo | `ui_load_active_draft` | Artefacto ligado por el servidor a esta sesión; no texto nuevo aportado por el modelo |
| Crear | `ui_open_create_prompt`, `ui_prepare_creation` | Tarea humana establecida conservada literalmente, revisión final y borrador; no persistencia |
| Adaptar | `ui_open_editor`, `ui_prepare_update` | Fuente/revisión y petición original autorizadas; propuesta pendiente de revisión humana |
| Completar entradas | `ui_fill_variable` | Nombre real, valor autorizado y evidencia literal; valor aplicado al campo correcto |
| Compilar y preparar destino | `ui_show_compiled_prompt`, `ui_open_provider_picker` | Vista compilada determinista y selector de destino; compilar no ejecuta |
| Revisar/copiar plantilla | `ui_show_template`, `ui_copy_template` | Representación TEMPLATE elegida explícitamente; recibo nuevo del texto exacto |
| Copiar compilado | `ui_copy_compiled_prompt` | Compilado actual y recibo nuevo de clipboard; sólo con solicitud de copia |
| Ejecutar | `ui_execute_local` | Texto compilado, datos completos y ejecución local autorizada; respuesta observada |
| Solicitar guardado | `ui_request_save_version` | Propuesta exacta ligada a hash y revisión; aprobación humana pendiente |
| Eliminar con revisión | `ui_review_deletion`, `ui_enable_deletion_confirmation`, `ui_verify_deletion` | Registro/revisión ligados a revisión humana, confirmación y comprobación posterior |
| Aclarar y finalizar | `ask_clarification`, `finish` | Pregunta concreta o verificación de todas las condiciones de la tarea |

La disponibilidad se calcula desde el estado y la tarea autorizada en `UiToolCatalog.Available` y `AgentOrchestrator.RunVisible`. Los nombres conocidos no hacen que una herramienta esté habilitada. El ejecutor ya lee y comprueba el estado entre operaciones; esa lectura no necesita otra inferencia redundante.

## Superficies que conservan sus propietarios

`PreparationReadTools` ofrece `get_prompt`, `get_prompt_revision` y `list_groups` sobre los servicios de biblioteca y colecciones. `PreparationTools` conserva contratos tipados, permisos por llamada, cancelación y presupuesto. Su registro no es un conjunto de funciones arbitrarias elegido por texto del modelo.

MCP tiene otra superficie: buscar prompts, buscar colecciones, leer y compilar, más creación cuando la conexión la permite. Sus scopes, identidad, consentimiento e idempotencia siguen siendo propios de MCP. Una capacidad MCP no concede automáticamente el mismo permiso a NOVA ni viceversa.

No se necesita un registro universal nuevo, otro servidor o una segunda base para completar este catálogo. Extender el propietario de una capacidad existente cuando una petición admitida no tenga herramienta; comprobar ambos consumidores cuando se afirme reutilización.

## Edición interna del candidato R2

El editor nuevo permanece aislado hasta superar su campaña semántica. Sus
operaciones son propuestas de contenido, no nuevas herramientas UI ni permisos:

| Operación | Entrada y control del host |
| --- | --- |
| KEEP | Copia exacta del snapshot confirmado |
| COMPRESS | Alternativas completas de subsecuencias originales; IDs y hash, variables y literales numéricos reconocidos conservados por campo |
| REWRITE | Mapa fijo de campos; texto nuevo con IDs de entradas elegidos por la persona; metadatos y papeles de referencias permanecen fuera de escritura |
| TRANSLATE | Primera llamada prepara preview e idiomas por campo; el host extrae vocabulario y consulta el corpus; segunda llamada elige acepciones identificadas y traduce campos completos con marcadores léxicos inmutables |

La petición actual determina el idioma destino. El idioma fuente se observa por
campo, sin deducirlo de la UI ni del idioma que deba usar el asistente futuro.
Las restricciones de la tarea conservan precedencia sobre preferencias de cuenta.
Una inferencia de idioma es un dato provisional; no concede autoridad.
Las preferencias existentes admiten idioma `es`, `en` o ausencia de preferencia;
los formatos son `JSON`, `Markdown`, `Tabla`, `Texto` o ausencia de preferencia.
La UI y `PreparationPreferenceRules` comparten esos valores. La preparación de
traducción declara idiomas por campo; el recurso local sólo acredita su par
soportado, no cualquier idioma que el modelo pueda reconocer.

La lectura léxica no pide al modelo copiar listas de palabras. Extrae los
segmentos literales del preview, excluye variables y normaliza NFC/mayúsculas
sólo para consultar. Una consulta por par de idiomas conserva todos los sentidos
recuperados y señala faltantes. La evidencia es local, versionada y separada del
contrato humano. El recurso actual cubre español→inglés mediante la inversión
completa del diccionario EN→ES; otros pares muestran explícitamente falta de
evidencia local, sin atribuirles esa cobertura.

La composición léxica identifica ocurrencias por campo y rango UTF16. Sólo usa
slots donde existen coincidencias y todas las categorías inglesas declaradas son
verbales. Cada elección conserva sus IDs de entrada, acepción y headword; no
escoge un primer significado. El modelo devuelve cada campo completo y copia
una vez cada marcador léxico en su campo. El host comprueba la correspondencia
entre acepción, palabra exacta y mayúsculas antes de resolver los marcadores.
En los campos con slots conserva el orden relativo de marcadores y entradas
originales; sin slots conserva la cantidad de cada entrada por campo. Los IDs
nuevos todavía deben pasar el contrato humano de variables existente.

Estas comprobaciones no demuestran categoría gramatical española, exhaustividad
del diccionario, gramática inglesa ni fidelidad de agentes/condiciones. La
redacción de cada campo sigue siendo generativa. KEEP_SOURCE conserva nombres o literales cuando corresponda;
dejar español ordinario sin traducir no aprueba una petición inglesa. La
evaluación contrasta el resultado completo y conserva cada fallo.

El corpus incluye 64.258 entradas y 133.073 aristas. Añade aproximadamente 4,9 MB
comprimidos y su [aviso de licencia](../../backend/QuickPrompt.Inference/Resources/NOTICE.translation-lexicon.md)
a la distribución. Primera carga local observada: 1,76–2,14 segundos y unos
70,5 MiB administrados retenidos; pico y entorno publicado pendientes. No añade
otro proveedor de IA, dependencia o consulta de red en ejecución.

El presupuesto sigue siendo dos llamadas, 1400 + 3500 tokens, 90 segundos
compartidos y 26000 bytes UTF8 de material completo más esquema de respuesta.
Catálogo, evidencia y esquema cuentan juntos. Si no caben, se devuelve el
control; no se recortan la fuente, las acepciones o los slots, ni se cambia de
mecanismo después de un fallo del modelo.

## Datos que deben permanecer separados

| Dato | Propietario/uso |
| --- | --- |
| Petición actual y antecedente autorizado | Objetivo de la tarea, sin reemplazarlo por un resumen del modelo |
| Texto reutilizable | Instrucciones para un asistente futuro; sus verbos no son órdenes para la aplicación actual |
| Nombres de variables | Control humano y contrato de campos; IDs canónicos cuando interviene el editor |
| Valores del caso | Datos autorizados con fuente y ámbito; no se convierten en instrucciones de edición |
| Preferencias de idioma/formato | Restricciones simultáneas que debe poder cumplir la herramienta seleccionada |
| Fuente y revisión | Identidad real y base de la propuesta; un cambio de revisión invalida la operación pendiente |
| Configuración de modelo/proveedor | Operador local; no se elige desde el cuerpo de una plantilla |
| Aprobaciones | Contenido y efecto ligados al artefacto exacto; una propuesta no los fabrica |

## Parámetros comprobables

El perfil actual conserva Qwen3 8B fijado en Docker Model Runner, contexto 12288, una secuencia local y keep-alive 5 minutos. La autoría comparte 90 segundos y hasta dos llamadas; cada etapa tiene `max_requests=1`. Las salidas actuales se limitan a 1400 y 3500 tokens. El decisor UI usa su propio límite de salida de 1000 y el presupuesto del recorrido existente; estos límites no deben sumarse o reiniciarse ocultamente.

La información completa de herramientas cuenta en el presupuesto del paquete. Si no cabe, se rechaza la petición antes de consultar el modelo; no se eliminan silenciosamente argumentos o condiciones. El perfil de razonamiento original está restaurado. No hay una mejora de calidad demostrada por cambiar flags, temperatura o modo de salida.

## Verificación de una extensión

1. Capturar los mensajes y el contrato canónico para demostrar qué ve el modelo y qué valida el host.
2. Ejecutar positivos y negativos de argumentos, identidad, revisión, estados y aprobaciones; una herramienta ausente debe seguir rechazada.
3. Consultar el proveedor real con herramientas y datos sintéticos, preservando cada primera respuesta y los límites.
4. Verificar el efecto y su lectura posterior en el flujo pertinente. Ningún recibo de inferencia equivale por sí solo a éxito de la tarea.
5. Ejecutar la campaña R2 congelada e independiente en el candidato final. El objetivo sigue siendo 18/20 con 9/10 por grupo, no sólo JSON válido.

Referencias técnicas: [function calling de Qwen](https://qwen.readthedocs.io/en/stable/framework/function_call.html), [modos de salida de Pydantic AI](https://ai.pydantic.dev/output/). El adaptador actual de Core comunica propuestas JSON estructuradas; no se presenta como una integración nueva de ejecución nativa Hermes. Cualquier cambio de transporte requiere su contraste propio y conservar los validadores existentes.
