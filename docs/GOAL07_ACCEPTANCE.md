# GOAL 07 — aceptación del hito interno

**Hito interno de preparación: PASS.** La ejecución real terminó con 15 comprobaciones aprobadas y cero errores de consola. No se declara GOAL 07 completo ni preparación comercial/producción. El SOW exige demostrar primero el núcleo de preparación y obtener aprobación antes de integrar el worker externo o ampliar conexiones.

[SOW recibido](nova/GOAL07_SOW.md) · [arquitectura, auditoría y límites](nova/GOAL07_ARCHITECTURE.md) · [contrato HTTP](../contracts/openapi/nova-v1.json).

## Evidencia reproducible

| Área | Estado | Resultado |
|---|---|---|
| Backend nuevo y SQLite real | PASS | 31 pruebas |
| React y contratos SSE | PASS | 14 pruebas |
| Regresión dominio/Blazor | PASS | 150 pruebas |
| A–J, según alcance descrito | PASS | 15 comprobaciones; Qwen3, SQLite y Edge reales |
| Intérprete de producción | PASS | 56/60, JSON válido 60/60; umbrales previos alcanzados |
| Igualdad binaria de clipboard Windows | FAIL | LF→CRLF nativo; argumento API y descarga exactos |
| Worker externo y nuevas conexiones | BLOCKED | Requieren aprobación del hito interno según SOW, línea 681 |
| Calidad humana / Recall@K general / datos privados originales | NOT_TESTED | No se equiparan a pruebas automáticas |

- Backend: [backend-tests.log](evidence/goal07/backend-tests.log), SQLite real aislado, contratos/permisos/presupuestos, fuentes inventadas, variables, defaults, duplicados, guardado paralelo, consentimiento, respaldo y reinicio.
- React: [frontend-tests.log](evidence/goal07/frontend-tests.log), intención → resultado sin editor, campos faltantes, recuperación anterior, errores sin perder trabajo, SSE fragmentado y cancelación.
- Dominio/Blazor previo: [legacy-tests.log](evidence/goal07/legacy-tests.log), 150 pruebas.
- Núcleo real: [acceptance.json](evidence/goal07/acceptance.json), Edge instalado, API, SQLite y Qwen3 auténticos; datos sintéticos y perfiles aislados.
- Intenciones: [60 escenarios congelados](../Tests/evaluation/goal07-v1.json), [resultados del intérprete de producción](evidence/goal07/intent-evaluation.json). No son 60 pruebas E2E de generación.
- Regresión de biblioteca: [library-regression.json](evidence/goal07/library-regression.json). Las comprobaciones funcionales pasan; igualdad binaria del clipboard nativo Windows conserva `EXECUTED_FAIL` por LF→CRLF. Descargar UTF-8 y el argumento enviado a Clipboard API conservan el texto exacto.
- Recursos: [snapshot](evidence/goal07/resources.json), [ventana de muestreo](evidence/goal07/resource-window.json). Distinguen memoria del proceso, memoria total de GPU y mediciones parciales.
- Capturas revisadas: [desktop](evidence/goal07/prepared-desktop.png), [móvil claro](evidence/goal07/prepared-mobile-light.png). No hay desbordamiento horizontal ni solapamientos en las vistas comprobadas.
- Verificación complementaria del build final: [prompt preparado con texto sintético](evidence/goal07/final-preview-api.json), título automático y estado READY en 5190. La vista compartida del editor se dejó intacta al detectar interacción del usuario; no se conserva su contenido en capturas de QA.

## Evaluación congelada

Umbrales fijados antes de ejecutar: accuracy de intención ≥85%, contrato válido ≥98%. Resultado: 56/60 (93,33%); desarrollo 20/20, validación 19/20, holdout 17/20. Contratos válidos 60/60. Latencia de interpretación p50 1279 ms y p95 1806 ms, con carga inicial incluida. Tool call válido directo 32; fallback JSON acotado 28; herramientas desconocidas propuestas 0. El fallback no se presenta como tool call exitoso. Los errores del holdout se conservan y no se usaron para ajustar el intérprete.

Las pruebas de generación y las correcciones de insumos/valores usan escenarios de desarrollo separados. El parser define las variables; no hay puntuación de «calidad» del propio modelo que autorice cambios. Recall@K general, calidad humana, precisión de inferencias personales y éxito estadístico de todo el agente permanecen `NOT_TESTED`.

En las siete preparaciones únicas del recorrido real, incluyendo búsqueda/clarificación sin generación, p50 fue 23 319 ms y p95 27 741 ms. Los replays de caché se excluyen de ese cálculo; no son latencia del modelo. [Resumen](evidence/goal07/summary-metrics.json). Los fallos de desarrollo y del capturador SSE permanecen en [iterations](evidence/goal07/iterations); el reporte final no los borra ni los presenta como PASS.

## Escenarios y límites

La ejecución de `Tests/nova/goal07.cjs` comprueba A búsqueda real; B resumidor desde intención, título automático, guardado sin editor y campos indispensables; C adaptación sin sobrescribir; D texto aportado compilado; E ejecución Qwen confirmada; F fallback fijo copiar/abrir con destino interceptado de prueba; G aclaración; H Ollama ausente; I preservación ante error; J repetición y guardado paralelo idempotentes. La preparación no guarda una plantilla final por sí misma.

F no certifica compatibilidad del editor real de ChatGPT, sesión ni envío. No se introducen nuevas APIs cloud, worker de terceros ni permisos MV3. Claude conserva el bloqueo previo. La base privada original y su migración son `NOT_TESTED`; los tests crean sus propias bases. Logs de producción no guardan contenido; los prompts en la evidencia son explícitamente sintéticos.

Pendientes internos declarados: registro ampliado de herramientas del SOW, composición con perfiles/instrucciones generales en la nueva preparación, historial/registro de uso preparado, versiones persistentes vinculadas de borradores, definición de variables opcionales y evaluación humana. La biblioteca anterior conserva sus capacidades; no se presenta esta entrega como paridad total del nuevo flujo.

## Reproducción

Con dependencias ya autorizadas/instaladas y Qwen3:8b ya presente:

```powershell
dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore
npm test --prefix apps/web
npm run build --prefix apps/web
dotnet build backend/QuickPrompt.Api/QuickPrompt.Api.csproj --no-restore -p:BaseOutputPath=C:/Projects/Repos/QuickPrompt-Web/.playwright/goal07-build/
node Tests/nova/goal07.cjs
```

El harness usa 5198 y su propia DB; nunca se conecta a un proceso ajeno. Usa Edge/Playwright ya existentes y termina sus procesos. Para medir el intérprete, restaurar el proyecto `backend/QuickPrompt.Evaluation` desde la caché existente, compilar y ejecutar su DLL con `Tests/evaluation/goal07-v1.json` y una ruta de reporte como argumentos. No descargar modelos, paquetes o navegadores para repetir estas pruebas.

La revisión se realiza en `http://127.0.0.1:5190` mediante `scripts/Start-Nova.ps1`. Su base es `.playwright/nova-review.sqlite`, con ejemplos sintéticos. No hay commits, push ni PR creados.
