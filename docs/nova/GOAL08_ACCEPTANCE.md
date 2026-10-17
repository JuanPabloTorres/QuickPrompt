# GOAL 08 — aceptación de ejecución visible

2026-10-16. Base de trabajo `424414e`, con NOVA 2.0 local conservado. Sin commits, publicación, instalaciones, descargas de modelos ni acceso a bibliotecas personales. Ver [uso y contratos](GOAL08_RUNTIME.md), [auditoría inicial](GOAL08_AUDIT.md) y [OpenAPI](../../contracts/openapi/nova-v1.json).

Resultado: **PASS_AGENTIC_VISIBLE**, limitado a los recorridos y entorno descritos. Las baterías aprobadas y los fallos previos se conservan por separado.

## Alcance implementado

El orquestador existente incorpora decisiones incrementales reales de Qwen, un catálogo cerrado de herramientas UI, BrowserWorker TypeScript y eventos SSE persistidos. React presenta actividad, procedencia, pausa, continuación, cancelación, revisión y aprobación. Edge se ejecuta con `headless=false`, en una sesión interactiva de Windows y un contexto nuevo limitado al origen local.

La preparación habitual sigue siendo un workflow de reglas identificado como tal. El modo navegador consulta el modelo después de observar cada resultado. Las acciones no guardan el origen hasta confirmar una propuesta vinculada al run, contenido y revisión. El guardado utiliza el control React y se verifica contra SQLite y su historial.

## Comprobaciones de origen

Las abreviaciones deben reducir realmente el cuerpo de la plantilla. Se verifican identidad, revisión, variables, compilación, requisitos explícitos y prohibiciones reconocidas del origen. La revisión visual detectó una omisión de «no inventes información» en un borrador anterior; se añadió validación independiente y una sola corrección real, sin rellenar silenciosamente el resultado. La preparación directa conserva su propio presupuesto único de revisión.

Las ejecuciones `real-1792176609332` y `real-1792177126303` quedan en FAIL: Qwen repitió objetos JSON hasta truncar la edición de arquitectura; el original permaneció intacto. La reproducción aislada, conservada en `docs/evidence/goal08/source-editor-diagnosis/`, mostró el mismo fallo con dos límites de longitud del esquema. Especificar los cinco campos de salida y distinguirlos de la lista de restricciones de entrada produjo un objeto completo. El deserializador también rechaza campos adicionales. No se alteraron datasets ni umbrales para corregirlo.

La regresión `docs/evidence/agentic-2/2026-10-16T19-13-18-229Z/` conserva otro FAIL: al solicitar una carta para un puesto supuestamente descrito, Qwen preguntó por los datos y el workflow ignoró la pregunta. La adaptación omitió una prohibición y la única revisión devolvió el original; ambos resultados fueron rechazados. La reproducción pública de `docs/evidence/goal08/missing-context-diagnosis/` conserva esas respuestas. Ahora se muestra la aclaración real antes de generar cuando faltan los datos previos referidos; las pruebas comprueban además que valores ya disponibles y adaptaciones reutilizables continúan normalmente.

La revisión visual de `docs/evidence/goal08/public-library-demo-initial/` cambió su aceptación automática a **FAIL**: faltaba la orden «Conserva nombres, fechas y compromisos tal como se proporcionan». El original seguía intacto. Se amplió la comprobación independiente a órdenes explícitas de conservación y se eliminó la sugerencia de reducir un porcentaje fijo; preservar las cláusulas completas tiene prioridad y se sigue exigiendo una longitud estrictamente menor. La reproducción y las dos respuestas rechazadas permanecen en `preservation-clause-diagnosis/`. La corrección mantiene una sola revisión, sin añadir texto silenciosamente. El benchmark anterior de 85 casos quedó preservado en `evaluation-before-preservation-clause/`; la corrección procede de esta demo pública y no de resultados del conjunto reservado.

`real-1792179747435` conserva un FAIL posterior: las dos demostraciones de edición y el caso B pasaron, pero Qwen pidió aclarar un valor de proyecto ya etiquetado antes de abrir el origen seleccionado. El sistema respetó la pregunta y bloqueó el run. Se aclaró la función del contexto seleccionado en ambas formas de decisión del modelo: sus tarjetas iniciales no son candidatos ambiguos para un origen ya elegido, y debe inspeccionar los campos antes de preguntar por datos etiquetados. El catálogo y la validación de IDs permanecen iguales; no se sustituyen las decisiones por un guion.

`real-1792180102245` conserva otra negativa real: Qwen seleccionó `ui_read_visible_state` con un argumento `query`, incompatible con su esquema vacío. No se ejecutó. La selección UI permite ahora una única reselección JSON después de argumentos inválidos de una herramienta disponible, registrando primero la decisión rechazada y su validación. Dos pruebas con modelo simulado demuestran tanto la recuperación mediante una nueva respuesta como el bloqueo tras un segundo resultado inválido. Herramientas desconocidas/no disponibles, IDs inventados y permisos insuficientes siguen bloqueándose sin reselección. La prueba específica del origen seleccionado también pasó con Qwen real en `real-1792180081332`.

`real-1792180553392` conserva la recuperación de argumentos seguida de una pregunta innecesaria sobre el propósito de un origen seleccionado que aún no se había abierto. El catálogo impedía abrirlo sin buscar de nuevo. Ahora un ID seleccionado, validado y realmente visible permite `ui_open_prompt` directamente; las preguntas sobre ese origen esperan a su inspección, o a una búsqueda que no lo encuentre. La búsqueda sin origen y la ambigüedad siguen protegidas. Tres pruebas cubren origen visible, origen no inspeccionado y origen no encontrado.

`evaluation-initial-validation-reject/` conserva un FAIL de 68 casos: `adapt-3` recibió el texto original y el adaptador lo rechazó antes de que el pipeline pudiera usar su única revisión. La validez de contratos quedó en 33/34 en validación, por debajo de su umbral inalterado de 100 %. La preparación directa permite ahora que ese intento inicial llegue a la evaluación independiente, que lo rechaza y solicita la revisión; un editor directo continúa rechazando un texto sin cambios. También se limitó la guía de brevedad a peticiones de abreviación, manteniendo las instrucciones de edición de otros tipos. Dos pruebas separan ambos contratos. No se ejecutó el conjunto reservado después de ese precheck fallido.

## Evidencia final

Los artefactos locales contienen únicamente los datos públicos/sintéticos autorizados para QA; los prompts privados no se trazan. Capturas aisladas no sustituyen las decisiones, eventos y verificaciones correlacionadas por RunId.

| Comprobación | Resultado observado | Evidencia local |
| --- | --- | --- |
| Código y contratos | 312 PASS: backend 126, legado 150, React 27, políticas Playwright 3, puente MV3 6 | Comandos de `Test-VisibleAgent.ps1`; resumen portable |
| Qwen real + Edge visible, A–J | 12 PASS, nueve runs, sin errores de página | `docs/evidence/goal08/real-1792180859289/` |
| Sugerir valores y preparar cambios manualmente | 2 PASS con Qwen real y actividad correlacionada | `docs/evidence/goal08/real-1792177931032/` |
| Fallos visibles deliberados | 9 PASS: modelo caído, herramienta desconocida, argumentos inválidos recuperables y no recuperables, ID inventado, timeout, cancelación, reinicio y fallo real al abrir un registro eliminado por el test | `docs/evidence/goal08/faults-1792180494854/` |
| Fallos previos | 10 PASS | `docs/evidence/agentic-2/faults-1792178649189/` |
| Comandos locales anteriores | 7 PASS | `docs/evidence/goal08/final-commands/` |
| NOVA 2.0 y ejecución encadenada | 13 PASS, incluida búsqueda → adaptación → compilación → ejecución en cinco herramientas | `docs/evidence/agentic-2/2026-10-16T20-14-06-044Z/` |
| Modal de preparación | PASS a 1280 y 390 px; cierre, foco, datos y diálogo de uso anidado | `docs/evidence/goal08/final-modal/` |
| Biblioteca y aprendizaje | 8 PASS: 60 prompts, categorías, importación idempotente, Unicode, cuatro WebM decodificados, pantalla completa y móvil | `docs/evidence/goal08/final-learning/` |
| Revisión visual | Escritorio y 390 px sin desbordamiento; botones separados, borrador visible y prohibición conservada; 22/22 comprobaciones de contraste | Capturas de los runs y `scripts/nova-design-check.cjs` |

La demostración final redujo el correo de 273 a 261 caracteres, conservando sus tres variables y las dos cláusulas completas sobre conservación de datos y no inventar información. Qwen eligió realmente `ui_search_prompt`, `ui_open_prompt`, `ui_open_editor`, `ui_prepare_update` y `finish`. Cancelar conservó la única versión. La comprobación de guardado confirmado verifica revisión 2 e historial de dos versiones.

Los runs del modo navegador conservan entrada pública, decisiones, eventos ordenados, estado observado, comprobaciones, capturas, metadata de Edge y WebM del worker bajo su RunId. Las acciones manuales conservan sus capturas, eventos y resultados de Qwen por separado. `environment.json` conserva modelo Q4_K_M, parámetros y hashes del API, aplicación, infraestructura, worker y frontend congelados. Edge observado: **155.0.4283.45**, headed, contexto aislado; Ollama: **0.40.4**.

La batería visible pasó con las dos cláusulas protegidas. Después solo se corrigió la revisión del flujo directo y la guía de adaptaciones que no piden brevedad; sus rutas visibles y de abreviación permanecen iguales. La demostración final sobre la biblioteca pública valida la compilación reconstruida; sus hashes y la evaluación se recogen en el [resumen portable](GOAL08_VALIDATION_SUMMARY.json).

### Evaluación congelada

**85 casos, 338 comprobaciones, PASS**. Desarrollo y validación aprobaron antes de ejecutar el conjunto reservado, sin cambiar después el producto, dataset, referencias ni umbrales. Dataset `agentic-2-v2.1`, SHA-256 `F357E01692553BC3BD2197054DA5A85142BC40EF3613B9B7D45BDCA2C2386BBE`.

| Partición | Casos | Intención / herramientas / contratos / variables / tareas | Recall@3 | MRR | p95 |
| --- | ---: | --- | ---: | ---: | ---: |
| Desarrollo | 34 | 100% en estas comprobaciones | 1 | 0,9583 | 28.337 ms |
| Validación | 34 | 100% en estas comprobaciones | 1 | 0,8611 | 26.651 ms |
| Reservado | 17 | 100% en estas comprobaciones | 1 | 0,9167 | 25.230 ms |

Cero mutaciones no autorizadas e IDs inventados aceptados; preservación de datos completa. El catálogo de evaluación no incluye herramientas de envío externo. Los informes separan inferencia real de los errores deliberados y conservan tiempos por herramienta, tokens disponibles, recursos muestreados y hashes de binarios. Son resultados del benchmark sintético local, no una medida universal de calidad ni una certificación comercial. Evidencia: `docs/evidence/goal08/evaluation/acceptance-summary.json`.

### Demo pública revisada

La compilación final está disponible localmente en **http://127.0.0.1:5228**, con SQLite independiente y 60 prompts prácticos. El run `179c6cea-d480-4631-8baa-299407470e15` ejecutó la petición obligatoria, mostró la revisión real y canceló sin modificar el correo ni su única versión. El borrador pasó de 273 a 261 caracteres, con ambas cláusulas y las tres variables intactas. Se revisó visualmente la [confirmación real](../evidence/goal08/public-library-demo/real-public-library-proposal.png); la traza, estados, resultado y hashes están en `docs/evidence/goal08/public-library-demo/`.

Si necesitas volver a iniciar esa demo después de detenerla, desde la raíz del repositorio:

```powershell
.\scripts\Start-Nova.ps1 -Port 5228 -DataPath .playwright/goal08-demo.sqlite
```

## Límites

Validación local de Windows, Edge, Ollama y Qwen3:8b Q4_K_M. No certifica otros navegadores, MAUI, ejecución como servicio, envíos externos ni calidad semántica universal. La extracción conservadora de prohibiciones no reconoce todas las formulaciones posibles. El navegador admite una ejecución activa y un tiempo total de ocho minutos, incluida la revisión humana.

Los eventos persistidos contienen metadatos. Solicitud, campos, observaciones y propuestas permanecen temporalmente en memoria; sessionStorage conserva únicamente el RunId para recuperar el panel. Reiniciar bloquea ejecuciones interrumpidas sin repetir acciones. El modo navegador no guarda contenido mediante el consentimiento del modo directo.

El script de búsqueda de la skill de UI no pudo ejecutarse por ausencia de Python utilizable en este Windows; se aplicaron sus instrucciones y referencias incluidas. La comprobación de contraste y la revisión real de escritorio/móvil se registran por separado.

## Reproducción

```powershell
.\scripts\Test-VisibleAgent.ps1 -RealQwen -Regressions
# Además, las 85 evaluaciones congeladas de NOVA 2.0:
.\scripts\Test-Agentic.ps1 -RealQwen -Headed
```

Utiliza exclusivamente dependencias, Edge y modelos ya instalados. El protocolo de evaluación de NOVA 2.0 conserva las particiones y umbrales congelados de v2.1; se comprueban desarrollo y validación antes de abrir el conjunto reservado. Las pruebas con errores simulados se presentan separadas de la inferencia real.
