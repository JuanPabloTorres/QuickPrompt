# GOAL 03 — QuickPrompt Web: Provider Bridge + Tailwind UX 2.0

## Misión y alcance

Trabaja en `C:\Projects\Repos\QuickPrompt-Web`, conservando la implementación web Blazor/ASP.NET Core/SQLite existente, la identidad visual de QuickPrompt y las evidencias aprobadas: el último reporte proporcionado indica 70/70 pruebas, E2E Edge operativo, Qwen3 funcional y .NET 8 instalado. **Son antecedentes reportados, no pruebas que debas presuponer como PASS en esta ejecución**. Audita archivos reales, rama, git status, tests y capturas existentes antes de implementar. Si el repositorio local tiene otra estructura, adapta las rutas, no inventes clases o archivos.

Entregar una integración **desde QuickPrompt Web hacia los compositores web de ChatGPT y Claude**, sin llaves API externas: (1) preparar/injectar un prompt ya compilado sin enviarlo y (2) enviarlo tras un acto explícito del usuario para **esa operación**. En paralelo modernizar la aplicación **con Tailwind CSS v4** (o versión compatible verificada) en lugar de reemplazar el producto original por un chatbot. Mantener todas las funciones sin Ollama: biblioteca, plantillas, variables, favoritos, búsqueda, import/export, versionado, copiado, historial, temas e idiomas.

No descargar modelos ni habilitar Firestore, anuncios, telemetría, cuentas cloud, API de OpenAI/Anthropic, hosting remoto o acceso a la LAN. No instalar tooling/dependencias sin comprobar cache/instalación y obtener autorización puntual si no están disponibles. No cambiar SDK/target a .NET 10 en esta fase salvo si existe autorización/SDK y plan separado.

## Fase 0 — Baseline y auditoría (obligatorio)

1. `git status --short`, `git branch --show-current`, `dotnet --info`, `node --version`, `npm --version`, `ollama list` (si disponibles), inventario de solución y paquetes. No tocar datos privados.
2. Inspecciona documentación `docs/PRODUCT_READINESS.md`, `docs/FEATURE_PARITY.md`, `docs/AI_QUALITY.md`, `docs/ISSUES_DECISIONS.md`, instrucciones previas y los tests actuales. Identifica run commands correctos desde el proyecto, NO la solución MAUI antigua.
3. Ejecuta baseline de build, 70 pruebas reportadas y E2E Edge si entorno disponible. Registra salidas reales; si faltan herramientas o dependencias, marca BLOQUEADO.
4. Audita interfaz real a partir de Blazor/CSS y capturas nuevas de escritorio/móvil, documenta puntos de fricción visual, navegación, errores, tiempos y accesibilidad. Anota componentes reutilizables y tokens de diseño originales (azul profundo #23486A, azul #3B6790, teal #4C7B8B, amarillo #EFB036 y estados semánticos) y posibles problemas de contraste.
5. Crea rama local `feature/provider-bridge-tailwind` si es seguro y todavía no hay una rama designada; preserva trabajo sin commitear, no ejecutar reset/clean destructivo.

## Fase 1 — Contrato de proveedores (antes de automatizar)

Define `ProviderId` = LocalOllama | ChatGptWeb | ClaudeWeb, y operación = `PrepareOnly` | `PrepareAndSend`. Diseña DTO versionado `PromptTransferRequest` con `requestId` (UUID), `provider`, `compiledPrompt`, `action`, `createdAtUtc`, `expiresAtUtc`, `explicitUserAction`, `templateId?`, `revisionId?` sin trasladar metadata privada innecesaria. Limitar tamaño (ej. 20_000 caracteres, configurable), rechazar vacíos, argumentos fuera de allowlist, URLs arbitrarias, payload caducado y replay. Nunca almacenar credenciales de ChatGPT/Claude.

Estados visibles: `Ready`, `ExtensionMissing`, `ProviderTabOpening`, `WaitingForEditor`, `Prepared`, `SubmissionRequested`, `SubmittedUI`, `NeedsManualPaste`, `LoginRequired`, `BlockedByProvider`, `Timeout`, `Failed`, `Cancelled`. `SubmittedUI` = interacción UI confirmada; **no garantiza aceptación/resultado del modelo**. Registrar `requestId`, proveedor, acción, resultado, duración y versión del adaptador, sin registrar texto de prompts en logs de la extensión. Distinguir transferencia web de inferencia Qwen local en el historial.

UI botones independientes por proveedor: `Preparar en ChatGPT`, `Enviar a ChatGPT`, `Preparar en Claude`, `Enviar a Claude`. Explicar privacidad: cuando el usuario usa proveedor externo, el contenido deja de ser exclusivamente local; advertencia antes de primera transferencia y para documentos sensibles. Historial de confirmación explícita, con opción de siempre volver a modo Preparar. No enviar contenido al cambiar selector, hacer hover, abrir página o iniciar QuickPrompt. No background jobs ni ejecuciones masivas.

## Fase 2 — Puente seguro web ↔ extensión Manifest V3

La app no puede modificar `chatgpt.com` y `claude.ai` directamente desde Blazor por Same Origin Policy. Construye una extensión MV3 sin servidores cloud ni CDN, dentro de `extensions/quickprompt-browser`:

- `manifest.json` con permisos mínimos `tabs`, `scripting`/`activeTab` según la ruta justificada, `storage` solo si indispensable; `host_permissions` limitados a `https://chatgpt.com/*` y `https://claude.ai/*` (y acceso mínimo al origen loopback para puente si se requiere), sin `<all_urls>` ni permisos para cookies/history/debugger/webRequest.
- `background/service-worker.js`: valida emisor como QuickPrompt local de origen exacto permitido (por defecto `http://127.0.0.1:5188`, configurable mediante control explícito; nunca aceptar dominios arbitrarios, hosts remotos ni puertos desconocidos). Valida destino por host exacto HTTPS, `requestId` único, expiración y límites de tamaño; evita reintentos de envío duplicado.
- `content/quickprompt-bridge.js` sobre la página local para trasladar, por una interacción UI, un mensaje a la extensión. Elegir un protocolo compatible y probado (`window.postMessage` con listener aislado o `externally_connectable` si admite los match patterns usados). Evitar exponer datos a orígenes externos y rechazar mensajes de ventanas/frames no confiables.
- `content/providers/chatgpt.js` y `content/providers/claude.js` como adaptadores separados con descubrimiento del editor editable real, estado, espera mediante `MutationObserver` y timeout acotado. No asumir selectores permanentes ni usar sleeps fijos. Preparar el texto preservando literalmente unicode, puntuación, variables ya resueltas y saltos de línea. Verificar que el contenido insertado coincide con el compilado antes de intentar enviar.
- Para `PrepareOnly`, mostrar `Prepared` y dejar el envío al usuario. Para `PrepareAndSend`, **únicamente tras clic claro sobre Enviar** y comprobar editor/identidad de pestaña, disparar una sola interacción de envío; idempotencia por `requestId`. No hacer clic sobre botones ambiguos, prompts vacíos, pestañas equivocadas, conversaciones no preparadas, captcha ni auth. Si no hay confirmación, informar `NeedsManualPaste` o `BlockedByProvider`, nunca `SubmittedUI` ficticio.
- Nunca leer/scrapear conversaciones o respuestas de terceros, interceptar peticiones, extraer cookies/tokens, manipular autenticación, sortear protecciones o automatizar acciones en masa. Revisar términos vigentes y documentar límites de la integración; ante restricción impeditiva ofrecer Preparar/Copiar/Abrir y no forzar AutoSend.
- Integrar detección de extensión instalada y compatibilidad por navegador (primero Edge estable Windows, luego Chrome) desde un servicio web desacoplado `IWebPromptTransfer` que use JS interop modular y cancelación. Evitar que Blazor server mande texto por red a dominios de terceros.
- Fallback en cualquier fallo: el usuario conserva el texto; botón `Copiar y abrir ChatGPT` / `Copiar y abrir Claude`, proveedor se abre solo por acción del usuario. No cambiar estado de plantilla ni almacenarla como enviada si solo se copió.

### Matriz de pruebas puente (no negociable)
- ChatGPT / Claude × `PrepareOnly` / `PrepareAndSend`; editor vacío y ya con contenido (nunca sobrescribir sin permiso), sesiones autenticada/no autenticada, página lenta, navegación SPA, tab ya abierta/nueva, extensión ausente/deshabilitada, permisos denegados, selector roto, pérdida de foco, timeout, cancelar, mensaje repetido (debe haber 1 envío), prompt con `<`, comillas, saltos de línea, emoji, ES/EN, largo hasta límite, URL maliciosa, XSS, intento de inyección desde página web ajena. Prohibido presuponer éxito en sitio externo sin E2E real observada y documentada.
- Para tests automáticos sin sesión real, construir **fixture de editor falso** en dominio de prueba local con simulación explícitamente etiquetada, y adaptadores testeados contra DOM real al menos en pruebas manuales supervisadas para cada proveedor. Nunca contar fixtures como éxito de la integración con ChatGPT/Claude.

## Fase 3 — Rediseño Tailwind y UX (no reescribir el negocio)

Moderniza la experiencia web con Tailwind v4 *compilado a CSS local*, sin CDN ni runtime externo. Usa npm CLI oficial solo si tooling/cache existe o autorización para instalar `tailwindcss` y `@tailwindcss/cli`. Verifica compatibilidad con la versión Blazor actual y cómo detectar clases en archivos `.razor`. Crea `package.json` con scripts `css:build`, `css:watch`, `css:check`, lockfile fijado; si Node/npm no existen, ofrece alternativa local standalone aprobada; nunca bloquees el arranque del producto por red. No eliminar CSS existente antes de comprobar paridad. Evita class strings dinámicos imposibles de compilar sin safelist/directivas.

### Sistema visual
- Recuperar identidad QuickPrompt: azul profundo, azul medio, teal y amarillo como acento **no dominante**; superficies neutrales, modo claro/oscuro, tipografía legible y escala coherente. Derivar `@theme`/CSS custom properties del diseño real del repositorio y fijar tokens: colores, tipografías, tipografía fluida cuando convenga, 4/8/12/16/24/32 espaciado, radii, sombras, focus, z-index y motion. Evitar gradientes estridentes y glassmorphism indiscriminado.
- Crear componentes Blazor reutilizables: `AppShell`, `SidebarNav`, `TopBar`, `PromptCard`, `PromptList`, `PromptEditor`, `VariableField`, `ProviderActionMenu`, `AiEnhancePanel`, `ModelStatus`, `Toast`, `ConfirmDialog`, `LoadingSkeleton`, `EmptyState`, `ErrorState`, `CommandPalette` y `ActivityHistory` (nombres flexibles si la estructura ya contiene equivalentes). Nada de componentes duplicados sin necesidad.
- Iconografía: escoger una sola familia coherente de SVG locales (Lucide/MIT o Heroicons/MIT, previa verificación de licencias); tamaño 18/20/24, stroke coherente, textos accesibles para botones solo-icono; iconos propios para proveedores solamente con uso de marcas autorizado. Si no se dispone del asset, mostrar nombre textual, no iconografía engañosa. Estados con texto además de color.
- Desktop: sidebar compacta biblioteca/favoritos/recientes/categorías, command palette Ctrl+K, lista/colección con búsqueda y filtros, editor legible, panel contextual lateral IA/variables/proveedores. Panel de resultados puede colapsarse; evitar 3 columnas en tamaños medios estrechos.
- Mobile: navegación adaptada y acciones accesibles, editor ancho completo, paneles en drawer y barra de acción sticky; sin scroll horizontal a 390px y 320px.
- Flujos: `buscar → seleccionar → completar → copiar` debe seguir siendo extremadamente corto; `mejorar → comparar diff → aceptar/rechazar`; `usar → seleccionar proveedor → preparar/enviar → feedback`. Botón principal visible, no obligar a completar IA para copiar.
- Loading: skeleton en biblioteca, spinner corto en acciones, estado con progreso por fases para IA local y transferencia web, botón cancelar real, timeouts y retry explícitos. Toasts no bloqueantes y accesibles `aria-live`; evitar loaders eternos/animaciones que se mueven sin fin. Disabled/empty/error/offline estados y mensajes ES/EN completos. Respetar `prefers-reduced-motion`.
- Responsive en 320, 390, 768, 1024, 1440 px. Contraste WCAG AA donde aplique, foco visible, navegación por teclado, modales focus trap / Escape / restore focus; ARIA con etiquetas correctas, botones no divs clicables.
- Antes/después: guardar capturas reales de UI antigua y nueva y tabla de paridad. No romper versionado, historial, import/export, caché de variables ni modos sin IA.

## Fase 4 — Pruebas integrales, seguridad y entrega

1. Ejecutar `dotnet build`, `dotnet test`, flujos Playwright/Edge actuales; preservar al menos 70 pruebas existentes si el baseline coincide; añadir unit/contratos para `IWebPromptTransfer`, validación y idempotencia, más E2E del puente contra fixture, más pruebas manuales supervisadas ChatGPT/Claude si se cuenta con sesión de usuario y autorización.
2. Validar UI con captura y mediciones: accesibilidad con auditoría automatizada + revisión teclado/lector de pantalla cuando posible, dark/light, responsive 320/390/768/1440, loaders, fallback clipboard, sin extensión, offline Qwen, importación y recuperación tras reinicio.
3. Validar que `Ollama` sigue limitado a loopback; no nueva exposición LAN, configuración de seguridad de extensión, privacidad, no logs de prompts, CSP del frontend, antiforgery donde aplique y seguridad de endpoints de import/export. No scraping de conversaciones.
4. Medir tiempo `seleccionar→copiar`, `seleccionar→preparar`, tasa de inserción correcta, tasa de envío UI confirmado, p50/p95 con muestra/tamaño declarados, bundle CSS local y tiempo de arranque. No inventar mediciones.
5. Crear `docs/PROVIDER_BRIDGE.md` (contratos, permisos, riesgos y compatibilidad), `docs/UI_UX_AUDIT_V2.md`, `docs/QA_MATRIX_PHASE03.md`, `docs/PHASE03_ACCEPTANCE.md`, guía de instalación local extensión en Edge/Chrome, y `docs/ISSUES_DECISIONS.md` actualizado. Registrar explícitamente qué pruebas de sitio real se hicieron y cuáles quedaron bloqueadas.
6. No publicar la extensión ni cambios GitHub, no hacer push/commit o instalar herramientas sin autorización. No tocar base de datos privada sin una copia aprobada.

## Condiciones de aceptación

**A — base:** biblioteca, variables, CRUD, búsqueda, favoritos, historial, copiar, importar/exportar, versión/restore y Qwen local no retroceden; build/tests/E2E en PASS real.
**B — UI:** apariencia moderna coherente con QuickPrompt, Tailwind CSS construido localmente sin CDN, light/dark/mobile/teclado/accesibilidad comprobados.
**C — bridge:** ChatGPT y Claude aparecen como destinos. Para cada uno, `PrepareOnly` coloca texto exacto en editor o informa fallback; `PrepareAndSend` solo actúa tras clic explícito y reporta un único intento con idempotencia. Si restricciones del servicio o falta de sesión impiden validación real, marcar **BLOQUEADO POR PROVEEDOR**, no PASS.
**D — privacidad y seguridad:** origen/destinos en allowlist, sin scraping/output/cookies, permisos MV3 mínimos, ausencia de leak en logs, cancelación y manejo de errores.
**E — evidencia:** capturas, logs depurados, tabla requisito→prueba→evidencia, instrucciones reproducibles, regresiones y pendientes honestos.

Estado final: `PASS_LOCAL_PHASE03` solo si A–E (incluidos proveedores reales) están demostrados; `PARTIAL_PROVIDER_BLOCKED` cuando web/UI local pasan pero proveedor externo no pudo comprobarse; `FAIL_NO_GO` ante regresiones/errores críticos; `BLOQUEADO` ante falta de SDK/permiso/instalación. No declarar producción o integración funcional a sitios que no fueron probados realmente.

## Secuencia de trabajo para Codex

Inspecciona → reproduce baseline → registra decisiones/ADR → implementa un slice completo `ChatGPT PrepareOnly + fallback` → E2E → completa `Claude PrepareOnly` → E2E → introduce envío opt-in solo con garantías y pruebas → integra UI/Tailwind en incrementos → regresión completa → informe final. Evita modificaciones enormes no verificadas y no reescribas todos los componentes al mismo tiempo.

### Documentación oficial de referencia

- Chrome MV3 content scripts: https://developer.chrome.com/docs/extensions/develop/concepts/content-scripts
- Chrome MV3 messaging: https://developer.chrome.com/docs/extensions/develop/concepts/messaging
- Chrome permissions: https://developer.chrome.com/docs/extensions/develop/concepts/declare-permissions
- Edge manifest MV3: https://learn.microsoft.com/en-us/microsoft-edge/extensions/getting-started/manifest-format
- Tailwind v4 CLI: https://tailwindcss.com/docs/installation/tailwind-cli
- Blazor JS interop: https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/call-javascript-from-dotnet?view=aspnetcore-8.0
- OpenAI Terms: https://openai.com/policies/terms-of-use/
