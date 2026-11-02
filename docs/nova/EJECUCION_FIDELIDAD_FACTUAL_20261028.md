# Fidelidad factual de ejecuciones: diagnóstico del 28 de octubre de 2026

Estado: **REEMPLAZO IMPLEMENTADO Y PROBADO EN POSTGRESQL / ACEPTACIÓN COMPLETA PENDIENTE**. La asignación autoritativa de roles factuales desde prosa libre activó `REFACTOR_OBLIGATORIO`; el nuevo modo usa revisión explícita y composición literal, conservando generación general como propuesta revisable. El candidato final corrige además la frontera de citas propuestas, igualdad de hechos frente a JSONB, rechazo de cuota y permisos heredados. Este expediente separa ejecución de plantillas de autoría asistida de NOVA. El contrato de autoría confirmado no verifica los hechos generados al ejecutar una plantilla.

## Caso y evidencia

El recibo histórico `rc7-primary-qwen-acta-acceptance.json` identifica el prompt QA `30703999-227e-42e9-b866-f2b029152747`, revisión 1, perfil `Mi-PC-Ejecutar-Prompts`, Qwen3:8b y una ejecución que conservó los hechos principales pero añadió participantes no establecidos. El supuesto archivo de respuesta RC7 sólo captura una vista de accesibilidad sin cambios: **no contiene notas ni respuesta literal**. No permite reconstruir el paquete original.

La plantilla literal recuperada por el propietario desde MCP es:

> Convierte las notas de &lt;reunion&gt; en un acta en español. Identifica acuerdos, tareas con responsable y plazo explícitos, y preguntas pendientes. Usa sólo estas notas: &lt;notas&gt;. Marca como pendiente cualquier dato que no aparezca; no lo inventes. Entrega Markdown.

El propietario recuperó después la instrucción y respuesta completas desde la UI del único resultado QA autorizado: `.playwright/saas-delivery-20261026/rc7-acta-recovered-source-20261028.json`. Valores exactos:

- `reunion`: `Revisión del prototipo · 28 de octubre de 2026`.
- `notas`: `Ana terminó el prototipo. Luis revisará las pruebas el 30 de octubre de 2026. El equipo acordó mantener el alcance actual. La fecha de publicación no se ha confirmado.`

Antes de la nueva inferencia A, `EditorTools.Compile` se comparó por igualdad exacta con `blocks[0]`: 458 caracteres, SHA-256 `839E57D3017D228B1810B2BF6637FC3CB841D805F74761DB1C848C1EA26C827B`, incluida la doble puntuación tras `confirmado..`. No se reformuló ni limpió el caso.

El control B sintético fijó antes de inferencia asistentes explícitos, una persona mencionada que **no asistió**, dos tareas con un único plazo explícito, decisión y fecha de publicación desconocida. Los criterios originales y el paquete completo quedan en `.playwright/saas-delivery-20261026/local-execution-factuality-20261028-02/acta-control-b-explicit-roles.json`; revisión manual del autor en `semantic-review.json` del mismo directorio.

## Recorrido observado y propietarios

```mermaid
---
title: Ejecutar una plantilla emparejada y comprobar sus hechos
---
flowchart TD
  E0[/Plantilla y valores autorizados/] --> E1[Capturar revisión y valores]
  E1 --> E2{¿Identidad y concesión vigentes?}
  E2 -->|No| EX([Rechazar sin ejecución])
  E2 -->|Sí| E3[Compilar plantilla literal]
  E3 --> E4[Construir un mensaje de usuario]
  E4 --> E5[Inferir con Qwen3:8b]
  E5 --> E6{¿Respuesta correlacionada y acotada?}
  E6 -->|No| EF([Error técnico sin resultado])
  E6 -->|Sí| E7[Guardar texto recibido]
  E7 --> E8[/Mostrar resultado generado/]
  E5 -. Primera divergencia B: tarea convertida en bloqueo .-> E9[Comparar roles con hechos fijados]
  E8 --> E9
  E9 --> E10{¿Todos los hechos y roles autorizados?}
  E10 -->|No| EU([Fidelidad no certificada])
  E10 -->|Sí| EP([Aceptar sólo caso evaluado])
```

Las flechas E8→E9 describen la **evaluación**, no un validador semántico instalado en producción. El diagrama no fue comprobado visualmente en un renderizador.

| Nodo | Propietario y contrato |
|---|---|
| E1–E2 | `AccountExecutionAccess.Submit`: captura `CompileWork` con fuente/revisión/valores; comprueba identidad, admisión y conexión. |
| E3 | `ExecutionQueue.Local.ClaimLocal` → `CompileExecutionHandler` → `EditorTools.Compile`: sustitución de variables de la instantánea autorizada. |
| E4 | `CompanionConnection.ClaimInference`: un mensaje `user` con la instrucción compilada, `think=false`, contexto 12288, salida 2600, temperatura 0. Sin mensaje de sistema adicional ni herramientas. |
| E5–E6 | `CompanionRunner` → `AdaptiveCoreModelTransport`: proveedor local seleccionado, límites y correlación del protocolo; contenido no vacío y máximo 64000 caracteres. |
| E7 | `ExecutionQueue.Local.FinishLocal`: revalida dueño, revisión/arrendamiento/cancelación, conserva respuesta y modelo. No reescribe roles factuales. |
| E9–E10 | Revisión del arnés y del autor con hechos congelados. No es una inferencia adicional ni aceptación humana. |

```text
capturar plantilla, revisión, valores y autorización
si identidad/conexión no permiten ejecutar: rechazar sin efectos
compilar los valores de esa instantánea; no extraer otras notas
enviar el paquete del adaptador emparejado al proveedor seleccionado una vez
si falla correlación, plazo o contenido: terminar como error técnico
revalidar arrendamiento, cancelación y propietario
guardar el texto recibido como resultado generado
en evaluación: comparar cada hecho y su rol con el oracle anterior a la llamada
si cambia un rol o añade una afirmación: conservar FAIL aunque el transporte pase
```

## Resultados y mecanismo

Control B: una inferencia real, 17,7 segundos; sin reintento, juez, cambio de modelo ni modificación del producto. Los asistentes y el no asistente se conservaron; la publicación siguió sin fecha. Sin embargo, Qwen clasificó la tarea de accesibilidad sin fecha bajo **Bloqueos**, aunque la nota sólo establece una tarea pendiente de plazo. Añadió una observación que vincula la ausencia de Laura con su participación en una revisión de documentación no establecida por las notas. **FAIL factual del control**, aunque la prueba xUnit pasó sus comprobaciones exclusivamente técnicas.

La primera divergencia observada está en el texto del proveedor. El adaptador envía la instrucción compilada y no transforma tareas en bloqueos. Las comprobaciones actuales verifican transporte, límites y autorización; **no demuestran que una clasificación factual de prosa sea consecuencia de la fuente**. Conservar nombres o citas no autoriza roles nuevos. Éste es un límite confirmado del contrato de ejecución actual; no demuestra incapacidad universal del modelo.

El primer intento del arnés usó por error Python bundled de Codex y falló antes de inferencia (`DEPENDENCY_MISSING`, cero solicitudes al proveedor). Se preserva en `local-execution-factuality-20261028-01`. Seleccionar el entorno **ya existente** de Adaptive Core corrigió sólo el arnés. No cuenta como fallo semántico.

Caso A recuperado: una inferencia real, 20,3 segundos, mismo adaptador y configuración; **FAIL**, porque vuelve a añadir `Participantes: Ana, Luis, Equipo`, aunque no existe afirmación de asistencia. La respuesta es idéntica, sin normalización, a la histórica: 980 caracteres y SHA-256 `C7EEE5C061550FD03DD386BA53116EC059AFF2455684CB45EE0A93E5F89647BF`. Se preservaron el trabajo terminado de Ana, la tarea futura de Luis y su plazo, el acuerdo colectivo y la publicación pendiente. Esos aciertos no compensan el rol inventado. Recibos en `local-execution-factuality-20261028-03/acta-original-a-recovered-rc7.json` y `semantic-review.json`.

Un intento de build anterior a A encontró `LibrarySummary` inexistente en el nuevo test de capacidad de otro propietario. Ese propietario lo corrigió; hubo cero llamadas al modelo antes de esa corrección. Recibo separado `local-execution-factuality-a-build-failure-20261028.json`. No se cuenta como fallo de inferencia.

Presupuesto final: **dos inferencias de dos autorizadas** (B y A), sin llamadas de juez ni más muestras para obtener PASS. Ambos tests xUnit pasaron compilación/replay y transporte; ambos controles semánticos fallaron. Qwen3:8b permaneció seleccionado; no se instalaron dependencias ni se cambió el producto.

El historial verificable de la familia **asignar roles factuales no autorizados por las notas** contiene RC7 (asistencia inferida), B (tarea clasificada como bloqueo) y A (asistencia inferida reproducida). Activa el reemplazo obligatorio de ese mecanismo si se pretende certificar transformación factual. El subtipo exacto de asistencia tiene dos ejecuciones; no fingir tres reproducciones idénticas ni mezclar fallos de autoría, entorno o build. Un control correcto de asistentes en B no borra su error de otra clasificación factual.

## Opciones y cierre

- Un cambio de instrucciones de ejemplo o un juez adicional no proporciona autoridad factual y no está justificado como reparación del contrato.
- Un modo factual explícito podría pedir al usuario hechos/roles confirmados, permitir que IA proponga clasificaciones editables y componer a partir de esa autoridad, con aprobación separada. Eso sería una capacidad nueva y una mitigación de autoría factual; **no una garantía de que la ejecución libre interprete todas las notas**. Requeriría decisión de producto, contratos/UI y casos de otras familias antes de implementarlo.
- Mantener ejecución general significa tratar sus resultados como propuestas para revisión, sin certificación factual autónoma. Una advertencia por sí sola no resuelve la exactitud pedida en el incidente.

No continuar parches de instrucciones, traducciones de encabezados o reintentos sobre el generador libre como si proporcionaran autoridad factual. El caso A ya fue recuperado y reproducido: el incidente permanece abierto por resultado incorrecto, no por falta de entradas. El contrato nuevo propuesto a continuación requerirá alcance de producto confirmado y aceptación con casos originales; no ampliar el presupuesto automáticamente.

Ningún cambio del producto, esquema, permisos, presupuesto o despliegue forma parte de este diagnóstico. La certificación comercial depende del cierre global del propietario de la entrega.

## Contrato de reemplazo propuesto, todavía no implementado

La ejecución general puede seguir existiendo como generación revisable. Para certificar una **transformación factual**, la decisión de roles no debe quedar como autoridad final de un generador libre. El usuario seleccionaría explícitamente ese modo; no se activaría buscando palabras como «acta» o «no inventes».

Contratos mínimos en los propietarios actuales:

| Contrato | Fuente de autoridad y reglas |
|---|---|
| `FactualSource` | Servidor captura cuenta, biblioteca, ejecución, revisión de plantilla, valores y texto compilado exactos. Su versión invalida revisiones pendientes si cambia la fuente. Ningún hash proporcionado por cliente sustituye esta identidad. |
| `FactProposal` | Una propuesta acotada del modelo con hechos, clasificación/rol, extracto y ubicación de fuente. Que el extracto exista sólo prueba procedencia, **no** que autorice el rol. Sin efectos ni resultado factual certificado. |
| `ConfirmedFacts` | El usuario revisa explícitamente contenido, rol y dato desconocido; puede corregir o rechazar. Servidor liga confirmación a la versión de fuente, propietario, conexión y turno vigentes. Guarda diferencia propuesta→confirmación para no afirmar comprensión autónoma del modelo. |
| `FactualLayout` | Secciones y campos seleccionados explícitamente, con desconocidos representados como pendientes. Roles confirmados determinan ubicación; una tarea sin fecha no cambia automáticamente a bloqueo. No hay catálogo de roles inferido por keywords de la petición. |
| `RenderedFactualDocument` | Compositor determinista en aplicación conserva texto y campos confirmados. No hay segunda llamada para reescribir la prosa, ni juez que apruebe sus propias inferencias. Roles no confirmados se omiten o aparecen como no especificados según el contrato elegido. |
| `FinalApproval` | Aprobación final del documento aparte de confirmar hechos. Cancelar, revocar o editar la fuente impide envío tardío/guardado. Preserva límites, cuotas, idempotencia, aislamiento y permisos actuales. |

```mermaid
---
title: Propuesta de transformación factual con autoridad confirmada
---
flowchart TD
  F0[/Modo factual seleccionado por usuario/] --> F1[Capturar fuente autorizada y versión]
  F1 --> F2[Proponer hechos y roles una vez]
  F2 --> F3[/Mostrar propuesta editable y extractos/]
  F3 --> F4{¿Usuario confirmó hechos y roles?}
  F4 -->|No o cancelación| FX([Terminar sin documento certificado])
  F4 -->|Sí| F5{¿Fuente y autoridad siguen vigentes?}
  F5 -->|No| FS([Invalidar revisión pendiente])
  F5 -->|Sí| F6[Componer sólo hechos confirmados]
  F6 --> F7[/Mostrar documento y diferencia/]
  F7 --> F8{¿Aprobación final vigente?}
  F8 -->|No| FX
  F8 -->|Sí| F9[Guardar recibo y documento aprobado]
  F9 --> FE([Terminar dentro del alcance probado])
```

```text
exigir selección explícita del modo factual y de disposición documental
capturar identidad, revisión y fuente exacta con el propietario actual
proponer hechos/roles una sola vez dentro del presupuesto elegido
mostrar cada propuesta con su fuente; jamás convertir cita existente en autorización semántica
esperar confirmación o corrección explícita del usuario
si cancela o cambia fuente/autoridad: invalidar; no componer ni guardar
verificar versión y propietario de la confirmación en servidor
renderizar literalmente los hechos/roles confirmados y los desconocidos declarados
registrar diferencias del modelo y origen humano sin atribuirlas a comprensión del modelo
esperar aprobación final separada y volver a comprobar cancelación/revisión
persistir una sola vez por operación; ante error conservar estado pendiente y recibo
```

Aceptación mínima del reemplazo: A sin asistentes inventados; B tarea de Diego bajo tareas y Laura no asistente; otro dominio con hecho mencionado que no implique rol; rechazo de citas válidas con roles no confirmados; edición/cancelación/revocación entre propuesta y confirmación; reintentos idempotentes; modelo indisponible sin guardado; baseline general y CRUD conservados; E2E con cuenta/conexión real y versiones congeladas. La propuesta inicial se evalúa por separado de las correcciones humanas; un resultado final correcto no certifica comprensión autónoma. No ejecutar estas campañas sin nuevo presupuesto explícito.

## Persistencia y decisión de producto pendiente

El usuario eligió **revisar hechos y guardar el documento en el historial**. El propietario autorizó implementar ese destino durable, conservando la ejecución general separada. Las opciones siguientes conservan el análisis previo; el registro por host fue descartado.

Se descartó un registro efímero nuevo por host: cambiar de instancia, reiniciar o perder afinidad rompería revisión, cancelación y aprobación. No es un diseño comercial multiinstancia. Un token firmado conserva integridad pero no registra de forma compartida cancelación, revocación, confirmación y aprobación final en orden. No sustituye el agregado autoritativo.

### Infraestructura reutilizable y fronteras que no reutilizar como bolsa de estados

| Owner actual | Reutilización correcta | Frontera a preservar |
|---|---|---|
| `ExecutionQueue`, `AccountModelProposals`, `PairedModelTransport` | Admisión/cuotas, operación idempotente, trabajo dirigido, plazo, cancelación y resultado estructurado del companion existente. | No implementar otra cola, llamar Ollama desde Cloud ni sumar otro planificador/juez. Una operación de propuesta puede usar la reparación estructural acotada del SDK actual; medir llamadas reales, no prometer una inferencia por contar una operación. |
| `ModelProposalResult` / `proposal_results` | Referenciar la salida original inmutable y su ejecución como evidencia de lo propuesto por el modelo. | Es inferencia privada **sin autoridad de efecto**. No sobrescribir `Payload` con hechos confirmados ni mezclar estados de aprobación dentro de ese paquete. |
| `AccountExecutionResult` / `execution_results` | Mantener instrucción compilada y respuesta libre separadas, con identidad/revisión y uso verificable. | `ExecutionQueue.Complete` valida el texto contra el compilador y la respuesta contra el recibo del modelo. No presentar un acta humana como respuesta original ni guardar otro dominio bajo `CompilePrompt`. |
| `AccountAgentMemory` / `agent_contexts` | Reutilizar infraestructura de transacción de actor y su patrón de aislamiento, sin apropiarse de sus entidades. | Sus sesiones describen borradores de prompts; al recuperar fuerzan `Approval=NOT_GRANTED` y borran acciones pendientes. No son almacén de decisiones de una transformación factual ni de documentos aprobados. |
| `AccountLiveRunStore` / `agent_runs` | Referencia para límites/recibos y aprobación ligada a versión. | El run visible está ligado a host, superficie SAME_TAB y commit de prompt. No introducir actas en `LiveApproval(SAVE)` ni tratar una recuperación interrumpida como control restaurado. |
| `Dialog`, `PromptVariableFields`, `SavedText`, `useAccountOperation` | Presentación, valores explícitos, copia/exportación y descarte de respuestas tardías. | La página no decide qué campos son notas ni qué persona asistió. Selección de fuente y revisión son controles explícitos; el servidor conserva identidad, versión y estado. |

### Mínimo común para varias instancias

Incluso la opción de sólo exportar necesita un agregado de revisión compartido si promete invalidación y aprobación ordenadas. Propuesta de nueva tabla acotada `qp_operations.factual_reviews`, todavía sin migración implementada:

- Identidad: ID generado por servidor, `OwnerId`, `LibraryId`, `PromptId`, revisión capturada y `ConnectionId`; `OperationId` único por dueño y fingerprint del encargo inicial para idempotencia.
- Fuente tipada/versionada: plantilla y valores capturados por servidor, instrucción compilada, **nombres de campos de fuente seleccionados explícitamente**. No inferir campos por nombre o intención. Limitar tamaños antes de cargar cuota; rechazar en vez de truncar.
- Referencia de propuesta: operación hija estable e ID de ejecución `ModelProposal`. El paquete actual sigue perteneciendo a la cola; no añadirlo como una bolsa genérica de workflow. La propuesta original se conserva o se referencia como dato inmutable hasta la expiración definida.
- Estado: `ADMITTED → PROPOSING → REVIEW → COMPOSED → APPROVED`, con terminales `CANCELLED`, `INVALIDATED`, `EXPIRED`, `FAILED`. `Version` monotónica para comparar y cambiar estado. Fechas del reloj de PostgreSQL, nunca reloj del navegador.
- Campos propios: hechos/roles propuestos, hechos/roles confirmados, diferencia de revisión, disposición documental explícita, preview determinista y comprobante de aprobación final. JSON tipado/versionado para listas de hechos, no mensajes arbitrarios ni planes ejecutables.
- Seguridad: FK compuestas dueño/biblioteca/origen, FORCE RLS y políticas por actor. El rol de la aplicación recibe únicamente el acceso de esta capacidad; el worker conserva sus privilegios actuales de cola, sin lectura de documentos finales. Ningún scope MCP, perfil de conexión ni permiso del sistema operativo se amplía.
- Retención: revisión pendiente con plazo acotado configurado por operador; cancelación/expiración retiran controles y contenido según la política aprobada. El child de inferencia tiene actualmente su propia retención de siete días; **no prometer borrado total a los quince minutos** sin conectar explícitamente su olvido. Añadir el agregado al borrado privado/retención, backup/restore y reconciliación de borrados antes del cierre.

La comprobación vigente de conexión/sello y la transición de aprobación deben ordenarse con el mismo cerco de revocación de la cola. Las transiciones del agregado requieren bloqueo de fila o CAS por `Version`; una carrera devuelve conflicto sin aprobación tardía. Revalidar fuente actual antes de confirmar y antes de aprobar. El historial de un documento ya aprobado pertenece al dueño y biblioteca; revocar el equipo invalida propuestas pendientes, no concede al equipo acceso al historial.

### Admisión recuperable sin otra cola

No acoplar dos conexiones PostgreSQL fingiendo una sola transacción. Puede reutilizarse la admisión idempotente existente:

```text
POST iniciar con OperationId explícito
  crear o leer revisión compartida por dueño+OperationId; validar fingerprint
  capturar fuente del servidor y operación hija estable una sola vez
  admitir ModelProposal mediante su puerto existente, con esa operación estable
  enlazar ID hijo usando CAS; si ya está enlazado, devolver el mismo

si el proceso cae entre commits:
  GET informa admisión incompleta, sin inventar finalización
  reanudar admisión por acción explícita del usuario con el mismo OperationId
  Submit devuelve el recibo existente si ya fue admitido; jamás otra muestra
  si la fuente/grant caducó, invalidar revisión y cancelar sólo su hijo pendiente

al confirmar:
  exigir hijo terminado y respuesta original válida
  revalidar dueño, biblioteca, origen/revisión, grant, expiry y Version
  conservar propuesta original y diferencia de hechos/roles confirmados
  componer literalmente; pasar a COMPOSED, sin llamar al modelo

al aprobar:
  comprobar nuevamente vigencia y Version
  aprobar exactamente el preview conservado por el servidor
  aplicar el destino elegido por producto; no aceptar cuerpo/hash cliente como autoridad
```

Requiere exponer al puerto de propuesta la misma validación de fuente bajo el cerco que ya usa la admisión de compilación; no duplicar su lógica de cuotas. El diseño debe probar caídas antes y después de admitir al hijo, respuestas tardías, doble confirmación y dos hosts. Un problema de entrega del mismo recibo no autoriza repetir inferencia.

### Diferencia entre destinos

| Aspecto | Sólo exportar tras aprobar | Historial privado durable |
|---|---|---|
| UI | Revisión, confirmación, preview y aprobación; después copiar/descargar documento. Explicar que debe exportarse antes de caducar. | Lo anterior, más lista de documentos, apertura, exportación y eliminación propia. No añadir sharing o venta implícitamente. |
| Estado común | `factual_reviews` compartido y acotado. No requiere afinidad de host; reinicio no restaura ninguna acción sin revalidación. | El mismo agregado para el trabajo pendiente. |
| Persistencia final | No nueva biblioteca de documentos; `APPROVED` permite obtener sólo ese documento mientras siga disponible bajo el plazo aprobado. | Nueva entidad `factual_documents`: documento aprobado inmutable, hechos/roles y procedencia/correcciones confirmadas, dueño/biblioteca/origen, fecha y `ReviewId` único. Insertar y aprobar en **una** transacción. |
| Idempotencia | Repetir aprobación de la misma versión devuelve el mismo documento; no ejecuta otro modelo ni escribe un prompt. | `ReviewId` único y transacción evitan dos documentos al repetir el POST; devolver el mismo ID y recibo. |
| Seguridad | Migración/RLS de revisión, límites y caducidad. | Añade RLS, índices de historial y política de borrado/retención de documentos. |
| Operación | Integrar limpieza y olvido de hijo con límites; recovery de revisión compartida y prueba multiinstancia. | Añade inclusión en backup/restore, borrado privado y reconciliación; conservación no puede depender de que siga disponible el raw de inferencia de siete días. |
| Pérdida de alcance | Cerrar/expirar sin exportar pierde el artefacto final según política visible; no fingir historial. | Requiere política explícita para conservar/borrar documentos y su procedencia; no simularla guardando como prompt. |

En ambos destinos hay **migración y acceso de base de datos nuevos**, aunque no se amplíen permisos delegados externos. Se debe avisar antes de implementarlos. Sin esta decisión de producto no hay un parche mínimo honesto que cierre transformación factual comercial; mantener generación general revisable sigue siendo una opción distinta y no resuelve el incidente de exactitud autónoma.

## Candidato implementado: PostgreSQL probado, proveedor y navegador pendientes

La decisión durable se implementó en `Application/FactualTransform.cs`, `Inference/FactualProposalGenerator.cs`, `Accounts/Executions/AccountFactualReviews*.cs`, `FactualRecords.cs`, `FactualContracts.cs` y `Cloud/FactualReviewEndpoints.cs`. La composición de Cloud registra la capacidad sólo cuando delegación está habilitada. No se creó otro coordinador ni un diccionario de estado por host.

Migración nueva `20261028203013_PrivateFactualDocuments`: tablas `qp_operations.factual_reviews` y `qp_library.factual_documents`, ambas FORCE RLS; FK compuestas por dueño/biblioteca para origen, hijo y documento/revisión; versión de concurrencia, timestamps de PostgreSQL y documento aprobado único por revisión. El worker no recibe lectura de documentos. La inserción documental comprueba el preview y los hechos de una revisión COMPOSED. La aprobación y la inserción se confirman juntas; repetir la operación devuelve el mismo documento. No hay downgrade destructivo de documentos: requiere backup y aplicación coincidentes.

La fuente elegida se limita a variables reales y valores aportados explícitamente. No se infieren campos por nombres ni se ejecutan instrucciones incluidas en su contenido. Rechazo visible si la fuente serializada supera 4000 bytes UTF-8, el paquete completo 8000 o la instantánea 128000; Qwen conserva contexto 12288 y salida 2600. Son márgenes conservadores de esta capacidad, **no un contador universal exacto de tokens**. No se trunca. Una propuesta vacía llega a REVIEW como «sin hechos propuestos»; no permite confirmar un documento vacío. El usuario puede añadir hechos o corregir roles y reconfirmarlos antes de aprobar; cada composición nueva cambia la versión e invalida decisiones sobre el preview anterior.

El documento durable conserva sólo fuentes seleccionadas originales, hechos confirmados, hechos propuestos, indicador de cambios, versión de origen y modelo. No copia plantilla completa ni valores no seleccionados. La revisión se limpia al aprobar; el documento posee la procedencia aprobada. Que la cita exista no prueba su clasificación y que el documento final sea correcto no certifica comprensión autónoma.

Editar/archivar el origen o revocar el equipo invalida sólo trabajo pendiente. El historial aprobado permanece. Borrar permanentemente el prompt **purga también documentos derivados** y contenido de sus revisiones/propuestas hijas: la UI debe avisarlo antes del borrado. Eliminar un documento borra ese documento, su copia de revisión y su único hijo de inferencia, en una transacción limitada al dueño; no elimina el prompt ni otras ejecuciones. El hijo de una revisión no olvidada mantiene la retención independiente de siete días. La cola ejecuta limpieza acotada de revisiones vencidas (100 por pasada) y solicita cancelar sus hijos, incluidos recibos admitidos antes de una caída y todavía no enlazados.

Pruebas ejecutadas: **18/18 aprobadas, cero omitidas**, en PostgreSQL nativo 18.6, base QA aislada `quickprompt_development`: siete de componente y once HTTP/PG. Incluyen A/B con correcciones sintéticas explícitas, otro dominio de inventario, cita existente sin autoridad, propuesta vacía, tamaños, escape, aprobación simultánea en dos hosts, CAS, actor ajeno/FORCE RLS, prohibición de lectura documental al worker, cancelación/edición/revocación con hijo Running y COMPOSED, expiración, borrado documental/origen y recuperación de la ventana de admisión incompleta sin repetir propuesta. **No son aceptación humana ni campaña del modelo.** Recibo final `factual-documents-postgres-20261028/factual-postgres-04.trx` en el directorio de evidencia local.

Se conserva `factual-postgres-01.trx`: 13/14 aprobadas y un fallo del arnés por iniciar el segundo cliente sin obtener antes CSRF. Se corrigió sólo ese setup; el segundo intento aprobó 14/14. Los negativos activos ampliados aprobaron 17/17 y finalmente 18/18 tras añadir la ventana de caída simulada. No hubo ninguna inferencia real en estas campañas. La revisión de procedencia distingue propuestas de IA sin fuente de hechos confirmados en revisión; ausencia de cita no atribuye autoría humana.

Se migró además la base QA vacía e independiente `quickprompt_accounts_local`, mediante EF Release sin build ni Docker y credencial existente sólo en memoria. Comprobación real: **22 tablas RLS y 22 FORCE RLS**, dos tablas nuevas incluidas; última migración `20261028203013_PrivateFactualDocuments`; EF no detecta cambios de modelo pendientes. No se modificó una base humana ni producción. La nueva campaña con Qwen y el flujo en navegador siguen pendientes; no se declara certificación comercial con estos componentes.

## Correcciones posteriores y congelación final

La campaña nativa nueva, declarada por raíz con máximo de tres llamadas y cero reintentos semánticos, usó dos:

1. **A llegó a FAILED antes de revisión.** La respuesta de Qwen era JSON tipado válido con cuatro hechos. Una cita contenía `Luis revisarán las pruebas el 30 de octubre de 2026.` y la fuente decía `Luis revisará las pruebas el 30 de octubre de 2026.`. `FactualProposalGenerator.Read` reutilizaba el guard de procedencia de confirmación y rechazó toda la propuesta editable. La primera divergencia y bytes exactos están en `factual-durable-20261028/original-A-codec-diagnosis.json` y `original-A-codec-packet-utf8.json`. La captura base64 confirmó ausencia de caracteres de reemplazo en la respuesta: una visualización de consola no demostraba corrupción del proveedor.
2. **B alcanzó REVIEW con nueve hechos.** El resultado crudo clasifica tareas futuras como `completedWork` y separa datos que requieren revisión; no acredita fidelidad semántica autónoma. Se conserva en `control-B-codec-packet-utf8.json`. Raíz informó corrección explícita a siete hechos, composición/aprobación distintas y guardado/recarga del documento QA; la revisión de Codex no se atribuye como aceptación humana del usuario.

Se separó `ValidateStructure` de `Validate`: la propuesta sólo pasa límites/tipos para poder revisarse; conserva sin normalizar una cita no literal. Confirmar y componer mantienen el guard de procedencia y las dos decisiones explícitas. Una regresión reproduce exactamente el contenido estructurado de A, muestra la cita errónea, impide confirmarla, la corrige explícitamente y guarda sólo tras aprobación final, **sin otra inferencia**. No se volvió a llamar A ni B.

La auditoría independiente detectó dos defectos adicionales, reproducidos en PostgreSQL real antes de corregirse:

- JSONB modifica formato/orden de propiedades. Comparar sus strings con JSON compacto marcaba `Edited=true` sin cambios y producía 409 al repetir la confirmación. Ahora se comparan los DTOs con `SequenceEqual`, conservando orden y valores literales; la estructura se valida antes para que `facts:null` no se convierta en 500. Una composición nueva sigue invalidando la aprobación de la versión anterior.
- Un rechazo de cuota anterior a admitir al hijo dejaba ADMITTED sin recibo recuperable para el cliente y bloqueaba otro encargo. Sólo `ExecutionQuotaException` conocida y sin hijo cierra ahora FAILED con recibo terminal y código `ADMISSION_*`; repetir el mismo OperationId devuelve ese mismo fallo y no recaptura ni reinfiere. Un intento de capacidad nuevo requiere una operación explícita distinta. Los commits inciertos conservan ADMITTED y reconciliación de la operación hija existente. No se añadieron retries ni se relajaron cuotas.

Los dos fallos están preservados en `factual-documents-postgres-20261028/factual-additional-diagnosis-01.trx`.

Se confirmó asimismo que los privilegios por defecto del bootstrap daban CRUD a tablas nuevas: GRANT de un subconjunto no eliminaba UPDATE de documentos ni DELETE de revisiones. La nueva migración forward **`20261028211701_FactualLeastPrivilege`** revoca primero todos los privilegios de la aplicación en esas dos tablas y concede únicamente SELECT/INSERT/UPDATE de revisiones y SELECT/INSERT/DELETE de documentos. No se reescribió la migración ya aplicada. Se aplicó a ambas bases QA sin reset; consultas de privilegios y pruebas SQL reales verifican denegación 42501. Los hashes de A fallida, revisión B y documento B permanecieron iguales. Recibo: `factual-durable-20261028/forward-qa-correction.json`.

**Resultado final ejecutado: 22/22 pruebas aprobadas, cero omitidas, PostgreSQL 18.6 real, cero inferencias del agente de backend.** Recibo exacto: `.playwright/saas-delivery-20261026/factual-documents-postgres-20261028/factual-postgres-05.trx`. Son ocho comprobaciones de componente y catorce casos HTTP/PG. Incluyen citas no literales revisables y bloqueadas al confirmar, procedencia sin cambios/cambios y replay idempotente, nulos, permisos efectivos, cuotas y las capacidades anteriores. El historial sintético conserva al menos un documento aprobado con propuesta/fuentes para ensayos de restore no vacíos.

Cloud Release final: SHA-256 `5B99CB32134365A51F3F79BA79E9C05D0C208DB0233CB92C9F14B91876D51197`. La migración forward sólo cambia permisos; no cambia entidades del modelo. Se conserva la comprobación EF anterior sin cambios pendientes; no se ejecutó otra después del cierre del turno.

Quedan pendientes la **única llamada de inventario restante**, navegador sobre el snapshot final, aceptación humana independiente, CI ampliada/restore y publicación. Raíz detuvo Computer Use porque el control no pudo determinar con confianza la URL del navegador para aplicar su política. No se sustituyó por otro navegador ni se intentó evadirlo. No se declara certificación comercial, no se ejecuta esa llamada y no se publica esta versión en este cierre. La fuente de producto quedó congelada; sólo se completaron este documento, manifiesto y recibos.
