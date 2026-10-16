# Matriz de paridad y alcance

La preservación del almacenamiento no implica paridad de toda la experiencia anterior. No se aprueba aún el retiro de Blazor.

| Capacidad | Nueva experiencia | Evidencia / pendiente |
|---|---|---|
| Crear con/sin variables | Implementado | Edge y SQLite reales |
| Buscar título/descripción/contenido/grupo | Implementado, sin IA | API/Edge; parser/búsqueda anteriores con 150 tests |
| Seleccionar/completar/copiar | Implementado, sin editor intermedio | Dos acciones sin variables; Unicode/repetidos/multilínea |
| Copia byte a byte | API y descarga exactas; portapapeles Windows con límite | LF→CRLF nativo `EXECUTED_FAIL` |
| Edición y revisión optimista | Implementado | Conflicto rechaza sobrescritura, borrador en memoria |
| Duplicar, favorito, archivo, recuperación, borrar confirmado | Implementado | Servicios y recorridos UI; archivo preferible al borrado |
| Colecciones muchos-a-muchos | Implementado | Crear/asignar/filtrar probado en UI; renombrar/eliminar con revisión en backend |
| Categorías | Se preservan en edición/import/export | Selector de categoría en React pendiente |
| Defaults | Se preservan y usan | Editor de defaults y optionalidad explícita pendientes |
| Versiones anteriores | Persistidas y consultables por API | Navegador/restauración de versiones en React pendiente |
| Perfiles y combinaciones | Preservados/importables/exportables | Editor y selección de perfiles en React pendientes |
| Historial de compilados/resultados anterior | Preservado en SQLite/Archive v2 | Explorador de historial en React pendiente |
| Último uso y contador | Tras acciones verificadas, con recibo idempotente | Copia/local completo; nunca por vista previa |
| Desactivar historial/recordar valores | Preferencia previa conservada | Controles de privacidad opt-in/opt-out en React pendientes |
| Import/export JSON v1/v2/legado | Implementado | Validadores y roundtrip; preview + confirmación |
| Import SQLite MAUI seleccionado | API/UI disponibles | Conversor validado en baseline; nuevo flujo UI con DB original `NOT_TESTED` |
| Backup/restore | Implementado | Snapshot, concurrencia, backup físico e integridad comprobados |
| NOVA búsqueda | Implementado con IDs de SQLite | Clasificación real Qwen3; evaluación inicial 17/18 |
| NOVA crear/mejorar/valores | Implementado, revisión antes de aplicar | Crear real en Edge; mejora y sugerencias nuevas no evaluadas exhaustivamente |
| Ejecución local SSE | Implementado | Qwen3 real, respuesta completa y cancelación tras tokens |
| Qwen2.5 alternativo | Selector explícito, sin fallback silencioso | Instalada; nueva experiencia `NOT_TESTED` con este modelo |
| Registry seis proveedores | Implementado | Estado manual explícito; sin auto-submit ni API credencial implícita |
| ChatGPT/Claude copiar/abrir | Implementado | Fixtures interceptados locales; integración autenticada real `NOT_TESTED` |
| Bridge MV3 | Extensión anterior conservada | Adaptación al nuevo origen/protocolo `NOT_TESTED`, no ofrecida por NOVA |
| Light/dark y responsive | Implementado | 320/390/768/1024/1440, foco principal/hit testing |
| Accesibilidad humana/zoom 200% completo | Parcial | Escape/labels/teclado probados; lector y evaluación humana `NOT_TESTED` |
| Datos privados, migración definitiva, retiro de Blazor | Pendiente | `BLOCKED`: autorización de datos y aceptación de paridad |

Esta es la primera entrega ejecutable solicitada en el cierre del SOW, con avances de las fases local/externa. Las fases de paridad completa, evaluación extensa y limpieza siguen abiertas; no se presentan como completadas.
