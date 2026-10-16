# Calidad IA local — GOAL 02

Resultado diferenciado: **contratos 18/18 PASS**, **controles automáticos 18/18 PASS**, **revisión semántica manual 17/18 PASS**. El caso restante es creación con Qwen2.5. No se convierte la conformidad JSON en exactitud semántica ni se elimina el fallo.

Se mantienen qwen3:8b principal y qwen2.5:7b-instruct como alternativa explícita; no hay fallback silencioso. Configuración muestra las limitaciones factuales observadas de la alternativa. Los borradores siempre se revisan, editar/guardar exige acción y mejorar aplica con aceptación y versiones.

## Dataset y autorización

`tests/fixtures/ai-quality-v2.json` contiene nuevos prompts de trabajo de GOAL 02: revisar cambios del proyecto, conservar restricciones, proponer pruebas y analizar evidencia del SDK. Son casos de uso reales del trabajo autorizado sobre este repositorio, escritos para esta evaluación. No son datos extraídos de una biblioteca privada. El usuario eligió continuar sin una copia de datos privados; esa migración queda bloqueada.

Hash v2 antes/después: **887154F3405283AF6037D21C5A08CE0C918AD4D73881F00BBCA62CB5C65657FB**. Se conserva v1 sin cambios. Criterios v2: contratos tipados, checks deterministas de instrucciones, preguntas factuales con fuente y límite de palabras, cancelación real de streaming y recuperación. Los checks son indicadores limitados, complementados por lectura humana; no se usa otro LLM como juez ni se reintenta hasta obtener PASS.

## Hallazgos y corrección

La primera corrida aprobó 18 contratos y 18 checks, pero falló la revisión humana de ambos borradores de creación: Qwen3 cambió una prohibición absoluta por permiso condicional, y Qwen2.5 creó una metaplantilla con <riesgos> como entrada. [Resultados iniciales](evidence/goal02/ai-quality-v2-initial.json), [revisión inicial](evidence/goal02/ai-quality-manual-initial.json).

Se precisó únicamente la instrucción de **crear**: dirigir la plantilla al asistente que realizará la tarea, preservar prohibiciones sin debilitarlas, respetar las variables nombradas y no convertir salidas solicitadas en entradas. El dataset y los criterios permanecieron iguales.

| Operación | Qwen3 | Qwen2.5 | Revisión |
|---|---|---|---|
| Mejorar y preservar instrucciones/variables | PASS | PASS | Conserva prohibición de modificar código, español, tres riesgos y ausencia de nuevas dependencias |
| Crear plantilla de revisión | PASS | **FAIL** | Qwen3 conserva las dos variables y prohibición. Qwen2.5 conserva las variables en la última corrida, pero omite la prohibición de modificar código |
| Sugerir valor con contexto autorizado | PASS limitado | PASS | Valores plausibles; Qwen3 resume parcialmente y omite parte del contexto. Ninguna sugerencia se aplica sola |
| Analizar sin reescribir | PASS | PASS | Texto original y observaciones separadas |
| Tres respuestas factuales acotadas | 3/3 PASS | 3/3 PASS | SQLite binario, C# de InteractiveServer en servidor y ausencia de validación net10 |
| Cancelar tras recibir contenido | PASS | PASS | Cancelación efectiva; no resultado completo devuelto |
| Nueva consulta tras cancelar | PASS | PASS | Respuesta RECUPERADO observada |

[Salida final completa](evidence/goal02/ai-quality-v2.json), [revisión final](evidence/goal02/ai-quality-manual-final.json). La corrida posterior al ajuste de creación terminó con código 1 al omitir Qwen2.5 una variable; se conserva en ai-quality-before-analysis-fix.json. Tras corregir por separado el análisis truncado, la última corrida termina con código 0 en los checks automáticos, pero la revisión manual sigue marcando FAIL por omisión de una prohibición. Un código 0 del harness no sustituye esa revisión.

Las tres preguntas factuales están explícitamente acotadas y algunas ofrecen alternativas. Sus 6/6 respuestas correctas **no invalidan** las 7/10 respuestas incorrectas de Qwen2.5 en el fixture v1 del MVP ni demuestran exactitud general. Fuentes: [formato SQLite](https://sqlite.org/fileformat.html), [InteractiveServer](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-8.0), evidencia local `dotnet --list-sdks`.

La [regresión actual del fixture v1 sin modificar](evidence/goal02/ai-evaluation-v1-regression.json) obtuvo **29/30**: Qwen3 15/15 y Qwen2.5 14/15. El borrador alternativo convirtió «80 palabras» en un marcador adicional, sin declararlo entre las variables; el adaptador lo rechazó correctamente. No se corrigió silenciosamente ni se repitió el caso para fabricar un PASS. Este fallo de contrato sigue abierto como limitación del modelo alternativo.

## Latencia y fallos

Medición real secuencial del adaptador local, sin consultas de IA del navegador en paralelo, contexto 8192, think=false, temperatura 0.2. Solo tres generaciones factuales por modelo: percentiles nearest-rank descriptivos, no SLA ni comparativa estadísticamente suficiente.

| Modelo | n | p50 | p95 |
|---|---:|---:|---:|
| qwen3:8b | 3 | 1913 ms | 1953 ms |
| qwen2.5:7b-instruct | 3 | 1397 ms | 1745 ms |

Los tiempos por operación, carga de modelo, cancelación y recuperación están en el JSON. Las corridas inicial/final se preservan; el fallo semántico no se cuenta como fallo de transporte. Contratos nuevos también reproducen cinco casos de JSON válido con tipos incorrectos y comprueban que la biblioteca sigue recuperable. El E2E existente comprueba no guardar respuestas parciales, revisión explícita de propuestas y persistencia tras reinicio.

Estas métricas v2 se midieron antes del último ajuste de concisión de la instrucción de ejecución. Después se ejecutó nuevamente el navegador integral sobre el código final: [PASS, Qwen3 real](evidence/goal02/browser-ai.json), ejecución observada de 24337 ms y recuperación tras reiniciar. Ese tiempo es una observación distinta, no reemplaza los percentiles de las preguntas acotadas. La consulta abierta truncada previa se conserva en [evidencia](evidence/goal02/execution-truncated-and-input-before.png); sigue siendo posible agotar la salida, cuyo parcial no se acepta como respuesta completa.

## Decisión de uso

Qwen3 es adecuado como asistente local **supervisado** para el alcance probado. Ningún resultado tiene autorización para ejecutar herramientas o alterar datos. Qwen2.5 permanece utilizable bajo revisión y con su fallo registrado; no se declara equivalente en calidad ni aprobado para aplicar instrucciones sin revisión. Falta evaluar mayor diversidad, repetición, tareas del usuario y datos reales autorizados antes de afirmar fiabilidad general.

Reproducir, sin instalar ni descargar:

```powershell
dotnet run --project src/QuickPrompt.Evaluation --no-restore -- --quality tests/fixtures/ai-quality-v2.json docs/evidence/goal02-rerun/ai-quality-v2.json
```

Usar otra ruta de salida para conservar la evidencia histórica. El código de salida puede ser 1 si falla un check automático. Aun con código 0, la revisión semántica manual puede fallar; no modificar fixtures o criterios para ocultarlo.
