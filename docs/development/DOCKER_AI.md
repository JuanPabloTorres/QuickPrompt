# Docker AI para QuickPrompt y otros consumidores

Corte local comprobado: 01/11/2026. No sustituye la aceptación pública R1–R6.

Docker Model Runner está habilitado con CUDA y Qwen3 8B ya instalado en Docker.
No se descargaron modelos ni se instaló otro servidor. Se conservaron los
ajustes existentes porque la API y la inferencia real pasaron.

| Configuración | Valor verificado |
| --- | --- |
| Proveedor | `docker-model-runner` |
| Endpoint del Companion | `http://127.0.0.1:12434/` |
| Pin físico | `ai/qwen3@sha256:977d1540140cd0d5437d2e22ffc6853714bef103bda39754edd0f2d9e2aabb82` |
| Alias de QuickPrompt | `qwen3:8b` |
| Contexto / concurrencia / razonamiento | 12288 / 1 / desactivado |
| Permanencia del modelo en memoria sin uso | 5 minutos |
| Sesión de ambos perfiles instalados | `connected` |

Los archivos instalados `nova.json` y `prompts.json`, en
`%LOCALAPPDATA%/QuickPromptNova/companion/dmr-connected-config-20261029`, ya
coinciden con ese endpoint, proveedor y pin. No se reescribieron ni se leyó una
credencial para comprobarlo. Sesión conectada describe la configuración; no
demuestra que el proceso esté abierto o autorizado ahora.

La API genérica puede reutilizarse desde otras aplicaciones con su propio
cliente. El perfil sin secretos `%LOCALAPPDATA%/DockerAI/profiles/qwen3.json`
usa el alias independiente `shared-qwen3`. El SDK existente acepta esa
sustitución explícita y se probó con otro dominio. Configuración, ejemplo y
límites: [Adaptive Core / DMR](../../../Adaptive-AI-Core/docs/docker-model-runner.md).
Ese enlace corresponde al workspace de desarrollo; el perfil y el SDK no se
incluyen automáticamente en una instalación de otra aplicación.

## Comprobaciones reales

El test `DockerModelRunnerRealProviderTests` pasó con el .NET/Core actual:
readiness 20722 ms (incluye carga inicial), cancelación 27 ms, siguiente
petición 2412 ms, de los cuales 212 ms corresponden al intervalo observado del
proveedor. No certifica que el motor terminara internamente en 27 ms.

Un segundo consumidor, sin importar el dominio QuickPrompt, clasificó un envío
y copió su código correctamente mediante JSON Schema en 3922 ms. Una llamada,
sin reparaciones ni efectos. La comprobación CORS devolvió 403 para un origen
ajeno y la conexión al puerto por la interfaz Wi-Fi propia fue rechazada.
No se cambiaron CORS, firewall, bind, cuotas ni permisos.

Recibos locales: `.playwright/priority-closure-20261101/docker-ai-setup/`:
`real-dmr.trx`, `dmr-cancellation/result.json`, `second-consumer-delivery.json`,
`shared-consumer.json` y `local-network.json`. El arnés inicial de CORS no
contemplaba HTTP 403; se corrigió su manejo de esa respuesta esperada, sin
volver a ejecutar ni seleccionar otra inferencia.

## Operación

Comprobar `docker model status`, `docker model list` y
`docker model configure show <pin>`. Después ejecutar el doctor del paquete con
`-CheckProvider`; éste sólo comprueba prerrequisitos. Inferencia, semántica y
petición pública son pruebas separadas. No deducir una API caída únicamente de
`Get-NetTCPConnection`, que en este equipo no mostró el listener aunque HTTP
y generación funcionaran.

Mantener el modelo y runtime en Docker. No crear una copia Ollama nativa ni
publicar el puerto 12434. La web alojada debe pasar por el Companion emparejado,
con identidad, permisos y revocación propios. CORS no es autenticación.
Otras apps que cambien el perfil global deben coordinarse; este corte no mide
carga concurrente ni garantiza recursos para varias inferencias simultáneas.

Continúan pendientes el arranque autenticado del Companion instalado, la petición
pública correlacionada y el resto de controles del [checklist](GO_TO_MARKET.md).
