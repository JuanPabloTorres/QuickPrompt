# Backend persistente de la ruta híbrida

La ruta elegida es **web React/Vite en Vercel y backend ASP.NET Core/.NET 10
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
`/health` exacto, sin query, con Host físico correcto y sin Origin ajeno: respuesta
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
ticket autorizado 5641363 enviado/abierto. Mailjet sólo se investiga, sin cuenta
creada ni adaptador operativo. No cambiar de proveedor ni añadir dependencias
como si ya hubiera entrega comprobada. El adaptador Brevo existente conserva su
configuración privada opcional en la plantilla y no hace envíos al arrancar.

El modo local conserva loopback si no se elige Public explícitamente. Un dominio,
un listener o una cabecera no habilitan por sí solos el modo público. Las pruebas
puras de OriginProxy no demuestran el comportamiento real de Vercel o del host.
