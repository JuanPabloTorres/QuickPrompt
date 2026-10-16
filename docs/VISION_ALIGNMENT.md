# Dirección de producto y entrega QuickPrompt Simple

La [visión recibida](PRODUCT_DIRECTION.md) sitúa encontrar → personalizar → utilizar en el centro. Esta entrega termina esa etapa sobre la aplicación existente, sin reconstruirla. [Aceptación GOAL05](GOAL05_ACCEPTANCE.md).

| Dirección | Estado verificable |
|---|---|
| Biblioteca universal, independiente del tipo de tarea | PASS: título/texto, búsqueda existente, favoritos/recientes, grupos opcionales; ejemplos de escritura, lectura, descanso y aprendizaje |
| Una superficie para elegir, completar y copiar | PASS Edge, escritorio lista/detalle y móvil lista o detalle con Volver |
| CompiledPrompt inmutable compartido | PASS: mismo snapshot para copia, Qwen y puente; DTO y compilador existentes conservados |
| Teclado y feedback discreto | PASS: Ctrl/CmdK, Enter selecciona, Tab y Ctrl/CmdEnter contextual copia; mensajes inline, disclosures y foco comprobados |
| IA contextual y revisión explícita | PASS Qwen3 real: creación/mejora revisada, editor solo borrador hasta Guardar, ejecución con confirmación, streaming/cancelar/copiar/guardar/cerrar |
| Web externa y APIs diferenciadas | PASS de límites: copiar/abrir y puente existente no se presentan como API ni como proveedor auténtico aprobado |
| QuickPrompt Everywhere / panel lateral | NOT_TESTED / futuro: el puente MV3 actual no incorpora un panel lateral ni una segunda biblioteca |
| APIs oficiales opcionales / nuevos proveedores | NOT_TESTED / futuro: requieren alcance, autorización, revisión de credenciales/coste y pruebas propias; ninguna conexión nueva |
| Búsqueda por intención y organización sugerida por IA | Futuro, condicionado a fricción observada; búsqueda textual y organización manual ya preservadas |
| <10s/<20s y 4 de5 personas sin ayuda | NOT_TESTED: metas de piloto humano propuestas, nunca inferidas a partir de tiempos automáticos |

Siguiente decisión: utilizar la biblioteca con tareas reales y registrar objetivo, acciones, dificultad y resultado. Corregir primero fricción reproducida. El piloto humano puede evaluar si se encuentra/copia un prompt y se completa otro con dos variables sin instrucciones previas. No se ha ejecutado ni se han inventado participantes, valoraciones o resultados. Las propuestas de panel lateral/API permanecen como evolución; no se suman funcionalidades de IA para justificar una versión.

El adjunto recibido el 16 de octubre de 2026 repite exactamente esta visión. Se preparó la [guía de uso real y piloto](DAILY_USE_PILOT.md), con registro vacío y separación explícita entre medición humana y automatización. No se reinterpretó el reenvío como una orden de reconstruir el producto o activar APIs.
