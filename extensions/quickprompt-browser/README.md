# QuickPrompt Local Bridge — instalación local

Extensión MV3 de desarrollo, sin tienda ni publicación. Instalarla en tu perfil es una decisión manual; las pruebas usaron perfiles aislados sin cuentas ni contraseñas.

1. Iniciar la aplicación existente: `dotnet run --project src/QuickPrompt.Web`.
2. Edge: abrir `edge://extensions`, activar **Modo de desarrollador**, **Cargar desempaquetada** y elegir esta carpeta, `extensions/quickprompt-browser`. Chrome: el mismo procedimiento en `chrome://extensions`.
3. Revisar los permisos y recargar **http://127.0.0.1:5188**. `localhost`, HTTPS, otros puertos o LAN no forman parte del origen admitido; no ampliar a dominios remotos.
4. Seleccionar plantilla, completar y pulsar **Usar en… → ChatGPT/Claude**: se compila el texto definitivo. Copiar y abrir queda visible. Para inserción directa, desplegar su opción, revisar texto y consentimiento. El diagnóstico manual está en **Configuración → Configuración avanzada → Comprobar extensión**. Preparar y Enviar son botones diferentes; Enviar está desactivado por defecto y vuelve a desactivarse tras cada operación.
5. Preparar coloca el texto sin pulsar Enviar. El sitio puede leer/guardar el borrador desde ese momento. Enviar solicita un solo clic y exige un cambio observable del compositor; no certifica respuesta ni aceptación del modelo.
6. Si hay sesión caducada, captcha, otro borrador, selector cambiado o timeout, usar **Copiar y abrir**. Autenticarse y pegar manualmente. Nunca reintentar ciegamente un envío cuya confirmación no llegó.

**Claude está bloqueado para automatización en esta distribución**, por la restricción conservada de GOAL04. Copiar y abrir sigue disponible. No hay opción de eludir esa restricción. ChatGPT real tampoco tiene aprobación E2E: en GOAL04 el usuario eligió continuar sin sesión; el HTTP403 del navegador aislado corresponde a Fase03, sin nueva prueba de sitio auténtico. Los PASS de fixtures no son PASS de proveedores. [GOAL05: evidencia actual](../../docs/GOAL05_ACCEPTANCE.md). GOAL06 añade el permiso `sidePanel` y el panel lateral, sin ampliar hosts ni añadir APIs remotas.

GOAL 04 deshabilita Preparar/Enviar si falta capacidad o consentimiento y muestra la causa. Detectar extensión no certifica sesión/editor externo. **Copiar y abrir** no requiere consentimiento de inserción automática: escribe el clipboard y abre la URL fija; debes pegar y enviar manualmente. Se registra COPIED_OPENED, nunca enviado. INSERTED significa texto verificado; SUBMITTED_UI solo una transición observada, sin garantía de entrega al modelo. Los metadatos locales de QuickPrompt aparecen por separado en Historial, sin contenido del prompt. [Estado y evidencia actuales](../../docs/PROVIDER_INTEGRATION_STATUS.md).

Permisos: `sidePanel` para abrir el panel desde el icono de la extensión; `scripting` para ejecutar adaptadores únicamente tras una acción; `storage` para rechazar replays entre suspensiones y conservar los últimos 200 registros de metadatos. Host permissions: solo chatgpt.com y claude.ai sobre HTTPS. Script local con match 127.0.0.1, que vuelve a exigir origen/puerto exactos y frame principal. Sin tabs global, cookies, historial, debugger, webRequest, CDN, analytics, claves API ni salida del modelo. La extensión no observa conversaciones.

No se registra texto del prompt en almacenamiento/logs; durante una operación sí está en memoria y en el editor de destino. El registro `audit` contiene UUID, proveedor, acción explícita, estado, código, duración y versión, máximo 200 entradas locales. Se elimina al desinstalar la extensión o borrar su almacenamiento desde las herramientas del navegador. Los identificadores de replay expiran después de la caducidad del request y un margen; nunca se vuelve a aceptar un request aún vigente.

Para actualizar código: **Recargar** la extensión y recargar QuickPrompt. Para rollback: deshabilitar o quitar la extensión; biblioteca, SQLite, copia e IA local siguen funcionando. No se modifica el perfil de ChatGPT/Claude ni sus credenciales.

Pruebas reproducibles (tooling ya existente):

```powershell
$bridgeRun = 'docs/evidence/bridge-rerun-' + (Get-Date -Format yyyyMMdd-HHmmss)
$env:QP_EVIDENCE_DIR = "$bridgeRun/edge"
node --test Tests/e2e/bridge-contracts.mjs
node Tests/e2e/provider-adapters.cjs # DOM local explícitamente simulado
node Tests/e2e/bridge-races.cjs # DOM local, carreras y deadlines
node Tests/e2e/worker-failures.cjs # MV3 aislado, fallos API inyectados
node Tests/e2e/extension-fixture.cjs # Edge, manifiesto real, fixture enrutado
$env:QP_BROWSER_CHANNEL='chrome'
$env:QP_EVIDENCE_DIR="$bridgeRun/chrome"
node Tests/e2e/extension-fixture.cjs
Remove-Item Env:QP_BROWSER_CHANNEL
Remove-Item Env:QP_EVIDENCE_DIR
```

Ejecutar desde la raíz del repositorio, con 5188 libre. El harness crea su propio proceso/DB/perfil y los cierra; rechaza operar si no recibe readiness de su proceso. No usar un perfil personal con datos privados como directorio de pruebas. `PLAYWRIGHT_MODULE` permite seleccionar la instalación existente, sin descargas automáticas.

## Panel lateral — GOAL06

Tras recargar la extensión y la pestaña local, pulsa su icono para abrir QuickPrompt. **Conectar biblioteca local** consulta la última pestaña QuickPrompt registrada (origen exacto5188). No solicita acceso a tu historial de navegación. Puedes buscar, filtrar favoritos, completar variables y copiar. **Copiar y abrir** usa URLs fijas; pegar y enviar se deciden en la web del proveedor. **Desconectar** elimina los datos de memoria del panel.

El content script registra solo el ID de la pestaña en almacenamiento de sesión. Plantillas y valores no se guardan en storage. Compilar usa el servicio C# y registra un uso en SQLite, igual que Copiar en la aplicación; no se ejecuta el modelo. Se requiere una pestaña local activa/conectada al servidor, aunque esté en segundo plano. Un cambio de revisión exige actualizar la biblioteca.

[Prueba de runtime y documento de panel](../../docs/evidence/goal06/panel/panel.json) en Edge aislado; docking visual NOT_TESTED. Si el panel no se abre en tu navegador, la pestaña web y la copia universal siguen disponibles. [Aceptación completa](../../docs/GOAL06_ACCEPTANCE.md).
