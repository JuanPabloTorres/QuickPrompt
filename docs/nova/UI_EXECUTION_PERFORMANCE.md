# Rendimiento de ejecución visible

Ejecución rápida es el valor predeterminado para una preferencia nueva. La preferencia previa `normal` se respeta y ahora se presenta como **Ejecución demostrativa**. Ambos modos usan AgentUiDriver, setters nativos, eventos React, cursor sobre el control real, comprobación de identidad/revisión/valores y recibos idempotentes. Las animaciones no autorizan ni completan operaciones.

Cambios: evitar scroll si el control ya cabe en el viewport; escribir el valor completo en modo rápido; retirar esperas fijas de asentamiento/variables solo en rápido. La búsqueda espera el resultado correspondiente a la consulta actual; useLibrary deja de presentar la lista anterior como lista preparada durante la transición. Esa condición permite retirar el sleep de 350 ms sin convertirlo en una carrera.

No se cambia pausa/cancelación ni se interpreta tocar, enfocar, hacer scroll u ocultar la pestaña como una pausa. Las ediciones que realmente cambian el campo/identidad siguen protegidas. Una confirmación perdida no reejecuta la acción DOM.

Benchmark aislado de componente: `node Tests/nova/ui-latency.cjs`. Sirve la App real y AgentUiDriver en un entry de QA separado del build de producto, con Edge visible, mismo formulario, compilación real y datos sintéticos. Alterna 30 escrituras por modo, excluye un calentamiento por modo y comprueba texto exacto Unicode, eventos input y dos destinos reales del cursor. No consulta Qwen; mide escritura y comprobación, no latencia completa del agente. No instala paquetes.

El benchmark de agente `Tests/nova/latency.cjs` usa el build normal del producto y el modelo real. El observador de navegador registra primera aparición del cursor, recepciones SSE y eventos input sin guardar texto. La telemetría del producto conserva solo métricas; el diario de actividad existente sigue acotado/coalescido y su detalle no bloquea las acciones.

## Resultado final

Evidencia: `docs/evidence/ui-latency-1792289118935/measurements.json` y `verified-editor.png`. **PASS**, 30 observaciones por modo, texto Unicode exacto en los 60 casos, dos controles reales señalados por el cursor en cada acción y cero errores del navegador. El texto sintético mide aproximadamente 1.2K caracteres. Se excluye un calentamiento por modo y el tiempo de limpiar los campos entre muestras.

| Modo | n | p50 ms | p95 ms | Acciones/s de servicio |
|---|---:|---:|---:|---:|
| Demostrativo (`normal`) | 30 | 3982.0 | 4373.1 | 0.2576 |
| Rápido (`fast`) | 30 | 362.2 | 402.4 | 2.8376 |

Ambos modos esperan la misma inspección/compilación real y verifican el mismo contenido. Rápido emite un evento input para el cuerpo; demostrativo emite varios fragmentos. La medición incluye el driver y la preparación de controles, no interpretación Qwen, puesta en marcha de la app, carga inicial del logo ni aprobación humana. Throughput es el inverso del servicio medio, no concurrencia de tareas NOVA. Edge usa renderizado por software únicamente en este benchmark de componente; los E2E del agente usan su lanzamiento habitual. La misma Page alterna los modos para reducir sesgo de orden.

Los logos oficiales se verifican cargados (`complete` y `naturalWidth > 0`) antes de medir y se inspeccionó la captura final. Las ejecuciones previas `ui-latency-1792285420471` (cierre del navegador, causa no demostrada), `ui-latency-1792285584367` (error del agregador de QA) y `ui-latency-1792285634721` (60 textos correctos, pero raíz de assets incorrecta en QA) se conservan. La última se sustituyó para aceptación visual por la tanda final con `publicDir` real y `assetsVerified=true`; no se mezclan sus tiempos con esta tabla.

La regresión de React verifica además invalidación de compilación en el mismo commit del render, búsqueda lista únicamente para su clave actual, aviso SSE recibido durante un poll pendiente, doble envío, pausa explícita, cancelación, reconexión y no repetir una escritura al reenviar el recibo. Ningún evento SSE autoriza una acción DOM.
