# Arquitectura y seguridad de NOVA

GOAL 07 añade un flujo nuevo de preparación desde la entrada principal. Sus límites, contratos y pendientes están en [GOAL07_ARCHITECTURE](GOAL07_ARCHITECTURE.md). Las descripciones siguientes de `NovaAssistant` y propuestas en el editor corresponden al asistente anterior conservado para compatibilidad y Focus.

`apps/web` contiene React 19, TypeScript estricto, Vite y Tailwind v4. Discover, Focus y Create comparten shell; hooks locales controlan formularios y solicitudes cancelables. Zod valida respuestas HTTP. Motion se limita a transiciones; NOVA/proveedores/colecciones/datos se cargan mediante imports dinámicos. No se añadieron router, estado global ni caché remota adicional.

`QuickPrompt.Api` es ASP.NET Core Web API, limitado a loopback. Sirve archivos estáticos React y OpenAPI embebido; no registra Razor, Blazor ni SignalR. `QuickPrompt.Application` define DTOs, puertos `IPromptReader`/`IPromptWriter`/`INovaInference` y casos de uso de biblioteca y asistente. `QuickPrompt.Infrastructure` adapta SQLite y Ollama. Referencia el dominio puro existente y enlaza las fuentes C# auditadas; excluye expresamente `PanelSession` y `WebPromptTransfer`, que requieren JSInterop de Blazor.

El enlace de fuentes es transitorio y explícito: evita copiar implementaciones de persistencia durante la convivencia. La nueva API no depende del assembly web anterior. Tras aprobar paridad se podrán trasladar esas fuentes en un cambio separado, con regresión y rollback.

## Invariantes

- El parser y compilador C# existentes son la única autoridad para variables, escapes, defaults y compilación. React no mantiene un parser alternativo.
- Guardar, archivar, favorito y eliminar verifican revisión dentro de la transacción SQLite. Una revisión ausente no se infiere al editar.
- Compilar o visualizar no registra uso. Tras una copia correcta o ejecución completa se envía un recibo con operationId y snapshot textual. El recibo persiste entre reinicios y rechaza replay con contenido diferente.
- Una modificación de instrucciones generales entre copia y registro provoca conflicto; nunca se guarda otro texto como si fuera el copiado.
- `NovaAssistant` recibe un lector y una interfaz de inferencia; no dispone de escritor. No expone shell, navegación, archivos arbitrarios ni CRUD al modelo.
- La interpretación devuelve una intención cerrada entre once valores. Un clasificador, una búsqueda y, cuando corresponde, una lectura: sin bucles autónomos, hasta tres pasos. Las recomendaciones y sus IDs salen de SQLite; el LLM no proporciona IDs de biblioteca.
- Proponer no persiste. El usuario lleva la propuesta al editor, la revisa y guarda una nueva revisión mediante los servicios normales.
- La ejecución local exige confirmación y coincidencia con el texto revisado. SSE permite progreso, complete o error; el cliente valida revisión, UTF-8 y evento de finalización. Un stream parcial no habilita copiar ni registrar uso. Abort propaga cancelación a Ollama.

## Controles comprobados

Host y puerto exactos, IP remota loopback, Origin exacto y rechazo de `Sec-Fetch-Site: cross-site`. Las mutaciones necesitan el header no secreto `X-QuickPrompt-Client: nova-v1`; no hay CORS abierto. El proxy Vite solo reconoce el origen de desarrollo explícito. No se confunde ese header con autenticación de un sistema multiusuario.

CSP restringe scripts a archivos propios; no se inyecta HTML generado. Los atributos de estilo están permitidos para Motion y los primitivos de foco. El overlay y scroll-lock se gestionan sin estilos dinámicos de Radix que violaban CSP. No se habilitó `unsafe-inline` para scripts ni para elementos style.

Ollama acepta únicamente IP loopback HTTP; sin proxy ni redirecciones. Modelos allowlisted, contexto acotado, timeout configurable de 5–180 segundos y una operación IA concurrente (429 mientras está ocupada). Tamaño HTTP general limitado a ~32 MiB para respaldos; texto/variables tienen límites adicionales. Las URLs externas son una allowlist fija; nunca se añade texto del prompt a ellas.

Los logs de hosting no registran consultas de búsqueda. Los fallos inesperados registran tipo y correlación, sin contenido ni excepciones completas. Respuestas API usan no-store. No hay credenciales en el frontend ni servicios cloud añadidos.

Pruebas incluyen host forjado por HTTP real, origen ajeno, ausencia del header, compilado exacto, concurrencia, rechazo de planes/intent desconocidos, backup/restore y cancelación real. No se declara una auditoría exhaustiva de MAUI/Blazor, del sistema operativo o de sitios externos. El servicio es local de un usuario; exponerlo en red requeriría otro diseño de autenticación/autorización.
