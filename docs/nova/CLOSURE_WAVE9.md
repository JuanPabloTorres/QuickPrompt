# Cierre — contexto del workflow real en el revisor de contenido

## Resultado conservado: RECHAZADO Y RETIRADO

La prueba real `latency-1792495995520` volvió a fallar. El revisor exigió controles
de guardado dentro del contenido y llegó a citar una descripción de capacidad
ausente del cuerpo propuesto. Las 38 pruebas de contrato no demostraron fidelidad.
Se retiraron DraftReviewWorkflow y su contexto de producción; las fuentes y la
traza del experimento se conservan. Las 37 comprobaciones dirigidas posteriores
a la retirada pasaron. La descripción siguiente es la propuesta histórica,
no una funcionalidad activa ni un bloque cerrado.

2026-10-20. La traza `latency-1792495675500` ya mantiene revisión y copia.
La primera propuesta añade controles de guardar/aprobar al cuerpo y se rechaza
correctamente. La segunda es el correo de entrevista con todos los invariantes
del origen, pero el revisor exige aprobación/guardar/original en el cuerpo.
Su paquete sólo contenía petición original y textos, sin el workflow real que
ya gobierna esas acciones. No se rebaja el criterio ni se elimina autoridad.

Propietarios: PrepareInput.Interpretation ya lleva el plan validado; el editor
ya recibía ApplicationEffects. ReviewDraftFidelity conserva petición original,
fuente y propuesta exactas, y añade un contexto tipado desde ese mismo plan.
SemanticCapabilityCatalog refleja atributos de las operaciones reales; Writes
distingue contenido reutilizable de efectos actuales sin routing por palabras.

```text
conservar el work order original completo y fuente/propuesta exactas
proyectar efectos del plan vigente mediante el catálogo C# reflejado
excluir escritores de ReusableInstruction del contexto de workflow
codificar/validar DraftReviewWorkflow con el helper JSON existente
revisor distingue obligaciones de app de requisitos reales del futuro lector
no aprobar, filtrar issues o descartar restricciones por nombres de operaciones
conservar reparación acotada, evidencia, guardias y aprobación humana existentes
```

Invariantes: una aprobación futura legítima sigue siendo contenido; un plan no
concede permisos ni prueba ejecución. No quitar fragmentos de la petición ni
reincorporar el contrato conjunto rechazado. Sin otro revisor/árbitro/estado,
llamadas adicionales, API pública, migración, modelo o presupuesto nuevo.
Alternativas: eliminar issues por texto sería parche y aceptaría defectos;
reducir autoridad al encargo del generador ocultaría requisitos. Se amplía
solamente el contexto del revisor actual con información que ya existe.
Variante 1, máximo dos con nueva evidencia. Cuatro pruebas de entrada antes
fallan; cubrir con/sin Goal, aprobación actual y aprobación futura legítima,
reparación por grupos y el mismo recorrido Qwen/UI antes de aceptar.
