# Experimentos de inferencia

Los umbrales de calidad se fijaron antes de optimizar en [EVALUATION_COMPARISON.md](EVALUATION_COMPARISON.md). Se mantiene Qwen3:8b, contexto 12288 y el editor con razonamiento. El replay de respuestas está desactivado en el benchmark.

| Variante | Variable aislada | Estado |
|---|---|---|
| A0 | Implementación base, clasificaciones con thinking existente | Medida |
| A1 | `SemanticThinking=false` únicamente en TextGoal y su reparación | RECHAZADA como predeterminada: holdout 8/16 frente a 9/16 base |
| A2 | Residencia/carga del mismo modelo | Estado frío inicial y residente registrados; sin reinicios ni unload forzado |
| A3a | `StopAfterVerifiedResult=true` | Elimina solo decisión final cuando no quedan acciones sustantivas; medición aislada contaminada por llamadas sin correlación, excluida de comparaciones de tiempo |
| A3b | Retirar segunda revisión de autoría | RECHAZADA. No acredita completar la adaptación; flag y código experimental eliminados |
| A3c | `ExecuteGroundedPlan=true` | Ejecuta búsqueda/apertura por identidad resuelta y valores con evidencia; conserva validaciones, modelo como fallback |
| A4 | Contexto ampliado de revisión y clasificación de alias | RECHAZADA. Fallos de generación/requisitos y omisión de adaptación; código experimental eliminado |
| A5 | `DirectSourceRead=true` | Lectura fresca por identidad ya vinculada; sin ranking ni caché de cuerpos |
| A6 | Ejecución rápida | Mismas acciones reales, comprobaciones y controles |
| A7 experimental | Thinking solo TextGoal desactivado + A3a/A3c/A5/A6 | Caso principal correcto en 69491 ms; 5/6 casos dirigidos correctos. No seleccionada por regresión del holdout |
| A7 final | Thinking original + A3a/A3c/A5/A6 | Validado con controles/recuperación reales; semántica original conservada. No acredita mejora global de latencia |

Rollback de cambios independientes: `QuickPrompt:Preparation:SemanticThinking=true`, `QuickPrompt:Live:StopAfterVerifiedResult=false`, `QuickPrompt:Live:ExecuteGroundedPlan=false`, `QuickPrompt:Preparation:DirectSourceRead=false` y preferencia `agentSpeed=normal`. Son opciones locales al proceso QuickPrompt. No cambian variables globales de Ollama. No existe un flag de producto para retirar la revisión de autoría: ese experimento se descartó.

No se probaron Flash Attention, tipo de KV, mayor concurrencia ni partición de GPU: cambiar el servicio o su configuración global requiere autorización y no es necesario para eliminar las inferencias redundantes observadas. No se descargó otro modelo. No se introducen embeddings ni otro motor agentic.

El adaptador ya emplea keep_alive de 5 minutos en las llamadas del editor; las estructuradas usan la residencia por defecto observada. No se añadió una precarga automática sin demostrar beneficio y coste de memoria. Carga y residencia se reportan por separado; una ejecución caliente no se presenta como mejora del clasificador frente a otra fría.

## Resultados conservados

Tiempos en milisegundos; muestras dirigidas de una observación por celda, excepto A0 principal (dos). No se calculan mejoras porcentuales ni un p95 poblacional con estas muestras. `incorrecto` conserva el tiempo del fallo; nunca se cuenta como resultado más rápido correcto.

| Perfil / evidencia local | Principal | Paráfrasis | Búsqueda | Creación | Variables |
|---|---:|---:|---:|---:|---:|
| A0 `3279133` / `3749749` | 134430, 161434 | 107888 incorrecto | 103989 | 94172 | 166689 error de pasos |
| A1 `4396650` | 90504 | 48939 incorrecto | 33333 | 48931 | 90234 error de pasos |
| A3c `4990430` | — | — | — | — | 62536 correcto |
| A7 semántica conservada `6706220` | 69491 | 28567 incorrecto | 26315 | 37383 | 30180 correcto |

Cada sufijo corresponde a `docs/evidence/latency-179228<SUFIJO>`; `environment.json` conserva hashes del API/React y opciones, `measurements.json` llamadas, tokens, herramientas, resultado real y correlación. A7 añade la variante de variable suministrada en línea separada: 25202 ms, correcta. La variante en línea conserva literalmente el texto tras `proyecto:` conforme al contrato actual; no se inventa una separación lingüística del valor.

Experimentos rechazados completos: `latency-1792284955784` (revisión retirada), `latency-1792285820691` y `latency-1792286108477` (contexto), `latency-1792286305496` (combinación con contexto). Este último tardó 31930 ms en el principal, pero omitió la adaptación: **no es una mejora válida**. No se retocaron los verificadores para aceptarlo.

La ejecución `latency-1792284849815` registró 24 solicitudes en el proxy y 17 en el RunId; la suma de duraciones supera el tiempo de pared. Su origen no está demostrado. Se excluye del A/B temporal. El envío duplicado del formulario sí se reprodujo por separado y se corrigió mediante exclusión durante el lanzamiento; una petición activa idéntica vuelve a su actividad existente. Las ejecuciones finales transportan un ID diagnóstico y distinguen llamadas ajenas/sin correlación.

Se conserva contexto 12288, los presupuestos originales por operación y el razonamiento del editor y de referencias complejas. No se habilitan paralelismo de inferencia, reducción indiscriminada de contexto ni caché nueva. La reducción de llamadas procede de ejecutar hechos ya autorizados y de terminar ante poscondiciones comprobadas, sin resumirlos otra vez con Qwen.

Predeterminados del API final: `SemanticThinking=true`, `StopAfterVerifiedResult=true`, `ExecuteGroundedPlan=true`, `DirectSourceRead=true`. El replay anterior sigue siendo configurable; no se usa en estas mediciones. Preferencia nueva: `agentSpeed=fast`; la opción demostrativa ya guardada se respeta. No se cambia ninguna configuración global de Ollama.
