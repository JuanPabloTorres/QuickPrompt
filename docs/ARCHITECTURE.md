# Arquitectura vigente — React / API modular

Actualización 2026-10-20: la aplicación actual usa React → ApiClient/HttpTransport → endpoints → servicios de aplicación → infraestructura. ApplicationModules compone biblioteca, agente y ejecución visible. PromptLibrary mantiene CRUD detrás de IPromptReader/IPromptWriter. Los diez servicios de persistencia/modelo compartidos viven en backend/QuickPrompt.Infrastructure/Local; el proyecto Blazor los consume mediante referencia de proyecto. Se retiró la inclusión de archivos desde el frontend anterior en el backend. No cambia el esquema de datos.

El catálogo de herramientas se genera desde métodos y tipos C# autorizados. El reemplazo del núcleo semántico está aislado en QuickPrompt.Evaluation/Experiments porque todavía no acredita fidelidad; la composición productiva conserva el intérprete anterior. Esto es deuda explícita, no un cierre del agente.

QuickPromptHost posee el arranque, middleware y endpoints; Program delega su CLI.
Un callback de servicios de código confiable permite sustituir módulos antes de
Build sin que producción dependa de Evaluation. Quince controles HTTP/UI prueban
CRUD con inferencia ausente y sustitución de un intérprete sintético por el endpoint
real; no son precisión LLM. startVisible distingue inicio rechazado con recibo
ausente de respuesta perdida, evitando publicar ejecuciones fantasma sin duplicar
POST. Contrato y límites: [HOST_COMPOSITION_20261020.md](nova/HOST_COMPOSITION_20261020.md).

[Mapa de propietarios, dependencias y retiradas pendientes](nova/MODULAR_SYSTEM_MAP.md). [Candidato del núcleo y fallos conservados](nova/AGENT_CORE_REPLACEMENT_20261020.md).

La sección siguiente documenta etapas anteriores; no describe por sí sola la composición vigente.

---

# Arquitectura web local

`src/QuickPrompt.Web.sln` es independiente de MAUI. Blazor Web App InteractiveServer → servicios C# → SQLite y adaptador HTTP Ollama. No frontend duplicado, escritorio, MAUI, Electron, Firestore, anuncios ni telemetría.

El csproj MAUI raíz excluye src/** de sus globs: MSBuild incluía 20 archivos nuevos antes y 0 después. Es el único ajuste de implementación al legado; evita que un futuro build MAUI compile accidentalmente el servidor web. No cambia sus targets ni dependencias.

`QuickPrompt.Domain` enlaza sin alterar las entidades/enums puros originales de Domain/. Reutiliza identidad Guid, reglas de creación, categorías, favoritos y soft delete. Añade tokenizador determinista, modelos tipados de versión, compilado, ejecución y propuesta. No referencia el csproj MAUI ni Core acoplado a UI.

`QuickPrompt.Web/Components` es presentación web; Home.razor + code-behind orquestan acciones explícitas. `Infrastructure/LocalStore` valida y persiste con transacciones y concurrencia optimista por revisión; `ILocalLlmClient` abstrae Ollama; `LegacyImporter` transforma archivos seleccionados en un archivo de intercambio validado. Se evita añadir proyectos para cada carpeta hasta justificar su separación.

Persistencia: SQLite fuera del repo, `%LOCALAPPDATA%/QuickPromptWeb/quickprompt.sqlite`. SchemaVersion y Documents(kind,id,payload JSON) con clave compuesta y índice de título. Migración aditiva v1→v2 transaccional, con backup previo de v1; rechazo de versiones futuras. Cada tipo tiene DTO propio, sin columnas dinámicas construidas desde entrada. Exportación v2 incluye plantillas, versiones, compilados, ejecuciones, propuestas y preferencias. Restore valida primero y reemplaza todo atómicamente. Import agrega y omite identidades/contenidos existentes; no sobreescribe. Borrar plantilla conserva los textos finalizados independientes.

La búsqueda es léxica en SQLite, normalizada por acentos/mayúsculas y por palabras en cualquier orden, sobre título, descripción, contenido, categoría enum y nombres de grupo. Páginas de12 y filtros de favorito/reciente/archivo/grupo. No se carga todo el catálogo para buscar. No FTS5, vectores ni Nomic: se requiere benchmark antes de añadir complejidad.

Entidad plantilla ≠ snapshot de versión ≠ texto compilado ≠ ejecución IA ≠ propuesta. Compilar guarda texto final y contador de uso en la misma transacción. Valores previos solo se guardan opt-in, aunque el texto final contiene inevitablemente los valores usados. Respuesta IA queda en memoria hasta Guardar resultado; cancelar o recibir salida parcial impide guardar. Aplicar mejora verifica revisión, texto y variables y guarda versión nueva y estado de propuesta atómicamente.

Ollama recibe un system fijo y datos del usuario como role=user. Modelos permitidos: qwen3:8b y qwen2.5:7b-instruct, sin cambio silencioso de modelo. Qwen2.5 se selecciona explícitamente. No herramientas ni HTML/SQL provenientes del modelo. JSON estructurado y validación C# posterior. Ejecución streaming NDJSON, timeout 180s, num_ctx 8192, num_predict 1800, cancelar/reintentar. Respuestas inválidas se rechazan sin aplicar cambios.

Evolución: SDK net10 primero con pruebas completas; después FTS si las mediciones lo justifican. Multiusuario exige diseño de autenticación, autorización de datos, HTTPS y sesiones; no basta cambiar el bind. No hay runtime dependency en LocalAI-Audit-Lab.

## GOAL06

[Auditoría y decisiones actuales](GOAL06_AUDIT.md). `TemplateEditor` posee historial y operaciones sobre marcadores; nunca valores de ejecución. `TemplatePackage` es una proyección mínima para compartir y se distingue de Archivev2 privado. `PreviewImport` reutiliza la validación de importación y no escribe. `CompiledPrompt.Values` es una copia de solo lectura.

Las solicitudes libres de Quick Assist pasan por `ILocalLlmClient.PlanAssist`, que devuelve operaciones tipadas de la lista cerrada `AssistAction`. Se retiró el reconocimiento por palabras/prefijos y la apertura automática del primer resultado. `PrepareAssist` liga identidades/revisiones y `ApplyAssist` valida y aplica cambios persistentes en una transacción tras aprobación. La propuesta generativa requiere su revisión independiente habitual. [Límites actuales de interpretación y validación](nova/SEMANTIC_BOUNDARIES.md).

El panel MV3 consulta métodos de `PanelSession` a través de la pestaña local y content script del origen exacto. No hay API HTTP nueva, CORS permisivo ni iframe. `PanelSession.Compile` usa LocalStore con revisión esperada; el panel conserva el snapshot hasta cambiar campos y descarta resultados tardíos. Solo storage de sesión para el ID de pestaña; no catálogo persistido en extensión.

`automation/playwright` es un intérprete opcional separado. No participa en guardar/copiar ni se ofrece como ejecución libre al modelo. [Uso, límites y pruebas](../automation/playwright/README.md).


## NOVA: planificación por capacidades (2026-10-19)

La API React/NOVA usa ITaskPlanner/NovaTaskPlanner. TaskConversationContext restaura y guarda la conversación en AgentSessions; TaskSourceResolver posee lectura, coincidencias y revisión del origen; el intérprete local propone significado y TaskClarification suspende efectos. ISourceReferenceResolver permite cargar el origen antes de interpretar sus campos/usos; su vínculo efímero conserva petición e identidad/revisión sin conceder selección UI ni aprobación. Se retira SemanticPlanning y se migran sus consumidores, manteniendo HTTP/JSON y SQLite. El catálogo por Reflection y las herramientas conservan sus propietarios.

[Contrato, Mermaid, pseudocódigo, pruebas y fallos reales](nova/SEMANTIC_SCOPE_RELIABILITY.md). La modularización y los tests de componentes no cierran por sí solos la fiabilidad del lenguaje libre.


## Contratos local/browser y panel — incremento 2026-10-20

ILocalLlmClient y sus tipos de interpretación/error, e IWebPromptTransfer, viven en
Application. Sus namespaces anteriores se conservan por compatibilidad y los
ensamblados anteriores usan TypeForwardedTo; no hay dependencia de Application al
proveedor. PromptPanel posee proyección/validación del panel; PanelSession sólo adapta
JSInvokable. Home y componentes usan los puertos de LocalPromptLibrary y no reciben
LocalStore. [Contrato y evidencia](nova/RUNTIME_PORTS_20261020.md).

## Composición y metadatos — incremento 2026-10-20

QuickPromptHost posee el arranque de la API; Program delega y un hook confiable de
servicios permite probar módulos ausentes/sustituidos sin copiar el servidor.
NovaInference recibe el intérprete semántico compartido y el cliente local mediante
sus puertos, conserva la proyección anterior y no posee recursos de inferencia.
[Composición](nova/HOST_COMPOSITION_20261020.md),
[adaptador y migración C#](nova/NOVA_INFERENCE_PORT_20261020.md).

QuickPrompt.ContractExport referencia Infrastructure y sus contratos, nunca Api o
Evaluation. Exporta JSON público de LocalModelPolicy y enums usando sus codecs reales;
el generador Node valida ese JSON y sustituye las listas correspondientes. El resto
del OpenAPI conserva su propietario actual. El documento publicado permanece igual.
[Generación, límites y pruebas](nova/CONTRACT_METADATA_SOURCE_20261020.md).

La ejecución visible compone UiDecisionModel sobre IAgentModelTransport, sin
construir el intérprete ni el editor de contenido. El selector conserva reglas y
reselección existentes; ejecutores/verificador conservan efectos y aprobación.
PreparationInference.Decide es un puente compatible hacia ese único propietario.
LocalModelPolicy posee el perfil de contexto de agente y el alias antiguo conserva
su valor. [Aislamiento y evidencia](nova/UI_DECISION_MODULE_20261020.md).

ICandidateSelector también se compone sobre transporte, mediante SemanticCandidateSelector.
La resolución de identidad/revisión/ambigüedad sigue en TaskSourceResolver; el selector
sólo devuelve pertenencia validada de candidatos reales. El constructor compatible
del orquestador deja de elegir por puntuación: usa capacidad explícita/publicada o
falla con CAPABILITY_UNAVAILABLE cuando se requiere. No se promueve otra interpretación
semántica por esta extracción. [Contrato y migración](nova/CANDIDATE_SELECTION_MODULE_20261020.md).

IInputValueExtractor tiene un proveedor independiente, PromptInputValueExtractor,
sobre transporte compartido. La fábrica lo inyecta en el orquestador y en el puente
compatible de autoría/alcance. El orquestador no requiere que el generador publique
esa capacidad; la comprobación de evidencia literal y permisos continúa en dominio.
[Contrato, compatibilidad y evidencia](nova/INPUT_EXTRACTION_MODULE_20261020.md).

IInputScopeInterpreter se compone como PromptInputScopeInterpreter sobre extracción
prestada. Planificador y resolvedor reciben el puerto explícito; SemanticContext
posee las proyecciones puras de fuente/campos. El parcial anterior se retira y su
método público conserva delegación compatible. Identidad/revisión y aprobación
siguen con sus dueños. [Contrato](nova/INPUT_SCOPE_MODULE_20261020.md).

Alcance vigente de la meta: versión web. La validación MAUI histórica queda fuera,
sin declarar PASS ni instalar herramientas. [Decisión](nova/WEB_SCOPE_DECISION_20261020.md).

La generación consume IDraftFidelityReviewer: DraftFidelityReviewer posee la revisión
semántica anterior y su corrección acotada, con transporte validado prestado y
diagnóstico por llamada. AgentStagePacket comparte serialización/metadatos de etapa
con el intérprete. El parcial anterior se retira; esta separación no acredita
fiabilidad de lenguaje ni aprueba efectos. [Contrato y límites](nova/FIDELITY_REVIEW_MODULE_20261020.md).

Generación se compone como IPromptGenerator/PromptDraftGenerator sobre transporte,
editor, extractor y revisor prestados, sin depender de construir interpretación.
PreparationInference delega generación y schemas compatibles; parcial Authoring
retirado. Diagnóstico opcional por llamada, sin estado de sesión en el singleton.
[Contrato y evidencia](nova/DRAFT_GENERATION_MODULE_20261020.md). La separación
conserva las limitaciones semánticas conocidas; no promueve otro núcleo.

Las interfaces semánticas productivas comparten SemanticIntentInterpreter sobre
transporte validado y selección prestada. No construyen generación, editor ni
campos. PreparationInference es una fachada no parcial con delegaciones/schema
compatibles. Seis parciales anteriores retirados; algoritmos e instrucciones
conservados. [Contrato, migración y evidencia](nova/SEMANTIC_INTERPRETER_MODULE_20261020.md).
La separación no resuelve por sí misma los fallos semánticos conservados.

API y Blazor construyen el adaptador ILocalLlmClient mediante
OllamaClient.CreateOwned. IDisposable libera sólo el HTTP creado por esa fábrica;
el constructor existente mantiene el HTTP externo prestado. El contenedor captura
el recurso del factory y lo dispone al terminar. Inferencia e instrucciones
sin cambios. [Contrato y reproducción](nova/LOCAL_CLIENT_LIFECYCLE_20261020.md).

La entrada React crea ApplicationServices sobre un ApiClient y lo entrega mediante
ServiceContext a App, hooks y pantallas; no mueve estado de dominio al contexto.
VisibleSessionClient mantiene las capacidades de cada instancia y delega a
LiveClient prestado, conservando recuperación sin repetir POST. Los exports
anteriores son adaptadores compatibles de la misma implementación.
[Contrato y evidencia](nova/FRONTEND_COMPOSITION_20261020.md).

## Política compartida de modelos — 2026-10-20

LocalModelPolicy pertenece a Domain; Application/API, telemetría, defaults y
exportador consultan al mismo propietario inmutable. Namespace público conservado
y tipos reenviados desde Infrastructure; exportador sin dependencia de proveedor.
Sin otro registro, modelo instalado, configuración ni permiso nuevo.
[Contrato y evidencia](nova/MODEL_POLICY_OWNERSHIP_20261020.md).

550 backend y 112 compartidas PASS, cero omitidas; 17 casos nuevos. Cuatro controles
HTTP reales de admisión y siete del exportador PASS, contrato público idéntico.
API/Blazor sin advertencias/errores, DLL compartidas iguales a ambos proyectos
probados. Consumidor binario antiguo funciona sin recompilar; su fixture conserva
dos advertencias MSB3277. Veinticinco controles estáticos PASS. Fallo de expectativa
antigua del exportador conservado y migrado al nuevo límite.

No acepta comprensión Qwen ni latencia; matriz canónica 34/54 intacta, versión
abierta y meta ACTIVA. MAUI excluido; servicios personales sin reiniciar.

## Capacidades semánticas conectadas por puerto — 2026-10-20

La fábrica registra explicación, referencia, revisión de cláusulas y selección
contextual como alias del intérprete existente. AgentOrchestrator recibe explicación;
NovaTaskPlanner transmite los otros puertos al resolvedor. Se elimina exigir esas
capacidades al generador o proveedor UI/selector básico. Compatibilidad al construir;
sin nuevos propietarios, algoritmos, schema o permisos.
[Contrato y evidencia](nova/SEMANTIC_CAPABILITY_WIRING_20261020.md).

Fallo reproducido: cuatro alias ausentes; mismo binario de 17 tests falla con cores
anteriores y pasa con los corregidos. Tanda final 388 backend/19 clases PASS, cero
omitidas y 19 casos nuevos, incluyendo propietario real/transporte controlado.
Quince controles Edge/HTTP de host normal/desactivado/sustituido PASS; perfil QA
migrado y error visible revisado. API/Blazor/evaluador sin advertencias ni errores;
DLL compartidas idénticas a las probadas. Veintiséis controles estáticos PASS.

Pruebas de conexión y efectos con respuestas controladas: no cualifican Qwen ni
latencia. Fallos originales y compilación inicial del fixture conservados. Matriz
canónica 34/54 intacta; versión abierta y meta ACTIVA, MAUI excluido.

## Fachada histórica retirada del runtime — 2026-10-20

PreparationInference ya no se compila en Infrastructure ni se registra en API. Sus
consumidores restantes son QA; archivo idéntico trasladado a Evaluation/Compatibility.
AgentStagePacket es un codec público puro compartido, sin duplicación ni friend
assemblies. Hashes QA apuntan a los propietarios reales y separan compatibilidad.
Cambio intencional del API .NET antiguo; puertos productivos y HTTP conservados.
[Contrato, migración y evidencia](nova/LEGACY_INFERENCE_RETIREMENT_20261020.md).

457 backend en 46 clases PASS, cero omitidas; dos pruebas de retirada fallan antes
y pasan después. Quince controles Edge/HTTP de host normal/desactivado/sustituido
PASS. API/Blazor/evaluador sin advertencias/errores y DLL comunes iguales a las
probadas; artefactos productivos sin DLL/dependencia Evaluation. Cuatro contratos
exportados y OpenAPI idénticos; siete controles de metadata y 27 estáticos PASS.
Fallo inicial y guarda de inyección antigua conservados.

No cambia el algoritmo semántico ni cualifica Qwen/latencia. Versión y meta siguen
abiertas; matriz 34/54 intacta. MAUI excluido, servicios personales sin reiniciar.

Las opciones técnicas singleton de NOVA se capturan en NovaAgentSettings al registrar
el módulo; una nueva configuración requiere nueva composición. Preferencias de
usuario siguen en sus stores. [Contrato](nova/RUNTIME_SETTINGS_SNAPSHOT_20261020.md).

App compone una sesión de propuestas UI propia. AgentUiDriver ejecuta DOM y no
posee un registro global de escritor; SameTabAgent adquiere/libera su lease y
EditorPreparation consume el puerto de su contexto. [Contrato](nova/UI_PROPOSAL_SESSION_20261020.md).

AgentUiDriver compone un registro tipado de comandos sobre su puerto de primitivas
DOM. Los IDs son de protocolo, no de frases; descriptores inmutables, estado por
driver y guardas/finalización comunes. [Contrato](nova/UI_COMMAND_REGISTRY_20261020.md).
