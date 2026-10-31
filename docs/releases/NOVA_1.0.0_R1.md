# QuickPrompt NOVA 1.0.0 — cierre de correcciones

Estado: **CERRADA POR ACEPTACIÓN DEL USUARIO**. Fecha: 2026-10-23, America/La_Paz. Tag de esta revisión: **`nova-v1.0.0-r1`**. Versión visible: **1.0.0 · Versión inicial**, desde `apps/web/package.json`, también comprobada en cabecera y Configuración.

El tag anterior `nova-v1.0.0` ya identifica la publicación inicial. Se conserva sin moverlo; `r1` identifica el nuevo snapshot con mantenimiento y correcciones dentro de la misma versión visible. Se conservan asimismo los demás tags históricos.

## Aceptación humana

Después de recibir las correcciones de F01–F05 y sus límites, el usuario confirmó de nuevo que lo probó y funcionó, y solicitó commit, push, tag y versión inicial visible. **REL-09 queda aceptado para el flujo probado por el usuario**. Su declaración es la fuente de esta aceptación; no proporcionó detalles adicionales de la tarea, medidas ni una observación independiente. No se inventan esos datos.

El flujo sigue siendo elegir la acción y usar IA para contenido/búsqueda, con revisión y confirmación de efectos. Esta aceptación no convierte fidelidad universal, carga sostenida o todos los proveedores externos en capacidades certificadas.

## Contenido publicado

- Campos de variables compartidos por detalle, editor y acciones NOVA, sin duplicar el renderizado ni la autoridad de dominio.
- Documentación vigente organizada por propietario; guías antiguas de comandos/modal archivadas con contexto. Reglas, AGENTS y skill local actualizados.
- Correcciones F01–F05 (referencia histórica conservada en el expediente original): guardado desde editor estable, revisión del snapshot actual, elección semántica con identidad/revisión, creación separada de controles de sesión y ejecución con valores visibles/catálogo preparado/evidencia fuera de datos.
- Tests de regresión y contrato público aditivo `LiveView.sourceChoices`; sin cambio de esquema SQLite, dependencias, proveedor o presupuestos.

## Verificación de este cierre

Backend **1163/1163** y frontend **260/260** se reejecutaron para esta publicación; build TypeScript/Vite PASS. Arquitectura estática **36 controles**, enlaces de **13 documentos** y **3 tests** del comprobador pasan. Se comprobó en navegador la versión visible sin escribir datos ni cambiar preferencias. El build mantiene un aviso de tamaño de bundle; no es un fallo de compilación.

La campaña real con Qwen3:8b inmediatamente anterior a este cierre conserva recibos de copia/versión con edición humana y sin editar a390px, revisión actual, elección de candidato y JSON local útil. Es **evidencia previa comprobada**, no una nueva ejecución de inferencia en este turno. El manifiesto [nova-v1.0.0-r1.json](nova-v1.0.0-r1.json) registra hashes de fuentes, candidato, logs y recibos para distinguir ambas campañas. El candidato local congelado se verificó por hash y coincide con el frontend recompilado.

## Límites conservados

La omisión de fallback de plazo en una propuesta y la restricción futura más estricta del control de archivos siguen documentadas. La revisión humana permitió corregir/aceptar contenido antes del guardado; no prueban fidelidad autónoma universal. Los fallos originales y los diseños retirados no se reescriben como PASS. Se conservan los 27 casos de auditoría (referencia histórica conservada en el expediente original) y la [aceptación inicial anterior](NOVA_1.0.0.md).

## Ejecución, publicación y rollback

La instancia habitual5277 conserva su biblioteca, SQLite íntegra y hash lógico idéntico antes/después de la activación. Se mantiene Production con Lab desactivado; la base privada5196 no se usa. El runtime conserva Qwen3:8b y el SDK local de Adaptive-AI-Core configurado. No se publican sus instalaciones, bases, credenciales, logs o binarios: esta entrega publica fuentes y manifiesto, no un instalador independiente.

Arranque y requisitos: [README](../../README.md) y [Core](../../agents/nova-core/README.md). Rollback: reconstruir API y frontend desde `nova-v1.0.0` o utilizar su candidato anterior conservado, con el mismo SDK y la biblioteca respaldada. No requiere migración de datos. No mover tags existentes ni usar force-push.
