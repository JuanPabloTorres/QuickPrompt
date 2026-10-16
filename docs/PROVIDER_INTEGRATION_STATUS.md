# Proveedores — GOAL 04

| Nivel | ChatGPT | Claude |
|---|---|---|
| Contratos / origen / TTL / replay / estados | PASS local | PASS local; política bloqueada |
| MV3 real en Edge/Chrome + DOM enrutado | PASS solo fixture: inserción literal, envío UI una vez, cancelación | Acción UI deshabilitada por política; adaptador solo en fixtures, no automatización del servicio |
| Sitio auténtico con sesión autorizada | **BLOCKED_REAL_PROVIDER**: usuario eligió continuar sin sesión en esta fase. HTTP 403 fue observado en Fase 03, no se presenta como una nueva navegación GOAL 04 | **BLOCKED**: se conserva la restricción de automatización del proveedor, sin permiso de Anthropic |
| Copiar y abrir manual | PASS UI normal, clipboard real, target local enrutado | PASS UI normal, clipboard real, target local enrutado |

No hay PASS de inserción/envío en sitios auténticos. No se usan perfiles personales, cookies, autenticación automática, scraping, endpoints privados ni bypass. El adaptador no se ha cambiado a ciegas para perseguir selectores de sitios bloqueados. Las restricciones revisadas se conservan en [PROVIDER_BRIDGE](PROVIDER_BRIDGE.md) y en la guía local de la extensión. La autorización de una dependencia no autoriza usar una cuenta externa.

## Reparación de la experiencia

Preparar/Enviar requieren extensión detectada, texto dentro del límite, consentimiento y ausencia de una operación local concurrente. Enviar exige además su habilitación específica y clic independiente. Claude permanece deshabilitado con razón visible. Detectar extensión no prueba una sesión ni disponibilidad del editor: la UI lo explica. El servidor comprueba de nuevo las condiciones; no basta con cambiar disabled desde la página.

**Copiar y abrir está disponible directamente**, sin tener que autorizar inserción automática: copia localmente, abre únicamente la URL fija y exige que el usuario pegue y decida el envío en el proveedor. La copia comienza durante el clic confiable, antes de perder foco. No se pone texto en URL, ni se inserta o envía contenido con ese fallback. [Evidencia](evidence/goal04/stage05/fallback/fallback.json): ambos destinos, permisos normales sin pregrant de clipboard-write, unicode/comillas/emoji/LF, persistencia después de reiniciar.

## Máquina de estados y auditoría

Se conserva PromptTransferRequest/Result v1 y sus estados de transporte aprobados. WebTransferSession agrega una máquina de aplicación tipada:

`CREATED → PREPARED → TRANSFERRING → INSERTED / FAILED / BLOCKED / UNKNOWN`

PREPARED describe el texto fuente validado; INSERTED exige la verificación literal del editor destino. Solo la acción PrepareAndSend permite continuar `INSERTED → SUBMISSION_REQUESTED → SUBMITTED_UI`. SUBMITTED_UI significa una transición observada del compositor, **sin garantía de entrega o respuesta**. Cancelar antes de un intento confirmado usa CANCELLED; pérdida de confirmación, desconexión o cancelación después de un posible clic usa UNKNOWN. Las transiciones inválidas fallan a estado incierto, sin fabricar éxito ni reintentar automáticamente.

Fallback completo: `CREATED → PREPARED → COPIED_OPENED`. Nunca SENT. Clipboard/popup bloqueados producen resultado manual incierto y una explicación concreta. La detección, hover o abrir QuickPrompt no inicia transferencias.

Se guardan hasta 200 registros locales de metadatos sin texto del prompt, separados de LlmExecution: UUID, IDs locales opcionales de plantilla/revisión, proveedor/acción, estado/fase, código, duración/versión del adaptador y UTC. Son visibles en Historial → Transferencias web, se incluyen en respaldos v2, sobreviven al reinicio y se borran con el historial. El campo opcional webTransfers mantiene lectura de respaldos v1 y v2 anteriores a esta ampliación. La extensión conserva su propio ledger durable sin texto y no recibe esos IDs locales adicionales.

Una consulta local y una transferencia directa no pueden compartir el estado de ejecución: se deshabilitan acciones incompatibles y se valida exclusión en el servidor. Cambiar/navegar cancela la operación abandonada; una confirmación tardía de otro UUID no libera ni sobrescribe una operación nueva. No hay guardado de respuestas externas.

## Pruebas y límites

Evidencia inicial de etapa 5: stage05-tests.log, stage05/edge/extension-runtime.json, worker-failures.json, stage05/chrome/extension-runtime.json. Los casos de API denegada son inyecciones controladas, no revocación manual de permisos desde la UI de Edge. Los adaptadores y carreras de DOM se prueban por separado. La validación humana de una sesión auténtica, lector de pantalla y cambio físico de ventana sigue sin ejecutarse.

Dos expectativas históricas de UI se actualizan por una corrección solicitada explícitamente: ya no se pulsa Preparar sin extensión ni Preparar en Claude bloqueado. Ahora se exige que sigan deshabilitados y que el fallback sea operativo. No se relajan los contratos, criterios de inserción/envío único ni datasets; las fuentes y evidencias anteriores están preservadas.

Cierre vigente: [Edge](evidence/goal04/verified/closure/edge/extension-runtime.json), [Chrome](evidence/goal04/verified/closure/chrome/extension-runtime.json) y [fallback persistente](evidence/goal04/verified/closure/fallback/fallback.json). Las aserciones verifican también las fases INSERTED y SUBMITTED_UI, sin sustituir verificación de entrega real. La decisión expresa del usuario para esta fase fue «Continuar sin sesión; registrar bloqueo externo»; no se intentó leer conversaciones ni insertar en una cuenta auténtica.
