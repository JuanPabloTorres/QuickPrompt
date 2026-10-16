# Arquitectura web local

`src/QuickPrompt.Web.sln` es independiente de MAUI. Blazor Web App InteractiveServer → servicios C# → SQLite y adaptador HTTP Ollama. No frontend duplicado, escritorio, MAUI, Electron, Firestore, anuncios ni telemetría.

El csproj MAUI raíz excluye src/** de sus globs: MSBuild incluía 20 archivos nuevos antes y 0 después. Es el único ajuste de implementación al legado; evita que un futuro build MAUI compile accidentalmente el servidor web. No cambia sus targets ni dependencias.

`QuickPrompt.Domain` enlaza sin alterar las entidades/enums puros originales de Domain/. Reutiliza identidad Guid, reglas de creación, categorías, favoritos y soft delete. Añade tokenizador determinista, modelos tipados de versión, compilado, ejecución y propuesta. No referencia el csproj MAUI ni Core acoplado a UI.

`QuickPrompt.Web/Components` es presentación web; Home.razor + code-behind orquestan acciones explícitas. `Infrastructure/LocalStore` valida y persiste con transacciones y concurrencia optimista por revisión; `ILocalLlmClient` abstrae Ollama; `LegacyImporter` transforma archivos seleccionados en un archivo de intercambio validado. Se evita añadir proyectos para cada carpeta hasta justificar su separación.

Persistencia: SQLite fuera del repo, `%LOCALAPPDATA%/QuickPromptWeb/quickprompt.sqlite`. SchemaVersion y Documents(kind,id,payload JSON) con clave compuesta y índice de título. Migración aditiva v1→v2 transaccional, con backup previo de v1; rechazo de versiones futuras. Cada tipo tiene DTO propio, sin columnas dinámicas construidas desde entrada. Exportación v2 incluye plantillas, versiones, compilados, ejecuciones, propuestas y preferencias. Restore valida primero y reemplaza todo atómicamente. Import agrega y omite identidades/contenidos existentes; no sobreescribe. Borrar plantilla conserva los textos finalizados independientes.

La búsqueda es léxica en SQLite, normalizada por acentos/mayúsculas y por palabras en cualquier orden, sobre título, descripción, contenido, categoría enum y nombres de grupo. Páginas de12 y filtros de favorito/reciente/archivo/grupo. No se carga todo el catálogo para buscar. No FTS5, vectores ni Nomic: se requiere benchmark antes de añadir complejidad.

Entidad plantilla ≠ snapshot de versión ≠ texto compilado ≠ ejecución IA ≠ propuesta. Compilar guarda texto final y contador de uso en la misma transacción. Valores previos solo se guardan opt-in, aunque el texto final contiene inevitablemente los valores usados. Respuesta IA queda en memoria hasta Guardar resultado; cancelar o recibir salida parcial impide guardar. Aplicar mejora verifica revisión, texto y variables y guarda versión nueva y estado de propuesta atómicamente.

Ollama recibe un system fijo y datos del usuario como role=user. Modelos permitidos: qwen3:8b y qwen2.5:7b-instruct, sin cambio silencioso de modelo. Qwen2.5 se selecciona explícitamente. No herramientas ni HTML/SQL provenientes del modelo. JSON estructurado y validación C# posterior. Ejecución streaming NDJSON, timeout 180s, num_ctx 8192, num_predict 1800, cancelar/reintentar. Respuestas inválidas se rechazan sin aplicar cambios.

Evolución: SDK net10 primero con pruebas completas; después FTS si las mediciones lo justifican. Multiusuario exige diseño de autenticación, autorización de datos, HTTPS y sesiones; no basta cambiar el bind. No hay runtime dependency en LocalAI-Audit-Lab.

## GOAL06

[Auditoría y decisiones actuales](GOAL06_AUDIT.md). `TemplateEditor` posee historial y operaciones sobre marcadores; nunca valores de ejecución. `TemplatePackage` es una proyección mínima para compartir y se distingue de Archivev2 privado. `PreviewImport` reutiliza la validación de importación y no escribe. `CompiledPrompt.Values` es una copia de solo lectura.

`QuickAssistCommands` resuelve comandos convencionales; `ILocalLlmClient.PlanAssist` interpreta únicamente la lista cerrada AssistAction. `PrepareAssist` liga identidades/revisiones y `ApplyAssist` valida y aplica cambios persistentes en una transacción tras aprobación. La propuesta generativa requiere su revisión independiente habitual.

El panel MV3 consulta métodos de `PanelSession` a través de la pestaña local y content script del origen exacto. No hay API HTTP nueva, CORS permisivo ni iframe. `PanelSession.Compile` usa LocalStore con revisión esperada; el panel conserva el snapshot hasta cambiar campos y descarta resultados tardíos. Solo storage de sesión para el ID de pestaña; no catálogo persistido en extensión.

`automation/playwright` es un intérprete opcional separado. No participa en guardar/copiar ni se ofrece como ejecución libre al modelo. [Uso, límites y pruebas](../automation/playwright/README.md).
