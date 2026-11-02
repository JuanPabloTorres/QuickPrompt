# QuickPrompt por MCP

## Aceptación web pendiente — 02/11/2026, 20:57 UTC

Nueva búsqueda sólo de lectura desde ChatGPT web con QuickPrompt seleccionado:
`QA cierre contrato literal`, página 0, límite 5. La UI mostró actividad de búsqueda
y `Worked for 23s`, seguida de **This response couldn’t load**. Expandir la actividad
sólo mostró su resumen; no expuso recibo JSON, destino de biblioteca ni resultado.
Chat conservado: `6ae8f8cb-5a04-83ea-96cb-6597b86acba7`. Sin regeneración ni escrituras.
No se atribuye el fallo de visualización al servidor MCP ni se certifica una llamada
exitosa por el resumen de actividad. La ventana nativa estaba minimizada y el
adaptador no permitió obtener su estado tras intentar abrir Edge; no se obtuvo
el control nativo comparativo. La aceptación de vinculación de las dos cuentas QA,
lectura/compilación/creación, scopes, refresh/expiración/revocación sigue pendiente.

## Seguimiento de los errores de consola — 02/11/2026

Se ejecutó además una sonda local sin QuickPrompt, cuenta ni datos privados.
El runner público emitió `ready` en ambos controles. La primera configuración,
con `allow-same-origin`, no equivalía al host y se conserva sólo como control.
Después se reprodujeron los atributos relevantes observados en el bundle host:
iframe oculto, `referrerpolicy=no-referrer`, sandbox `allow-scripts`, con origen
opaco. Esta segunda configuración emitió `ready` en 130 ms con Playwright y
125 ms con Edge. Console mantuvo el aviso CSP; Network de Playwright registró
200 para HTML y módulo. Esto prueba arranque en esas sondas, no creación del
runner con contenido ni renderizado de la conversación autenticada.

El bundle host público `882456.2209655360.js`, obtenido de una URL observada,
configura un timeout de 10 s para esperar el frame y contiene recuperación
acotada, condicionada por su configuración. Es compatible con el corte previo
de 10 s de Network, pero no demuestra qué evento falló en aquel chat. La captura
preactivada de red perdió eventos iniciales (`truncated`) y después se desconectó
el depurador. Tampoco se obtuvo evidencia visual estable del chat: una captura
posterior de la sonda devolvió `about:blank` sin navegación solicitada. El navegador
alternativo no tenía sesión de ChatGPT; no se copiaron credenciales ni cookies.
Se preservan estos fallos de captura en lugar de presentarlos como evidencia
de una avería de QuickPrompt. Código y recibos de la sonda están en
`renderer-investigation-20261102` bajo las visualizaciones de esta conversación.

Inspección estática posterior del runner público v15: su módulo emite `ready`
con un `MessagePort` y recibe `createRunner` con `compiledDil`. Es un protocolo
de renderizado del navegador, separado del resultado JSON-RPC de MCP. El
bootstrap sólo emite `moduleError` para el script marcado como módulo del
runner; un aviso CSP de otro inline no basta para declarar fallido ese módulo.
No se capturó el evento que falló en el incidente original. El SDK MCP documenta
el cierre del POST después de devolver el resultado; no se identificó un callback
de refresh pendiente de implementar en QuickPrompt. Evidencia pública con hashes:
`renderer-investigation-20261102/public-renderer-contract.json` en las
visualizaciones de esta conversación. Esto no certifica el frontend autenticado.

No se aplicó una reparación de producto: los recursos de los tres errores
registrados pertenecen a `chatgpt.com` o `cdn.platform.openai.com`. Su ubicación
no basta para determinar si existe una causa indirecta en QuickPrompt.

La pestaña reabierta bajo control del navegador mostró de nuevo el error en las
dos respuestas del corte anterior. Network marcó `runner.html` como solicitud
fallida; Response indicó `No resource with given identifier found`. El canal CDP
no pudo habilitar Network porque el control del navegador estaba resolviendo una
respuesta de documento pausada. Esto limita la prueba y puede introducir
interferencia; no demuestra una causa del fallo original del usuario.

Como control se abrió el mismo chat mediante navegación nativa en el mismo Edge,
sin vincular esa segunda pestaña al control de páginas. Mostró las respuestas
guardadas. Una nueva búsqueda de lectura, `QA-CONSOLE-20261102-E61C`, página 0,
límite 1, mostró `items=[]` sin recargar. En su captura Network, dos solicitudes
`list_accessible` devolvieron HTTP 200. El 500 anterior no se reprodujo y no se
recuperó su cuerpo de error; no se presenta como corregido.

El aviso CSP permaneció con la respuesta visible. Una descarga pública de
`https://cdn.platform.openai.com/deployments/dil/v15/runner.html` devolvió 200:
su script inline principal tiene el hash permitido por su meta CSP; en la línea
366 incorpora otro script inline de Cloudflare (`/cdn-cgi/challenge-platform/`),
fuera de ese hash permitido. Coincide con la línea del aviso, pero no se capturó
el cuerpo exacto servido al navegador para comparar el hash bloqueado. La
configuración de ese CDN no pertenece a QuickPrompt. No se relajó CSP ni se
ocultaron mensajes. El 404 de `chatgpt.com/favicon.ico` tampoco se corrige
cambiando el favicon de nuestra aplicación.

**Estado:** diagnóstico parcial; sin reparación confirmada del fallo intermitente.
La comparación de pestañas obliga a distinguir el producto de la instrumentación.
Evidencia nueva: `f12-test-20261102/native-control-console.jpg` bajo las
visualizaciones de esta conversación. Las pruebas anteriores conservan su alcance
de visualización momentánea, no de fiabilidad tras reabrir el chat.

## Prueba con consola F12 abierta — 02/11/2026

Se abrió DevTools/Console en Edge mediante control nativo y se observaron dos
búsquedas sintéticas nuevas, con límite 1 y sin escrituras. Ambas mostraron
`items=[]` en su respuesta final **sin refrescar la página**. La segunda conserva
la actividad visible `Searched Approved Prompt Library`. Inicios observados:
16:02:47Z y 16:05:19Z (12:02:47 y 12:05:19 de America/La_Paz).

Mensajes visibles de Console:

- Antes de seleccionar QuickPrompt: bloqueo CSP de un script inline en
  `https://cdn.platform.openai.com/deployments/dil/v15/runner.html`.
- Después de seleccionar el plugin y antes de enviar la primera búsqueda:
  POST `https://chatgpt.com/backend-api/aip/connectors/links/list_accessible`
  respondió HTTP 500.
- Durante la navegación del primer turno: dos mensajes 404 de
  `https://chatgpt.com/favicon.ico`.

En el segundo turno no se observaron errores visibles adicionales; permanecieron
esas entradas. Que los errores CSP/500 coexistieran con una respuesta correcta
no demuestra su causa ni permite atribuirlos al MCP de QuickPrompt. No se cambió
CSP, seguridad, conexión, código o despliegue; no se ejecutó JavaScript en Console.

**Resultado de este corte:** dos respuestas visibles y cero refresh; el fallo
original no se reprodujo. No es una reparación ni prueba de fiabilidad permanente.
Se conserva el historial de intentos fallidos anteriores. Captura y observaciones:
`f12-test-20261102/first-result-console.jpg` y `observations.json`, bajo las
visualizaciones de esta conversación. La inspección posterior se detuvo ante
interacción del usuario detectada en la ventana.

## Auditoría del payload y del estándar esperado — 02/11/2026

Se reejecutó el auditor existente sobre los cuerpos HTTP sintéticos retenidos:
9 casos y 12 respuestas para `2025-06-18`; 9 casos y 11 respuestas para
`2026-07-28`. Pasaron el subconjunto exportado de outputSchema, paridad
SDK/HTTP/catálogo y cinco controles negativos por modo. Además se contrastaron
los campos obligatorios con los JSON Schema públicos de ambas revisiones.
Son comprobaciones nuevas de capturas anteriores, no una nueva corrida del
servidor ni una captura del frontend de producción. No es validación general
de todo JSON Schema/MCP ni de todas las cabeceras HTTP.

Captura real de QA para una búsqueda vacía, mostrada después de decodificar
el evento SSE `message` (ID sintético original):

```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "content": [{ "type": "text", "text": "{\"items\":[]}" }],
    "structuredContent": { "items": [] }
  }
}
```

El transporte observado es HTTP 200 `text/event-stream`, `event: message`,
`data: <JSON-RPC>` y un separador vacío final. El contenido textual contiene
`\u0022` en el texto HTTP retenido; al decodificar equivale a comillas y
al parsear una vez produce un objeto, no una cadena doblemente codificada.
El modo `2026-07-28` añade `resultType: "complete"` y metadata pública del SDK;
discovery/catálogo incluyen `ttlMs: 0` y `cacheScope: "private"`.

Los campos requeridos observados están presentes; `items=[]` satisface el schema
de búsqueda. Omitir `isError` significa éxito. `title` del descriptor es opcional
según MCP, con nombre como fallback: su ausencia es una mejora de guía de producto,
no una infracción protocolaria demostrada. El estándar no requiere un marcador
OpenAI `[DONE]` dentro del stream MCP ni un callback adicional de renderizado.
La respuesta JSON-RPC terminal y el cierre del stream pertenecen a este transporte.

Fuentes: [herramientas 2025](https://modelcontextprotocol.io/specification/2025-06-18/server/tools),
[herramientas 2026](https://modelcontextprotocol.io/specification/2026-07-28/server/tools),
[transporte](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) y
[contrato OpenAI](https://developers.openai.com/plugins/reference#tool-results).

El conector público consultado de nuevo devolvió `content` y `structuredContent`
con `items=[]`, sin error. Es un resultado decodificado por el conector, no el
cuerpo HTTP público ni el mensaje que consume la web. El resultado MCP debe
distinguirse de los mensajes de conversación que el servicio ChatGPT entrega
al navegador. No se capturaron el tipo, identidad, contenido ni cierre de esos
mensajes en el turno fallido, ni el catálogo efectivo de ese turno. La captura
CDP siguió indisponible; el control nativo no consiguió operar la ventana
minimizada de forma estable. No se sustituyó esa evidencia por llamadas privadas
a endpoints internos de ChatGPT.

Conclusión: no se encontró un incumplimiento en los cuerpos MCP auditados.
El cumplimiento y la transformación efectivos hasta el frontend de producción
siguen SIN CONFIRMAR; no se certifican a partir del 200, de los tests de QA o
del resultado decodificado. Artefactos de auditoría de esta sesión:
`protocol-audit-20261102/wire-audit.json`, schemas públicos con hashes y ejemplos
SSE bajo el directorio de visualizaciones de la conversación.

## Revisión causal de la propuesta — 02/11/2026

Esta revisión sustituye el orden de experimentos de la primera propuesta de abajo.
El usuario pidió revisar el razonamiento y corregir la propuesta; no se presenta
un cambio de payload como reparación sin un mecanismo comprobado.

**Conclusión corregida:** que el resultado llegue al modelo no demuestra que la
actualización llegue al navegador; que aparezca después de recargar tampoco
demuestra una avería de red. Son fronteras distintas. La evidencia conservada
demuestra una respuesta legible en el historial después del refresh y un fallo
de visualización inicial. No fija el instante en que terminó de generarse o
persistirse ni revela qué recibió el navegador antes de recargar.

| Frontera | Evidencia disponible | Lo que sigue sin comprobarse |
| --- | --- | --- |
| Herramienta MCP → consumidor ChatGPT | Conector directo con `items=[]`; logs publicados de retorno y cierre HTTP | Correlación exacta entre la traza del servidor y el turno web; intercambio efectivo completo |
| Resultado de herramienta → respuesta del modelo | Respuesta textual coherente recuperada posteriormente en el mismo chat | Si ese texto ya existía al mostrar el error inicial |
| Servicio ChatGPT → navegador | Respuesta inicial vacía y después error observado | Texto final y señal de cierre recibidos en la actualización inicial |
| Actualización recibida → estado → DOM → pantalla | La recarga acabó mostrando el texto; control sin herramientas visible | Primera transformación que omitió, rechazó u ocultó la respuesta |

Retirado como primera reparación: cambiar `content` de JSON a narración. La
duplicación con `structuredContent` es válida y el modelo consiguió una respuesta
coherente. El ejemplo narrativo oficial no aporta un mecanismo que explique este
fallo. El formato alternativo queda como experimento condicionado a una
incompatibilidad observada en la transformación o presentación del resultado.
También se retira cambiar las pruebas sólo para acomodar ese experimento: la
paridad actual sigue siendo una regresión válida del adaptador que utiliza el
SDK. Sólo se modificaría si se aprueba y verifica otro contrato de presentación.

No se priorizan títulos, widgets, `resultType`, autenticación o reconexiones por
el solo hecho de diferir de un ejemplo o de no tener captura. Los títulos siguen
siendo una mejora de metadata; no explican por sí solos la pérdida de una respuesta.

Plan corregido:

1. Fijar el caso en el navegador/perfil actual y capturar la actualización inicial
   antes de recargar. Usar datos sintéticos y conservar sólo estado HTTP, eventos
   de finalización y texto final autorizado o su tamaño/hash; excluir credenciales,
   contenido de otras conversaciones y razonamiento interno del modelo.
2. Correlacionar conversación y mensaje de la entrega inicial con su lectura
   posterior guardada. La proximidad temporal y un 200 del backend no sustituyen
   esta identidad. No regenerar la respuesta durante la comparación.
3. Elegir la frontera por evidencia, sin convertir una señal ausente en éxito:

   | Observación contrastada | Área a investigar y condición para corregir |
   | --- | --- |
   | Historial tiene texto, entrega inicial no lo trae o se interrumpe | Entrega en vivo/reconciliación. Identificar en cuál conexión ocurre; el SSE MCP y la actualización ChatGPT–navegador no son el mismo canal |
   | Entrega inicial contiene texto y cierre, pero el DOM no los incorpora | Parseo, asociación del mensaje o actualización del estado. Localizar el evento/bloque que se rechaza; una incompatibilidad específica del bloque de herramienta todavía podría involucrar nuestro adaptador |
   | DOM contiene la respuesta, pero no se ve | Renderizado o visibilidad; observar el elemento y error concretos |
   | Intercambio MCP real incumple schema, identidad o formato negociado | Corregir el productor/adaptador/proxy de QuickPrompt responsable y cubrir esa familia contractual |
   | No se capturó alguna frontera decisiva | Causa sin confirmar; obtener esa señal antes de escoger el cambio |

4. Aplicar una corrección mínima sólo en el propietario demostrado. Si corresponde
   a código del cliente ChatGPT que no controla QuickPrompt, registrar ese límite;
   no sustituirlo por un cambio arbitrario del servidor ni atribuirle el fallo
   sin el contraste anterior.
5. Verificar la respuesta inicial sin refresh, lectura posterior, control sin
   herramientas y casos MCP vacíos/con resultados. Conservar permisos y efectos
   de las herramientas. Los tests de servidor permanecen como controles de su
   frontera y no pasan a certificar el navegador.

Pseudocódigo: fijar caso e identidad; observar entrega inicial, estado y DOM;
leer la misma respuesta guardada sin regenerar; si falta señal, conservar UNKNOWN;
si los valores divergen, localizar la primera transformación contrastada; corregir
su propietario cuando esté autorizado y sea accesible; ejecutar el caso original
y controles; aceptar sólo con visibilidad inicial comprobada.

Limitación de esta revisión: el inventario actual de herramientas sólo ofrece
el navegador integrado, sin Edge conectado ni su pestaña autenticada de ChatGPT.
No se obtuvo una nueva captura del cliente ni se repitió el fallo en este corte.
Se corrigió el diagnóstico/propuesta, sin cambios de producto o despliegue.

## Contraste con la documentación oficial — 02/11/2026

Análisis del fallo de visualización, sin cambios de producto ni despliegue.
Fuentes consultadas ese día: [servidor MCP](https://developers.openai.com/plugins/build/mcp-server),
[resultados y metadata](https://developers.openai.com/plugins/reference#tool-results),
[diagnóstico](https://developers.openai.com/plugins/deploy/troubleshooting),
[conexión y pruebas](https://developers.openai.com/plugins/deploy/connect-chatgpt) y
[UI opcional](https://developers.openai.com/plugins/build/chatgpt-ui).

El contrato no exige UI propia para devolver respuestas útiles. `content` y
`structuredContent` llegan al modelo; los datos estructurados deben ajustarse a
`outputSchema`. Los estados `openai/toolInvocation/*` son etiquetas opcionales,
no señales para refrescar la respuesta. Los callbacks de MCP Events corresponden
a suscripciones y no a finalizar una búsqueda síncrona. Por tanto, su ausencia
no demuestra una omisión causante de este fallo.

Hallazgos de código y fixtures existentes:

- `AccountMcpTools.Search` devuelve una estructura tipada y el SDK genera JSON
  tanto en `content[0].text` como en `structuredContent`. Es una representación
  válida; no ofrece la narración breve que muestran los ejemplos de OpenAI.
  Separar narración y datos es un candidato de compatibilidad, no una reparación
  demostrada. Los dos canales no necesitan ser JSON idéntico según la referencia:
  las pruebas actuales comprueban una decisión del adaptador, no una obligación
  universal del protocolo.
- El catálogo Cloud de `search_prompts` no contiene `title`; la guía pide títulos
  legibles. Añadirlos al registro MCP alinea la metadata, pero no hay evidencia
  de que su ausencia cause la respuesta vacía. No confundir esta mejora con
  causalidad demostrada.
- El fixture más reciente usa `server/discover` y protocolo `2026-07-28`; su
  respuesta HTTP incluye `resultType: "complete"` y metadata pública del SDK.
  El modo `2025-06-18` no incluye esos campos. El
  [SDK 2.3.0](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.3.0/docs/concepts/stateless/stateless.md)
  documenta ambos recorridos. No está capturada la versión efectiva de ChatGPT;
  no se debe quitar un campo normal del protocolo nuevo ni fijar otra versión
  como supuesto arreglo.
- Los cuerpos HTTP sintéticos omiten `isError` cuando no hay error. El `null`
  del objeto C# serializado en `samples` no equivale a un `isError: null` en
  el intercambio HTTP. No se encontró esa supuesta anomalía en el wire.
- La plantilla Vercel reenvía `/mcp` con caché desactivada. No implementa un parser
  JavaScript que vuelva a serializar el resultado. La guía advierte sobre buffering
  de SSE: su ausencia en la ruta desplegada requiere una medición a través del
  proxy; leer la plantilla y un 200 del backend no lo demuestra.

Primera propuesta, no ejecutada y sustituida por la revisión causal anterior:

1. Capturar de forma segura la versión MCP efectiva, la correlación de la llamada
   y el cierre del resultado a través del endpoint público. Conservar identidad,
   biblioteca y scopes; no registrar tokens ni cuerpos privados.
2. Contrastar un candidato que conserve `structuredContent` y `outputSchema`
   actuales, cambiando únicamente `content` a una narración derivada del resultado
   (por ejemplo, «No se encontraron prompts» para `items=[]`). Verificar coherencia
   semántica entre canales, en lugar de exigir igualdad literal. No añadir
   instrucciones de autoridad al resultado.
3. Si persiste, contrastar por separado la entrega SSE a través del proxy:
   cabeceras, framing, versión negociada y terminación de la respuesta. Mantener
   autenticación y permisos. No se ha verificado una opción de respuesta JSON
   alternativa en la API instalada del SDK; no prescribir un flag inventado.
4. Probar metadata legible como cambio independiente. Cuando se publique un
   candidato autorizado, refrescar el catálogo y verificar que ChatGPT usa esa
   revisión antes de abrir un chat nuevo.
5. Aceptar sólo si búsquedas vacías y con coincidencias, lectura y compilación
   muestran su respuesta inicial sin refresh, conservan errores e aislamiento y
   siguen visibles al reabrir. Registrar todos los intentos. Los controles HTTP
   por sí solos no satisfacen esta aceptación.

Pseudocódigo del contraste: fijar baseline y caso; registrar versión/correlación;
seleccionar un único cambio de presentación o transporte; ejecutar caso y
controles en ChatGPT; comprobar visibilidad antes de cualquier recarga; conservar
el resultado y descartar el candidato si no mejora o rompe contratos. Sin
predicción confirmada, conservar el fallo abierto, sin sumar cambios a ciegas.

Una UI propia sería una capacidad adicional: recurso HTML registrado,
`_meta.ui.resourceUri` y recepción de `ui/notifications/tool-result` para pintar
`structuredContent`. Puede ofrecer una vista del resultado independiente de la
narración final; no está demostrado que elimine «This response couldn’t load».
No se recomienda añadirla sólo para ocultar el fallo ni como requisito MCP.

## Diagnóstico de visualización del 02/11/2026

El criterio del usuario es **respuesta visible al terminar, sin refresh**. Verla
después de recargar no cierra el fallo. La búsqueda sintética
`QA-CARGA-20261102-INEXISTENTE-89C1`, límite 1, reprodujo una respuesta vacía
en chat temporal y «This response couldn’t load» en un chat con historial.
Este último acabó mostrando el resultado tras recargar; un turno posterior sin
herramientas mostró `CONTROL SIN HERRAMIENTAS` sin otra recarga. No hubo escrituras
en la biblioteca ni pruebas manuales del usuario en otro perfil.

El conector directo devolvió texto JSON y `structuredContent` iguales a
`{"items":[]}`, sin error. En los logs publicados de las 09:08:53 (America/La_Paz),
la traza `13b9a757c1ee43d6ac24d0b5c9dbb924` une un recibo `search_prompts RETURNED`
con `CHANNELS_MATCH`, campos raíz presentes y un cierre HTTP `POST 200 JSON SSE`
en 365 ms. No se capturó esa traza en el cliente web: su asociación exacta con el
turno de ChatGPT sigue sin comprobarse. Estos recibos no validan por sí solos el
schema completo ni el renderizado.

Pasaron 26 controles locales HTTP/recibos en el checkout MCP `4cb104b` y dos
casos JSON de RC2 con PostgreSQL de QA. Son ejecuciones distintas, no una
certificación conjunta del servicio. La consola del chat no registró errores;
la herramienta no permitió capturar su flujo de red. AI Delivery Lab se abrió
para RC2 sin fuentes de captura. Evidencia de esta sesión:
`.playwright/mcp-load-20261102/observations.json`.

**Sin resolver:** la divergencia observada está entre la finalización de la
llamada y la visualización inicial; el mecanismo causal exacto no está capturado.
No se cambiaron contratos, SDK, permisos ni despliegue por una hipótesis. R4
permanece abierto. El resultado visible anterior no prueba una reparación.

## Verificación del 01/11/2026

El módulo MCP inspeccionado coincide con el backend público `4cb104b`. Siete
controles de integración con PostgreSQL de QA pasan: PKCE, ámbitos, biblioteca,
renovación, revocación y correspondencia entre schema de salida, texto JSON y
contenido estructurado. El SDK oficial 2.3.0 es dueño de esa serialización;
`UseStructuredContent` anuncia el schema y devuelve la estructura tipada, sin
envoltorios añadidos por QuickPrompt. Ver su
[contrato de contenido estructurado](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.3.0/docs/concepts/tools/tools.md).
Estas pruebas no certifican la interfaz de ChatGPT.

En producción, una segunda cuenta abre una biblioteca distinta con cuatro
prompts. El conector de la cuenta principal rechaza un ID activo de aquella
biblioteca. Sus permisos no cambian por la cookie de la cuenta abierta en la web.
La continuidad después de la ventana de cinco minutos del access token funciona,
pero no se capturó directamente la rotación del cliente web.

**Fallo de aceptación conservado:** las respuestas inicialmente visibles en
ChatGPT no cargan al reabrir el chat. La contraprueba en un chat nuevo, sin apps
ni herramientas, presenta el mismo «This response couldn’t load» tras recargar.
Esto demuestra que una llamada MCP no es necesaria para reproducirlo; no
identifica su causa exacta ni acredita la persistencia final. No se alteran
payloads, scopes ni versiones sin otra evidencia y no se envía a soporte.
Recibos: `.playwright/mcp-acceptance-20261101/`. Estado comercial:
[R4 del checklist](GO_TO_MARKET.md), todavía pendiente de aceptación durable.

## Candidato con cuentas y biblioteca autorizada

El host `QuickPrompt.Cloud` incorpora un MCP por cuenta diferente del adaptador
local descrito abajo. El entorno público autorizado ya permitió leer, compilar
y crear con respuesta final visible desde ChatGPT en la biblioteca aprobada.
También se comprobaron revocación y reconexión equivalente. Otro recorrido de
búsqueda conserva un fallo al mostrar la respuesta final; la aceptación completa
con dos cuentas y renovación no se infiere de los recorridos correctos. El estado
y los recibos vigentes pertenecen a [GO_TO_MARKET](GO_TO_MARKET.md), no a los
ejemplos de instalación local de esta página.

### Contraste de JSON y configuración — 30/10/2026

Por instrucción expresa del usuario no se enviará información a soporte de
OpenAI. El diagnóstico se realiza sobre nuestros contratos y configuración.
`McpJsonContractHttpTests` captura exclusivamente respuestas HTTP del `/mcp`
sintético de QA, sin cabeceras, cookies o tokens. Compara `tools/list`, resultados
de `tools/call`, JSON de texto y `structuredContent`, Unicode, campos requeridos,
búsqueda vacía/paginada, `collectionId=null` y errores controlados.

Se ejecutó con protocolo predeterminado del SDK 2.3.0 y con `2025-06-18`: nueve
casos de herramienta por modo, con once y doce respuestas HTTP/SSE respectivamente.
Los envelopes JSON-RPC y sus IDs, los esquemas de HTTP frente a SDK y los dos
canales de resultados coincidieron. El auditor offline rechaza keywords no
soportadas y cubre los ocho keywords/formatos realmente exportados en el fixture;
no es un validador JSON Schema general. Sus controles negativos rechazan campos
requeridos ausentes, revisión con tipo incorrecto, catálogo hipotético incompatible,
keyword desconocido y JSON doblemente codificado.

El último filtro ejecutado obtuvo **3 PASS/0 FAIL/0 omitidas**: los dos modos más
el control existente de cuenta/biblioteca, renovación y revocación. No certifica
el cliente ChatGPT ni todas las versiones de protocolo. La metadata pública de
Vercel respondió JSON 200 en los tres documentos de discovery y pasó
`validatedMetadata` con issuer/recurso/endpoints/scopes del mismo origen.

Local y Cloud tienen distintas respuestas de `search_prompts`: la local incluye
metadata de paginación y resúmenes más amplios; Cloud devuelve `items` con
`id/title/revision`. La diferencia no demuestra que el catálogo de ChatGPT esté
mezclado. Tampoco demuestra un error que la declaración resumida de `get_prompt`
enumere sólo sus cinco campos requeridos: el esquema actual declara metadata
opcional adicional y la respuesta pública contiene esos campos opcionales.
Faltan el esquema efectivo y los cuerpos del intercambio fallido para localizar
esa frontera. No se alteran payloads, permisos o callbacks por esa hipótesis.

Evidencia, esquemas, datos sintéticos y validación reproducible:
`.playwright/goal-closure-20261029/mcp-json-audit-20261030/`.

### Recibos de contrato y POST sin tipo de contenido — corte local del 30/10

La comprobación pública anónima recibió 401 en 453 ms y su `X-QP-MCP-Trace`
coincidió exactamente con el recibo 5320 de Render (112 ms de servidor). La
instrumentación HTTP ya estaba publicada: no se atribuye su existencia a este
cambio. Un refresco del catálogo, sin cambiar endpoint, cuenta o permisos, no
resolvió la respuesta vacía de ChatGPT. La búsqueda vacía falló al mostrarse en
12 s; una lectura de un registro existente falló en 7 s. El control sin apps sí
mostró su texto. Los resultados directos del conector pasan paridad de canales y
el subconjunto del esquema retenido; no son los cuerpos de los intercambios web
fallidos ni prueban su causa. La relectura durable quedó sin confirmar cuando el
control del navegador perdió la referencia y no pudo recuperar el foco.

Se reprodujo además una excepción del SDK 2.3.0 ante POST sin `Content-Type`.
`RejectMissingPostContentType` devuelve 415 **después** de autorización, límites y
conexión activa, antes de leer JSON. No sustituye las otras validaciones del SDK;
la conexión rechazada conserva su 401. El caso corregido, JSON válido y tipos
incorrectos se comprueban con el SDK y HTTP reales en TestServer, sin PostgreSQL.
Esto no demuestra que esa cabecera causara el fallo visual observado.

El cambio local añade el evento 5321 `McpToolContractObserved`: nombre de la
herramienta registrada, resultado/cancelación/fallo, indicador de error, cantidad
de bloques, clase de estructura, paridad JSON y presencia de campos raíz requeridos.
`SchemaSha256` corresponde sólo al esquema público, nunca al contenido privado.
Comparte la traza independiente del evento HTTP 5320, que ahora clasifica tipos de
medio como JSON/SSE/MISSING/OTHER. No registra argumentos, cuerpo, tokens, cookies,
identidad ni IDs enviados por el cliente; tampoco la metadata privada del resultado.

La inspección de texto está acotada a 262144 caracteres y profundidad 32. Superar
el límite o no poder inspeccionar deja señal parcial/sin confirmar. `CHANNELS_MATCH`
y campos raíz presentes no son validación completa de JSON Schema, semántica,
permisos o aceptación. El filtro llama una vez y devuelve el mismo resultado o
excepción; incluso un fallo del logger no cambia la llamada. El SDK añade después
su metadata pública `io.modelcontextprotocol/serverInfo`; las regresiones conservan
esa adición y toda la metadata original, sin eliminarla para comparar.

**63 PASS locales** de la selección MCP/login/autoría, incluyendo privacidad,
rechazo, cancelación y recuperación. Las rutas de cuenta/biblioteca con PostgreSQL
no se repitieron: QA sigue detenido y su arranque anterior fue rechazado. El nuevo
guard y el recibo 5321 **no están publicados**; falta correlacionarlos con el cliente
web real antes de cerrar R4. Evidencia:
`.playwright/goal-closure-20261029/mcp-boundary-followup-20261030/`.

### Configuración de identidad y permisos

Configuración del operador: `QuickPrompt:Delegation:Enabled=true`, claves privadas
existentes y `QuickPrompt:Mcp:RedirectUri` con **un retorno HTTPS exacto** del cliente.
El lanzador acepta `-McpRedirectUri`; sin ese parámetro MCP permanece apagado. No hay
registro dinámico ni cambio automático del retorno ya registrado. Un cliente
incompatible hace fallar el arranque en vez de ampliar permisos silenciosamente.

- Cliente público registrado: `quickprompt-mcp`; código de autorización con PKCE
  **S256**, retorno exacto, `state` y `resource=https://<origen>/mcp`.
- Descubrimiento: `/.well-known/oauth-protected-resource/mcp` y
  `/.well-known/oauth-authorization-server`. Un 401 del MCP incluye
  `WWW-Authenticate` con `resource_metadata`, también para tokens revocados.
  El challenge inicial indica sólo `prompts:read` como alcance mínimo; anunciar la
  capacidad opcional de importar no convierte la primera conexión en escritura.
- Scope base: `prompts:read`; `offline_access` opcional permite renovación.
  Código y refresh token tienen las validaciones de OpenIddict. El permiso vence
  como máximo en 30 días, incluso si la sesión OAuth se renueva.
- Consentimiento: sesión de cuenta, formulario nativo con CSRF y selección explícita
  de una biblioteca propia. Aprobar no concede acceso a otras bibliotecas del mismo
  usuario. Denegar no crea permiso. Un código no se puede canjear dos veces.
- Herramientas: `search_prompts`, `get_prompt`, `compile_prompt`. Dueño, biblioteca
  y conexión vienen del permiso; no son argumentos del modelo. Se reutilizan
  `LibraryAccess`, RLS y `EditorTools.Compile`; no se leen memoria, defaults privados,
  valores guardados ni resultados. No hay inferencia en este transporte.
- Scope adicional optativo: `shares:import`. El consentimiento describe lectura de
  snapshots ya aceptados e importación en la biblioteca elegida. Habilita
  `list_shared_prompts`, `get_shared_prompt`, `import_shared_prompt`. El catálogo y
  las llamadas se filtran por autorización; el permiso de lectura anterior no gana
  estas capacidades al renovar. No se aceptan invitaciones ni se publica por MCP.
- Scope adicional optativo: `prompts:create`. Habilita `create_prompt`, únicamente
  para prompts nuevos en la biblioteca aprobada. Conserva cuerpo, Unicode,
  saltos de línea y marcadores; el título sigue la validación/trim de biblioteca.
  Sus argumentos son exactamente `operationId`, `title` y `templateText`; no
  acepta descripción, categoría o valores. Si se solicitan esos metadatos, el
  cliente debe explicar el límite, sin afirmar que los guardó. El parámetro
  `description` de la herramienta local descrita más abajo no pertenece a este
  contrato por cuenta.
  No permite editar ni borrar. Repetir operación, título y cuerpo devuelve el recibo
  inicial; cambiar contenido o destino con ese ID devuelve conflicto.
- Scope adicional optativo e independiente: `collections:import`. Habilita
  `list_shared_collections`, `get_shared_collection`, `import_shared_collection`
  para snapshots de colecciones que la cuenta ya aceptó. Importa todo el conjunto
  como una colección y prompts nuevos en la biblioteca aprobada. No acepta
  invitaciones, publica, edita ni borra. No exige ni concede `shares:import` o
  `prompts:create`; esos permisos corresponden a prompts individuales y autoría libre.
  El consentimiento distingue las tres capacidades y las conexiones anteriores
  conservan sus scopes, incluso al renovar.
- Importar crea una copia independiente, versión y recibo mediante `SharingAccess`.
  Repetir `operationId` y publicación devuelve el recibo original, sin otra copia;
  reutilizarlo con otro destino falla. Revocar bloquea lectura e importación,
  incluyendo repetir el recibo. La copia ya importada permanece en la biblioteca.
- Revocación: Conexiones y permisos permite revocar la conexión. Cada herramienta
  vuelve a validar token/grant, stamp de cuenta y biblioteca dentro del mismo cerco
  que las invalidaciones. Cerrar todas las sesiones invalida también el MCP.
  Una autorización MCP nunca se ofrece como equipo de ejecución de Qwen.

La migración aditiva `LibraryBoundMcpDelegation` añade biblioteca a la conexión y
una FK compuesta de mismo dueño. La columna es inmutable para el rol de aplicación;
el perfil MCP exige biblioteca y el perfil del companion exige que sea nula.
Aplicar/restaurar sólo en QA sintético hasta verificar el esquema final completo;
el host no migra al arrancar. No retirar esta columna por rollback destructivo.

Evidencia de este incremento: `McpDelegationHttpTests` usa el SDK MCP real, TestServer
y PostgreSQL con rol limitado: dos clientes A/B, segunda biblioteca del propio A,
compilación literal, revisión, PKCE, CSRF, scope/recurso, retorno, renovación,
revocación y cambio de stamp. `mcp-accounts-regression.trx` conserva **66/66** pruebas
del módulo completo. Esto no sustituye navegación real ni conexión real de ChatGPT.
El incremento posterior `McpSharingHttpTests` comprueba A publica v1, edita su
original privado, B acepta por web e importa sólo v1 por MCP; C no recibe acceso.
Se conservan recibos, edición independiente y revocación. `McpSharedImportGrant`
añade el perfil de importación con biblioteca obligatoria e inmutable. El registro
del cliente reconoce sólo la actualización de política anterior conocida, sin
modificar scopes de grants existentes. Un cambio de callback o permisos desconocidos
sigue impidiendo el arranque. No hay migración de datos privados.

`ScopedPromptCaptures` añade recibos privados con FORCE RLS y capacidades inmutables
por conexión: lectura=1, importación=2, creación=4. Los perfiles anteriores se
conservan con sus permisos originales; los grants nuevos usan `mcp-library` y una
combinación explícita de capacidades. Cada token debe ser un subconjunto del grant.
La migración usa una política temporal sólo para el propietario del esquema al
rellenar registros anteriores, retirada antes del commit; no amplía políticas del
rol de aplicación. El guard comprueba que esa política ya no exista.

`McpCollectionCapability` añade el bit colecciones=8 al CHECK de conexiones sin
actualizar conexiones, autorizaciones o tokens. El registro reconoce sólo las
políticas anteriores conocidas con el mismo retorno, cliente público y PKCE.
Un cliente puede solicitar el nuevo scope en un consentimiento nuevo; anunciarlo
en metadatos no lo concede al token. Sin él, las tres herramientas de colección
no figuran en el catálogo y las llamadas forzadas se rechazan. La cuenta y el
destino siguen fuera de los argumentos de herramientas.

`SharingAccess.ImportCollection` reutiliza el snapshot revisado y su recibo
atómico: no copia IDs privados, historial, memoria, defaults, valores o resultados.
Repetir la operación devuelve la versión inicial, sin sobrescribir la copia
editada ni recrear un grupo eliminado. Revocar la publicación deniega también
ese replay; los prompts ya importados permanecen independientes.

`McpCollectionHttpTests` verifica las ocho combinaciones de permisos y el flujo
A/B/C con dos hosts, SDK MCP real y PostgreSQL: invitación pendiente rechazada,
aceptación, destino fijo, recibo estable y revocación de publicación/conexión.
Los catálogos tienen 3 herramientas base, más 3 por importación individual,
1 por creación y 3 por colecciones. Esto no certifica conexión real desde ChatGPT.

`LibraryAccess.Capture` es el propietario de creación idempotente, reutiliza el
escritor de prompts/versiones y guarda un hash SHA-256 del destino/título/cuerpo.
Los IDs de creación tienen un espacio separado de los IDs de importación. El
recibo refiere la primera versión; editar después no cambia lo que devuelve un
reintento. Para leer la revisión actual se usa `get_prompt`.

La misma operación está en POST
`/api/accounts/v1/libraries/{libraryId}/captures`, protegida por cookie y CSRF, con
`{operationId,title,templateText}`. El formulario de edición existente conserva
su endpoint anterior; adoptar el recibo en esa UI queda pendiente. Los errores
400/404/409 conservan los datos del cliente y no autorizan otro intento con ID nuevo.
No se admiten descripción, defaults ni historia en este contrato de creación.

Los grants personales comprobados no autorizan publicación o invitación por MCP
ni heredan scopes de las capacidades optativas. La conexión pública y sus
aceptaciones observadas se registran en [el roadmap único](GO_TO_MARKET.md);
[el expediente SaaS](SAAS_DELIVERY.md) conserva el diseño de entrega.

La comparación literal del emisor también se comprueba después de detectar y
corregir una diferencia de barra final; `mcp-issuer-verified.trx` revalida los cinco
casos MCP en el candidato posterior. Referencias del contrato:
[autorización MCP](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization)
y [validación de metadata OAuth](https://www.rfc-editor.org/rfc/rfc8414.html#section-3.3).

## Adaptador local conservado

El adaptador C# permite que un cliente MCP consulte y guarde prompts en la API local existente. Usa el SDK oficial `ModelContextProtocol.AspNetCore` 2.3.0 sobre .NET 10. No ejecuta el contenido ni llama a un modelo: buscar, leer y guardar funcionan sin Ollama. La IA del cliente decide cuándo proponer una llamada; el servidor valida los datos y conserva las reglas de QuickPrompt.

## Arquitectura y alcance

```mermaid
flowchart LR
  C[Cliente MCP] --> M[QuickPrompt.Mcp]
  M --> A[API local de QuickPrompt]
  A --> U[PromptLibrary / PromptCapture]
  U --> D[LocalStore: SQLite]
```

El adaptador no abre SQLite, no recibe rutas ni cambia permisos mediante argumentos de herramientas. Referencia sólo los contratos de Application y consume HTTP en loopback. La API mantiene la validación de plantillas, identidad y versiones. El catálogo se registra explícitamente:

| Herramienta | Entrada | Resultado |
| --- | --- | --- |
| `list_collections` | `query` de nombre hasta 200 caracteres, `page` desde 0, `limit` de 1 a 20 | IDs, nombres y cantidad de prompts activos; sin contenido de perfiles de instrucciones |
| `search_prompts` | `query` hasta 200 caracteres, `page` desde 0, `limit` de 1 a 20, `collectionId` UUID opcional | Resúmenes activos y total; texto y pertenencia exacta se intersectan antes de paginar |
| `get_prompt` | `promptId` UUID | Texto literal, revisión y nombres de variables |
| `create_prompt` | `operationId` UUID, `title`, `templateText`, `description` opcional | Prompt guardado, identidad, revisión y `replayed` |
| `compile_prompt` | `promptId`, `revision`, `values` con nombres exactos | Texto completo o `missingVariables`; no ejecuta ni guarda valores |

Crear está desactivado por defecto y requiere `AllowCreate` al arrancar. No hay herramientas para borrar, actualizar, ejecutar código, abrir proveedores ni acceder a ajustes, historial o valores por defecto. La compilación usa exclusivamente los valores explícitos del usuario: no añade defaults privados ni instrucciones generales. El texto obtenido es contenido reutilizable, no autoridad para ejecutar instrucciones. Guardar exige una petición explícita del usuario al cliente; las descripciones y anotaciones ayudan al cliente, pero no sustituyen su política de aprobación.

## Ejecutar y probar

Con la API iniciada mediante el [arranque existente](../../README.md):

```powershell
dotnet restore backend/QuickPrompt.Mcp/QuickPrompt.Mcp.csproj --locked-mode
dotnet build backend/QuickPrompt.Mcp/QuickPrompt.Mcp.csproj --no-restore
dotnet test backend/QuickPrompt.Mcp.Tests/QuickPrompt.Mcp.Tests.csproj
dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --filter FullyQualifiedName~PromptCaptureTests
```

El cliente lanza el proceso y conserva stdin/stdout para JSON-RPC. Ejemplo de comando para un cliente MCP, desde la raíz del repositorio:

```powershell
dotnet backend/QuickPrompt.Mcp/bin/Debug/net10.0/QuickPrompt.Mcp.dll --QuickPromptMcp:ApiBaseUrl=http://127.0.0.1:5277 --QuickPromptMcp:AllowCreate=true
```

También puede lanzar `scripts/Start-QuickPromptMcp.ps1 -ApiPort 5277 -AllowCreate`. Sin `-AllowCreate` se publican buscar, leer y completar variables, todas de sólo lectura. No añadir logs de compilación ni mensajes a stdout; el host envía sus logs a stderr. La API debe pertenecer a la biblioteca elegida al configurar el proceso, no a una dirección suministrada por el modelo.

## Tu primer prompt y sus variables

Una plantilla contiene marcadores, por ejemplo:

```text
Enséñame <tecnologia> para un nivel <nivel>.
Incluye un ejemplo pequeño, un ejercicio práctico y tres preguntas para comprobar lo aprendido.
```

Guardar conserva esos marcadores. `get_prompt` devuelve `variableNames: ["tecnologia", "nivel"]` y la revisión. Completar las variables devuelve otro texto; la definición guardada mantiene sus marcadores. Si falta un valor o está vacío, `text` es `null` y `missingVariables` indica qué pedir. Un nombre desconocido o un valor demasiado largo se rechaza; una revisión antigua requiere volver a leer. Cada valor admite hasta 8.000 caracteres, con un máximo de 60 valores. Un marcador dentro de un valor se conserva literalmente y no se vuelve a interpretar.

Cuando el plugin esté conectado en ChatGPT, puedes escribir:

1. «Usa QuickPrompt y guarda un prompt nuevo llamado “Mi tutor”. El contenido exacto es: Enséñame `<tecnologia>` para un nivel `<nivel>`. Incluye un ejemplo y un ejercicio». Revisa el guardado cuando ChatGPT lo solicite.
2. «Busca “Mi tutor” en QuickPrompt y muéstrame sus variables». Si hay varios resultados, elige el que corresponda.
3. «Completa ese prompt con tecnologia = MCP y nivel = principiante. Muéstrame el texto sin ejecutarlo». El cliente usa el ID y revisión recuperados, no necesita que los escribas.
4. «Aplica en esta conversación el texto que acabas de preparar». La ejecución de esa tarea corresponde a ChatGPT; el MCP sólo recupera, guarda y compila contenido.

Hasta completar la conexión de cuenta, puedes aprender con el [cliente de ejemplo C#](../../examples/QuickPrompt.McpClient/Program.cs), que usa el SDK MCP oficial y no requiere una clave de IA. Desde la raíz:

```powershell
dotnet run --project examples/QuickPrompt.McpClient -- 5277 tools
```

Para guardar, crea un archivo local `crear.json` con este formato. Genera un UUID para cada **nuevo guardado solicitado**; conserva archivo y UUID para reintentar el mismo guardado:

```json
{
  "operationId": "f8e7c73a-1c91-4ff9-8d26-4eaef7798ecd",
  "title": "Mi tutor",
  "templateText": "Enséñame <tecnologia> para un nivel <nivel>.",
  "description": "Mi primer prompt reutilizable"
}
```

```powershell
dotnet run --project examples/QuickPrompt.McpClient -- 5277 create_prompt crear.json
```

Para buscar, `buscar.json` contiene `{"query":"Mi tutor","page":0,"limit":5}`:

```powershell
dotnet run --project examples/QuickPrompt.McpClient -- 5277 search_prompts buscar.json
```

Para leer, `leer.json` contiene el ID real obtenido: `{"promptId":"UUID_DEL_RESULTADO"}`. Para completar variables, `variables.json` contiene ese mismo ID y su revisión real:

```json
{
  "promptId": "UUID_DEL_RESULTADO",
  "revision": 1,
  "values": { "tecnologia": "MCP", "nivel": "principiante" }
}
```

```powershell
dotnet run --project examples/QuickPrompt.McpClient -- 5277 get_prompt leer.json
dotnet run --project examples/QuickPrompt.McpClient -- 5277 compile_prompt variables.json
```

Sustituye 5277 por el puerto explícito de tu biblioteca de prueba. `tools` muestra el catálogo de lectura; `create_prompt` habilita creación sólo en el proceso de esa llamada. Estos comandos recorren cliente MCP → servidor MCP → API → aplicación; no son atajos HTTP. No hay reintentos automáticos. Los archivos de argumentos pueden contener tus instrucciones y valores: consérvalos fuera de Git si son privados.

`POST /api/v1/prompts/{id}/render` es la operación de aplicación usada por `compile_prompt`. Devuelve `promptId`, `templateRevision`, `text` y `missingVariables`; reutiliza `EditorTools.Compile`. La compilación habitual de la interfaz conserva su comportamiento de composición y ajustes.

## Buscar por colección

Pide «Busca mis prompts de la colección Formación» o «Dentro de Formación, busca tutor». El cliente debe llamar primero a `list_collections` con `{"query":"Formación"}` y resolver la colección por su ID. Si hay varias candidatas, debe pedir que elijas; no tomar la primera por semejanza. Después llama a `search_prompts` con `{"collectionId":"UUID_REAL","query":"tutor","page":0,"limit":10}`. Con `query` vacío lista sus prompts activos.

`collectionId` se traduce a `groupId` de la API existente. Un prompt de varias colecciones puede aparecer en cada una. Una colección vacía, un ID inexistente o una consulta sin coincidencias devuelven cero resultados, sin ampliar la búsqueda a toda la biblioteca. Los UUID vacíos o mal formados se rechazan. Omitir el filtro conserva la búsqueda global. Los nombres también forman parte del texto del catálogo, pero buscar sólo el nombre no equivale a filtrar su pertenencia.

`list_collections` busca términos literales en los nombres, ignorando mayúsculas y acentos con el normalizador existente. Proyecta y pagina el catálogo acotado de grupos (máximo 500); devuelve `items`, `total`, `page` y `limit`. No expone perfiles, sus textos ni sus contadores. Los límites de consulta se alinean con los 200 caracteres que admite el catálogo de la API; el antiguo anuncio de 500 no era aceptado por ese catálogo.

El cliente de ejemplo admite `list_collections` con un archivo JSON de argumentos, igual que `search_prompts`. Tras actualizar el binario de un túnel ya conectado, reinicia su runtime y actualiza las herramientas del plugin en ChatGPT para que descubra el nuevo catálogo. Compilar el código no actualiza por sí solo una copia publicada o un proceso activo.

## Captura y reintentos

`POST /api/v1/prompts/capture` recibe `{ operationId, prompt }`. Es una operación aditiva de la API local, con la misma cabecera de cliente y protección de origen existentes. Esta cabecera constante es una protección de la forma de la petición, no una credencial remota.

El servidor guarda prompt, primera versión y recibo juntos en una transacción. El recibo conserva un hash del contenido y la identidad/revisión, sin duplicar texto ni defaults. Con el mismo UUID y contenido, un reintento devuelve el registro existente. Reutilizar el UUID con otro contenido produce conflicto. Si el registro se editó o borró, el reintento tampoco lo sobrescribe ni lo resucita.

Si el cliente recibe `CAPTURE_UNCONFIRMED`, debe reutilizar **el mismo UUID y contenido** para comprobar la operación; no crear otra clave automáticamente. Un error de conexión después de guardar puede significar que sólo faltó la respuesta. Una captura exitosa confirma almacenamiento, no ejecución ni calidad semántica. No incrementa usos ni crea una respuesta IA.

La garantía corresponde a la misma biblioteca SQLite. Su copia completa conserva recibos; una exportación/importación portátil de contenidos no transfiere estas claves a otra biblioteca. Cambiar de biblioteca requiere decidir una nueva operación explícitamente.

## Transporte y acceso

La opción recomendada para una conexión privada de un propietario es **stdio mediante Secure MCP Tunnel**: el cliente oficial del túnel inicia este proceso sin exponer un puerto MCP público. La cuenta, el túnel y el equipo deben restringir quién accede a la biblioteca. No se concede acceso sólo por compilar el servidor.

Existe también HTTP local en `127.0.0.1:5278/mcp`, seleccionando `QuickPromptMcp:Transport=http`. Exige una credencial bearer de 32–256 caracteres ASCII sin espacios en la variable de entorno `QuickPromptMcp__HttpToken`. No pasar secretos por argumentos ni guardarlos en el repositorio. Rechaza host remoto, Origin y peticiones sin credencial. `/health` es local y no requiere bearer. Este modo sirve a clientes locales compatibles; no implementa OAuth para un servicio público ni permite configurar una URL remota de QuickPrompt.

La portada de QuickPrompt permite una navegación GET al documento raíz, incluso cuando procede de un enlace externo. La excepción no permite leer la API desde otro sitio, cargarla en un frame ni escribir. Los rechazos devuelven `X-QuickPrompt-Rejection` con un motivo fijo y lo registran sin URL, consultas, instrucciones ni credenciales: `LOOPBACK_REQUIRED`, `HOST_MISMATCH`, `ORIGIN_MISMATCH`, `CROSS_SITE_REQUEST` o `CLIENT_MARKER_REQUIRED`.

Un bloqueo del control de navegador por no poder identificar su URL pertenece a esa herramienta, no al MCP: usar sus controles de pestaña con URL conocida y conservar la política. Una solicitud de reautenticación de OpenAI Platform se resuelve reconectando esa app, no cambiando CORS, la biblioteca o el token MCP.

## Conectar ChatGPT

La conexión se prepara con un túnel propio para QuickPrompt; no reutilizar túneles de navegador u otros proyectos. El cliente oficial necesita un `tunnel_id` y una credencial de control del túnel con los permisos necesarios. La configuración de la credencial se completa mediante el flujo seguro de cuenta, nunca pegándola en el chat. Crear el recurso requiere permisos diferentes de usar uno existente.

Pasos de aceptación:

1. Verificar el cliente oficial y configurar un perfil stdio para este servidor y su biblioteca.
2. Ejecutar su diagnóstico y arrancar el túnel con credenciales autorizadas.
3. En ChatGPT, crear el plugin MCP de QuickPrompt con su Tunnel ID y revisar el alcance de lectura/creación antes de concederlo.
4. Desde ChatGPT, buscar, leer y solicitar guardar un prompt sintético. Comparar su ID y contenido con la biblioteca de QuickPrompt; repetir el UUID para comprobar ausencia de duplicados.

Un test MCP local no prueba que la cuenta ChatGPT ya esté conectada. No declarar conexión hasta completar ese último recorrido. Una instalación pública o para varios usuarios requiere diseñar OAuth, autorización y aislamiento por usuario antes de ampliar el acceso.

Fuentes: [Secure MCP Tunnels](https://developers.openai.com/api/docs/guides/secure-mcp-tunnels), [servidores MCP personalizados](https://developers.openai.com/api/docs/guides/custom-mcp-server), [SDK oficial C#](https://github.com/modelcontextprotocol/csharp-sdk) y [cliente oficial del túnel](https://github.com/openai/tunnel-client).
