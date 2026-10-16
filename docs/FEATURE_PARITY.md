# Matriz de paridad — original frente a web

Estado vigente: [matriz GOAL06](GOAL06_AUDIT.md) y [aceptación GOAL06](GOAL06_ACCEPTANCE.md). La tabla siguiente conserva el baseline histórico del MVP; sus exclusiones de extensión y formatosv1 han evolucionado en GOAL03–06.

Verificación original = lectura de código; ejecución MAUI bloqueada por NETSDK1045. README e informe histórico no son evidencia de PASS. Estado web final y pruebas exactas en MVP_ACCEPTANCE.

| Función | Evidencia original | Implementación web / validación |
|---|---|---|
| Crear y organizar plantillas | MainPage, CreatePromptUseCase, Domain.PromptTemplate | Editor servidor, categorías originales, validación de campos; Edge crear/editar |
| Buscar y paginar | QuickPromptViewModel, repositorio filtro título en memoria | Título/descripción/cuerpo, ordenación, páginas 12, filtros categoría/favorito/archivo |
| Favoritos/recientes/usos | Domain.ToggleFavorite, filtros Core | Favoritos plantillas y finalizados, LastUsed y Uses atómicos, ordenar recientes |
| Completar variables <nombre> | AngleBraceTextHandler, ExecutePromptUseCase | Tokenizador con escapes/defaults/Unicode/repetidos; no sustitución recursiva; unitarias y clipboard Edge |
| Editor texto/visual | MainPage/PromptBuilderPage, EditPromptPage | Vista chips, selección por teclado y convertir selección en marcador |
| Guardar/copy/finalizados | ExecutePromptUseCase, FinalPrompt, detalles | Compilado separado, historial local independiente, favorito/copy/reutilizar/borrar |
| Editar/eliminar | Update/DeletePromptUseCase y UI | Versiones inmutables, revisión optimista y confirmación al borrar |
| Duplicar/archivar/restaurar | Borrado lógico Domain, CRUD legado | Acciones explícitas y restauración sin perder versiones |
| Import/export | SharePromptService, MainPageViewModel | JSON v1 completo y objeto legado; DB seleccionada read-only; idempotencia y backup/restore transaccional |
| Sugerencias previas | Cache Preferences automáticamente | Opt-in por privacidad; borrar historial elimina valores |
| Idioma/tema/ayuda | Settings y Guide, themes XAML | ES/EN, light/dark, ayuda, preferencias persistidas |
| Diseño | Tokens XAML efectivos | Azul/teal/amarillo, fuentes locales, tarjetas/chips, navegación biblioteca primaria; responsive Edge |
| Lanzador IA/WebView externo | AiLauncher, WebViewInjectionService | Eliminado del runtime web; ninguna inyección/auto-submit externa |
| Mejorar/crear/autocompletar/analizar | Roadmap README; no implementación local equivalente | Ollama backend, propuestas tipadas y revisión; evaluación real Qwen3/Qwen2.5 |
| Ejecutar modelo local | No en original (lanzamiento externo) | Streaming, confirmar/detener/reintentar/copiar/guardar; historial separado |
| Cloud/anuncios | Firestore/AdMob en csproj e Infrastructure | Excluidos; aplicación usable sin Ollama |
| Quick launcher | Fuera del contrato MAUI auditado | Ctrl/Cmd+K modal web; no atajo global del SO |
| Búsqueda semántica | No verificada en original | Diferida, sin Nomic ni DB vectorial; baseline léxico |
| Pruebas | 77/100% afirmación histórica, xUnit/Moq net9 no ejecutable | Suite web independiente SQLite real, contratos, Edge y evaluación local; sin heredar métricas |

Límites explícitos: no multiusuario, sincronización, extensiones de navegador ni lanzamiento global. Categorías conservan enum estable original (no se inventa soporte de categorías arbitrarias del README). Etiquetas libres no se añaden al MVP por no existir contrato efectivo auditado.

## Actualización GOAL 02 — uso diario

La tabla del MVP anterior se conserva. La estabilización y sus resultados nuevos se documentan en [PRODUCT_READINESS](PRODUCT_READINESS.md), sin sustituir evidencia histórica.

| Flujo | Estado GOAL 02 / diferencia comprobada |
|---|---|
| Arranque sin Demo ni DataPath | Verificado sobre DB normal del perfil, inicialmente vacía, sin seed implícito |
| Persistencia al cerrar proceso y volver a abrir | Plantilla de trabajo, favorito, revisiones e historial recuperados |
| Biblioteca sin Ollama | Crear, editar, buscar, completar, copiar, archivar/restaurar y borrar una plantilla propia sin modelo accesible |
| Búsqueda diaria | Revision encuentra Revisión en biblioteca y lanzador, sin alterar datos almacenados |
| Teclado y móvil | Selección enfoca primer campo; crear/editar enfoca título; panel se desplaza en móvil. aria-pressed explícito true/false |
| Restore completo | Rechaza objeto legado; conserva importación aditiva. Idioma/tema/modelo/opt-in aplicados inmediatamente; rollback ante fallo real de SQLite |
| Evolución de DB | Rechaza schema futuro sin cambiar sus bytes; exportación usa snapshot transaccional |
| Límite local | Configuración Kestrel:Endpoints rechazada para impedir que sustituya el bind. QuickPrompt:Port continúa soportado |
| IA local | Contratos y revisión semántica/factual separados. Qwen3 principal; Qwen2.5 con limitación visible y fallos registrados |
| Análisis IA | Observaciones breves tipadas; el código conserva el original, sin eco regenerado del modelo |
| Regresión final | Build sin errores/advertencias, 70/70 pruebas, PASS_COMPLETE_BROWSER_ACCEPTANCE en Edge con Qwen3 real, móvil y reinicio; evidencias separadas del MVP |
| .NET 10 | BLOQUEADO: SDK ausente, estrategia incremental con rollback; no compatibilidad declarada |
| Importación de datos privados reales | BLOQUEADO: usuario decidió no autorizar una copia en esta sesión |

## Fase 03 — puente y UX incremental

La exclusión de extensiones/proveedores web de las tablas del MVP es histórica; este nuevo alcance la amplía sin cambiar SQLite/JSON v1 ni inferencia local. [Matriz QA detallada](QA_MATRIX_PHASE03.md).

| Flujo | Estado actual |
|---|---|
| Biblioteca → completar → copiar; datos/versiones/Qwen | Regresión integral Edge PASS; 81/81 pruebas, 70 anteriores conservadas |
| UI responsive/temas/teclado/estados | Tailwind 4.3.0 compilado localmente tras autorización, lockfile y css:check PASS; SVG local y cinco viewports probados con el CSS servido por Blazor |
| ChatGPT Preparar / Enviar / cancelar | MV3 real sobre fixture en Edge/Chrome PASS; sitio real BLOQUEADO, HTTP 403 sin editor/sesión |
| Claude Preparar / Enviar | Adaptador DOM sobre fixture PASS; automatización de distribución BLOQUEADA por términos sin permiso de Anthropic |
| Copiar y abrir cualquiera de los dos | Acción manual con consentimiento; portapapeles/URL fija comprobados en fixture, nunca registrado como enviado |
| Extensión ausente / contrato inválido / borrador ajeno | Texto conservado, fallback explícito, rechazo de origen/TTL/size/replay y de sobreescritura |
| Sesión/render/consulta en curso/cancelación y fallos de APIs | 14 carreras DOM y 3 fallos API inyectados aprobados; errores de confirmación no declaran «sin envío» sin prueba, auditoría fallida avisa sin bloquear |

## GOAL 04 — estado vigente

Las restricciones y cifras de las tablas anteriores corresponden a sus fases. [Aceptación actual](GOAL04_ACCEPTANCE.md): **PARTIAL_PROVIDER_BLOCKED**, aplicación local verificada, sin certificación comercial.

| Funcionalidad | Resultado actual |
|---|---|
| Funciones heredadas, categorías y diseño original | PASS regresión integral Edge; identidad azul/teal/amarillo, SVG y fuentes locales conservadas |
| Layout/overlays/teclado | PASS cinco anchos/ambos temas, escritorio bajo, hit testing, foco/Escape y reduced motion; copia en flujo normal y OverlayHost nativo |
| Estado de edición | PASS borrador conservado al reseleccionar; nueva entidad recrea controles y rechaza eventos anteriores; error de aceptar visible dentro del modal |
| Grupos personalizados independientes | PASS CRUD, icono/color, fijación/orden, contadores, relación muchos-a-muchos, asignación múltiple, sin grupo y filtros combinados; borrar grupo conserva contenido |
| Instruction Profiles | PASS CRUD/favoritos/grupos/categorías/versiones, selección explícita de uno o ninguno, precedencia determinista, preview y combinaciones reutilizables |
| Biblioteca de cientos/miles | PASS regresión 1000 plantillas; COUNT + LIMIT/OFFSET, 12 tarjetas/página, lanzador 10 resultados; selector perfiles 50 metadatos |
| Respaldo y esquema | SQLite 1→2 aditivo con copia previa y rollback probado; JSON v2 export/restore e import aditivo, lectura v1/legado; JSON simétrico 32 MiB; UI roundtrip exacto 450 plantillas/5 MB |
| IA local | PASS flujo normal compuesto con Qwen3 real/streaming/cancelación/guardado/reinicio y propuestas con revisión. Exactitud contextual FAIL limitado documentado; Qwen2.5 conserva limitaciones históricas |
| Proveedores web | PASS local/fixtures en Edge/Chrome; estados y metadatos persistentes. Sitios auténticos BLOCKED: ChatGPT sin sesión autorizada y Claude por restricción conservada |
| Fallback manual | PASS clipboard exacto, abrir URL fija y COPIED_OPENED; accesible sin autorizar inserción automática, pegado/envío manuales |
| Regresión y seguridad | 109/109, build limpio, CSS servido comprobado; guard loopback, auditorías npm/NuGet sin hallazgos reportados; sin dependencias/modelos nuevos |
| Importación privada / .NET 10 | BLOCKED por decisión de no usar datos privados / SDK ausente. Sin compatibilidad ficticia |

## GOAL 05 — estado vigente, QuickPrompt Simple

Las tablas anteriores corresponden a sus fases. [Aceptación actual](GOAL05_ACCEPTANCE.md): **PASS local / BLOCKED proveedores auténticos**.

| Capacidad preservada | Ubicación/resultado GOAL05 |
|---|---|
| Biblioteca/búsqueda/favoritos/recientes/categorías | Una biblioteca; filtros rápidos y Filtros compactos, consulta paginada SQL preservada; A–D/J PASS |
| Variables/valores por defecto/editor visual | Campos inmediatos, Copiar valida/compila determinísticamente; creación con título/texto y chips opcionales; 60 campos/Unicode/defaults/errores PASS |
| Grupos muchos-a-muchos y bulk | Crear/asignar contextual atómico, filtro/grupos y selección múltiple desplegables; CRUD/delete conserva prompts PASS |
| Perfiles/versiones/combinaciones | Opciones avanzadas y gestor contextual existente; selección opcional, borrado limpia composición viva, snapshot histórico intacto; PASS |
| Edición/archivo/versiones/historial/finalizados | Opciones y Configuración, mismas entidades/contratos; guardado explícito y regreso al uso normal; regresión PASS |
| Import/export/migraciones | SQLite2/JSON2 sin cambios; datasets/base privadas sin acceso; restore transaccional y 450 plantillas/5MB exactos PASS |
| Qwen3/Qwen2.5 configurable | Qwen3 real PASS revisión/aceptación/streaming/cancelar/copiar/guardar/reinicio. Qwen2.5 y límites históricos conservados; factualidad no certificada |
| Creación/mejora IA | Una ayuda de IA dentro del editor y herramientas contextuales; propuesta revisada, aceptación de borrador transaccional sin persistir plantilla hasta Guardar; PASS |
| MV3 preparar/enviar/cancelar | Contratos/manifiesto/permisos intactos, destino único contextual; Edge/Chrome con fixtures PASS, auténticos BLOCKED |
| Fallback manual | Ambos destinos copian exacto y abren URL fija sin consentimiento de inserción; COPIED_OPENED persistente, nunca SENT; PASS |
| Diseño/teclado/móvil | Identidad local y Tailwind4.3.0 conservados, sidebar/hero/paneles repetidos consolidados; cinco anchos/ambos temas/ES–EN/alto400 PASS |
| Regresión y preservación | 115/115 (109 originales intactas +6 nuevas), suite integral Edge PASS; hashes de724 evidencias anteriores y32 fuentes protegidas PASS |
| Límites | Piloto humano NOT_TESTED; importación privada/SDK10/proveedores auténticos BLOCKED. Panel lateral/API siguen como dirección futura |
