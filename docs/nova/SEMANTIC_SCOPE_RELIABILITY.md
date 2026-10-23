# Fiabilidad por responsabilidad semántica

2026-10-19. IMPLEMENTACIÓN PARCIAL; versión NO CERRADA. Se aplica Universal Modular Architecture local junto con algorithmic-engineering y adaptive-ai-systems. Los resultados de componentes, comandos tipados y peticiones libres se separan.

## Auditoría y causa compartida

PRESENTES: modelo local Qwen, catálogo de capacidades por Reflection, retriever SQLite, selector de cardinalidad, sesiones, UI de la misma pestaña, recibos y aprobación. Recuperación vectorial y TaskGoals/TaskResultContract: experimentales, DESACTIVADOS. Otro modelo/framework: INNECESARIO para comprobar este cambio.

Hechos reproducidos: el selector comparaba toda la orden y sus datos de caso con la función del origen. Tres regresiones fallaron antes del cambio, incluyendo el título de otro registro dentro de un campo. El intérprete también decidía efectos antes de leer el cuerpo y los campos del origen, para volver a interpretarlos después. En boundVariables de modular-v1 se buscó el tema del caso en lugar de Proyecto técnico. El adaptador de destino convirtió PREPARE/null a capacidad antes de revisar si el uso de IA estaba solicitado. En guardado modular-v2 el generador incorporó el workflow de aprobación al cuerpo; la revisión lo rechazó y no hubo persistencia. Este último fallo pertenece a autoría, no a cardinalidad o al control de formularios.

## Contrato y arquitectura elegida

Se retira la clase/archivo SemanticPlanning; su original se conserva en evidencia. ITaskPlanner/NovaTaskPlanner es el puerto y coordinador compartido de UI, preparación, comandos y evaluaciones. Responsabilidades:

| Propietario | Contrato y dependencias | Estado/autoridad |
| --- | --- | --- |
| NovaTaskPlanner | Context, Interpret y Resolve; compone los propietarios existentes | No implementa CRUD ni concede permisos |
| TaskConversationContext | Restaurar y guardar checkpoint mediante AgentSessions | AgentSessions sigue siendo la única autoridad de conversación |
| TaskSourceResolver | Lectura fresca, preparación, lookup compartido y refinamiento dependiente del origen | ID, revisión, archivo y cardinalidad; LookupSource es un método interno, no otro módulo |
| ISourceReferenceResolver | Capacidad opcional del intérprete para resolver referencia antes de la tarea | null conserva el adaptador combinado; no se inventa soporte |
| ResolvedTaskReference | Vínculo efímero SHA256 de petición + referencia + revisión | Interno/JsonIgnore; no equivale a selección humana, aprobación ni efecto |
| PreparationInference | Semántica de referencia, contenido, campos, uso y workflow | Propone; dominio y ejecutor conservan autorización y recibos |
| TaskClarification | Suspender efectos conservando objetivos diferidos | No sustituye origen ni declara cumplimiento |

Alternativas: A) extender el monolito con más reglas por fallo, rechazada por el acoplamiento observado; B) composición pequeña con propietarios separados y puertos existentes, elegida; C) nuevo framework/orquestador/índice/modelo, injustificado. No se añade routing por palabras/regex ni clases por frase. Las condiciones de revisiones, permisos y ciclo de vida se conservan.

La búsqueda compara sólo Query/descripcion del origen; la tarea original completa y los valores llegan al intérprete de objetivos. Los nombres reales de campos del catálogo permiten excluir datos literales de la vista de referencia, sin conceder identidad ni valores. La fuente se carga antes de interpretar sus usos; sus datos permanecen separados de Selected. Se relee su revisión antes de efectos y no se repite selección/refinamiento cuando ese vínculo ya existe.

Compatibilidad: SemanticResolution y HTTP/JSON públicos se mantienen. Los consumidores C# y DI se migran a ITaskPlanner; no hay migración SQLite ni instalación, cambio de modelo, activación experimental o despliegue. Para sustituir intérprete, implementar su contrato y opcionalmente ISourceReferenceResolver; la ausencia de esa capacidad usa el flujo combinado probado. No se afirma que cualquier nuevo adaptador sea compatible sin validar sus contratos. Rollback: snapshots de consumidores y archivo retirado conservados; no descartar trabajo ajeno ni relajar controles.

## Flujo ejecutado

~~~mermaid
flowchart TD
 A[Petición original y contexto de UI] --> P[NovaTaskPlanner]
 P --> C[TaskConversationContext restaura sesión]
 C --> T{¿Comando tipado?}
 T -- Sí --> V[Compilar y validar en dominio]
 T -- No --> R{¿Referencia separada disponible?}
 R -- Sí --> Q[Resolver referencia con datos de campos excluidos]
 Q --> L[TaskSourceResolver.LookupSource con descripción del origen]
 L --> B{¿Origen único vigente?}
 B -- No --> X[TaskClarification y checkpoint; sin efectos]
 B -- Sí --> S[Leer y ligar petición, origen y revisión]
 R -- No --> G[Intérprete de tarea]
 S --> G
 G --> V
 V --> F[Resolver dependencias y releer revisión]
 F --> K[Checkpoint sin permiso de efectos]
 K --> O[Ejecutor existente, UI, recibos y aprobación]
~~~

~~~text
restaurar contexto mediante AgentSessions
si comando tipado: compilar/validar con el contrato existente
si hay tarea pendiente de campos o selección: conservar su petición y límites
si hay referencia separada:
    excluir datos de campos declarados sólo para resolver referencia
    comparar descripción del origen con candidatos reales
    aclarar ausencia/ambigüedad sin elegir por score
    leer fuente vigente y ligar referencia a petición/revisión
interpretar objetivos con petición original, datos y fuente real
validar; refinar sólo cuando el origen no estaba disponible antes
releer revisión; rechazar cambios, archivo, identidad ajena y cancelación
conservar checkpoint; entregar a herramientas existentes
persistir sólo con aprobación vigente del cuerpo exacto
~~~

| Nodo | Implementación | Evidencia |
| --- | --- | --- |
| P/C/K | TaskPlanning.cs y TaskConversationContext.cs | backend-modular-equivalence.trx + SemanticContextTests |
| Q/S | ResolveSourceReference y ResolvedTaskReference.Bind/Validate | SourceReferenceScopeTransportTests; GroundedCatalogReferenceTransportTests |
| L/B | TaskSourceResolver.LookupSource | SourceSelectionBoundaryTests antes/después; ReferenceFirstPlanningTests |
| X | TaskClarification.Apply y Checkpoint | ausencia, duplicados y petición pendiente en ReferenceFirstPlanningTests |
| G/V/F | Interpret y Resolve + SemanticPlanValidator | backend-reference-first-final.trx y backend-destination-order-verified.trx |
| O | Ejecutor/UI ya existentes | recorridos reales registrados debajo; no equivale a todas las peticiones libres |

Invariantes: IDs reales, cero/uno/varios explícitos, no elección por similitud, petición y datos originales conservados, revisión vigente, cancelación acotada y ninguna aprobación implícita. Las pruebas incluyen cambio de revisión durante matching/inferencia, fuente ajena, ausencia, ambigüedad, adaptación sin referencia separada y fuente cargada que no altera la selección UI.

## Evidencia ejecutada

Carpeta: docs/evidence/semantic-scope-20261019. Los TRX iniciales con errores de fixtures y experimentos rechazados se conservan; sólo la tanda posterior verificada se acredita.

- SourceSelectionBoundary: antes 0/3; después 10/10. Comparación aislada Qwen sobre descripción del origen: 5/5 frente a 3/5 con orden completa. Es desarrollo de un componente, no holdout/UI.
- Equivalencia de sustitución: 551/551. Referencia temprana: 12 contratos PASS, luego backend 564/564; proyección de campos: 567/567.
- Orden de revisión del destino: baseline válido 2/6 (cuatro fallos reproducidos), después 6/6. Backend final modular-v3: 573/573, cero omitidas; build API cero warnings/errores. La tanda de transición con 571/573 se conserva; se actualizaron dos simulaciones de transporte sin retirar sus aserciones de seguridad.
- Frontend no cambia en este incremento; su última evidencia correspondiente a web-v3 es 112/112. No se presenta como repetición actual.
- Modular-v2, Qwen/Edge: apertura libre PASS 34982 ms/7 llamadas; variables FAIL 30298 ms/7; boundVariables FAIL 7021 ms/5 por PREPARE/null. Mismos mensajes congelados, sin bajar oráculos. Raw: latency-1792435586957.
- Modular-v2, UI con comandos tipados: 8/8 controles, seis runs COMPLETED y respuesta local LISTO. Incluye campo/preview real, proveedor sin envío y guardado manual/reapertura. Raw: ui-surface-20261019/1792435835910. No demuestra comprensión de lenguaje libre.
- Modular-v2, guardado libre: FAIL/BLOCKED INVALID_DECISION, 113996 ms/14 llamadas; dos cuerpos con workflow de aprobación, rechazados antes de guardar. Raw: latency-1792435920703. El guardado PASS del build anterior es histórico y no acredita esta sustitución.

Prototipos NO INCORPORADOS: reducción de contexto de destino, testigo de destinatario, reasoning con presupuestos original/ampliado, gate binario de interés y uso conjunto (joint-use-development: 2/9 en su contrato acotado). Esquemas válidos siguieron añadiendo o perdiendo efectos; no se activan ni se reinterpretan como éxito. Otros prototipos rechazados: provider-first 4/9 y actor-boundary con capacidades declaradas 5/9. Exponer las descripciones de herramientas por sí solo no demuestra comprensión. No se aumenta presupuesto de producción. La validación semántica general y autoría siguen abiertas.

Modular-v3 (latency-1792436543200): apertura PASS 43160 ms/7 llamadas; variables FAIL 29857 ms/7 y boundVariables FAIL/BLOCKED 8591 ms/7. La corrección del orden de revisión no recupera por sí sola la clasificación. La captura variables-1.png fue inspeccionada: el campo incluye la instrucción final de copiar porque la gramática literal existente consume hasta el final de la asignación; el preview conserva esa contaminación. No se describe como fidelidad completa de valores. Modular-v4 añade la proyección uniforme de workflow al editor: backend 575/575 y builds API/evaluador sin advertencias/errores; los recorridos finales se registran en validation.json. No hay p95 ni prueba de rendimiento bajo memoria estable. La UI personal 5190 y su proceso se conservan, y todos estos recorridos usan SQLite sintética.

## Contrato: revisar el uso antes de resolver la capacidad (2026-10-19)

Observado en boundVariables del build modular-v2: PREPARE/null se convierte mediante DestinationGoal.Objective antes de la revisión de alcance y aborta la tarea. Esa conversión mezcla una propuesta semántica sin verificar con la disponibilidad de la capacidad. Propietario existente: PreparationInference.ReviewExecutionScope; no se añade otro clasificador ni se cambia el esquema del modelo.

Alternativas: A) cambiar el orden y hacer que la revisión reciba la acción propuesta, elegida; B) introducir un segundo validador/reintento, rechazada por duplicar propiedad y llamadas; C) asignar proveedor o retirar el efecto por nulidad, rechazada porque la disponibilidad no demuestra intención.

Entrada/salida: acción y proveedor propuestos -> revisión de petición actual/pendiente -> destino conservado o descartado -> conversión a ExecutionObjective sólo para un efecto realmente solicitado. La revisión admite proveedor aún desconocido: una solicitud de IA sin proveedor válido sigue fallando; no se degrada silenciosamente a lectura. Ejecución requiere evidencia actual. Ningún cambio HTTP/SQLite/modelo/autoridad. Cancelación y dos intentos existentes permanecen acotados.

~~~mermaid
flowchart LR
 A[Propuesta de uso] --> B[ReviewExecutionScope: petición literal]
 B --> C{¿Uso de IA solicitado?}
 C -- No --> D[Descartar sólo efecto de IA]
 C -- Sí --> E[DestinationGoal.Objective: validar proveedor/capacidad]
 E --> F{¿Capacidad válida?}
 F -- No --> G[Rechazo acotado sin efectos]
 F -- Sí --> H[Workflow y validación de dominio existentes]
 D --> H
~~~

~~~text
para cada intento ya permitido:
    interpretar propuesta de destino con el contrato actual
    revisar acción contra petición literal, sin convertirla aún a capacidad
    si no solicitada: destino NONE, sin elegir proveedor
    si solicitada: validar proveedor y capacidad; conservar rechazo si inválida
    componer campos/contenido/workflow con los propietarios existentes
    respetar cancelación, revisión y aprobación antes de efectos
~~~

Verificación previa/posterior: DestinationValidationOrderTests prueba PREPARE sin proveedor y EXECUTE_NOW externo tanto solicitados como no solicitados; positivos de preparación externa y ejecución local; ausencia de edición/guardado añadidos. Regresión completa y mismo mensaje libre en UI/Qwen antes de afirmar recuperación.


## Contrato: workflow del editor independiente del perfil

EditorWorkflowBoundaryTests reproduce una divergencia: con TaskGoal se transmite ApplicationEffects; sin él se transmite null aunque SemanticInterpretation ya contiene los mismos efectos validados. Es el flujo de producción actual. Propietario: PreparationInference.EditTemplate. Se extiende su proyección existente usando Goal.RequiredEffects o, cuando no existe ese envoltorio experimental, plan.Intentions. Se conserva el filtro de autoría, el usuario literal, el delta interpretado y la revisión independiente. No se cambia autoridad ni se entrega invocación de herramientas al editor.

Alternativa A: completar la proyección actual, elegida; B: nuevo parser de frases de workflow, rechazado; C: activar TaskGoals para recibir el campo, rechazado por cambiar todo el perfil sin evidencia. Ningún endpoint, esquema de persistencia, modelo, límite o llamada adicional. El adaptador directo sin interpretación conserva su contrato.

~~~mermaid
flowchart LR
 A[Plan validado] --> B[Proyectar efectos de aplicación al contrato del editor]
 B --> C[Editor: usuario literal y delta separados del workflow]
 C --> D[ReviewDraftFidelity contra autoridad original]
 D --> E{¿Artefacto fiel?}
 E -- No --> F[Rechazo y corrección ya acotados]
 E -- Sí --> G[Formulario, revisión y aprobación existentes]
~~~

~~~text
obtener los efectos validados del plan, exista o no TaskGoal
proyectar los efectos ajenos a autoría en el campo ApplicationEffects existente
generar sin ejecutar, guardar ni conceder autorización
revisar el cuerpo contra petición/fuente originales, sin cambiar el revisor
rechazar contaminación y conservar evidencia; aprobar sólo mediante dominio
~~~

La prueba de contrato sólo acredita transporte coherente; no acredita fidelidad del modelo. Repetir saveVersion completo con Qwen/Edge antes de afirmar recuperación.

## Resultado del candidato final modular-v4

[Manifiesto y hashes](../evidence/semantic-scope-20261019/validation.json). Backend 575/575 PASS; API y evaluador compilan sin warnings/errores. UI real Qwen/Edge: **3/3 PASS en el alcance congelado**: saveVersion 134256 ms/14 llamadas, provider 58262 ms/9, local 35578 ms/9. [Evidencia bruta](../evidence/latency-1792436909263/validation.json). Son pruebas de desarrollo, no holdout ni aceptación general.

El primer cuerpo de adaptación aún contenía workflow; la corrección acotada produjo una instrucción para solicitar una entrevista sin órdenes de sesión. Se inspeccionaron cuerpo completo y captura. La aprobación real se consumió, el mismo ID pasó de revisión 1 a 2, formulario/preview/borrador aprobado/registro coinciden, cardinalidad igual y otros registros intactos. No se presenta como recuperación general de creación ni como generación fiel al primer intento. El resumen conserva REQUEST_SAVE_COPY; el resultado de escritura validado fue la revisión 2 del mismo origen con el control de revisión/aprobación existente, sin elegir manualmente otro tipo de persistencia. No se normaliza ni borra el resumen para ocultarlo.

Claude quedó preparado sin envío ni ejecución externa. La ejecución local devolvió LISTO exacto. Campos y preview nativos coinciden, correlación de inferencias válida, una pestaña y cero peticiones externas en los tres recorridos. Capturas de guardado/local inspeccionadas. No hay prueba de reinicio de ese guardado ni comparación de latencia bajo RAM estable.

Siguen abiertos los dos recorridos libres de variables/copia y los criterios ya existentes de autoría, continuidad y rendimiento. La modificación v4 sólo toca la proyección del editor: no se repiten esos dos recorridos como si hubieran pasado en v4. Los fallos de v3 se mantienen vigentes. Matriz **88 obligatorios / 21 cerrados en alcance histórico / 67 abiertos; NO CERRADA**. Sin cambios de criterios. La instancia 5190/PID 3480 permanece intacta; puertos QA 5236/5237/5242 cerrados tras limpiar recursos propios.
