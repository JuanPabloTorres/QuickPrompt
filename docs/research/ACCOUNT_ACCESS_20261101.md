# Registro y recuperación de acceso — 01/11/2026

## Publicación y aceptación pública de Brevo — 02/11/2026

Tras la autorización expresa del titular, Render quedó Live en `e16e4ee`,
despliegue `dep-dbkedkl9fdbs739tt1mg`, con el mismo árbol del candidato aprobado.
El backend informa correo configurado y conserva Google/delegación habilitados.
Se completaron por interfaz el registro, la confirmación recibida y el acceso;
se solicitó recuperación por formulario, se recibió y abrió su enlace y se
consumió mediante la API pública. La contraseña nueva permitió entrar desde
el navegador. Reset reutilizado y contraseña anterior rechazados; dos sesiones
independientes revocadas. Los 21 controles HTTP públicos aprobaron y se cerraron
las sesiones QA. El submit del formulario de reset no se automatizó.

Confirmación en spam y recuperación en inbox: no se generaliza entregabilidad.
No se publicó RC2 completo ni se modificaron Neon, origen, Google o claves.
El [informe propietario de correo](../development/ACCOUNT_MAIL.md) conserva
revisión, límites, incidentes y ubicación de los recibos públicos. Las notas
inferiores de permiso pendiente o SMTP bloqueado son cortes históricos.

## Avance de Brevo — 02/11/2026

El titular pidió usar Brevo después de revisar su confirmación de activación.
Se reutilizaron la aplicación M2M y la credencial DPAPI existentes, sin nuevas
concesiones. El adaptador real obtuvo token 200/envío 201 y recepción en Gmail.
Un segundo recorrido con Identity real, PostgreSQL QA local y dos correos Brevo
recibidos aprobó 15 controles de confirmación, acceso, reset, rechazo de replay
y revocación de dos sesiones. Confirmación en spam; recuperación en inbox.
La selección de transportes pasó 210 pruebas y se preparó Cloud Release.

No se cambió producción: Render conserva `4cb104b` sin configuración de correo;
requiere publicar primero el candidato compatible con M2M. La credencial no salió
de su destino local autorizado. Detalle y recibos en
[ACCOUNT_MAIL](../development/ACCOUNT_MAIL.md), sección del 02/11.
Las notas inferiores de bloqueo del proveedor son históricas.

## Necesidad y diagnóstico

El usuario necesita crear una cuenta y recuperar su contraseña desde QuickPrompt.
Se auditó RC2 y se consultaron los chats «Completa Quick Prompt Web» (cierre,
correo, hosting y Companion) y «Analiza y cierra Quick Prompt» (NOVA y DMR).
No se cambian sus proveedores, credenciales, modelos ni la publicación del candidato.

Observación pública mediante Playwright el 01/11/2026:
`https://project-sxf1z.vercel.app/account/options` respondió 200 con `email=false`,
`google=true`, `delegation=true` y `mailDelivery=unconfigured`.
La pantalla mostraba «Crear una cuenta» deshabilitado. Es evidencia actual del
bloqueo de configuración; no prueba un fallo nuevo del proveedor externo.

El backend ya tenía registro, confirmación, reenvío, recuperación y cambio de
contraseña en `AccountCredentials` y `AccountEndpoints`. `AccountForm` ya componía
todos salvo reenvío. Faltaban rutas directas para registro/recuperación/reenvío y
confirmación de contraseña en el formulario. La falta de correo sigue impidiendo
completar públicamente esos recorridos.

## Decisión y fuentes

Se extienden los propietarios existentes. Una nueva identidad/Firebase Auth o
un nuevo proveedor de correo duplicaría cuentas y requeriría migración y permisos;
un motor de formularios no aporta variación necesaria. Sin dependencias nuevas,
esquema de base nuevo, cambios de políticas de contraseña o bypass de confirmación.

Se consultó documentación actual de ASP.NET Core mediante Context7 y
[confirmación y recuperación de Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm?view=aspnetcore-10.0).
Se conserva su mecanismo: token por correo, confirmación antes de entrar y
restablecimiento mediante Identity. La documentación orienta la integración;
no acredita entrega de correo en QuickPrompt. La configuración operativa sigue
siendo propiedad de [ACCOUNT_MAIL](../development/ACCOUNT_MAIL.md).

## Flujo y contratos implementados

El diagrama previsto se publicó en el chat antes del código. Sus nodos representan:

| Nodo | Propietario | Contrato | Evidencia pertinente |
| --- | --- | --- | --- |
| A–E | `AccountForm` | Rutas login/register/recover/resend; sólo petición explícita | UI y navegador |
| F–G | `IAccountMail.Available`, opciones y formulario | Sin correo: explicar y bloquear envío; conservar login | Pruebas de formulario y observación pública |
| H | `createAccountService`, `AccountCsrf`, limitador | Mutaciones same-origin con CSRF; sin actor aportado por UI | Cliente y HTTP |
| I–J | `AccountCredentials`, Identity, `IAccountMail` | Respuesta condicional sin revelar existencia ni prometer entrega | HTTP y buzón QA |
| K–M | Formulario e Identity | Token sólo en memoria tras retirarlo de URL; confirmación explícita | StrictMode y navegador |
| N–O | Identity y cola de ejecuciones | Restablecer, invalidar sesiones y rechazar reutilización | HTTP y navegador con segunda sesión |
| P | Formulario/servicio | Mostrar error; ofrecer otro enlace | Token inválido e incompleto |

Pseudocódigo:

```text
al abrir una ruta de acceso: elegir su modo; si tiene token, conservarlo en memoria y retirar query
al elegir otro modo: reemplazar ruta, limpiar contraseñas y mensajes, conservar correo
al enviar:
  si registro/recuperación/reenvío y correo no habilitado: no enviar
  si registro/reset y contraseñas no coinciden: mostrar error y no enviar
  si falta token para confirmar/reset: explicar y ofrecer enlace nuevo
  iniciar una única operación cancelable ligada al formulario
  llamar al servicio existente con CSRF, sin reintento automático
  conservar el mensaje condicional; no tratar aceptación como entrega
  después de confirmar/reset: retirar campos sensibles y volver a login
  ante fallo vigente: mostrar error sin aplicar respuestas de formularios retirados
```

`AccountHost.MapAccountPage` incorpora GET de acceso. La plantilla Vercel sirve
esos GET/HEAD como HTML antes del proxy; los POST siguen hacia la API. Es un cambio
local de la plantilla, sin aplicar configuración ni desplegar producción.

La API conserva contraseña de 12–256 caracteres y las reglas de Identity, CSRF,
limitación, confirmación y revocación. La segunda contraseña pertenece sólo a la
UI; no se incorpora al contrato HTTP ni se almacena.

## Validación y límites

Baseline de formularios/cuentas: 39 pruebas aprobadas. Tras el cambio inicial:
53 pruebas de acceso/cuentas aprobadas. Un primer typecheck detectó cuatro usos
de una opción de Testing Library no tipada en el test nuevo; se corrigió y el
typecheck y build de cuentas terminaron con éxito. El build conserva su aviso de
chunk superior a 500 kB; no se modificó el empaquetado por este cambio.

La primera selección `AccountHttpTests` fue demasiado amplia por las clases
partial. Se interrumpió tras observar el fallo NOVA
`ChangedSourceRejectsContentContinuationAndKeepsTheNewerPrompt`: esperaba
`NEEDS_CLARIFICATION`, recibió `INVALID_DECISION` en
`AccountContentClarificationHttpTests.cs:190`. El propietario de NOVA confirmó que
corresponde a su migración de fixtures; no se cambió esa lógica ni su prueba.
No se declara aprobada la suite amplia ni se atribuye el fallo a este incremento.

Una selección limitada ejecutó nueve contratos de rutas, reenvío, CSRF,
caducidad y reset/revocación: 9 aprobados, 0 fallidos, 0 omitidos.
Recibo `.playwright/account-access-20261101/auth-targeted.trx`.
Se ejecutó con `--no-build` para respetar la campaña concurrente de NOVA. El
binario comprobado contiene los cambios de producto y la primera versión de las
cinco pruebas nuevas. Después se precisó únicamente la selección del mensaje
reenviado en una prueba, sin modificar el producto probado.

Tras liberar NOVA la compilación compartida el mismo 01/11, se recompiló
`QuickPrompt.Accounts.Tests` con `--no-restore` y se repitió la selección limitada,
incluida esa aserción actualizada: **9 aprobados, 0 fallidos, 0 omitidos**, en
28 segundos de pruebas. Recibo
`.playwright/account-access-20261101/auth-rebuilt.trx`.
Queda cerrado el pendiente de recompilación de autenticación. No se utilizó DMR,
no se ejecutaron campañas NOVA y no se desplegó el producto.

Además, seis contratos de la plantilla Vercel aprobaron GET/HEAD HTML antes del
proxy, conservando los POST de API. El primer intento usó el entorno JSDOM para
una lectura de archivos y falló; se corrigió seleccionando el entorno Node en ese
archivo de tests. Resultado final: 59 pruebas frontend distintas aprobadas y
TypeScript sin errores. Se conservan los fallos de arnés, sin atribuirlos al producto.

`Tests/accounts/access-browser.cjs` ejecutó Chromium existente contra el host
local 5297, API/Identity reales y `quickprompt_development` en PostgreSQL Docker
55432. No se accedió a bases privadas. El buzón QA usa sólo el namespace SHA-256
de una dirección sintética recién creada. No se exportan enlaces o tokens.

Se comprobaron alta, reenvío explícito, confirmación inválida/válida, login,
recuperación, cambio de contraseña, revocación de otra sesión, rechazo de replay,
rechazo de contraseña anterior y acceso con la nueva. El registro móvil conserva
la ruta al recargar y no tiene desbordamiento horizontal; cero errores de página.
Recibo `.playwright/account-access-20261101/browser/result.json` y capturas
`register-desktop.png`, `register-mobile.png`. Se revisó visualmente la captura móvil.
Un primer arranque del arnés no encontró la revisión de Chromium predeterminada;
se seleccionó el ejecutable existente explícitamente, sin instalar nada.

Para reproducir: compilar con `QUICKPROMPT_WEB_MODE=accounts`, preparar la base
sintética con `scripts/Start-QuickPromptPostgres.ps1`, usar el host local en
`http://127.0.0.1:5297` con `ASPNETCORE_ENVIRONMENT=Development`,
`QuickPrompt__Accounts__DevelopmentMail=true`, delegación deshabilitada y
`QuickPrompt__WebRoot` apuntando al build. Las conexiones limitadas de
Accounts/Executions se cargan privadamente desde los secretos QA existentes.
Ejecutar `node Tests/accounts/access-browser.cjs` con `NODE_PATH` apuntando al
Playwright ya instalado y, si corresponde, `QUICKPROMPT_TEST_CHROMIUM` al
ejecutable Chromium existente. El arnés rechaza opciones sin el buzón de desarrollo
y no admite otro origen. El lanzador privado de esta ejecución quedó en
`.playwright/account-access-20261101/launch-private-qa.ps1`, sin secretos literales.

El diagrama publicado se exportó desde su fuente literal y se inspeccionó el PNG:
16 nodos, 18 conexiones, rombos de decisión y ramas rotuladas verificados en el
SVG exportado. Recibo `flow-render/validation.json`, bajo el mismo directorio de
evidencia. Esto no certifica el visor nativo del chat ni ejecución interna continua.

Al cerrar la fase local quedaban pendientes publicar el candidato y aceptar la
entrega de correo real. El buzón de desarrollo no probaba recepción por Internet,
activación del proveedor ni disponibilidad comercial. En esa fase no se hizo
commit, push o despliegue. La publicación posterior se registra a continuación.

## Publicación acotada ejecutada — 01/11/2026

El usuario autorizó activar correo y publicar cambios. Se publicó la interfaz
desde un worktree aislado `C:/Projects/Repos/QuickPrompt-Auth-Release-20261101`,
basado en el commit público `4cb104bfb874004dd6274e1aef3c9537a834cb3b`.
Sólo se incorporaron el formulario de acceso, el modo/método de reenvío del
cliente, las rutas HTML de Vercel y las pruebas pertinentes. No se desplegó el
checkout RC2 completo ni los experimentos NOVA rechazados.

El corte aislado obtuvo 56 pruebas frontend aprobadas y build correcto. Un primer
intento de pruebas identificó que la revisión antigua no incluía la plantilla
Vercel; se añadió la plantilla revisada y se repitió la selección con éxito.
La sesión CLI existente confirmó la cuenta propietaria y el proyecto correcto;
el conector sólo permitía leer metadata pública y no acceder al proyecto.

Se preparó un staging explícito de 143 archivos de frontend y metadata OAuth/MCP
pública regenerada del backend vigente. El dry-run enumeró esos mismos archivos:
sin bases, claves, archivos de credenciales ni coincidencias del secreto de origen.
SHA-256 del manifiesto:
`48c40770843ffdf8bca810e96e02edce7edfe9c4969cc0ef50e5f5fbd52d373c`.
El secreto del proxy se mantiene como referencia privada de entorno.

Vercel creó un despliegue de producción con `--skip-domain`; antes de promoverlo,
la API autenticada confirmó READY, proyecto y fingerprint del manifiesto.
Se promovió ese mismo artefacto y se comprobó el dominio canónico:

| Propiedad | Resultado |
| --- | --- |
| Web | `https://project-sxf1z.vercel.app` |
| Despliegue nuevo | `dpl_8rQ46btmN7ztTNwTB58XKzHFnaxV` |
| Reversión disponible | `dpl_C2eTt8fwy7ufTJCTxamGCZ6kDTvW` |
| Backend y base | Sin cambios: Render y conexión Neon vigentes |
| Credenciales e inferencia | Sin subir nuevas claves ni cambiar modelos |

La API pública ya contiene todos los POST necesarios. No fue necesario publicar
el cambio de rutas de `AccountHost`: los GET/HEAD nuevos se sirven como SPA en
Vercel, mientras sus POST conservan el proxy y la validación del backend existente.

Aceptación pública con navegador aislado y HTTP: las seis rutas de acceso sirven
HTML de cuentas con CSP, no-sniff y referrer policy; registro/reenvío/recuperación
se abren desde sus enlaces; Google y login siguen disponibles; `/account/me`
rechaza anónimos y `/health` responde 200. Los tres discovery OAuth/MCP siguen
siendo JSON del origen canónico. Registro móvil conserva la ruta tras recarga,
sin desbordamiento horizontal ni errores JavaScript. Cero correos enviados,
cuentas creadas o llamadas a modelos durante esta aceptación pública.

Recibos en `.playwright/auth-publication-20261101/`: `deployment-dry.json`,
`source-manifest.json`, `artifact-verified.json`, `canonical-deployment.json`,
`public-acceptance.json`, capturas y `delivery-receipt.json`.
`publication-state.json` registra la secuencia de publicación; no sustituye esos
resultados de HTTP/navegador ni certifica la activación de correo.

### Bloqueo actual de correo y siguiente acción

El panel real de Brevo sigue mostrando que hay que verificar el teléfono antes
de enviar mensajes o crear una clave API. Se abrió «One last step to secure your
account» para el titular, sin enviar otro SMS. El panel transaccional permanece
esperando logs; no se declaró activación del proveedor. La consulta acotada al
correo conectado no encontró una notificación de activación del ticket existente.

El usuario recibió una solicitud de completar esa verificación en Edge mientras
continuaba la publicación. Sigue siendo necesario que Brevo habilite el envío;
después se podrá configurar el transporte privado autorizado y comprobar que se
reciben los enlaces reales de confirmación y recuperación. No se repetirá por
otra vía el sondeo anterior que otro chat registró como rechazado por revisión
automática con «blocked by policy», sin detalle adicional.

La web pública continúa respondiendo `email=false`, `mailDelivery=unconfigured`.
Los formularios explican el bloqueo y no simulan envíos. El resultado actual es
**interfaz publicada y comprobada; correo real todavía pendiente**.

## Cambio de proveedor solicitado y preparación de Mailjet

El usuario descartó Brevo después de no conseguir verificarlo y pidió otro
servicio. Se detuvo ese recorrido, sin borrar cuentas, claves o compatibilidad
histórica. La decisión nueva y el estado operativo se conservan al principio de
[ACCOUNT_MAIL](../development/ACCOUNT_MAIL.md).

Se eligió Mailjet porque el adaptador ya existe y permite validar una dirección
remitente por correo. Su plan Free documenta 6.000 envíos mensuales, 200 diarios y
1.000 contactos. No se activó un plan de pago ni se prometió entrega antes de
comprobarla. No se sustituyó Identity ni se trasladaron cuentas a otro sistema.

En `C:/Projects/Repos/QuickPrompt-Auth-Release-20261101` se portó el módulo de correo
existente y sus dependencias cohesivas desde RC2 a la base pública, conservando
la interfaz `IAccountMail`, selección explícita de un único proveedor, contratos
de enlaces y recibos acotados. Se conserva la respuesta pública condicional ante
fallos del transporte para no revelar existencia de cuentas. Las credenciales
siguen fuera del código y el frontend; no se añadieron paquetes.

Pruebas ejecutadas sobre ese corte:

- 180 contratos HTTP controlados del módulo: 180 PASS, 0 FAIL, 0 SKIP.
- Cinco pruebas HTTP con PostgreSQL QA: respuestas uniformes ante rechazo del
  proveedor en registro/recuperación/reenvío, CSRF y reset/revocación; 5 PASS,
  0 FAIL, 0 SKIP.
- Build Cloud Release: 0 errores y 0 advertencias.

Recibos dentro del worktree aislado:
`.playwright/mailjet-transition-20261101/mail-transports.trx`,
`mail-identity-http.trx` y `mail-source-manifest.json`.
La revisión del diff no encontró cambios de esquema, módulos NOVA o configuración
productiva. Es autorrevisión, no revisión humana independiente.

El usuario indicó que estaba dentro de Mailjet, pero la pestaña accesible continuó
en Login. Su siguiente mensaje aclaró que seguía cargando. Se observó el fallo
de conexión de reCAPTCHA; no se dedujo un rechazo de contraseña ni se aseguró
que existiera una sesión autenticada. No se modificó el CAPTCHA, la privacidad
del navegador o los permisos de la cuenta para evitarlo. El diagnóstico avanzado
de red del navegador no estuvo disponible; no se cambió el estado del navegador
mediante ese intento. Posteriormente cambió la conexión de Edge y se restauró
la página de acceso del mismo proveedor y perfil.

En ese corte, la publicación del backend y configuración de Mailjet quedaron
pendientes de la sesión efectiva, validación del remitente y credenciales.
La UI pública desplegada en la fase anterior permanece en su artefacto aprobado.

## Mailjet descartado; evaluación de Elastic Email

El usuario pidió explícitamente otro servicio después de que Mailjet continuara
sin acceso. Se detuvo su recorrido sin borrar cuentas ni secretos. Se comunicó
el cambio al chat propietario de la entrega, que conserva sus trabajos de
Companion y NOVA. No se cambiaron el backend ni su configuración privada.

La evaluación documental de Elastic Email confirma un plan Email API Free de
3.000 envíos mensuales/100 diarios y verificación de una dirección individual.
Su formulario real de alta cargó en Edge, con Google o correo/contraseña y
aceptación explícita de términos. Se dejó abierto para que el titular complete
esas acciones. No se afirma que la cuenta o el envío estén habilitados.

Contrato candidato comprobado contra el OpenAPI oficial:

- `POST https://api.elasticemail.com/v4/emails/transactional`.
- Cabecera `X-ElasticEmail-ApiKey`; permiso mínimo de envío `SendHttp`.
- Un destinatario en `Recipients.To`, cuerpo y remitente en `Content`.
- `Options.TrackClicks=false` y `TrackOpens=false` para enlaces de Identity.
- HTTP 200 con `TransactionID` y `MessageID`: un recibo de aceptación, no prueba
  de recepción ni de consumo válido del enlace.

Si la cuenta resulta viable, se reutilizarán los contratos existentes de
`IAccountMail`, origen canónico, plazo de 15 segundos, recibo acotado a 16 KiB,
errores saneados y respuestas públicas uniformes. No se añadió código del
proveedor antes de confirmar esa precondición; no se requiere otro SDK.

Fuentes: [precios](https://elasticemail.com/email-api-pricing),
[verificación individual](https://help.elasticemail.com/en/articles/5140589-how-to-verify-your-sender-email),
[OpenAPI v4](https://api.elasticemail.com/public/v4/swagger).
El plan conserva presupuesto cero y requiere una comprobación real posterior
de alta, confirmación, recuperación y revocación. No se repitieron las pruebas
del módulo anterior porque este corte sólo cambia documentación e investigación.

### Alta completada; verificación de envío todavía pendiente

Después de la intervención del titular se observó el panel autenticado de Email
API, con saludo y navegación Start/Activity/Logs/Settings. Esto confirma acceso
al panel en ese instante y sustituye el estado anterior de alta sin enviar.
El aviso `Verify your account to unlock full sending features` continuaba visible.
`Verify account` abrió `Verify with a card`, con botón `Continue to Stripe`;
el proveedor declaraba autorización temporal sin cargo. No se pulsó ese botón,
no se introdujo una tarjeta y no se configuró un plan de pago.

Al abrir Settings > Manage domains, apareció de nuevo `Your Session Has Expired`.
No se deduce la causa de esa expiración ni que el remitente esté verificado.
Se conservó la pestaña para el titular. No hay credenciales de envío ni cambios
de Render. El documento oficial de
[límites de cuentas nuevas](https://help.elasticemail.com/en/articles/2446055-what-are-sending-limits-for-new-accounts)
también exige verificar la cuenta antes de enviar en el plan gratuito.

El usuario pidió mantener seguimiento y continuar el cierre. Se creó y actualizó
un heartbeat en este chat cada 15 minutos, sin avisos repetidos mientras no cambie
el estado y sin ampliar el presupuesto o la autorización. La creación de la
agenda no se presenta como ejecución posterior ni como activación del correo.

### El titular elige continuar sin tarjeta

El usuario prefirió explícitamente una opción sin tarjeta. Se detuvo la ruta de
verificación de Elastic Email, conservando la cuenta y su evidencia. Se actualizó
el seguimiento del chat para no volver a Stripe, Brevo o Mailjet.

Se evaluó EmailJS mediante sus fuentes oficiales: Free de 200 solicitudes/mes,
dos plantillas, alta sin tarjeta y conexión Gmail para uso de volumen muy bajo.
La API REST admite una clave privada `accessToken`; la documentación del SDK
requiere habilitar llamadas no-browser. Antes de implementar habrá que comprobar
que esos controles estén disponibles y exigidos en Free, además de desactivar el
historial de variables para enlaces de Identity. No se confunde una clave pública
con autorización privada. Tampoco se conserva un historial de tokens como
sustituto de la entrega real.

Se abrió el formulario oficial `https://dashboard.emailjs.com/sign-up` en Edge y
se completaron únicamente nombre/correo verificados. Se dejó la introducción y
envío de contraseña nueva, protección antibot y términos al titular. No se creó
una clave ni se concedió acceso a Gmail. Se informó al chat propietario de entrega.
SMTP SendPulse fue descartado en esta revisión para el mismo escenario porque
su guía vigente requiere dominio propio autenticado para salir de SMTP Test;
no se abrió otra cuenta basada en una promesa de remitente Gmail antigua.

Fuentes: [EmailJS Free](https://www.emailjs.com/pricing/),
[conexión Gmail](https://www.emailjs.com/docs/user-guide/connecting-email-services/),
[contrato REST](https://www.emailjs.com/docs/rest-api/send/),
[clave privada](https://www.emailjs.com/docs/sdk/options/),
[SDK oficial y llamadas de servidor](https://github.com/emailjs-com/emailjs-nodejs),
[restricción SendPulse](https://sendpulse.com/knowledge-base/smtp/setup-smtp-server).
Este corte cambia selección, seguimiento y documentación; no agrega otro
adaptador, no cambia el backend público ni ejecuta envíos reales.

### Preparación de EmailJS y sondeo posterior

Después del corte anterior, el titular completó el alta. Se observó el panel
autenticado con 0/200 solicitudes; se abrió la confirmación desde el buzón correcto.
El titular autorizó explícitamente Gmail send, API no-browser protegida con clave
privada, uso privado de esa clave y desactivación de historial de parámetros.
Se guardó la opción de API servidor y se comprobó tras recargar.

Se crearon y guardaron las plantillas `quickprompt_confirm` y `quickprompt_reset`
con contenido fijo en español, variables limitadas a destinatario/enlace, sin
historial privado ni Google Analytics. La copia inicial de plantilla no quedó
guardada y llegó a 404; se conservó la plantilla original y se creó recuperación
como borrador nuevo, comprobando guardado y persistencia de su privacidad.

Se implementaron `EmailJsAccountMail`, `EmailJsMailSettings` y la selección
explícita en el worktree de publicación aislado. El límite de recibos de 16 KiB
y el saneamiento de lecturas interrumpidas se comparten entre JSON y texto.
No se cambiaron Identity, contratos públicos, esquema, NOVA o dependencias.
Pruebas de transporte: 210 PASS, 0 FAIL, 0 SKIP; Cloud Release: 0 errores y
advertencias. Evidencia en `.playwright/emailjs-transition-20261101/` del
worktree aislado: `mail-transports.trx` y `mail-source-manifest.json`.

Google quedó cargando al seleccionar la cuenta, con entrada del navegador y
nativa. La restauración de la ventana permitió continuar plantillas, pero no
demostró reparar Google. Al intentar desconectar la depuración para diagnosticar
el bloqueo, Computer Use terminó automáticamente por no poder determinar con
suficiente certeza la URL actual en Windows; se detuvo el trabajo UI de ese turno.
La causa de la carga y el consentimiento efectivo siguen sin confirmar.

Sondeo del 01/11/2026 22:11 UTC: el inventario autorizado no encontró Edge ni sus
pestañas. No se buscaron sesiones por vías alternativas ni se repitió el alta.
Se actualizó el seguimiento con estos avances para evitar registros, adaptadores
o preguntas de autorización duplicados. No hay entrega real, claves configuradas
ni publicación nueva de backend. El chat propietario indicó que no dispone de
un helper API/CLI Render vigente: antes de publicar hay que inspeccionar servicio,
rama y SHA, conservando Neon y las claves actuales; no usar Deploy latest supuesto.

En ese sondeo, `/account/options` público no respondió dentro de 15 segundos.
El resultado de sus opciones actuales queda sin confirmar; un timeout aislado
no demuestra caída del sitio ni cambia el estado histórico de correo sin activar.
No se reintentó el envío ni se notificó como una nueva caída confirmada.

Sondeo del 01/11/2026 22:41 UTC: Edge reapareció con conexión nueva; EmailJS
conservaba sesión autenticada, 0/200 y ningún servicio. Se preparó una única
conexión Gmail con el ID previsto `quickprompt_accounts`. La selección de cuenta
mediante la herramienta autorizada volvió a terminar en timeout de entrada;
seguía la pantalla Choose an account, sin evidencia de permiso efectivo o conexión.
No se intentó desconectar depuración ni otra ruta tras ese resultado. Se dejaron
ambas pestañas disponibles y se actualizó el seguimiento para limitar próximos
sondeos a lectura hasta un cambio verificable. No hubo nuevo envío, alta o
publicación; se conservó silencio por persistir el bloqueo ya informado.
