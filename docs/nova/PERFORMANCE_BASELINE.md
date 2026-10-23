# Baseline de rendimiento

A0 es la implementación funcional del árbol de trabajo, con el logo oficial, congelada antes de optimizar. No es Git HEAD. Evidencia inmutable: `docs/evidence/latency-1792283045926` (diagnóstico frío), `docs/evidence/latency-1792283279133` (dos repeticiones residentes), `docs/evidence/latency-1792283749749` (casos dirigidos). Cada ejecución conserva hashes de assemblies, metadatos del modelo, parámetros, conteos/tokens/duraciones, acciones y screenshot/video cuando fue posible.

Equipo observado: Windows, Intel Core i7-13620H, 16 procesadores lógicos, aproximadamente 16 GiB de RAM física; NVIDIA RTX 4070 Laptop, 8188 MiB VRAM. Ollama 0.40.4; Qwen3 8.2B `qwen3:8b`, Q4_K_M, digest `500a1f067a9f782620b40bee6f7b0c89e17ae61f686b92c24933e4ca4b2b8b41`. Contexto 12288. `/api/ps` después de cargar: 5,225,374,496 bytes, todos reportados como VRAM. No se reinició Ollama ni se descargaron modelos.

Había otras aplicaciones abiertas y aproximadamente 1.4–1.6 GiB de RAM libre en las comprobaciones iniciales. Durante la evaluación semántica se observó también 492568 KiB libres y 6185 MiB de VRAM ocupada por el conjunto de aplicaciones (`docs/evidence/performance-hardware-20261017.json`). No se cerraron aplicaciones personales. Esto limita la comparabilidad, especialmente de colas/carga/renderizado. Las pruebas Qwen de este trabajo se ejecutaron secuencialmente.

Configuración A0: thinking del clasificador de contenido activado para Qwen3, editor con razonamiento, contexto fijo 12288, presupuestos existentes, modo demostrativo (`normal`), finalización decidida por el modelo. Para las comparaciones se desactiva el replay de inferencia: `ReuseInference=false`. No se mezclan sus hits con aceleración real de inferencia.

Primera petición exacta exigida: «Busca mi prompt para redactar correos, adáptalo para solicitar una entrevista y déjalo listo para copiar.» Se envió desde el input real de NOVA, en Edge visible y en una sola pestaña, sobre SQLite sintético. Tiempo medido hasta controles y resultado verificados; para un borrador, termina en la revisión humana sin aprobar automáticamente.

Las dos observaciones residentes iniciales correctas fueron 134430 ms y 161434 ms. Con n=2, p50 por rango más próximo es 134430 ms y p95 es 161434 ms. Estos percentiles son descriptivos de dos muestras, no una estimación estable del p95 poblacional.

Reproducción: `node Tests/nova/latency.cjs`. `NOVA_TEST_BUILD`, `NOVA_WEB_BUILD`, `NOVA_PERF_FIXTURE`, `NOVA_PERF_OPTIONS`, `NOVA_PERF_SCENARIOS`, `NOVA_PERF_COUNT`, `NOVA_PERF_SPEED` seleccionan exclusivamente builds, fixture y configuración del experimento. Los paths exactos y parámetros se conservan en `environment.json`.

Para A0 residente se usó `.playwright/latency-1792283279133/api/QuickPrompt.Api.dll` y su carpeta `web`. El fixture de esa carpeta se copia con el proceso parado para mantener las mismas identidades y revisiones entre variantes. Nunca se usa la biblioteca personal.

## Comparación dirigida, todavía de muestra pequeña

| Medida del principal | A0 residente, repetición 1 | A0 residente, repetición 2 | A7 con thinking TextGoal desactivado |
|---|---:|---:|---:|
| Resultado correcto | Sí | Sí | Sí |
| Tiempo hasta borrador verificado ms | 134430 | 161434 | 69491 |
| Solicitudes al modelo en el proxy | 25 | 25 | 22 |
| Suma de tiempos de solicitudes LLM ms | 120678 | 132155 | 64843 |
| Prompt tokens | 22402 | 22435 | 18255 |
| Output tokens, incluido razonamiento cuando se informa | 2237 | 2460 | 1496 |
| Despachos UI, incluidas observaciones/start | 10 | 10 | 10 |
| Primera acción UI reconocida ms | 93602 | 90035 | 33442 |
| Working set API observado bytes | 91791360 | 70971392 | 124153856 |

A7 corresponde a `latency-1792286706220`, con correlación por RunId y ninguna llamada ajena/sin correlación. El proxy A0 es anterior a la cabecera diagnóstica; el contador backend inicial omitía la inferencia hija del editor, por eso se utiliza el proxy para el conteo A0. Los tiempos LLM incluyen transporte; no son exclusivamente tiempo de GPU. El working set es una muestra del proceso, no memoria exclusiva por tarea ni pico. La telemetría agregada no conserva texto.

Estas cifras no acreditan el objetivo de reducir un 20% el p95 del conjunto principal. Faltan 30 repeticiones comparables por escenario y la decisión de configuración depende además de los resultados de calidad. Los percentiles fiables disponibles aquí corresponden a los benchmarks aislados de UI y lectura, no al agente completo.

El perfil finalmente seleccionado mantiene thinking original: `latency-1792287823707`, principal correcto en **118986 ms**, 22 llamadas, 18224 prompt tokens / 2166 output tokens, 106116 ms de solicitudes LLM, 10 despachos UI, primera acción reconocida 76848 ms, cursor real 77071 ms, working set API 112287744 bytes. Su fixture funcional añade cinco registros sintéticos para comprobar otros recorridos (siete en total); no se usa esta observación ampliada para una comparación porcentual con A0 de dos registros. No hubo solicitudes externas del navegador. El perfil más rápido de 69491 ms de la tabla anterior **no es el predeterminado**, porque perdió calidad en el holdout.

La repetición final con **el fixture original de dos registros y los mismos IDs/revisiones** (`latency-1792288494863`) dio **166585 ms**, correcta, 22 llamadas, 18268 prompt tokens / 2140 output tokens, 159039 ms de solicitudes LLM y primera acción reconocida a 122933 ms. No es una mejora frente a las dos observaciones A0. Con n=1 no se estima un p95 estable ni se declara ganancia porcentual. Variables pasó en 105426 ms con 14 solicitudes; A0 había fallado por límite de pasos en 166689 ms con 26 solicitudes. La finalización correcta de variables mejora; no se calcula un porcentaje de tiempo contra un fallo.

En esa repetición, Ollama informó en conjunto 1739 ms de carga, 5378 ms de evaluación de entrada y 101634 ms de generación. La pared de las solicitudes incluye además transporte/espera/servicio no atribuido por esos campos; no se supone que todo ese residuo sea generación. A0 informó 82269/91078 ms de generación. La muestra tardía de recursos tenía 527940 KiB de RAM libre y GPU a 85 °C; una observación no demuestra causalidad térmica ni atribución de memoria exclusiva. No se cambiaron perfiles de potencia ni se cerraron aplicaciones del usuario.
