# Correo de confirmación y recuperación

El host de cuentas compone `IAccountMail` mediante `AccountMailRegistration`.
El modo local no cambia. Brevo sigue siendo la opción compatible con la configuración
anterior. SMTP2GO y Mailjet son alternativas HTTPS opt-in: no se crea una cuenta, no se instala
un SDK y no se envía correo por activar el módulo o consultar `/account/options`.
La preparación del adaptador no cambia el proveedor publicado ni acredita entrega.

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

El adaptador Brevo se conserva por compatibilidad, sin reactivar su alta después
de que el usuario eligiera cambiar. Su bloqueo era externo a `IAccountMail`.
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
