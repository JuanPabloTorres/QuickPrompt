# Playwright opcional

Para verlo funcionar en una ventana de Edge, desde la raíz ejecuta `powershell -File scripts/Ver-Playwright.ps1`. Se crea una biblioteca de prueba con los prompts prácticos, se busca uno, se rellenan sus campos y se copia. No se modifica la biblioteca abierta ni se utilizan perfiles personales. La demostración termina y cierra únicamente su navegador/proceso. El video de este recorrido está también en **Empieza aquí: guía y videos** dentro de NOVA.

El runner admite `--headed` para QA visible y `--record` para grabación de QA local, manteniendo la allowlist y las acciones cerradas. Sin esos parámetros conserva su ejecución anterior sin ventana. El motivo de que antes no se viera era `headless: workflow.mode === 'qa'`.

`runner.cjs` interpreta JSON con acciones cerradas: navigate, click, fill, press, expectVisible, expectText y screenshot. Los localizadores requieren role y nombre exacto. No acepta selectores/scripts/cookies/perfiles de usuario; no ejecuta código propuesto por un modelo. No se conecta a Quick Assist como herramienta libre.

La entrada de inicio sí atiende peticiones locales: **«Ve al prompt “…”»**, **«Edita el prompt “…”»**, **«Actualiza el prompt “…” para …»** y **«Actualiza la biblioteca»**. Usa `/api/v1/nova/command` para localizar IDs reales y las APIs de dominio existentes para preparar/guardar cambios. Si hay varios candidatos, falta el título o no se especificó el cambio, pide aclaración. La propuesta de Qwen abre el editor del mismo prompt y **Guardar versión** aplica el cambio. La navegación interna no necesita otro navegador; Playwright verifica ese recorrido real, conforme al SOW.

Para repetir el recorrido de navegación y actualización en una ventana visible, con una biblioteca aislada y Qwen instalado:

```powershell
dotnet build backend/QuickPrompt.Api/QuickPrompt.Api.csproj --no-restore -p:BaseOutputPath=C:/Projects/Repos/QuickPrompt-Web/.playwright/command-build/
node Tests/nova/local-commands.cjs --headed --record
```

QA admite solo loopback. Para una instancia local ya abierta:

```powershell
node automation/playwright/runner.cjs automation/playwright/workflows/library.json automation/playwright/permissions/local.json .playwright/qa-result
```

Es una lectura/captura de la app, sin mutaciones de dominio. Las pruebas de flujos interactivos están en Tests/e2e y usan sus propios datos. Los archivos de workflow/policy son configuración revisada por una persona; no autorización generada por el LLM.

En modo `supervised`, la política puede nombrar hasta10 orígenes HTTP loopback/HTTPS exactos, sin wildcard. Nunca asumir autorización por existir un dominio en una salida del modelo. Solo habilitar sitios y recorridos autorizados por su propietario y sus condiciones aplicables. No se entrega ningún origen externo habilitado.

Cada click/fill/press en un origen externo exige escribir APPROVE en una terminal interactiva, con acción/control/valor visibles. Sin terminal o sin aprobación devuelve BLOCKED. Etiquetas de autenticación/captcha se bloquean; no automatizar inicios de sesión ni introducir credenciales. Toda navegación, incluidas redirecciones/recursos, se restringe a la allowlist. Solicitudes externas no GET/HEAD solo se permiten brevemente tras una aprobación; ese control no reemplaza revisar la acción del sitio. Popups y descargas se cierran/cancelan. No existe reintento automático de envíos.

Máximo20 pasos y120s de ejecución después de crear el contexto; espera por localizador de10s por defecto. Para recorridos con inferencia local, el operador puede fijar `--locator-timeout=90000` (1000..90000ms); se mantiene el límite total del workflow y la cancelación. La configuración del LLM no puede ampliar estos límites. Perfil nuevo sin sesión, sin persistir storageState. Ctrl+C cancela y cierra el contexto; un fallo requiere revisar el resultado y ejecutar un workflow nuevo. El resultado solo contiene índices/tipos/estados/duraciones, sin valores, DOM, URLs privadas ni cookies. Capturas únicamente en pasos screenshot, archivos locales. No compartir capturas que contengan información privada.

Estados: PASS, FAIL, BLOCKED, CANCELLED, TIMEOUT. [Pruebas locales de fallo/cancelación/recuperación/timeout](../../docs/evidence/goal06/automation/automation.json). Supervisión real sobre terceros: NOT_TESTED. Se utiliza Playwright ya instalado; si no está disponible, registrar BLOCKED sin instalar paquetes o navegadores automáticamente.
