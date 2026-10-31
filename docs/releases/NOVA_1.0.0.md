# QuickPrompt NOVA 1.0.0 — versión inicial

Estado: **CERRADA POR ACEPTACIÓN DEL USUARIO**. Fecha de publicación: 2026-10-23, America/La_Paz. Tag: `nova-v1.0.0`. Existe un tag histórico `v1.0.0`; se conserva y no se sustituye.

## Aceptación y alcance

El usuario confirmó: «la prueba humana ya pasó, lo probé y funcionó» y solicitó cerrar, hacer commit/push y crear la versión inicial visible en el sistema. Se registra su testimonio como aceptación humana **REL-09**, sin inventar detalles de la tarea realizada, mediciones o una observación independiente.

El flujo aceptado es el elegido explícitamente por el usuario: **elegir la acción y usar IA para contenido y búsqueda**. Permite buscar por significado, crear y mejorar prompts, revisar/guardar copias o versiones, aplicar el prompt para obtener una respuesta y eliminar el registro confirmado. Se mantienen permisos, identidad/revisión, aprobaciones y verificación de efectos. La biblioteca manual sigue disponible sin modelo.

La interpretación automática libre rechazada está retirada de producción. Sus fallos permanecen en evaluación y no se certifican como reparados. Tampoco se afirma validación de todos los proveedores externos, paridad móvil completa, fidelidad universal, rendimiento global ni todos los criterios del expediente original ampliado. La decisión de cerrar esta versión inicial procede del usuario; no se cambia el validador histórico para producir un resultado verde.

## Identidad visible

`apps/web/package.json` es la fuente del número **1.0.0**; el lockfile coincide. Cabecera y Configuración importan esa misma versión, sin duplicar números en los componentes. Configuración identifica «Versión inicial». No hay cambio de esquema de datos ni API pública por añadir esta etiqueta.

## Validación

El acta técnica anterior (referencia histórica conservada en el expediente original) conserva el recorrido completo **14/14 PASS con Qwen real**, con búsqueda original, persistencia/reapertura, mejora/revisión 2, respuesta útil, eliminación confirmada y preservación de otro registro. Incluye un segundo consumidor del SDK y los intentos fallidos del arnés. La revisión anterior comprobó 1160 backend, 253 React y 54 SDK por sus evidencias identificadas.

Para esta publicación se vuelven a ejecutar backend, React y build web, y se comprueba en navegador la etiqueta de versión. Los resultados finales y hashes portables se registran en [nova-v1.0.0.json](nova-v1.0.0.json). La prueba humana es el testimonio del usuario, separado de estas ejecuciones automatizadas.

## Ejecución y dependencias

Se conservan React/TypeScript, ASP.NET Core, SQLite y el adaptador existente a Adaptive-AI-Core. Proveedor local: `qwen3:8b`, digest `500a1f067a9f782620b40bee6f7b0c89e17ae61f686b92c24933e4ca4b2b8b41`. Arranque: `scripts/Start-Nova.ps1` con las rutas explícitas `-CorePython` y `-CoreSdkRoot` de la instalación existente. [Configuración de Core](../../agents/nova-core/README.md). No se descargan modelos ni se amplían límites para publicar.

Adaptive-AI-Core es una dependencia externa local; no se incluye como submódulo ni se publica su repositorio desde este commit. El manifiesto registra los hashes del SDK/worker usados en la aceptación. Un clon necesita las dependencias documentadas; este tag es una publicación de fuentes, no un instalador independiente.

## Historial, pendientes y rollback

La matriz histórica (referencia histórica conservada en el expediente original) conserva el alcance original ampliado y los criterios sin certificar. REL-09 cambia a CERRADO por aceptación del usuario; los demás estados no se marcan PASS sin evidencia. La aceptación de esta entrega inicial no demuestra fiabilidad general del antiguo intérprete ni completa ese expediente histórico.

Se conserva MAUI en `apps/mobile`, Blazor de compatibilidad en `src` y los diagnósticos en Evaluation. Las bases privadas, logs, binarios, capturas y artefactos temporales quedan fuera del commit. La activación local cambia el frontend mostrado por la instancia de revisión; conserva su misma base y una copia previa. Para volver al frontend anterior se usa su ruta conservada, con la misma API y datos; no requiere migración.
