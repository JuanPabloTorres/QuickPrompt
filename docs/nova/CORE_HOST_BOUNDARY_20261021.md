# Separación Core / anfitrión — contrato de cambio y ADR

Estado: implementación autorizada; aceptación funcional pendiente. Complementa el
incidente ORDERED_AGENT_CLOSURE_20261020, no sustituye sus gates.

Último candidato con evaluación ciega y navegador: **intent-owned-provider**. Backend 1080/1080,
compartidas 168/168; desarrollo Qwen 13/16 y navegador con oráculo corregido 8/9,
ambos conjuntos FAIL. La versión sigue abierta. Los nombres intent-final de las
secciones históricas corresponden a un candidato anterior a la corrección de proveedor.

## Incremento: auditoría de intención y resultado

Contrato de cambio: extender los propietarios existentes TaskGoal y el compilador
del dominio. Capturar TaskIntentContract antes del plan, con petición vinculada,
efectos, prohibiciones, identidad conocida, resultados y aprobaciones inmutables.
El plan no puede reemplazar esos criterios. Validar compatibilidad antes de efectos
y recibos al terminar. Sin cambios de API pública, DB, Core, transporte o modelo.

Alternativas: (A) añadir verificaciones al objetivo derivado del plan conserva la
circularidad; descartado. (B) contrato inmutable en Application, consumido por los
validadores existentes; elegido. (C) otro juez/planner LLM introduce otra inferencia
no calibrada; descartado (el ensayo anterior sólo acertó 5/8). No nuevos servicios.

Invariantes y extensión: no routing por palabras, no permisos nuevos ni retries;
las dependencias se calculan con el compilador existente. La interpretación inicial
sigue siendo falible: este contrato protege de divergencias posteriores, no acredita
por sí mismo la semántica original. Una aclaración conserva el encargo suspendido,
no lo completa. Cancelar/pausar/reanudar son controles de sesión, no continuación
del contenido de un encargo anterior. No autorizan artefactos ni escrituras.

Verificación prevista: plan de lectura frente a copia requerida; copia no solicitada;
condiciones borradas en una copia del goal; recibo de clipboard ausente; identidad
contradictoria; suspensión; control con metadatos de continuación erróneos;
regresión backend y desarrollo Qwen con los mismos oráculos congelados.
Despliegue: candidato aislado; conservar revisión 5196 y fallos previos. Rollback:
retirar exclusivamente este incremento, nunca restaurar el motor legacy.

Contraste semántico: intent-v1 conserva el contrato previo de operaciones como
control congelado. Su captura sintética confirma que los efectos adicionales de
02/03/11 vienen en la propuesta anterior a Compile; no los agrega el transporte.
En 13 la propuesta de adaptar sin antecedente es suspendida, pero la rama que
creaba TaskGoal consultaba el ASK resultante y perdía la autoridad de contenido:
reproducción determinista roja `intent-authoring-suspension-red.trx`.
En 15 un control de sesión recibía metadatos de continuación de contenido; se
normaliza exclusivamente ese metadato irrelevante, sin ampliar autoridad.

Intent-v1 detectó además una regresión propia en 12: el compilador existente
normaliza búsqueda con consulta de NONE a NAMED, pero el contrato congelaba NONE.
La corrección conserva la referencia normalizada por el mismo propietario, con
prueba de regresión de búsqueda y suspensión. No se cambia el oráculo.

Hipótesis contrastable intent-v2: representar el encargo como resultados de usuario
del vocabulario SemanticDecision existente, en vez de operaciones internas, reduce
efectos no pedidos. Array conserva múltiples resultados independientes; mapeo total
uno a uno probado contra el catálogo del compilador. Cambia sólo el contrato interno
QuickPrompt a `quickprompt-work-order/2` (outcomes/forbiddenOutcomes); el protocolo
genérico `adaptive-inference/1`, proveedores, SDK, permisos y endpoints siguen iguales.
No se incorpora otro intérprete ni un juez que apruebe la propuesta original.

Decisión del contraste: rechazado intent-v2 por regresiones en búsquedas 01/02,
incluida copia no solicitada. Se restauraron exactamente los cuatro archivos
guardados antes del experimento y se conservó la corrección independiente de
suspensión de autoría. La fuente final conserva `quickprompt-work-order/1`.
Los artefactos de v2 y sus resultados no representan el candidato final.

## Contrato de cambio

- Resultado: Core genérico reutilizable; QuickPrompt propietario del dominio.
- Línea base anterior a boundary-handoff (ya sustituida): nova_worker.py contiene operaciones, fuentes, reglas de QuickPrompt
  y normalizadores; el intérprete C# requiere exactamente una llamada. El
  transporte .NET y UiDecisionModel también implementan protocolo/reparación LLM.
- Propietarios: Python: proveedor, inferencia tipada, límites y reparación
  estructural. C#: metadatos, esquema de encargo, resolución de identidades,
  permisos, revisiones, generación/validación de contenido del dominio, ejecución
  y verificación de efectos. React/browser: estado observado y controles reales.
- Invariantes: sin acceso Python a DB anfitriona; salida del modelo no autoriza
  efectos; ningún éxito de inferencia implica finalización de tarea; sin fallback
  silencioso al motor antiguo ni sustitución de modelo; cancelación mata solamente
  el proceso hijo propio; errores no incluyen prompts ni configuración privada.
- Extensión: esquemas JSON y contexto enviados por el anfitrión. Core no importa
  clases, enumeraciones ni nombres de QuickPrompt. Se prueba otro consumidor.
- Dependencias: anfitrión -> protocolo -> Core -> Pydantic AI -> Ollama local.
  Core no llama repositorios ni controles del dominio desde esta operación.
- Estado: la aplicación mantiene sesión/artefactos/aprobaciones. Core mantiene
  únicamente el turno de inferencia; los recibos de negocio siguen en .NET.
- Opcionales: el resto del producto/manual sigue disponible si Core falla. La
  función Core requerida falla explícitamente, sin simular un resultado.
- Protocolo: `adaptive-inference/1`, JSON UTF-8 por stdin/stdout, una petición por
  proceso; requestId correlacionado, modo text/structured/tools, instrucciones,
  mensajes, esquema/catálogo, modelo, límites. Respuesta proposed/failed, output,
  uso real y error estable. No contiene estado COMPLETED de negocio.
- Límites: máximo dos solicitudes por inferencia; timeout y salida limitados;
  errores de esquema pueden repararse una vez, errores de dominio requieren
  decisión explícita del anfitrión. Sin reiterar escrituras ni aprobarlas.
- Compatibilidad: el puerto interno IAgentModelTransport adapta consumidores
  existentes; no cambia endpoints públicos, esquema DB o flujo de aprobación.
  Contrato de planificación antiguo retirado del worker canónico. La revisión
  local congelada permanece separada hasta validación del candidato.
- Retirada: reemplazar el cuerpo específico de nova_worker; mover su contrato
  y binding a C#; usar Core para los transportes del agente en modo adaptive-core;
  retirar del selector UI el retry de protocolo delegado a Core.
- Verificación: contratos malformados, versión/ID incorrectos, límites,
  cancelación, selección fuera de catálogo, aislamiento sin Core, sustitución de
  esquema con segundo dominio, tests backend/SDK y Qwen/UI con fixture sintético.
- Rollback: artefactos históricos congelados conservados; no reactivar código
  retirado como fallback automático. No migración de datos ni cambio de 5196.

## Decisión

A. Mantener el worker con reglas QuickPrompt: rechazado; duplica conocimiento.
B. Extender los puertos existentes con contrato genérico y agente Pydantic AI:
seleccionado; conserva dueños del dominio y elimina el parser/retry propio de la
frontera migrada. Dependencia Python opcional explícita y fijada.
C. Nuevo servidor/orquestador de dominio: rechazado; duplica estado y agrega
despliegue innecesario.

La orquestación de casos de uso .NET no se mueve por su nombre: sus transacciones,
aprobaciones y verificaciones son reglas del anfitrión. La selección cognitiva
usa Core; Core recibe observaciones/esquemas sin convertirse en dueño de UI/DB.
Cambiar esta frontera no demuestra que Qwen interprete todas las peticiones.

## Aceptación

No se cierran requisitos por compilar ni por inferencia tipada. Mantener fallos
de desarrollo y exigir los criterios originales de semántica y efecto completo.

## Implementación observada

- Core: `adaptive_core/inference.py`, `host.py` y `ollama_framework.py` usan
  Pydantic AI con un protocolo genérico. Request/reply JSON Schema publicados en
  el SDK. La salida estructurada exige un objeto explícito. Sin acceso a DB/DOM
  del anfitrión ni otra sesión de negocio.
- QuickPrompt: `QuickPromptCoreContract` publica la semántica y enlaza evidencia
  literal/fuentes; `AdaptiveCoreInterpreter` compila al dominio; el transporte
  adapta el puerto interno a stdio correlacionado. El selector UI ya no repara
  formatos. Los demás servicios mantienen contratos y reglas del negocio.
- Eliminados: puente Python lector alternativo, su CLI/config/tests y el modo
  experimental registered-tools. El worker canónico quedó como lanzador de 14
  líneas. No quedan operaciones de QuickPrompt en el runtime Python genérico.
- Retirados de producción: los ocho parciales del intérprete nativo y el
  transporte/cache nativo C# se movieron a Evaluation/Compatibility/NativeAgent
  para controles históricos. La API no depende de Evaluation ni contiene esos
  tipos. La composición solo admite adaptive-core y no registra refinadores
  nativos. Sus antiguos switches de goals/resultados/cache salieron del registro.
- Contenido manual: `PromptContentInterpreter` reemplaza el uso residual del
  agregado nativo. No selecciona acciones ni identidades. Blazor y la API
  inyectan Core en el cliente de sus operaciones IA.
- Compatibilidad deliberada: endpoints y esquema DB intactos. El cliente local
  mantiene su API de bajo nivel para consumidores independientes y probes de
  metadatos; no constituye otro motor registrado en la ruta agéntica. No se
  migraron embeddings/RAG ni móvil como parte de esta frontera.

## Fallos de la migración conservados y corregidos

1. El esquema aceptaba evidencia null para efectos ordinarios aunque el binding
   la rechazaba. El esquema del dominio ahora exige ID de instrucción para todos
   los efectos excepto ASK_CLARIFICATION. El intento UI fallido permanece.
2. El nuevo codec omitía type=object en la unión de herramientas; StructuredDict
   de Pydantic AI lo rechazaba antes de inferir. Reproducción aislada: failed con
   usage.requests=0. Test C# rojo en boundary-tool-envelope-red.trx; encabezado
   corregido y test verde, más pruebas Python de unión de capacidades y rechazo
   temprano de esquemas implícitos. Este defecto fue del adaptador nuevo, no de
   Qwen. No se interpretó como una falta de capacidad del modelo.

## Resultados del candidato boundary-handoff

Evidencia: `docs/evidence/skill-audit-20261021/boundary-handoff-manifest.json`.
API/evaluador/worker/SDK y lock Python congelados; modelo qwen3:8b conservado.

| Comprobación ejecutada | Resultado |
| --- | --- |
| Backend | 1058/1058 PASS |
| SDK Python, incluyendo framework/transporte nativo | 42/42 PASS |
| Lanzador y protocolo entre procesos | 4/4 PASS |
| Compartidas/Blazor | 168/168 PASS |
| Límites de módulos y exclusión de Evaluation | PASS estático |
| Edge + API + SQLite + Qwen + clipboard | 8/9; conjunto FAIL |
| Desarrollo semántico completo | 11/16 estrictos; conjunto FAIL |
| Validation/holdout | NOT RUN: gate de desarrollo falló |

La prueba real pasó copia explícita con recibos nuevos, copia libre, lectura,
ejecución local, contrato de cliente/notas con el mismo Core, actividad visible
e integridad del otro registro. Preparar para copiar siguió fallando: el recorrido
terminó COMPLETED con adaptación no solicitada y sin el objetivo COPY_PROMPT
esperado. No se reescribe ese oráculo para aceptar el plan elegido.

Fallos de desarrollo conservados: 02 FORBIDDEN_OPERATION, 03 efecto adicional,
11 preparación/copia incorrecta, 13 MISSING_CONTENT_AUTHORITY, 15 validación de
control de sesión. Esta evaluación comprueba interpretación/resolución SQLite;
no demuestra ejecución de todos sus planes. La aceptación original continúa
abierta. En aquel candidato boundary-handoff, TaskGoal derivaba condiciones del
plan propuesto. El incremento de intención posterior se detalla al final; tampoco
constituye por sí solo una comprobación semántica independiente de la petición humana.

## Activación y siguiente incremento

La instancia 5196, PID 30760, conserva el candidato histórico congelado de
protocolo 10; no se reemplazó ni se leyó su base privada. Los servidores 5243 de
aceptación usaron bases sintéticas y fueron cerrados por el arnés. Para ejecutar
la fuente nueva deben usarse API/worker/SDK actuales y el Python de la virtualenv
con el extra inference: ver agents/nova-core/README.md. No hay fallback al motor
anterior si la configuración está incompleta; la biblioteca manual sigue operable.

El siguiente incremento queda localizado en la interpretación de encargos y
aceptación contra el objetivo original, no en otra sustitución de transportes.
No se añadió un segundo juez: su calibración previa falló 3/8 y permanece rechazada.
No se declara cerrada la versión ni probada fiabilidad universal del framework.

## Auditoría contrastada con código y ejecución: causas por caso

| Caso | Primera divergencia observada | Propietario y mecanismo | Estado de la intervención |
|---|---|---|---|
| 02 | Propuesta previa a Compile incluye búsqueda, adaptación, creación, rellenado y copia | Interpretación Core/Qwen propone efectos incompatibles; C# rechaza la combinación. No es un fallo de transporte | Semántica pendiente; no debilitar el rechazo |
| 03 | Propuesta añade COPY_TO_CLIPBOARD a una lectura sin cambios | Una cita literal de la frase no prueba que esa frase autorice copiar. El encargo inicial puede ser erróneo aunque su JSON sea válido | Semántica pendiente |
| 11 | Propuesta sustituye preparar copia por escribir clipboard | COPY_PROMPT y COPY_TO_CLIPBOARD tienen recibos distintos; inferencia escoge el objetivo equivocado | Semántica pendiente |
| 13 | Propuesta ADAPT sin origen se convierte correctamente en ASK, después falla la creación del goal | C# elegía cláusulas según el plan suspendido, mientras Create comprobaba los efectos diferidos. Perdía la autoridad de contenido | Corregido desde raíces del encargo; rojo conservado, regresión determinista verde |
| 15 | CANCEL trae continuesPendingTask=true, sin tarea de contenido pendiente | C# comprobaba la continuación antes de normalizar el control de sesión | Corregido: control no adquiere continuación, destino ni autoridad de contenido |

La captura nueva vive sólo en Evaluation y exige fixture syntheticOnly. Conserva
la propuesta JSON pública de cada caso, incluso si Bind/Compile la rechazan.
No registra razonamiento interno ni lee la DB de revisión. Cada callback queda
asociado explícitamente al runId/fila del caso; los oráculos no consultan la traza.

El contrato inmutable se fija desde SemanticMeaning antes del plan de ejecución.
Guarda hash de petición, raíces, cierre de dependencias del compilador existente,
prohibiciones, objetivos observables y aprobaciones. FromIntent reutiliza las
validaciones de cláusulas; la evaluación final consume los resultados congelados,
no una nueva proyección del plan. ValidateIntent se comprueba antes/después de
resolver fuentes; reemplazar el goal no desconecta el contrato del plan. Un recibo
de herramienta UI no permitida impide completar aunque los otros resultados estén
listos. Los controles de sesión no se acreditan mediante una simple foto de UI.

Límite explícito: el contrato protege contra cambios posteriores de planificación,
no demuestra que Qwen haya entendido bien la petición al proponerlo. Por tanto,
puede persistir el falso éxito cuando la interpretación inicial ya es equivocada.
No se afirma que la verificación semántica independiente esté implementada.

Pruebas del candidato final: 1074/1074 backend (16 casos nuevos del contrato),
168/168 compartidas, SDK 42/42 y worker 4/4; fitness estático 35 comprobaciones PASS.
Resultados semánticos y de navegador del candidato final se registran aparte abajo.

Experimento rechazado intent-v2: 4/16 estrictos con mismos datos/modelo/umbrales.
Se retira el cambio a resultados nominales; no se incorpora otro motor, juez,
framework, retry, modelo ni permiso. Se conservan artefactos y trazas del fallo.

### Evidencia del candidato intent-final

- Manifiesto: `docs/evidence/skill-audit-20261021/intent-final-manifest.json`.
  Fuente coincide con sus hashes; API y Evaluation contienen las mismas DLL de
  Application e Infrastructure. API no contiene Evaluation. 50 artefactos fijados.
- Backend: `.playwright/skill-audit-20261021/tests/intent-final-backend.trx`,
  1074 PASS, incluidos 16 casos nuevos del contrato.
- Compartidas: `intent-final-shared.trx`, 168 PASS. SDK 42 y worker 4 PASS.
  Sin modificaciones del SDK en este incremento. Fitness estático: 35 PASS en
  `docs/evidence/skill-audit-20261021/intent-final-modules.json`.
- Desarrollo Qwen: `docs/evidence/skill-audit-20261021/intent-final-development.json`,
  **13/16 estrictos, FAIL**, mismos hashes de dataset/umbrales y digest del modelo.
  Pasan 12/13/15; fallan 02/03/11. No hay acciones de negocio ejecutadas por este
  arnés: mide interpretación/resolución, no éxito integral de las 16 tareas.
- Latencia observada de planificación/resolución, todos los intentos de esta
  corrida: p50 23.354 s, p95 33.331 s. No es latencia final de navegador ni acredita
  un SLA. No se infieren dimensiones de calidad o primer token no observadas.
- Validation/holdout: NOT RUN porque desarrollo no supera el gate. No se alteraron
  oráculos, umbrales, casos, permisos o elección de proveedor/modelo para cerrarlo.
- Navegador: ejecución aislada en curso al publicar esta sección; resultado final
  se añade a continuación. No sustituye la revisión 5196 ni lee su base.

### Defecto del oráculo E2E detectado en la revisión de recibos

La primera corrida intent-final-ui/1792620458564 declaró 9/9 PASS. Es insuficiente:
su preparación (runId en runs[2] del artefacto) terminó COMPLETED con
PROPOSE_ADAPTATION y COPY_PROMPT, draftChanged=true y cambió el cuerpo de
`Explica <tema> sin inventar datos.` a una definición específica de energía hidráulica.
El arnés sólo exigía preparación/valores y ausencia de clipboard/ejecución; no
excluía esa modificación extra. Se conserva el PASS histórico como salida de aquel
oráculo, pero **se rechaza como aceptación de la petición original**.

Corrección del arnés, no del producto: clipboard-task-outcomes/2 exige que preparar
los valores de esta plantilla conserve su definición y no añada autoría, persistencia,
destinos o ejecución. Las expectativas anteriores se conservan; no se debilitan.
Cada recibo nuevo guarda taskEvidence con clase de petición, escenario, texto literal
del fixture y runId; el reporte guarda versión de oráculo y SHA del arnés. Se ejecuta
otra vez el mismo binario congelado en intent-final-ui-strict. Las dos corridas se
conservan; un resultado posterior bueno no borra este falso COMPLETED demostrado.

Intento adicional de captura HTTP de los recibos anteriores: no ejecutado porque
el servidor de QA ya estaba cerrado; se rechazó la identidad antes de leer datos.
El diagnóstico anterior se basa en los recibos persistidos del arnés y su fixture
sintético declarado, no en esa captura fallida ni en la base privada de 5196.

### Incidente adicional: identidad del proveedor local

La primera corrida con oráculo v2 terminó 7/9, incluidos un falso COMPLETED de
preparación y un fallo UNKNOWN_PROVIDER antes de obtener semantic/decisions en
ejecución local. El valor concreto devuelto por Qwen no se capturó en ese fallo:
no se inventa su contenido. El rechazo sólo puede venir de un ProviderId ajeno al
registro. El adaptador Core pasaba ese metadato directamente al dominio, mientras
SemanticDecision ya vinculaba objetivos locales a ollama y tareas sin destino a null.

Reproducción determinista: cuatro tests rojos para ejecución/preparación local y
metadato irrelevante en lectura (`intent-provider-owner-red.trx`). Corrección de
propiedad: extraer la regla existente a ProviderRegistry.DestinationFor y reutilizarla
en SemanticDecision y el adaptador Core, antes de fijar el contrato. El proveedor
aceptado también queda fijado en TaskIntentContract. Un destino externo desconocido
sigue fallando; no se selecciona un proveedor remoto ni se concede ejecución externa.
No se cambian modelo, endpoint, transporte o motor ni se añade routing por frases.
La mejora de la tarea real requiere otra corrida; la causa exacta de la propuesta
inicial de ese evento permanece sin captura, distinta de este defecto comprobable.
El esquema también limita providerId a IDs del registro o null, de modo que Core
puede reparar un identificador inventado dentro de su presupuesto existente, antes
de entregar la propuesta. No se añaden alias por nombre de modelo ni retries.

## Resultado definitivo del incremento, sin cierre de versión

Candidato congelado **intent-owned-provider**; Qwen3:8b y digest
500a1f067a9f782620b40bee6f7b0c89e17ae61f686b92c24933e4ca4b2b8b41.
Manifiesto `docs/evidence/skill-audit-20261021/intent-owned-provider-manifest.json`
incluye fuente, binarios API/Evaluation, SDK/worker y SHA del arnés. No cambios de
DB, endpoints, modelo o activación de la revisión 5196.

| Capa | Evidencia ejecutada | Dictamen |
|---|---|---|
| Backend | intent-owned-provider-backend.trx; 1080/1080, 22 casos nuevos respecto de 1058 | PASS de esa capa |
| Compartidas | intent-owned-provider-shared.trx; 168/168 | PASS |
| SDK / worker | 42 / 4; mismo SDK sin cambios | PASS |
| Fitness de módulos | intent-owned-provider-modules.json; 35 comprobaciones | PASS estático |
| Desarrollo Qwen | intent-owned-provider-development.json; 13/16 estrictos | FAIL; 02/03/11 |
| Navegador real | intent-owned-provider-ui/1792621775115/results.json; 8/9, oráculo clipboard-task-outcomes/2 | FAIL; preparación |
| Validation / holdout | Gate de desarrollo fallido | NOT RUN |

El navegador comprueba copias explícitas y libre con recibos nuevos, lectura,
ejecución local real, actividad del modelo anterior a completar, otra entidad sin
cambios y una sola pestaña. También pasa otro esquema de cliente/tarea con el
mismo Core y Qwen, sin efectos en DB. Es sustitución de contrato, no prueba de un
segundo producto completo ni de universalidad del framework.

La ejecución local pasa después de vincular su destino al registro. El desconocido
externo sigue rechazado por pruebas. El fallo restante de preparación está
correlacionado por runId y taskEvidence original: declara COMPLETED con adaptación
y definición cambiada; el arnés lo rechaza. No se sustituye ese fallo por el PASS
del oráculo anterior. No se afirma que la corrección haya resuelto la semántica.

Las tres divergencias de desarrollo permanecen en la propuesta de efectos inicial:
02 propone objetivos incompatibles, 03 agrega clipboard a lectura y 11 confunde
preparar copia con copiar ahora. El anfitrión todavía puede aceptar un encargo
inicial equivocado. Congelarlo evita modificaciones posteriores del plan, pero no
es una validación semántica independiente de la intención humana. Esa brecha
continúa abierta; el anterior juez no calibrado no se integra como parche.

```text
propuesta = Core.infer(petición original, contexto autorizado, esquema del dominio)
significado = dominio.bind(propuesta, citas literales, referencias ofrecidas)
significado.destino = ProviderRegistry.DestinationFor(efectos, metadato)
contrato = fijar(significado)                    # interpretación aún falible
plan = compilar(significado)
validar compatibilidad del contrato antes/después de resolver fuentes
ejecutar solamente herramientas permitidas; observar recibos/revisiones
si recibos satisfacen contrato: la aplicación puede declarar COMPLETED
arnés independiente compara petición original con efectos reales
si interpretación inicial es incorrecta: FAIL aunque la aplicación diga COMPLETED
```

La revisión 5196 conserva la compilación anterior; salud comprobada, sin lectura
de su DB. Verificación final: cero listeners QA 5243 y cero procesos del candidato
intent-owned-provider; salud 5196 ok, PID 30760 preservado, schemaVersion 2.
La matriz de 88 requisitos mantiene sus estados; no se cierran filas por este incremento.

## AI Delivery Lab — integración explícita y alcance independiente

Se importó `AI_Delivery_Lab_v0_2.zip` en `../AI-Delivery-Lab`, con SHA256 y hashes
originales en `IMPORT_PROVENANCE.json`. Las 31 pruebas del ZIP se ejecutaron antes
de editar. No se instaló una habilidad global ni un proveedor, framework o modelo
adicional; se conserva Qwen3:8b con su digest existente.

### Contrato y propietarios

Core ofrece un callback opcional `output_check` en su validador Pydantic AI.
Schema y scope comparten el límite total de dos llamadas: una propuesta rechazada
no reinicia otro presupuesto. El SDK no importa Lab ni vocabulario QuickPrompt.
`adaptive-inference/1` sigue vigente; `ai-delivery-guard/1` es un sobre interno
opcional del Lab, con petición original, scope independiente, hash y proyección
tipada de efectos. No publica COMPLETED ni ejecuta acciones.

`RecoveryEvaluation.Lab.cs` conecta el puente antes de `Bind/Compile` sólo al
declarar `NOVA_EVAL_LAB_ROOT`, dentro de fixtures SyntheticOnly. `Case.Required`
es la autoridad QA de los objetivos. El alcance se comunica al modelo y la
corrida se rotula `QA_SCOPE_ASSISTED_DIAGNOSTIC`; su gate ciego es
`NOT_APPLICABLE_QA_ASSISTED`. Ninguna fila REL se cierra con esta asistencia.

`SemanticContext.CanContinuePendingTask` reúne la precondición ya existente del
dominio: plan pendiente y petición no vacía. El esquema ahora sólo ofrece true
cuando esa precondición existe; Compile y SemanticPlanValidator reutilizan la
misma regla. No se añade un normalizador por frase ni otro motor semántico.

### Reproducciones y correcciones

1. El scope congelado del ZIP admitía ampliar un set original del caller después
   de crear el contrato. Se copian y convierten las tres colecciones a frozenset;
   se validan tipos y presupuesto entero. Reproducción roja preservada.
2. El evaluador aceptaba recibos después del final, propuesta en fase incorrecta,
   booleanos como secuencias y hashes inválidos. Cuatro negativos rojos y verdes.
3. El nuevo puente heredaba CP1252 en Windows; .NET consumía UTF-8. Se fuerza
   UTF-8, probado mediante proceso real bajo CP1252, HTTP sintético, acentos y
   caracteres chinos. Core.host ya tenía correctamente esta configuración.
4. En lab-v021, 02/03/11 pasan asistidos; controles 11/13. Los dos rechazos restantes
   tenían continuación inexistente. Cinco pruebas rojas demuestran que el esquema
   ofrecía metadatos que el validador posterior no podía aceptar; corregido en el
   propietario .NET, no dentro de Python.
5. lab-v022 terminó 14/16 asistidos: 08 eligió una fuente incorrecta y 16 agregó
   búsqueda como objetivo. El adaptador QA había aplicado el cierre de dependencias
   del plan a la propuesta de objetivos. El contrafactual sobre la salida exacta
   16 la rechaza usando únicamente los objetivos originales. `lab-v023` conserva
   esa distinción antes de Compile. Los candidatos anteriores permanecen intactos.

### Evidencia independiente de UI

`Tests/nova/export-delivery-lab.py` transforma solamente el escenario prepare del
reporte histórico SyntheticOnly `clipboard-task-outcomes/2`. Su fixture conserva
texto/valores esperados por el arnés; una decisión de herramienta no se convierte
en recibo. El cambio observado de definición genera un efecto prohibido y ausencia
del resultado COPY_READY. Lab devuelve FAIL aunque el agente dijera COMPLETED;
`diagnose` emite STOP_AND_AUDIT_EFFECTS y promotion_permitted=false.

Este análisis histórico no equivale a un nuevo navegador sobre lab-v023. La
campaña asistida actual interpreta y resuelve fuentes SQLite, no ejecuta UI,
generación de contenido, clipboard, guardado o efectos del usuario.

### Evidencia reproducible

- 1085/1085 backend PASS, incluyendo cinco casos nuevos del esquema de continuidad;
  `.playwright/skill-audit-20261021/tests/lab-v022-backend.trx`.
- Lab 48/48 PASS con venv de inferencia; base sin extras: 39 PASS y 9 SKIP.
- Core SDK 42/42 y worker stdio 4/4 PASS; exportador de evidencia 5/5 PASS.
- Arquitectura estática 35/35 PASS; no valida fiabilidad semántica general.
- Segundo consumidor mínimo de soporte con mismo puente y Qwen real: propuesta
  DRAFT_REPLY, ticket sintético 7, una llamada, cero efectos. No es una segunda
  aplicación completa ni acredita proveedores distintos.
- Ejecución reproducible: `Tests/nova/verify-delivery-lab.ps1` con Python, SDK,
  LabRoot y Candidate explícitos. Congela binarios/SDK/Lab y preserva hashes,
  modelo, dataset y umbrales originales. No activa una instancia API.

El runtime público sigue sin un scope humano independiente para el chat libre.
Un conjunto de operaciones conforme tampoco demuestra selección correcta de la
fuente: el ejemplo 08 anterior evidencia ese límite. Se preservan la evaluación
ciega 13/16 y UI 8/9 previas; validation/holdout y nuevo E2E no se ejecutan como
calificación de este incremento. La revisión 5196 permanece en PID 30760,
`/health` status ok/schemaVersion 2, sin lectura de contenido privado ni despliegue.

### Ampliación visual y observación nativa Core

El Lab ahora relaciona nodos con modales de eventos, casos, archivos y semántica;
las tarjetas conectan petición, opciones ofrecidas, propuesta, contrato y valores.
El visor de código tiene sintaxis, gutter, línea registrada y explorador con filtro
por misión y búsqueda de archivo/símbolo. Core incorpora el puerto opcional genérico
`infer(..., observer=sink)` / `adaptive-observation/1`, sin importar el Lab o reglas
QuickPrompt. El protocolo de inferencia, permisos y postcondiciones no cambian.

Se reprodujo y corrigió un fallo real del propio Lab: respuestas de estado más
lentas que su intervalo causaban descarte perpetuo por consultas solapadas.
Ahora se serializan. El navegador prueba retraso real de lectura 1600ms frente
al intervalo1200ms. La misión `lab-live-583050ede772` conservó el mismo modal desde
RUNNING hasta PASS con Qwen real, con animación y valor exacto, sin recargar ni
errores JS. Esa evidencia está en `../AI-Delivery-Lab/artifacts/visual/live-browser/`.
Las iteraciones que fallaron se conservan.

Validación ampliada: Lab59, Core46 y navegador Lab11 PASS. Backend1085 PASS de la
revisión anterior de este incremento; no se volvió a cambiar código .NET después.
Un segundo consumidor de soporte, con otro esquema/proyección, ejecutó Qwen real
desde su UI en8791: ticket7 correcto, DRAFT_REPLY, cero efectos y cuatro eventos
nativos Core. Evidencia en `../AI-Delivery-Lab/artifacts/support-visual/`.
Es un segundo consumidor mínimo, no universalidad ni otra aplicación completa.
La campaña de16 asistida previa mantiene15/16 y development08 como fallo abierto;
el hook de observación no es una corrección de interpretación. No se acreditan
puertas ciegas, holdout o UI final del agente QuickPrompt ni se cierra la versión.

### Lab visual: ejecución observada y diagnóstico por valores

Se implementó `../AI-Delivery-Lab` como consola local independiente. La entrega
visual está en `http://127.0.0.1:8790/`: mapa con nodos/iconos, fases registradas,
animación condicionada a proceso/señal reales, meta PASS/FAIL/sin confirmar,
timeline, visor de código, comparaciones por campo, contratos de metas creados
desde UI, matriz oficial de sólo lectura e informes JSON/HTML/SVG persistentes.
La UI no ejecuta comandos arbitrarios; usa adaptadores configurados y sesión
local con comprobación de origen/token. Los valores públicos requieren fixtures
sintéticos y captura explícita. No instrumenta automáticamente todo el programa.

La misión `lab-live-0ffca2291a42`, iniciada desde UI, ejecutó los 16 casos con
Qwen3:8b real entre 2026-10-22T00:16:00Z y 00:22:30Z (hora UTC): 15 PASS / 1 FAIL,
fixture inalterada. Continúa fallando development-08 por origen: esperado informe
`...0001`, obtenido receta `...0002`. La observación queda en el diario visual
con su caso y la línea real del verificador `RecoveryEvaluation.cs:151`.
Es un punto de detección; no se atribuye automáticamente la causa raíz a esa línea.
El alcance propuesto es conforme, lo cual no prueba correcta identidad de origen.
El perfil sigue siendo `ADAPTIVE_CORE_QA_SCOPE_ASSISTED`; ni ciego ni ejecución UI.

Una meta creada desde el formulario (`goal-b3d74178d7064fc0`, misión
`lab-live-d8ce2eca2b23`) exigió FILL_VARIABLES con `equipo = Equipo Aurora`:
PASS, valor vinculado exacto por `InputScope`. Su contrato y evidencia se conservan.
El Lab propio pasó 58 tests (85 observaciones incluyendo subcasos, 0 omitidos)
ejecutados también desde UI (`lab-live-76f730144206`). Backend 1085/1085 y Core
42/42 PASS. El navegador del Lab pasó nueve controles reales, incluyendo mapa,
inspector, valor literal, código, teclado, matrices, gráficos y 390/768px.

Evidencia: `../AI-Delivery-Lab/artifacts/visual/browser/acceptance.json`,
`artifacts/visual/events.jsonl`, `artifacts/visual/runs/` y `artifacts/visual/reports/`;
campaña Qwen: `docs/evidence/skill-audit-20261021/lab-live-0ffca2291a42/`.
Los intentos de navegador fallidos del arnés y el reinicio rechazado se conservan.
La instancia nueva evita detener servicios previos. No se migró DB ni se desplegó
el API; estados de cierre intactos. Validation/holdout y UI final de QuickPrompt
siguen sin calificarse en este incremento.


### Uso normal de la app y Lab en paralelo, Development

Se añadió un observador opcional y un decorador de ILiveRunStore: después de
persistencia/validación registra eventos reales del agente y recibos React.
AdaptiveCoreModelTransport propaga el runId actual al sink genérico del worker.
No se cambian permisos, decisiones, esquema HTTP o base de datos. Producción
rechaza opt-in antes de construir la base; captura privada apagada por defecto.
El modo de contenido sintético exige base QA .playwright y activación explícita.

Revisión nueva independiente: app5197 + Lab8792; 5196 y servicios anteriores
intactos. Qwen3:8b real abrió desde React el prompt sintético exacto; recibos
verificados, biblioteca sin cambios, 67 eventos unidos por runId
8696b69d-0b56-4fb5-8c9c-eb60d190da48. Ocho comprobaciones E2E PASS; Lab64,
Core49 y backend1088 PASS, cero omitidos; publish y build React PASS (advertencia
de tamaño de chunk existente). Control negativo Production sin DB ni diario PASS.
El Lab muestra COMPLETED como UNCONFIRMED cuando no recibió oráculo independiente;
la prueba externa acredita únicamente este caso real, no peticiones arbitrarias.

Contrato/configuración/algoritmo y limitaciones en ../AI-Delivery-Lab/docs/VISUAL_LAB.md.
Evidencia ../AI-Delivery-Lab/artifacts/quickprompt-development/20261021-211336/;
intentos fallidos del arnés preservados. Matriz oficial 88 estados intactos:
33 cerrados, 55 abiertos. Development08 sigue fallando origen; no nueva campaña
ciega/holdout ni calificación general de UI. Este incremento no cierra QuickPrompt.

### Continuación del diagnóstico con evidencia correlacionada

El estado anterior describe aquella campaña, no el último candidato. La investigación
posterior y los intentos conservados están en `LAB_CLOSURE_DIAGNOSIS_20261021.md`.
Se comprobaron defectos de transporte de roles en Core, recuperación con puntuación
en .NET, binding de valores literales y clasificación de candidatos. El contrato
privado /4 publica el catálogo real de proveedores; adaptive-inference/1 permanece
genérico. Backend1110, Core52 y Lab72 PASS. El candidato `provider-contract` pasó
Claude con identidad estricta y desarrollo16/16 sin alcance QA entregado al modelo.
App5198 y Lab8798 permiten inspeccionar fuentes congeladas y esperado/obtenido.
La validación general y los flujos UI finales se acreditan por separado; los
55 requisitos abiertos permanecen abiertos hasta satisfacer sus criterios propios.


Actualización posterior: el candidato input-authority-v9 corrige en .NET la proyección de valores de campos hacia tareas que no los requieren. Backend1117 y desarrollo16/16 pasan; UI12/13 falla por omisión de preparación de Claude en Core/Qwen. Lab8800 y revisión sintética5199 están disponibles. La certificación lab-live-3814e79d2c91 es FAIL y los55 requisitos siguen abiertos. Véase LAB_CLOSURE_DIAGNOSIS_20261021.md para causas separadas, intentos descartados, prueba Win32 y evidencia exacta.
