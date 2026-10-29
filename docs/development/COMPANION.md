# Componente local de QuickPrompt

Candidato Windows: OpenIddict Client, .NET 10 y Adaptive Core/PydanticAI con
Ollama/Qwen3:8b. El servidor enlaza trabajos con una cuenta y conexión. El equipo
recibe sólo el trabajo que esa cuenta envió a la conexión aprobada y devuelve
datos, sin ejecutar herramientas ni acceder a archivos del sistema. El componente no es aún una entrega cerrada.

## Distribución Windows sin repositorio

El empaquetador es `scripts/New-QuickPromptCompanionPackage.ps1`. Publica una DLL
framework-dependent en una copia aislada de los cuatro proyectos y sus archivos
enlazados; restaura con locks y la caché local NuGet como única fuente. No actualiza
paquetes ni toca los binarios del Companion en uso. Un paquete ausente causa un
fallo explícito. El lock Companion conserva las versiones ya resueltas. No se afirma
reproducibilidad binaria bit a bit.

```powershell
./scripts/New-QuickPromptCompanionPackage.ps1 `
  -OutputDirectory ./artifacts/QuickPrompt-Companion-Windows `
  -CoreSdkRoot D:/SDK/Adaptive-AI-Core
```

El destino y su ZIP deben ser nuevos. El SDK propio de Adaptive Core es una entrada
explícita: sólo se copian nueve módulos Python, dos esquemas y sus manifiestos,
nunca su venv, datos o configuración. Se incluyen hashes de fuentes/payload,
locks y avisos/licencias presentes, sin asignar licencias nuevas. La evaluación de
distribución pública sigue separada de la prueba de portabilidad interna. Las
bibliotecas Python se instalan por separado bajo control del operador.

En el equipo destino hacen falta PowerShell 7.2+, ASP.NET Core Runtime 10 (y
Microsoft.NETCore.App), Python 3.11+ con las versiones de
`runtime/requirements-inference.lock.txt`, Ollama y `qwen3:8b`. El ZIP no incluye
Python, .NET, modelos, perfiles ni credenciales. No instala ni descarga nada.
No añade servicios/tareas de inicio ni cambia certificados, firewall o bind.

Extraer y copiar `companion.example.json` (NOVA, lote 12) o
`companion.prompts.example.json` (prompts, lote 1) a `companion.json`; elegir origen
HTTPS exacto, slot/capacidad y Python instalado. Se reutilizan los lotes vigentes:
doce trabajos no mantienen el modelo siempre activo y cinco minutos sin trabajo
detienen ambos perfiles. Las rutas relativas se resuelven respecto al JSON;
dotnet puede estar en PATH. Ninguna ruta del desarrollador es necesaria. El
esquema rechaza campos desconocidos, tokens, destinos remotos del modelo y límites
fuera de 1..50. NOVA puede necesitar varias llamadas para un recorrido: no sustituir
su configuración por un lote de uno suponiendo que una llamada equivale a una tarea.

```powershell
pwsh -File ./Companion.ps1 -Command doctor
pwsh -File ./Companion.ps1 -Command doctor -CheckOllama
pwsh -File ./Companion.ps1 -Command pair
pwsh -File ./Companion.ps1 -Command status
pwsh -File ./Companion.ps1 -Command run
pwsh -File ./Companion.ps1 -Command disconnect
```

El doctor por defecto es offline: valida hashes/configuración, ambos frameworks,
versiones Python e imports reales del SDK. No lee credenciales ni llama al servidor.
`-CheckOllama` hace sólo GET loopback `/api/tags`, sin proxy/redirect, con tres
segundos y 128 KiB máximos. Modelo listado no demuestra inferencia ni autorización;
el resultado conserva `inferenceVerified=false`. `run` exige acción explícita y
comprueba esos requisitos antes de invocar el mismo CLI. El launcher de desarrollo
también delega con `-PackageDirectory` sin mezclar sus parámetros con el JSON portable.

`Tests/companion/package-contract.ps1` verifica configuración y runtimes;
`Tests/companion/portable-export.ps1` extrae el ZIP fuera del repositorio, comprueba
exclusiones/hashes/tamper y ejecuta imports/doctor con runtimes existentes. Esa
reubicación real no equivale a instalar en Windows limpio ni ejecutar al agente:
vinculación, cancelación, revocación y tarea real del paquete requieren aceptación
manual separada. El proceso no reintenta un inicio previamente bloqueado.

| Operación | Cambios previstos | Datos que conserva |
| --- | --- | --- |
| Instalar | Extraer en carpeta nueva y crear `companion.json`; requisitos instalados explícitamente por el operador | No importa perfiles, bibliotecas o credenciales del desarrollador |
| Actualizar | Detener la sesión manualmente, extraer otro ZIP y copiar sólo el JSON revisado | Mantiene el almacenamiento DPAPI existente del mismo usuario/origen/slot/capacidad; no sustituye binarios activos |
| Retirar | Desconectar/revocar explícitamente la conexión; eliminar la carpeta de aplicación sólo cuando el usuario decida hacerlo | No borra otros perfiles, bibliotecas ni datos de LocalAppData; no creó servicios, tareas ni variables globales |

Las pruebas automatizadas no ejecutan instalación de requisitos, emparejamiento,
desconexión real ni eliminación de perfiles. El cierre de esos pasos requiere una
sesión autorizada en Windows limpio con el artefacto final identificado.

La entrega usa publicación DLL/framework-dependent de
[.NET](https://learn.microsoft.com/dotnet/core/deploying/),
[`dotnet publish`](https://learn.microsoft.com/dotnet/core/tools/dotnet-publish) y
[`restore --locked-mode`](https://learn.microsoft.com/dotnet/core/tools/dotnet-restore),
consultados el 2026-10-29. Reutiliza Companion y Adaptive Core; no añade otro runtime
agente, instalador ni autoactualizador.

## Vincular y consultar

La señal autenticada reciente de actividad y sus límites se documentan en
[Presencia del Companion](COMPANION_PRESENCE.md); no sustituye autorización ni prueba de inferencia.

La web caduca también la etiqueta positiva en reposo: usa el vencimiento recibido
y un máximo local de veinte segundos medido con reloj monotónico. Ese temporizador
no hace HTTP, no revoca consentimiento ni desmonta un run activo. Sólo una consulta
explícita renueva el snapshot mostrado; una respuesta anterior no puede sobrescribir
una consulta posterior. Tras reconectar la página se recuperan recibos, sin heredar
la capacidad DOM de una pestaña anterior ni repetir el inicio de una operación.

Usar un servidor de cuentas HTTPS configurado y migrado. El piloto autorizado usa
`https://project-sxf1z.vercel.app/`; los ejemplos de abajo usan el QA sintético
local en 5301. No sustituir el origen de una credencial existente ni trasladar
bibliotecas privadas al entorno de pruebas.

```powershell
./scripts/Start-QuickPromptCompanion.ps1 -Command pair -Server https://127.0.0.1:5301 -Slot mi-equipo
./scripts/Start-QuickPromptCompanion.ps1 -Command status -Server https://127.0.0.1:5301 -Slot mi-equipo
```

El primer comando muestra un código corto. Entrar en QuickPrompt con la cuenta
correcta, abrir Conexiones locales, revisar el código, equipo y permisos y aprobar
o rechazar. No copiar códigos/tokens a chats o informes. Status verifica autorización;
no ejecuta ni certifica el modelo. Un perfil sólo admite un proceso simultáneo.

DPAPI CurrentUser cifra la renovación bajo LocalAppData/QuickPromptNova/companion,
separada por origen/perfil. Las credenciales no se guardan en el proyecto ni en
almacenamiento del navegador. Cambiar la cuenta requiere desconectar/vincular
explícitamente o usar otro perfil local; no se hereda la cuenta de otra pestaña.

## Ejecutar

Ollama debe estar funcionando, no sólo tener pesos descargados. El CLI oficial
`ollama serve` mantiene su API local abierta. El nombre permitido actual es
`qwen3:8b`. No se descargan modelos ni se cambia de proveedor automáticamente.

Para run, pasar `-CorePython` con el Python **existente** que tiene las dependencias
de Adaptive Core. `-CoreSdkRoot` permite seleccionar su carpeta existente; por
defecto se busca el repositorio hermano Adaptive-AI-Core. `-CoreWorker` selecciona
el launcher local existente; el predeterminado es agents/nova-core/nova_worker.py.
Estas rutas son configuración del operador local, nunca argumentos de un prompt.

En QuickPrompt: abrir una biblioteca → Usar prompt → completar valores → elegir
el equipo → Ejecutar en mi equipo. Mantener el componente run abierto. Por defecto
procesa un trabajo; `-MaximumJobs` admite hasta 50 por lote y termina tras cinco
minutos sin trabajo. Ctrl+C detiene el componente. Cancelar o revocar desde la web
impide guardar resultados tardíos. El historial conserva instrucción y respuesta
por separado y no vuelve a ejecutar al leer/copiar.

### Una consola abierta no demuestra que el worker siga activo

Los accesos que conservan PowerShell mediante `-NoExit` pueden dejar una ventana
abierta después de que el componente termine. Al entrar en el bucle de trabajo,
después de comprobar la autorización, el componente muestra «Componente activo.
Esperando trabajos de QuickPrompt». Al alcanzar el límite de trabajos también
anuncia su parada; la salida por inactividad conserva su mensaje existente.
El lanzador compartido rotula la consola `DETENIDO` y escribe su estado al salir
del proceso, incluso ante fallo. El título es una ayuda visual opcional del host;
no sustituye comprobar que el proceso sigue activo.

`status` comprueba únicamente autorización y puede renovar la credencial existente
en su almacenamiento privado. No deja un worker ejecutándose. Si confirma
autorización vigente pero no hay un proceso `run`, hay que iniciar una nueva
sesión de trabajo, sin volver a vincular ni aumentar sus límites por ese motivo.
No repetir automáticamente una inferencia ni un inicio rechazado por la política
del entorno para intentar obtener la señal de actividad.

### Registro local de salida

En cada `run`, después de adquirir el bloqueo exclusivo del perfil, se sustituye
su `last-run.json` dentro del mismo directorio privado de LocalAppData. La
credencial permanece separada en `session.dpapi`, cifrada con DPAPI CurrentUser.
El JSON sólo admite fase (conectar, recoger, generar, enviar), PID, tiempos,
perfil, hash del DLL leído al inicio, número de trabajos y códigos acotados de
salida/HTTP/OAuth. No recibe prompts, paquetes, respuestas, tokens, rutas o texto
de excepciones. La falta del hash queda explícita; no detiene trabajo autorizado.

`status` muestra el último registro válido como **histórico**, sin modificarlo;
la comprobación de autorización puede renovar su credencial como antes. Un PID
guardado, una fase o `End=null` no demuestran que el proceso siga activo. Si fue
terminado abruptamente, puede faltar el final. Un fallo anterior a adquirir el
perfil/configuración puede no dejar un registro nuevo; conservar uno anterior
no prueba que el nuevo intento llegara a iniciarse. La escritura fallida del
diagnóstico no cancela la tarea ni expone su excepción. Los cierres conservan
el bloqueo hasta terminar el recibo para no sobrescribir una ejecución posterior.

En NOVA, `LOCAL_DISPATCH_TIMEOUT` indica que la última observación del servidor
seguía en cola sin inicio al vencer el plazo total. Puede haber un claim entre
esa lectura y el vencimiento: no certifica que el equipo esté offline. Comprobar
la consola/proceso antes de iniciar otra sesión. `MODEL_TIMEOUT` conserva el
agotamiento del plazo cuando se observó inicio. Ninguno repite la inferencia,
aumenta límites o guarda una propuesta. Esta distinción requiere publicar el
backend corregido; el backend f49 todavía informa MODEL_TIMEOUT en ambos casos.

## Desconectar y límites comprobados

```powershell
./scripts/Start-QuickPromptCompanion.ps1 -Command disconnect -Server https://127.0.0.1:5301 -Slot mi-equipo
```

Se revoca sólo esa conexión y se retira su credencial local. La interfaz de la cuenta
también puede revocarla. Un fallo de transporte no autoriza repetición de la tarea.

La validación operativa y sus límites se registran en
[Go To Market](GO_TO_MARKET.md). Las campañas históricas usaron exclusivamente
runtime/DB/cuentas sintéticas verificadas, con HTTPS normal.
El emparejamiento, renovación, reclamación, fallo por proveedor detenido y
desconexión funcionaron. El 2026-10-27, una ejecución real adicional completó el
perfil `prompts` mediante el ejecutable Windows, DPAPI, Adaptive Core/PydanticAI y
Qwen3:8b en 51,79 segundos. La lectura posterior conserva nombre, tipo y propósito.
La revisión de su respuesta detectó dos observaciones de calidad: enumeración
incorrecta en Datos y una afirmación imprecisa sobre extensión de Factory. Ese
resultado prueba el recorrido de una ejecución, no fiabilidad semántica universal
ni aceptación de navegador. Ver resultados/fallos preservados en
[el registro vigente de entrega](GO_TO_MARKET.md).

## Capacidad separada de propuestas para NOVA

El perfil predeterminado `prompts` conserva `companion:work` y la ejecución de
instrucciones compiladas. `-Capability nova` solicita otro perfil:
`ollama-qwen3-8b-proposals`, scope `companion:proposals`, resource exacto
`<origen>/api/companion/proposals`, protocolo `quickprompt-local-proposal/1`.
Requiere otro consentimiento. La renovación de un permiso anterior no lo amplía.

```powershell
./scripts/Start-QuickPromptCompanion.ps1 -Command pair -Server https://127.0.0.1:5301 -Slot nova-equipo -Capability nova
./scripts/Start-QuickPromptCompanion.ps1 -Command status -Server https://127.0.0.1:5301 -Slot nova-equipo -Capability nova
```

Usar la misma capacidad en `run` y `disconnect`. Un slot existente con la otra
capacidad se rechaza antes de renovar; no se sobrescribe su credencial. El
consentimiento explica que contexto seleccionado, datos y valores pueden llegar
al equipo. El modelo sigue siendo Qwen3:8b, y las rutas Python/SDK/endpoint siguen
siendo configuración local del operador. El paquete no puede cambiarlas.

El arnés también admite `-Capability nova` sin `-AuthorizationOnly`: crea una
biblioteca sintética nueva y usa el motor autenticado con un máximo de ocho
minutos y doce etapas, igual que la campaña real existente. Los recibos y la
aprobación de UI son un fixture HTTP explícito; el cliente Windows, OAuth, DPAPI,
proveedor y persistencia son reales. Antes de aprobar exige las tres variables y
los requisitos literales originales mediante el inspector propietario. Compara
después el guardado con el artefacto revisado, retira sólo su worker ocioso al cerrar
y desconecta/revoca sólo su conexión. Un error conserva estado y cancela el run
propio; no cambia presupuestos del producto ni ejecuta otra tarea para ocultarlo.
Abrir el Lab no convierte este arnés en captura de navegador.

Campaña del 2026-10-27: el perfil NOVA completó cuatro llamadas y guardado aprobado
en dos cuentas con conexiones distintas (110,97 s para A y 105,26 s para B), con
credencial Windows protegida y desconexión. La revisión independiente del artefacto
de B detectó protocolo interno dentro del prompt: el éxito operativo no es cierre
de fidelidad. El expediente mantiene ese fallo y el refactor obligatorio.

`-FixtureActor B -SharedFromA` usa el mismo arnés para que A publique una versión,
B acepte e importe, A revoque y B ejecute únicamente su copia conservada mediante
su propio equipo. Comprueba rechazo previo a aceptación, acceso directo ajeno 404,
revocación de lectura/importación incluso con el recibo anterior y origen intacto.
El caso real completó en 37,36 s; su respuesta también tiene observaciones de
fidelidad documentadas. Los actores y rutas se validan contra fixtures/DB QA; estos
parámetros no son una vía para ejecutar con cuentas privadas.

El servidor admite paquetes a través de `AccountModelProposals.Submit`, un puerto
interno de servidor con actor y biblioteca verificados. No hay un endpoint público
para suministrar inferencia arbitraria. El companion ofrece claim/heartbeat/finish
en `/api/companion/v1/proposals`, bajo su permiso separado. La cola, cuotas,
lease, cancelación y revocación se comparten con las ejecuciones existentes.

El paquete permite texto, JSON Schema o una propuesta de herramienta ofrecida,
con 128000 bytes, 16 mensajes, contexto 8192–12288, salida máxima 4000 tokens y
hasta dos llamadas del SDK. Las propuestas se conservan en una tabla privada
con FORCE RLS; no son versiones de prompts, resultados compilados ni aprobaciones.
El consumidor debe validar el significado, permisos, revisiones y efectos antes
utilizarlas. Se ocultan del listado de preparaciones de prompts porque todavía
no tienen el contrato de resultado de ese flujo. El olvido y la retención de
siete días también borran este contenido.

El transporte está integrado con `AccountNovaCoordinator`, el estado privado y
el driver visible de NOVA. Las campañas históricas conservan sus alcances y fallos;
las pruebas con respuestas sintéticas no acreditan inferencia real. El cierre
actual del recorrido público y de fidelidad está en [Go To Market](GO_TO_MARKET.md).


Los recibos históricos del arnés con `-Capability nova -AuthorizationOnly`
comprueban emparejamiento, renovación protegida y desconexión contra HTTPS.
Ese arnés histórico no forma parte del paquete portable: su resultado NOT_RUN
no acredita inferencia ni ejecución del agente. El caso ampliado HTTP/PG vigente se encuentra en
`CompanionProposalHttpTests`. Los resultados se atribuyen al alcance ejecutado en
el expediente, sin sustituir la campaña real de NOVA/Qwen pendiente.


## Puerto de modelo para el host autenticado

`AccountModelProposals.ForConnection(actor, libraryId, connectionId)` devuelve
`IValidatedAgentModelTransport` enlazado por composición confiable. Su contrato
puro vive en Application (`AgentModelTransport.cs`); el namespace público se
conserva y el módulo Inference reenvía los tipos para consumidores compilados.
Accounts no referencia SDKs, Python, SQLite ni el host local para usarlo.

Cada `Send` valida/admite una operación privada, espera su propio recibo, vuelve a
comprobar el vínculo y devuelve datos al validador consumidor. Las identidades y
el destino no se leen del paquete. El codec conserva JsonSerializerDefaults.Web,
tanto para objetos tipados como para JsonElement. El puerto no guarda prompts ni
aprueba efectos y no repite una inferencia fallida. `Reject` no tiene caché que
invalidar; el consumidor conserva su presupuesto de corrección existente.

La espera total, incluido el tiempo en cola, está limitada por MaximumSeconds de
la política de ejecuciones (180 segundos en este candidato). El consumidor puede
cancelar antes con su plazo de etapa. La cancelación solicita detener sólo ese
trabajo; no afirma que el equipo ya se haya detenido. Un resultado terminal que
haya ganado la transacción permanece como recibo y no se borra ni se repite.
Mantener vivo el scope propietario hasta que termine la llamada. Si la cancelación
no puede registrarse por un fallo de almacenamiento, el fallo se propaga y no se
presenta como una detención confirmada.

Este puerto es una dependencia para la composición de NOVA por cuenta. El ciclo
visible, estado activo y aprobación pertenecen al coordinador y al store de runs,
no a este transporte. No usar el contexto recordado como autoridad de un run activo.

## Autoría compartida para el consumidor por cuenta

QuickPrompt.Inference también contiene los propietarios existentes de generación,
fidelidad, extracción, selección y edición, sin persistencia. Infrastructure
reenvía los tipos públicos para conservar consumidores anteriores. La composición
Cloud/AccountNovaInference fija actor/biblioteca/conexión mediante ForConnection;
OllamaClient.ForTransport recibe sólo ese puerto prestado y no construye HTTP o
un endpoint local. Su disponibilidad debe consultarse en el equipo emparejado.
Los SDK/procesos de inferencia siguen ejecutándose en el dispositivo, no en Cloud.

La campaña RealQwen de AccountNovaRealProviderTests comprueba autoría con tres
variables reales y un error de conexión explícito usando el SDK real sobre el
protocolo HTTP/PG. Su adaptador de dispositivo es sólo QA; no sustituye el caso
Windows/DPAPI ni monta la orquestación y aprobación visible autenticada. La
inspección de fidelidad y las limitaciones están en [Go To Market](GO_TO_MARKET.md).
