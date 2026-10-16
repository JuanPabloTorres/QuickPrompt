# Aceptación — entrega ejecutable 1

Fecha: 2026-10-16. Evidencia sintética, almacenada en SQLite real; Edge instalado y Ollama instalado cuando se indica. No hay evaluación sobre los datos privados originales.

**No se declara PASS global del SOW.** La igualdad byte a byte del portapapeles Windows tiene un fallo observado y la matriz de paridad contiene trabajo pendiente.

| Comprobación | Estado |
|---|---|
| Baseline existente .NET | `EXECUTED_PASS`: 150/150, salida aislada, sin detener instancia anterior |
| Solución NOVA .NET | `EXECUTED_PASS`: build sin warnings/errores; 13/13 tests de casos de uso y SQLite |
| React/TypeScript y Vite | `EXECUTED_PASS`: strict typecheck y build estático |
| Vitest/Testing Library | `EXECUTED_PASS`: 9/9; espera de variables sin previews inválidos, clipboard denegado, snapshot, conflicto, Escape, UTF-8 fragmentado, stream parcial/desconocido y cancelación |
| Biblioteca en Edge | `EXECUTED_PASS`: crear, buscar, abrir, campos, editar, archivo/recuperación, cancelar borrado y copiar |
| Copia sin variables | `EXECUTED_PASS`: seleccionar y copiar en dos acciones desde fila visible |
| Plantilla/compilado | `EXECUTED_PASS`: Unicode, repetidos, multilínea, escapes, defaults y no mutación por compilar |
| Texto entregado a Clipboard API | `EXECUTED_PASS`: igualdad exacta instrumentada sin reemplazar la escritura real |
| Lectura del portapapeles nativo con LF | **`EXECUTED_FAIL`**: Edge/Windows devuelve CRLF. También se reprodujo con ClipboardItem/Blob; no se ocultó mediante un PASS normalizado |
| Descarga UTF-8 | `EXECUTED_PASS`: archivo descargado coincide byte a byte con compilado LF |
| Colecciones | `EXECUTED_PASS`: crear/asignar/filtrar desde React→API→SQLite |
| Restore | `EXECUTED_PASS`: confirmación, snapshot vigente, respaldo físico verificado, rechazo sin cambios ante conflicto |
| Reinicio real de proceso | `EXECUTED_PASS`: documento idéntico, integridad y datos conservados |
| Ollama ausente | `EXECUTED_PASS`: recorrido completo con endpoint deliberadamente inaccesible |
| NOVA/Qwen3 | `EXECUTED_PASS` acotado: borrador→revisión→guardar; propuestas no escriben por sí mismas |
| Qwen3/SSE/cancelación | `EXECUTED_PASS`: respuesta real completa; detener tras tokens visibles no permite copiar parcial ni cuenta uso |
| Evaluación de intención v1 | `EXECUTED_PASS` agregado con fallo individual: 17/18 (94.44%), schema 100%, cero IDs inventados/escrituras por interpretar |
| Proveedores externos | `EXECUTED_PASS` solo para fallback interceptado ChatGPT/Claude; interacción autenticada real `NOT_TESTED` |
| Responsive/temas/teclado | `EXECUTED_PASS` en el alcance automatizado: cinco anchos, ambos temas, hit testing, Ctrl K, Escape y reduced motion |
| Accesibilidad humana y zoom nativo 200% | `NOT_TESTED`; no se afirma WCAG certificada |
| Datos privados y paridad definitiva | `BLOCKED`: autorización de originales y aceptación posterior requeridas |
| MV3 React y retiro Blazor | `NOT_TESTED`/pendientes; el sistema anterior se conserva |

## Evidencia

- [Recorrido Edge final](../evidence/nova/acceptance.json), [sin Ollama](../evidence/nova/acceptance-offline.json), [con IA real](../evidence/nova/acceptance-ai.json). El estado global queda `EXECUTED_FAIL` por la diferencia LF/CRLF, aunque sus recorridos funcionales aprobados se enumeran por separado. El harness devuelve exit code 2 ante ese fallo de igualdad; no lo convierte en PASS de CI.
- [Evaluación IA](../evidence/nova/ai-evaluation.json), dataset fijo en `Tests/evaluation/nova-intents-v1.json`: development/validation/holdout. Umbrales establecidos antes de ejecutar: accuracy ≥90%, schema 100%, cero IDs inventados y cero escrituras no autorizadas.
- El caso holdout de COMPILE se clasificó como FILL_VARIABLES. No se alteró el caso ni se afinó el clasificador con ese resultado. La evaluación es una muestra inicial de 18 solicitudes, no una garantía de calidad general. La latencia exacta p50/p95 está en el JSON; es una medición de extremo a extremo, sin separar carga de modelo e inferencia. Tokens/s, RAM/VRAM, retrieval Recall@K/MRR, multivuelta y evaluación humana quedan `NOT_TESTED`.
- [Aislamiento MAUI](../evidence/nova/legacy-isolation.json), [arranque y OpenAPI](../evidence/nova/launcher.json), [licencias declaradas/lock hash](../evidence/nova/dependencies.json), [contraste de tokens](../evidence/nova/contrast.json).
- [Vista anterior](../evidence/nova/legacy-desktop.png), [Discover](../evidence/nova/discover-desktop.png), [Focus](../evidence/nova/focus-desktop.png), [móvil claro](../evidence/nova/focus-mobile-light.png), [inferencia real](../evidence/nova/real-qwen-stream.png).

## Hallazgos corregidos durante la validación

Se corrigieron estilos dinámicos de overlay incompatibles con CSP sin abrir scripts inline; el checkbox de colección ahora responde inmediatamente y revierte ante fallo; el selector tiene nombre accesible inequívoco; el harness espera la escritura efectiva del portapapeles, evitando confundir un toast anterior con una operación terminada. El registro de uso ya iniciado tras una acción correcta continúa aunque se cierre Focus. El feedback se colocó dentro del flujo para no cubrir acciones en móvil. La vista previa espera campos requeridos antes de compilar, evitando errores prematuros al abrir un prompt sin completar.

Las pruebas no equivalen a usabilidad humana, autorización para migrar originales ni verificación real de proveedores por fixtures. La [matriz de paridad](PARITY.md) define lo que falta antes del retiro de la presentación anterior.
