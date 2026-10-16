# Provider Bridge — Fase 03

El código está en `extensions/quickprompt-browser`; [instalación local Edge/Chrome](../extensions/quickprompt-browser/README.md). Las validaciones de DOM son fixtures, claramente separadas de [sitios reales](evidence/phase03/real-providers.json).

## Arquitectura y contratos

Se mantiene ASP.NET Core/Blazor InteractiveServer, Domain enlazado al legado válido y SQLite v1. `IWebPromptTransfer` es un servicio scoped de JS interop modular; el backend no llama a ChatGPT ni Claude. `PromptTransferRequest` v1 contiene UUID, proveedor, texto compilado, acción, UTC/expiración y acción explícita. IDs de plantilla/revisión son opcionales y la UI no los transfiere. Máximo 20 000 unidades UTF-16, coherente entre .NET y JS; vacío, enum desconocido, versión futura, TTL mayor a dos minutos o caducidad se rechazan. No se admite URL de destino del caller. `LocalOllama` existe como identidad pero no pasa por el puente web.

Ruta: clic en botón local → Blazor valida → módulo JS → postMessage del origen exacto → content script aislado, frame principal → puerto runtime → SW valida emisor/contrato/replay → pestaña root/new-chat permitida → adaptador de compositor. Los resultados/progreso vuelven por el mismo puerto. Las acciones requieren consentimiento por operación; enviar exige además habilitarlo y pulsar el botón de ese proveedor. Un selector, hover o carga de página no transfiere texto.

El content script exige un clic DOM `isTrusted`, una única acción/proveedor y una ventana de 10 segundos para la ida/vuelta de Blazor. Esto no protege de una aplicación local comprometida/XSS; ese origen es un límite de confianza. Sí rechaza otros orígenes/puertos, ventanas y frames, y mensajes sin gesto. El SW vuelve a validar `sender.id`, `frameId`, URL de documento y pestaña. La política de destinos exige host HTTPS exacto, sin credenciales, puerto, query, fragmento ni ruta de conversación. No se navega una conversación existente para reutilizarla.

La reserva de requestId es serial y durable en storage.local antes de tocar el proveedor. Permite hasta 2048 identificadores vigentes; al saturarse falla cerrado. La caducidad impide aceptar el mismo payload cuando se poda. Los últimos 200 resultados/auditorías conservan acción explícita y metadatos, sin texto ni cookies. Storage se restringe a contextos de extensión confiables. No se altera el formato SQLite/JSON de QuickPrompt ni se confunde actividad web con una inferencia local guardada.

## Preparación, envío y cancelación

Los adaptadores separados descubren un único editor visible con selectores específicos, MutationObserver y deadline. No sobrescriben un borrador diferente. Usan texto/valor de textarea, eventos input y verificación literal después del render del editor; HTML dentro del prompt nunca se interpreta. Si el editor normaliza un texto de manera diferente, se ofrece fallback en lugar de enviar una versión alterada.

PrepareAndSend primero prepara y verifica; luego revalida ruta, pestaña activa/ventana enfocada, foco del documento, texto e identidad del editor. La segunda fase marca el intento antes de pulsar un único botón inequívoco. Se observa vaciado del editor o control de detener; solo entonces devuelve SubmittedUI. No lee respuesta, conversación ni tráfico. Un cambio UI no garantiza que el modelo aceptara la consulta; si no se observa, devuelve NeedsManualPaste y no repite el clic.

Cancelar durante la espera impide el envío. Después de un clic no puede deshacerlo; el resultado informa intento sin confirmar y obliga a revisar la web. Cerrar/navegar el origen desconecta el puerto y pide cancelación; no existen jobs pendientes de reinicio ni lotes automáticos. El texto solo permanece en memoria durante la operación; una preparación sin envío no deja una solicitud de commit pendiente.

La auditoría posterior añadió comprobaciones de identidad del editor después de render, consulta ya en curso y autenticación antes del commit. Un control Detener preexistente no sirve para confirmar un envío nuevo. Los observadores respetan el deadline aunque no haya mutaciones; la espera de dos frames también tiene deadline/cancelación, para no dejar pendiente una operación si el navegador suspende animaciones. Si se pierde la confirmación de JS interop o el RPC después de pedir commit, se devuelve un resultado manual incierto, nunca «cancelado antes de enviar» sin prueba. Si falla escribir la auditoría final, se conserva el estado UI realmente observado y se avisa; la reserva previa sigue siendo obligatoria para actuar.

Pruebas de esta revisión: [14 carreras DOM](evidence/phase03/races-final/bridge-races.json), [fallos de API inyectados en MV3 real](evidence/phase03/races-final/worker-failures.json), [extensión Edge](evidence/phase03/races-final/extension-runtime.json) y [Chrome](evidence/phase03/races-final/chrome/extension-runtime.json). Son fixtures/errores simulados identificados; no validan el sitio externo ni equivalen a revocar permisos desde la UI real del navegador.

Estados v1: Ready, ExtensionMissing, ProviderTabOpening, WaitingForEditor, Prepared, SubmissionRequested, SubmittedUI, NeedsManualPaste, LoginRequired, BlockedByProvider, Timeout, Failed y Cancelled. UI ES/EN, botón de cancelar, historial de la sesión y fallback manual independiente. Copiar/abrir conserva texto y no lo registra como enviado; URL fija, sin prompt ni metadatos privados en query.

## Permisos y política

`scripting` y `storage`; no permiso global tabs, cookies, history, debugger, webRequest ni all_urls. Host permissions limitados a los dos hosts HTTPS. El patrón de content script local requiere un guard adicional porque los match patterns no restringen el puerto de la misma manera que un origen exacto. Esta entrega admite únicamente 127.0.0.1:5188; otra configuración requiere un cambio local explícito revisado, no se descubre ni amplía automáticamente.

Claude está **BLOQUEADO POR PROVEEDOR** antes de abrir pestaña o ejecutar adaptador: [términos de consumidor, 3.7](https://www.anthropic.com/legal/consumer-terms), revisados 2026-10-15, restringen acceso automatizado salvo API/permiso expreso. No hay permiso de Anthropic en esta sesión. Se conserva el adaptador para tests DOM etiquetados y se ofrece copia/apertura manual, sin bypass.

ChatGPT: [términos](https://openai.com/policies/terms-of-use/) revisados; no extracción de Output, scraping, evasión de controles ni manipulación de autenticación. La navegación real aislada obtuvo **403 y cero editores**. Preparación/envío reales están **BLOQUEADOS**, no aprobados por los fixtures. Una sesión iniciada por el usuario y validación supervisada son pasos pendientes; no se solicita una contraseña ni se usan perfiles privados para automatizar login.

Fuentes de implementación: [mensajería](https://developer.chrome.com/docs/extensions/develop/concepts/messaging), [scripts aislados](https://developer.chrome.com/docs/extensions/develop/concepts/content-scripts), [scripting](https://developer.chrome.com/docs/extensions/reference/api/scripting), [storage](https://developer.chrome.com/docs/extensions/reference/api/storage), [MV3 Edge](https://learn.microsoft.com/en-us/microsoft-edge/extensions/getting-started/manifest-format). Navegadores realmente ejecutados y versiones constan en los JSON, no se afirma compatibilidad universal con Chromium.
