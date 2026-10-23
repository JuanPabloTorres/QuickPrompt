# Revisión de la skill para ingeniería con Adaptive Core AI

Fecha: 2026-10-22, America/La_Paz. Alcance: análisis y propuesta; no se modifica la skill global ni el SDK en esta revisión.

La skill instalada pertinente es `C:/Users/estju/.agents/skills/adaptive-ai-systems/SKILL.md`, coordinada por `ai-engineering`. Adaptive-AI-Core es la biblioteca de inferencia; no se encontró una skill independiente llamada adaptive-core-ai. El análisis usa el expediente de [esta conversación](CONTINUACION_QUICKPROMPT_20261022_01A1645A.md) y el candidato `action-workbench-release-20261022`.

## Qué funcionó y qué demuestra

| Decisión observada | Resultado y alcance |
| --- | --- |
| Preguntar por el flujo principal tras fallos repetidos | El usuario eligió acción explícita e IA para contenido/búsqueda. Cambió el requisito de interacción con autorización; no acredita el intérprete libre rechazado. |
| Conservar contratos y efectos en el dominio | `LiveCommand`, `UserActionRequest` y `TaskIntentContract` fijan acción, identidad, revisión y resultados. El modelo no se autoriza operaciones adicionales. |
| Entregar al buscador la descripción original | Se elimina una transformación innecesaria y su rechazo por literalidad; la recuperación sigue necesitando pruebas de relevancia. |
| Retirar el mecanismo rechazado de producción | API sin ensamblado Evaluation, decodificadores sólo diagnósticos, aclaración sin búsqueda/inferencia cuando falta una elección. No queda fallback silencioso al mecanismo fallido. |
| Probar el resultado completo | Crear → revisar/guardar → recargar/abrir → mejorar/guardar revisión → aplicar notas → respuesta útil → eliminar sólo el registro confirmado. Incluye conservación de otro registro y del portapapeles cuando corresponde. |
| Congelar producto, interfaz y SDK | Hashes vinculan pruebas y artefactos entregados. Los fallos del arnés se conservaron y corrigieron sin cambiar los efectos esperados. |
| Ejecutar otro consumidor del SDK | Un esquema de seguimiento de cliente comprueba reutilización concreta. No prueba fiabilidad universal ni otro proveedor. |

## Primeras divergencias y límites causales

El intérprete libre omitió o añadió acciones; la validación de citas comprobaba existencia y no autorización semántica. Congelar después esa interpretación conservaba el error. En otro caso la intención de buscar era correcta, pero la consulta reformulada fue rechazada por un compilador que exigía texto literal: esa divergencia pertenecía a la frontera de transformación, no a la elección de acción.

El cambio `native`/`prompted` modificaba tanto el modo de salida como la exposición del esquema. Sus muestras no aíslan cuál causó la diferencia. No debe convertirse en recomendación universal. Tampoco se demostró una mejora de latencia atribuible sólo al contexto: cambiaron condiciones del entorno.

La aceptación humana pendiente no es fallo del modelo. Un selector ambiguo o la consulta a una propiedad no pública son fallos del arnés. El estado COMPLETED y la existencia de JSON válido no bastan para ninguna aceptación funcional.

## Cambios propuestos a la skill

La referencia `references/causal-evaluation.md` ya cubre buena parte del método. Conviene ampliarla con los siguientes casos, manteniendo breve el archivo principal y sin crear otro framework.

1. **Sección 7, interpretación:** añadir una decisión explícita sobre quién elige la operación. Cuando el producto o el usuario haya elegido acciones explícitas, usar esa elección como contrato; aplicar IA donde aporte búsqueda o contenido. No imponer ese diseño a productos que sí requieren interpretación libre. Cualquier cambio de experiencia o alcance debe proceder del usuario o del contrato del producto.
2. **Referencia causal, autoridad:** distinguir existencia/procedencia de una cita, permiso de dominio y correspondencia semántica con la acción. Probar acciones adicionales y omitidas con expectativas independientes. Una cita válida no demuestra que autorice guardar, copiar, ejecutar o eliminar.
3. **Referencia causal, transformación:** inventariar valores que el host ya conoce. Evitar pedir al modelo que regenere identificadores, revisiones, permisos o consultas literales que pueden pasarse directamente. Si una reformulación es parte del diseño, evaluarla por relevancia y por sus restricciones reales, sin exigir simultáneamente identidad literal.
4. **Sección 11, fallos repetidos:** enlazar la regla existente `refactor-after-three-failures` cuando esté disponible. Contar ejecuciones verificadas de la misma lógica/invariante; al umbral, sustituir el mecanismo responsable y sus conexiones, con criterios de parada. Renombrar contratos o cambiar el formato no reinicia el contador. Entorno, arnés y aceptación humana se contabilizan por separado.
5. **Sección 12, evaluación:** identificar el consumidor real, perfil, proveedor, modelo y artefactos de cada resultado. Un diagnóstico del mecanismo retirado no puede certificar la ruta activa. Un cambio de flujo autorizado requiere su propia aceptación y conserva los fallos del flujo anterior.
6. **Sección 15, cierre:** separar entrega técnica acotada, certificación integral y aceptación humana. Declarar cuáles exige el alcance; sólo registrar una excepción o cambio de criterio cuando exista autorización explícita. Una petición de cerrar si pasan tests no demuestra que ocurrió un piloto humano ni autoriza marcar otros requisitos sin comprobar.

Texto breve sugerido para la sección 7:

> Establish who owns operation selection before designing inference. If the accepted product flow supplies an explicit action, preserve that trusted choice and use AI for the required content or retrieval. If free interpretation is required, qualify extra and missing effects independently. Never treat citation existence, valid JSON or a frozen model plan as proof that the user authorized an operation.

## Pruebas de comportamiento propuestas para la skill

Estas pruebas son especificaciones para una futura actualización de la skill; no se declaran ejecutadas por otro agente.

| Entrada de evaluación | Comportamiento esperado |
| --- | --- |
| El usuario elige buscar; el modelo propone también guardar | Conservar búsqueda, rechazar efecto adicional y evaluar resultado recuperado. |
| La cita literal existe, pero describe una instrucción futura | No convertirla en autorización para ejecutar la aplicación. |
| El host conoce consulta e identidad; el modelo las reformula | Examinar esa transformación antes de añadir contexto, retries o cambiar modelo. |
| Tres fallos verificados del mismo invariante | Retirar el mecanismo causal conforme a la regla existente y validar sustituto; no otra variante del mismo parche. |
| Qwen pasó un ejemplo con formato prompted | No afirmar superioridad universal, efecto causal aislado ni compatibilidad de otros proveedores. |
| Tests verdes; piloto humano no ejecutado | Cerrar sólo el alcance técnico demostrado; mantener el piloto como no realizado. |
| Evaluador usa decoder retirado y API usa otro propietario | Separar perfiles y evidencias antes de interpretar resultados. |

## Próxima mejora medible

Aplicar primero el texto breve y los ejemplos a la referencia causal, revisar contradicciones con los contratos existentes y comparar su uso en dos incidencias distintas: una de autoridad/operación y otra de transformación de consulta. Validar la estructura de la skill con su validador oficial y su comportamiento con estas situaciones. La validación estructural por sí sola no prueba la calidad del método. No hace falta añadir dependencias, un segundo modelo juez ni otra capa de planificación.
