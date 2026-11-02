# Correo de confirmación y recuperación

## Correo público operativo — 02/11/2026, 15:44 America/La_Paz

El titular autorizó expresamente publicar el candidato probado y guardar la
credencial M2M existente en Render. El servicio `srv-dbgppfu7bikc73b93oog` está
Live en `e16e4ee6c5ea0ae43e2f217b50d680e5b123899e`, despliegue
`dep-dbkedkl9fdbs739tt1mg`. Su árbol Git coincide exactamente con el candidato
aprobado (`b65e5c3fa95719a351815a7e9cb056913d431462`); se reutilizó la revisión
remota idéntica sin force-push ni publicación del checkout RC2 experimental.

Se guardaron las seis variables Brevo con Save only y después se desplegó ese
commit exacto. El primer despliegue, ya iniciado antes del guardado, seguía
informando correo no configurado; ambos recibos permanecen separados. El segundo
expone `email=true`, `mailDelivery=configured`, `google=true`, `delegation=true`.
Neon, el origen, Google, las claves de host y los permisos M2M se conservaron.
El archivo temporal privado usado para importar las variables fue retirado;
la configuración cifrada original permanece disponible localmente.

Aceptación pública en `https://project-sxf1z.vercel.app`: registro por formulario,
confirmación recibida y consumida, acceso al espacio privado, recuperación
solicitada por formulario y enlace recibido. Se abrió la página real de reset,
se comprobó que retira el token de la dirección y se consumió ese mismo token
por la API pública. El envío del formulario de cambio de contraseña no se automatizó.
La nueva contraseña volvió a abrir el espacio privado en el navegador.

Los 21 controles HTTP públicos aprobaron, incluidos CSRF, rechazo anónimo,
cuenta no confirmada, rechazo de reset usado y contraseña anterior, revocación
de dos sesiones independientes y cierre explícito de las sesiones QA. No se
crearon bibliotecas ni prompts. Los datos temporales de acceso se retiraron.
La confirmación pública llegó a SPAM y la recuperación a INBOX; no se certifica
entregabilidad comercial o universal. No se volvió a medir caducidad por tiempo
real; sus pruebas controladas anteriores conservan su alcance.

Evidencia: `QuickPrompt-Auth-Release-20261101/.playwright/brevo-activation-20261102/public/`
contiene `acceptance.json`, `options-configured.json`, `deployment-sequence.json`
y capturas de registro, cuenta confirmada, recuperación y acceso restaurado.
El incidente de espera del arnés tras logout se conservó: la ruta de recuperación
ya estaba abierta, sin fallo del producto. Los cortes inferiores son históricos.

## Activación y entrega real comprobadas — 02/11/2026

Brevo confirmó en el ticket 5641363, a las 12:20 America/La_Paz, que añadió
manualmente el teléfono, validó la cuenta y activó el correo transaccional.
La nueva prueba del adaptador real obtuvo token 200 y envío 201 en 1.102 ms;
Gmail recibió el marcador exacto. El rechazo 403 histórico ya no se reprodujo.

Se ejecutaron registro, confirmación, acceso, recuperación y revocación con
Identity real, PostgreSQL QA local y Brevo real. Se enviaron dos mensajes a un
alias del buzón autorizado; se contrastaron los enlaces recibidos antes de
consumirlos. Los 15 controles aprobaron, incluidos rechazo de reset reutilizado,
revocación de dos sesiones, rechazo de contraseña anterior y acceso con la nueva.
Tokens y contraseñas permanecieron en memoria. No se utilizó la base pública.

Límite observado: el diagnóstico y la confirmación llegaron a spam; la
recuperación llegó a la bandeja de entrada. Brevo usó su dominio de transporte
`12596247.brevosend.com`. Esto acredita recepción en este buzón y ejecución QA,
sin prometer entregabilidad comercial ni llegada universal a la bandeja principal.

La selección de transportes actual pasó 210 pruebas, sin fallos ni omitidas;
el harness y Cloud compilaron y se preparó un publish Release. No se modificó
código de producto para obtener esos resultados. El candidato aislado conserva
la base pública `4cb104b`; no incluye NOVA experimental del checkout RC2.

Render sigue Live en `4cb104b`, rama `deploy/quickprompt-saas-pilot-20261027`,
sin variables de correo visibles en su configuración. Ese backend no admite M2M.
La credencial existente conserva su protección DPAPI local; no se transfirió a
Render ni se modificó producción. Falta aprobar y publicar el candidato compatible,
configurar el secreto en el backend y aceptar los recorridos públicos.

Recibos: `C:/Projects/Repos/QuickPrompt-Auth-Release-20261101/.playwright/brevo-activation-20261102/`:
`real-adapter-send.json`, `inbox-diagnostic.json`, `identity-observations.jsonl`,
`confirm-received.json`, `reset-received.json` y `mail-transports.trx`.
Los apartados inferiores conservan los cortes históricos anteriores a la activación.

## Integración RC2 comprobada — 02/11/2026

El candidato local RC2 incorpora exactamente el adaptador EmailJS preparado por
su propietario en QuickPrompt-Auth-Release-20261101: dos fuentes nuevas, registro
explícito, lector compartido acotado y sus pruebas. No se configura una credencial,
se cambia el proveedor efectivo ni se hace un envío por copiar estas fuentes.
Procedencia y hashes: `.playwright/closure-20261102/mail-owner-integration.json`.
Suite focal actual: **208 PASS, 0 FAIL y 1 opt-in real no ejecutada**; no sumar
el 210 histórico del worktree de origen. Cloud Release compila sin errores ni
advertencias. Recibos `mail-integrated/mail-integrated.trx` y `cloud-final.log`
bajo `.playwright/closure-20261102/`.

El público continúa con `email=false` y `mailDelivery=unconfigured`, comprobado
de nuevo el 02/11. La prueba real de registro/confirmación/reset en navegador usa
exclusivamente buzón privado de desarrollo y PostgreSQL sintético: demuestra
Identity y sus formularios, **no entrega externa**. Falta conectar el servicio
Gmail ya autorizado, configurar las claves fuera de Git y demostrar recepción.
No repetir los proveedores descartados ni una acción previamente rechazada.

Configuración del adaptador integrado: `QuickPrompt:Accounts:MailProvider=EmailJs`
y sección `QuickPrompt:Accounts:EmailJs` con `PublicKey`, `PrivateKey`, `ServiceId`,
`ConfirmTemplate` y `ResetTemplate`, todos juntos. Mantener clave privada obligatoria
en el proveedor y exclusivamente en el backend; las plantillas fijas reciben sólo
`to_email` y `action_url`. No hay fallback ni reintentos. La API oficial conserva
el contrato [POST /send](https://www.emailjs.com/docs/rest-api/send/) y la
[autorización privada](https://www.emailjs.com/docs/sdk/options/), consultados el
02/11/2026. Un recibo OK no demuestra recepción ni confirmación del enlace.

## Decisión vigente — 01/11/2026

El usuario descartó expresamente **Brevo y Mailjet** por no poder completar sus
respectivos accesos/verificaciones. No continuar sus altas ni repetir sus sondeos.
La compatibilidad histórica del código no significa uso del proveedor.

**Decisión posterior del titular: sin tarjeta.** No continuar la verificación
de Elastic Email mediante Stripe. El candidato actual es **EmailJS Free**:
documenta alta sin tarjeta, 200 solicitudes mensuales, dos plantillas y conexión
Gmail para volumen muy bajo. El titular completó el registro y se comprobó el
panel autenticado con cupo 0/200. La confirmación de correo se abrió desde el
buzón correspondiente, sin publicar el enlace.

El usuario autorizó expresamente conectar Gmail para enviar desde su dirección,
habilitar la API de servidor con clave privada obligatoria, usar esa clave sólo
en configuración privada del backend y desactivar historial de variables. No
pedir esas mismas autorizaciones otra vez. Se habilitó la API no-browser y se
comprobó su persistencia tras recargar, con clave privada obligatoria.

Se guardaron `quickprompt_confirm` y `quickprompt_reset`, con asunto y contenido
fijos en español, destinatario `{{to_email}}` y enlace `{{action_url}}`. Ambas
tienen `Do not save private data` marcado y Google Analytics desmarcado; el estado
de recuperación se comprobó nuevamente tras recargar.

El adaptador `EmailJsAccountMail` y su configuración están implementados en el
worktree aislado `C:/Projects/Repos/QuickPrompt-Auth-Release-20261101`. La suite de
transportes ejecutó 210 pruebas: 210 aprobadas, 0 fallidas/omitidas; Cloud Release
compiló con 0 errores/advertencias. Recibos y manifiesto en
`.playwright/emailjs-transition-20261101/`. Son pruebas controladas, sin envío real.

Fuentes: [plan y ausencia de tarjeta](https://www.emailjs.com/pricing/),
[conexión de servicios](https://www.emailjs.com/docs/user-guide/connecting-email-services/),
[API REST](https://www.emailjs.com/docs/rest-api/send/),
[opciones de clave privada](https://www.emailjs.com/docs/sdk/options/).

Pendiente actual: conexión efectiva de Gmail, configuración privada, recepción
real y aceptación de enlaces antes de publicar el backend. La selección de cuenta
Google quedó cargando; el control del navegador terminó automáticamente al no
poder determinar la URL de Windows con suficiente certeza. Los sondeos de
22:11 y 22:26 UTC no encontraron Edge. A las 22:41 UTC volvió la conexión y se
comprobó EmailJS autenticado, con 0/200 y sin servicios. Un único intento de
seleccionar Gmail volvió a terminar en timeout; se conservaron sus pestañas.
No repetir esa selección ni crear otros popups mientras no cambie el estado.
No se configuraron claves reales ni se cambió Render.
El seguimiento de 15 minutos de este chat se actualizó a esa decisión y conserva
silencio si el estado no cambia. No habilita tarjeta ni pagos automáticamente.

## Evaluación anterior de Elastic Email — sin activar tarjeta

Se evaluó **Elastic Email, Email API Free**: su documentación permite verificar
una dirección individual y anuncia 3.000 envíos mensuales, con límite de 100
diarios. El formulario de alta cargó en Edge y ofrece Google o correo/contraseña.
El titular completó el alta: se observó el panel autenticado de Email API con su
saludo. No se debe repetir el registro. El panel todavía exige `Verify account`;
su diálogo ofrece `Verify with a card` mediante Stripe y declara una autorización
temporal sin cargo. No se introdujo tarjeta ni se activó un plan de pago.
Al consultar Domains volvió a aparecer `Your Session Has Expired`. La cuenta
creada no acredita acceso estable, remitente habilitado o entrega.
No se añadió un adaptador ni se cambió Render: primero se comprobará la viabilidad
de la cuenta. No se autorizó un plan de pago ni comprar un dominio.

Fuentes: [plan Email API](https://elasticemail.com/email-api-pricing),
[remitente individual](https://help.elasticemail.com/en/articles/5140589-how-to-verify-your-sender-email),
[contrato OpenAPI v4](https://api.elasticemail.com/public/v4/swagger).

Su cuenta se conserva sin continuar la verificación con tarjeta, por decisión
del titular. Los formularios ya publicados siguen con envíos bloqueados mientras
`/account/options` indique correo sin configurar.

La agenda no certifica acceso continuo ni sustituye la intervención del titular.

## Preparación previa de Mailjet — descartado por el usuario

El módulo existente se preparó en el worktree aislado
`C:/Projects/Repos/QuickPrompt-Auth-Release-20261101`, sobre la revisión pública:
180 contratos de transporte y cinco pruebas HTTP de Identity/PostgreSQL
aprobados, sin fallidos/omitidos; build Cloud Release sin errores ni advertencias.
Son comprobaciones controladas, sin credenciales reales ni envíos al proveedor.

La sesión controlada de Mailjet todavía no accedió al panel: el formulario mostró
`Could not connect to the reCAPTCHA service` y la consola `reCAPTCHA Timeout`.
La causa anterior a ese timeout sigue sin confirmar. La página de estado no
reportaba incidentes y dos scripts públicos de Google/gstatic respondieron 200
desde el host; esto no prueba la conectividad de reCAPTCHA dentro de la sesión.
Se recargó normalmente, sin modificar controles de seguridad ni eludir el desafío.
Después de renovarse la conexión de Edge se restauró una sola pestaña de Mailjet.

No se configuró Mailjet en producción ni se declaró el correo activo. El usuario
pidió sustituirlo después de esos intentos; su activación deja de ser una tarea
pendiente. Se conserva esta evidencia como historial.

Fuentes consultadas: [alta de Mailjet](https://documentation.mailjet.com/hc/en-us/articles/37251169295003--Quick-Start-with-Mailjet),
[remitente](https://documentation.mailjet.com/hc/en-us/articles/360042759253-How-to-add-a-sender-address),
[plan gratuito](https://documentation.mailjet.com/hc/en-us/articles/19154256993051-Understanding-Mailjet-Free-Plan-Limits),
[API v3.1](https://dev.mailjet.com/docs/email-api/send-api-v31/send-basic-email),
[estado](https://status.mailjet.com/).

## Implementación e historial anterior

El host de cuentas compone `IAccountMail` mediante `AccountMailRegistration`.
El modo local no cambia. Brevo sigue siendo la opción compatible con la configuración
anterior. SMTP2GO y Mailjet son alternativas HTTPS opt-in: no se crea una cuenta, no se instala
un SDK y no se envía correo por activar el módulo o consultar `/account/options`.
La preparación del adaptador no cambia el proveedor publicado ni acredita entrega.

**Corte real del 31/10, posterior a la preparación:** aplicación M2M privada creada,
único scope transactional.email:write, secreto real cifrado con DPAPI CurrentUser.
Token HTTP 200, Bearer, 3600 segundos y scope exacto; token no persistido.
El adaptador real llega después al envío, pero recibe 403 permission_denied:
la cuenta SMTP todavía no está activada. Ningún mensaje de diagnóstico fue
aceptado; no se creó una cuenta ni un token de Identity para esos enlaces.
La solicitud limitada de activación se publicó una vez con autorización específica;
el ticket quedó Open. Falta activación, entrega real y aceptación completa de
alta/recuperación. No se cambió producción ni se habilitó el correo público.

La CLI oculta el secreto también en JSON de creación: [hidden] es un marcador,
no una credencial. Ese error de configuración produjo invalid_client; se corrigió
con credentials --reveal-secret mediante su confirmación interactiva oficial,
salida privada y cifrado inmediato. Se comprobó el token real antes de aceptar la
configuración. Logout local terminado y cero coincidencias del secreto en once
archivos privados de texto inspeccionados. Revocación remota CLI no verificada.

Estado observado el 29/10/2026: en la cuenta autorizada de Brevo figura
QuickPrompt `<cuenta-autorizada@example.invalid>` como remitente Verified. No aparecen
claves API/SMTP y la cuenta mantiene la verificación de teléfono pendiente.
El endpoint público `/account/options` informa `mailDelivery=unconfigured` y
`email=false`. Por tanto, remitente verificado no significa correo operativo:
alta/confirmación y recuperación requieren entrega real antes de aceptarse.
Véanse los recibos y límites en [el checklist vigente](GO_TO_MARKET.md).

El 31/10 el usuario pidió cambiar de proveedor. Se detuvo la verificación de Brevo.
La revisión vigente de SMTP2GO exige correo propio de empresa/dominio y rechaza
altas Gmail/Yahoo; disponer de un adaptador y poder verificar remitentes individuales
no elimina ese requisito de alta. Mailjet permite verificar una dirección individual;
su integración está preparada, pero la cuenta, las credenciales, la recepción y el
cambio efectivo de producción deben comprobarse aparte.

## Revisión adicional de Brevo y recibos — 31/10

El usuario retomó Brevo. El panel real mantiene API keys vacías y no contiene
SMTP keys. La llamada de verificación se solicitó una vez; el usuario no recibió
un código utilizable. No se repitió el envío ni se creó una clave.

Se reprodujo una interrupción de lectura en el puerto real `IAccountMail` con
HTTP sintético: **6 FAIL/3 PASS** antes del cambio. `AccountMailContract.ReadReceipt`
posee ahora la traducción común de IOException a `MAIL_UNCONFIRMED`, sin incluir
el diagnóstico privado ni reintentar el envío; la cancelación conserva prioridad.
Los tres proveedores pasan **139 tests/0 FAIL/0 SKIP** después de esta corrección.
Son pruebas controladas, no correo entregado. El cambio es posterior a bbefd38,
local y sin commit/despliegue; la CI de bbefd38 no se atribuye a esta modificación.

Brevo documenta OAuth M2M como alternativa a la clave estática. La metadata pública
observada anuncia `client_credentials` y `transactional.email:write`, con token
endpoint `https://oauth.brevo.com/realms/partner/oauth/token`. El HTML de la guía
M2M mostró un host distinto; se conserva esa discrepancia y se usa discovery como
observación, no como credencial utilizable. La CLI oficial 2.6.0 está instalada
fuera del repositorio. La pantalla pidió además campañas, contactos/CRM y otras
áreas. Se obtuvo autorización específica para ese acceso temporal y para crear
una aplicación M2M privada limitada a transactional.email:write.
El consentimiento se aceptó, pero la CLI agotó 300 segundos sin credenciales.
La reproducción con diagnóstico abrió su receptor local y no observó peticiones
de devolución; la página permaneció en Logging you in. Después se perdió la
conexión de Edge. Eso no prueba la causa de la espera anterior.
En ese primer corte no se creó una app ni un secreto M2M. No repetir el acceso sin una intervención
que permita distinguir su causa; la autorización concedida no se pierde por el
fallo, pero tampoco acredita una credencial utilizable.

El host incorpora ahora M2M opt-in, todavía local y sin publicar. La campaña final
suma **180 PASS/0 FAIL/0 SKIP** con HTTP controlado: token y envío, cancelación,
límite total, configuración ambigua, recibos interrumpidos y concurrencia.
Un control previo reprodujo el rechazo de un token válido sin campo scope;
se corrigió según RFC 6749 §5.1: si se pidió scope explícito, su omisión en la
respuesta conserva ese permiso. Una respuesta con scope diferente se rechaza.
Ambos recibos se conservan; no se atribuyen a CI anterior ni a correo real.

Fuentes: [M2M](https://developers.brevo.com/docs/oauth-m2m-authentication),
[permisos](https://developers.brevo.com/docs/oauth-scopes),
[CLI](https://developers.brevo.com/docs/cli-reference),
[verificación SMTP](https://help.brevo.com/hc/en-us/articles/7959631848850-Create-and-manage-your-SMTP-keys).

## Configuración privada

Configurar juntas estas tres variables fuera de Git:

| Variable | Contenido |
|---|---|
| `QuickPrompt__Accounts__Brevo__ApiKey` | Clave privada de la API de Brevo. No colocar en el frontend, una URL, documentación o el chat. |
| `QuickPrompt__Accounts__Brevo__SenderEmail` | Dirección de remitente configurada y verificada en Brevo. |
| `QuickPrompt__Accounts__Brevo__SenderName` | Nombre visible del remitente; de 1 a 100 caracteres. |

Para seleccionar SMTP2GO, configurar **explícitamente**
`QuickPrompt__Accounts__MailProvider=Smtp2Go` y las tres variables equivalentes
`QuickPrompt__Accounts__Smtp2Go__ApiKey`, `SenderEmail` y `SenderName`, con esos prefijos.
Retirar las tres opciones de Brevo. `MailProvider=Brevo` también es válido; omitir
el selector conserva únicamente la configuración Brevo anterior. Otros valores,
SMTP2GO sin selector, mezcla de proveedores o configuración parcial fallan al
componer el host. No hay fallback entre proveedores. Un selector sin credenciales
conserva `mailDelivery=unconfigured`; no demuestra disponibilidad del proveedor.

Para Mailjet usar `QuickPrompt__Accounts__MailProvider=Mailjet` y configurar juntas:

| Variable | Contenido privado o de remitente |
|---|---|
| `QuickPrompt__Accounts__Mailjet__ApiKey` | Clave API de la cuenta de correo autorizada. |
| `QuickPrompt__Accounts__Mailjet__ApiSecret` | Secreto correspondiente a esa clave; nunca en Git, web, chat o logs. |
| `QuickPrompt__Accounts__Mailjet__SenderEmail` | Dirección validada y activa en Mailjet. |
| `QuickPrompt__Accounts__Mailjet__SenderName` | Nombre de remitente, de 1 a 100 caracteres. |

Retirar todas las opciones del proveedor anterior antes de activar el nuevo.
Mailjet sin selector, una pareja de claves incompleta o proveedores mezclados
fallan al componer el host. No hay sustitución automática ni ampliación de permisos
de usuario. El selector predeterminado de configuraciones anteriores sigue siendo Brevo.

El origen público de cuentas conserva su configuración en
`QuickPrompt__Accounts__Origin`, con HTTPS, sin rutas, credenciales, query o
fragmento. No es el endpoint de Brevo y nunca se obtiene del mensaje del usuario.

Con las tres opciones ausentes, el correo externo queda sin configurar. Una
configuración parcial o inválida falla al componer el host. No se admite combinar
cualquier proveedor externo con `QuickPrompt__Accounts__DevelopmentMail=true`. El buzón de desarrollo
sólo se selecciona explícitamente dentro de Development; en Production no se
convierte en una entrega externa.

## M2M de Brevo: preparación local, pendiente de aceptación real

Requiere el código posterior a bbefd38. El panel de Render confirmó el 31/10 que
el backend público sigue Live en 4cb104b, que sólo admite la clave API anterior:
**no configurar M2M allí sin publicar primero el candidato compatible mediante
una operación aprobada**. Una configuración parcial puede impedir el inicio.

| Variable privada | Valor o significado |
|---|---|
| `QuickPrompt__Accounts__MailProvider` | `Brevo` |
| `QuickPrompt__Accounts__Brevo__AuthMode` | `ClientCredentials` |
| `QuickPrompt__Accounts__Brevo__ClientId` | Identificador de la aplicación privada autorizada. |
| `QuickPrompt__Accounts__Brevo__ClientSecret` | Secreto de esa aplicación; fuera de Git, chat, logs y frontend. |
| `QuickPrompt__Accounts__Brevo__SenderEmail` | Remitente verificado. |
| `QuickPrompt__Accounts__Brevo__SenderName` | Nombre visible del remitente. |

Retirar ApiKey y opciones de otros proveedores; no mezclar modos. La configuración
anterior usa ApiKey si AuthMode se omite o vale ApiKey. No se infiere M2M por tener
un secreto. Modo explícito sin credenciales conserva el correo no configurado;
valores parciales o incompatibles se rechazan al componer.

Cada mensaje valida primero finalidad y origen, solicita un token mediante
client_credentials con scope explícito `transactional.email:write` y realiza un
solo envío con Bearer. Ese permiso de Brevo también cubre plantillas y bloqueos:
no es send-only. El adaptador de QuickPrompt sólo usa envío.
Token y envío comparten 15 segundos; no hay caché global, refresh ni reintento.
El token vive en memoria y se valida con expires_in entre 1 y 3600 segundos.

El secreto M2M no tiene una caducidad fija documentada; no hereda la fecha
aprobada para una clave API distinta. Crear la aplicación, guardar el secreto
o ampliar la delegación requiere autorización. Cerrar la sesión local de la CLI
no demuestra revocación de su concesión remota.

La cuenta impone una restricción observada: el token M2M real funciona, pero el
envío devuelve 403 por plataforma SMTP sin activar. Faltan recepción, confirmación
y recuperación reales; la solicitud de activación ya fue enviada con autorización.
El remitente figura Verified, pero el panel advierte del dominio freemail y
su autenticación. No se modificó el remitente ni se certificó entregabilidad.

Referencia: [RFC 6749 §5.1](https://www.rfc-editor.org/rfc/rfc6749#section-5.1).
Activación separada:
[guía oficial de Brevo](https://help.brevo.com/hc/en-us/articles/115000188150-Troubleshooting-Issues-with-Brevo-SMTP).

## Contrato del adaptador

`BrevoAccountMail` envía una sola petición JSON a
`https://api.brevo.com/v3/smtp/email`, con una dirección destinataria, remitente,
asunto y cuerpo de texto. No permite adjuntos, campañas, destinatarios adicionales,
URLs de proveedor configurables ni contenido arbitrario de un agente.
`Smtp2GoAccountMail` usa `https://api.smtp2go.com/v3/email/send`, autenticación en
`X-Smtp2go-Api-Key` y el mismo límite de un destinatario. Sólo transmite `sender`,
`to`, `subject` y `text_body`. El enlace, el cuerpo y las comprobaciones comunes
pertenecen a `AccountMailContract`; Identity conserva la autoridad sobre los tokens.

`MailjetAccountMail` usa únicamente `https://api.mailjet.com/v3.1/send`
con Basic Authentication de clave/secreto y un único elemento `Messages`.
Se conserva el casing del API (`Messages`, `From`, `To`, `Subject`, `TextPart`),
sin los defaults camelCase de JSON web. No se añaden CC/BCC, adjuntos, campañas
ni sandbox; el endpoint no procede del contenido de un usuario o del agente.

Todos los adaptadores aceptan sólo finalidades `confirm` y `reset`. El enlace debe pertenecer al origen
canónico, usar `/account/confirm` o `/account/reset` según corresponda y conservar
el contrato `userId`/`code` de `AccountCredentials`. El adaptador no genera códigos,
no confirma correos por sí mismo y no cambia contraseñas.

El cliente HTTP no sigue redirecciones ni comparte cookies. Sus loggers se
deshabilitan sólo para este cliente y las cabeceras se marcan para redacción.
La operación completa, incluida lectura del recibo, tiene un límite de 15 segundos
y respeta la cancelación del usuario. El recibo tiene un máximo de 16 KiB.

En Brevo, una respuesta 201 con `messageId` no vacío confirma aceptación de la API.
En SMTP2GO se exige 200, `data.succeeded=1`, `failed=0`, `failures=[]` y un `email_id`
no vacío. Mailjet exige 200, un solo elemento `Messages` con `Status=success`, un
único `To` para el destinatario solicitado, `MessageID` entero positivo y
`MessageUUID` no vacío. CC/BCC o errores adicionales no permiten darlo por aceptado.
Un 200 sin ese recibo no es éxito. La aceptación de cualquiera de las APIs
**no demuestra recepción en la bandeja de entrada**. Otros estados o recibos
incompletos fallan con un diagnóstico seguro (`MAIL_REJECTED`, `MAIL_RATE_LIMITED`,
`MAIL_UNAVAILABLE`, `MAIL_TIMEOUT` o `MAIL_UNCONFIRMED`). No se conservan ni se
incluyen claves, enlaces con token o respuestas crudas en las excepciones.
No hay reintentos automáticos: ante una respuesta incierta el proveedor podría
haber recibido ya la solicitud. El usuario puede solicitar reenvío explícito
desde el flujo de cuentas.

La frontera `AccountCredentials` conserva el 202 condicional de registro,
recuperación y reenvío si el adaptador informa rechazo o envío incierto. Antes de
esta corrección, el fallo del proveedor podía producir 500 para una cuenta y 202
para otra. Ahora registra el evento operativo privado `AccountMailUnconfirmed`
(5296), con finalidad y un código de la lista fija; no registra destinatario,
enlace, token, respuesta del proveedor ni excepción. La cancelación del solicitante
no se convierte en éxito. Esto uniforma estado y cuerpo públicos; no demuestra
indistinguibilidad temporal de todas las rutas ni entrega de correo.

## Comparación de alternativas y límites de $0

Consulta oficial del 29/10/2026: SMTP2GO documenta API HTTPS y verificación de un
dominio o un remitente individual. Su plan gratuito anuncia 1.000 correos/mes y
200/día; sin dominio verificado aplica 25/hora. El límite diario puede encolar
envíos y el mensual rechaza los siguientes; por eso aceptación no implica llegada
inmediata. La actividad gratuita se conserva cinco días. No usar ese registro como
respaldo de auditoría de cuentas. [Plan gratuito](https://support.smtp2go.com/hc/en-gb/articles/223087947-Free-Plan).

La validación individual del remitente es distinta de los requisitos del alta;
dominios con DMARC restrictivo pueden exigir además verificación DNS. No se ha
demostrado que `cuenta-autorizada@example.invalid` sea un remitente aceptado por este
servicio y no se cambió el remitente del piloto. Con un remitente individual el
proveedor puede mostrar su propio dominio de transporte. Para continuidad comercial
conviene un dominio controlado y autenticado; la evaluación de $0 sigue pendiente
de cuenta, condiciones, identidad y entregabilidad reales.
[Remitentes verificados](https://support.smtp2go.com/hc/en-gb/articles/115004408567-Verified-Senders).

**Corrección de la viabilidad del alta (31/10):** aunque SMTP2GO admita remitentes
individuales, su FAQ actual rechaza el registro con Gmail/Yahoo y requiere correo
de dominio propio. La preparación anterior del adaptador no demostraba ese alta.
No se creó allí una cuenta. [FAQ oficial](https://support.smtp2go.com/hc/en-gb/articles/12922807451289-Migrating-to-SMTP2GO-FAQ).

Mailjet permite verificar una dirección individual mediante enlace. El plan Free
documenta 6.000 mensajes/mes, 200/día y 1.000 contactos almacenados. El API puede
crear automáticamente el contacto destinatario; no se importan listas desde
QuickPrompt. El límite diario puede encolar correo y hacer que un enlace de dos
horas llegue vencido: monitorizar capacidad y entrega antes de ofrecer ese volumen.
No se contrata un plan para evitar el límite sin autorización.
[Plan Free](https://documentation.mailjet.com/hc/en-us/articles/19154256993051-Understanding-Mailjet-Free-Plan-Limits),
[contrato de envío](https://dev.mailjet.com/docs/email-api/send-api-v31/send-basic-email).

Mailjet advierte que remitentes Gmail/Yahoo pueden sufrir spam o rechazo y no
permiten configurar SPF/DKIM del dominio ajeno. Un remitente individual validado
no acredita entregabilidad comercial. La recepción real, los límites de cuenta y
la autenticación de un dominio controlado conservan su evaluación separada.
[Dirección individual](https://documentation.mailjet.com/hc/en-us/articles/360042759253-How-to-add-a-sender-address),
[riesgos de freemail](https://documentation.mailjet.com/hc/en-us/articles/19308800104091-Why-you-should-avoid-free-webmail-as-a-sender-address).

El adaptador Brevo se conserva por compatibilidad y vuelve a ser la ruta elegida
por el usuario. El bloqueo de credenciales pertenece a la cuenta externa;
preparar M2M no demuestra que esa cuenta permita usarlo.
SMTP2GO permite otra integración, pero no
garantiza que su propio proceso de alta carezca de comprobaciones. El adaptador
permite evaluar una alternativa sin reescribir Identity ni activar dos proveedores.
Su contrato HTTP y recibo se contrastaron con
[envío por API](https://developers.smtp2go.com/docs/send-an-email).

## Verificación antes de habilitarlo al público

- Crear la cuenta del proveedor y verificar el remitente elegido.
- Mantener la clave en configuración privada y comprobar rotación sin exposición.
- Ejecutar el registro y confirmación reales desde QuickPrompt, después la
  recuperación. Comparar la cuenta afectada y que los enlaces vencidos o usados
  no restauren acceso.
- Comprobar recepción y spam con el remitente real. `Available=true` y un test
  HTTP simulado sólo acreditan configuración/transporte.
- Comprobar los límites vigentes del plan, disponibilidad del proveedor y el
  procedimiento de soporte ante errores o envío incierto.

Pruebas locales focales: `BrevoAccountMailTests`, `Smtp2GoAccountMailTests` y `MailjetAccountMailTests` usan
HTTP sintético, incluidas respuestas ambiguas, cancelación, límite de recibo,
ausencia de redirecciones/cookies y rechazo de configuración mezclada. No usan
credenciales reales, no crean cuentas y no acreditan registro ni recuperación
recibidos en un buzón. Para habilitar SMTP2GO sigue faltando autorización para
crear/configurar la cuenta, verificar el remitente y guardar una clave limitada
al endpoint de envío en el destino privado elegido. No enviar claves por el chat.

La campaña HTTP de la frontera de cuentas preserva tres fallos de reproducción
(registro, recuperación, reenvío) y siete casos finales aprobados: respuesta pública
uniforme ante un adaptador controlado que falla, diagnóstico operativo seguro,
expiración de tokens del proveedor real de Identity con TTL positivo de un segundo
sólo en la fixture, replay de recuperación, revocación de sesiones y CSRF/cuenta
no confirmada. Usa PostgreSQL sintético y protección efímera en memoria; no es una
campaña con el proveedor externo ni una prueba del buzón del usuario. La
implementación de expiración fue contrastada con
[Identity 10.0.14](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Identity/Core/src/DataProtectorTokenProvider.cs).

Observación pública del 29/10/2026 antes del nuevo candidato: `/account/options`
respondió 200 con `mailDelivery=unconfigured` en 2.390 ms. Los tres perfiles privados
históricos de despliegue revisados tampoco tenían opciones completas de Brevo o
SMTP2GO; eso no inspecciona posibles cambios guardados pero no desplegados en el
panel remoto. No existe en esta campaña evidencia de una clave/remitente utilizable
ni autorización para crear una nueva cuenta SMTP2GO. El siguiente paso real exige
seleccionar la cuenta y el remitente autorizados, configurar una clave limitada en
el backend privado y consumir confirmación/recuperación recibidas por el usuario.

Un subdominio web gratuito no concede control del DNS del dominio del proveedor
para autenticar correo. Una dirección Gmail de contacto tampoco acredita un
dominio de envío propio. Brevo advierte que puede reemplazar remitentes de dominios
gratuitos o no autenticados por una dirección compatible; no se promete
entregabilidad comercial con ese mecanismo.

Fuentes primarias consultadas el 27/10/2026:
[envío transaccional](https://developers.brevo.com/docs/send-a-transactional-email),
[contrato HTTP y respuesta 201](https://developers.brevo.com/reference/send-transac-email),
[requisitos de remitentes](https://help.brevo.com/hc/en-us/articles/14925263522578-Comply-with-Gmail-Yahoo-and-Microsoft-s-requirements-for-email-senders),
[cliente HTTP de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-10.0).
