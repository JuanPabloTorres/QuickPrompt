# Bootstrap público PostgreSQL

## Catálogo y borrado privado

La migración `20261028092140_PrivatePromptErasure` añade `prompts.DeletedAt`, el índice de visibilidad y una función de borrado acotada. `20261028113000_PrivateErasureVisibility` añade políticas SELECT restrictivas para ocultar las tumbas y sus versiones incluso a consultas antiguas, y una política INSERT que impide nuevas versiones sobre ellas. El migrador conserva acceso técnico sujeto a actor/biblioteca; la aplicación no recibe derechos generales de escritura. Revisar y aplicar ambas con el migrador del candidato; no ejecutar el bootstrap de base vacía sobre una base existente.

Después del primer borrado, el mínimo binario compatible debe reconocer `DeletedAt`, rechazar replays contra destinos eliminados y conservar los recibos técnicos. El host actual rechaza un esquema sin la función y las tres políticas. Un binario previo no está soportado: RLS oculta filas vacías, pero no implementa los nuevos contratos de reintento. `Down` de ambas migraciones está bloqueado; recuperar mediante roll-forward compatible. Sólo restaurar un respaldo **posterior a los borrados** comprobando sus tumbas, esquema y aplicación antes de abrir tráfico. Un respaldo anterior contiene contenido eliminado: no puede servirse como rollback sin reaplicar primero un registro exhaustivo y autorizado de borrados; ese procedimiento todavía no está implementado ni certificado. Conservar una copia de seguridad no equivale a autorizar su publicación.

`DELETE /api/accounts/v1/libraries/{biblioteca}/prompts/{prompt}` exige cookie, CSRF y `{ "revision": N, "confirmed": true }`. El actor procede de la sesión. La operación verifica revisión y ausencia de trabajo activo en esa biblioteca, y toma los bloqueos existentes de admisión/colecciones. Vacía título, cuerpo, descripción y defaults del prompt y todas sus versiones, retira membresías y conserva sólo tumbas técnicas y recibos para evitar resurrecciones. No es archivar. Un reintento confirmado con la revisión original devuelve 204; una revisión distinta o trabajo activo devuelve 409. El recurso privado deja de estar disponible en lectura, búsqueda, exportación, edición, restauración, compilación y MCP.

Los snapshots ya publicados, copias importadas por otras cuentas, resultados y borradores recordados son artefactos separados y se conservan según sus controles de revocación/olvido/retención. La confirmación debe explicar esta frontera. Un borrador con origen eliminado queda obsoleto y no autoriza sobrescribirlo; puede guardarse explícitamente como otro prompt. Repetir una captura/importación antigua hacia un destino eliminado no lo recrea ni devuelve contenido vacío como éxito.

La función `qp_library.erase_private_prompt` usa un propietario sin BYPASSRLS, `search_path` fijo y comprobación de actor/biblioteca. FORCE RLS continúa activo; no se concede UPDATE general sobre versiones ni mutación de captures/imports a la aplicación. Las únicas políticas añadidas para el migrador permiten limpieza de colecciones del actor. Triggers impiden reescribir una tumba o insertar nuevas versiones en ella. Los permisos MCP actuales no reciben ninguna operación de borrado.

El contraste de restauración compara también la definición de la política real
`erasure_collection_change`. `Tests/accounts/postgres-restore-contract.ps1` ejercita
la consulta del script propietario en dos bases desechables del contenedor de
desarrollo: reproduce la omisión de la política anterior, exige detectar sus cambios
y verifica el contrato tras `pg_dump`/`pg_restore`. No lee bibliotecas privadas ni
el respaldo de producción. Ejecutarlo con un directorio nuevo de evidencia:

```powershell
./Tests/accounts/postgres-restore-contract.ps1 -EvidenceDirectory .playwright/restore-policy-contrast
```

Este contraste no sustituye `scripts/Test-QuickPromptPostgresRestore.ps1`, que valida
la base sintética completa, ni acredita recuperación de producción, retención,
RPO/RTO o disponibilidad después de la caducidad del proveedor gratuito. Los
recibos deben distinguir esas comprobaciones; una copia con ACL privada no implica
cifrado del archivo ni una copia recuperable fuera del equipo.

La integración `HostKeyRingRestorePostgresTests` añade el recorrido criptográfico:
protege una sesión sintética, realiza `pg_dump`/`pg_restore` en una base nueva y
exige descifrarla con otro proveedor de Data Protection y el certificado original.
Un certificado distinto y otra identidad de aplicación deben fallar. Los
certificados sólo viven en memoria y la base y el archivo del contraste se retiran
al terminar. Requiere el mismo contenedor aislado y `QUICKPROMPT_TEST_POSTGRES=1`;
se incluye en la suite de cuentas. Su resultado debe proceder de una ejecución real,
no de compilar la prueba. Tampoco implementa el registro de borrados posteriores
al respaldo ni demuestra recuperación de datos privados del proveedor.

## Recuperación operativa y límites del piloto

El candidato `4236c3b69fee2a3e628e1a74c737aae31ef65d7e` pasó CI
`40736397613`. La evidencia descargada verifica restauración de filas y catálogo
sintéticos, 20 tablas FORCE RLS, tumbas sin contenido, detección de cambios de
políticas y descifrado de una sesión anterior al respaldo con un proveedor nuevo.
Los controles negativos de certificado y aplicación distintos también pasaron.
Esto cierra esos contratos sintéticos; no restaura el respaldo privado de Render
ni verifica copias externas automáticas, retención o un objetivo RPO/RTO comercial.

Antes de recuperar un destino publicado:

1. Identificar el artefacto compatible, las migraciones aplicadas y el respaldo por
   SHA-256. Conservar el mínimo compatible con borrado privado; no ejecutar `Down`
   ni desplegar el binario anterior a ese contrato como rollback de emergencia.
2. Mantener cerrado el tráfico y la admisión de trabajos mientras se comprueban
   las condiciones de recuperación. La autorización actual del respaldo real
   permite sólo su carpeta privada de Windows; no autoriza copiar datos privados
   a QA, artefactos de CI ni otro proveedor.
3. Verificar que el respaldo incorpora los borrados exigidos. Si es anterior,
   exigir un registro exhaustivo y autorizado, independiente del contenido perdido,
   antes de servirlo. Sin ese registro, detener la recuperación: cifrar o restaurar
   técnicamente el archivo no evita resucitar datos eliminados. Este registro
   todavía no está implementado.
4. Recuperar el material criptográfico autorizado junto con el keyring: los PFX
   actuales y anteriores que aún descifran claves retenidas, sus contraseñas y las
   claves de firma/cifrado de delegación. No exportar claves privadas sin cifrar,
   crear credenciales sustitutivas ni usar el disco efímero de Vercel como respaldo.
   Una ACL privada protege acceso local; no cifra el archivo de respaldo.
5. Verificar la identidad lógica del host. `AccountHost` deriva la identidad de
   Data Protection y la cookie del origen canónico y del nombre de la base. Un
   destino con otro nombre de base cambia ese aislamiento y requiere iniciar
   sesión de nuevo. La prueba sintética fija explícitamente su identidad de
   aplicación; no demuestra continuidad automática de cookies de un host renombrado.
6. Antes de abrir tráfico, comprobar aislamiento positivo/negativo de cuentas,
   roles y RLS, revisiones, borrados, claves y controles de reintento. Revisar también
   consentimientos y revocaciones posteriores al respaldo: restaurar una fila
   anterior a una revocación no autoriza reactivar esa conexión. La continuidad o
   invalidación deliberada de sesiones y delegaciones necesita un recorrido
   operativo propio, todavía pendiente en el destino publicado.

El cierre comercial exige además un destino de copia externa autorizado, cifrado
y recuperación verificados, conservación y eliminación acordadas, responsable de
operación y objetivos explícitos de pérdida de datos (RPO) y tiempo de recuperación
(RTO), medidos en un ejercicio real. No hay aquí un cron, una política de retención
seleccionada, una carga de datos privados ni un cambio de infraestructura implícito.
La caducidad del PostgreSQL gratuito tampoco se amplía mediante estos controles.

La admisión de compilación/ejecución local revalida con el propietario de aplicación la identidad, biblioteca, revisión y texto capturados bajo el mismo bloqueo 5283 que usa el borrado. Una lectura anterior al borrado no permite encolar después su contenido. Si la admisión gana, el trabajo activo bloquea el borrado; si gana el borrado, se rechaza la admisión. Los recibos existentes siguen disponibles tras edición/archivo ordinarios, pero no autorizan reutilizar un origen eliminado. El trabajador no recibe acceso SQL a bibliotecas.

`GET /api/accounts/v1/libraries/{biblioteca}/prompts/catalog` es un adaptador del buscador PostgreSQL existente. Acepta `search` (hasta 200 caracteres), `filter=all|favorites|recent|archived`, `collectionId`, `ungrouped`, `page` (0–10000), `size` (1–100) y `sort=updated|title|recent`. Devuelve `{items,total,page,size}`; una colección vacía no amplía la búsqueda y una ajena/no disponible se rechaza. La ruta anterior `/prompts` conserva su respuesta array. Los DTO añaden metadata y nombres de variables calculados por su propietario del servidor; nunca incluyen valores privados por defecto.

`POST /prompts/{id}/favorite`, `/archive`, `/restore` y `/duplicate` reciben `{revision}` y reutilizan el propietario de dominio. `/use` conserva el contrato existente de valores, texto esperado e identidad de operación: no permite asignar arbitrariamente `LastUsedAt`. Editar sólo título/cuerpo no borra favoritos, archivo o metadatos ya guardados.

Los resultados privados pueden incluir `usage: {values,operationId}` verificado contra el trabajo persistido y recompilado por el propietario existente. Su UUID es la ejecución: copiar tras recargar no duplica un uso ya registrado. Abrir/compilar/consultar un resultado no cuenta. El servicio compañero registra sólo la finalización local aceptada con éxito; error, parada o cancelación no cuentan. Si una edición concurrente impide registrar estadísticas, el resultado completado permanece disponible y no se fuerza el contador de la nueva revisión. Los snapshots históricos incompatibles no reciben recibo de uso; no se amplían privilegios para repararlos. Los valores sólo se devuelven al actor/biblioteca del resultado dentro de su retención.

La ruta elegida sirve la web desde Vercel y mantiene .NET/PostgreSQL fuera de las
funciones web. Esta guía conserva el bootstrap **específico de Render PostgreSQL**
ya usado para la base vacía del piloto; no selecciona el alojamiento del backend
ni es un bootstrap genérico para otro proveedor. El helper valida `provider`,
`serviceId` y hostname Render: no cambiar sólo el JSON para aparentar compatibilidad.
Para operar la base ya migrada, conservar sus credenciales/roles y no repetir el
bootstrap de vacío. El [estado del piloto](../../docs/development/GO_TO_MARKET.md)
registra caducidad y verificaciones pendientes.

El propietario es `Initialize-PublicPostgres.ps1` con `PublicPostgres.psm1`. Reutiliza
Npgsql del candidato Cloud Release y las migraciones EF de `QuickPrompt.Accounts`;
no cambia el bootstrap Docker local ni arranca aplicaciones. Requiere PowerShell
7.2+, el build Release identificado y el runtime .NET 10.0.14 resuelto por el repo.

El archivo operador es privado, físico y está fuera del repositorio. Su formato es:

```json
{
  "provider": "Render",
  "serviceId": "dpg-IDENTIFICADOR-a",
  "externalDatabaseUrl": "postgresql://USUARIO:CLAVE_CODIFICADA@HOST_EXTERNO:5432/BASE"
}
```

El helper exige por separado `-ExpectedHost` y `-ExpectedDatabase`, coteja el hostname
con el recurso Render, rechaza localhost, IPs, query strings y destinos ambiguos, y
fija `SSL Mode=VerifyFull` para operador y los tres roles. No toma la URL por CLI.
El operador y los archivos de salida reciben la protección privada existente del
repo; enlaces/junctions y ubicaciones dentro del checkout se rechazan.

Primero comprobar sin escrituras:

```powershell
./deploy/cloud/Initialize-PublicPostgres.ps1 -OperatorFile RUTA_PRIVADA_JSON `
  -ExpectedHost HOST_EXTERNO_VERIFICADO -ExpectedDatabase BASE_VERIFICADA -ProbeOnly
```

La consulta real usa una transacción read-only y devuelve sólo capacidades e
identificadores: TLS, versión, owner/CREATEROLE y conteos de objetos. No imprime
URI, contraseña, SQL sensible ni diagnósticos internos del driver.

Inicializar exige una base dedicada vacía PostgreSQL 16+, operador owner con
CREATEROLE y ninguna coincidencia previa de los tres roles o cinco schemas:

```powershell
./deploy/cloud/Initialize-PublicPostgres.ps1 -OperatorFile RUTA_PRIVADA_JSON `
  -ExpectedHost HOST_EXTERNO_VERIFICADO -ExpectedDatabase BASE_VERIFICADA `
  -OutputDirectory DIRECTORIO_PRIVADO_NUEVO
```

Genera credenciales distintas de 256 bits y las protege **antes** de enviar DDL.
Crea roles sin SUPERUSER/CREATEDB/CREATEROLE/REPLICATION/BYPASSRLS ni membresías entre
ellos. Roles y schemas se crean en una transacción con lock de bootstrap y segunda
comprobación de vacío. No hay `IF NOT EXISTS`, reset de contraseña o apropiación de
objetos existentes. El operador conserva su autoridad administrativa y obtiene SET
sobre el migrador para asignar schemas; esa credencial nunca se sube al runtime web.

Los REVOKE/GRANT y default privileges se ejecutan con `SET LOCAL ROLE` al dueño
migrador y se vuelve al operador al final. Tener SET permite cambiar de rol; no
concede por sí solo los permisos de schemas porque la membresía no hereda. No se
concede CREATE de base al migrador.

La transacción confirma antes de ejecutar EF con la credencial del migrador
limitado y `--no-build`. La salida local de EF queda capturada para evitar imprimir
datos sensibles; los logs/auditoría del proveedor requieren su propia gestión de
acceso. Después se verifican autenticación y atributos de los tres roles, propietarios
de schemas, un smoke de al menos 20 tablas FORCE RLS y permisos limitados del key ring.
Ese conteo no detecta por sí solo una futura tabla tenant añadida sin RLS. Esto no
sustituye las pruebas de acceso cruzado, backup, restauración ni runtime público.

La carpeta privada contiene `postgres-migrator.txt`, `postgres-application.txt`,
`postgres-execution.txt` y `postgres-connections.json` con claves
`migrator`, `application`, `execution`. Sólo las conexiones application/execution
se configuran en Cloud. No imprimir archivos, añadirlos a Git o enviarlos por chat.

Si falla el DDL, su transacción se revierte y los archivos privados se conservan.
La excepción sólo proyecta fase/subfase fija, SQLSTATE permitido, estado del commit
y código de salida EF; no copia mensajes, SQL ni excepciones internas. Verificar con
`-ProbeOnly` que siguen en cero roles/schemas/relaciones del destino. Para diagnosticar
el mismo bootstrap sin conservar DDL, `-BootstrapRecovery ProbeRollback` usa las
credenciales originales y siempre revierte su transacción, sin ejecutar EF. Después
de revisar causa y evidencia, `-BootstrapRecovery RetryRolledBack` permite una
repetición deliberada con **la misma carpeta**, únicamente si la base sigue vacía.
Ningún modo sobrescribe secretos o resetea roles existentes.
Si falla EF, quedan roles/schemas confirmados y posibles migraciones anteriores
aplicadas. No hay retry ni eliminación automática. Revisar el destino y, para una
recuperación deliberada de migraciones con las mismas credenciales, usar el mismo
comando con `-ResumeMigrations`: autentica los roles guardados y ejecuta EF sin volver
a crear roles o schemas. `-ResumeMigrations` no sirve para el estado anterior al commit
DDL porque allí todavía no existen roles. Nunca cambiar a un directorio nuevo para ocultar un fallo.
El key ring se conserva; su `Down` bloquea la destrucción. La compatibilidad de
rollback del binario está limitada por el borrado privado descrito arriba.

Pruebas sin red:

```powershell
./deploy/cloud/Test-PublicPostgresBootstrap.ps1
```

Usan sólo archivos sintéticos privados temporales: parser, permisos, rechazo de
destinos, degradación TLS, sobreescritura, enlaces y contrato SQL. No prueban la
ejecución DDL del proveedor. La consulta read-only real del piloto confirmó una base
vacía y CREATEROLE sin superusuario; su evidencia está en el expediente local QA.

Render no ofrece superuser y no documenta CREATEROLE como garantía general. Medir la
capacidad por instancia. Su URL interna usa certificados autofirmados y no soporta
VerifyFull: este contrato usa el hostname **externo** verificado, sin bajar a Require.
Cuando se elija el backend persistente, restringir la allowlist externa a sus IPs de egreso
verificados y los del operador, conservando acceso de recuperación. El navegador y
el proyecto Vercel no reciben conexiones PostgreSQL ni credenciales de roles. No
añadir rangos arbitrarios ni ampliar reglas para sortear un fallo de conexión.

Fuentes: [conexión/TLS Render](https://render.com/docs/postgresql-creating-connecting),
[credenciales Render](https://render.com/docs/postgresql-credentials),
[ausencia de superuser](https://render.com/docs/postgresql-pg-repack),
[roles PostgreSQL 18](https://www.postgresql.org/docs/18/sql-createrole.html),
[membresía y SET](https://www.postgresql.org/docs/18/role-membership.html).
