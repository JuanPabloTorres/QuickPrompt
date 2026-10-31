# QuickPrompt Companion para Windows

Paquete portable dependiente del runtime: no necesita el repositorio del desarrollador.
No instala nada, no arranca proveedores, no descarga modelos ni crea servicios de inicio automático.

## Preparar el equipo

1. Instala PowerShell 7.2+, el **ASP.NET Core Runtime 10** (incluye el runtime .NET) y Python 3.11+ desde sus fuentes oficiales.
2. Extrae el ZIP en una carpeta de tu usuario. Conserva `package-manifest.json`: los hashes detectan cambios accidentales, no sustituyen una firma de un distribuidor confiable.
3. Crea un entorno Python propio fuera de este paquete. Instala conscientemente sus dependencias con `python -m pip install -r .\runtime\requirements-inference.lock.txt`. Este comando descarga dependencias; el lanzador nunca lo ejecuta. No copies un venv de otro equipo.
4. Para la instalación actual con Docker Model Runner, copia `companion.dmr.connected.example.json` a `companion.json`: NOVA usa su slot propio y una sesión conectada explícita. Para ejecutar prompts, elige `capability="prompts"` y otro slot; requiere su propia vinculación. Los ejemplos v1/v2 limitados se conservan para instalaciones existentes, sin convertirlas automáticamente a v3. Configura `python` con el ejecutable del nuevo entorno; `dotnet` puede quedar vacío para usar PATH. Las rutas relativas se resuelven respecto al JSON. No añadas tokens ni contraseñas: el esquema los rechaza.
5. Prepara Qwen en **Docker Model Runner**, reutilizando el modelo ya instalado antes de descargar otra copia. Su API para el Companion es `http://127.0.0.1:12434/`. No instales otro servidor nativo Ollama como alternativa automática. El paquete no cambia modelo, puerto, bind o confianza HTTPS. La configuración efectiva debe coincidir con el perfil descrito abajo.

### Perfil explícito de Docker Model Runner

La configuración v1 heredada mantiene el protocolo Ollama y sus campos exactos; añadirle
`provider` es un error. Para DMR, copiar `companion.dmr.example.json` a
`companion.json`: v2 exige `provider="docker-model-runner"`, `physicalModel`
fijado por `@sha256:…`, `thinking=false` booleano y endpoint raíz loopback HTTP
en el puerto 12434. La referencia física tiene un máximo de 240 caracteres.
El alias de dominio continúa siendo `qwen3:8b`; no cambiar el perfil, permisos o
alcance de la conexión por el nombre físico. El ejemplo conserva NOVA/lote 12;
para prompts seleccionar conscientemente su capacidad, slot y lote de uno.

DMR debe estar preparado de forma independiente. Este paquete no instala su
backend, importa modelos, los descarga, cambia configuración ni arranca el runner.
No hay fallback a Ollama si faltan pesos, cambia el digest o el servicio está
detenido. Las opciones físicas sólo provienen del JSON o CLI del operador, nunca
de un trabajo recibido.

```powershell
pwsh -File .\Companion.ps1 -Command doctor
# Opt-in: sólo GET de configuración nativa de DMR; no inferencia.
pwsh -File .\Companion.ps1 -Command doctor -CheckProvider
```

El chequeo exige ModelID igual al digest fijado, backend `llama.cpp`, modo
`completion`, contexto 12.288, `reasoning-budget=0` y `--parallel 1`. Está acotado
a tres segundos y 128 KiB, sin redirecciones, proxy o cookies. Su resultado
`DMR_MODEL_CONFIGURED_NOT_INFERENCE_VERIFIED` no acredita pesos cargados, GPU,
inferencias, fidelidad ni vinculación. El worker vuelve a validar la configuración
antes de inferir. `-CheckOllama` se conserva para v1 y se rechaza con DMR.

### Sesión conectada explícita (v3)

Para mantener el componente disponible en primer plano, copiar
`companion.dmr.connected.example.json` a `companion.json`. La versión 3 conserva
el proveedor, pin y `thinking=false` de DMR, añade `sessionMode="connected"` y
**omite** `maximumJobs`: combinarlos se rechaza, no se ignora un presupuesto.
Los formatos v1/v2 siguen siendo limitados y no cambian de modo al actualizar.

En modo conectado, `run` sigue consultando la cola durante la inactividad y después
de completar trabajos. No genera texto ni mantiene el modelo ocupado por estar
esperando. No termina por el lote ni por cinco minutos sin trabajo. Ctrl+C lo
detiene; la revocación, pérdida del turno, error del proveedor/transporte o límite
de tiempo del trabajo también detienen la sesión, sin reiniciar ni repetir el
trabajo. Se conserva la retirada de presencia y el rechazo de respuestas tardías.
Los límites y validaciones de **cada trabajo**, la caducidad de la autorización y
los permisos de la conexión permanecen vigentes. Mantener abierta una sesión no
amplía su autorización.

`status`, `pair` y `disconnect` no reciben un modo de sesión o máximo de trabajos.
Doctor continúa siendo una comprobación sin worker; no convierte una configuración
conectada en una sesión activa. No hay inicio automático de Windows, servicio ni
tarea programada: el usuario debe iniciar conscientemente `run`.

Para salidas con JSON Schema, Core usa una gramática **estructural** nativa y
valida el resultado completo contra el `outputSchema` original en el host.
Los límites de longitud/cantidad permanecen en el contrato final; no se eliminan
para satisfacer al compilador de gramática de DMR. El perfil no cambia mensajes,
permisos o efectos y no hace fallback ni reintenta un HTTP 400. El modo de texto
no necesita esa máscara; `prompted` sólo se usa si el anfitrión lo pide de forma
explícita. Doctor no ejecuta ni certifica este recorrido de inferencia.

```powershell
pwsh -File .\Companion.ps1 -Command doctor
# Opt-in: GET local /api/tags; no generación, descarga ni arranque.
pwsh -File .\Companion.ps1 -Command doctor -CheckOllama
```

`PREREQUISITES_READY` significa requisitos/imports verificados. `providerChecked=false`
significa que Ollama no se consultó. Ver un modelo listado tampoco demuestra inferencia,
vinculación, calidad ni funcionamiento del agente. Los fallos son explícitos y no se reparan
instalando o reiniciando procesos automáticamente. `doctor` no lee credenciales ni llama al servidor QuickPrompt.

## Vincular y trabajar

Elige el servidor HTTPS exacto de tu instalación y un slot distinto por capacidad.
`nova` propone contenido para NOVA; `prompts` ejecuta instrucciones compiladas. Cambiar de
capacidad requiere su propia vinculación/consentimiento: no amplía una autorización anterior.

```powershell
pwsh -File .\Companion.ps1 -Command pair
# Revisa el código en Conexiones locales de TU cuenta y aprueba allí.
pwsh -File .\Companion.ps1 -Command status
pwsh -File .\Companion.ps1 -Command run
pwsh -File .\Companion.ps1 -Command disconnect
```

`status` comprueba autorización, no deja un worker activo. `run` requiere una acción explícita,
comprueba requisitos del proveedor elegido y trabaja en primer plano; Ctrl+C lo detiene. En v1/v2
el ejemplo prompts procesa un trabajo y el ejemplo NOVA hasta doce, con parada tras cinco minutos sin trabajo. Esos formatos admiten de 1 a 50. Lote de doce no significa modelo siempre activo.
La sesión v3 usa el modo conectado descrito arriba; conserva los perfiles y sus capacidades.
Al salir no se reinicia. Una consola abierta o un PID histórico no prueban que siga activo.
Las credenciales se crean sólo al vincular: DPAPI CurrentUser en LocalAppData, nunca en este ZIP
ni JSON. Desconectar/revocar y cancelar conservan los controles del Companion existente.
Una respuesta incierta no permite repetir automáticamente la instrucción.

## Diagnosticar un arranque

Al abrir `run`, la consola muestra la ruta de un registro seguro en
`%LOCALAPPDATA%/QuickPromptNova/companion/startup-diagnostics/`.
Cada intento tiene su propio archivo, agrupado por un hash de la ruta de su
configuración. Registra horas UTC, PID del **launcher**, fase, salida y códigos
de requisitos. No guarda el JSON del perfil, argumentos, claves, prompts ni
mensajes de excepciones. `-StartupEvidenceDirectory` permite elegir otro
directorio del operador; debe quedar fuera del paquete.

El registro se crea antes de cargar las validaciones del paquete. Si esa carga
falla, queda `INVALID_PACKAGE_OR_CONFIGURATION`, salida 2 y ningún runtime
invocado. El módulo mínimo `Companion.Startup.ps1` debe estar presente y poder
cargarse; un fallo anterior a él continúa sin observación.

Para un acceso directo de escritorio usa `pwsh -NoProfile -NoExit -File` seguido
de la ruta absoluta de `Companion.ps1` y sus argumentos. `-NoExit` mantiene
visible la consola tras un error; no reinicia el componente ni cambia el código
guardado en el registro. No es necesario en automatización por terminal.

`PREREQUISITES_BLOCKED` identifica el requisito que falta y no inicia .NET.
`INVOKING_RUNTIME` sólo confirma que se alcanzó la llamada al ejecutable;
no demuestra autorización, conexión ni inferencia. `RUNTIME_EXITED` conserva el
código real de salida; `RUNTIME_EXIT_UNCONFIRMED` conserva una salida sin código.
Si no aparece un registro, esa entrada no fue observada: revisar el acceso y el
host PowerShell sin asumir que el usuario no lo abrió o que existe un bloqueo
de Windows. Un error al escribir estos metadatos no cambia la ejecución.

`doctor` continúa sin iniciar workers ni escribir registros de `run`. El archivo
`last-run.json` del cliente .NET es otra frontera: describe el ciclo del cliente
y tampoco acredita por sí solo que el proceso siga vivo.

## Actualizar o retirar

Para actualizar, detén tú la sesión, extrae un nuevo ZIP en otra carpeta, revisa su manifiesto
y copia sólo tu configuración sin credenciales. Las credenciales siguen ligadas al mismo usuario
Windows/origen/slot/capacidad. No borres las carpetas privadas para simular una actualización.
Para retirar acceso usa `disconnect` o revócalo en la web antes de eliminar la carpeta extraída.
No se creó servicio, tarea programada ni variable global.

## Componentes y límites

El ZIP contiene binarios publicados, fuentes mínimas de Adaptive Core, worker, lock de Python,
locks .NET y avisos. `provenance.json` identifica hashes de fuentes y configuración de compilación;
no contiene datos del usuario. Los manifiestos no aseguran reproducibilidad binaria bit a bit
ni sustituyen procedencia autenticada. Este paquete necesita validación manual de instalación,
emparejamiento y tarea real en un Windows limpio; las pruebas sintéticas no certifican al agente.

Los paquetes nuevos usan manifiesto schema2 con los proveedores admitidos
explícitos; el lector conserva compatibilidad con schema1/Ollama. La versión
`1.0.1-rc.1-dmr.4` identifica exclusivamente un candidato de integración local:
no sustituye el ZIP del RC ni cambia la versión de la web o la certificación comercial.

Requisitos oficiales: [PowerShell](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows),
[.NET](https://dotnet.microsoft.com/download/dotnet/10.0), [Python](https://www.python.org/downloads/windows/),
[Ollama](https://docs.ollama.com/).
