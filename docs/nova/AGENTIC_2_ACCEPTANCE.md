# NOVA Agentic Engineering 2.0 — aceptación local

Fecha: 2026-10-16. Base recuperable: `424414e`, conservada en Git. Resultado: **implementación local verificada**, con la aplicación React normal, SQLite aislada y Qwen3:8b Q4_K_M real. Alcance experimental; no certifica producción comercial ni envíos automatizados externos.

## Qué puede hacer el usuario

Desde la misma entrada: buscar y abrir un prompt real, adaptar una copia, completar valores autorizados, pedir aclaraciones y preparar una ejecución local. Un resultado completo ofrece Copiar, Guardar, Usar en… y Ajustar. Guardar una adaptación permite conservar una copia o confirmar una nueva versión del origen; la revisión impide aplicar propuestas obsoletas. Se conservan el modal, la biblioteca, los videos y el botón reversible de variables.

Ejemplo verificado: «Busca mi prompt de arquitectura, adáptalo para una API .NET y déjalo listo para utilizar». También se verificó en una sola petición buscar, adaptar, compilar, validar y ejecutar con Qwen dentro de cinco herramientas, incluso tras una revisión automática. La validación de la revisión evita repetir un paso puro y deja disponible el último paso para ejecutar.

## Matriz de entrega

| Capacidad | Estado / evidencia |
| --- | --- |
| Auditoría previa | Matriz y reproducción de 7 flujos anteriores con Qwen antes de implementar; [auditoría](AGENTIC_2_AUDIT.md) |
| Runtime finito | Estados, RunId/SessionId, plan, IDs/revisiones, herramientas, duraciones, presupuesto y aprobación; checkpoints mínimos SQLite |
| Recuperación | Borradores completos con consentimiento; operaciones interrumpidas bloqueadas; recibos impiden repetir efectos incluso tras borrar diagnósticos/restaurar biblioteca |
| Rutas deterministas | Abrir un título inequívoco, completar/compilar y destinos claros; interpretación local para semántica y varias operaciones |
| Retrieval | SQL con filtros, ranking de grupos, equivalencias bilingües, duplicados y selección semántica limitada a candidatos reales; FTS5 comparado sin migrar biblioteca |
| Preparación | Reutiliza generador/editor, resolver y compilador existentes mediante etapas separadas; valores literales y defaults autorizados |
| Evaluación | Identidad, revisión, variables, compilación, límites, restricciones y contexto solicitado; una revisión máxima; diagnóstico inicial/revisado temporal |
| Memoria | Sesión temporal separada de biblioteca y preferencias explícitas revisables/eliminables; sin conversación permanente automática |
| Herramientas | Contratos C#, argumentos estrictos, permisos, timeout/cancelación y presupuesto; sin SQL libre, shell ni HTTP arbitrario para el modelo |
| Ejecución local | Qwen real, streaming, cancelación y recibos; títulos citados, material y peticiones pospuestas no conceden permiso |
| Cambios persistentes | Guardado/versión explícitos, fuente vinculada a revisión y aprobación por propuesta; no autosave de biblioteca |
| Proveedores | Preparación y copiar/abrir mediante registro de capacidades; envío automatizado sigue fuera de las capacidades habilitadas |
| UI | Living Canvas y modal existentes, estados comprensibles, foco y móvil; detalles técnicos solo en desarrollo |
| Evaluaciones | Protocolo v2.1: 85 casos, desarrollo/validación/holdout nuevo, **338 controles PASS**; [método, métricas y límites](AGENTIC_2_MEASUREMENTS.md) |
| Compatibilidad | 150 regresiones Blazor/dominio, comandos antiguos, modal, biblioteca práctica y cuatro videos; MAUI/móvil sin nueva certificación de build |

## Comprobaciones ejecutadas

| Comprobación | Resultado |
| --- | ---: |
| Backend NOVA, contratos y runtime | 87 PASS |
| Blazor/dominio existentes | 150 PASS |
| Componentes React y SSE | 22 PASS |
| Contratos del worker Playwright | 3 PASS |
| Contratos de extensión/puente | 6 PASS |
| Total de pruebas de código y contratos | **268 PASS** |
| CSS reproducible, TypeScript/Vite, API .NET | PASS |

Recorridos adicionales de Edge real, sobre bases propias:

- `2026-10-16T16-56-40-405Z`: 13 verificaciones, incluidos los diez escenarios solicitados, confirmación móvil y petición encadenada con Qwen real.
- `2026-10-16T17-03-08-912Z`: comprobación de la compilación final, 3 verificaciones de apertura, preparación obligatoria y ejecución encadenada; diagnóstico con cinco herramientas y plan FIND → ADAPT → COMPILE → EXECUTE.
- `faults-1792170242379`: 10 verificaciones con Ollama deliberadamente simulado: caída, herramienta desconocida, IDs inventados, timeout, cancelación, no repetición, autorización, continuidad de valores y preferencias móviles. No se presentan como prueba de calidad de Qwen.
- `regression-local-commands`: 7 verificaciones con Qwen real y worker visible, actualización del mismo ID y dos versiones, cancelación y aclaraciones.
- `regression-preparation-modal`: 1280 y 390 px, cierres, foco, valores conservados, diálogo anidado, vista previa y cero errores de consola.
- `regression-learning`: 8 verificaciones, 60 prompts, categorías, importación idempotente, variable reversible Unicode, cuatro WebM decodificados, pantalla completa y móvil.

Los directorios anteriores están bajo `docs/evidence/agentic-2/` y permanecen locales. Se conservaron también los intentos fallidos y el baseline original. [Resumen portátil](AGENTIC_2_EVALUATION_SUMMARY.json).

## Cómo probar

Demo supervisada con datos públicos de ejemplo: **http://127.0.0.1:5224/**. Contiene la biblioteca práctica y «Demo de arquitectura .NET». Petición sugerida:

> Adapta el prompt «Demo de arquitectura .NET» para una API .NET y déjalo listo para utilizar.

La demo usa `.playwright/agentic-demo-review.sqlite`; no sustituye ni importa bibliotecas personales. Ejecutar toda la verificación en otro entorno con dependencias y Qwen ya instalados:

```powershell
.\scripts\Test-Agentic.ps1 -RealQwen -Headed
```

El script compila en una salida aislada, detiene el protocolo si falla desarrollo/validación y reserva el holdout para el final. Los datos temporales y videos generados quedan fuera del código versionado. No instala paquetes ni descarga modelos.

## Límites verificados

El protocolo v1 conserva su FAIL: se corrigió el evaluador y se usó un holdout nuevo, sin borrar el historial ni reducir umbrales. Los ajustes de producto posteriores se basaron en casos de desarrollo y contratos propios, no en ajustar prompts a respuestas del holdout.

Los corpus de evaluación son sintéticos y pequeños; Recall@3/MRR no prueban recuperación universal ni rendimiento con bibliotecas grandes. La selección semántica no busca fuera de su ventana de candidatos. Las comprobaciones literales son conservadoras y no sustituyen revisión humana de calidad, hechos o adecuación. Los proveedores remotos permiten preparación manual, no envíos automáticos certificados.

La diferencia LF→CRLF del portapapeles nativo de Windows sigue documentada. No se declara conformidad byte a byte del portapapeles ni se instalaron workloads para certificar MAUI. La política de orígenes, CSRF, CSP y aprobación de la extensión permanece activa.
