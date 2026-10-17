# NOVA — aceptación semántica y contextual

**Estado: puertas de aceptación aprobadas el 17 de octubre de 2026.** El resultado corresponde a esta muestra y al modelo instalado; permanecen cuatro errores de interpretación medidos.

## Qué cambió

Las peticiones naturales pasan por Qwen local antes de seleccionar acciones. El intérprete separa lectura, adaptación, creación, datos, preparación, ejecución y revisión de guardado. Un plan tipado añade dependencias y el dominio comprueba sus permisos, IDs y revisiones. Las acciones explícitas de los botones conservan sus rutas directas.

La selección de la biblioteca y el borrador anterior son contextos distintos. También se sincronizan los cambios manuales del editor y la recuperación de un borrador, incluidos los que todavía no tienen un ID guardado.

## Cómo utilizarlo

1. Escribe lo que necesitas en «Cuéntame qué quieres preparar…». Puedes pedir varias acciones en la misma petición.
2. Revisa «Lo que entendió NOVA». La lista muestra las operaciones del plan validado.
3. Si falta identificar el origen, elige uno de los candidatos o precisa el título. Los campos vacíos de una plantilla se completan cuando quieras utilizarla.
4. Una adaptación abre una propuesta en el editor; el original mantiene su revisión. **Guardar versión** es tu acción explícita para reemplazar su contenido conservando el historial. **Cancelar** permite salir conservando el borrador durante la sesión.
5. Pedir que quede listo para Qwen prepara el texto. La inferencia final necesita una petición explícita de ejecución o el control correspondiente.
6. En «Navegador visible · Edge» puedes observar las acciones reales sobre prompts guardados. Una propuesta se detiene para revisión; puedes cancelarla o confirmar su guardado. Los borradores completamente nuevos se preparan en el lienzo.

## Evidencia y criterio

- Baseline reproducido con el binario anterior y una biblioteca sintética: búsqueda compuesta que perdía la adaptación y referencias conversacionales no resueltas. [Auditoría](SEMANTIC_INTENT_AUDIT.md).
- Dataset congelado: `Tests/evaluation/semantic-v1.json`, 60 casos, SHA-256 `b3e1b4aa8b2f6e26dce6b32e90f379f9f97cf5e15dc310665b8730d871e1ec3a`.
- Separación: desarrollo 24, validación 20, holdout 16. Umbrales fijados antes de evaluar: exactitud ≥85 %, planes válidos ≥95 %, equivalencia metamórfica ≥90 %, errores críticos cero.
- Los fallos de desarrollo permanecen en evidencia. Una ejecución que no cumple el umbral impide avanzar a validación/holdout; no se cambia el conjunto para lograr PASS.
- Las pruebas con respuestas controladas comprueban contratos y efectos. Se distinguen de la evaluación con Qwen real y de los recorridos reales de Edge.
- La aceptación requiere además la matriz **entrada → contexto → interpretación real → plan → herramientas → resultado visible → validación**, incluyendo cambios efectivos de texto, conservación del origen, aprobación explícita, cancelación y rechazo de propuestas obsoletas.

La comparación de cuatro solicitudes del baseline identifica el defecto; no estima la precisión general del sistema. Las métricas de los conjuntos son observaciones de esta muestra y este modelo instalado, no una garantía de que todas las formulaciones futuras se entenderán correctamente. Los tiempos se miden en esta estación de trabajo compartida, no en un benchmark aislado.

## Reproducción y límites

[Flujo y límites técnicos](SEMANTIC_RUNTIME.md). Ejecuta `scripts/Test-SemanticIntent.ps1 -RealQwen -Headed -Regressions` con las herramientas ya instaladas. Las pruebas usan bibliotecas SQLite propias y conservan los artefactos de las ejecuciones fallidas. No requieren instalar dependencias, descargar modelos ni publicar cambios.

La [revisión sistemática](SEMANTIC_SYSTEM_REVIEW.md) distingue las propiedades del dominio de la calidad probabilística del modelo. El candidato `semantic-compact-contract` supera 196 pruebas de backend, incluida una matriz de 10 800 combinaciones tipadas de capacidades, la composición del contrato de origen y el presupuesto del paquete de corrección. Esa matriz no mide comprensión del lenguaje. Los resultados históricos de otros candidatos permanecen en la auditoría y no se acumulan para aprobar el actual.

[Resumen medido](SEMANTIC_VALIDATION_SUMMARY.json) y [matriz real disponible](SEMANTIC_MATRIX.md). Se regeneran desde `docs/evidence/semantic/acceptance-compact-contract`, sin mezclar binarios ni ejecuciones parciales anteriores. Un resultado NOT_RUN, RUNNING, FAIL o NOT_ACCEPTED mantiene pendiente la aceptación. La puerta final exige los tres conjuntos, las relaciones metamórficas y las 21 comprobaciones del recorrido headed con los mismos binarios y pesos, incluyendo el compilador de dominio.

## Resultado del candidato congelado

| Conjunto | Aciertos | Planes válidos | Errores críticos | p50 / p95 de interpretación |
| --- | --- | --- | --- | --- |
| Desarrollo | 24/24 (100 %) | 24/24 | 0 | 13,5 / 25,4 s |
| Validación | 18/20 (90 %) | 20/20 | 0 | 14,6 / 20,9 s |
| Holdout | 14/16 (87,5 %) | 16/16 | 0 | 15,6 / 32,6 s |

Las ocho comprobaciones relacionales/metamórficas y las 21 comprobaciones headed pasaron. Los hashes de informes y de infraestructura/aplicación/dominio coinciden con la ejecución visible; los tres conjuntos utilizaron el mismo digest de Qwen3:8b. No se cambió código de producción, dataset ni umbrales después de abrir holdout.

Los cuatro desacuerdos permanecen en el informe: reutilización interpretada como lectura en lugar de adaptación (`sem-034`); consejo interpretado como propuesta (`sem-043`); búsqueda compuesta que perdió la propuesta posterior (`sem-048`); y aclaración innecesaria ante un título con instrucciones citadas (`sem-059`). Ninguno concedió ejecución final ni guardado no solicitado. Esto acredita los umbrales acordados, no comprensión universal ni ausencia de fallos lingüísticos. La [revisión sistemática](SEMANTIC_SYSTEM_REVIEW.md) explica sus límites y las responsabilidades del dominio.
