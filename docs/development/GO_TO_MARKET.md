# Checklist de salida al mercado de QuickPrompt

Estado de revisión: **29 de octubre de 2026 · piloto publicado en validación; NO GO comercial**.
La versión visible sigue siendo **1.0.0**. Este documento no crea una versión,
un tag ni una nueva aceptación humana. Registra el piloto cuya publicación fue
autorizada; no certifica una salida comercial. El contrato y los
incidentes conservan su propietario en `SAAS_DELIVERY.md` (expediente local).
Las rutas de expedientes locales entre comillas de código no son enlaces ni
archivos incluidos en este candidato; no son requisitos de instalación.

### Diagnóstico del componente y distinción de plazos del 29/10

Una consulta READ ONLY con el rol limitado de aplicación recuperó el run original
`522641c7-8a67-4992-b35b-efe8d7d75fb2`: FAILED, `MODEL_TIMEOUT`, secuencia 28,
sin efecto ni commit de prompt. Su error se registró a las 13:00:00Z. El trabajo
candidato de esa misma cuenta/biblioteca/conexión y ventana temporal terminó
Cancelled, con cero intentos y `StartedAt=null`. No existe enlace padre-hijo
explícito entre ambos registros: es un indicio de falta de recogida, no prueba de
causalidad ni ejecución de Qwen. No se leyeron paquetes, prompts o respuestas.
Evidencia: `nova-runtime-diagnosis-20261029/run-metadata.json` (expediente local).

Se corrigió la clasificación del puerto existente: al vencer el plazo con último
snapshot Queued y sin inicio, devuelve `LOCAL_DISPATCH_TIMEOUT`. Si observó inicio,
conserva `MODEL_TIMEOUT`. Un claim puede ocurrir entre la lectura y el vencimiento;
el mensaje dice «no se confirmó», sin afirmar que el equipo esté desconectado.
El código tipado y su mensaje seguro atraviesan los adaptadores y liberan el
control sin guardar ni reintentar. No cambian los 180 segundos, permisos o cuotas.
Es código local validado; no se presenta como comportamiento publicado de f49.

El componente Windows ahora conserva sólo metadatos tipados en `last-run.json`,
dentro del directorio privado del perfil: fase, tiempos, PID, hash del DLL al
inicio y causa de salida acotada. No incluye paquetes, credenciales, respuestas
ni mensajes de excepción. `status` lee el recibo como histórico; nunca certifica
actividad por su existencia. La revisión independiente detectó un cierre fuera
del bloqueo y una cancelación mal clasificada en el último trabajo; se corrigieron
manteniendo el bloqueo hasta publicar el final y comprobando la cancelación al
regresar de la ejecución. Faltan esos contrastes en un recorrido real del worker.

Validación local: **7/7** casos PostgreSQL de transporte, **52/52** casos de
propagación/control sin guardado y **27/27** de Companion. PostgreSQL fue la
instancia sintética existente, separada del respaldo privado. Se preservan el
baseline fallido (espera en cola etiquetada MODEL_TIMEOUT) y la primera revisión
fallida: el nuevo código se perdía en una allowlist y el test de respuesta tardía
esperaba 204 pese al contrato existente 409. Se extendió la allowlist propietaria
y se conservó el rechazo 409, sin relajar el cerco de resultados. Las compilaciones
simultáneas chocaron con archivos en uso; la suite general se verificó en salida
aislada sin detener la API local. No se contó ese fallo de entorno como PASS.

Ambos perfiles públicos pasaron consultas reales de autorización; Ollama respondió
200 y listó Qwen3:8b. Ninguna comprobación ejecutó inferencia. A las 14:14:54Z
todavía no existía el nuevo recibo de arranque manual. Edge volvió al inventario,
pero la lectura de pestañas continuó sin responder. No se reenvió la petición.
La fidelidad, el navegador final y la certificación comercial continúan abiertos;
correo, recuperación completa y alojamiento comercial conservan sus puertas.

### Continuación del 29/10: restauración del esquema final y reparación del historial

El plan conjunto `62ba704680b6792e49b9b57449331054f29167380a7ae7de74baeafa0fd66f77`
fue autorizado y ejecutado **una vez**. El respaldo del piloto f49 y su restauración
privada comprobaron 35 tablas, 22 FORCE RLS, 31 IDs exactos de migración y tres
secuencias, con las mismas referencias de filas antes y después de retirar el
acceso temporal. Se verificaron cleanup de origen, eliminación de pgpass, ACL
privada para usuario/SYSTEM y apagado sin listener en 55439. El dump tiene 609.086
bytes, SHA `411e9059596dfe812d4c856710eb65660e882a1518d028207bc2164896546437`.
Esta aprobación ya está consumida. La copia anterior de 33 tablas se conserva.
No se probaron equivalencia completa de catálogo, flags globales de roles,
descifrado de llaves, cookies/revocaciones ni cifrado del dump completo. Tampoco
se creó respaldo externo automático. El resumen seguro queda en
`commercial-followthrough-f49-20261029/final-schema-recovery-summary.json`.

Ollama respondió en 127.0.0.1:11434 y su catálogo confirmó Qwen3:8b instalado. Se
envió una sola petición original de conceptualización, con tres campos explícitos,
en el run `522641c7-8a67-4992-b35b-efe8d7d75fb2`. Llegó al editor/redacción, pero
la observación de la pestaña perdió respuesta; al recuperar la misma ejecución
apareció «No se pudo completar», sin propuesta ni guardado. En esa observación no
se obtuvo el código terminal; la consulta posterior documentada arriba recuperó
`MODEL_TIMEOUT`, sin evidencia de inferencia/worker activo. No se atribuye este
intento a un fallo semántico de Qwen ni se declara fidelidad comprobada.

El historial recuperado mostró un defecto independiente: el observador montado
después de abrir el dock perdía su señal inicial de visibilidad. Dos regresiones
reprodujeron el fallo en COMPLETED y FAILED. `NovaLive` ahora lee el estado actual
del panel y conserva los eventos posteriores. Pasan 48 pruebas de actividad y la
campaña final **482/482** de frontend, sin controles obsoletos de ejecución/guardado.
La primera campaña general 481/482 se conserva: su único fallo contaba la petición
inicial de bibliotecas como tráfico del cambio de tema. Se corrigió la espera de
bootstrap de esa prueba, sin cambiar el producto ni relajar la aserción de tráfico.

El parche web `f49-history-recovery-20261029` se publicó en Vercel como
`dpl_4XcgBioKmEAKYu4m9H1L8waCYF9F`, conservando backend f49 y proxy/configuración.
Los 203 archivos de staging están identificados por el manifiesto
`b9612a45cebde9d54b17261abdfd73f0ca868b23c8a9e4956341b555f1df8a09`.
Las fuentes probadas coinciden con staging; sólo cambian el componente, dos tests
y el ignore de empaquetado respecto de la base Git. Pasaron build de cuentas,
nueve tests de configuración y **14/14** probes públicos anónimos. El JS publicado
`index-2sahKoK5.js` coincide byte a byte con el build local, SHA
`87aa55ae106f4c14933ec4891c9f82e20e1e2beb2090143a1977d2e346ff0e69`.
El primer recibo del smoke mantenía la etiqueta de base f49; se preserva y el
recibo identificado posterior usa el nombre completo del parche. La interacción
del historial en el navegador publicado sigue sin confirmar: el control de esa
pestaña volvió a quedar desconectado. No se repitió la petición NOVA.

El parche está consolidado en el commit `e446aa55353155de4fda01b7c82e269220bded48`,
subido sin force-push a la rama del piloto. Se verificaron los 199 blobs web del
commit contra staging; configuración y tres documentos públicos de discovery son
los cuatro archivos generados adicionales. HEAD e índice normales se conservaron.
Tres preparaciones de commit rechazadas antes de crear revisión quedan registradas;
se retiró ese mecanismo y el commit se compuso desde el árbol inmutable de f49 y
cuatro blobs revisados, sin usar índices. CI **40881780250** terminó **SUCCESS**
sobre ese commit: **2.097 pruebas .NET aprobadas, cero fallos, 21 casos de proveedor
real no ejecutados y 482 pruebas de frontend aprobadas**. Los TRX descargados y
sus hashes están en `commercial-followthrough-f49-20261029/ci-results-summary.json`.
Se contaron los resultados individuales: el contador agregado NotExecuted del
TRX de cuentas indica cero, pero sus 21 resultados individuales y motivos dicen
que requieren la campaña Qwen/Ollama explícita. No se ocultó esa discrepancia ni
se sumaron los omitidos como aprobados. La restauración sintética de CI también
pasó; conserva su separación respecto del respaldo privado real.

Brevo continúa Open, sin respuesta posterior al seguimiento autorizado, y la API
pública sigue `mailDelivery=unconfigured`. Se mantienen **NO GO comercial** y la
versión visible 1.0.0; no hay tag estable nuevo. Los recibos nuevos están en
`commercial-followthrough-f49-20261029` y el smoke identificado en
`commercial-rollout-f49-20261029/public-smoke-history-identified.json`.

### Revisión operativa del 29/10: permisos reales y restricción del hosting

Las consultas originales de `AccountRoleGuard` y `ExecutionRoleGuard` de f49
pasaron **2/2** sobre las conexiones limitadas reales del piloto, dentro de
transacciones READ ONLY. Cubren también permisos por columna, MAINTAIN,
propietarios/RLS de tablas factuales, funciones con search_path fijado, acceso
PUBLIC y triggers habilitados. No se ejecutaron las funciones de negocio ni
mutaciones de catálogo. Esto amplía la comprobación positiva posterior a la
migración; los negativos de deriva siguen siendo pruebas sintéticas de CI.
Recibo local: `commercial-operational-f49-20261029/deployed-role-guards.json`.

El usuario indicó que ambas consolas estaban abiertas. La inspección posterior
encontró los padres PowerShell desde el inicio anterior, pero ningún worker.
Los accesos apuntan a los slots correctos, con límites 12/1 y `-NoExit`; el código
termina tras cinco minutos sin trabajo. Eso explica por qué una ventana abierta
no prueba actividad; no se conserva un código de salida que demuestre la causa
exacta de la parada anterior. Dos consultas reales `status` confirmaron que las
autorizaciones de NOVA y prompts siguen vigentes, sin iniciar workers o modelos.

Se aclaró el ciclo de vida en el cliente local: mensaje al esperar trabajos y al
consumir el lote; el lanzador compartido marca `DETENIDO` después de la salida.
No se cambiaron límites, scopes, proveedor ni permisos. Las 16 pruebas de
Companion pasaron y el script pasó sintaxis. De los 128 archivos fuente de su
cierre, sólo `CompanionRunner.cs` difiere de f49 por esos mensajes; el DLL local
compilado tiene SHA `58c1e5f99da04ed3c064f5e7d3c6c36626d064632aabdc9540b349aef7a75b46`.
Su metadata Git conserva el HEAD del checkout normal, por lo que no se presenta
como el binario CI de f49. El arranque manual nuevo y las pruebas semánticas
públicas siguen pendientes; no se convirtió `status` en una certificación de IA.

La API autenticada del equipo Vercel confirmó `billing.plan=hobby`, activo.
Su [documentación vigente](https://vercel.com/docs/plans/hobby) limita Hobby a uso
personal no comercial. Es una restricción distinta de que la aplicación funcione:
no corresponde certificar una explotación comercial sobre este plan.
La [documentación Free de Render](https://render.com/docs/free) tampoco recomienda
ese nivel para producción, limita el servicio a una instancia y mantiene la
caducidad de PostgreSQL a 30 días sin respaldo gestionado. No se contrataron
planes, habilitaron cobros ni cambiaron proveedores.

Comparación dirigida, todavía sin recursos creados:

| Ruta | Compatibilidad y límite comprobado | Decisión pendiente |
|---|---|---|
| Vercel Hobby + Render Free actuales | El piloto funciona, pero Hobby restringe uso comercial y la DB Free caduca. | Conservar como evidencia/piloto; no certificar producción comercial. |
| Azure App Service F1 | Puede ejecutar ASP.NET, pero su [tarifa oficial](https://azure.microsoft.com/en-us/pricing/details/app-service/linux/) no admite cargas de producción en Free. | No trasladar el mismo impedimento a otro proveedor. |
| OCI Always Free | Una VM Linux podría conservar .NET/PostgreSQL sin reescribir el producto. Su [contrato de recursos](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm) incluye cómputo y almacenamiento gratuitos, pero admite falta de capacidad y reclamación de instancias inactivas. | Es una candidata por investigar, no un alojamiento provisionado ni certificado; faltan cuenta, condiciones aplicables, disponibilidad, cuotas, operación/backup y aprobación del destino de datos/credenciales. |
| Planes productivos de Vercel/Render | Conservan la arquitectura elegida, pero exceden el presupuesto autorizado de $0. | Sólo preparar cotización si el usuario cambia esa restricción; no comprar ni activar pruebas de pago automáticamente. |

La meta conserva su alcance comercial. El presupuesto $0 y la preferencia por
Vercel/Render no autorizan ignorar estas restricciones ni sustituir certificación
por una aceptación de piloto. El usuario eligió **evaluar OCI Always Free**,
conservando .NET/PostgreSQL y sujeto a capacidad y condiciones. Esta elección
autoriza preparar la alternativa; no crea cuentas, recursos, contratos ni un
nuevo destino de datos o credenciales.

La [evaluación de OCI](../research/OCI_FREE_REVIEW_20261029.md) registra cuotas
actuales, términos por confirmar, reclamación de instancias, HTTPS/DNS y un plan
de respaldo y aceptación. La lectura de los tres manifests fijados en el
Dockerfile confirmó entradas Linux ARM64 y AMD64; no se compiló ni ejecutó la
aplicación para ARM64. `AccountHost.MapAccountPage` ya activa el modo de cuentas
al servir el build compartido, sin necesitar una segunda lógica de frontend.
Recibo local: `commercial-operational-f49-20261029/oci-image-manifests.json`.

No se declararon disponibilidad de una cuenta/región, permiso contractual para
este SaaS, un dominio gratuito, capacidad, SLO, RPO/RTO ni aceptación humana.
Conservar Vercel Hobby mantendría su restricción comercial aunque la base y el
backend se trasladaran a OCI. El gate I01 y el dictamen **NO GO** siguen abiertos.

### Candidato publicado del 29/10: f49ebd6 y contraste de empaquetado

Después de la restauración privada se aplicaron **sólo** las migraciones
`20261028203013_PrivateFactualDocuments` y
`20261028211701_FactualLeastPrivilege`, con el migrador existente y target EF
explícito. El historial remoto pasó de 29 a 31 IDs exactos; se verificaron TLS,
35 tablas, 22 FORCE RLS, tres secuencias y los permisos de tabla factuales. No se
recrearon roles ni credenciales. El primer preflight rechazó un conteo antiguo
del arnés antes de leer secretos/conectar; se conserva y no es un fallo de datos.

Render publicó `f49ebd6260937b79166d1663efc2477eddd15cd1`, despliegue
`dep-dbhj5b4s728c73crg870`, observado **Live**. La rama del piloto se adelantó a
ese commit sin force-push ni cambios al índice normal. Vercel sirve ahora
`dpl_Gm99hF3932koiT5fiMFZyDJERXFk` en la URL habitual
`https://project-sxf1z.vercel.app/`. La versión visible sigue siendo 1.0.0.

Se conserva el primer despliegue web `dpl_Bxc4bBGdkoS2rjoe8e95DsxzppXx` y su
smoke **12/14**: sus 244 archivos subidos coincidían con el stage, pero también
incluían el `dist` local. Tailwind escaneó tokens de los bundles y generó CSS
adicional. El contraste controlado añadió únicamente `.vercelignore` para
excluir artefactos, node_modules y archivos de entorno de la subida. No cambió
las fuentes de aplicación f49. El árbol del segundo despliegue conserva el
marcador del directorio dist con **cero archivos**. Sus JS `index-BF3yTpeA.js`
y CSS `index-Bf39ry_6.css` coinciden byte a byte con los preparados localmente.
El smoke posterior pasó **14/14**, conservando las expectativas y los fallos
anteriores. Las 13 pruebas de configuración/metadatos Vercel también pasaron.
El override de empaquetado tiene SHA-256
`2d5a098ad39fdf3273ec4c1ecbc31bc9ba800b019c0a76d95426f60cdaa913aa`.
La explicación es coherente con el [escaneo de Tailwind](https://tailwindcss.com/docs/detecting-classes-in-source-files)
y la [exclusión de archivos en Vercel](https://vercel.com/docs/deployments/vercel-ignore);
el contraste ejecutado, no las fuentes por sí solas, acredita la corrección.

En la cuenta principal se comprobó login Google, creación manual con tres
variables, preparación sin IA, copia literal con un solo uso registrado, edición,
recuperación de versión, archivo/recuperación y colección creada en biblioteca
visible/asignada en detalle. La fixture `d8d60294-ef7e-4a1f-adf3-1cc3a083b426`
queda recuperada en revisión 5 y su colección QA se conserva. El MCP conectado
leyó/compiló la fixture, rechazó revisión obsoleta y mantuvo la identidad A al
cambiar el navegador a B. La cuenta B mostró sólo su biblioteca y recibió un
problema 404 al abrir la ruta exacta de esa fixture de A. No hubo borrado definitivo.

La conversación de ChatGPT `6ae2d361-aba8-83e8-b21e-a1a9f2399404` ya muestra
tanto el control antiguo como la respuesta antigua antes ilegible. Una solicitud
nueva mostró correctamente título, revisión 5 y variables de la fixture RC7,
con actividad «Retrieved exact approved prompt template». Esto verifica lectura
y presentación observables de una cuenta; no aporta un recibo crudo del servidor
ni prueba segunda cuenta ChatGPT, renovación/revocación o creación en ese cliente.
No se atribuye una causa definitiva al fallo de presentación anterior.

Brevo 5641363 sigue Open y no muestra una respuesta posterior al seguimiento
40247224582418; el proveedor público sigue `mailDelivery=unconfigured`. El proceso
Companion esperado no está activo y se solicitó inicio manual de los dos perfiles,
sin repetir el arranque rechazado. No hubo inferencia Qwen nueva. Correo, fidelidad
pública del agente, carga/capacidad, recuperación operativa completa y aceptación
SaaS mantienen el estado **NO GO comercial**. Evidencia local segura:
`commercial-rollout-f49-20261029/`, con recibos de migración, ambos smokes, contraste
de assets y capturas de Render, resultado preparado y ChatGPT.

### Restauración real del 29/10: contrato portable verificado

El usuario aprobó expresamente el plan conjunto `82882477…3f49f`. Se ejecutaron
una sola vez el respaldo `e51eafb6…ff105` y la restauración `88d521cd…b90d1`.
El respaldo terminó `BACKUP_CREATED_RESTORE_PENDING`, con limpieza de permisos
verificada, snapshot cerrado, secuencias sin cambios y credencial efímera retirada.
El archivo privado tiene 550442 bytes y SHA-256
`4a8644c5509406e98dfd960e967ee2990409e0576612c240e6e07a1772d3d28e`.

La restauración terminó `RESTORED_VALIDATED_PRIVATE_NO_TRAFFIC`: coincidieron
los conteos y huellas de las 33 tablas, el hash del texto original y las tres
secuencias, tanto antes como después de retirar el rol/políticas temporales de
la copia. El inventario verificó 20 tablas FORCE RLS. El recibo confirma
`restoreTested=true` y `stopVerified=true`; raíz comprobó además la ausencia del
listener 55439 y de `pgpass.ephemeral`, y la ACL protegida sólo para el usuario
Windows aprobado y SYSTEM. La copia se conserva apagada en el destino autorizado.

Esto acredita recuperación de **ese snapshot privado de 33 tablas**, sin tráfico.
No certifica el esquema final de 35 tablas, comparación exacta de catálogo fuente,
descifrado de claves, continuidad de cookies/revocaciones, flags globales de roles,
rollback público, respaldo externo automático/cifrado, retención ni RPO/RTO.
El fallo y la copia v1 permanecen conservados; no se recalifican como aprobados.
No se ejecutaron migraciones, despliegues, modelos ni un tag en este ejercicio.
La aprobación específica queda consumida. Resumen seguro local:
`private-restore-portable-v2/execution-summary-root-20261029.json`; los recibos
completos permanecen únicamente en el directorio privado aprobado, fuera de Git.

El control del navegador volvió a fallar con `native pipe closed before response`.
No se verificaron nuevos estados de Render, Brevo o presentación en ChatGPT.
El cierre de recuperación de esta copia supone progreso; la certificación
comercial continúa NO GO por las puertas restantes.

### Continuación del 29/10: disponibilidad, ingreso y compilación MCP

La URL pública devuelve HTML 200 en modo cuentas y el bundle anterior
`index-Cwh0RRa1.js`; no sirve todavía `index-BF3yTpeA.js` del candidato preparado.
Health devuelve 200. Su primera comprobación tardó 46,4 segundos; esa observación
no acredita capacidad, causa de la espera ni un SLO comercial. El correo sigue
devolviendo `mailDelivery=unconfigured` y `email=false`.

El primer arnés HTTP obtuvo 13/15 resultados esperados. Se conservan sus dos
fallos: pidió el documento de recurso en la raíz y esperó 401 de GET `/mcp`.
La auditoría del owner `AccountMcpModule` mostró el contrato correcto: transporte
stateless y metadatos en `/.well-known/oauth-protected-resource/mcp`. El contraste
dirigido posterior pasó 3/3: POST initialize anónimo devuelve 401 con el challenge
al recurso y scope de lectura, el documento publicado devuelve 200 y GET `/mcp`
devuelve 405 con `Allow: POST`. No se cambió el producto para obtener ese resultado.
Los rechazos 401 de cuenta/biblioteca y 403 de origen ajeno, ingreso directo,
health directo con query y forwarded host falsificado también pasaron. La cabecera
de ingreso falsificada en la URL pública no permitió acceder a la biblioteca.

La lectura MCP real recuperó la fixture revisión 5. Compilar con el nombre de
proyecto y la confirmación textual proporcionados por el usuario produjo el texto
literal esperado y ninguna variable faltante; usar revisión 4 devolvió
`REVISION_CHANGED`. No se guardaron valores ni se invocó un modelo. Esto acredita
lectura/compilación desde el cliente conectado; no cierra presentación en ChatGPT,
segunda cuenta, renovación o revocación.

El conector Vercel devolvió 404 tanto para el dominio como para el despliegue y
proyecto conocidos. La CLI existente, con el mismo equipo explícito, confirmó
`dpl_BAjPR2qSSzb6vAvZUd5E6LurxSdF` en READY/production. La discrepancia del
conector queda sin causa confirmada; no se recrearon recursos ni se cambiaron
credenciales. La rama del piloto sigue en `fe373fc`.

El control del navegador falló antes de leer páginas, también tras reinicializar
su enlace. No se comprobó una nueva respuesta de Brevo ni el estado actual de la
presentación en ChatGPT. Las vistas de Lab/observador figuran abiertas en el
inventario, pero su contenido actual no se certifica por ese inventario. Ollama
responde 200 en loopback y lista Qwen3:8b; el listener sigue en `::`:11434 y no
aparecen workers Companion. No se reinició automáticamente ninguno.

El plan conjunto conserva SHA `82882477…3f49f`, sin aprobación nueva ni ejecución.
Los dos recibos Execute y el archivo efímero de credencial no existen. Esta
continuación produjo evidencia independiente y no es una espera de un proceso
activo. Recibos locales: `commercial-continuation-20261029-01/public-boundaries.json`,
`mcp-http-targeted.json`, `mcp-compilation.json`, `vercel-cli-inspect.json` y
`ollama-readonly.json`. No cierra puertas compuestas ni crea un tag.

Referencias consultadas mediante Context7 y documentación oficial el 29/10:
[diagnóstico por capas de plugins](https://developers.openai.com/plugins/deploy/troubleshooting),
[transporte stateless del SDK C#](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/stateless/stateless.md).
El paquete del proyecto permanece fijado en 2.3.0; la documentación no sustituye
los tres resultados HTTP ni autoriza actualizar ese paquete.

### Cierre operativo del 29/10: respaldo y comprobación de exportación resueltos

El usuario autorizó el nuevo plan nativo `3566aa4c…63e07` y se ejecutó una sola
vez. Terminó `BACKUP_CREATED_RESTORE_PENDING`: acceso temporal retirado,
catálogo/roles verificados y `pgpass.ephemeral` eliminado. El archivo privado
tiene 520069 bytes y SHA-256
`2792270106a07e79b40ede261965f66a31e57ffb6f44f459720d3c5ffacc38a4`.
La lectura offline de su inventario verificó 33 entradas de datos de tablas y
tres secuencias. No se copiaron datos privados a QA, Git o la nube. El permiso de
ese respaldo queda consumido; no autoriza otra ejecución.

El usuario autorizó después la restauración privada del plan `57cd0e49…de4`.
El archivo se cargó en una única base local, autenticada sólo mediante SSPI para
la identidad de Windows aprobada, en `127.0.0.1:55439`. La ejecución **falló** al
comparar la huella de las filas; no alcanzó las secuencias ni la limpieza de los
permisos del respaldo dentro de la copia. El servidor se apagó y la copia quedó
retenida en el directorio privado autorizado. El resultado original se conserva:
`restoreTested=false`, `stopVerified=true`. Esto no revierte la limpieza ya
comprobada en producción al crear el respaldo.

El contraste posterior verificó origen `en_US.UTF8` y copia `C`. Una sola
transacción de lectura en la misma copia reprodujo la huella fallida. Ordenar
únicamente los 33 resultados según el orden exacto de origen tampoco recuperó
la huella original. La hipótesis del **orden exterior como única causa queda
descartada**; no se atribuye la diferencia restante a corrupción ni al orden
interior sin evidencia. Las tres secuencias sí coinciden; el inventario tiene
33 tablas, 20 con FORCE RLS y tres secuencias. El servidor volvió a quedar apagado.

La auditoría estática confirmó que Npgsql entregó el texto directamente al hash
UTF8, y que el snapshot se mantuvo abierto durante `pg_dump`. El recibo original
no retuvo las huellas por tabla ni los ajustes originales de sesión. No podemos
reconstruir esa referencia a partir de datos actuales ni usar la copia como su
propio oráculo. La recuperación de filas sigue **sin verificar**. El SQL actual
también hereda collation al ordenar cada fila dentro de `string_agg`.

Se implementó la preparación de un contrato v2 con orden C en ambos niveles,
UTC/ISO/UTF8 explícitos y manifiesto privado por tabla, conservando el owner,
snapshot, transporte, permisos y limpieza existentes. El contraste sintético
ejecutado una sola vez en Render aprobó ocho controles: los dos órdenes cambian
independientemente, permutar conserva la huella y mutar valor/cantidad la altera.
No leyó tablas de negocio y sólo probó un host. Las 42 guardas offline aprobaron
sin invocar el owner ni leer credenciales. Root revisó el delta por separado.

Se eligió extender el agregador existente porque conserva el snapshot y los
límites ya verificados sin añadir dependencias. La alternativa de reordenar sólo
los resultados exteriores ya fue insuficiente. Calcular una referencia a partir
de la copia restaurada no acredita el origen. Un nuevo framework no recuperaría
la evidencia que el recibo original descartó.

El nuevo ReviewOnly produjo el plan `e51eafb6…ff105`, contrato v2, 15 archivos
cerrados sin diferencias, 33 tablas/20 tenant/tres secuencias; no abrió conexiones
ni ejecutó DDL, y no creó archivo de credencial o respaldo. La restauración v2
pasó 38 controles offline y ReviewOnly sin leer referencias privadas ni iniciar
procesos. Root detectó y se corrigió antes de ejecutar una diferencia de
separadores de rutas; se conservaron el plan y los 34 controles iniciales.

El plan conjunto `82882477…3f49f` vincula ese respaldo y la restauración
`88d521cd…b90d1`, con una sola operación de cada tipo, la misma identidad SSPI y
puerto 55439, copia privada retenida apagada y cero secretos nuevos. **La nueva
autorización fue solicitada y está pendiente**. Ningún Execute v2 se ha realizado.
La referencia original no se convierte en aprobada. Artefactos locales:
`reviewed-public-backup-portable-v2`,
`private-restore-portable-v2/COMBINED_REVIEW.md`, `combined-review.json` y
`private-restore-order-probe-20261029/synthetic-fingerprint-portability-v1-result.json`.
La documentación oficial de [agregados](https://www.postgresql.org/docs/18/functions-aggregate.html)
y [collation](https://www.postgresql.org/docs/18/collation.html), consultada el
29/10/2026, fundamenta el experimento; no demuestra su resultado local.
Custodia externa, cifrado del archivo y objetivos de recuperación siguen pendientes.

El usuario abrió el JSON exportado y confirmó el título y los tres marcadores
`nombre_proyecto`, `tipo` y `proposito`. Se cierra esa comprobación por **evidencia
humana**, sin inspección automatizada del archivo ni hash de su contenido.
El bloqueo previo del navegador se conserva; no se usó una vía alternativa.

Brevo confirmó el envío del seguimiento autorizado en el ticket **5641363**:
comentario `40247224582418`, estado **Open** y mensaje «Your reply was successfully
added». Se compartieron únicamente el correo del proyecto, QuickPrompt y Puerto
Rico; no teléfono, claves ni adjuntos. El panel aún exigía verificar el teléfono;
no hay clave transaccional o entrega de confirmación/recuperación comprobadas.
Enviar un ticket no acredita correo operativo.

La comprobación directa del MCP público encontró y leyó la fixture en revisión 5;
compilar sin valores devolvió `proyecto` y `evidencia` faltantes. Una conversación
nueva de control en ChatGPT, sin aplicaciones ni herramientas, volvió a mostrar
«This response couldn’t load»; copiar la respuesta produjo ese mismo texto.
La solicitud posterior con el plugin instalado quedó visible al reabrir la
conversación, pero su respuesta también falló. El resumen público de actividad
no es un recibo de herramienta; no prueba por sí solo qué devolvió el servidor.
No se atribuye la causa al MCP y no se repitió el envío ante el timeout del control.

Los dos accesos del escritorio conservaron sus consolas, pero sus procesos worker
ya no estaban activos. El código establece salida tras cinco minutos sin trabajo
y límites de 12/1 trabajos; el horario es compatible con esa salida. Sin el
mensaje o código de salida no se atribuye una causa definitiva ni se cambian
tokens, modelo o límites. No se reinició automáticamente el companion.

El candidato `f49ebd6` se exportó desde Git y compiló aparte del checkout: backend
Release sin errores/advertencias y web en modo cuentas con dependencias fijadas.
El paquete web contiene `index-BF3yTpeA.js`; el DLL declara
`1.0.0+f49ebd6260937b79166d1663efc2477eddd15cd1`. Son artefactos preparados,
no pruebas de que Render/Vercel los sirvan. La UI de Render aún muestra `fe373fc`.

Recibos seguros: `commercial-closure-20261029-01/current-evidence-01.json` y
`current-evidence-02.json`. El primero precede al seguimiento de Brevo y al fallo
de restauración y se conserva. El Lab conserva los 36 resultados mixtos del corte
08: 22 PASS y 14 FAIL, incluidos los fallos históricos; no son 36 inferencias ni
una campaña semántica nueva.
No se aplicaron migraciones ni se publicó un nuevo candidato en este corte.
Los siguientes cortes conservan las condiciones y los fallos anteriores.

### Último corte: f49ebd6 aprobado en CI, sin publicar

[CI 40800436479](https://github.com/JuanPabloTorres/QuickPrompt/actions/runs/40800436479)
aprobó `f49ebd6260937b79166d1663efc2477eddd15cd1` en ambos jobs. Los siete
artefactos TRX contienen **2097 ejecutadas y aprobadas, cero fallidas**, con 21
pruebas de inferencia no ejecutadas. El build web y las **480/480** pruebas de
interfaz pasaron. PostgreSQL y su restauración sintética con 22 tablas FORCE RLS
también pasaron. El fallo de interfaz de 6ccd514 se conserva en la sección siguiente;
el ajuste de sincronización no retiró ninguna aserción ni cambió el producto.
Los 138 archivos de entrada UI siguen coincidiendo con el candidato final.

El contraste O04 reconstruyó `fe373fc` sin editar sus 267 archivos fuente y
comprobó arranque y lectura con las dos migraciones factuales presentes en QA:
health 200, lectura anónima 401, acceso del propietario y rechazo de otra cuenta.
No generó claves ni usó producción. El worker conservó su registro original,
pero el arnés retuvo el bloqueo de admisión; no se probaron colas, cookies,
Google, TLS público, MCP ni rollback del contenedor Render. Se revisó el arnés
por separado del autor. Esto reduce la incertidumbre de compatibilidad, sin
cerrar O04 ni sustituir restauración operativa de datos reales.

Producción y la rama del piloto permanecían en `fe373fc` en este corte histórico;
no se aplicaron las migraciones ni se creó un tag estable. La aprobación del
respaldo estaba pendiente entonces y se resolvió en el corte del 29/10 anterior. También
siguen abiertos correo, recuperación operativa, configuración Google de
producción, ChatGPT web/múltiples cuentas/revocación, carga comercial y aceptación
humana SaaS. El resultado continúa siendo **NO GO comercial**.

Recibos: `commercial-ops-rc9/attempt-09/ci-final.json`, `ci-trx-reviewed.json`,
`ci-web.log`, `ci-artifacts` y `o04-fe373f-6ccd514-native-01/handoff-manifest.json`.
Este corte documental se añadió localmente después del commit identificado;
no cambia los archivos de producto probados ni crea otro candidato.

### Corte posterior: candidato 6ccd514 rechazado por CI

[CI 40798665085](https://github.com/JuanPabloTorres/QuickPrompt/actions/runs/40798665085)
terminó **failure** para `6ccd514eefa877a3180159e913ce94172b16e262`.
Sus artefactos TRX verifican 2097 aprobadas y cero fallidas; las 21 de inferencia
no ejecutadas siguen excluidas. PostgreSQL, sus 16 contrastes del comparador y
la restauración sintética con 22 tablas FORCE RLS pasaron. El build web pasó;
la interfaz obtuvo **479/480**, con un fallo al comprobar la máscara de navegación
durante una consulta de identidad pendiente. El pase local anterior de 480/480
no sustituye este resultado remoto. No se desplegó este candidato.

La prueba focal aislada no reprodujo el fallo. La revisión localiza una aserción
síncrona tras el evento de foco, sin barrera del efecto React ni comprobación de
la segunda consulta de identidad. Se contrasta un arnés con `await act`, respuesta
retenida y recuento de solicitudes, conservando las aserciones de ocultación y
los mismos nodos al recuperar el acceso. La causa exacta de la carrera en CI aún
no está confirmada; no se atribuye un fallo de seguridad al producto sin evidencia.

El arnés corregido aprobó **41/41** contrastes de sesión y **480/480** pruebas de
interfaz en una suite Release completa. Mantiene todas las aserciones de máscara,
la identidad de los nodos y la recuperación; añade una comprobación de las dos
consultas de identidad. Sólo cambia el test de sesión, sin cambios de producto,
permisos ni configuración. La revisión del propietario y la revisión del diff
por raíz son distintas; el resultado remoto del siguiente candidato sigue
pendiente. Recibo local: `commercial-ops-rc9/attempt-09/account-workspace-release.json`.

Se compararon los 138 archivos de entrada de la UI local con ese árbol Git: cero
derivas y cero archivos extra en `src`/`public`. Esto correlaciona fuentes del QA,
no certifica reproducción binaria ni despliegue. El navegador volvió a abrir el
inventario factual aprobado y conservó sus secciones de propiedad, trabajo
terminado, tareas e información pendiente, sin otra llamada al modelo.
Recibos: `commercial-ops-rc9/attempt-08/ci-final.json`, `ci-trx-reviewed.json`,
`ci-artifacts`, `ci-failed.log` y `ui-source-continuity.json`.

### Corte aprobado anterior: candidato f21dc31, pendiente de despliegue

[CI 40791828142](https://github.com/JuanPabloTorres/QuickPrompt/actions/runs/40791828142)
terminó **success** para `f21dc3183418abe1ef6019af7f74016c6c38864d`, que añade
la corrección de capas del modal al candidato base `92672c2`, también aprobado.
Los artefactos TRX contienen **2097 pruebas ejecutadas y aprobadas, cero fallidas**:
442 PostgreSQL, 1409 backend, 8 telemetría Debug, 27 MCP, 16 companion,
27 recuperación y 168 legado. Las 21 pruebas de inferencia PostgreSQL omitidas
no se contabilizan como aprobadas. El job web aprobó además **478/478** pruebas
de interfaz y su build. No se repitió la campaña de tres inferencias reales.

El comparador pasó sus 16 contrastes y la restauración sintética completa
confirmó 22 tablas FORCE RLS, filas y contrato factual, permisos, borrado privado
y key ring cifrado. Esto cierra el fallo del comparador en CI, sin certificar la
recuperación de los datos reales ni una carga comercial. Los intentos fallidos
anteriores se conservan más abajo con sus causas conocidas o desconocidas.

La instancia QA nativa reiniciada con Cloud `DFD09C19` conservó ambos documentos
aprobados: la comparación HTTP verificó texto y trazabilidad exactos; el navegador
volvió a entrar y abrió el inventario corregido desde el historial. No hubo una
nueva llamada al modelo. Es una comprobación de Codex con datos sintéticos,
no aceptación humana del nuevo flujo SaaS.

La producción continúa en **fe373fc**. Antes de aplicar las dos migraciones
factuales y publicar backend/interfaz faltan ejecutar y verificar un respaldo
nuevo con autorización específica de su destino temporal de credencial. Su plan
nativo v4 quedó preparado en modo ReviewOnly: ninguna conexión productiva ni DDL.
El hash y el destino exacto están en el expediente privado; el permiso antiguo
de un solo uso no se reutiliza. Siguen pendientes correo real, recuperación
operativa, presentación en ChatGPT web y aceptación SaaS. **NO GO comercial**;
no corresponde crear un tag estable.

Recibos locales: `commercial-ops-rc9/attempt-07/ci-final.json`,
`ci-trx-reviewed.json`, artefactos `ci-postgres`/`ci-web` y
`factual-role-guards/history-continuity-availability-final.json` y
`commercial-ops-rc9/attempt-07/local-ui-evidence.json`.
Las siguientes secciones conservan la evolución histórica, no sustituyen este corte.

### Contraste posterior de móvil y MCP

En el piloto publicado, una sesión Google nueva de la cuenta principal abrió la
biblioteca y encontró la colección de la fixture en el detalle del prompt. La
auditoría a 390 px detectó una superposición visual: la barra lateral usa capa 40,
pero el modal común y su fondo usaban 11/10. Radix sí impedía eventos de fondo;
no se observó un fallo de autorización ni se atribuye un bloqueo de clics.

La corrección compartida eleva fondo/contenido a 50/51, conservando el historial
70/71 y los controles de NOVA 10000/10001. El build de cuentas y las 478 pruebas
de interfaz pasaron. En QA aislado se comprobó el modal completo a 390 y 1280 px,
el cierre con retorno del foco y la lectura móvil del documento factual aprobado.
El reinicio conservó la sesión y los dos documentos; no hubo otra inferencia.
Esta validación local no convierte en corregida la web pública anterior; la CI y
el despliegue deben corresponder al commit que incluya esta corrección.

El MCP público volvió a buscar y leer la fixture de auditoría en revisión 5.
Sin valores devolvió ambas variables faltantes; con valores sintéticos compiló
el texto literal sin inferencia. Una lectura posterior conservó revisión, usos y
fecha de uso. Es evidencia del conector existente, no de una segunda cuenta de
ChatGPT ni de su presentación web. M02/M03 permanecen abiertos. Recibos privados
en `commercial-ops-rc9/attempt-07`; las capturas del navegador sólo están en las
salidas de herramienta, sin archivo exportado certificado.

Después se preparó y copió desde la web un resultado sintético de esa misma
fixture: pasó de uno a dos usos. Una segunda copia del mismo resultado mantuvo
dos usos y la misma fecha, comprobado mediante MCP. La plantilla permaneció en
revisión 5; no se ejecutó IA ni se modificaron prompts personales. El resultado
de auditoría quedó sujeto a la retención normal de siete días. Ocho controles
HTTP públicos pasaron sin credenciales reales, y una lectura nueva de Render
confirmó que producción seguía en `fe373fc`; no son controles del nuevo candidato
desplegado. Las conversaciones históricas de ChatGPT, reabiertas sin generar
respuestas, conservaron el error de carga. El dashboard de Brevo aún exige
verificar el teléfono y el ticket leído desde el servidor conserva dos comentarios.

### Recorrido manual posterior: archivo, revisión y colección

La fixture privada «QA comercial f21dc31 · Flujo sin IA», creada sólo en QA,
conservó sus tres marcadores al crear, editar, consultar la revisión inicial y
recuperarla como una revisión nueva. La colección creada desde la biblioteca
apareció en el detalle, permaneció asignada y filtró exactamente esa plantilla.
Archivo creó la revisión 4 y recuperar produjo la 5; cuerpo, variables y colección
se conservaron tras recargar. No se solicitó IA ni se borraron datos.

Ese recorrido descubrió una consulta incorrecta de la interfaz: el detalle seguía
inspeccionando una fuente archivada. Una reproducción HTTP independiente, con el
mismo actor/ID/revisión/valores sintéticos, confirmó el 404 requerido por el servidor.
La UI mostraba un error genérico; el uso permanecía bloqueado. La corrección pausa
la inspección al archivar, cancela trabajo anterior y reanuda con la revisión actual
al recuperar, conservando los valores. La regla pertenece al hook compartido: el
detalle y el editor la consumen sin repetirla. El editor explica la pausa y bloquea
conservar contexto ligado al archivo; mantiene el guardado manual de título/cuerpo.
No se relajaron controles del backend. Se conservaron el baseline de 10/12 —dos
fallos— y el corte intermedio 12/12 con 479/479. El owner final aprobó 13/13 focales,
480/480 de interfaz y el build de cuentas.
El navegador contrastó la misma revisión archivada 4 antes/después y la recuperación
5 con vista previa exacta. La CI del nuevo corte debe comprobarse separadamente
del pase anterior de f21dc31. El contraste final comprobó además detalle/editor
archivados y recuperación en revisiones 6/7, con cuerpo y valores correctos.
Recibos: `commercial-ops-rc9/attempt-08`.

Conservar un borrador separado recuperó título, texto y tres valores tras recargar;
la plantilla guardada permaneció en revisión 5 y no hubo una preparación solicitada.
Después de cambiar la fuente a revisión 7, recuperar el contexto antiguo mostró el
aviso de origen cambiado: sobrescribir y conservar contexto quedaron bloqueados;
guardar como nuevo siguió disponible. Se canceló sin guardar ni ejecutar. Son
recorridos de Codex con datos sintéticos, no aceptación humana ni prueba A/B pública.

Exportar anunció éxito en la aplicación, pero expiró la captura del evento de
descarga. La política del navegador rechazó la lista interna de descargas de Edge;
el archivo permanece sin verificar y se solicitó comprobación manual. No se usó
otra vía para inspeccionar esa lista ni se cerró W01 por el mensaje de éxito.

### Revisión factual durable — implementación local, aún no publicada

El usuario eligió revisar hechos y guardar el documento en el historial. La nueva
capacidad usa la cola de propuestas existente: fuentes seleccionadas, revisión
explícita de hechos, composición literal sin otro escritor y aprobación final
separada. Añade dos tablas privadas con FORCE RLS; no reutiliza el permiso de
guardar una plantilla para autorizar un documento.

La suite local inicial de interfaz aprobó **477/477**, sin omisiones, y los builds general
y de cuentas aprobaron. La integración focal inicial aprobó **18/18** con
PostgreSQL 18.6. Son comprobaciones locales; la regresión completa, el restore con
el esquema final y la publicación del nuevo candidato siguen pendientes.

El corte focal posterior aprobó **22/22**, sin omisiones, en
`factual-documents-postgres-20261028/factual-postgres-05.trx`. Reproduce la
respuesta grabada del caso A y demuestra que su cita incorrecta llega intacta a
revisión, se rechaza al confirmar y puede corregirse antes de componer. El replay
no consume otra inferencia ni convierte el primer intento fallido en PASS.
La regresión posterior de interfaz aprobó **23/23** focales y TypeScript; el nuevo
test de cita no formaba parte de las 477 anteriores. La CI posterior aprobó
**478/478** de interfaz; su job PostgreSQL falló, como se detalla abajo.

La revisión adicional confirmó tres defectos del mecanismo inicial y sus
correcciones: los privilegios CRUD por defecto sobrevivían a un GRANT más
estrecho; comparar cadenas JSON con JSONB alteraba `edited` y el replay de
confirmación; un rechazo conocido por cuota dejaba una revisión ADMITTED sin
hijo. Una migración forward retira los privilegios indebidos sin reescribir la
migración aplicada; la aplicación compara hechos tipados conservando orden y
literales, y devuelve un recibo FAILED terminal cuando la cuota rechazó al hijo
antes de admitirlo. No vuelve a inferir al repetir una operación fallida. Los
contrastes iniciales se conservan y los registros A/B mantuvieron sus hashes
antes/después del ajuste de permisos.

La restauración sintética ahora compara también filas, RLS, permisos, índices,
constraints, funciones y triggers factuales. Su contraste nativo aprobó once
comprobaciones en dos bases nuevas desechables; conservó un fallo intermedio de
conversión SQL. La prueba de dump sintético cifrado y key ring aprobó en CI;
los pasos independientes del comparador y restore completo no se ejecutaron tras
fallar otra prueba. No se ha restaurado una biblioteca privada en QA.

La campaña `factual-durable-20261028` se declaró antes de inferir: máximo tres
llamadas a Qwen3:8b, sin reintentos para mejorar el resultado semántico. El primer
caso obtuvo JSON válido pero fue rechazado antes de REVIEW por una cita con
«revisarán» frente a «revisará». La validación de procedencia se había situado
antes de la corrección humana. Se conserva el fallo y su respuesta original;
el ajuste de esa frontera debe volver a validarse sin convertirlo en un PASS
autónomo de Qwen.

El control con asistentes explícitos llegó a revisión, pero NOVA clasificó dos
tareas futuras como trabajo terminado: **fidelidad cruda FAIL**. Codex corrigió
roles y textos en la interfaz, confirmó los hechos y, por separado, aprobó el
documento. La API registró cero documentos antes de aprobar y uno después; el
texto y sus fuentes siguieron disponibles al recargar y abrir el historial.
Este recorrido demuestra la revisión asistida de una fixture sintética; no es
aceptación humana ni comprensión autónoma. Recibos locales:
`factual-durable-20261028/control-B-browser-receipt.json` y
`control-B-approved-document.json`.

ChatGPT web también mostró **This response couldn't load** con una petición
mínima que prohibía usar herramientas, en una conversación nueva. El fallo no
queda limitado al MCP. No se observó su causa interna ni errores en la lectura
acotada de consola; M02/M03 y la presentación web continúan pendientes.
Recibo: `rc10-chatgpt-no-tool-control-20261028.json`.

La utilidad de recuperación nueva usa AES-GCM y un destinatario RSA dedicado;
sus **27/27 pruebas propias y 27/27 independientes** aprobaron. No ejecuta el
ensayo CMS rechazado anteriormente. No se leyó ni transmitió el respaldo real:
faltan destinatario, custodia, destino externo y objetivos operativos acordados.
El cifrado de una fixture no cierra O01 ni la recuperación productiva.

### CI del candidato 88ecfdd y contraste posterior de carga

El commit `88ecfdd02bf050bf43ef2c4e3f7cc20fce770a00`, publicado únicamente en la
rama de validación, terminó **FAIL** en
[CI 40773288419](https://github.com/JuanPabloTorres/QuickPrompt/actions/runs/40773288419).
El job Windows aprobó backend **1409**, Debug **8**, MCP **27**, Companion **16**,
recuperación **27**, consumidor legado **168** e interfaz **478/478**, además del
build. PostgreSQL aprobó **406**, falló **una** y omitió **21** pruebas reales de
Qwen. El dump sintético cifrado y su restauración con el key ring aprobaron dentro
de esa suite; los dos pasos posteriores de comparación/restore no se ejecutaron
porque `dotnet test` devolvió error. No se desplegó ese candidato.

El fallo fue un HTTP **429** donde el escenario de diez propietarios y dos hosts
esperaba aceptar la solicitud de recuperación. Los recibos originales no guardaban
su body tipado; el job log tampoco conserva el motivo. Un intento local y dos
contrastes fijados de propietario ocupado/no ocupado no reprodujeron ese rechazo:
su capa original continúa **desconocida**. No se presenta una repetición favorable
como reparación demostrada del productor.

Se corrigió una carencia comprobable del arnés: ahora sigue los cuatro trabajos
adicionales aceptados en la fase de cuota y deja todos sus recibos propios en
estado terminal. Añade diagnóstico acotado de rechazo y conteos de cola, sin
payloads, tokens, cambios de cuotas o borrado de trabajos ajenos. Un control con
los límites reales distingue `QUEUE_FULL` global (64 en cola y cero del dueño
sondeado) de `OWNER_QUEUE_FULL` (cuatro del dueño). La regresión local ampliada
aprobó **6/6**, sin modelos; el primer observador paralelo del control global
agotó el pool de una conexión y su fallo separado quedó conservado. La observación
de limpieza se acotó y pasó. Este ajuste de prueba aún requiere CI completa.

La CI del hijo `df3e9bab2928278f5a7c34aa3fdbe779a738f277` terminó con
**409 PostgreSQL aprobadas, una fallida y 21 de Qwen omitidas** en
[40777060862](https://github.com/JuanPabloTorres/QuickPrompt/actions/runs/40777060862).
El job Windows aprobó, incluidos build e interfaz. El nuevo contraste que fija
al propietario ocupado recibió `OWNER_QUEUE_FULL` al preparar el fallo
controlado: cuatro trabajos propios seguían en cola, sin ninguno ejecutándose.
El arnés esperaba sólo los diez recibos iniciales, aunque había admitido cuatro
adicionales. La limpieza al salir no sincronizaba ese cambio de fase. Es una
carrera confirmada de la prueba; la cuota del producto actuó correctamente.
La barrera terminal explícita ya se agregó, con un control que retiene esos
cuatro trabajos para demostrar que la fase siguiente espera. Sus **4/4 controles
PostgreSQL** aprobaron: original, dueño ocupado, otro dueño y cola global. No se
modifica la cuota. Este diagnóstico no atribuye retrospectivamente el primer 429 sin body.
Ambos intentos fallidos y sus TRX se conservan; los pasos independientes de
restauración tampoco se ejecutaron en este hijo.

La revisión de arranque detectó otra omisión: las guardas existentes no
inspeccionaban las dos tablas ni las funciones factuales. El contraste previo
aceptó 19 derivas que debía rechazar. Se agregaron predicados en las guardas
actuales, sin otra migración: FORCE RLS, propietario, privilegios de tabla y
columna, firmas/seguridad de funciones y triggers requeridos. El contraste
corregido aprobó **20/20**, y la regresión HTTP focal con las guardas nuevas,
**23/23**. Los negativos alteraron sólo una transacción sintética y verificaron
rollback por fingerprint; no debilitaron permanentemente ninguna política.
Los TRX de `factual-role-guards` conservan tanto el fallo previo como el resultado
posterior. Esto comprueba esas derivas declaradas, no cualquier alteración posible
del catálogo.

La revisión independiente encontró dos huecos adicionales: REFERENCES por
columna en revisiones y MAINTAIN en PostgreSQL 17+. Los cinco negativos nuevos
reprodujeron la aceptación indebida antes de corregir; ese segundo baseline y
el freeze inicial se conservaron. El corte posterior aprobó **25/25** controles
(24 negativos y el positivo) en PostgreSQL 18.6, comprobando rollback. MAINTAIN
se consulta sólo desde la versión 17 para conservar la rama compatible con
PostgreSQL 16; esa versión anterior no se ejecutó en esta campaña. La revisión
independiente posterior confirmó por lectura que ambos hallazgos están atendidos;
no ejecutó esas pruebas.

El host QA de navegador se reinició con el artefacto `866DD980…972976`, mantuvo
health 200 y volvió a abrir ambos documentos. El contraste HTTP comparó texto,
hechos, propuesta original y fuentes exactamente contra los recibos aprobados.
La sesión web sintética volvió a entrar y mostró ambos textos en el historial,
sin otra inferencia. Recibos: `factual-role-guards/runtime-final-02.json`,
`history-continuity-final.json` e `history-browser-final.json`. No se cambiaron
producción ni las fuentes de la campaña de tres llamadas. La CI completa del
nuevo corte sigue pendiente; la continuidad QA no es aceptación humana del SaaS.

La campaña factual declarada terminó con **tres llamadas reales de Qwen y cero
reintentos semánticos**, cada una con un ID de ejecución y Core propio. El
inventario de otro ámbito también incluyó una cita incorrecta. En el snapshot
corregido llegó a REVIEW, mostró el error y mantuvo bloqueada la confirmación
incluso al marcar la revisión. Codex corrigió cita, relaciones y textos; aprobó
por separado cuatro hechos completos y el documento final. La biblioteca pasó
de un documento a dos y el inventario volvió a abrirse después de recargar. Se
conservaron fuente y seis propuestas originales, sin otro escritor. Recibos:
`factual-durable-20261028/campaign-completion.json`, `inventory-browser-receipt.json`
y `inventory-http-after.json`. Es revisión asistida verificada, **no fidelidad
autónoma ni aceptación humana del SaaS**.

El MCP público existente leyó de nuevo la fixture RC8, revisión 2, y compiló sus
tres variables; omitir `proposito` devolvió `missingVariables` y texto nulo. Son
lecturas/compilaciones mediante Codex, sin escrituras ni nuevo consentimiento.
ChatGPT web siguió sin mostrar su respuesta anterior incluso sin herramientas;
la inspección de red no estuvo disponible. M02/M03 continúan abiertos. Recibo:
`rc10-mcp-read-compile-20261028.json`.

La revisión de Google Cloud confirmó el callback final de Vercel y el retorno
local en el mismo cliente «QuickPrompt Web · Local», dentro de `quickprompt-web`.
La audiencia figura **External / Testing**, con un usuario de prueba; **Publish
app** está deshabilitado y solicita completar Branding. Página principal,
privacidad y condiciones están vacías. No se cambió esa configuración ni se
comprobó un login nuevo de otra cuenta en esta revisión. P06 sigue abierto:
[Google exige separar pruebas y producción y disponer de una página pública en
dominio verificado](https://developers.google.com/identity/protocols/oauth2/production-readiness/policy-compliance).
El recibo `commercial-factual-rc10/google-oauth-readonly-20261028.json` conserva
sólo metadatos públicos, sin secretos.

### CI 7d32760: cuentas aprobadas; comparador de restauración corregido localmente

El hijo `7d32760e7c58d477a6c7d8f0b3b1cc79e7a3c088` terminó **FAIL** en
[CI 40780752964](https://github.com/JuanPabloTorres/QuickPrompt/actions/runs/40780752964).
Windows aprobó backend **1409**, Debug **8**, MCP **27**, Companion **16**,
recuperación **27**, legado **168**, build e interfaz **478/478**. PostgreSQL
aprobó **435**, sin fallidas, y omitió **21** pruebas de Qwen. El contraste del
comparador pasó; el paso independiente de restauración completa falló al comparar
el contrato factual, antes de escribir un recibo de éxito. La cuota y las guardas
nuevas no fallaron en ese corte.

La reproducción nativa completa conservó las filas generales y factuales. El
diff por componente localizó dos representaciones distintas: `factual_review_shape`
y las condiciones de `factual_expiry_cancel`, por la decompilación de casts de
arrays tras `pg_dump`/`pg_restore`. Orden y ACL no explicaban el fallo. El ajuste
reparsea los CHECK en tablas TEMP y las condiciones de políticas en una vista TEMP,
con rollback; conserva roles, operación, permisividad, nulos, valores y el resto
del contrato. No elimina casts o términos para obtener igualdad.

El intento nativo 02 ya igualó el catálogo, pero se detuvo por falta de una fixture
de key ring cifrado; se conserva como incompleto. Después de un único setup
sintético con el control existente de claves en RAM, el intento **03 aprobó el
script completo**, incluidas las **22 tablas FORCE RLS**, privilegios, borrado,
MCP y claves cifradas. El comparador ampliado aprobó **16/16** controles, incluidos
quitar un CHECK, perder un término del CHECK o de una política y el round-trip
de arrays varchar con subconsultas. En el intento 04, quitar sólo el CHECK del
target restaurado produjo el fallo esperado: 44 a 43 restricciones, sin recibo
de éxito y con diagnóstico por huellas/conteos. Ese diagnóstico se añade al
artefacto de CI; no contiene filas, claves ni configuración.

Los recibos de `commercial-factual-rc10/full-restore-native-01` a `04` y
`restore-canonical-review` conservan fallos, causas, fuentes y resultados. No se
leyó el backup privado ni se alteró producción. La revisión independiente de
código y recibos no encontró hallazgos bloqueantes y confirmó los hashes; no
ejecutó las pruebas. La CI completa del nuevo ajuste sigue pendiente;
**NO GO comercial** permanece.

### CI b5b2540: disponibilidad y presupuesto físico del arnés

El hijo `b5b25403a044d1e0688d508e5ab2d4401627173c` terminó **FAIL** en
[CI 40784350897](https://github.com/JuanPabloTorres/QuickPrompt/actions/runs/40784350897).
Windows aprobó; PostgreSQL aprobó **434**, falló **una** y omitió **21** pruebas
de Qwen. El contraste ocupado recibió HTTP 500 en una admisión inicial, con
cola y admisiones en cero. Su excepción original no se conservó: sigue
**UNKNOWN**. Los pasos posteriores de restore no se ejecutaron en este corte.

Retener sólo el pool de cuentas en un control reproducible produjo el mismo
cuerpo público 500 y conservó la cadena de tipos: EF InvalidOperationException →
NpgsqlException transitoria → TimeoutException. Esto demuestra una carencia de
clasificación en ese control, sin atribuir retrospectivamente el origen del CI.
Dos contrastes de CPU acotados aprobaron y no confirmaron la hipótesis de presión
de threadpool; no se repiten para buscar un resultado favorable.

El ajuste de producto conserva primero los estados de dominio y clasifica como
**503** los fallos Npgsql transitorios encontrados en una cadena interna máxima
de ocho niveles. Mantiene el 500 no transitorio, cuotas, autenticación y
cancelaciones. El título público conserva incertidumbre sobre el resultado; no
afirma que una escritura no ocurrió, no añade Retry-After ni reintenta operaciones.
El control retenido verifica cero admisiones automáticas y una sola admisión
explícita, con el mismo OperationId, después de liberar el pool.

Los positivos de HTTP ahora se acotan a **una petición por datasource físico**,
sin aumentar sus pools de una conexión. Los dos hosts tienen datasources distintos;
conservan diez propietarios lógicos, dos handlers de ejecución concurrentes y
las cuotas originales. No prueban diez admisiones HTTP simultáneas ni un SLA.
La saturación queda separada del control positivo y exige 503/recuperación. El
diagnóstico sólo serializa tipos, IsTransient y SQLSTATE acotado; no mensajes,
SQL, correlaciones o payloads.

La regresión local aprobó **23/23**: siete controles operacionales, seis del
clasificador, seis de rate/auth y cuatro de readiness, sin omisiones. El
clasificador también aprobó su corte separado de seis. La revisión independiente
de los cuatro archivos no encontró bloqueos y confirmó sus hashes; no repitió
la ejecución. Los recibos y el baseline 500 se conservan en
`operational-commercial-20261028/capacity-ci-b5b2540`. CI del ajuste sigue pendiente.
La recuperación real, correo y aceptación SaaS continúan abiertos.

### Candidato operativo fe373fc — CI aprobada y backend publicado

El commit `fe373fccc19bee35c421e1f43eb6de750cad52eb` pasó los dos jobs de
[CI 40759073433](https://github.com/JuanPabloTorres/QuickPrompt/actions/runs/40759073433):
backend Release **1409/1409**, telemetría Debug **8/8**, MCP **27/27**,
Companion **16/16**, consumidor legado **168/168** y PostgreSQL **385 aprobadas,
cero fallidas y 21 de Qwen omitidas**. El build y la suite de interfaz aprobaron.
Los TRX, los tres recibos de operaciones y los dos de restauración quedaron
descargados y cotejados con ese SHA en `commercial-ops-rc9/attempt-02` del
expediente privado de entrega. No hubo nuevas llamadas al modelo en esa CI.

El contraste PostgreSQL ahora observa `/health` 200 antes, 503 durante el
agotamiento con y sin cookie, 403 para origen ajeno y 200 después de liberar la
conexión. La lectura privada se recupera y conserva su resultado anterior.
Los controles de diez propietarios y dos hosts vuelven a pasar con cero
duplicados y los límites intactos. Se añadieron señales operativas de
cardinalidad acotada y cinco lockfiles del grafo Cloud, sin cambios de versiones;
el restore bloqueado pasó. Esto cierra esos controles sintéticos, no el SLA,
la capacidad de inferencia, las alertas externas ni la recuperación productiva.

La rama del piloto avanzó normalmente a ese commit. Las fuentes de `apps/web`
son idénticas a RC8; no se reconstruyó la web de Vercel. Render confirmó el commit
exacto y **Deploy succeeded | Live**, deploy `dep-dbh5dno8v5cc73bvqkm0`. El clic
inicial agotó el tiempo de respuesta; se leyó el resultado posterior sin repetir
el despliegue. GET/HEAD de salud canónica y salud directa devolvieron 200;
biblioteca anónima 401, origen ajeno y API directa del backend 403. La conexión
OAuth MCP existente leyó después la fixture RC8 de la cuenta principal, revisión
2 y sus tres variables exactas, sin pedir otro consentimiento. No demuestra que
se haya renovado un token ni repara la presentación de ChatGPT web.

Recibos: `rc9-public-http-20261028.json` y `rc9-mcp-post-deploy-20261028.json`.
La captura gráfica adicional y la continuidad de la cookie del navegador no se
confirmaron: el control se desconectó después de observar Live. No se fabricó una
captura ni se sustituyó esa comprobación por la lectura MCP. No cambiaron esquemas,
secretos, proveedor/modelo ni la versión visible y no se creó un tag estable.

### Verificación adicional del 28/10 — puertas que siguen abiertas

El candidato de operaciones `84faba7b024f3739772c98cdbb82664d242cacf3` se publicó
sólo en la rama de validación. Su CI `40756884417` aprobó el job Windows y el
restore bloqueado del grafo Cloud; PostgreSQL aprobó 380 pruebas y falló una,
con 21 casos reales de Qwen no ejecutados. Los dos controles de concurrencia
pasaron: diez propietarios y dos hosts, máximo dos handlers globales y uno por
propietario, cuatro trabajos adicionales admitidos y uno rechazado por cuota,
cero invocaciones duplicadas y diez lecturas NOVA sin inferencia ni escrituras.
La prueba de agotamiento del pool recibió **500 en `/health` en vez de 503**;
se conserva como defecto abierto de disponibilidad. El recorrido de recuperación
posterior no se alcanzó en ese intento. Son mediciones HTTP TestServer con
PostgreSQL sintético, no carga pública, DOM, capacidad de Qwen o SLA comercial.
Los recibos de ese corte no se convierten en PASS al corregir un corte posterior.

La reparación posterior sitúa únicamente GET/HEAD de `/health` después del
control de origen/ingreso y antes de validar cookies. Reutiliza el probe de
PostgreSQL; no mueve la autenticación de rutas privadas. Once comprobaciones
locales pasaron: el contraste controlado reproduce el framing anterior
(cookie: 500; anónimo: 503), el nuevo permite llegar al probe en ambos casos,
las rutas privadas conservan autenticación y un origen ajeno recibe 403. La
causa exacta del intento PostgreSQL anterior sigue siendo probable hasta el
contraste de integración; esas once pruebas no certifican su recuperación real.

La revisión siguiente conserva el candidato público RC8. Los cambios de
observabilidad, dependencias y capacidad posteriores deben pasar su propia CI
antes de atribuirlos al despliegue. No se marca una puerta compuesta como aprobada
por completar únicamente uno de sus controles.

Tabla histórica del corte RC8 del 28/10; los avances posteriores de respaldo,
exportación y seguimiento a Brevo están en el corte del 29/10 al inicio.

| Puerta | Resultado comprobado en esta revisión | Cierre pendiente |
|---|---|---|
| Correo, U03/P05 | Brevo autenticado todavía exige verificar el teléfono. El ticket 5641363 muestra `Awaiting your reply`; sólo aparecen la solicitud original y la respuesta del agente de Brevo que pide ambos formatos del móvil. El envío comunicado por el usuario no está visible en ese ticket. No se generó ni leyó una clave. | Verificación del proveedor, remitente y clave limitada; recepción y uso reales de confirmación y recuperación. No reenviar información privada ni habilitar registro sin esta evidencia. |
| Recuperación, D02/A02/P04/O01/O04 | Se cotejaron los recibos del SHA `4236c3b` y siete pruebas críticas: restauración/descifrado con proveedor nuevo, certificado/aplicación incorrectos, rotación, borrado, retención acotada, concurrencia y protección del key ring. PostgreSQL conserva 20 tablas FORCE RLS y el contraste de deriva de políticas. | Restauración operativa, destino externo autorizado, cifrado/retención, RPO/RTO y rollback. El aislamiento de cookies de AccountHost depende de origen y nombre de base: cambiar la base requiere reautenticación o conservar esa identidad lógica. Reconciliar borrados y revocaciones posteriores al backup antes de admitir tráfico. |
| Fidelidad, N01–N03 | Las inferencias históricas A/B fallan aunque el transporte pase. El reemplazo factual ya está implementado localmente y en la rama de validación: 22 pruebas focales PostgreSQL, 478 de interfaz y dos documentos corregidos por Codex QA guardados y reabiertos con Qwen real. La campaña de tres llamadas conserva los errores crudos; no acredita fidelidad autónoma. | Cerrar CI completa, migración/publicación y aceptación humana del nuevo recorrido SaaS. La generación general y la autoría/actualización de plantillas conservan sus incidentes; no se cierran por la capacidad factual nueva ni se cambia su oráculo. |
| ChatGPT web, M02/M03 | Plugin multiusuario instalado, OAuth y permiso `Allow low-risk` visibles en la cuenta principal. La lectura limitada de `QA RC8 · Variables y revisión` termina con `This response couldn’t load`; recargar no recupera el texto. La traza visible dice que buscó y recuperó el recurso. | Presentación completa al usuario, segunda cuenta real, renovación y revocación del cliente. La causa del fallo de presentación sigue sin confirmar; no se atribuye a serialización MCP sin evidencia. |

Recibos locales: `brevo-mail-gate-20261028-02.json`,
`ops-recovery-evidence-rc8-20261028.json`,
`local-execution-factuality-20261028-02/semantic-review.json`,
`local-execution-factuality-20261028-03/semantic-review.json` y
`rc8-chatgpt-web-acceptance-20261028.json`, dentro del expediente
`.playwright/saas-delivery-20261026`. El
[diagnóstico factual](../nova/EJECUCION_FIDELIDAD_FACTUAL_20261028.md) separa
fallos de modelo, arnés y build, y conserva el diseño de reemplazo.

**La certificación y el tag estable siguen bloqueados por resultados y controles
concretos; no por falta de actualizar las casillas.**

### RC8 publicado — validación comercial en curso

El candidato `4236c3b69fee2a3e628e1a74c737aae31ef65d7e` pasó la
[CI remota 40736397613](https://github.com/JuanPabloTorres/QuickPrompt/actions/runs/40736397613)
y fue publicado mediante avance normal de la rama del piloto, sin tag ni cambios
en el índice local. Se recuperó la sesión del navegador y se desplegó ese commit
exacto en Render, `dep-dbh3pgjbc2fs73elqe70` (**Deploy succeeded | Live**).
Vercel promovió `dpl_BAjPR2qSSzb6vAvZUd5E6LurxSdF` sin reconstruir: la URL
principal sirve `/assets/index-Cwh0RRa1.js`, en modo `accounts`. Se conservan el
servicio gratuito, los secretos y las políticas de ingreso existentes. Este corte
todavía no está certificado comercialmente.

Google permitió volver a entrar como `est.juanpablotorres@gmail.com`. El MCP creó
sólo `QA RC8 · Variables y revisión` en su biblioteca, con las tres variables
exactas. Repetir la operación devolvió el mismo ID sin duplicarlo. La edición por
la web produjo revisión 2 y el MCP leyó ese contenido; rechazó compilar revisión
1, detectó `proposito` ausente, compuso literalmente las tres variables completas
y rechazó leer el recurso QA de otra cuenta. Esa creación pasó la biblioteca de
88 a 89 prompts activos. NOVA creó después únicamente la segunda fixture
`QA RC8 · NOVA conceptualización` (ID `0cf57619-2b45-4523-8f5c-86e07003bdb4`,
revisión 1): **90** activos en total. Los 84 migrados no se tocaron.
Recibos: `rc8-public-deployment-20261028.json`,
`rc8-mcp-crud-acceptance-20261028.json` y `rc8-public-mcp-auth-02-20261028.json`
en el expediente local de entrega. La lectura/creación real de este plugin no
certifica la renovación o revocación con un segundo cliente ChatGPT.

El primer intento público de autoría, sin worker local observado, terminó en
`MODEL_TIMEOUT`, devolvió el control y no guardó un prompt. La actividad exportada
conserva el run `6eae2303-4f7d-4d5d-b1da-26efc24ea6ac`; no es un fallo de semántica
de Qwen, porque no se observó inferencia. La revisión automática rechazó el inicio
oculto con logs privados. El lanzador oficial en primer plano, limitado a un solo
trabajo, sí inició el componente y alcanzó una llamada real a Qwen. Ese intento
llegó al contrato y a la composición, pero perdió la sesión antes de la aprobación
final; no se guardó y no se reutilizaron sus permisos. No se confirmó la causa de
esa pérdida de sesión. Una nueva entrada Google y otro intento identificado
completaron el recorrido público: contrato revisado, composición sin otra llamada
de autoría, aprobación final y lectura por MCP. Los ocho controles de
`rc8-public-nova-acceptance-20261028.json` comprobaron cuerpo idéntico al revisado,
descripción, nombre, URI, Factory obligatorio y exactamente tres variables. Hubo
cero coincidencias con ese título antes de aprobar y una después. Los workers
acotados terminaron; no se afirma que exista inferencia local siempre activa.

En ambos intentos con worker, la propuesta inicial de Qwen confundió el control
actual «no rellenes ahora sus valores» con una prohibición para el futuro
asistente. La revisión manual lo corrigió y precisó el uso de la referencia.
Este límite semántico se conserva como tal; el guardado técnico aprobado no
certifica comprensión autónoma ni aceptación del usuario final. AI Delivery Lab
incorpora una fuente RC8 separada de los cortes históricos y conserva ambos
resultados. La autoría revisada no cierra el incidente independiente de fidelidad
de ejecución de actas.

La corrección de cuotas separa el
presupuesto de usuarios autenticados detrás del mismo proxy: el contraste anterior
reprodujo dos bloqueos indebidos y la regresión focal pasó **44/44** pruebas.
Conserva un límite de ingreso físico y los límites previos de OAuth; no confía en
cabeceras de identidad o IP suministradas por el cliente. Su contrato y límites están
en `COMMERCIAL_SECURITY_ACCEPTANCE_20261028.md` (expediente local).

La autoría ahora se detiene ante un contrato editable: tarea, requisitos, variables
y papel explícito de cada enlace. Confirmarlo compone literalmente la plantilla,
sin otra redacción del modelo; el permiso final de guardar sigue separado. El
diseño de autoría confirmada del expediente local `../nova/FIDELIDAD_CONTRATO_CONFIRMADO_20261028.md`
conserva la propuesta de IA frente a la corrección humana. El candidato final pasó
**1409/1409** pruebas de backend Release, **8/8** de telemetría Debug,
**27/27** MCP, **16/16** Companion, **168/168** del consumidor legado y **463/463**
de interfaz. Son pruebas técnicas; no acreditan comprensión autónoma del modelo.
El recorrido local real con Qwen, contrato corregido manualmente y aprobación de
guardado conservó título, descripción, cuerpo y las tres variables exactas; hubo
cero prompts antes del permiso final y uno después. Los fallos anteriores de
continuación y pérdida de descripción se preservaron y sus correcciones se
validaron en ambos editores.

La campaña nativa con Qwen3:8b conserva **todos** los intentos en
`confirmed-brief-native-20261028-01` y `confirmed-brief-native-20261028-02`:

- El caso original de software conserva las tres variables y la URI al componer el
  contrato corregido sintéticamente. La propuesta inicial todavía convierte Factory
  en opcional: no acredita comprensión autónoma ni aceptación humana.
- En el primer caso ambiguo hubo una pregunta real y ningún borrador final. La
  prueba falló porque el arnés suponía que siempre recibiría un contrato. La
  pregunta no especificaba claramente aceptar frente a rechazar.
- Tras corregir sólo ese arnés, otra ejecución propuso evaluar neutralmente en vez
  de preguntar. La composición del contrato corregido pasó, pero la ruta nativa de
  continuación de esa pregunta **no se alcanzó en ese intento**. No combinar ambos
  intentos para declarar ese recorrido aprobado.

La CI ejecutó la restauración sintética de PostgreSQL: **377 aprobadas, 19
omitidas y cero fallidas**. Incluye descifrar un payload anterior al backup con
negativos de certificado/aplicación. Sus dos recibos JSON conservados verifican
20 tablas con RLS forzada, aislamiento anónimo, borrado, privilegios y el contraste
del fingerprint de políticas antes/después de pg_dump/pg_restore. Esto no restaura
datos privados ni certifica el backup productivo o sus objetivos de recuperación.
Docker Desktop
falla en Windows con error 1920 al recrear sockets transitorios de Ingest/Secrets
Engine. Conservar y renombrar sólo sus directorios de sockets permitió avanzar una
frontera, pero no recuperar el motor. No se restablecieron datos, discos ni WSL.
La validación PostgreSQL se ejecutó en el runner Linux del workflow existente,
sin usar la base ni el respaldo privados. La ejecución remota está ligada al SHA
del candidato; los recibos de Render y Vercel demuestran su despliegue por separado.

Brevo respondió al ticket 5641363 solicitando los dos formatos del móvil para
revisión manual; el usuario informó haberlos enviado. La activación del proveedor,
credencial limitada, recepción de confirmación y recuperación siguen pendientes de
comprobar. No se sustituyen por correo de desarrollo ni por iniciar sesión Google.
La lectura local actual de Ollama vuelve a mostrar un listener `::`:11434; la API
de loopback responde y contiene Qwen3:8b, pero no debe reutilizarse el recibo
anterior de loopback como prueba del estado presente.

**Dictamen del candidato: NO GO comercial.** Su fuente identificada y CI están
publicadas y el despliegue coordinado está comprobado. Falta completar los
recorridos reales pendientes y cerrar los controles
operativos de recuperación, correo, MCP y capacidad. El fallo de fidelidad al
ejecutar el acta de RC7 tampoco queda reparado por cambiar la autoría.

### Corte RC7 publicado — 28/10/2026

La web y el backend están activos en `https://project-sxf1z.vercel.app`. El
snapshot publicado corresponde al commit `06be920b566cef67ce5b171ef71e47913d7aca39`
y al candidato `quickprompt-pilot-20261028-rc7`. Render desplegó ese commit en
`dep-dbgvlb942hec73e42m2g`; Vercel recibió 138 archivos de fuente congelada,
construyó en Linux y promovió `dpl_2irZxZYcWVNY9NJhYMJDBQU5hyGe`. La CLI de Vercel
detectó además el HEAD local antiguo; la procedencia de lo subido se demuestra con
el manifiesto y la metadata explícita `sourceCommit`, no con esa detección Git.
No se creó un tag ni una certificación comercial.

- Pruebas finales: backend Release **1401/1401**, Accounts con PostgreSQL
  **368 aprobadas y 16 casos de Qwen omitidos deliberadamente**, frontend
  **458/458**, MCP **27/27**, Companion **16/16**. Build de cuentas y Linux
  aprobados. Los fallos previos de arnés/configuración y sus recibos se conservan;
  estos contadores no certifican semántica ni capacidad comercial.
- Google permitió entrar de nuevo como `est.juanpablotorres@gmail.com`. Su
  biblioteca contiene los 84 prompts migrados y los recursos previos/de prueba;
  tras borrar la copia autorizada quedaron **88 prompts activos**. No se borró
  ninguna de las tres bibliotecas originales. La excepción documentada de Google
  para `openid`, `email` y `profile` permite usar el modo Testing sin añadir cada
  usuario a una allowlist; no acredita verificación de marca ni acceso permitido
  por administradores Workspace. [Comportamiento oficial](https://developers.google.com/identity/protocols/oauth2/production-readiness/overview).
- CRUD público observado: crear, leer, editar, favorito, archivar/restaurar,
  duplicar y borrar sólo la copia sintética confirmada. PostgreSQL confirmó
  borrado de su contenido/versiones, cero membresías y original intacto. Una
  colección creada en biblioteca apareció en el selector del detalle; se asignó
  el prompt. Preparación sin IA y copia conservaron ambos valores; lectura MCP
  comprobó un único uso, sin modificar la plantilla.
- Compartir A/B público: la cuenta principal publicó la revisión 5 del prompt
  sintético de auditoría. La cuenta receptora `jptorres.software@gmail.com` sólo
  vio su biblioteca, aceptó la invitación, leyó esa versión exacta e importó una
  copia con ID distinto y revisión 1. El propietario revocó la publicación; un
  login fresco del receptor mostró **Acceso no disponible**, sin acción de leer
  o importar. Su copia independiente permaneció disponible. Esta prueba no
  concede acceso a otros prompts ni demuestra envío de correo. Recibo:
  `rc7-public-sharing-ab-20261028.json`.
- El plugin **QuickPrompt · Piloto multiusuario** ya buscó, creó, compiló y aplicó
  una plantilla desde ChatGPT web. Tras desplegar RC7, una lectura real conservó
  el mismo permiso y devolvió el recurso exacto de la cuenta principal. La
  presentación falló inicialmente y luego funcionó sin cambiar el formato MCP;
  no se atribuye una causa interna. Falta completar dos cuentas reales de ChatGPT
  y revocación/renovación del cliente final; no ampliar esos permisos para simular
  el cierre. POST MCP anónimo devolvió 401 con challenge canónico; Origin ajeno y
  entrada directa al backend devolvieron 403.
- Ambos perfiles principales se ejecutaron con **Qwen3:8b local**. La respuesta del
  prompt quedó guardada, pero añadió participantes no confirmados en las notas:
  **fallo de fidelidad**. NOVA reprodujo la petición original y generó los tres
  marcadores y campos correctos; omitió la URI de referencia y convirtió Factory
  en opcional. Se rechazó sin guardar. En otro caso ignoró la solicitud de
  preguntar antes de elegir y redactó una plantilla neutral; se rechazó también.
  El puerto tipado de aclaración pasó QA, pero su rama real quedó
  **NOT_REACHED**. No presentar estas propuestas como aceptación humana ni
  reintentar hasta obtener una muestra favorable.
- El respaldo real de 33 tablas y tres secuencias quedó fuera de Git, con SHA-256
  `507f15099c85563272c7b2cdb362b4e0b2e2727778d8482d603ba4aa3e2bdf4d`.
  Se leyeron/descomprimieron todos sus bloques. Se retiraron todos los roles y
  políticas temporales; la comprobación posterior al borrado volvió a confirmar
  cero accesos temporales. No se restauró la base privada en QA. Recuperación
  real, automatización, retención y objetivos RPO/RTO siguen pendientes.
- Accesos de escritorio: **QuickPrompt Cloud**, **QuickPrompt NOVA Público** y
  **QuickPrompt Prompts Público**, con icono oficial. Los dos últimos reutilizan
  el companion y sus perfiles existentes; máximo 12/1 trabajos y cinco minutos
  inactivo, sin autoinicio. Para usar la inferencia local el equipo y Ollama deben
  permanecer disponibles. DbGate conserva el perfil cifrado local y una consulta
  de lectura de la biblioteca; no se confirmó su apertura gráfica final.

**Dictamen: publicado para evaluación supervisada; NO GO comercial.** Bloquean el
cierre los fallos semánticos registrados en AI Delivery Lab, la rama real de
aclaración, las verificaciones A/B aún pendientes, correo externo sin configurar,
recuperación/backup operativo, capacidad y aceptación SaaS. Render Free puede
arrancar lentamente tras inactividad y la base gratuita vence el **26/11/2026**;
no se contrataron planes ni se prometió disponibilidad comercial.

El ajuste local de Ollama se cerró con evidencia: su aplicación tenía activado
**Expose Ollama to the network**, que mantenía el servidor en `::`:11434 pese a
la variable de usuario de loopback. Se desactivó esa opción desde los ajustes
oficiales. El proceso posterior escuchó sólo en **127.0.0.1:11434**; la API
respondió y confirmó Qwen3:8b instalado. Ambos perfiles principales confirmaron
su autorización vigente después del ajuste, sin nuevas inferencias. La negativa
automática del reinicio por shell y el primer chequeo fallido se conservan; no se
repitió ese comando ni se cambió el firewall. Recibo final:
`rc7-local-ollama-ui-final-20261028.json`. Esto no convierte los tres resultados
semánticos públicos fallidos en casos aprobados.

Recibos: `rc7-public-deployment-20261028.json`,
`rc7-primary-qwen-acta-acceptance.json`, `rc7-nova-original-public-acceptance.json`,
`rc7-public-ai-lab-report.json`, `rc7-real-copy-erasure-20261028.json` y
`public-backup-integrity-20261028.json`, bajo `.playwright/saas-delivery-20261026`.
La observación puntual de un primer valor vacío después de cerrar colecciones no
se reprodujo al repetir el cierre ni en dos contrastes de componentes con
respuestas tardías y catálogo diferido. Se conserva como causa desconocida; no
se cambió producto por esa hipótesis. El primer arnés tenía datos de colección
incompatibles y se corrigió sólo el fixture, preservando el fallo inicial.

### Corte anterior del 28/10 — previo a RC7

El piloto público recibió únicamente la corrección CSP del callback registrado
de ChatGPT: backend `09c5698e31865c9caa6b07fd9331b98edbf31d27`, Render
`dep-dbgslo08v5cc73fgabt0` y Vercel `dpl_xW2ztzHZDx7Rrv5B7xFQFhoaxjGg`.
No contiene todavía las correcciones posteriores de interfaz, borrado y admisión.
Las identidades de la primera publicación de la tabla inferior son históricas.

- Los 84 prompts de las tres bibliotecas se copiaron a la biblioteca privada de
  `est.juanpablotorres@gmail.com`; lectura fresca cotejó los 84 títulos/cuerpos,
  conservando un prompt previo. No se migraron historiales ni valores privados.
- El plugin **QuickPrompt · Piloto multiusuario** completó OAuth y llamadas reales
  de búsqueda, lectura, creación, compilación e idempotencia en esa biblioteca.
  Se conservó el plugin personal anterior. Las primeras vistas de ChatGPT no
  pudieron cargar respuestas; una conversación guardada recuperada mostró después
  la búsqueda exacta. ChatGPT web también creó otra plantilla con dos variables:
  lectura MCP posterior comprobó texto/revisión y una sola coincidencia. Su
  aplicación en ChatGPT conservó los tres hechos sintéticos y la fecha pendiente.
  No se cambió el formato MCP para obtener ese resultado ni se atribuye una causa
  interna al fallo inicial de presentación.
- Los perfiles **Mi-PC-NOVA** y **Mi-PC-Ejecutar-Prompts** quedaron autorizados
  separadamente en la cuenta principal. Autorizar no demuestra un worker activo
  ni una ejecución completa; falta la aceptación del candidato publicado.
- Frontend: 444/444 pruebas, tipos, build de cuentas y 40 controles estáticos
  aprobados, con 188 entradas estables. Es evidencia local de su corte, no de la UI
  publicada ni de la calidad semántica de Qwen. La corrección posterior del texto
  de conexiones pasó otras 42 pruebas focales. El backend actual pasó 1389/1389;
  Accounts/PostgreSQL pasó 355 y omitió deliberadamente 16 casos con Qwen.
- Restauración sintética con borrado y RLS restrictivo comprobada. Un procedimiento
  administrativo de respaldo con rol temporal NOLOGIN se corrigió tras reproducir
  el rechazo `0LP01` de una concesión ADMIN redundante. El respaldo real de Render
  se creó el 28/10: 33 tablas, tres secuencias y descompresión completa verificadas;
  roles, políticas y catálogo quedaron restaurados. SHA-256 del archivo privado:
  `507f15099c85563272c7b2cdb362b4e0b2e2727778d8482d603ba4aa3e2bdf4d`.
  No se restauraron datos privados en QA ni se certificó recuperación del proveedor.
  El recibo original conserva un defecto de envoltura de PowerShell; la inspección
  posterior validó el único descriptor del archivo sin reescribir ese recibo.
- Las migraciones `20261028092140_PrivatePromptErasure` y
  `20261028113000_PrivateErasureVisibility` se aplicaron en Render con el migrador
  existente y VerifyFull. La comprobación posterior confirmó 20 tablas FORCE RLS,
  tres políticas restrictivas y ausencia del acceso temporal de respaldo; no borró
  prompts ni sustituye la aceptación del CRUD publicado.
- Las nueve llamadas semánticas anteriores a la continuación tipada conservan fallos de utilidad. La unión
  exclusiva ya es compatible con Core y la gramática nativa; el caso original aún
  pide el tipo como dato actual. Un contraste aislado con thinking produjo las
  tres variables, pero siguió encargando escribir otro prompt. No se habilitó como
  supuesta solución ni se fabricó aprobación humana o semántica.
- El recibo HTTP seguro de MCP pasó 64/64 pruebas de frontera y autorización.
  Su publicación y correlación con ChatGPT web están pendientes. La continuación
  de preguntas de contenido de NOVA ya tiene un puerto tipado, conservando el
  encargo original, la acción, la fuente y las variables; su nueva aprobación no
  se hereda. El reenvío humano de una respuesta incierta conserva el mismo UUID.
  Sus pruebas focales no sustituyen la aceptación pública con Qwen.

RC5 se congeló y compiló en Linux; cambios posteriores de diagnóstico y aclaración
lo superaron y requieren otro candidato identificado. No se publicó como cierre.
Correo externo, recuperación real, publicación correlacionada y recorridos finales
siguen siendo puertas de salida. El acceso Google puede servir al piloto; no
sustituye la confirmación y recuperación por correo ni cambia el NO GO comercial.

Hay una base multiusuario implementada y comprobada en QA. La salida comercial
requiere, además, verificar el mismo candidato desplegado con dos cuentas reales,
un dispositivo autorizado y un cliente ChatGPT. No se obtiene sumando pruebas de
componentes ni trasladando la aceptación del antiguo modo local.

## Ruta elegida — web Vercel y backend persistente

El usuario eligió web React/Vite en Vercel y backend ASP.NET Core persistente con
PostgreSQL, cola y companion existentes. Vercel no aloja el agente ni recibe
credenciales de PostgreSQL, certificados o claves de delegación. La
guía híbrida local `../../deploy/vercel/README.md` posee build, rutas y proxy; la
guía local del backend `../../deploy/cloud/README.md` posee ingreso autenticado y claves estables.

El proyecto `quickprompt-web-jpt`, ID `prj_fHkWBxkbKbx1WUcJ3GFnDACucSaC`, se creó
en el equipo Hobby verificado. La creación inicial por plugin devolvió 403 y se
resolvió mediante la UI; ese fallo se conserva. **El piloto ya está publicado**:

| Capa | Identidad observada del piloto | Alcance |
|---|---|---|
| Fuente de backend | Commit `ab72213bb89c443b11445581e8a3b7299f617336`, rama `deploy/quickprompt-saas-pilot-20261027`, 376 archivos. | Fuente publicada para el piloto; no sustituye un manifiesto/gates correlacionados del cierre comercial. |
| Render | Servicio `srv-dbgppfu7bikc73b93oog`, deploy `dep-dbgpvkvlk1mc7396p12g`, **Live**; [backend HTTPS](https://quickprompt-nova-jpt.onrender.com). | Health público 200, modo accounts-candidate. No es aceptación del agente. |
| Vercel | Deploy `dpl_46Dsdg4js7f3ECiwLEY33aaGuoSF`, **Ready**; [web canónica](https://project-sxf1z.vercel.app). | HTML de cuentas y proxy comprobados por lectura HTTP. La primera publicación `dpl_FkXN3eqDv56zmof7a55ZSRFYmgML` permanece como historia. |

La primera publicación tenía una regresión P2: el HTML estático no recibía CSP,
Referrer-Policy ni nosniff, aunque la API sí. La nueva publicación sólo cambió
`vercel.json` de sus 129 inputs (`vercel-security-redeploy-boundary.json`). Cuatro
GET posteriores confirman CSP idéntico a AccountHost con `frame-ancestors 'none'`,
`strict-origin`, `nosniff`, `no-store` y HSTS en HTML/SPA/metadata/API. P2 cerrado
para ese alcance HTTP; no se borra la observación inicial ni se deduce una prueba
de navegador o de ausencia de otras vulnerabilidades.

La receta RenderFreeEdgeOnly se retiró de la configuración activa; conserva sus
resultados históricos. El piloto utiliza OriginProxy, no un CIDR de confianza
inventado ni acceso del backend a localhost del cliente.

La adaptación OriginProxy cambia la frontera de red. Los cortes 1377 de aplicación
y 264 PASS/16 omitidas de Accounts de la tabla siguiente son **anteriores** a ella.
También lo son la imagen `eac395f765f3cea7737d99bc13ba12cd8f20e3f1208ff0a15fb40bfc25f19586`
y el baseline `quickprompt-saas-proposal-20261027-rc2`, SHA256
`cccc14354a99889e84cfdead6705583735108e7dca17ad35c227b6a036361cb1`.
Se conserva `BASELINE_CAPTURED`, sin cierre ni atribuirle cambios posteriores.
Faltan artefactos correlacionados y aceptación del mismo candidato híbrido.

## Cómo usar el checklist

- **Verificado localmente**: existe una ejecución con el alcance indicado. No
  acredita por sí sola otro binario, otra migración o el servicio publicado.
- **Pendiente de candidato final**: el mecanismo existe, pero falta repetir su
  aceptación después de los cambios que afectan al recorrido.
- **Bloqueado por acceso/configuración**: falta una señal externa o una entrada
  necesaria. La falta de credenciales no autoriza a debilitar controles.
- **No ejecutado**: no hay evidencia suficiente para marcar la condición.

Un `[x]` acredita exclusivamente su frase y evidencia. Los elementos pendientes
son puertas de salida, no recomendaciones opcionales. El piloto gratuito puede
servir para demostrar el recorrido sin ser una oferta comercial operable.

## Qué está comprobado

Los recibos locales citados viven en `.playwright/saas-delivery-20261026/` y se
conservan fuera de Git por la política de evidencia local `../evidence/README.md`.
Son ejecuciones identificadas; no deben presentarse como un candidato final
correlacionado retroactivamente.

| Área | Comprobación observada | Evidencia y límite |
|---|---|---|
| Backend, corte previo a OriginProxy | **1377/1377 PASS**, cero omitidas. Guardias de revisión antes de efectos: **96/96 PASS**. | `tests/r3-content-review-full-final.trx` y `tests/r3-content-review-effect-boundary-second.trx`; sin modelo, PostgreSQL, DOM real ni aceptación humana. No acreditan la adaptación híbrida; se conservan los fallos de fixtures. |
| Cuentas y PostgreSQL, corte previo a OriginProxy | **264 aprobadas, cero fallidas y 16 omitidas** de 280 descubiertas, después de los guardias previos a efectos. | `tests/draft-proposal-accounts-postgres-p1-final.trx`; las 16 de proveedor real se desactivaron explícitamente. QA `quickprompt_development`, sin base humana ni modelo. Cinco DLL estables durante la ejecución; sin jobs/leases activos al terminar. No son una regresión posterior al cambio de ingreso. |
| Frontend, corte previo a la adaptación híbrida | **396/396** pruebas, 51 suites, cero pendientes; build aprobado del diálogo de propuesta completa, diferencias literales y CONTENT_ONLY. | `tests/draft-proposal-web-full-final.json` y `tests/proposal-review-purpose-saved-build-final.json`; este último es recibo de herramienta, sin correlación retrospectiva a candidato. No acreditan modo Vite/proxy/SSE posteriores, DOM real ni aceptación humana. |
| Frontend híbrido, corte propio | **403/403 PASS**, 52 suites, cero pendientes. | `tests/vercel-web-full-final.json`; no vuelve a certificar backend/Accounts, SSE público, inferencia o aceptación humana. El 396 anterior conserva su alcance histórico. |
| Tooling de política Vercel, corte propio | **12/12 PASS**, cero omitidas. | `tests/vercel-security-baseline-final.tap`; configuración/build de despliegue, no prueba completa de usuario. No se suma a las 76 de hosting como certificado global. |
| Lectura HTTP pública independiente | HTML accounts; tres metadatos JSON/issuer exactos y JWKS público; opciones Google habilitado/correo desactivado; me anónimo 401, backend directo 403 y Origin ajeno 403. Cabecera falsa del cliente no altera la respuesta correcta del proxy. | `public-http-independent-20261028-01.json` y `public-http-independent-20261028-01-assessment.json`; 12 GET sin credenciales. El retest `public-http-headers-retest-20261028-03.json` cierra la regresión de cabeceras tras la nueva publicación. |
| Challenge MCP público anónimo | POST initialize rechazado con **401**, `resource_metadata` canónico y scope mínimo `prompts:read`, sin cookie ni sesión MCP. | `public-mcp-anonymous-initialize-20261028-02.json`; una petición sin herramientas/credenciales. El GET 405 previo se conserva como método no soportado, sin afirmar bypass. No prueba OAuth autenticado ni ChatGPT. |
| Navegador público, recorrido de raíz | Callback Google del origen final guardado; login real con la cuenta de trabajo, biblioteca demo y prompt manual con tres variables creados. | Observación de raíz el 28/10 y `google-pilot-callback-saved.png`. Es creación manual, no inferencia NOVA, prueba A/B o aceptación humana del SaaS. |
| Revisor histórico con Qwen | Cuatro controles de la familia de aislamiento de autoría aprobados, incluido el flujo válido de aclaración. | `tests/market-single-definition-real.trx`; incluye replay controlado de autoría, no acredita toda la generación ni fiabilidad universal. |
| Cobertura ampliada con Qwen | Dos casos aprobados y dos fallidos. Paráfrasis completa y tarea futura JSON anidada fueron aceptadas; el esquema incompleto no alcanzó el rechazo semántico esperado y la adaptación correcta al origen no obtuvo una revisión válida. Ningún fallo registró aceptación de contenido. | `coverage-exhaustive-real/*-result.json`: `observed-outline` terminó en `PreparationToolException`; `source-delta` en `CONTENT_FIDELITY_REVIEW_INVALID`. Seguridad cerrada no equivale a completar la tarea. |
| Autoría RC1 retirada | **0/3** con Qwen: inventario inválido, sin assessment ni efecto. | Candidato `quickprompt-saas-r3-20261027-rc1`, SHA256 `f98905323f00448ca551d623ecb52f28a107808a2b43b432813442aeb2113834`; snapshot y fallos conservados. No se reutiliza su certificado de tests como aceptación del reemplazo. |
| Propuesta completa con Qwen | Probe de componente: **tres llamadas, arnés 2 PASS / 1 FAIL**. Inspección independiente: software **FAIL**, correo útil, reducción útil por paráfrasis con ACK humano pendiente. | `draft-proposal-component-real-20261027/`; huellas antes/después del componente, no candidato íntegro. El fallo literal de reducción se conserva. No prueba cuentas, escritura, navegador ni aceptación humana. |
| Reparación de referencias | 18 pruebas de contrato aprobadas para transmitir diferencias y preservar papeles/enlaces fijados desde el encargo. | `tests/market-reference-feedback.trx`; no demuestra que Qwen repare todos los casos reales. |
| Hosting, TLS y certificados, historial | 37 pruebas puras aprobadas y 55 del perfil Render retirado. La primera campaña PostgreSQL de rotación tiene siete aprobadas y una fallida; la corrección con proveedor reiniciado pasa. | `tests/hosting-tls-rotation-verified.trx`, `tests/hosting-render-edge-only.trx`, `tests/market-host-mcp-export-rotation.trx` y `tests/hosting-rotation-restart.trx`. No acreditan OriginProxy ni el runtime híbrido. |
| Ingreso OriginProxy | **76 pruebas puras PASS**, cero fallos, de configuración, cabecera privada, Host/Origin y superficie de cuentas. | `tests/hosting-origin-proxy-final.trx`; primer corte 71 PASS/4 FAIL preservado, corrección de autoridad canónica y corte intermedio 75 PASS conservados. No prueba Vercel, upstream HTTPS, SSE ni acceso público del backend; sin rebuild de Docker. |
| Renovación SSE, QA local | **1 PASS**: renovación a 30 s conserva run/secuencia/propuesta/acción pendiente; Last-Event-ID evita replay y el propietario cancela al final. | `tests/hosting-visible-sse-renewal.trx`, con cero jobs/leases antes y después. Sin inferencia; falta comprobarlo a través del proxy Vercel real. |
| Errores del proveedor | Tres casos conservan diagnóstico, liberan control y no guardan ni repiten acciones. | `tests/market-provider-failures.trx`; prueba controlada, no caída real del proveedor en el servicio publicado. |
| Concurrencia de persistencia | 16 propietarios simultáneos, cuatro rondas y 320 llamadas con comprobaciones positivas/negativas de propietario. | `tests/market-concurrent-private.trx`; secuencia de cinco llamadas: p50 538,0 ms / p95 725,3 ms en esta máquina. No es carga HTTP pública ni un SLA. |
| Restauración | Huella de datos sintéticos, migraciones, permisos y 20 tablas FORCE RLS conservados. | `market-restore/restore-result.json`; repetir sobre el esquema final si se añaden tablas/migraciones. Nunca usa la base humana. |
| Key ring de host | Claves cifradas y permisos de la nueva tabla de host conservados al restaurar; las tablas de contenido siguen siendo 20 con FORCE RLS. | `market-host-key-restore/restore-result.json`; la tabla de claves es infraestructura compartida del host, no memoria por usuario. No prueba rotación ni restauración del proveedor público. |
| Recorridos de cuentas | Preferencias privadas, creación/edición, lectura de versiones y recuperación del contenido como nueva revisión comprobados en navegador. | `market-private-versions.png` y recibos del recorrido; no equivalen a aceptación humana del SaaS. |
| Companion Windows | A completó el flujo nativo con Qwen, cinco llamadas y aprobación consumida; B rechazó contaminación y terminó en fallo del proveedor. | `market-native-nova-a/` y `market-native-nova-b/`. El productor y la propagación del error se cambiaron después: ambos recorridos finales siguen siendo obligatorios. |
| Dependencias | Consulta de NuGet sobre 14 proyectos sin vulnerabilidades reportadas; npm de producción sin vulnerabilidades reportadas. | `market-dotnet-vulnerabilities.json` y salida de `npm audit --omit=dev --json`; consulta fechada, no auditoría de seguridad completa. |
| Protocolo MCP y cliente local, corte previo a la adaptación híbrida | **27/27** del MCP local y **16/16** del cliente Windows, cero omitidas. | `tests/draft-proposal-mcp-final.trx` y `tests/draft-proposal-companion-final.trx`; transporte sintético/controlado, sin Qwen ni ChatGPT real. El MCP delegado se prueba en Accounts; el plugin público multiusuario sigue pendiente. |

La autoría vigente se detalla en el expediente local `../nova/FIDELIDAD_R3_DISENO_20261027.md`: contexto de aplicación, una propuesta IA completa y revisión humana ligada a cuerpo, metadata, fuentes y diferencias literales. `CONTENT_ONLY` consume revisión para continuar y **no autoriza guardar**. El campo/URI presente no demuestra utilidad; **IA no resuelta**.

## Puertas de aceptación del producto

| ID | Verificación para el mismo candidato | Estado de cierre |
|---|---|---|
| U01 | [x] Identidad por cuenta; actor obtenido de la sesión/token y fuera de argumentos del modelo. | Verificado localmente en HTTP/PostgreSQL. |
| U02 | [x] A y B conservan bibliotecas, prompts, revisiones, colecciones, valores, resultados y memoria independientes. IDs ajenos no cambian actor. | Verificado localmente; repetir recorrido publicado A/B. |
| U03 | [ ] Registro, confirmación, recuperación, cierre de todas las sesiones y vinculación Google/contraseña funcionan desde la interfaz publicada. No unir cuentas sólo por email coincidente. | Login Google público y callback final comprobados por raíz. Correo/contraseña, confirmación, recuperación y cierre/vinculación completos siguen pendientes; no cerrar esta puerta con un solo login. |
| D01 | [x] Migraciones y roles limitados, aislamiento en aplicación y defensa RLS comprobados. | Verificado localmente. El rol de migración no debe usarse como fallback del host. |
| D02 | [ ] Aplicar las migraciones finales y restaurar una copia del destino publicado con roles, políticas, índices y datos sintéticos intactos. | f49: migraciones finales aplicadas y restauración privada real de 35 tablas/22 FORCE RLS/31 migraciones/tres secuencias aprobada. Faltan equivalencia completa de catálogo/índices y flags de roles; la copia privada no se usó como QA sintético. |
| A01 | [x] Preferencias y contexto conservado están separados por usuario; recuperar no restaura aprobaciones ni repite efectos. | Verificado localmente. |
| A02 | [ ] Borrar/olvidar datos, retención, exportación y tratamiento de backups tienen un recorrido comprobado y una política operativa comunicada. | Hay olvido y caducidad de datos de ejecuciones/contextos; no equivalen a borrado completo de cuenta/backups. |
| S01 | [x] A publica una revisión concreta; B sólo lee/importa después de aceptar una invitación. La copia no modifica el original. | API/PostgreSQL/SDK MCP comprobados; faltan todas las superficies del candidato público juntas. |
| S02 | [x] Revocar bloquea lecturas e importaciones futuras; un recibo antiguo no recupera autoridad. Las copias ya importadas siguen siendo propias. | Verificado localmente. |
| N01 | [ ] NOVA crea/abre/actualiza con el cuerpo, campos, fuente y revisión correctos en A y B usando el Windows Companion real y el navegador real. | RC8 completó creación pública en A con contrato corregido manualmente, revisión exacta y permiso final: cuerpo, descripción, URI, Factory y tres variables conservados, con lectura MCP posterior. No acredita apertura/actualización A/B ni comprensión autónoma. Acta A reproducida y control B fallan fidelidad factual; los incidentes permanecen abiertos. |
| N02 | [ ] La campaña final cubre éxito, paráfrasis, ambigüedad, referencias, omisiones, contenido extra, protocolo filtrado, proveedor caído, espera, cancelación y revocación. | Cobertura con citas: 2/4 y sin assessment en los fallos. Cobertura con IDs: 2/4, con falso positivo confirmado en el esquema incompleto y un fallo de proveedor. El inventario previo RC1 falló 0/3 y fue retirado. La propuesta completa posterior conserva un fallo de utilidad de software aunque el arnés de ese caso pase. Campaña y aceptación finales pendientes. |
| N03 | [ ] Ámbitos/campos sin confirmar piden aclaración sin inferir; formato inválido y errores tipados conservan diagnósticos; contenido requiere revisión exacta y ACK de cada diferencia literal. | Puerto tipado de aclaración, replay y errores comprobados en QA. En RC7 real Qwen ignoró la petición de preguntar antes de decidir: rama NOT_REACHED, propuesta neutral rechazada. El caso software conservó campos pero perdió una referencia. Revisión humana limita efectos; no acredita fidelidad. |
| E01 | [x] Cola, cuotas, leases, cancelación y recuperación evitan repetir automáticamente un efecto incierto o ya confirmado. | Integración local comprobada; reinicio/recuperación del despliegue final pendientes. |
| C01 | [x] El servidor no intenta alcanzar el `localhost` del usuario; el dispositivo local tiene identidad, perfil y consentimiento propios revocables. | Ambos perfiles limitados de la cuenta principal fueron aprobados y ejecutados. RC8 observó workers oficiales acotados y Qwen local; terminaron al completar su trabajo. No hay evidencia de un worker siempre activo ni de que abrir el acceso directo lo mantenga disponible. |
| M01 | [x] MCP delegado exige identidad, audiencia, biblioteca y scopes por conexión. Refresh no amplía permisos y revocar invalida acceso futuro. | SDK real/HTTP/PostgreSQL comprobados; no acredita la instalación final en ChatGPT. |
| M02 | [ ] Dos cuentas de ChatGPT conectan el endpoint público por OAuth; cada una busca, lee, compila y guarda sólo con su scope, y no puede acceder a recursos de la otra. | Una cuenta ChatGPT de la biblioteca principal ya buscó, leyó, creó, compiló y aplicó contenido. El mismo permiso siguió leyendo tras desplegar RC7 y tras cambiar de cuenta web. A/B web e importación/revocación reales comprobados; falta una segunda cuenta real de ChatGPT. |
| M03 | [ ] Consentimiento, descubrimiento, denegación, scopes opcionales, PKCE, errores, renovación y revocación funcionan con el cliente ChatGPT real. | OAuth y consentimiento reales de la biblioteca principal, lectura/creación y continuidad del permiso tras desplegar comprobados. Challenge anónimo 401 y Origin ajeno 403 en RC7. No se ejecutó revocación/renovación de ese cliente ChatGPT en producción; conservar esa puerta abierta. |
| W01 | [ ] Biblioteca, editor, variables, uso, colecciones, resultados, versiones, exportación y borradores tienen navegación y errores recuperables en escritorio y móvil. | Producción: preparación sin IA, copia idempotente y reapertura exacta. QA: creación manual con tres variables, colección desde biblioteca visible en detalle, edición, recuperación de versión, archivo/recuperación y persistencia; fallo de inspección archivada corregido en detalle/editor. Borrador recuperado con valores tras recarga y bloqueo de sobrescritura cuando su fuente cambia. Modal móvil/escritorio comprobado. El 29/10 el usuario confirmó título y tres variables del JSON descargado; no se inspeccionó automáticamente el archivo. Falta el recorrido completo del nuevo candidato publicado. |

## Puertas del piloto público y de operación comercial

| ID | Condición verificable | Piloto / comercial |
|---|---|---|
| P01 | [x] Web Vercel y backend persistente creados, desplegados y accesibles por sus URLs reales. | **Verificado para el piloto 28/10**: Vercel Ready y Render Live en las identidades/URLs de la tabla. No cierra agente, plugin, aislamiento A/B ni operación comercial. |
| P02 | [ ] Mismo candidato híbrido arrancado; proxy sobrescribe cabecera privada, usa upstream HTTPS literal y conserva cookies, CSRF, redirects, issuer y SSE. Acceso directo sin secreto se rechaza salvo health terminal limitado. | **Parcial**: proxy/upstream público, rechazo directo/origen ajeno, header falso, issuer/MIME, baseline de HTML y challenge MCP anónimo comprobados. Google login real observado. Faltan SSE a través del proxy, continuidad/recuperación de cookies y sesión, CSRF/redirects negativos y flujo completo del mismo candidato. No marcar esta puerta completa. |
| P03 | [ ] Base PostgreSQL externa configurada con TLS y roles separados; sólo fixtures autorizados en las pruebas públicas. | **29/10, f49**: 31 migraciones exactas, 35 tablas/22 FORCE RLS/tres secuencias, roles limitados y restauración privada del esquema final comprobados. Login A/B y rechazo de la fixture A desde B verificados; se conservan las 84 definiciones migradas originalmente. Faltan negativos completos de sus recursos factuales en el destino publicado. |
| P04 | [ ] Llaves de delegación y Data Protection persisten, se cifran y rotan con procedimiento probado; reiniciar no cambia autoridad ni permite replay. | Key ring y restore local comprobados. Rotación: fallo inicial conservado y prueba con proveedor reiniciado PASS en `tests/hosting-rotation-restart.trx`. Repetir continuidad/restore en el backend persistente final; no sustituirlo por disco efímero o claves en Vercel. |
| P05 | [ ] Correo de confirmación/recuperación recibido y utilizado; envío fallido tiene diagnóstico y recuperación sin enumerar cuentas. | **Bloqueado**: la aplicación pública devuelve `mailDelivery=unconfigured`; el panel Brevo exige teléfono y el usuario reportó fallo real de SMS. Seguimiento autorizado del 29/10 enviado en ticket **5641363**, comentario `40247224582418`, estado Open. No hay entrega comprobada. Mailjet permanece sólo en investigación; no se habilitó otro proveedor. |
| P06 | [ ] Google utiliza un proyecto/cliente de producción separado y callbacks del origen final. | Consola real: callback Vercel correcto, compartido con localhost en el cliente Local; audiencia External / Testing, un usuario de prueba, publicación deshabilitada por Branding incompleto. Página principal, privacidad y condiciones vacías. No se publicó ni amplió acceso. El login público anterior no cierra estos pendientes. [Política oficial de Google](https://developers.google.com/identity/protocols/oauth2/production-readiness/policy-compliance). |
| O01 | [ ] Backups externos automáticos, cifrado, retención y restauración se verifican con objetivos de pérdida de datos y tiempo de recuperación acordados. | **Parcial, 29/10**: respaldo y restauración privada finales de 35 tablas/22 FORCE RLS/31 migraciones/tres secuencias aprobados y ejecutados una vez, referencias intactas antes/después de limpieza, copia apagada y ACL comprobada. Se conservan los recibos anteriores de 33 tablas. Faltan respaldo externo automático/cifrado, retención, RPO/RTO y continuidad de claves/sesiones; no cierra la puerta comercial completa. |
| O02 | [ ] Health/readiness, errores, cola, cuotas, fallos del dispositivo y expiraciones tienen alertas y un responsable de respuesta. Logs sin credenciales ni razonamiento privado. | fe373fc verifica health 200→503→200, señales internas tipadas y ausencia de payloads/credenciales en ellas. Faltan collector, entrega de alertas y responsable operativo; no basta el probe ni contar logs como notificación entregada. |
| O03 | [ ] Carga HTTP y del agente con la capacidad inicial prevista, fallos de almacenamiento y más de una instancia no mezclan usuarios ni exceden cuotas. | CI fe373fc: diez cuentas sintéticas, dos hosts, límites 2 global/1 propietario, cuatro admisiones y un rechazo por cuota, cero invocaciones duplicadas, diez lecturas de agente sin modelo y recuperación de pool. TestServer+PostgreSQL real no son carga pública, DOM o capacidad de Qwen. Declarar capacidad y SLO antes de ofrecerlos. |
| O04 | [ ] Despliegue y rollback del artefacto identificado comprobados; las migraciones tienen una recuperación compatible con el backup y los datos reales. | Piloto desplegado, pero rollback no ejecutado. No revertir ejecutables ignorando el estado del esquema. |
| B03 | [ ] Fuente revisada/snapshot privado recuperable, build, dependencias, artefactos y pruebas del candidato se correlacionan con hashes. | Captura de bytes, commits 84faba7/fe373fc, índice normal intacto, cinco locks sin cambiar versiones y recibos CI con hashes comprobados. Falta correlación final con el artefacto desplegado/recuperable; pinning y un restore no prueban reproducibilidad binaria completa. |
| CI01 | [x] CI remoto ejecuta los gates del candidato y conserva artefactos y fallos. | **29/10, e446aa5 / run40881780250 SUCCESS**: jobs Windows y PostgreSQL aprobados; 2.097 resultados .NET PASS/0 FAIL/21 NotExecuted y 482 frontend PASS. TRX, recibos sintéticos y hashes descargados. CI no ejecuta Qwen. Se conservan los fallos anteriores y los recibos locales 481/482 antes de corregir el bootstrap del arnés. |
| H01 | [ ] Una persona completa y acepta el flujo SaaS definido, incluyendo el dispositivo y ChatGPT, en el candidato publicado. | La aceptación humana de NOVA local 1.0.0 se conserva; no se traslada automáticamente al SaaS. |
| GTM01 | [ ] Alcance ofrecido, límites gratuitos, soporte, privacidad, retención y recuperación se comunican de acuerdo con lo que realmente funciona. | No anunciar venta/escala/disponibilidad que el piloto no ha probado. La venta de prompts no forma parte de este incremento. |
| I01 | [ ] El alojamiento elegido permite el uso comercial y ofrece persistencia y operación acordes con los criterios de aceptación, dentro del presupuesto aprobado. | **Bloqueante, 29/10**: Vercel Hobby activo restringe uso comercial; Render Free no recomienda producción y su PostgreSQL caduca. La ruta gratuita alternativa no está provisionada/validada y no hay presupuesto de pago autorizado. |

### Base del piloto conservada — observación del 27/10/2026

La elección de Vercel no migra ni renueva la base Render ya creada. Durante el alta
se registró una caducidad de **30 días**; no usarla como almacenamiento comercial
permanente ni prometer continuidad. El servicio web Render se publicó después, el
28/10, sin que eso amplíe la retención de esta base.

La base creada para este piloto es `quickprompt-nova-db`
(`dpg-dbg9ipt9el5s73ca86gg-a`), PostgreSQL 18 Free, Ohio. La captura
`market-render-database-available.png` muestra **Available** y
`market-render-database-plan.png` muestra el plan de **$0/mes** y su caducidad de
30 días. La fecha de vencimiento registrada durante el alta es el
**26 de noviembre de 2026**. Estos identificadores no son credenciales. El despliegue
híbrido y sus URLs se comprobaron el 28/10; la conexión final de ChatGPT sigue
pendiente. El estado de la base y un health 200 no transforman el dictamen en GO.

El MCP HTTP debe comprobar la autorización del recurso y el servidor de
autorización; una herramienta descubierta, una búsqueda exitosa o un token
presente no conceden acceso a otra biblioteca. [Contrato oficial MCP](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization).

## Fallos de despliegue conservados

- Render construyó correctamente el primer artefacto, pero el comando Docker fue
  interpretado como un nombre de programa y terminó con exit 127 antes del
  entrypoint. Clasificación: **configuración del comando de despliegue**, no fallo
  semántico ni prueba fallida del agente. Recibo `render-first-public-startup-failure.json`;
  el deploy fallido `dep-dbgppge7bikc73b93pt0` permanece junto al Live posterior.
- El build local para Vercel falló en Windows con `spawn cmd.exe ENOENT`.
  `vercel-local-build-failure-classification.json` lo clasifica como entorno de
  construcción; no se cambió la aplicación para hacerlo pasar. El despliegue
  posterior usó build remoto Linux. Ese éxito no elimina el fallo local.
- La regresión pública P2 de cabeceras del primer Vercel se conserva en el recibo
  independiente 01; el retest 03 verifica su reparación, sin reescribir el primero.

**Siguiente gate del piloto:** aprobación del emparejamiento por el usuario,
NOVA/companion y revisión SAVE/CONTENT_ONLY reales A/B, cliente MCP autenticado y
ChatGPT, recuperación/SSE del proxy y correo operativo. Mail permanece apagado.
La disponibilidad del piloto no constituye cierre de estas puertas ni aceptación
humana/comercial.

## Identificar el candidato sin inventar su procedencia

`scripts/New-QuickPromptCandidate.ps1` sólo escribe metadatos privados bajo
`.playwright/candidates/<id>`. No construye, publica, ejecuta inferencia, toca
PostgreSQL ni crea cuentas. Incluye fuentes seguidas por Git y nuevas no ignoradas,
excluye rutas de configuración privada/llaves/datos y rechaza enlaces/junctions.
No sustituye una revisión de secretos incrustados en archivos de código permitidos.
`dependencyInputs` identifica por hash los manifests, lockfiles y proyectos;
los `.deps.json` seleccionados pertenecen al inventario de artefactos, no a una
afirmación de que se ejecutó una restauración limpia de dependencias.

Flujo: **Begin → build y validaciones reales → Complete → revisar dictamen**.

```powershell
./scripts/New-QuickPromptCandidate.ps1 -Mode Begin -CandidateId quickprompt-1.0.0-saas-rc1

# Ejecutar los gates pertinentes de DEVELOPMENT.md después de Begin.
# Conservar sus recibos y el snapshot propio de artefactos que realmente se ejecutó.

./scripts/New-QuickPromptCandidate.ps1 -Mode Complete -CandidateId quickprompt-1.0.0-saas-rc1 `
  -ArtifactDirectory '.playwright/RUTA_REAL_DEL_SNAPSHOT' `
  -EvidenceFiles @('.playwright/RUTA_REAL_DE_LA_SUITE.trx')
```

Los nombres de snapshot y recibo del ejemplo son marcadores; sustituirlos por
artefactos existentes del mismo recorrido. La utilidad nunca sobrescribe una
fase ya capturada. `SOURCE_CHANGED_REVALIDATE` conserva el manifiesto y devuelve
exit code 2: no abrir otro ID para ocultar un cambio o reiniciar un presupuesto.

`SOURCE_STABLE_REVIEW_REQUIRED` significa que coinciden el hash y commit iniciales
y finales. No monitoriza ediciones intermedias ni acredita qué binario utilizó
un proceso. Los contadores TRX se conservan, incluso si hay fallos; su tiempo
interno se clasifica dentro/fuera de la ventana. JSON se referencia por hash sin
copiar cuerpos, secretos o razonamiento. El dictamen siempre empieza como
`NOT_ASSESSED`; la revisión de los recibos decide qué acredita cada prueba.

Un worktree sucio requiere un snapshot privado revisado o una revisión Git
recuperable antes de afirmar reconstrucción reproducible. Un inventario no
reconstruye archivos perdidos y un tag antiguo no identifica cambios posteriores.
No incluir configuración privada en el snapshot o en Git para resolverlo.

La regresión del mecanismo se ejecuta con:

```powershell
./Tests/accounts/candidate-manifest.ps1
```

Usa un repositorio sintético aislado, sin proveedor, base ni red. Comprueba
inventario determinista, exclusión de secretos/configuración, rutas y junctions,
rechazo de DTD, preservación de contadores fallidos, ausencia de sobrescritura y
revalidación al cambiar las fuentes. No ejecuta ni certifica el producto.

## Regla de decisión

**GO piloto** sólo después de cerrar U02, S01/S02, N01/N03, M02/M03, P01–P05 y
la identificación del candidato para ese piloto, con los límites comunicados.
**GO comercial** requiere cerrar todas las puertas de producto y operación que
forman parte de la oferta, además de la aceptación humana del recorrido SaaS.

Registrar el resultado con fecha, candidato, entorno, alcance, recibos y pendientes.
Si una condición falla o falta evidencia, conservar **NO GO** para esa salida.
No convertir una campaña pequeña, un test omitido, una mitigación o una pantalla
de alta preparada en una condición aprobada.





