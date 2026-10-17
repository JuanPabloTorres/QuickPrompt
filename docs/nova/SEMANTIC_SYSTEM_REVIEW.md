# Revisión sistemática de NOVA

Estado: puertas aprobadas el 17 de octubre de 2026 para `semantic-compact-contract`. Este documento explica las responsabilidades actuales; los resultados medidos están en `SEMANTIC_VALIDATION_SUMMARY.json`. No garantiza comprensión de cualquier formulación.

## Dónde quedó el trabajo

El defecto original era estructural: una ruta rápida consumía una solicitud natural como búsqueda antes de interpretar sus demás objetivos. Las rutas web ahora usan `ISemanticIntentInterpreter`; Qwen selecciona objetivos y referencias tipados, y el dominio decide qué dependencias y permisos requieren. React, SQLite y el worker visible existentes se conservan.

Los experimentos anteriores resolvieron fallos de contexto y contratos, pero no completaron la aceptación. Se conservaron resultados parciales y candidatos rechazados. Haber aprobado desarrollo y validación en un candidato anterior no valida los binarios actuales. Tampoco se pueden sumar recorridos parciales de candidatos diferentes para declarar un recorrido completo.

## Responsabilidades y propiedades generales

| Etapa | Autoridad y datos | Propiedad que debe cumplirse |
| --- | --- | --- |
| Entrada React/API | Petición literal, selección explícita y sesión acotada | Los valores de campos y el texto de una plantilla son datos, no permisos. |
| Referencia semántica | Qwen elige un rol de origen; el repositorio aporta entidades reales | Ningún ID inventado ni sustitución de un origen ya vinculado por un resultado de búsqueda. Ambigüedad significa aclaración. |
| Objetivos semánticos | Contratos independientes de contenido, workflow y restricciones | Abrir, editar, crear, llenar, preparar, ejecutar y revisar un guardado son objetivos distintos y combinables. |
| Composición y compilación | Operaciones enumeradas y dependencias del dominio | No clasifica palabras. Una edición depende de resolver su origen; generar depende de validar antes de compilar/ejecutar. |
| Validación del plan | Registro de capacidades, IDs, revisiones, evidencia y presupuesto | Los objetivos no concedidos no pueden aparecer como herramientas ejecutables. Guardar por lenguaje natural solo solicita revisión. |
| Generación | Petición literal y transformación interpretada, separadas | La interpretación orienta el cambio pero no amplía la autoridad del usuario. El cuerpo, no sus metadatos, debe contener el cambio. |
| Integridad de contenido | Variables reales y cláusulas protegidas del origen | El generador no decide si puede eliminar un campo o una restricción. La validación vuelve a comprobar el texto final. |
| Ejecución | Plan validado y estado real del editor/worker | No ejecutar un paso dependiente de una propuesta rechazada. Listo para Qwen no significa ejecutado. |
| Persistencia | Control explícito de aprobación y concurrencia optimista | Proponer no modifica SQLite. Una revisión obsoleta no puede guardarse. |
| Observabilidad | Interpretación, plan, decisiones, herramientas y resultado reales | Un evento determinista del sistema no se presenta como razonamiento del modelo. Un rechazo no se presenta como éxito. |

Estas propiedades se aplican a cualquier categoría de prompt; no dependen de que su contenido sea programación, atención al cliente o investigación.

## Causas comunes encontradas

1. **Pérdida de objetivos por enrutamiento anticipado.** Se eliminó la clasificación web por verbos/regex; todos los objetivos naturales se interpretan antes de ejecutar herramientas.
2. **Confusión entre selección, borrador y origen persistido.** Son identidades separadas. El cuerpo manual más reciente y la revisión canónica se conservan por separado. Leer un borrador no lo reemplaza por su original.
3. **Transferencia accidental de autoridad entre etapas.** Las consultas de búsqueda, resúmenes del modelo y valores aportados no se convierten en nuevas peticiones. Una aclaración conserva una única petición anterior literal.
4. **Contratos que se contradicen.** La reparación local recibe únicamente el contrato rechazado y sus reglas. Una corrección de referencia no contamina el clasificador de contenido. Los reintentos y el tiempo total son finitos; persistir la contradicción retira los efectos.
5. **Pedir al generador que reproduzca invariantes como única defensa.** `TemplateSourceBinding` compone el contrato real del origen en el dominio. Conserva las cláusulas protegidas omitidas y, si faltan campos conocidos, incorpora una sección `Entradas / Inputs` con sus nombres exactos, sin inventar valores. Los campos que ya están en el cuerpo mantienen su posición; los que aparecen en una cláusula retenida no se duplican. Variables ajenas al origen se rechazan. Después se validan longitud, variables, requisitos y cambio real. Quitar campos o restricciones y volver a incorporarlos no constituye una adaptación. El flujo legado conserva su contrato anterior de corrección/rechazo.
6. **Confundir pruebas controladas con comprensión real.** Las respuestas simuladas comprueban permisos y fallos; no estiman exactitud de Qwen. Las métricas del modelo y el recorrido visible se miden por separado y exigen los mismos binarios y pesos.

No se añadieron nuevas listas de frases ni demostraciones al clasificador durante esta revisión. Se congeló el candidato para comprobar el flujo completo antes de considerar más cambios.

La revisión encontró además dos fallos de frontera reproducibles: un JSON no objeto podía escapar como excepción interna, y el reintento del editor recibía sus errores como un JSON escapado mientras `previousDraft` seguía vacío. Se comprueba el tipo de raíz antes de acceder a campos; el editor ahora recibe la corrección como objeto y el candidato rechazado en su campo propio. Esto conserva la petición literal y el límite de un reintento.

El candidato `semantic-systematic-structured` pasó las 17 comprobaciones del lienzo y la pregunta inicial del agente visible, pero fue rechazado cuando Qwen omitió un campo en ambos intentos. No se abrió holdout. Ese fallo motivó mover la composición de campos al dominio, en vez de añadir otra frase al prompt del modelo. El siguiente candidato, `semantic-source-contract`, pasó 195 pruebas de backend, incluidas la matriz de capacidades, composición multilingüe de entradas, rechazo de variables inventadas, no edición disfrazada de conservación, idempotencia, límites de longitud y procedencia visible.

La regla de composición se anuncia como acción de QuickPrompt mediante avisos/eventos propios y la razón de la propuesta del lienzo. No se atribuye al modelo. La comparación de binarios para aceptación incluye ahora infraestructura, aplicación **y dominio**, donde reside el compilador. No se acepta una evidencia que omita ese componente.

El candidato de composición también expuso una duplicación del paquete de corrección: el candidato estaba tanto en `previousDraft` como dentro de `validationFeedback`, junto con instrucciones genéricas y una segunda copia del esquema. Una reconstrucción del paquete fallido contabilizó 8 909 unidades de su cota conservadora frente a 8 192 permitidas; el rechazo sucedió antes de enviar la segunda llamada. `semantic-compact-contract` conserva un solo candidato, todos los fallos y requisitos, y el esquema en `format`. No aumenta el contexto ni trunca datos. La regresión con una plantilla mediana y su candidato pasa dentro del presupuesto original. El candidato actual supera **196 pruebas de backend**.

## Resultado real y errores que permanecen

La ejecución final congelada está en `docs/evidence/semantic/acceptance-compact-contract`: desarrollo 24/24, validación 18/20, holdout 14/16; todos los planes válidos, cero errores críticos, ocho comprobaciones relacionales aprobadas y 21 comprobaciones headed aprobadas. Los hashes de informes, los tres componentes de dominio/aplicación/infraestructura y los pesos del modelo se comprobaron conjuntamente. Holdout se abrió una sola vez en este candidato, tras pasar las puertas anteriores; sus resultados no se usaron para modificar producción.

| Caso | Diferencia respecto del resultado esperado | Límite observado |
| --- | --- | --- |
| `sem-034`, referencia | Abrir/reutilizar en vez de proponer una adaptación a otro proyecto | El modelo no reconoció el cambio implícito de tarea. |
| `sem-043`, consejo | Propuesta de adaptación en vez de explicación | El modelo confundió consulta e instrucción indirecta de edición; no guardó ni ejecutó. |
| `sem-048`, combinación | Búsqueda/lectura sin la propuesta posterior en inglés | El modelo trató el prerrequisito de recuperar el origen como motivo para omitir el objetivo posterior. |
| `sem-059`, título citado | Aclaración en vez de lectura del título indicado | Interpretación excesivamente conservadora de la referencia; las palabras citadas no concedieron ejecución. |

Estos fallos son de interpretación, no se ocultan dentro de los aciertos de contratos. No se añadieron reglas ni demostraciones para sus frases. Cumplir los umbrales no prueba que toda petición compuesta o indirecta vaya a interpretarse correctamente; el plan mostrado y la revisión de propuestas siguen siendo parte del uso del sistema.

## Cobertura y límites de generalización

- `SemanticCapabilityMatrixTests` recorre 10 800 combinaciones de objetivos tipados, presencia de origen y dominios. Comprueba ausencia de escalamiento, IDs vinculados, aclaración sin efectos, validación de borrador y prohibición de herramientas de escritura/SQL. Es una prueba de límites del dominio, **no** 10 800 interpretaciones de lenguaje natural.
- Las pruebas de contexto cubren revisiones, candidatos, valores, edición manual, borradores nuevos, recuperación, cancelación y tarea pendiente.
- Las pruebas de fallo usan API, SQLite y navegador reales con respuestas de modelo controladas. Comprueban esquemas inválidos, IDs inventados, herramientas no permitidas, cancelación, desconexión y recuperación.
- El dataset semántico congelado contiene 60 casos separados en desarrollo/validación/holdout y relaciones metamórficas. La validación histórica ya fue observada durante desarrollo; no es una estimación independiente del rendimiento futuro. Holdout se ejecuta solo tras aprobar las puertas previas y permanece sin modificar.
- El recorrido headed exige 21 comprobaciones sobre propuestas, lectura, llenado, preparación, ejecución, guardado aprobado, selección, aclaración, edición manual, copia, concurrencia y worker visible. Se graban solicitudes/respuestas reales de Qwen sobre una biblioteca sintética.

La comprensión sigue siendo probabilística. El modelo instalado puede solicitar aclaraciones innecesarias o interpretar mal un cambio; los contratos limitan los efectos, pero no demuestran que una adaptación sea óptima para cualquier profesión o idioma. La detección conservadora de cláusulas protegidas del origen reconoce ciertas formas imperativas en español/inglés; no es un analizador universal de restricciones. Los límites de contexto, presupuesto y tiempo se rechazan de forma visible.

## Criterio para continuar sin parches

Ante un fallo: localizar la primera etapa incorrecta en la traza; identificar si pertenece a referencia, autoridad, objetivo, integridad, herramientas o presentación; cambiar solo su responsabilidad; comprobar una familia de contrastes y repetir las puertas con los mismos binarios. No agregar una condición para la frase fallida ni cambiar el dataset/umbral para obtener PASS. Si el modelo no supera una puerta, conservar el fallo y registrar que la aceptación sigue pendiente.

La historia detallada y el baseline permanecen en [SEMANTIC_INTENT_AUDIT.md](SEMANTIC_INTENT_AUDIT.md); el contrato operativo está en [SEMANTIC_RUNTIME.md](SEMANTIC_RUNTIME.md).
