# Evaluaciones NOVA Agentic 2.0

## Protocolo congelado

Dataset `Tests/evaluation/agentic-2-v1.json`: 85 casos sintéticos, 34 desarrollo, 34 validación y 17 holdout; SHA-256 `7A93C7C489942B0F31C5A222E29F13B356884A3F69A0C11649867189ABC132A9`. Cada caso declara intención, herramientas, referencias a fixtures reales, resultado y efectos prohibidos. Las referencias simbólicas se enlazan a IDs creados en una SQLite nueva por caso; nunca a datos personales. Los fallos controlados están separados de inferencia real. No se usa el holdout para ajustes.

El runner ejecuta el orquestador de producción y verifica plantillas, versiones, historial y ejecuciones antes/después. Un JSON válido no prueba tarea completada: se comprueba fuente, variables, estado y cambios reales de texto. No se utiliza a Qwen como juez independiente de sí mismo. La revisión de adecuación se complementa con asserts específicos y los recorridos React sobre Edge real.

## Baseline de desarrollo, antes de optimizar

34/34 casos ejecutados con Qwen3:8b local y fallos sintéticos explícitos: intención 32/34 (94,12%), herramientas 32/34 (94,12%), contratos 34/34, tareas 32/34 (94,12%), Recall@3 1, MRR 0,875, resolución de variables 1, p50 279 ms y p95 33.926 ms. Cero mutaciones de biblioteca y cero IDs desconocidos aceptados; preservación de datos en todos los casos. Los envíos externos no forman parte de las herramientas disponibles.

Las dos adaptaciones devuelven READY pero no cumplen el cambio de texto esperado: se cuentan como fallos. Dos solicitudes anotadas ADAPT se interpretan IMPROVE; se conserva esa discrepancia en el informe. No se corrigen etiquetas para aumentar puntuaciones.

El informe local `docs/evidence/agentic-2/development-baseline.json` conserva modelo, cuantización reportada por Ollama, versión, parámetros, duraciones por herramienta, muestreo de RAM del proceso Ollama y asignación VRAM del modelo cada segundo. No representa el pico absoluto de recursos ni el uso total de GPU. No se ejecutaron varias generaciones simultáneamente.

Los umbrales de `Tests/evaluation/agentic-2-thresholds.json` se fijaron después de este baseline y antes de optimizar o ejecutar validación/holdout: 90% intención, herramientas y tareas; 100% contratos y variables; Recall@3 ≥0,9, MRR ≥0,75, p95 ≤90 s. Los controles críticos requieren cero efectos no autorizados y preservación completa. No se declara aceptación si una de estas comprobaciones falla.

## Reproducción

```powershell
dotnet build backend/QuickPrompt.Evaluation/QuickPrompt.Evaluation.csproj --no-restore -p:BaseOutputPath=C:/Projects/Repos/QuickPrompt-Web/.playwright/agentic-evals/
dotnet .playwright/agentic-evals/Debug/net8.0/QuickPrompt.Evaluation.dll --agentic Tests/evaluation/agentic-2-v1.json docs/evidence/agentic-2/development.json development
# Ejecutar validation y holdout por separado, después de congelar los cambios.
```

Las evidencias generadas permanecen locales. Mantener directorios separados por ejecución para conservar los intentos fallidos y anteriores. No instalar dependencias, descargar modelos ni publicar como parte de estas pruebas.

## Evaluación final y corrección del protocolo

El primer holdout de v1 quedó en **FAIL** y se conserva íntegro en `docs/evidence/agentic-2/verified-20261016/acceptance-summary.json`. La comprobación contaba como errores una revisión automática permitida y el prefijo de una herramienta que falla de forma controlada. El assert lingüístico aceptaba «elegir» pero rechazaba «elige», presente en un prompt correcto. No se reescribió ese resultado como PASS.

El protocolo v2 declara las secuencias de herramientas permitidas por caso, conserva el máximo de cinco pasos y una sola revisión, y distingue un fallo esperado de una secuencia incorrecta. También corrige dos etiquetas de desarrollo de ADAPT a IMPROVE para peticiones que dicen «mejora». Los asserts de contenido reconocen flexiones de verbos; siguen comprobando la tecnología/contexto solicitado dentro del cuerpo del prompt y la fuente real. Se creó un conjunto reservado nuevo de 17 peticiones. Los cambios posteriores de producto que afectan la generación se justifican con el caso de desarrollo `create-2`: un cardinal no es un tema y los resultados de una comparación no deben pedirse como entradas. Los resultados del holdout anterior no se usaron para ajustar prompts. El control posterior de preferencias aisladas y los permisos de ejecución tienen pruebas de contrato propias.

`Tests/evaluation/agentic-2-v2.json` (versión interna v2.1) conserva 34 casos de desarrollo, 34 de validación y los 17 reservados nuevos. La definición anterior de v2 permanece en `agentic-2-v2-initial.json`. Los umbrales cuantitativos se mantienen en `agentic-2-v2-thresholds.json`; no se redujeron para obtener el resultado.

Resultado final: **85 casos, 338 comprobaciones, PASS**, en `docs/evidence/agentic-2/protocol-v2-final/acceptance-summary.json`. La prueba incluye inferencia Qwen real cuando el flujo la requiere y cinco errores de generador deliberadamente controlados; las rutas deterministas no hacen llamadas al modelo innecesarias.

| Partición | Casos | Intención / herramientas / contratos | Variables / tareas | Recall@3 | MRR | p50 | p95 |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: |
| Desarrollo | 34 | 100% / 100% / 100% | 100% / 100% | 1 | 0,9583 | 146 ms | 26.014 ms |
| Validación | 34 | 100% / 100% / 100% | 100% / 100% | 1 | 0,9583 | 64 ms | 27.179 ms |
| Holdout nuevo | 17 | 100% / 100% / 100% | 100% / 100% | 1 | 0,8333 | 2.221 ms | 22.415 ms |

En todas las particiones: cero mutaciones de biblioteca no autorizadas, cero envíos externos, cero IDs inventados aceptados y preservación de datos completa. La precisión de herramientas mide las secuencias cerradas realmente permitidas; no equivale a un agente con herramientas arbitrarias. El resumen contiene referencias simbólicas del dataset resueltas a los GUID y revisiones efectivos de cada SQLite de prueba.

Muestreo local cada segundo: hasta 297.107.456 bytes de RAM en el proceso observado de Ollama y 5.225.374.496 bytes de asignación VRAM del modelo. Son muestras y asignación reportada por Ollama, no picos absolutos ni memoria total del sistema/GPU. Se registran versión de Ollama, Qwen3:8b Q4_K_M, parámetros de cada adaptador, uso de tokens disponible y hashes de los binarios evaluados. El editor y ejecutor heredados no exponen conteos de tokens mediante su interfaz.

## SQL léxico frente a FTS5/BM25

Comparación controlada con las mismas peticiones crudas y fixtures, índice FTS en memoria, sin modificar la biblioteca:

| Partición | Consultas | Recall@3 SQL / FTS5 | MRR SQL / FTS5 | Media SQL / FTS5 |
| --- | ---: | --- | --- | --- |
| Desarrollo | 12 | 1 / 1 | 0,8333 / 0,7778 | 11,28 / 0,17 ms |
| Validación | 12 | 1 / 0,5 | 0,8194 / 0,4028 | 12,45 / 0,14 ms |
| Holdout | 6 | 1 / 1 | 0,75 / 0,8056 | 11,40 / 0,33 ms |

FTS5 reduce la consulta aislada, pero no demuestra una mejora consistente de recuperación. El coste de crear/actualizar el índice está excluido y el corpus es pequeño; no es una prueba de escala. Se conserva SQL con expansión bilingüe y selección semántica acotada, sin embeddings ni almacenamiento adicional. El Recall del workflow final incluye la interpretación local de consulta y no debe confundirse con esta comparación léxica aislada.

Reproducción completa del protocolo actual, usando dependencias y modelo ya instalados:

```powershell
.\scripts\Test-Agentic.ps1 -RealQwen -Headed
```

Sin `-RealQwen` ejecuta componentes, backend y errores controlados, sin descargar ni llamar a modelos reales. El script reserva el holdout para después de aprobar desarrollo/validación. La certificación se limita a estos casos sintéticos, los contratos y recorridos de navegador registrados; no demuestra exactitud semántica universal, producción comercial ni envío automatizado a proveedores externos.
