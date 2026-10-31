# Desarrollo y mantenimiento

## Dependencias y ejecución

La solución web usa **.NET 10 LTS**, fijado por `global.json`. El legado móvil
conserva su toolchain independiente. Con un SDK instalado en una ruta aislada,
seleccionarlo sólo para esta consola antes de restaurar o ejecutar pruebas:

```powershell
$novaDotnet = & ./scripts/Resolve-NovaDotnet.ps1 -ForBuild
$env:DOTNET_ROOT = Split-Path -Parent $novaDotnet
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet --version
dotnet restore QuickPrompt.Nova.sln
```

El resolver no instala software. Acepta `QUICKPROMPT_DOTNET` como configuración
confiable del operador y falla si no cumple el SDK/runtime requerido. Los
lanzadores conservan la compatibilidad de artefactos existentes al leer su
`runtimeconfig.json`; no fuerzan un DLL anterior a usar otra versión major.

[Arranque](../../README.md) y [Core](../../agents/nova-core/README.md). El repositorio no incluye el SDK externo, el entorno Python, modelos, bases privadas ni paquetes instalados. Configurar explícitamente las rutas y el proveedor. No instalar ni descargar para ocultar un fallo de configuración.

## Comprobaciones

Desde la raíz y con las dependencias existentes:

```powershell
node scripts/Check-Documentation.cjs
node --test Tests/tooling/documentation.test.cjs
node scripts/Check-ModuleBoundaries.cjs
npm run test:release --prefix apps/web
npm run build --prefix apps/web
node Tests/nova/prompt-variable-fields.cjs
dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore
dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore -c Release
dotnet test backend/QuickPrompt.Mcp.Tests/QuickPrompt.Mcp.Tests.csproj --no-restore -c Release
```

Ejecutar sólo lo pertinente al cambio y los checks requeridos. Los cambios visuales se prueban en navegador; los de inferencia requieren el proveedor real configurado y resultados independientes. Los arneses de `Tests/nova` tienen perfiles, builds y bibliotecas propios: leer su configuración antes de invocarlos. No reutilizar una base privada para QA.

Los tests de captura positiva de `NovaTelemetry.DebugPoint` pertenecen a Debug.
En Release una prueba específica acredita que ni siquiera con un listener se
evalúa/captura el payload. El gate de CI ejecuta ambos contratos; no habilitar
captura de diagnóstico en producción para satisfacer una expectativa de test.

El avance de cuentas, PostgreSQL y compartición está en
[el expediente del goal](SAAS_DELIVERY.md), separado de la aceptación inicial.

### Integración de cuentas con PostgreSQL

Con Docker iniciado y el SDK resuelto como arriba:

```powershell
./scripts/Start-QuickPromptPostgres.ps1
$env:QUICKPROMPT_TEST_POSTGRES='1'
dotnet test backend/QuickPrompt.Accounts.Tests -c Release
./scripts/Test-QuickPromptPostgresRestore.ps1
./scripts/Migrate-QuickPromptAccounts.ps1 -DatabaseName quickprompt_accounts_local
./scripts/Start-QuickPromptAccounts.ps1 -Background
```

La suite aplica migraciones **sólo** a `quickprompt_development` con el rol migrator;
el host usa app y rechaza privilegios de migración/administrador o tablas sin FORCE
RLS. No migra automáticamente ni apunta a la SQLite privada. El script genera
secretos fuera de Git con ACL privada y no rota los de un volumen existente. La
restauración usa otra base aleatoria y conserva el dump sintético sólo bajo
`.playwright`. No usar estos secretos/datos de desarrollo en una instalación pública.

El host de cuentas es todavía un candidato en loopback, con formularios React de
acceso/confirmación/reset, bibliotecas y edición/publicación de prompts: `/account/options`,
`/account/csrf`, registro/confirmación/login/recuperación/reset/logout-all y
`/api/accounts/v1/libraries`. NOVA autenticado compone las capacidades existentes
mediante un companion emparejado y revocable. Su aceptación final con Qwen y el
navegador está en [el checklist de salida](GO_TO_MARKET.md). POST/PUT requieren la cookie antiforgery y la cabecera
`X-QP-CSRF` obtenida de `/account/csrf`; renovar el token al cambiar de identidad.
`/account/me` identifica la cuenta desde la sesión, nunca desde JSON.

`DevelopmentMail` escribe mensajes de confirmación/reset sólo en la carpeta privada
`LocalAppData/QuickPromptNova/saas-development/mail`; no entrega correo externo.
Los mensajes se separan por hash del destinatario; los enlaces abren el formulario
de confirmación/reset de cuentas. La API
recibe `userId/code` mediante POST con CSRF. No publicar ni imprimir esos enlaces.
El host registra tipos/correlaciones de error; no cuerpos, contraseñas o consultas.

Google se habilita sólo con ambos valores OAuth privados y origen HTTPS. El JSON
oficial descargado puede pasarse a `-GoogleSettingsFile` fuera del repo; el script
verifica el retorno `/signin-google` y sólo usa client ID/secret, con endpoints del
proveedor fijados en código. Un email coincidente no vincula cuentas: el usuario
autenticado debe revalidar contraseña para iniciar `/account/google/link`. La política
de [Google OIDC](https://developers.google.com/identity/openid-connect/openid-connect)
requiere un cliente OAuth propio. El cliente local de Testing ya está creado y la UI
inicia el desafío con POST nativo/CSRF. El login real, logout y segunda entrada con
biblioteca persistente están comprobados. Falta añadir la vinculación en UI.
El callback usa código/PKCE y respuesta query; no guarda
tokens Google ni vincula automáticamente cuentas con el mismo email.

La política `strict-origin` conserva Origin en formularios nativos sin enviar rutas
ni queries como referrer. No sustituirla por `no-referrer`: un POST nativo pasaría a
Origin null y sería rechazado por el filtro de origen exacto. Ese filtro sigue
rechazando orígenes nulos o ajenos. Los logs sólo conservan decisiones booleanas.

HTTPS requiere un certificado válido y confiable para el navegador; el lanzador no
modifica la confianza del equipo. `-UseHttpDevelopment` sirve sólo para comprobar la
API local sin Google. Cookies y claves Data Protection se separan por origen y base;
el lanzador copia DLLs/assets a un snapshot identificado por proceso/puerto. Para QA
usar `-DatabaseName quickprompt_development`; Google usa `quickprompt_accounts_local`.
El hosting público, TLS y el key ring compartido cifrado tienen implementación y
pruebas locales. Antes de dar el piloto por verificado, faltan el recorrido del
artefacto publicado, correo real, continuidad de claves y los demás criterios del
[checklist de salida](GO_TO_MARKET.md).

### Compartición del candidato

La API `/api/accounts/v1/sharing` exige la misma sesión. POST `/publications` recibe
`libraryId/promptId/revision`; POST `/publications/{id}/invite` recibe `email` y devuelve
202 con un mensaje genérico tanto si existe una cuenta confirmada como si no.
GET `/publications?libraryId=...&promptId=...` permite al autor recuperar sus
publicaciones; los identificadores del origen nunca se incluyen en la proyección
de lectura del destinatario. GET `/invitations` devuelve
metadatos; POST `/invitations/{id}/decision` recibe `state` (1 aceptar, 2 rechazar,
3 revocar por el autor). GET `/publications/{id}` exige autor o aceptación vigente.
POST `/publications/{id}/import` recibe `libraryId/operationId`; reutilizar la clave
en otro destino devuelve 409. POST `/publications/{id}/revoke` revoca la publicación.
Toda mutación requiere CSRF y actor de sesión; los DTOs no aceptan owner/actor del
cliente. La UI permite publicar, invitar, aceptar/rechazar, importar y confirmar la
revocación. También hay publicaciones versionadas de colecciones y consumidores
MCP con permisos independientes; sus contratos están en [MCP.md](MCP.md). Las
pruebas API/SDK no sustituyen el recorrido publicado con NOVA y ChatGPT.

Una revocación impide futuras lecturas e importaciones; no retira copias importadas
previamente. Los snapshots/recibos se conservan y el rollback destructivo se rechaza:
restaurar un backup verificado con la aplicación correspondiente. El test de restore
amplía el fingerprint a biblioteca, compartición, memoria y ejecuciones: 20 tablas
con FORCE RLS. El key ring compartido del host tiene permisos propios y no es
contenido de una biblioteca.

### Memoria del candidato

`/account/preferences/` permite consultar, confirmar y olvidar preferencias de la
cuenta autenticada. NOVA y la UI consumen esas preferencias dentro del contexto
privado de la cuenta. No seleccionan endpoints ni credenciales del modelo.

`/api/accounts/v1/libraries/{libraryId}/memory` ofrece GET `/sessions`, GET/DELETE
`/sessions/{id}` y POST `/editor`. El actor procede de la sesión, nunca del DTO.
Conservar exige `confirmed`, `sessionId`, `expectedDraftId/expectedDraftRevision`
y `expectedMemoryRevision`; esta última cambia también al editar sólo valores.
El compilador y transición de borrador pertenecen a Application. Cada transacción
aplica identidad local a la conexión; no retiene una conexión durante inferencia.

La UI conserva y recupera sólo por elección explícita. Un origen cambiado bloquea
sobrescritura; el usuario puede revisar y guardar una copia nueva. Olvidar elimina
el contexto y valores, sin borrar prompts/versiones. Cada cuenta admite hasta 100
contextos; caducan a los siete días y se purgan al acceder a su memoria. La limpieza
física periódica y composición del agente completo siguen pendientes. Las preparaciones
compiladas tienen el almacenamiento separado de resultados descrito a continuación.

### Cola y resultados del candidato

El lanzador requiere también `ConnectionStrings:Executions`, usando el rol
`quickprompt_execution` y el secreto privado `postgres-execution.txt` creado por
`Start-QuickPromptPostgres`. Debe apuntar al mismo host/puerto/base que Accounts.
El guard verifica que ese rol no tenga acceso a schemas de identidad/biblioteca,
no pueda cambiar entradas ni resultados y no sea superuser/BYPASSRLS. La aplicación
sólo lee ejecuciones/resultados de su dueño y actualiza la petición de cancelación.
No se puede usar el rol migrator como fallback de ninguno de los dos.

POST `/api/accounts/v1/libraries/{libraryId}/prompts/{promptId}/render` recibe
`revision/values` y devuelve `PromptRendering`, sin guardar ni inferir.
POST `.../compile` recibe `operationId/revision/values`: verifica origen y variables,
admite una operación única y devuelve 202 con metadatos. Reintentar el mismo ID y
solicitud recupera su recibo, incluso si el original cambió posteriormente. Otro
contenido con el mismo ID devuelve 409. Cada nueva solicitud usa un snapshot privado
de la revisión autorizada; el resultado no modifica el prompt.

GET `.../libraries/{libraryId}/executions` lista las últimas 100; GET `.../{id}`
consulta estado; GET `.../{id}/result` recupera sólo el resultado propio completado;
POST `.../{id}/cancel` solicita detenerlo. Mutaciones exigen CSRF y sesión. Lease,
payload, hash y dueño no se exponen en estos DTOs. La UI ofrece Usar, campos del
servidor, resultado literal, copia y descarga; no presenta compilación como IA.

Política versionada del candidato, común a todos sus dispatchers: cuatro pendientes
por cuenta, 64 globales, una ejecución por cuenta, dos globales, 100 admisiones por
día UTC (incluidas fallidas/canceladas), 15 minutos en cola, lease de 30 s y espera de
procesamiento de hasta 180 s. No hay override por proceso que permita mezclar límites.
Transacciones cortas y bloqueo PostgreSQL coordinan admisión/leases entre instancias;
la espera del procesador ocurre fuera. Una cuenta sin turno reciente precede a la
segunda solicitud de otra. Cancelar o perder lease impide commit posterior; resultado
y estado terminal se guardan juntos. Un trabajo perdido/caducado no se reintenta solo.

La espera se acota aunque un Task devuelva tarde. Esto no mata un proceso externo ni
garantiza detener efectos de un adaptador que ignore cancelación: toda integración
futura debe probar cancelación y cercar cada efecto con autoridad/revisión vigentes.
Todavía faltan NOVA/companion, estados durables de aprobación, SSE y la política de
retención/olvido de entradas y resultados antes de publicación comercial.

`Check-Documentation` verifica enlaces a archivos locales de las guías vigentes y las reglas del repositorio. No verifica contenido, anchors internos, URLs remotas ni todos los informes históricos. `Check-ModuleBoundaries` verifica arquitectura estática, no fiabilidad universal de la IA.

## Candidato del componente local

`Start-QuickPromptAccounts.ps1` habilita delegación en sus arranques HTTPS y crea
claves privadas persistentes por origen/base mediante
`New-QuickPromptDelegationKeys.ps1`. El modo HTTP conserva cuentas sin delegación.
Una configuración directa del host debe declarar
`QuickPrompt:Delegation:Enabled=true` y `QuickPrompt:Delegation:KeyDirectory`;
por defecto la delegación está apagada. Aplicar antes las migraciones con el rol
migrador: el arranque no las aplica y rechaza permisos/RLS incorrectos.

El candidato sintético del incremento A03 está en `https://127.0.0.1:5301/`, con
`quickprompt_development`, separado del acceso Google humano de 5299. No ejecutar
pruebas de cola contra esa base mientras un dispatcher QA externo esté activo:
puede consumir sus fixtures. Verificar el artefacto/PID propio antes de detenerlo.
«Conexiones locales» permite revisar, decidir y revocar autorizaciones.
`dispatchReady` indica el protocolo del servidor; no acredita un equipo o modelo
activo. El [cliente Windows](COMPANION.md) ya se empareja y recibe trabajos; falta
verificar generación exitosa con Ollama activo. Evidencia y límites en
[el expediente SaaS](SAAS_DELIVERY.md).

## Documentación

El [MCP de cuentas](MCP.md) es optativo: `Start-QuickPromptAccounts.ps1` admite
`-McpRedirectUri` para un retorno HTTPS registrado. Activa código PKCE y lectura /
compilación de una biblioteca explícitamente aprobada; no habilita escrituras ni
convierte una conexión MCP en worker local. Sin ese parámetro se conserva el
comportamiento previo. Aplicar la migración aditiva antes del arranque.

Actualizar [el índice](../README.md) y el documento propietario de la capacidad. Evitar repetir comandos, estados y contratos en cada informe. Si una guía queda obsoleta, inspeccionar sus referencias antes de retirarla; mantener un aviso histórico cuando sea evidencia. Los textos retirados del README siguen disponibles en el tag `nova-v1.0.0`.

El número visible pertenece a `apps/web/package.json`; los componentes lo importan. Un informe de mantenimiento no cambia por sí solo la versión, el tag publicado ni la aceptación anterior. Commit/push/publicación requieren una petición del usuario para ese trabajo.

## Skills, agentes y reglas

[AGENTS raíz](../../AGENTS.md) define las fronteras del proyecto y [AGENTS web](../../apps/web/AGENTS.md) las de UI. La [skill local](../../.agents/skills/universal-modular-architecture/SKILL.md) aplica el método de arquitectura y enlaza los propietarios actuales. La skill global se mantiene genérica; las particularidades de QuickPrompt se documentan aquí.

El validador básico de la skill comprueba estructura y recursos locales. El validador oficial depende de PyYAML; si no existe en el entorno, registrar la limitación sin instalar o simular esa dependencia. La validación de estructura no califica el comportamiento de una skill.
