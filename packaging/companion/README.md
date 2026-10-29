# QuickPrompt Companion para Windows

Paquete portable dependiente del runtime: no necesita el repositorio del desarrollador.
No instala nada, arranca Ollama, descarga modelos ni crea servicios de inicio automático.

## Preparar el equipo

1. Instala PowerShell 7.2+, el **ASP.NET Core Runtime 10** (incluye el runtime .NET) y Python 3.11+ desde sus fuentes oficiales.
2. Extrae el ZIP en una carpeta de tu usuario. Conserva `package-manifest.json`: los hashes detectan cambios accidentales, no sustituyen una firma de un distribuidor confiable.
3. Crea un entorno Python propio fuera de este paquete. Instala conscientemente sus dependencias con `python -m pip install -r .\runtime\requirements-inference.lock.txt`. Este comando descarga dependencias; el lanzador nunca lo ejecuta. No copies un venv de otro equipo.
4. Copia `companion.example.json` (NOVA, lote de 12) o `companion.prompts.example.json` (ejecutar prompts, lote de 1) a `companion.json`. Son las configuraciones vigentes del producto, con slots separados. Configura `python` con el ejecutable del nuevo entorno; `dotnet` puede quedar vacío para usar PATH. Las rutas relativas se resuelven respecto al JSON. No añadas tokens ni contraseñas: el esquema los rechaza.
5. Instala/configura Ollama y `qwen3:8b` por separado. Mantén su API sólo en `http://127.0.0.1:11434/`. El paquete no cambia modelo, puerto, bind o confianza HTTPS.

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
comprueba requisitos/listado Ollama y trabaja en primer plano; Ctrl+C lo detiene. Por defecto
el ejemplo prompts procesa un trabajo y el ejemplo NOVA hasta doce, con parada tras cinco minutos sin trabajo. El JSON admite de 1 a 50. Lote de doce no significa modelo siempre activo.
Al salir no se reinicia. Una consola abierta o un PID histórico no prueban que siga activo.
Las credenciales se crean sólo al vincular: DPAPI CurrentUser en LocalAppData, nunca en este ZIP
ni JSON. Desconectar/revocar y cancelar conservan los controles del Companion existente.
Una respuesta incierta no permite repetir automáticamente la instrucción.

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

Requisitos oficiales: [PowerShell](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows),
[.NET](https://dotnet.microsoft.com/download/dotnet/10.0), [Python](https://www.python.org/downloads/windows/),
[Ollama](https://docs.ollama.com/).
