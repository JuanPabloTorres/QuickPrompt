# Aceptación de rendimiento NOVA

Estado: **ACEPTACIÓN PARCIAL; objetivo global de latencia y calidad semántica NO ACREDITADOS**. Se implementaron y validaron mejoras deterministas y de UI. No se habilitó el clasificador más rápido que perdió precisión. No hay commit, push, publicación, instalación ni modificación de la biblioteca personal.

## Resultado medido

| Comprobación | Resultado |
|---|---|
| UI real, 30 observaciones por modo | p95 demostrativo **4373.1 ms**, rápido **402.4 ms**; contenido exacto en 60/60 |
| Lectura de origen, corpus 1000, 30 por ruta | p95 ranking **24.3991 ms**, por ID **0.7320 ms**; documentos/revisiones idénticos |
| Recuperación general sin cambios | Recall@6=1, MRR=1 en 30 títulos exactos; no prueba recall semántico |
| Principal, mismo fixture de dos registros | A0 **134430 / 161434 ms**, final **166585 ms**; correcto; 25 → 22 solicitudes; no mejora global demostrada |
| Principal, fixture funcional ampliado | **118986 ms**, correcto; no se usa para porcentaje contra A0 de dos registros |
| Variables, fixture original | A0 falla por límite de pasos; final correcto en **105426 ms**, 14 solicitudes |
| Interpretación holdout, Qwen real | Base **9/16**, variante rápida **8/16**; ambas FAIL contra umbrales existentes; variante rápida rechazada |
| Modelo/VRAM | Qwen3:8b Q4_K_M y contexto 12288 conservados; residencia observada 5225374496 bytes, sin medición de pico |
| Regresión automatizada | **272 backend + 150 heredadas + 83 React = 505 PASS**, cero omitidas en .NET |
| Builds | API sin warnings/errores; React/TypeScript correctos, aviso existente de bundle principal 553.37 kB / gzip 171.25 kB |

No se declara una reducción del 20% del p95 del agente. Solo UI y SQLite tienen las 30 observaciones por variante elegida. Las muestras completas de agente son pequeñas, con presión de memoria y variación observada; el resultado final más lento se conserva. Menos llamadas no garantiza menor tiempo de pared. El perfil experimental de 69491 ms tampoco es un resultado aceptado del producto: falló calidad.

## Cambios entregados

- Diagnóstico local por RunId con ActivitySource/Meter, duración, llamadas/uso de modelo, herramientas, planificación, recuperación, verificación, UI y eventos. Retención acotada, sin exportador ni texto privado. Endpoint documentado `GET /api/v1/nova/live/{id}/performance`.
- Navegación por identidad/revisión realmente resueltas y llenado con valores fundamentados. Mismos contratos, permisos, catálogo filtrado, recibos, revisión y aprobación; Qwen sigue eligiendo cuando el estado no determina la acción.
- Finalización por poscondiciones comprobadas, sin otra inferencia de confirmación. Las elecciones deterministas se atribuyen a código, no al modelo.
- Lectura fresca por ID sin ranking/caché, escritura rápida nativa, menos scroll y esperas fijas, compilación y resultados vinculados a los inputs actuales.
- Notificación SSE que despierta el poll autenticado, fallback acotado, reenvío de recibos sin repetir efectos y exclusión de envíos duplicados del compositor.
- Ejecución rápida predeterminada para preferencias nuevas; modo demostrativo opcional. Se conservan preferencias explícitas anteriores, cursor, acciones verdaderas, pausa/cancelación y protección ante conflictos reales de campos.

Predeterminados finales: `SemanticThinking=true`, `StopAfterVerifiedResult=true`, `ExecuteGroundedPlan=true`, `DirectSourceRead=true`. El editor y las referencias complejas mantienen su razonamiento. El memo previo sigue configurable; todos los benchmarks de inferencia usan `ReuseInference=false`.

## Recorridos y límites

Edge visible y una sola Page: adaptación principal, proveedor Claude sin envío, lectura sin edición, pregunta ante ambigüedad, cancelación durante inferencia, aprobación exacta y revisión incrementada, variables, ejecución local seleccionada, adaptación seleccionada más larga y errores transitorio/persistente. Las capturas se revisaron visualmente, incluidos logo oficial, campos reales, `LISTO` y versión 02. Solo la aprobación de QA modifica su registro sintético; los demás registros y recorridos quedan intactos. No se observan envíos externos, IDs inventados aceptados ni guardados sobre revisiones equivocadas en estas pruebas.

Permanecen fallos semánticos: paráfrasis que abre sin adaptar, resolución de origen desde Home en el caso local, decisión inválida en el caso largo y texto de workflow añadido a algunos borradores. Seleccionar primero la tarjeta permite comprobar ejecución/local y campos, pero no convierte los fallos de Home en éxitos. La evaluación detectó también 9/16 de precisión base y 8/16 experimental; no se ajustó el holdout ni se debilitaron umbrales. Sus grupos metamórficos están vacíos: el 1 serializado por convención no acredita esa dimensión.

No se ensayaron Flash Attention, KV, alta concurrencia, otro modelo, partición GPU/CPU ni precarga automática del producto. La precarga vacía de QA se registra aparte y no se incluye en el tiempo de tarea; no hay unload ni reinicio de Ollama. Las muestras de CPU/RAM/VRAM son observaciones del proceso/equipo, no picos ni atribución exclusiva; llegó a haber aproximadamente 0.5 GiB de RAM libre. Los escenarios largos no alcanzan el máximo de contexto. El error persistente es HTTP 503 inyectado, no una evaluación de generación.

Revisión de preparación para entrega:

| Severidad | Evidencia y decisión | Verificación necesaria para cerrar |
|---|---|---|
| Blocker para aceptación semántica global | 9/16 base, 8/16 experimental; ajuste rápido desactivado | Corregir interpretación con datos de desarrollo independientes y superar los umbrales sin ajustar contra el holdout |
| High para objetivo de rendimiento total | Última repetición comparable 166585 ms; n insuficiente | Repeticiones comparables con recursos estables y p95 de resultado correcto, incluyendo todos los fallos |
| Medium de medición | Presión de RAM observada y ausencia de picos por proceso/modelo | Medir recursos durante una campaña controlada; no atribuir variación a una causa no demostrada |
| Low de empaquetado web | Aviso de bundle 553.37 kB; build correcto | Perfilar arranque antes de decidir cambios de partición; no se ocultó el aviso |

## Reproducción y alcance de entrega

```powershell
dotnet build backend/QuickPrompt.Api/QuickPrompt.Api.csproj --no-restore -o .nova-build/latency/candidate -m:1 -nodeReuse:false
dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore -m:1 -nodeReuse:false
dotnet test src/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore -p:BaseOutputPath=C:/Projects/Repos/QuickPrompt-Web/.nova-build/latency/legacy/ -m:1 -nodeReuse:false
npm --prefix apps/web test
npm --prefix apps/web run build
dotnet run --project Tests/nova/benchmarks/LatencyBenchmarks.csproj --no-restore
node Tests/nova/ui-latency.cjs
$env:NOVA_TEST_BUILD='.nova-build/latency/candidate/QuickPrompt.Api.dll'
$env:NOVA_PERF_SPEED='fast'
$env:NOVA_PERF_VARIANT='A7-final'
$env:NOVA_PERF_SCENARIOS='interview,variables'
node Tests/nova/latency.cjs
```

Ejecutar los benchmarks secuencialmente, sin builds ni otra inferencia simultánea. El runner copia binarios/dist y usa únicamente SQLite sintético bajo `.playwright`; preserva hashes, opciones, modelo, tiempos y fallos en `docs/evidence`. Para repetir las identidades originales puede fijarse `NOVA_PERF_FIXTURE=.playwright/latency-1792283279133/synthetic.sqlite`. La evaluación semántica y sus comandos están en [EVALUATION_COMPARISON.md](EVALUATION_COMPARISON.md).

El API final se compiló/ejecutó en instancias sintéticas aisladas; no se reiniciaron las instancias existentes que usan datos personales. Se reconstruyó `apps/web/dist`. Las instancias y navegadores de QA se cierran al terminar. No se afirma despliegue externo ni actualización del backend ya cargado por una instancia anterior. MAUI/net9 no se pudo compilar con el único SDK instalado, 8.0.425; no se instaló otro. Se excluyó el proyecto de benchmark de los globs de MAUI para mantenerlo fuera del producto, conservando los cambios anteriores de branding.

Rollback local: `QuickPrompt:Live:StopAfterVerifiedResult=false`, `QuickPrompt:Live:ExecuteGroundedPlan=false`, `QuickPrompt:Preparation:DirectSourceRead=false` y modo demostrativo. `SemanticThinking=true` ya conserva la base. No hay migración de datos ni cambios globales de Ollama que revertir.

Fuentes de evidencia: [baseline](PERFORMANCE_BASELINE.md), [mapa y causas](AGENT_LATENCY_ANALYSIS.md), [experimentos rechazados/seleccionados](INFERENCE_EXPERIMENTS.md), [recuperación](RETRIEVAL_BENCHMARK.md), [UI](UI_EXECUTION_PERFORMANCE.md) y [comparación de calidad/E2E](EVALUATION_COMPARISON.md). TRX locales: `docs/evidence/performance-final-checks`. Fallos y resultados supersedidos se conservan; no se publica un PASS global.
