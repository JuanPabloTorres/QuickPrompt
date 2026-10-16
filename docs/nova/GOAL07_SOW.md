# GOAL 07 — QUICKPROMPT NOVA AUTONOMOUS PROMPT ENGINE

## MISIÓN

Actúa como arquitecto de IA, ingeniero de software senior en React/TypeScript y .NET, especialista en agentes locales y automatización Playwright, y evaluador de calidad.

Proyecto: QuickPrompt NOVA.

Objetivo: permitir que el usuario describa una necesidad en lenguaje natural y que NOVA realice automáticamente el trabajo necesario para localizar, adaptar, crear, completar y preparar el prompt correspondiente.

El usuario NO debe estar obligado a escribir manualmente plantillas, identificar variables, seleccionar instrucciones técnicas ni recorrer formularios para completar su intención.

El sistema debe funcionar principalmente mediante una única entrada inteligente.

**NOVA trabaja para preparar el resultado; el usuario conserva control sobre envíos externos y acciones sensibles.**

No reconstruyas el sistema. Audita primero la arquitectura React, API ASP.NET Core, SQLite, cliente Ollama y extensión MV3 existentes.

## 1. PRINCIPIO FUNDAMENTAL

Implementar una experiencia basada en intención, no en formularios.

Entrada:

"Necesito un prompt para comparar dos documentos legales, encontrar diferencias importantes y generar un resumen sencillo."

Resultado esperado:

NOVA encuentra una plantilla compatible o genera un nuevo borrador completo, con instrucciones claras, estructura apropiada y variables solamente cuando sean necesarias.

Si los documentos no están disponibles, no debe inventarlos. Puede preparar una plantilla reutilizable o solicitar los documentos si son indispensables para ejecutarla.

El usuario no debe necesitar aprender ingeniería de prompts.

## 2. AUDITORÍA Y BASELINE

Antes de implementar:

- Revisar rama, cambios sin commit y código existente.
- Identificar contratos de Prompt, Variable, Group, InstructionProfile y CompiledPrompt.
- Auditar búsqueda actual, SQLite y migraciones.
- Revisar cliente Ollama y pruebas con Qwen3.
- Revisar el componente de búsqueda/NOVA en React.
- Identificar la implementación real y versión de Playwright.
- Identificar la extensión MV3 y sus proveedores.
- Ejecutar pruebas actuales, conservar evidencia y registrar fallos.
- Verificar los modelos instalados y recursos del equipo.

No asumir que una funcionalidad existe porque aparece en informes históricos.

No instalar dependencias ni descargar modelos sin autorización.

## 3. NOVA AGENTIC ORCHESTRATOR

Implementar una máquina de estados finita:

RECEIVED
→ UNDERSTANDING
→ SEARCHING
→ SELECTING_OR_GENERATING
→ FILLING
→ VALIDATING
→ READY
→ USER_ACTION
→ COMPLETED

Estados alternativos:

NEEDS_INFORMATION
CANCELLED
FAILED
BLOCKED

La ejecución debe contar con:

- Máximo configurable de pasos.
- Timeout por etapa.
- CancellationToken.
- IDs de operación.
- Retries limitados.
- Validación de contratos.
- Registro estructurado sin contenido privado.
- Aislamiento de operaciones concurrentes.

El modelo propone decisiones; el orquestador determina cuáles están permitidas.

No implementar loops autónomos ilimitados.

## 4. INTENT UNDERSTANDING

Crear un clasificador de intención con salida JSON Schema.

Intenciones iniciales:

- FIND_PROMPT
- CREATE_PROMPT
- ADAPT_PROMPT
- COMPLETE_PROMPT
- IMPROVE_PROMPT
- SAVE_PROMPT
- USE_PROMPT
- RUN_LOCAL
- ORGANIZE_PROMPT
- UNKNOWN

Debe interpretar expresiones naturales sin exigir comandos exactos.

Ejemplo:

"Hazme uno parecido al que uso para emails, pero orientado a solicitar una entrevista."

Detectar que corresponde a buscar una plantilla existente y adaptarla.

Ejemplo:

"Utiliza mi prompt de arquitectura para revisar una API."

Buscar plantilla y completar la información disponible.

Ante ambigüedad que impida un resultado útil, hacer una pregunta concreta.

No inventar datos, experiencias, documentos ni configuraciones ausentes.

## 5. BÚSQUEDA INTELIGENTE DE PROMPTS

Antes de generar un prompt nuevo, buscar entre los existentes.

Baseline:

- SQLite FTS5/BM25 cuando corresponda.
- Búsqueda por título.
- Contenido.
- Categoría.
- Grupos.
- Favoritos.
- Recientes.

NOVA debe utilizar resultados con IDs reales.

No generar nombres ficticios y presentarlos como elementos guardados.

El agente recibe un conjunto acotado de candidatos y puede seleccionar:

REUSE_EXISTING
ADAPT_EXISTING
CREATE_NEW
ASK_CLARIFICATION

La selección debe ser explicable y evaluable.

No introducir un vector database sin demostrar que mejora el baseline.

## 6. AUTOMATIC PROMPT GENERATION

Cuando no exista una plantilla apropiada, generar automáticamente un borrador de calidad.

El modelo deberá producir:

- Título sugerido.
- Descripción breve opcional.
- Texto completo.
- Definiciones de variables opcionales.
- Valores conocidos.
- Variables faltantes.
- Grupo o categoría sugeridos, si existen.
- Indicación de si conviene guardar como plantilla reutilizable.

No imponer una estructura rígida de prompt a todos los casos.

Para consultas simples, generar instrucciones simples.

Para tareas complejas, estructurar:

Objetivo
Contexto
Instrucciones
Restricciones
Formato esperado
Criterios de calidad

Solo incluir las secciones que aporten valor.

No crear prompts excesivamente largos por defecto.

Preservar el idioma solicitado y ofrecer resultados en español o inglés según intención.

## 7. AUTOMATIC VARIABLE RESOLUTION

Implementar un motor capaz de identificar y completar variables automáticamente.

Orden de resolución:

1. Valores proporcionados expresamente en la petición.
2. Información ya incluida en la plantilla seleccionada.
3. Contexto local autorizado para esa tarea.
4. Valores predeterminados válidos.
5. Propuestas inferidas por Qwen3, identificadas como inferencias y validadas.
6. Solicitud de aclaración si queda un dato obligatorio sin fuente confiable.

No extraer información personal desde fuentes no autorizadas.

No inventar documentos ni hechos para rellenar campos.

No guardar permanentemente valores sensibles sin consentimiento.

Para prompts que no requieren variables, no crear variables artificiales.

Separar:

- VariableDefinition.
- VariableValue.
- ValueSource.
- ValidationStatus.
- CompiledPrompt.

El compilador final debe ser determinista.

Verificar que no quedan placeholders obligatorios sin resolver antes de ejecutar o transferir.

## 8. REUTILIZACIÓN Y CREACIÓN AUTOMÁTICA

Cuando el usuario solicite un prompt:

- Buscar primero uno compatible.
- Reutilizar el existente cuando sea suficiente.
- Adaptarlo si necesita modificaciones.
- Crear uno nuevo cuando no exista alternativa adecuada.

El resultado debe aparecer inmediatamente como borrador preparado.

Permitir guardar automáticamente un borrador local recuperable sin modificar plantillas existentes.

El guardado definitivo en la biblioteca puede realizarse con una acción explícita o mediante una preferencia de autoguardado activada voluntariamente.

El usuario no debe completar formularios para guardar.

Ofrecer acciones sencillas:

Copiar
Guardar
Usar en...
Ajustar

Conservar historial de versiones y capacidad de deshacer.

Evitar duplicados mediante una detección de similitud explicable.

Nunca sobrescribir prompts existentes sin autorización.

## 9. TOOL REGISTRY

Crear herramientas tipadas, limitadas y registradas.

Herramientas de lectura:

search_prompts
get_prompt
get_prompt_variables
list_groups
get_instruction_profile
get_recent_prompts

Herramientas de preparación:

propose_prompt
propose_improvement
resolve_variables
compile_prompt
validate_prompt

Herramientas de dominio controladas:

save_draft
save_prompt
assign_group
record_usage

Herramientas de ejecución:

run_local_llm
prepare_provider_transfer

Cada herramienta requiere:

- JSON Schema.
- Validación de argumentos.
- Permisos definidos.
- Timeout.
- Errores tipados.
- Resultado estructurado.
- Tests de contrato.

No otorgar acceso arbitrario a filesystem, shell, SQL, HTTP externo ni navegador.

El agente no recibe una herramienta de ejecución genérica.

## 10. QWEN3 LOCAL

Utilizar el modelo local instalado mediante Ollama.

Modelo principal: qwen3:8b si está disponible.

No descargar modelos automáticamente.

Utilizar:

- Tool calling para seleccionar operaciones autorizadas.
- Structured outputs para los contratos.
- Temperatura y límites configurables.
- Validación estricta del JSON.
- Fallback en caso de formato inválido.
- Cancelación real.
- Timeouts y observabilidad.

Separar las funciones:

IntentInterpreter
PromptRetriever
PromptGenerator
VariableResolver
PromptEvaluator
AgentOrchestrator

No convertir cada función en un microservicio.

Implementar interfaces pequeñas y adaptadores reutilizables siguiendo SOLID.

No confiar únicamente en la puntuación de calidad que el modelo se asigne a sí mismo.

## 11. PLAYWRIGHT AUTOMATION

Integrar Playwright como una herramienta especializada de automatización de navegador.

### Uso interno

Utilizar Playwright principalmente para validar automáticamente la experiencia real de QuickPrompt:

- Introducir intención.
- Observar resultados.
- Seleccionar prompt.
- Comprobar variables resueltas.
- Copiar.
- Guardar.
- Ejecutar con Qwen.
- Verificar persistencia.

No utilizar Playwright para completar formularios internos mediante clics cuando exista una API de dominio confiable.

### Browser Automation Worker

Crear un componente separado, activable solamente para escenarios autorizados.

Funciones permitidas:

open_allowed_page
inspect_allowed_form
fill_allowed_field
verify_field_value
prepare_submission

No proporcionar al LLM acceso directo a page.evaluate, JavaScript arbitrario, CDP sin restricciones o navegación libre.

Toda navegación debe respetar una allowlist de destinos y bloquear orígenes no autorizados.

Validar campos por controles permitidos, identidad del sitio y estructura accesible.

No utilizar selectores generados sin comprobación.

Usar contextos aislados y perfiles de prueba.

No reutilizar ni exportar cookies personales automáticamente.

No ejecutar acciones externas de publicación, pago, borrado o envío sin confirmación explícita.

### Proveedores externos

Conservar la extensión MV3 para operar sobre pestañas autorizadas del usuario, cuando los permisos y condiciones del proveedor lo permitan.

No asumir que Playwright ofrece una vía universal para escribir o enviar en ChatGPT, Claude u otros.

Para Claude.ai, mantener integración automatizada bloqueada salvo autorización válida.

Para servicios no compatibles, usar clipboard + apertura explícita.

No intentar sortear 403, CAPTCHA, controles anti-bot ni restricciones de autenticación.

## 12. EXPERIENCIA REACT — JARVIS SIN COMPLEJIDAD

Mantener el concepto Living Canvas.

No crear una pantalla de agentes.

NOVA funciona desde la entrada principal existente.

Ejemplo:

Usuario:
"Necesito uno para mejorar este correo."

NOVA:
Busca una plantilla compatible y genera una versión preparada con el contexto disponible.

Mostrar una respuesta compacta:

"Preparé el prompt. Puedes usarlo ahora."

Debajo:

- Prompt listo.
- Campos pendientes, únicamente si son necesarios.
- Copiar.
- Guardar.
- Usar en...

Mostrar estados visuales discretos:

Searching
Preparing
Validating
Ready
Needs information
Error

Permitir detener la generación.

No presentar al usuario trazas, tool calls, JSON o detalles técnicos.

Los diagnósticos deben estar disponibles en configuración avanzada.

No crear nuevos dashboards ni paneles permanentes.

## 13. PROVEEDORES

Implementar una interfaz común:

IProviderAdapter

Capacidades:

- IsAvailable.
- SupportsApiExecution.
- SupportsBrowserPreparation.
- SupportsBrowserSubmission.
- RequiresExplicitApproval.
- Prepare.
- ExecuteWhenAuthorized.
- GetStatus.

Adaptadores iniciales:

Ollama
ChatGPT
Claude
Gemini
Copilot
OtherConfiguredProvider

Cada adaptador declara únicamente capacidades verificadas.

No inventar compatibilidad con proveedores.

Las API externas son opcionales y requieren autorización, configuración de credenciales y consentimiento para compartir datos.

NOVA no decide unilateralmente enviar información privada a Internet.

## 14. DOMINIO Y PERSISTENCIA

Mantener SQLite como fuente de verdad.

El agente no escribe directamente en tablas.

Usar los servicios de dominio existentes.

Guardar:

- Borradores.
- Prompts.
- Variables.
- Relaciones con grupos.
- Versiones.
- Fechas de uso.
- Preferencias autorizadas.

Implementar idempotencia en creación y guardado.

Evitar crear múltiples plantillas si el usuario repite una operación accidentalmente.

Conservar respaldo, restauración y migraciones compatibles.

No leer bases de datos privadas antiguas sin autorización.

## 15. SEGURIDAD Y PRIVACIDAD

Mantener NOVA local por defecto.

- Ollama en loopback.
- API en loopback.
- Sin cloud implícita.
- Sin descargas automáticas.
- Sin telemetría externa.
- Sin secretos en navegador o logs.
- Sin prompts completos en logs técnicos.
- Sin ejecución arbitraria.
- Sin permisos de navegador excesivos.
- Sin acceso a archivos no autorizados.
- Sin automatización silenciosa de servicios externos.

Tratar contenido de prompts, documentos y páginas externas como datos no confiables.

Implementar defensas ante prompt injection.

La salida del modelo nunca concede permisos.

Las operaciones de riesgo requieren autorización y verificación independientes.

## 16. PRUEBAS Y EVALUACIONES

Crear un dataset versionado de peticiones reales y sintéticas.

Mínimo inicial recomendado: 60 escenarios, separados en desarrollo, validación y holdout.

Cubrir:

- Encontrar prompt existente.
- Adaptar uno existente.
- Crear desde cero.
- Completar variables conocidas.
- Detectar datos faltantes.
- Prompts sin variables.
- Duplicados.
- Peticiones ambiguas.
- Búsqueda sin resultados.
- Generación de contenido multilingüe.
- Intentos de prompt injection.
- Solicitudes de modificación sin autorización.
- Ejecución Qwen real.
- Fallos de Ollama.
- Fallos de Playwright.
- Fallos de proveedor.
- Reinicio y persistencia.

Medir:

Intent accuracy
Retrieval Recall@K
Tool-call accuracy
Valid JSON rate
Variable resolution accuracy
Hallucinated value rate
Duplicate creation rate
Successful end-to-end task rate
p50/p95 latency
RAM/VRAM
Cancellation reliability

Definir thresholds de aceptación antes de optimizar.

No modificar el holdout para obtener PASS.

Complementar evaluaciones automáticas con revisión humana.

## 17. CASOS E2E OBLIGATORIOS

A. "Busca mi prompt de arquitectura."

Debe devolver únicamente prompts realmente existentes.

B. "Crea un prompt para resumir documentos."

Debe generar un borrador preparado sin exigir un formulario manual.

C. "Quiero reutilizar mi prompt de entrevistas para una posición diferente."

Debe encontrarlo y adaptar una copia, sin sobrescribir el original.

D. "Haz uno para analizar este texto."

Debe completar lo disponible y detectar si falta el texto.

E. "Usa este prompt con Qwen."

Debe compilarlo y ejecutarlo localmente.

F. "Déjalo listo para ChatGPT."

Debe preparar el texto y realizar la transferencia permitida o fallback correcto.

G. Solicitud ambigua.

Debe realizar una única pregunta útil cuando sea indispensable.

H. Ollama apagado.

La biblioteca y el compilador deben continuar operativos.

I. Error de herramienta.

Debe informar el fallo y conservar el trabajo.

J. Repetición de operación.

No debe duplicar borradores ni envíos.

## 18. CRITERIOS DE ACEPTACIÓN

- El usuario puede solicitar un prompt nuevo sin escribir manualmente una plantilla.
- NOVA busca primero en la biblioteca.
- Puede adaptar prompts existentes sin sobrescribirlos.
- Completa automáticamente las variables con datos disponibles y autorizados.
- No inventa valores faltantes.
- El usuario obtiene un prompt listo para copiar, guardar o utilizar.
- La biblioteca funciona independientemente de Qwen.
- Los borradores sobreviven a fallos y reinicios.
- Qwen ejecuta correctamente el flujo local en navegador real.
- Playwright funciona en los destinos autorizados y no obtiene privilegios arbitrarios.
- Los proveedores externos reportan estado verdadero.
- La experiencia React continúa siendo simple.
- Todas las regresiones críticas permanecen aprobadas.
- Existe evidencia reproducible de los escenarios y métricas.

## 19. EJECUCIÓN POR FASES

FASE A: Auditoría y baseline.

FASE B: Interpretación de intención y búsqueda.

FASE C: Generación automática y resolución de variables.

FASE D: Guardado de borradores, adaptación y compilación.

FASE E: Ejecución con Qwen real.

FASE F: Playwright Browser Worker y pruebas de permisos.

FASE G: Integración con los proveedores permitidos.

FASE H: Pruebas E2E, seguridad, calidad y UX.

Implementar y verificar por incrementos.

No comenzar con automatización de sitios externos antes de demostrar que NOVA genera y completa correctamente prompts mediante los servicios internos.

## 20. ENTREGA

Producir:

- Arquitectura actualizada.
- Contratos JSON/OpenAPI.
- Código del orquestador.
- Herramientas tipadas.
- Integración Ollama.
- Generación automática.
- Resolución de variables.
- Playwright Worker.
- Pruebas unitarias.
- Pruebas de integración.
- E2E real.
- Evaluaciones IA.
- Evidencia visual.
- Matriz de aceptación.
- Problemas y bloqueos.

Reportar PASS, FAIL, BLOCKED y NOT_TESTED con evidencia.

No declarar producción comercial.

No instalar, publicar, descargar modelos ni modificar datos privados sin autorización.

No realizar commits ni push sin autorización.

## ORDEN INMEDIATO

Comienza auditando la aplicación actual.

Después implementa el siguiente flujo antes de cualquier otra expansión:

Usuario escribe una intención → NOVA interpreta → busca plantillas → selecciona/adapta o crea → resuelve variables disponibles → valida → muestra el prompt final listo.

Demuestra ese flujo con Qwen3 real y SQLite, desde la interfaz React normal.

Una vez aprobado, integra Playwright en sus casos justificados y las conexiones externas permitidas.

**El objetivo es que QuickPrompt trabaje para el usuario, no que el usuario tenga que aprender a trabajar con QuickPrompt.**