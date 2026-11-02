# Matriz de aceptación del MVP

## Correo público comprobado — 02/11/2026, posterior al corte integral

Render `e16e4ee`, despliegue `dep-dbkedkl9fdbs739tt1mg`: email=true y correo
configurado, Google/delegación preservados. Registro por formulario, confirmación
recibida/consumida y acceso por navegador comprobados. Recuperación por formulario,
correo recibido, reset por API pública y login con contraseña nueva comprobados;
dos sesiones revocadas y enlace usado rechazado. 21 controles HTTP públicos PASS.
Confirmación en spam; recuperación en inbox. No se automatizó el submit del
formulario de reset ni la espera de caducidad real; no se certifica entregabilidad
universal. No es aceptación conjunta del candidato integral ni cambia su NO-GO.
Fuentes y recibos en [ACCOUNT_MAIL](development/ACCOUNT_MAIL.md).

## Corte actual — 02/11/2026

**NO-GO.** Candidato local `closure-20261102-execution-review`, web/Companion `1.0.1-rc.2`,
base `bbefd38626e82c4ce389cd3735a657b0a2f929f0` y delta sin publicar.
Identidad final: `.playwright/candidates/closure-20261102-execution-review/candidate.json`.
El roadmap continúa siendo [R1–R6](development/GO_TO_MARKET.md).
La [matriz detallada local](../.playwright/execution-task-frame-20261102/acceptance-matrix.json)
registra para cada control candidato, entorno, comando/recorrido, esperado,
observado, PASS/FAIL/BLOQUEADO/NO EJECUTADO, latencia conocida, hash de evidencia y
siguiente acción. Una latencia null significa que no fue medida, no cero.

Revisión posterior de ejecución: contexto AST **2/6**, corrección de roles
**5/6**; ambos FAIL frente al criterio 6/6. Se conserva la omisión de la foto
en H-E02 y no se recalcula la campaña 14/20. La integración experimental se
retiró; siete preimágenes idénticas y cinco añadidos archivados. Regresión del
producto restaurado: **1792/0/2** backend y **114/0/0** Companion. Las tres sondas
públicas de las 16:51:26 UTC agotaron 15 segundos; disponibilidad sin confirmar.
Evidencia y límites: `execution-task-frame-20261102/RESULT.md` bajo `.playwright/`.

Actualización nativa R2: **6/6 respuestas de protocolo válidas; 0/6 de semántica**
según los seis oráculos congelados. Replay sin inferencia con DLL actuales:
cuatro composiciones, un rechazo del dominio y uno de la política numérica QA.
Doce controles offline PASS. No se integra este candidato ni se recalcula el
14/20 principal. La consulta pública posterior de opciones agotó 30063 ms;
configuración actual sin confirmar. Recibos bajo `closure-r2-native-20261102`.

| Frontera actual | Resultado | Recibo y límite |
| --- | --- | --- |
| Backend Release completo | Última regresión **1792 PASS, 0 FAIL, 2 SKIP**; histórico **1794/0/0** incluidas ambas optativas DMR | `execution-task-frame-20261102/restored-tests/restored-backend.trx` y `.playwright/backend-allpass-20261102/full-suite/backend-all.trx`; la corrida nueva no ejecutó las dos optativas. No califican semántica |
| Web | **532 PASS**, build PASS, chunk 539,09 kB advertido | `closure-20261102/web-tests.log`, `web-build.log`; 209 inputs web sin cambios durante validación |
| Cloud y contenedor | Build sin errores/advertencias; Docker build PASS; **19 controles de claves/entrada PASS** sin red, read-only, UID 1654 | `cloud-final.log`, `container-build.log`, `container-secrets.log`; no es despliegue Render |
| Accounts/PG | **731 PASS / 0 FAIL / 35 SKIP**, total 766, 16 min 25 s; focal separado 5/5 | `accounts-corrected-all/accounts-corrected-all.trx`. Los tres fallos previos se diagnosticaron como selección incorrecta de Crear en el arnés; las 66 aserciones originales no cambian y se añade Crear literal por HTTP con cero trabajos y cero efectos. Los 35 opt-in no son PASS. Intentos anteriores y contaminación conservados |
| MCP/Companion/Recovery | **27/114/45 PASS**, respectivamente | Suites separadas y alcance controlado; no acreditar ChatGPT o worker público por estos números |
| Navegador real local | **6 controles de acceso + 8 de dos cuentas PASS**, sin mocks HTTP | `access-browser-actual/result.json`, `two-account-e2e-run2/result.json`; Identity y PG reales, buzón QA privado. Incluye reinicio, aislamiento, CRUD, versiones, compilación, compartir y revocar; no correo externo |
| Campaña NOVA de veinte | **14/20 FAIL respecto a 18/20**; 20 llamadas, cero retries | `semantic-assessment.json`; adaptar 5/8, buscar 6/6, ejecutar 3/6. Los diez casos rotulados holdout ya eran visibles en el expediente; no son un nuevo holdout ciego |
| Traducción por campo | **0/4**, ocho llamadas, cero retries | `translation-assessment.json`; campaña separada, dos rechazos de marcadores y dos divergencias semánticas/literales |
| Paquete | Dos ZIP idénticos; **68 archivos**, export **15/15**, ciclo portable **9/9**, contratos **58/58** | SHA-256 `727c311faee80f775f69eff471fe78dc6829a3182abcdb8de1fc4dd76f8758f0`; Windows con prerrequisitos existentes, sin SO limpio ni actualización activa autenticada |
| Companion instalado | Arranque/conexión real y Ctrl+C comprobados; `UserStopped`, **0 trabajos** | `installed-run.json`; no hay propuesta pública, aprobación/persistencia ni reconexión con trabajo |
| Público | Corte inicial health/discovery 200; nueva comprobación health timeout 30081 ms, opciones 200/16964 ms; email=false y correo sin configurar | `public-fingerprint.json`, `accounts-diagnosis/public-status-recheck.json`; Render directo posterior 200/244 ms no determina la causa inicial. SHA desplegado no confirmado y assets distintos del local |
| ChatGPT web | Respuesta de búsqueda sintética visible; detalle de llamada sin confirmar tras perder conexión del navegador | Chat «Buscar título exacto» del 02/11. Falta autenticación/dos cuentas, lectura/compilación/creación, scopes, refresh y revocación del candidato final; no se sustituyen por un mock o una llamada Codex |

Los paths sin prefijo de esta tabla están bajo `.playwright/closure-20261102/`.
La evidencia anterior se conserva a continuación como cortes históricos.
La revisión semántica es del agente contra los checks congelados, no aceptación
humana independiente. K-E03 falla el requisito de valor exacto aunque conserva el
significado: no se relaja ese oráculo después de ver la respuesta.

Siguen bloqueando GO: R2, correo real, aceptación pública instalada/NOVA/ChatGPT,
protección recurrente autorizada, límites/corte del alojamiento y las verificaciones
finales pendientes. No hay tag estable ni publicación por esta revisión.

Código de referencia: **1.0.1-rc.2 / bbefd38626e82c4ce389cd3735a657b0a2f929f0**.
Base del 31/10/2026; delta local actualizado el 01/11/2026, sin publicar.
Roadmap único: [R1–R6](development/GO_TO_MARKET.md).
Estado global: **NO-GO**. Esta matriz es una vista de aceptación, no otro roadmap.

Criterio de cierre actualizado por el usuario el 01/11: MVP funcional, sin exigir
perfección. Los [subchecklists y umbrales](development/GO_TO_MARKET.md#subchecklists-de-aceptación-funcional-del-mvp--01112026)
requieren 100% de controles críticos, todos los recorridos básicos ofrecidos y
al menos 18/20 tareas de NOVA (90%), con mínimos por grupo/familia. Los defectos
menores documentados no bloquean por sí solos. Esta definición no transforma los
resultados históricos ni los pendientes de esta tabla en PASS.

PASS se limita al entorno y artefacto indicados. FAIL conserva un resultado
incorrecto observado. BLOCKED identifica una dependencia concreta. NOT_TESTED
significa que no hay ejecución conjunta del escenario sobre el candidato final.

## Procedencia de las cifras — 01/11/2026

**Los 1504 PASS son un corte histórico, no la suite del árbol actual.** Su TRX
terminó el 01/11 a las 10:09:33 a. m. (UTC−04:00), antes de los cambios posteriores
de autoría. SHA-256 de `commercial-team/backend-current.trx`:
`351621a266382a4d1e3ffccc65d64233fd2037d0f3f8a2a1874a5386b9d522b2`.
No atribuir ese verde al productor en integración ni a la CI del candidato final.

El snapshot posterior de parser/consumidores obtuvo **105 PASS/11 FAIL de 116**
el 01/11 a las 6:17:36 p. m. (UTC−04:00). Recibo
`.playwright/nova-compression-ledger-20261101/parser-whole-candidate-final.trx`,
SHA-256 `27cfe7886c9b609ee6fbb42dc8ddd5c1c50724d523fb9ecc378a9b1e1a7c3d55`.
Es una suite de snapshot de autoría, no una nueva ejecución de todo el backend.
Los once fixtures esperan la integración del productor; el A/B preservado reproduce
esos once fallos con el parser anterior. No se borran pruebas ni se cambian sus
expectativas para sustituir la integración. El estado posterior y sus límites
permanecen en el [checklist único](development/GO_TO_MARKET.md).

## Corte histórico del trabajo en equipo — 01/11/2026

La corrección de conexión de Core en `ApplicationModules`/`NovaAgentSettings`
se probó en RC2 con cambios locales, sin publicar. Las nueve regresiones nuevas
pasaron dentro del focal de 73. El primer focal preserva 8 FAIL; el cambio
experimental a un editor sin HTTP produjo otro FAIL real de disponibilidad y fue
retirado. No se modificaron permisos ni el motor/modelo.

| Control del corte histórico | Resultado y alcance | Recibo local |
| --- | --- | --- |
| Backend Release | 1504 PASS, 0 FAIL, 2 SKIP opt-in de proveedor real | `commercial-team/backend-current.trx` |
| Fábrica con DMR real, opt-in separado | 1 PASS; extracción sintética exacta, una llamada, 16 376 ms; sin cuentas, UI, DB o Companion instalado | `factory-connection/model-connection-real-executed.trx`, `factory-connection/real/factory-dmr.json` |
| Frontend Release | 502 PASS en 63 archivos; jsdom, no aceptación SaaS | `commercial-team/web-tests.log` |
| Cloud y frontend build | PASS; Cloud 0 warnings/errores, web conserva chunk de 537,13 kB | `commercial-team/cloud-build.log`, `commercial-team/web-build.log` |
| Recovery | 45 PASS, 0 FAIL/omitidas; criptografía/CLI/filesystem sintéticos | `team-recovery/recovery-current.trx` |
| Layout y fronteras | 7 y 40 controles PASS; estáticos | `commercial-team/module-boundaries.json` |
| Integridad del delta de fábrica durante la suite | Hashes iguales de las dos fuentes y su prueba; no es inventario integral del release | `commercial-team/factory-inputs-before.json`, `commercial-team/factory-inputs-after.json` |

Todos los recibos de esta tabla están bajo
`.playwright/priority-closure-20261101/`. No sumar los focales a las suites como
casos distintos ni atribuirlos a la CI de bbefd38. El opt-in DMR se ejecutó después
con el mismo assembly Release, sin recompilar; su PASS procede del recibo real,
no del SKIP. El intento anterior de compilarlo coincidió con la suite del otro
chat y sufrió un bloqueo de DLL en Windows: fallo de arnés conservado, no del modelo.

Diez propuestas reales de reemplazos de campo completo no cumplieron aceptación:
K-A01 no acorta y K-A03/H-A01 alteran el alcance o disparador de condiciones;
el contraste de idioma cambió un marcador y el host lo rechazó. Diez propuestas
de un selector alternativo dieron 9/10 conjuntos correctos (6/6 de la muestra de
búsqueda), pero siguieron ampliando un título explícito a su tema. Ambos contrastes
quedan **RECHAZADOS, SIN INTEGRAR** en `field-transaction-probe-01/` y
`selector-set-probe-01/`. Son componentes aislados, no una nueva campaña del
producto ni una modificación de su puntuación previa.

## Recorrido ofrecido

Medición posterior del subchecklist,01/11:la muestra real de20 componentes obtuvo
9/20 correctos(45%),sin aceptación pública. Adaptación0/8,selección4/6,ejecución5/6.
Tras corregir la pérdida de la orden de edición,el contraste de8 adaptaciones
continúa0/8 en utilidad aunque el paquete ya contiene el encargo. La suite de
regresión backend pasa1495/1 SKIP; no certifica esta calidad. Detalle y recibos
en el [único checklist](development/GO_TO_MARKET.md#ejecución-del-subchecklist-nova--muestra-real-del-01112026).

| Requisito / criterio observable | Resultado sobre el RC final ofrecido | Evidencia y límite |
|---|---|---|
| Registrar cuenta, recibir confirmación y acceder | PASS público acotado; conjunto RC pendiente | 02/11: Brevo real, confirmación recibida/consumida y acceso por navegador en e16e4ee. Confirmación en spam. Evidencia y límites en ACCOUNT_MAIL. El 403 por falta de activación es histórico. |
| Iniciar sesión con Google en la cuenta principal | PASS público; RC pendiente | Navegador real 31/10: cuenta principal y biblioteca personal visibles. La revisión servida no se identificó de nuevo como bbefd38. |
| Crear y recargar un prompt con/sin variables | NOT_TESTED conjunto RC | Hay pruebas de Accounts y aceptación histórica de biblioteca; falta repetir el recorrido en el candidato publicado. |
| Buscar → seleccionar → completar → compilar → copiar íntegro | PASS público acotado; RC pendiente | Navegador real 31/10: plantilla de aceptación existente, tres variables, Unicode y ampersand; portapapeles real igual a 165 caracteres esperados. Recarga y lectura del resultado persistido exactas; plantilla/revisión 1 conservadas. No hubo inferencia ni prompt nuevo. El SHA servido no se acaba de identificar. |
| Campos faltantes, Unicode y valores no autorizados | PASS de contratos; UI RC pendiente | CI de backend/Accounts; los controles no demuestran portapapeles real del candidato. |
| Editar, recargar y detectar conflictos de revisión | NOT_TESTED conjunto RC | CI y pruebas frontend no sustituyen persistencia pública después de la operación. |
| Duplicar y eliminar sólo datos autorizados | NOT_TESTED conjunto RC | No se borraron datos privados durante esta revisión. Borrado irreversible exige autorización específica. |
| Crear colección y verla en el detalle del prompt | NOT_TESTED conjunto RC | Propietario compartido en Accounts; pruebas de colecciones y UI en CI. |
| Cuenta A intenta leer/escribir un recurso de B | PASS PostgreSQL sintético; público RC pendiente | CI 41228098851, Accounts 590 PASS/30 no ejecutadas y restauración con 23 tablas FORCE RLS. |
| NOVA conserva tarea, variables y requisitos al crear | PASS local; público RC pendiente | Cambio expresamente autorizado: creación conserva el contrato humano sin discovery/escritor; 1492 backend y 502 web PASS. Revisión del componente real en navegador con datos sintéticos; no persistencia SaaS. |
| Mejorar/Adaptar reescribe sólo por elección explícita y revisión separada | PASS acotado de utilidad; familia semántica abierta | Una nueva adaptación real acorta repeticiones y conserva variable, restricciones e idioma; dos llamadas y autorrevisión de primera sugerencia. Los dos fallos históricos del escritor quedan preservados, sin convertir este caso en campaña universal. |
| Confirmar contenido conserva exactamente el documento y sus metadatos | PASS de composición; persistencia pública RC pendiente | Cuatro composiciones controladas y contratos de igualdad; confirmación sintética distinta de aceptación humana. |
| Companion instalado ejecuta, cancela y reconecta sin repetición | BLOCKED aceptación pública | Doctor del perfil instalado y prueba real .NET/Core/DMR PASS: cancelación 27 ms y petición posterior 2412 ms. No es arranque autenticado del acceso instalado; permanece pendiente por rechazo previo de revisión. |
| ChatGPT busca, lee, compila y crea mediante MCP | PASS público acotado; RC pendiente | Recibos reales del 31/10: resultado visible y durable. Sin nueva publicación del RC; renovación/revocación conjunta pendiente. |
| Recuperación: correo, cambio de clave, rechazo de enlace usado/vencido | PASS público de recepción/reset/replay; caducidad controlada | 02/11: recuperación por formulario, correo Brevo recibido en inbox, enlace abierto y consumido por API pública, dos sesiones revocadas y nuevo login por navegador. Caducidad sólo en pruebas controladas anteriores; submit del formulario reset no automatizado. |
| Reinicio, instalación limpia, actualización y retirada | NOT_TESTED completo | ZIP repetible/traslado/imports comprobados; no equivalen a Windows limpio ni a agente autenticado. |
| Respaldo cifrado recurrente y restauración operativa | BLOCKED cierre comercial | CI restaura datos sintéticos. No se reutilizaron permisos puntuales para otro respaldo privado. |

Delta local 01/11: backend 1492 PASS/0 FAIL/1 hardware SKIP en suite ordinaria;
el test DMR opt-in posterior pasó realmente, por separado. Web 502 PASS y build
PASS; Cloud build sin errores/warnings; Accounts 90 controles PASS sin SQL.
Dos exportaciones Companion idénticas (ZIP SHA-256
53583098fd3a22f8963edd72bc4dee6a3c6094bf925d2927f0a4302d89fd70ff),
15 verificaciones de traslado PASS. No es instalación de un Windows limpio.
Recibos `.playwright/priority-closure-20261101/`; configuración compartida y
segundo consumidor real en [Docker AI](development/DOCKER_AI.md).

## Baseline del código exacto

[CI 41228098851](https://github.com/JuanPabloTorres/QuickPrompt/actions/runs/41228098851):
jobs `web` y `postgres-accounts` SUCCESS. TRX: backend 1487 PASS/1 no ejecutada;
Debug 8 PASS; MCP 27 PASS; Companion 78 PASS; recuperación 45 PASS; legacy 168
PASS; Accounts 590 PASS/30 no ejecutadas. No sumar estas etapas como casos únicos.
El workflow también terminó build y pruebas frontend; no se infiere su conteo
desde un TRX que ese job no publica.

Build local nuevo de Cloud Release: PASS, 0 errores/0 advertencias. Build React:
PASS; conserva advertencia de chunk de 536,94 kB. Los 130 tests de correo de los
tres adaptadores usan HTTP controlado, sin proveedor ni entrega reales.
Delta local M2M posterior: 180 PASS controlados. La comprobación externa consiguió
token 200 con scope exacto y documentó envío 403 SMTP sin activar; cero mensajes
aceptados. No es una aceptación del alta o la recuperación y no está publicado.

Evidencia privada del operador: `mail-provider-switch-20261031`,
`desktop-entry-boundary-20261031`, `literal-requirements-20261030` y
`mcp-full-schema-20261031`, bajo el directorio de cierre del 29/10. No se incluyen
credenciales, contenido de biblioteca o conversaciones privadas en este documento.
