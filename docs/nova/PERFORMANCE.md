# Ejecución y actividad sin esperas de diagnóstico

Este documento conserva la validación anterior del observador y la reutilización de respuestas. La auditoría posterior con inferencia fresca, muestras de UI/SQLite y holdout completo se documenta en [PERFORMANCE_ACCEPTANCE.md](PERFORMANCE_ACCEPTANCE.md). Los tiempos de caché de esta página no representan aceleración de solicitudes nuevas.

La escritura y sus recibos de verificación son el camino de ejecución. El progreso visual es observación: un POST de progreso lento o fallido no puede detener el driver, convertir una escritura correcta en fallo ni retrasar el recibo. Se permite como máximo un envío de progreso pendiente, con un límite de 750 ms; al confirmar una acción se abandona ese envío auxiliar. El recibo autorizado sigue esperando aceptación y se reenvía con la misma identidad si la conexión falla, sin repetir la acción DOM.

El registro de valores se actualiza en memoria y notifica a React de forma agrupada cada 80 ms. El panel cerrado no se suscribe a esas notificaciones. El observador SSE conserva secuencias, deduplicación y recuperación de huecos, pero agrupa consultas del snapshot en ventanas de 120 ms, con un solo GET pendiente. Las órdenes de pausa, cancelación y aprobación no esperan esa agrupación.

El lanzador circular también suspende la suscripción y el renderizado del detalle cuando se cierra el asistente, aunque el registro estuviera desplegado antes. Ocultar esa interfaz no desmonta el compositor ni detiene al executor.

## Reutilización de inferencia

La API activa por defecto una caché exclusivamente en memoria de respuestas estructuradas de `PreparationInference`. La identidad combina el hash del paquete íntegro enviado al modelo (mensajes, contexto observado, valores, herramientas y esquemas, opciones y modelo) y el digest instalado que devuelve Ollama. No utiliza semejanza de frases ni una lista de casos predefinidos. Antes de reutilizar se consulta de nuevo la identidad del modelo; si no se puede obtener, se hace inferencia normal. También se comprueba el digest antes de guardar una respuesta tras una inferencia larga.

Solo se retienen respuestas completas que satisfacen el esquema o una selección única con herramienta y argumentos disponibles. No se conservan fallos de transporte, respuestas truncadas, JSON inválido, acciones en lote o herramientas desconocidas. Se elimina el campo privado de pensamiento de la copia. La caché tiene una vigencia de cinco minutos, hasta 64 entradas, un máximo de cuatro MiB de respuestas codificadas en UTF-8 y 256 000 bytes por respuesta; una nueva operación elimina entradas vencidas. Puede desactivarse con `QuickPrompt:Preparation:ReuseInference=false` para comparaciones operativas.

Reutilizar un cálculo no ejecuta una acción guardada. Cada operación vuelve a leer la interfaz y el origen vigente, valida identidad, revisión, datos literales, capacidades y resultados. No se memorizan ni reutilizan aprobaciones, escrituras, recibos, ejecuciones de tareas locales o confirmaciones de guardado. Las transformaciones del editor conservan su validación del dominio. El caché no escribe contenidos privados en disco ni en logs.

## Perfil estable del modelo

Las fases semánticas, la generación, las decisiones y el cliente local de esta API usan el presupuesto de contexto de 12 288 tokens que ya empleaban las decisiones de interfaz. Se mantienen modelos, razonamiento, temperaturas, presupuestos de salida y límites de seguridad de entrada. El cliente compartido conserva 8192 por defecto para otros consumidores; esta API solicita expresamente el perfil común.

La comparación anterior mostraba cargas repetidas de 9–22 segundos al cambiar entre fases. Un tamaño estable permite reutilizar el runner; esto concuerda con la comparación de opciones de runner de [Ollama](https://github.com/ollama/ollama/blob/main/server/sched.go) y sus [reglas de contexto y memoria](https://docs.ollama.com/faq). La velocidad inicial sigue dependiendo de si el modelo está cargado, de sus cálculos y de la memoria disponible. No se desactivan verificadores ni se elige una herramienta en sustitución de Qwen para conseguir una cifra menor.

## Reproducción

Sin instalar paquetes, modelos o navegadores:

```powershell
dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore
npm.cmd test --prefix apps/web
npm.cmd run build --prefix apps/web
dotnet build backend/QuickPrompt.Api/QuickPrompt.Api.csproj --no-restore -o .nova-build/performance/api
node Tests/nova/performance.cjs
```

El runner requiere una copia previa del backend y React en `.playwright/performance-baseline/api` y `web`. Arranca exclusivamente su API 5194, proxy local de medición 5195 y SQLite sintética. Los binarios y assets se copian a salidas fijas por variante. Compara dos recorridos anteriores y cuatro nuevos: petición inicial, repetición idéntica, datos distintos y repetición con el panel cerrado. Comprueba el campo real, el registro observado y que el origen persistido no cambió. El proxy registra únicamente cantidades, hashes y tiempos, sin guardar mensajes ni respuestas del modelo. Conserva vídeo, capturas y mediciones en `docs/evidence/performance-*`, fuera de Git.

Para reutilizar una línea base ya terminada con sus mismos IDs sintéticos y realizar solo la nueva variante:

```powershell
node Tests/nova/performance.cjs --baseline=docs/evidence/performance-1792276876709
```

Las mediciones de la primera ejecución de caché y observador (antes de unificar el perfil) están en `performance-1792276876709`: repetición anterior 60 653 ms con siete inferencias y 32 snapshots; repetición con caché 2533 ms sin inferencias y nueve snapshots. El cambio de datos produjo el nuevo valor literal correcto, sin usar el anterior, y su siguiente repetición tardó 2560 ms con el panel cerrado. Estas cifras corresponden a esos recorridos y equipo, no a una garantía para tareas nuevas.

La variante final, con perfil estable, está en `performance-1792277197752/validation.json`. Todos sus recorridos pasan con Qwen real y Edge visible, manteniendo el texto guardado sin cambios y cero errores del navegador:

| Recorrido | Tiempo | Inferencias nuevas | Snapshots |
|---|---:|---:|---:|
| Anterior, modelo ya utilizado, panel abierto | 60,7 s | 7 | 32 |
| Perfil final, primera petición sin caché | 34,7 s | 7 | 14 |
| Perfil final, repetición idéntica, panel abierto | 2,4 s | 0 | 9 |
| Perfil final, valores distintos | 17,4 s | 5 | 11 |
| Perfil final, repetición de esos valores, panel cerrado | 10,4 s | 0 | 16 |

En todas las inferencias finales se observó `num_ctx=12288` y las cargas por llamada fueron de 4–11 ms. La primera petición anterior en frío había tardado 83,0 s; no se usa para calcular la comparación con modelo caliente. La última repetición coincidió con la compilación del evaluador y no sirve para aislar el coste de abrir/cerrar el panel. La regresión de transporte sí mantiene bloqueadas las respuestas de progreso y demuestra que el driver puede completar los campos y confirmar su recibo mientras el diagnóstico no responde. El informe completo conserva esa variación, sin seleccionar solo el mejor tiempo.

Validación automatizada: **260 pruebas backend y 71 React**, compilaciones de API y frontend correctas. Las pruebas de caché incluyen identidad/revisión, datos y esquemas distintos, cambio de digest, metadata no disponible, cancelación, respuestas incompletas y expulsión de resultados antiguos al llenar el límite.

También pasan cuatro controles semánticos reales del perfil final: adaptar el seleccionado, buscar sin editar, referencia ambigua que debe preguntar y crear un artefacto nuevo sin reemplazar el anterior. `semantic-profile.json` conserva los cuatro resultados, cero errores críticos y precisión 1 en esta muestra dirigida. Su estado `DIAGNOSTIC_COMPLETE` distingue esta muestra de una aceptación completa del dataset; no se afirma haber repetido todo el holdout.
