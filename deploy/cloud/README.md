# Host público de QuickPrompt

> Destino vigente: **Render Free para la aplicación completa + Neon Free para
> PostgreSQL**, aprobado por el usuario el 01/11/2026, con presupuesto $0.
> La preparación sigue [R1–R6](../../docs/development/GO_TO_MARKET.md). La imagen
> existente incluye React/.NET/NOVA/MCP; la aceptación de Neon y del ingreso HTTPS
> directo sigue pendiente. No retirar Vercel ni cambiar las conexiones del piloto
> hasta comprobar migración, permisos y rollback. Firebase y Railway se conservan
> debajo como evaluaciones históricas; no se activa facturación.

### Preparación del operador Neon

El helper [POSTGRES.md](POSTGRES.md) conserva un único bootstrap y acepta ahora
`provider=Neon`, `serviceId` igual al endpoint `ep-...` y `externalDatabaseUrl`
canónica sin query/fragmento. Sólo admite el endpoint AWS directo y el host exacto
declarado por el operador; no el hostname `-pooler`. `VerifyFull` se impone en
Npgsql, no se deriva de `sslmode=require` de una URL copiada. No guardar claves en
este documento, Git, argumentos de comandos ni salidas de diagnóstico.

El administrador inicial de Neon se usa sólo para preparar la base autorizada;
los roles de aplicación y dispatcher deben crearse con SQL y sin membresía
`neon_superuser`. La consola Neon otorga esa membresía a los roles creados allí,
por lo que no es el mecanismo para crear los usuarios limitados de QuickPrompt.
Los probes reales y las pruebas sintéticas se registran por separado.

### Ingreso Render directo preparado; pendiente de aceptación pública

`QuickPrompt__Hosting__IngressProfile=RenderEdge` es optativo, no se selecciona
por detectar el proveedor. Requiere Public/Production, origen HTTPS exactamente
igual a `RENDER_EXTERNAL_HOSTNAME`, `RENDER=true`, un `RENDER_SERVICE_ID` válido
y listener `http://0.0.0.0:PORT`. Render debe aportar esa metadata; no inventarla
para una instalación fuera de Render. Sólo admite su hostname asignado, sin
dominios personalizados en esta versión. No combinar con IngressOrigin,
OriginSecret, TrustedProxies/TrustedNetworks, HealthCheckHost o auto-forwarding.

Este adaptador se apoya en el [ingreso documentado de Render](https://render.com/docs/web-services):
TLS terminado en su balanceador, HTTP público redirigido a HTTPS y puerto del
contenedor no accesible directamente desde Internet. Conserva el Host exacto y
la política de Origin; retira Forwarded/X-Forwarded-* y el secreto del perfil
anterior, sin obtener identidad o esquema del cliente. Sesión, CSRF, roles y MCP
siguen bajo sus propietarios actuales. No activa el host ni amplía scopes.

Las pruebas locales de ingreso pasan, pero no autoriza un corte ni acredita el
proveedor real. Mantener el piloto hasta comprobar HTTPS/Google/cookies/SSE/MCP
con el candidato y la configuración revisados. La receta `RenderFreeEdgeOnly`
sigue retirada; no es alias del nuevo perfil explícito.

## Preparación de Railway — 01/11/2026

La meta activa conserva [R1–R6](../../docs/development/GO_TO_MARKET.md) como único
roadmap. El manifiesto [railway.json](railway.json) reutiliza el Dockerfile de este
directorio con contexto en la raíz del repositorio: un servicio de aplicación,
una réplica, `/health` y hasta tres reinicios por fallo. En Railway, el campo
Config File debe apuntar a `/deploy/cloud/railway.json`; no cambiar Root Directory
a `deploy/cloud`, porque el Dockerfile necesita los proyectos .NET y la web.
No contiene secretos, no ejecuta migraciones ni selecciona un plan de pago.

**No desplegado.** La cuenta existente pudo leerse por CLI y navegador; el panel
mostró prueba vencida, crédito $0, despliegues pausados y una factura pendiente que
impide cambiar de plan. No se pagó, no se creó otro workspace para eludir ese
bloqueo y no se tocó su proyecto existente. La oferta Free publicada no demuestra
habilitación de esta cuenta. Resolver la cuenta fuera del presupuesto $0 requiere
una decisión del usuario; no ejecutar `up` ni publicar secretos mientras tanto.

El cambio de proveedor requiere comprobar estas fronteras existentes, sin crear
otra implementación de cuentas/NOVA/MCP:

- **Ingreso:** `OriginProxy` exige una cabecera privada sobrescrita por un proxy
  de confianza. Exponer directamente la imagen no lo configura. `TrustedProxy`
  sólo sirve con direcciones reales verificadas y acotadas; no usar comodines,
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED` ni copiar rangos privados supuestos.
- **Salud:** Railway documenta el Host `healthcheck.railway.app`. Su sonda sólo
  puede alcanzar GET/HEAD `/health` exacto y terminal; no equivale a acceso a la
  aplicación. Configurar explícitamente
  `QuickPrompt__Hosting__HealthCheckHost=healthcheck.railway.app` con OriginProxy;
  sin esa opción se conserva el rechazo del Host alternativo. No permite otras
  rutas, métodos, query o un Origin ajeno. El listener existente ya usa `PORT`.
  Verificar HTTP en el destino.
- **Datos:** servicio PostgreSQL separado en el mismo proyecto, con volumen
  persistente, versión compatible, roles limitados y TLS VerifyFull incluyendo
  CA/hostname correctos. No inyectar el `DATABASE_URL` administrativo de la
  plantilla como credencial Accounts o Executions.
- **Claves:** el PFX puede cargarse por la variable privada del inventario. Para
  los PEM, conservar archivos privados montados con `KeyDirectory` o proporcionar
  ambos `QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64` y
  `QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64`, retirando `KeyDirectory`.
  El entrypoint crea un directorio temporal 0700 y archivos 0600, retira las
  variables base64 consumidas y conserva los bytes de las claves estables.
  Rechaza una pareja incompleta, vacía, mal codificada o mezclada con la vía
  montada antes de ejecutar .NET; la validación criptográfica sigue en .NET.
  Esos archivos temporales no son respaldo ni custodia persistente: conservar el
  material autorizado en el almacén privado del proveedor, nunca en la imagen,
  build arguments, logs o Git. No se cargaron claves reales a Railway.
- **Transición:** medir consumo real e inactividad; validar el origen definitivo,
  Google, cookies, MCP, Companion, respaldo cifrado y rollback antes de trasladar
  datos privados. No subir el checkout sucio completo por CLI: preparar y revisar
  un contexto de fuentes permitido, sin `.playwright`, bibliotecas o credenciales.

Fuentes: [configuración como código](https://docs.railway.com/config-as-code/reference),
[healthchecks](https://docs.railway.com/deployments/healthchecks) y
[planes](https://railway.com/pricing). El manifiesto es preparación del candidato,
no evidencia de construcción remota ni de continuidad comercial.

### Comprobar el contenedor antes de publicar

La alternativa de claves por variables usa el mismo propietario .NET que la vía
montada. No modifica scopes, cifrado, persistencia, claves actuales ni resultados
de NOVA. El harness contiene sólo bytes sintéticos: intercepta la invocación a
.NET para inspeccionar entrega, permisos y entorno, por lo que **no** prueba login,
tokens, PostgreSQL o delegación pública. `DelegationKeyFileTests` verifica por
separado firma/cifrado RSA y rechazo de claves débiles, públicas o ausentes.

Desde la raíz, reproducir en PowerShell con Docker disponible:

```powershell
docker build -f deploy/cloud/Dockerfile -t quickprompt-cloud:railway-keys-20261101 .
$checks = (Resolve-Path Tests/cloud/entrypoint-secrets.sh).Path
docker run --pull=never --rm --network none --read-only --tmpfs /tmp:rw,exec,nosuid,nodev,mode=1777 -e ENTRYPOINT_SOURCE=/app/entrypoint.sh --mount "type=bind,source=$checks,target=/checks/entrypoint-secrets.sh,readonly" --entrypoint /bin/sh quickprompt-cloud:railway-keys-20261101 /checks/entrypoint-secrets.sh
dotnet test backend/QuickPrompt.Accounts.Tests/QuickPrompt.Accounts.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~DelegationKeyFileTests
```

`exec` en el tmpfs pertenece al harness: necesita su stub sintético de proceso.
El job `cloud-container` del workflow existente ejecuta build y harness sin red,
puertos ni secretos de producción. Está preparado; el resultado remoto requiere
la ejecución del candidato publicado. Los logs locales y hashes se conservan en
`.playwright/railway-closure-20261101/entrypoint/`. El tag local anterior no es un
tag Git estable ni una publicación en Railway.

> Actualización 01/11/2026: Oracle/OCI fue descartado por el usuario. La nueva
> preferencia vigente es una aplicación y un proveedor de alojamiento gratuito.
> React, API, NOVA y MCP pueden compartir la imagen .NET existente; véase la
> [topología acordada](../../docs/ARCHITECTURE.md) y la
> [evaluación vigente](../../docs/research/CLOUD_FREE_REVIEW_20261101.md).
> [Coolify](COOLIFY.md) queda como alternativa, sin instalación.
> No se cambió el despliegue público ni se aprobó una migración; la ruta híbrida
> descrita abajo conserva su valor como contrato e historial del piloto.

La ruta híbrida histórica usa **web React/Vite en Vercel y backend ASP.NET Core/.NET 10
persistente con PostgreSQL**. Vercel sirve la web en modo cuentas y envía las rutas
necesarias al backend; no ejecuta el agente ni sustituye la cola persistente.
La IA continúa en el companion autorizado del usuario. La guía propietaria del
frontend/proxy es [deploy/vercel](../vercel/README.md).

Estado al 27/10/2026: proyecto Vercel creado y vacío, backend y despliegue público
sin comprobar. La creación no acredita login, correo, MCP o recuperación. Véanse
el [checklist](../../docs/development/GO_TO_MARKET.md) y el
[expediente con cortes históricos](../../docs/development/SAAS_DELIVERY.md).
La receta RenderFreeEdgeOnly fue retirada; no es una opción de configuración.

## 1. Separar identidades y secretos

Usar [.env.template](.env.template) sólo como inventario del backend. Configurar
los valores reales fuera de Git, sin imprimirlos ni ponerlos en `VITE_*`.

| Configuración | Propietario y significado |
| --- | --- |
| `QuickPrompt__Accounts__Origin` | URL HTTPS canónica de la web pública Vercel; cookies, redirects y issuer se ligan a ella. |
| `QuickPrompt__Hosting__IngressOrigin` | Origen HTTPS físico del backend persistente, sin ruta/query ni credenciales. |
| `QuickPrompt__Hosting__IngressProfile=OriginProxy` | Frontera elegida de ingreso autenticado; exige Public fuera de Development. |
| `QuickPrompt__Hosting__OriginSecret` | Secreto privado de exactamente 64 hex, igual al `ORIGIN_SECRET` del proxy Vercel. |
| `QuickPrompt__Hosting__ListenUrl` | Listener HTTP por IP del proceso, separado de la identidad pública; el entrypoint admite `PORT` (10000 por defecto). |
| Conexiones Accounts/Executions | Sólo backend, roles limitados distintos y `SSL Mode=VerifyFull` en ambas. |
| PFX, contraseña y PEM de delegación | Sólo backend; material estable del mismo origen y base, con backup privado separado. |

El proxy debe usar upstream HTTPS literal y **sobrescribir** `X-QP-Origin-Secret`,
no reenviar un valor aportado por el navegador. El backend comprueba Host físico,
Origin permitido y una sola cabecera privada mediante comparación constante;
después retira la cabecera y usa Host/esquema del origen público configurado.
No obtiene identidad ni esquema de X-Forwarded-For/Proto/Host o Forwarded.
OriginProxy prohíbe TrustedProxies/TrustedNetworks y el auto-forwarding.
`TrustedProxy` sigue disponible para otra infraestructura con IP/CIDR explícitos
verificados; no se combina con OriginProxy ni se habilita con comodines.

El acceso directo sin secreto devuelve 403. La única excepción es GET/HEAD
`/health` exacto, sin query, con Host físico correcto o `HealthCheckHost`
explícitamente configurado y sin Origin ajeno: respuesta
terminal que comprueba PostgreSQL, sin sesión, cookie ni capacidad de aplicación.
No usar health como prueba de autenticación o de recorrido completo.

## 2. Conservar PostgreSQL y claves del host

El [bootstrap y recuperación](POSTGRES.md) describe el helper específico de la base
Render del piloto ya migrada. No repetir su bootstrap de vacío ni enviar a Vercel
credenciales de operador/migrador. Mantener roles separados de aplicación,
ejecución y migración, las 20 tablas de contenido con FORCE RLS y VerifyFull.
La base del piloto tiene vencimiento registrado para el 26/11/2026; Vercel no lo
renueva. Backend persistente no significa almacenamiento comercial ya resuelto.

El host usa `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.14:
`AccountDatabase` implementa `IDataProtectionKeyContext`, persiste claves en
`qp_host.data_protection_keys` y las cifra con `ProtectKeysWithCertificate`.
No hay key ring efímero de fallback. El PFX actual requiere RSA privada de al
menos 3072 bits y vigencia; es cifrado del key ring, **no** certificado TLS ni
instalación en el almacén de confianza. Conservarlo con los PEM entre reinicios,
réplicas y reemplazos. El nombre criptográfico permanece ligado al origen/base.

La migración `20261027121059_PersistHostDataProtection` requiere crear antes
`qp_host AUTHORIZATION quickprompt_migrator` si el migrador limitado no puede
crear schemas. El arranque no aplica DDL ni amplía permisos. La aplicación sólo
recibe SELECT/INSERT de claves; no UPDATE/DELETE/TRUNCATE/CREATE. El dispatcher no
recibe USAGE de qp_host. `HostKeyRingGuard` comprueba la frontera al iniciar.
Las claves de host no son contenido por usuario y no usan RLS por usuario.

El rollback del binario conserva schema, claves y certificados. `Down` bloquea
explícitamente la destrucción del key ring; su retirada irreversible requiere un
procedimiento administrativo con backup, no un rollback automático.

Para preparar material nuevo en un directorio privado físico fuera del repo:

```powershell
./deploy/cloud/New-PublicHostingSecrets.ps1 -OutputDirectory 'RUTA_ABSOLUTA_PRIVADA_NUEVA'
```

Requiere PowerShell 7.2+, no sobrescribe y crea `signing.pem`, `encryption.pem`,
`key-ring.pfx`, `key-ring-password.txt` y `key-ring-base64.txt` con permisos privados.
El entrypoint puede decodificar `QUICKPROMPT_KEY_RING_CERTIFICATE_BASE64` con umask
077 y retirar el base64 antes de .NET; la contraseña se configura separadamente.
El soporte genérico de secret files/base64 no depende de elegir Render web.

La rotación usa `UnprotectKeysWithAnyCertificate`: hasta cuatro PFX privados
anteriores en `PreviousKeyRingCertificates`, sólo para descifrar. El entrypoint
admite índices 0..3 como muestra la plantilla. Archivos/directorios físicos, sin
symlinks/junctions; cada certificado se libera al detener. Desplegar primero
lectores compatibles en todas las réplicas, generar/activar claves por el mecanismo
oficial y probar reemplazo/rollback antes de retirar certificados anteriores.
Cambiar el certificado no recifra claves ni fuerza una clave nueva. Las pruebas QA
con proveedores independientes no acreditan propagación o backup públicos.

## 3. Construir y desplegar el mismo candidato

```powershell
docker build --file deploy/cloud/Dockerfile --tag quickprompt-cloud:pilot .
```

Es una instrucción de build, no un resultado ejecutado aquí. El Dockerfile fija
digests de Node/.NET, usa lockfile y publica Cloud con usuario no root; su
[allowlist de contexto](Dockerfile.dockerignore) excluye bases, configuración
privada y claves. La imagen puede conservar el frontend empaquetado; la ruta
pública elegida sirve la web desde Vercel. No necesita Python/Ollama, credenciales
migradoras ni SQLite humano en la imagen.

Elegir el host persistente, configurar listener, HTTPS físico, secretos y las dos
conexiones limitadas. Aplicar migraciones mediante el rol migrador antes de
arrancar. Registrar Google y el cliente MCP para el origen público canónico,
no para una URL QA ni asumir un callback de ejemplo. Los metadatos públicos y las
rutas Vercel se verifican según su guía; no desactivar controles para publicarlos.
Mantener el proceso durante una revisión pendiente: PostgreSQL no reconstruye sus
controles/aprobaciones en RAM tras reiniciar, como explica la guía Vercel.

La imagen local histórica `eac395f765f3cea7737d99bc13ba12cd8f20e3f1208ff0a15fb40bfc25f19586`
precede OriginProxy. No atribuirle sus pruebas ni publicarla como candidato actual
sin rebuild, identificación y comprobación del artefacto resultante.

## 4. Comprobar antes de abrir el piloto

- [ ] Frontend y backend desplegados desde fuentes/artefactos correlacionados.
- [ ] Proxy HTTPS sobrescribe secreto; Host/Origin falsos y acceso directo se rechazan.
- [ ] Cookies, CSRF, issuer, callbacks Google/MCP y redirects usan el origen público.
- [ ] SSE conserva eventos/cursor al renovar, cancela y no restaura aprobaciones.
- [ ] Reinicio/restore conserva datos, claves y permisos; revocación sigue vigente.
- [ ] Dos cuentas aíslan bibliotecas y sólo importan lo compartido y aceptado.
- [ ] Companion y cliente ChatGPT reales completan sus recorridos autorizados.
- [ ] Confirmación/recuperación por correo llegan y sus enlaces funcionan.

Correo sigue bloqueado: Brevo no tiene clave y el usuario reportó SMS fallido;
ticket autorizado 5641363 enviado/abierto. El 31/10 el usuario eligió cambiar:
el adaptador HTTPS Mailjet está implementado sin dependencias nuevas. La cuenta,
el remitente, los secretos y la llegada real siguen pendientes de verificación;
consultar `docs/development/ACCOUNT_MAIL.md`. No presentar la preparación o los
tests controlados como entrega comprobada. El adaptador Brevo existente conserva su
configuración privada opcional en la plantilla y no hace envíos al arrancar.

El modo local conserva loopback si no se elige Public explícitamente. Un dominio,
un listener o una cabecera no habilitan por sí solos el modo público. Las pruebas
puras de OriginProxy no demuestran el comportamiento real de Vercel o del host.
