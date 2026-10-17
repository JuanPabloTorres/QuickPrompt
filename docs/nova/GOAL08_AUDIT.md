# GOAL 08 — auditoría previa

2026-10-16. Rama `main`, base `424414e`. NOVA 2.0 tiene cambios locales verificados que se conservan; no se crean commits ni se descartan archivos.

## Evidencia observada antes de implementar

`node Tests/nova/visible-baseline.cjs` abrió Edge **headed**, React normal y una SQLite nueva con un prompt público. Petición: «Busca el prompt Escribir un correo profesional y prepara una versión más breve». El original quedó intacto, pero el resultado reutilizó la plantilla y pidió sus variables: la ruta determinista reconoció buscar e ignoró la intención de abreviar. Evidencia local: `docs/evidence/goal08/baseline-1792170969494/`.

La petición quedó en una ruta por reglas y no consultó Qwen. El campo `model` de ese baseline identifica la configuración; las demostraciones posteriores registran las decisiones reales del modelo por separado.

| Pieza | Existe | Falta / riesgo |
| --- | --- | --- |
| React Living Canvas | Biblioteca, Focus, editor, variables, modal, destinos | Actividad correlacionada, pausa, reconexión y selección de modo |
| AgentOrchestrator | Workflow finito, cinco herramientas, checkpoints, validación, cancelación y sesiones | Procedencia explícita y decisiones sucesivas de herramientas UI |
| Qwen/Ollama | Tool calling, JSON Schema, editor real, streaming, límites | Seleccionar siguiente acción a partir de una observación real del DOM |
| SQLite | Revisiones, guardado explícito, recibos, restore protegido | Eventos mínimos secuenciados y recibos de ejecución visible |
| Playwright runner | Intérprete cerrado, Edge headed, contexto aislado y política de origen | Recibe un guion completo fijo; no es una decisión agentic incremental |
| MV3 | Puente y destinos con aprobación | No debe convertirse en acceso general al navegador ni ampliarse a sitios externos |
| Pruebas | NOVA 2.0 y regresiones separadas de mocks | Casos A–J con intención → decisión → clic → observación → persistencia |

## Decisiones de arquitectura

Extender el orquestador actual con una variante visible finita y reutilizar sus servicios de edición, compilación y almacenamiento. Un servicio de ciclo de vida aporta controles y eventos a ambos mecanismos. Un BrowserWorker TypeScript recibe una sola acción lógica por vez mediante stdin/stdout y traduce exclusivamente el catálogo permitido a locators estables. El worker no decide, no tiene SQL ni acepta código o selectores del modelo.

El modo directo conserva el flujo de preparación y declara sus selecciones por reglas como tales. El modo navegador consulta Qwen entre observaciones reales; un fallo de modelo no activa un guion de sustitución. Edge usa un contexto nuevo, `headless=false`, origen local exacto y bloqueo de otros destinos. El proceso Node es supervisado; únicamente la ventana del navegador es visible.

Los eventos persistentes contienen IDs, estados, procedencia, duraciones y mensajes de catálogo, sin consultas, valores ni contenido de borradores. La solicitud, observaciones y propuestas permanecen temporales en memoria. Tras reiniciar se bloquean operaciones interrumpidas: recuperar eventos nunca repite efectos. Pausa en límites de operaciones, cancelación real y aprobación vinculada al run, contenido y revisión se validan en backend.

## Validación prevista

Primera demostración con el prompt público y Qwen real, apertura y edición por controles React, cancelación y guardado confirmado de una nueva revisión. Casos A–J, fallos simulados separados, reconexión ordenada, seguridad de contratos/origen, móvil y regresiones de NOVA 2.0. No instalar software, usar datos privados ni automatizar proveedores externos.
