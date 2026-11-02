# QuickPrompt — estado del Release Candidate

## Integración de la versión inicial 1.0.1 — 02/11/2026

Propietario actual: chat Cerrar integralmente QuickPrompt. Se retoma el checkout
`QuickPrompt-Initial-Release-20261102` que preparó el otro chat; no hay otro plan
de entrega. La orden actual autoriza commit, merge, CI/CD, tag y promoción tras
completar los controles requeridos. El tag final todavía no acredita aceptación.

La muestra conjunta congelada pasó 19/20 (8/8 adaptación COMPRESS, 6/6 búsqueda,
5/6 ejecución), 28 llamadas reales y cero retries. El fallo H-E02 de foto omitida
se conserva. Ese score no mide authoring TRANSLATE. Las capturas R13/R14 sin aclaración siguen 3/4; R14 se rechazó. Se integra
la conservación de citas como contexto de lectura de R13; el usuario confirmó ahora que ficha
significa tarjeta/registro de interfaz. La nueva comprobación debe etiquetarse
como asistida por esa aclaración, sin alterar las capturas originales.

Integración CI/CD: snapshot Core permitido por hashes, recurso lingüístico en
trabajo Linux explícito, instalación/rollback/retiro del paquete en Windows nuevo,
publicación de ZIP/hash y GHCR tras controles, promoción explícita por SHA exacto.
Resultados remotos todavía pendientes; no declarar PASS por escribir el YAML.

Estado integral: PENDIENTE. La aceptación pública NOVA/MCP se interrumpió cuando
el usuario detuvo Computer Use con Escape; no se operará más el navegador en ese
turno. Permanecen también respaldo cifrado operativo y restauración privados,
horario de 12 horas público y comprobación de instalación final. Se conserva el
resto del expediente histórico; las fechas inferiores no son estado nuevo.

## R3/R5, dos cuentas y persistencia públicas — 02/11/2026, 20:49 UTC

Dos cuentas QA públicas distintas se confirmaron y recuperaron con correo real.
En `e16e4ee` pasaron login por navegador, crear/guardar texto exacto, buscar,
editar, versiones, duplicar, variables/render/compilación real en cola,
aislamiento directo y compartir/aceptar/revocar por UI. Eliminación por API
comprobada sólo sobre el prompt sintético propio y con controles negativos.

Reinicio real de Render observado a las 16:48 (UTC−4), health canónico 200;
el duplicado exacto y ambas sesiones sobrevivieron y la cuenta ajena recibió 404.
La continuación de persistencia PASS conserva el FAIL previo del arnés por
espera de operador de 180 segundos; no repitió escrituras ni llamadas al modelo.
Recibos: `.playwright/public-two-account-20261102/RESULT.md` y `persistence-01/`.
Estos controles sustituyen el pendiente de dos cuentas del piloto, sin certificar
NOVA/Companion/ChatGPT ni cambiar el **NO-GO general**. El corte de correo
inferior pertenece a otra cuenta QA que no creó bibliotecas; estas dos sí tienen
bibliotecas sintéticas dedicadas y un duplicado preservado para aceptación.

## R3/R5, correo público aceptado — 02/11/2026, 15:44 America/La_Paz

Brevo está configurado en Render y el backend `e16e4ee` está Live, despliegue
`dep-dbkedkl9fdbs739tt1mg`. El usuario autorizó expresamente publicación y uso
remoto de la credencial existente. Árbol idéntico al candidato aislado aprobado;
no se promovió RC2 ni NOVA experimental. Opciones públicas: email=true,
mailDelivery=configured, google=true y delegation=true.

Registro/confirmación/acceso comprobados por navegador y correo real; recuperación
solicitada por formulario, enlace recibido y abierto, reset consumido por la API
pública y acceso con la nueva contraseña comprobado por navegador. Los 21 controles
HTTP públicos aprobaron, incluidas revocación de dos sesiones y rechazo de replay.
Confirmación en spam y recuperación en inbox. El submit del formulario de reset
y la caducidad por espera real no se automatizaron; no se certifica entrega universal.
Sesiones QA cerradas, sin bibliotecas/prompts creados y temporales privados retirados.

Este corte sustituye los bloqueos anteriores de activación/configuración/recepción
del correo; conserva el **NO-GO general** y los otros pendientes R1–R6.
Detalle y recibos: [correo](ACCOUNT_MAIL.md), sección del 02/11 a las 15:44.

## Continuación del cierre — 02/11/2026, 19:22 UTC

La meta sigue activa y **NO-GO**. Se recuperó la activación real de Brevo descrita
debajo: sustituye la dependencia de conectar Gmail en EmailJS. La aceptación de
correo público requiere publicar únicamente el candidato aislado de cuentas y
autorizar el traslado de la credencial local a Render; solicitud concreta pendiente
en este chat. No se publicó RC2 ni se cambió proveedor efectivo en producción.

R2 continúa sobre fuentes aisladas en
`.playwright/translation-boundary-repair-20261102/`, sin promoverlas al producto.
La traducción de campos completos pasó de 0/4 a 2/4 y después 3/4 en capturas
separadas, conservadas con sus fuentes y binarios. La posedición ya conserva la
fecha, la exclusividad y la continuación de las dos condiciones portuguesas.
La revisión 05 todavía traduce incorrectamente `ficha` como `chip`: **3/4, ocho
llamadas reales, cero retries y cero efectos**. No califica ni modifica el 14/20
del candidato integral. La revisión 06 corrige la conexión del contrato de
evidencia léxica; su aceptación real sigue pendiente. Los intentos de contexto
excesivo y el error del arnés al medir el sobre JSON permanecen en sus recibos.

No repetir las altas de correo ni inferir que estas mejoras parciales completan
el recorrido público, la campaña conjunta o la aceptación de Companion/ChatGPT.

## R3/R5, Brevo activado y aceptado en QA — 02/11/2026

El correo de soporte del 02/11 a las 12:20 America/La_Paz confirma cuenta y
envío transaccional activados. Token 200, envío 201 y recepción real comprobados.
Registro/confirmación/acceso/recuperación con Identity real y PostgreSQL QA local:
15 controles aprobados, dos correos recibidos y revocación de ambas sesiones.
Confirmación en spam y recuperación en inbox; no se certifica entregabilidad
comercial. Transportes: 210 PASS / 0 FAIL / 0 SKIP; Cloud Release preparado.

Producción sigue pendiente: Render Live `4cb104b` no soporta M2M y no tiene correo
configurado. No se publicaron cambios ni se transfirió la credencial local a Render.
No equivale a aceptación pública ni modifica el NO-GO general. Se reutilizó el
candidato aislado `QuickPrompt-Auth-Release-20261101`, sin NOVA experimental.
Recibos y límites: [correo](ACCOUNT_MAIL.md), sección del 02/11.

## Corte ejecutado de cierre integral — 02/11/2026

**Decisión vigente: NO-GO.** Este es el estado actual de R1–R6; los apartados
fechados inferiores conservan los intentos y decisiones anteriores. Una sola meta
de cierre en el chat «Cerrar integralmente QuickPrompt», sin publicación ni tag.
Checkout comprobado: `C:/Projects/Repos/QuickPrompt-Web-RC2`, rama
`release/quickprompt-1.0.1-rc.2`, base
`bbefd38626e82c4ce389cd3735a657b0a2f929f0`, versión web/Companion `1.0.1-rc.2`.
Incluye el delta local; no atribuirlo al commit base ni al piloto público.
El manifiesto final y su inventario están en
`.playwright/candidates/closure-20261102-execution-review/`; los recibos del corte en
`.playwright/closure-20261102/`. Son artefactos locales, no una publicación.
Los cortes anteriores `closure-20261102-final` y `closure-20261102-accounts` y sus ZIP permanecen intactos como
históricos. La entrega vigente está en
`.playwright/execution-task-frame-20261102/delivery.json`.

**Seguimiento público R5 — 17:02–17:11 UTC:** health respondió 200 en Vercel
(464 ms) y Render (301 ms); dos GET de opciones Python respondieron 200, con y
sin proxy. Ambos confirman email=false y mailDelivery=unconfigured. Los timeouts
previos no se reprodujeron y su causa sigue desconocida: no se afirma reparación.
GET anónimos de bibliotecas, recurso y perfil Companion: 401; CSRF 200 con cookie
Secure/HttpOnly/Strict; metadata MCP 200 con resource exacto. GET MCP devuelve 405,
permitido cuando no ofrece stream SSE; POST initialize anónimo devuelve 401 con
challenge y metadata correctos. Se conservan los dos lotes del arnés con sus
expectativas iniciales erróneas, sin modificar los controles del producto.
Vercel identifica el despliegue `dpl_8rQ46btmN7ztTNwTB58XKzHFnaxV`, READY y
origen CLI; commit Git publicado aún sin confirmar. No se equipara al candidato.
Recibos: `.playwright/public-route-diagnosis-20261102/RESULT.md`.
El snapshot reproducible anterior permanece intacto; estas son notas operativas
posteriores. Correo continúa pendiente del titular; esto no bloquea reparar R2.

**Actualización de ejecución R2 — sin promoción:** el primer ensayo de contexto
estructurado obtuvo **2/6** y la única corrección acotada de roles tarea/contexto
obtuvo **5/6**, con seis llamadas reales por ensayo, cero retries y cero efectos.
Se revisaron los seis oráculos completos originales; H-E02 sigue omitiendo admitir
la fotografía ofrecida. No se cambia ese requisito ni se calcula un nuevo 20/20
combinando respuestas. El criterio congelado exigía 6/6: **FAIL**.
Se inició prematuramente una integración optativa; se archivó y retiró por no
superar ese criterio. Siete archivos restaurados con sus hashes originales y
cinco archivos añadidos retirados del producto. Work v1, permisos, paquete instalado
y despliegue conservan su comportamiento. El código del producto y las pruebas
coinciden con el corte `closure-20261102-native-final`; este corte sólo actualiza
documentación. Prototipos, respuestas e intentos HTTP fallidos permanecen en
`.playwright/execution-bound-context-20261102/` y
`.playwright/execution-task-frame-20261102/`; la revisión es del agente, no aceptación humana.

Tras retirar la integración: backend **1792 PASS / 0 FAIL / 2 SKIP**, Companion
**114 PASS**, siete controles HTTP/PostgreSQL de ejecución local **PASS** (dos
cuentas, dispositivo/lease, revocación, cancelación y uso sin duplicados).
Las dos optativas reales no se repitieron en esta corrida; el recibo
histórico de 1794 PASS permanece separado. Tres sondas públicas sin autenticación
del 02/11 a las 16:51:26 UTC (health de ambos orígenes y opciones del frontend)
agotaron aproximadamente 15 segundos: disponibilidad y opciones actuales
**SIN CONFIRMAR**, sin atribuir una causa al proveedor o al proxy.
El resultado completo y los recibos están en `execution-task-frame-20261102/RESULT.md`.

**Actualización R2 — protocolo nativo ejecutado:** se reutilizó la primitiva QA6
congelada, sin cambiar fuentes, oráculos, pin, perfil ni presupuesto. Doce controles
offline PASS; seis POST, seis llamadas nativas válidas, cero retries y cero efectos.
Replay con DLL actuales: cuatro composiciones, un rechazo del dominio por perder
una variable y un rechazo de la política numérica del ensayo. **Semántica: 0/6**
contra sus oráculos completos, incluidas dos reducciones parciales que conservan
redundancia. No se implementa la integración nativa: su prerrequisito de
calificación falló. La campaña final permanece 14/20 y traducción 0/4; no se suman
los seis aciertos de protocolo ni se repiten propuestas para seleccionar una.
Resultado y hashes: `.playwright/closure-r2-native-20261102/RESULT.md` y
`semantic-assessment.json`; bytes originales en
`.playwright/nova-native-tools-primitive-01/capture-01/`.
El origen interno de la decisión semántica sigue sin confirmar. No se ha probado
incapacidad universal de Qwen ni se ha encontrado un reemplazo calificado.
El intento de manifiesto `closure-20261102-native` se conserva como
`SOURCE_CHANGED_REVALIDATE`: detectó una modificación concurrente legítima en
`MCP.md`. Se esperó al fin del chat propietario y se volvió a capturar con otra
identidad, sin sobrescribir el rechazo. La revisión MCP corrigió su diagnóstico;
no reparó producto ni atribuyó el incidente a la conexión sin captura inicial.
Una nueva lectura pública de `/account/options` agotó 30063 ms; no se infiere su
configuración actual de ese timeout. El último resultado legible y el heartbeat
del propietario mantienen correo sin configurar/conexión Gmail pendiente.

**Actualización Accounts:** suite completa con Identity/HTTP/PostgreSQL reales:
**731 PASS / 0 FAIL / 35 SKIP**, 766 casos, 16 min 25 s. Los tres fallos anteriores
eran pruebas de aclaración por IA que seleccionaban Crear después de confirmar
una tarea completa; el motor devolvía correctamente revisión literal sin modelo.
Ahora ejercitan Adaptar con fuente propia y la variable declarada `<tipo>`;
sus **66 líneas de aserciones permanecen idénticas**. Una prueba HTTP adicional
exige Crear literal, revisión humana, cero trabajos de inferencia y ningún
guardado antes de la aprobación. El sondeo vacío del arnés pasó de 50 a 250 ms
para evitar agotar la cuota real; no se cambió ningún control del servidor.
Focal 5/5, recibo completo `accounts-corrected-all/accounts-corrected-all.trx`.
Diagnóstico y todos los intentos fallidos: `accounts-diagnosis/diagnosis.md`.
Sólo cambiaron tres archivos de pruebas y estas vistas documentales; el producto,
las campañas 14/20 y 0/4, el ZIP Companion y el despliegue siguen sin cambios.

| ID | Estado actual y resultado comprobado | Lo que falta para cerrar |
| --- | --- | --- |
| R1 | PARCIAL. Instalado compat.1: doctor DMR y autorización real PASS. Launcher ejecutado contra el servicio público entre 9:10:57 y 9:12:12 a. m. (UTC−4); `last-run.json` registra `UserStopped`, cero trabajos y ausencia del PID tras Ctrl+C. Paquete nuevo reproducible; contratos 114 PASS, export 15/15 y ciclo portable 9/9 | Petición pública correlacionada, propuesta/revisión/aprobación/guardado exacto, continuidad y reconexión del instalado. El paquete nuevo no sustituyó el instalado ni el backend publicado |
| R2 | **FAIL.** Integrado el editor de revisiones existente, conservando Crear literal y aprobación externa. Campaña única `closure-20261102-semantic-20`: **14/20**, adaptación 5/8, búsqueda 6/6, ejecución 3/6, 20 llamadas reales, cero retries. Prueba separada de traducción: **0/4**, ocho llamadas; no se suma a los veinte | Reemplazar y calificar el mecanismo semántico fallido, sin parches por frase, rebajar 18/20 ni seleccionar otra respuesta. Los controles críticos y el recorrido público siguen abiertos |
| R3 | **Piloto público verificado parcialmente** en `e16e4ee`: dos cuentas sintéticas con confirmación/recuperación por correo real; login UI, CRUD literal (borrado API), versiones, duplicación, variables/render/compilación real, compartir/aceptar/revocar UI, negativos de bibliotecas/prompts/memoria/resultados. Reinicio real conserva texto exacto, sesiones y aislamiento. Recibos `public-two-account-20261102/`; se conserva el deadline FAIL del primer arnés | Sesión vencida final, enlaces caducados por tiempo real, colecciones/historial y aceptación conjunta del candidato final. No equiparar este piloto al RC2 semántico |
| R4 | PARCIAL. MCP: 27 contratos PASS. ChatGPT web mostró una búsqueda sintética sin coincidencias en un chat nuevo; el detalle de llamada no pudo expandirse tras perder la conexión del navegador. No se presenta la afirmación del asistente como recibo MCP completo | Vinculación con dos cuentas QA públicas, lectura/compilación/creación en destino exacto, scopes, refresh, expiración y revocación del cliente web final. No escribir en bibliotecas personales para suplir ese control |
| R5 | **PARCIAL.** HTTPS/health/discovery y guardas públicas observados; cookie CSRF Secure/HttpOnly/SameSite Strict. Render/Neon Free, origen Vercel. Brevo configurado con autorización expresa y correo recibido; opciones email/google/delegation=true, mailDelivery=configured en `e16e4ee`. Reinicio público con dos sesiones y datos exactos PASS. Restore PostgreSQL QA y Recovery 45 PASS son evidencia local fechada | Corte y límites del alojamiento final; política, clave, destino, retención y alertas de backup recurrente, restauración privada y rollback del candidato final. No reutilizar autorizaciones de respaldos anteriores ni volver a la base Render obsoleta |
| R6 | PARCIAL. Frontend 532 PASS/build PASS; Cloud build PASS; imagen Docker construida y 19 controles de entrada/secretos PASS como UID 1654. Backend final del chat «Corrige fallidos y omitidos»: **1794 PASS/0 FAIL/0 SKIP** incluidos dos opt-in DMR. La matriz conserva por separado las suites Accounts, campañas y fallos del arnés | Resolver R1–R5, terminar controles críticos faltantes y revisar una única identidad publicada. Sin GO por un build, contrato JSON, ZIP o suite verde |

La [matriz de aceptación](../MVP_ACCEPTANCE.md), la [guía operativa](../../deploy/operations/README.md)
y el [acta de cierre](../MVP_CLOSURE.md) son vistas de este registro, no otros roadmaps.
Los 1504 PASS históricos no se reutilizan como validación del árbol actual.

### Cambios y evidencia atribuida

Se conecta `PromptDraftGenerator` al `AuthoringRevisionEditor` ya existente,
con contrato confirmado, variables, restricciones, referencias y plazo de 90 s.
No se permite que el editor confirme o persista. Se comprueba cancelación antes
de interpretar cada respuesta. Dos fixtures ahora suministran explícitamente una
respuesta controlada; sus assertions de cancelación/revisión no cambian. La
prueba de modo de autoría usa el ASK vigente con los campos nulos obligatorios.
El exportador incorpora sólo el recurso y aviso de traducción declarados por el
proyecto; no amplía su inventario a configuraciones privadas.

La ejecución inicial del backend conservó 1672 PASS/120 FAIL/2 SKIP; la integración
redujo a 1787/5/2 y la siguiente a 1790/2/2. El usuario pidió en otro chat resolver
estos dos contratos y ejecutar las opt-in: su corte final preserva la igualdad
estricta del esquema con `translationPreparation` y actualiza sólo etiqueta y
descripción de Crear literal en el snapshot, sin cambiar argumentos, permisos,
timeouts u operaciones. Comprobar `backend-allpass-20261102/contract-deltas.json`,
`verification.json` y `full-suite/backend-all.trx` bajo `.playwright/`.
Son comprobaciones de contrato, **no fidelidad semántica**. Los fallos anteriores
siguen en `closure-20261102/backend*/`.

Se respetó la reserva exclusiva `QuickPrompt.Nova.SemanticModel.lease`: primero
las campañas, después el Companion, y se liberó antes de las opt-in del otro chat.
Qwen conserva el pin físico `977d1540140c…2aabb82`, endpoint 12434, contexto 12288,
parallel 1 y reasoning-budget 0. Petición real Core/.NET PASS; cancelación 30 ms
y siguiente petición 2030 ms en el recibo de este chat. El corte posterior del
otro chat conserva sus propios tiempos; no se mezclan.

### R2: primera divergencia y límite de la campaña

**REFACTOR_OBLIGATORIO, no resuelto:** el contraste nativo posterior confirmó seis
llamadas auténticas, pero no corrigió el resultado (QA6 0/6); por ello no se integra
ni se sustituye el editor activo por esa alternativa. K-A01/K-A02/K-A03 recibieron alternativas
completas `task/0` y `task/1` dentro de 6896/6239/5655 bytes. Qwen eligió `task/0`
en las tres llamadas y el compilador conservó exactamente esa elección: no hubo
acortamiento útil. No es ausencia del catálogo ni un timeout. La causa de la
elección semántica permanece sin determinar; no prueba incapacidad universal de
Qwen. No se añadió otro parche de prompt ni se repitieron esos casos.

La campaña conserva todas las primeras respuestas y el oráculo fuera del paquete
del modelo. Los diez casos rotulados holdout pertenecen al conjunto histórico ya
visible en el proyecto: no se presentan como un nuevo holdout ciego. La revisión
es del agente sobre checks congelados, no aceptación humana independiente.
`semantic-assessment.json` registra por caso motivo, latencia y hash; mediana
4389 ms, máximo 31192 ms. Tres adaptaciones no mejoran; K-E01 añade/contradice
tareas/responsables, K-E03 incumple el valor exacto del propósito y H-E02 omite el
control de la foto. La prueba separada `translation-assessment.json` conserva dos
rechazos de marcadores, el cambio del literal «Firmar» y una condición portuguesa
traducida con significado incorrecto. El compilador que rechaza una respuesta no
demuestra traducción correcta. No se mezcla el 14/20 con los 9/20 o 18/28 anteriores.

### Límites de operación y aceptación externa

El E2E local usa exclusivamente datos `@example.invalid` y buzón QA. La última
suite Accounts se lanzó inicialmente con el host QA aún consumiendo su cola;
ese intento queda conservado como contaminación de arnés, no certificado del
producto. Se detuvo el PID propio: el contraste limpio conservó 727 PASS/3 FAIL/
35 SKIP antes de corregir el arnés; el nuevo corte completo obtiene 731/0/35,
sin ese consumidor. La matriz identifica cada TRX y sus fallos históricos, sin
sumar focales ni campañas.

En la comprobación pública posterior, `/health` agotó 30081 ms y
`/account/options` respondió 200 en 16964 ms con correo sin configurar. El
backend directo Render respondió después 200 en 244 ms. Se conservan ambos
recibos; la causa de la demora inicial no está confirmada y no se declara
disponibilidad final por la respuesta posterior.

El público sirve assets distintos del build RC2 (`public-fingerprint.json`);
el SHA efectivo del backend publicado no fue observado en el panel en este corte.
No atribuirle el commit local ni repetir el SHA histórico como observación actual.
Render/Neon se mantienen; sus límites oficiales se revisaron sin cambiar planes
ni activar facturación. La autenticación Google, el correo y el conector final
requieren acceso verificable del titular. No hubo nuevas acciones de soporte,
revocaciones reales, respaldos privados o modificación de bibliotecas personales.

## Última ejecución NOVA: fallos separados y puente QA corregido — 01/11/2026

**DMR ya permite ejecutar Qwen; NOVA sigue NO-GO.** El contraste dependiente
propuesta → observación literal del host → revisión completó seis casos y siete
llamadas reales, sin reintentos. Cinco casos no alcanzaron la revisión por un
defecto de codificación del arnés QA: PowerShell emitía OEM 437 y Python esperaba
UTF-8 bajo el lanzador .NET sin consola. El único caso completo, C-MULTI-TASK,
repitió la misma respuesta y sustituyó «No inventes datos» por la tarea. Son dos
fallos diferentes; corregir el transporte no acredita fidelidad.

El puente se corrigió en un candidato offline separado: productor UTF-8 explícito
y captura binaria con rechazo controlado. Pasan 29 controles y el replay de los
seis primeros archivos bajo el mismo arranque anidado, incluyendo acentos,
marcadores y emoji. Cero llamadas nuevas, cero builds compartidos y ningún caso
reanudado fuera de su plazo. Evidencia:
`.playwright/nova-host-delta-review-primitive-01/RUN_RESULT.md` y
`.playwright/nova-host-delta-review-primitive-02/README.md`.

En una evaluación de componente distinta, traducción con recursos léxicos obtuvo
**10/10 significado y 10/10 compilación**, 20 llamadas sin reintentos; recibo
`.playwright/nova-apertium-resource-slots-qa-01/capture-01/ASSESSMENT.json`, SHA-256
`361b719541236d3a0aa8b36e70bd898f4386baf9922d41e215b00505a8bb8585`.
Los idiomas y la política de literales estaban explícitos en los fixtures;
no acredita descubrimiento de idioma, integración, holdout ni aceptación pública.

**18/28 conserva su significado histórico.** No se calcula un porcentaje nuevo
mezclando estas campañas. Falta integrar un mecanismo semánticamente calificado,
ejecutar el recorrido completo y el holdout, y cerrar la aceptación pública.
Sin publicación ni tag estable por este trabajo.

## Delta NOVA: DMR disponible y extracción V2 ejecutada — 01/11/2026

**Ya se ejecutó Qwen real; el registro V2 no supera su contrato.** El propietario
de Docker comprobó respaldo y actualización a Desktop 4.97.0.244584; después del
arranque manual de Windows, Models volvió a listar los modelos y el smoke de Core
pasó con el mismo pin Qwen3:8b y perfil original. Los errores de socket/502
anteriores son históricos, no el resultado de estas llamadas. No se demuestra con
ello arranque automático ni causa exacta del fallo previo.

En reserva exclusiva se ejecutaron **seis casos congelados, seis llamadas**, sin
build, reintentos ni reparaciones: transporte 6/6 y compilador real 6/6 aceptados.
Consumieron 99502 ms en conjunto, entre 9907 y 23091 ms por caso y entre 256 y 684
tokens de salida. El test de captura PASS acredita transporte; no acredita tarea.
El replay independiente del contenido completo obtiene **4/6**, con compresión
parcial en C-MULTI-TASK y C-MULTI-FIELDS.

Hay además divergencias de significado en los registros propuestos: H-A03 vuelve
opcional la pregunta obligatoria y pierde «antes de recomendar»; K-A01 omite
justificar los otros patrones; varias paráfrasis se convierten en prerrequisitos.
Un resultado literal correcto al reutilizar el origen no demuestra que la
extracción sea fiel. La revisión semántica independiente concluyó **1/6 extracción
completa y 1/6 conjunción extracción/replay**; se conserva en
`capture-01/ASSESSMENT.json` (SHA-256 `60c7b43f451e0a5ac872e2fce1095f5c9e90d9d5e72372135068115ca9a581fc`).
No se integró este mecanismo ni se modificaron permisos o persistencia.

Evidencias y comandos: `.playwright/nova-source-obligation-ledger-primitive-02/`
(`RUN_RESULT.md`, `capture-01`, `compiled-01`, `REPLAY_ASSESSMENT.json`). Fixture,
oráculo y ensamblados originales permanecen congelados. **18/28 sigue siendo el
resultado de la campaña de desarrollo anterior**, no de esta muestra de seis.
La prueba separada de posedición de traducción terminó **7/10 significado y 9/10
compilación**, sin reintentos: falla el actor/modo de la tarea en Signature,
Firmar se convierte en Sign (rechazado por protección de literal) y Sample no
conserva la acción show en QuoteKEEP. Recibo independiente
`.playwright/nova-apertium-postedit-primitive-01/capture-01/ASSESSMENT.json`, SHA-256
`4d066d19f1f9667255c1e0a7e5e0fb86f988f0c79f6a508611cfbac31445db12`.
Recibir idiomas explícitos en esos fixtures no prueba su descubrimiento. Tampoco
prueba solicitudes compuestas, instalación ni el recorrido público. Los dos
candidatos fallidos se conservan fuera del camino activo. La alternativa de
reescritura directa quedó descartada como mecanismo nuevo: ya existe en el writer
y su contraste con EditingOrder obtuvo 0/8 tareas acortadas con esos mismos hashes
de Material/Definition. La comparación está en `DIRECT_WRITER_COMPARISON.md`;
`REPLACEMENT_DESIGN.md` conserva el diseño evaluado y su descarte. No se volvió a
ejecutar la alternativa conocida bajo otro nombre ni se declaró reparada.
La integración y el holdout siguen sin ejecutarse para un reemplazo calificado. **NO-GO**
para cerrar NOVA; sin nueva publicación ni tag estable por este trabajo.

## Continuación NOVA: lectura JSON e integración pendiente — 01/11/2026

Se reprodujo un defecto adicional de frontera: ParseContent permitía propiedades
JSON repetidas y la deserialización elegía el último valor. La reparación acotada
en AgentResponseContent usa AllowDuplicateProperties=false. El resultado conserva
la ValidationException controlada; no cambia const, tipos, permisos ni precedencia.
El hash de origen sigue siendo responsabilidad del compilador. Los consumidores
que pasan un JsonElement ya analizado conservan la obligación de rechazar duplicados
en su propio lector; no se amplió el validador parcial ContractSchema.

**94 controles de componente PASS**, cero omitidos: los 79 anteriores más 15 de
wire/consumo. Hay casos top/nested/JSON truncado y recorrido real del generador con
respuesta controlada: la ambigüedad detiene la llamada antes del escritor y de
cualquier aprobación; una cancelación posterior a la respuesta mantiene prioridad.
Esto no califica semántica Qwen ni corrige los cinco fallos de marcadores de
traducción de la campaña de 28 casos. Fuente del parser SHA-256
`81c9809c8b6cd088b89dc31850979e58b951be0eac531d1ddfd2f07e0dce119d`.

**FAIL de integración conservado:** al incluir los consumidores de autoría, el
snapshot completo obtiene 105 PASS/11 FAIL de 116. Los mismos once casos fallan
con el parser anterior: el A/B de 22 consumidores obtiene 11 PASS/11 FAIL antes
y después. Los helpers y assertions esperan el nuevo Editor, pero el generador
activo mantiene Discovery/Definition antiguos. No se adaptaron respuestas falsas
ni se volvieron las expectativas al contrato anterior para mostrar CI verde.
La integración del Editor calificado sigue pendiente; sus assertions sólo podrán
reconciliarse con el contrato final aprobado, preservando contenido y permisos.

Se conservan por separado el snapshot original del compilador y parser-candidate,
con manifiesto de 143 archivos, recibos A/B y fallos originales. El primer probe
de wire confundió const en un lector parcial con el vínculo final del compilador;
esa expectativa se corrigió en su frontera explícita y no modificó producto.
Una prueba global de JsonElement se preservó como alcance no adoptado, sin aplicar
su guard global. Recibos `.playwright/nova-compression-ledger-20261101/`.

La recuperación DMR continúa en su chat propietario: actualización Docker ya
autorizada, condicionada a respaldo verificado de discos/modelos/configuración.
No se accedió a Docker/DMR durante esa operación ni se cambió el proveedor. Falta
extracción real, reparación de traducción y calificación del recorrido/holdout.
Se mantiene **NO-GO**, sin publicación ni tag estable.

## Auditoría causal y refactor de compresión NOVA — 01/11/2026

**R2 sigue FAIL; hay un compilador nuevo verificado, todavía no integrado.** La
calificación de desarrollo de 28 casos registra 18 correctos, cinco fallos de
compilación del protocolo y cinco fallos semánticos. No es el conjunto histórico
de 20 ni su holdout. En cuatro fallos de compresión hay alternativas completas
disponibles: seleccionar el original sin mejorar, retirar una continuación única
o elegir sólo parte de las omisiones no se explica por ausencia del catálogo.
En traducción se observa además identificación errónea de portugués como español,
confusión entre un nombre de archivo y un verbo, y pérdida de marcadores/entradas.
Los cinco errores técnicos no son cinco timeouts: el compilador rechazó respuestas
fuera del contrato de marcadores. Relajar ese rechazo no demuestra fidelidad.

Se añadió `AuthoringCompressionLedger` con un contrato tipado de obligaciones por
unidad original. El host conserva IDs/textos/orden; el modelo propone hechos con
predicado, argumentos, polaridad, modalidad, alcance y dependencias. El compilador
valida base, cobertura completa y DAG; compara estructuras sin depender de fact IDs;
conserva obligaciones nuevas y retira todas las unidades posteriores ya cubiertas
por testigos retenidos del mismo campo. Delega la edición a AuthoringEdits y
conserva sus límites, variables, cantidades, metadatos y roles. La equivalencia
estructural no certifica que el modelo extrajo todas las obligaciones ni permite
guardar, confirmar o ejecutar. No hay routing por palabras, juez adicional o SDK
nuevo. La skill agents se aplicó conservando Qwen/DMR y los propietarios existentes.

**PASS determinista: 79/79, cero omitidos**, en snapshot de Inference/Application/
Domain con sus proyectos reales. Son 32 casos nuevos y 47 regresiones existentes,
con hechos suministrados; no se presentan como 79 aciertos de Qwen. Se preserva un
primer fallo de fixture (campo 15k, ya inválido en el dominio); se corrigió el dato
de prueba a cuatro campos válidos de 7k para probar el límite 26k sin truncamiento.
El contrato nativo usa el mismo enum finito f0..f127 en IDs, dependencias y mapas;
no depende de pattern ni permite confundir fact IDs con unit IDs.
Fuente congelada SHA-256
`27343e6b423eb3c088efd1840bf5397c01c799887e211ee3206d371b81dec158`.
Recibos: `.playwright/nova-compression-ledger-20261101/`.

No se modificaron Editor.Plan, Previews, traducción, Core ni el recorrido activo.
El propietario NOVA conserva integración y reserva de inferencia. Falta comprobar
la extracción con Qwen usando exactamente Material/Schema/Compile congelados;
después, calificar las familias originales y el holdout antes de conectar el
reemplazo. El modelo no recibe el oráculo. DMR seguía devolviendo 502 según su
propietario: recuperar el motor no acredita recuperar inferencia. No se publicó
este candidato ni se creó un tag estable.

**R1/R3/R5, señal actual de auditoría a las 5:35 p. m. (UTC−4):** HTTPS health
de Vercel y Render 200; health ejecuta SELECT 1, no un CRUD completo. Opciones
públicas: google=true, delegation=true, email=false, mailDelivery=unconfigured.
El instalado Companion coincide con sus 67 hashes; no hay worker observado y la
API DMR 12434 no responde. El primer inventario amplio incluyó el PowerShell del
propio auditor: se descartó ese falso positivo y la consulta de procesos de
invocación real confirmó cero workers. Recibos
`.playwright/closure-audit-20261101/`. La base activa ya es Neon según los recibos
del traslado: la caducidad de la antigua base de Render no es el bloqueo actual.
Siguen pendientes respaldo recurrente, correo real, entorno comercial autorizado
y aceptación conjunta de los componentes exactos. El timeout del visor del
auditor es una limitación de observación separada; no causa por sí solo NO-GO.

## Delta de cierre: Companion compatible y aceptación MCP — 01/11/2026

**R1 — incompatibilidad y vinculación reparadas; ejecución pública pendiente.**
Cliente `1.0.1-rc.2-compat.1` instalado, ZIP SHA-256
`8cecf55d896d231d6a73dfe9ce622915bbd7f422af918aa6d1ba2ddcbadbcdef`.
Acepta de forma explícita propuestas `/1` y `/2` para el mismo perfil aprobado;
rechaza versiones desconocidas, otras capacidades y campos `/2` en paquetes `/1`.
No se desplegó NOVA ni se modificaron sus resultados. Antes: 4 FAIL/8 PASS en los
nuevos controles; después: 114/114, repetidos con fuentes congeladas. Dos builds
idénticos y ciclo portable 9/9. Se verificaron 134 archivos de la base congelada;
el único archivo de producto cambiado en ese paquete es CompanionConnection.cs.
El primer ciclo portable falló por escoger el Python de Codex en vez del venv ya
configurado; se preserva el fallo, sin instalar dependencias para ocultarlo.

`status` real completó el pendiente ya aprobado y otra consulta renovó y guardó
la credencial: sesión cifrada presente, pendiente retirado, cero grants nuevos.
Sólo se actualizó el lanzador NOVA. Tras la confirmación humana de arranque, la
comprobación encontró cero procesos Companion y sólo registros antiguos. No hay
un nuevo intento del lanzador ni una tarea recibida acreditados. Se conserva esa
divergencia; la confirmación humana no sustituye el PID/recibo. No se repitió el
arranque automático rechazado. Recibos `.playwright/companion-compat-20261101/`.

**R4 — integración y negativo real PASS; aceptación durable BLOQUEADA.**
Siete contratos con HTTP de QA y PostgreSQL sintético pasan, sin omitidos:
cuentas/biblioteca, PKCE, renovación, revocación y canales/schema JSON coherentes.
Las fuentes MCP inspeccionadas coinciden con el backend publicado `4cb104b`.
La cuenta secundaria abre su biblioteca real de cuatro prompts; el conector de
la principal rechaza un ID activo de esa biblioteca con RESOURCE_UNAVAILABLE.
La conexión vuelve a aceptar una consulta nueva tras más de quince minutos,
sin consentimiento interactivo; no se capturó su intercambio de refresh, por lo
que esa continuidad no se presenta como prueba directa de rotación del token.

Al reabrir ChatGPT, los resultados de búsqueda/lectura/compilación previamente
visibles aparecen vacíos y después «This response couldn’t load». Se reprodujo
también con una respuesta sin herramientas en ese chat y con otra en un **chat
nuevo que nunca usó MCP**. El MCP no es necesario para reproducir el fallo;
la causa exacta de recuperación/renderizado permanece desconocida. No se cambia
JSON, SDK o autoridad para ocultarlo, ni se envía a soporte. El negativo web de
aislamiento tampoco obtiene una respuesta final durable. Recibos y capturas:
`.playwright/mcp-acceptance-20261101/`. Falta aceptación pública conjunta y
revocación/renovación del cliente web final; no se revocaron sus permisos reales.

**R2 y correo — no cerrados por este delta.** La calificación NOVA paralela sigue
fallida y no se integró. La UI de acceso fue publicada por su propietario como
`dpl_8rQ46btmN7ztTNwTB58XKzHFnaxV`, sobre el backend `4cb104b`, sin cambios de
NOVA/Neon/secretos. El usuario descartó Brevo y después Mailjet en aquel trabajo;
el propietario de correo preparó Elastic Email y el alta externa está pendiente.
No se ha configurado ni verificado ese transporte. Alta y recuperación reales siguen sin
entrega acreditada; no se repitió el envío antes rechazado ni se habilitó un
transporte sin configurar. Son propietarios y recibos separados, no un candidato
completo certificado. Se mantiene **NO-GO** y no hay tag estable.

## Comprobación de horarios y cierres pendientes — 01/11/2026

**PASS local — horarios de doce horas.** Se centralizó la presentación en
`components/ui/dateTime.ts`, sin cambiar ISO, zona horaria, persistencia ni
contratos. Actividad, historial, versiones, publicaciones, documentos y
vencimientos usan 1–12 con a. m./p. m. Siete casos de límites y 87 pruebas de
interfaz relacionadas pasaron; TypeScript y build Vite terminaron correctamente.
El navegador mostró medianoche, mediodía y tarde, además del componente real
HistoryResult con datos sintéticos. No es evidencia de UI publicada: estos
cambios permanecen locales y el árbol incluye trabajo NOVA de otra evaluación.
Recibo: `.playwright/closure-12h-20261101/verification.json`.

**R1 — FAIL observado, sin atribuirlo al lanzador.** El acceso NOVA apunta a
PowerShell y al paquete `1.0.1-rc.2-4d00491f6c87`, sin ExecutionPolicy Bypass.
El último registro es histórico: RUNTIME_EXITED, código 2, cero trabajos. En el
slot configurado inicialmente no existía `session.dpapi` ni `pending-pair.dpapi`.
La consulta `status` falló. Después de confirmación expresa, se revocó únicamente
JUAN-P-TORRES que vencía el 1/12/2026 a las 10:53:21 a. m. y se aprobó su
sustituto limitado, sin cambiar los otros permisos. `pair` reprodujo
`LOCAL_PROFILE_WRONGPROTOCOL`: el cliente exige `quickprompt-local-proposal/2`
y el servidor del commit público conserva `/1`. `pending-pair.dpapi` existe;
`session.dpapi` no. No repetir la delegación: primero alinear versiones y luego
reanudar la vinculación pendiente, conservando validación estricta. No se ejecutó
`run` ni se usó DMR. Faltan petición pública, resultado, cancelación y reconexión.
Health Render y descubrimiento OAuth Vercel devuelven HTTP 200; eso no prueba
la autorización operativa del Companion.

**R3/R5 — correo BLOQUEADO.** La web real mantiene deshabilitado el alta por
correo. La credencial M2M local sigue cifrada; no se subió a Render. La revisión
automática rechazó la nueva prueba de envío con «blocked by policy», sin detalle:
el proceso no arrancó y no hay resultado nuevo del proveedor ni envío acreditado.
No se repitió por otra vía. El último rechazo documentado del proveedor sigue
siendo SMTP no activado, no un nuevo resultado de esta comprobación. Los
formularios/rutas de registro y recuperación se trabajan por separado; no
confundirlos con entrega real de correos o configuración del binario público.

**R2 — FAIL en candidato paralelo, sin integrar.** Su propietario informó que la
calificación ampliada de 28 terminó fallida: resultado independiente preliminar
18/28, con cinco fallos técnicos y cinco semánticos. No es la campaña final de
20 casos ni reemplaza su umbral 18/20. Se conserva DMR reservado para esa
evaluación. El candidato no se integra ni publica; la fidelidad sigue abierta.

**R4 — tres controles reales PASS, aceptación final parcial.** ChatGPT web
ejecutó búsqueda (consulta vacía, límite 2), lectura exacta de revisión 1 y
compilación sin variables. Mostró títulos/revisiones y respuestas completas;
las etapas visibles acreditan las llamadas. Tiempos mostrados por ChatGPT:
7/11/10 s, no latencias HTTP medidas. La consulta directa al conector confirmó
que la compilación conserva literalmente la plantilla. No se creó, editó ni
ejecutó ningún prompt. No se reprodujo el fallo de visualización en estos tres
casos; eso no identifica su causa anterior ni acredita universalidad. Recibo y
capturas: `.playwright/closure-12h-20261101/chatgpt-verification.json`.
Faltan renovar/revocar, aislamiento web con dos cuentas y repetir la aceptación
en el candidato final publicado. La versión pública no incluye estos cambios RC2.

Se conservan los controles R1–R6 como único checklist comercial. Este trabajo no
crea commit, tag estable ni despliegue de código y no cambia el veredicto NO-GO.

## Traslado Render → Neon cerrado — 01/11/2026

**La base completa está activa en Neon**. Render usa el build existente del mismo
commit `4cb104bfb874004dd6274e1aef3c9537a834cb3b`, Live en
`dep-dbjpcm142hec73dnc6d0`. Sólo cambiaron las conexiones Accounts/Executions;
ambos roles tienen sesiones en Neon y cero en la fuente. No cambió el origen web,
Google, las claves ni el código. La fuente se conserva, pero no se debe volver a
ella automáticamente después de las nuevas escrituras de Neon.
El origen público sigue pasando por el Vercel existente; activar ingreso directo
Render y retirar ese proxy no formó parte de este cambio autorizado de base.

Aceptación real: Google abre ambas cuentas; la principal tiene 109 prompts
activos y la secundaria su biblioteca aislada de 4. MCP conectado en Codex busca,
lee y compila una plantilla de tres variables; rechaza un ID activo de la otra
cuenta. Health HTTPS: Vercel 200/0,366 s y Render 200/0,240 s. Postcambio:
34 tablas heredadas con huellas iguales, secuencias iguales, historial con la
migración aditiva esperada; 36 tablas/32 migraciones finales y 23 FORCE RLS.
Sin actor, el rol de aplicación ve cero prompts y no puede leer el historial de
migración. Los tres roles limitados no tienen autoridad elevada; la base vacía
retenida no admite conexiones de esos roles. Cero lectores temporales residuales.

La copia nativa se ejecutó una vez. El rechazo automático posterior fue una
comparación textual de 12 expresiones normalizadas por PostgreSQL; la restauración
sí terminó y no llegó al timeout. La equivalencia se comprobó mediante reparsing
en transacción revertida y revisión separada de roles/comandos/permisividad.
La continuación verificó esa misma copia, sin repetir el respaldo. Se conservan
FAIL y recibos de recuperación. El reanudo inicial de Render conservó variables
antiguas; se corrigió usando Save and deploy sobre el build existente. Se
sincronizaron los dos registros OAuth surgidos de aquella validación y se volvió
a comprobar la igualdad de los datos antes de cerrar.

El [acta del traslado](../../deploy/cloud/TRANSFER.md) y las evidencias
`.playwright/render-neon-20261101/` detallan límites y comandos. Se retiene el
respaldo inicial cifrado con DPAPI y ACL privada; los pgpass/dumps temporales se
eliminaron. No sustituye un respaldo actualizado de Neon, portátil y recurrente.
La normalización del catálogo debe integrarse antes de reutilizar el ejecutor
para otro traslado; la continuación operativa no equivale a reparar ese CLI.

**No es todavía GO comercial**: siguen los controles R1–R6 restantes. Este
trabajo no publica NOVA/RC2, no acredita una nueva campaña semántica, Companion,
CRUD completo o UI de ChatGPT web. El registro/recuperación por correo siguen
deshabilitados en la web real. No se creó un tag estable.

## Historial de preparación y primer intento: Render + Neon — 01/11/2026

El usuario aceptó esta recomendación con presupuesto $0: **un despliegue de la
aplicación completa en Render Free y una base Neon Free**. Esta decisión permite
dos proveedores de alojamiento; no reactiva Netlify/Vercel como frontend ni
autoriza un plan de pago. Firebase se conserva como historial de la evaluación.
Se mantiene R1–R6 como único checklist y la separación de dominio, IA, Companion
y conectores. No se retiran datos, permisos ni servicios existentes durante la preparación.

Observado ahora en Render: `quickprompt-nova-jpt`, servicio
`srv-dbgppfu7bikc73b93oog`, Docker **Free**, Ohio, deploy **Live** del commit
`4cb104bfb874004dd6274e1aef3c9537a834cb3b`. Auto-deploy está desactivado, contexto
Docker en la raíz y Dockerfile `deploy/cloud/Dockerfile`. Es estado del proveedor,
no aceptación de los cambios locales posteriores. No se solicitó otro deploy.

Neon: acceso con la cuenta de infraestructura `jptorres.software@gmail.com` y
organización QuickPrompt. El formulario revisado fija `quickprompt-cloud`,
PostgreSQL 18, AWS Ohio y base `quickprompt_cloud`, sin servicios adicionales.
El usuario autorizó crear esta base gratuita y guardar sus nuevas credenciales
fuera de Git en la carpeta privada `quickprompt-render-neon-20261101` bajo el
directorio local QuickPromptNova/cloud-pilot, con ACL de usuario/SYSTEM y sin
cifrado propio de archivo. Se creó el proyecto **`square-voice-62672825`**, rama
`br-holy-dew-b54kyz8l`, endpoint directo
`ep-twilight-morning-b5djqken.c-7.us-east-2.aws.neon.tech`. La consola confirma Free,
PostgreSQL 18 y rama sin vencimiento. La conexión real verifica TLS `VerifyFull`;
el servidor devuelve versión 18.6. La credencial está en el destino privado
autorizado, sin publicarse en chat, Git, imagen o Render.

El preparador PostgreSQL existente admite ahora endpoints AWS directos de Neon
fijados por host e ID; conserva Render, VerifyFull, rechazo de poolers/opciones URL
y roles limitados sin BYPASSRLS. Baseline **30 PASS**; nuevo caso Neon rechazado
antes del cambio; después **45 PASS**, sin conexiones remotas ni escrituras SQL.
Son contratos sintéticos, no un PASS de migración o de aislamiento Neon real.
Recibo: `.playwright/render-neon-20261101/bootstrap-contracts.json`.

Inicialización real Neon: **PASS**, tres roles SQL limitados, cinco esquemas,
32 migraciones y 23 tablas con FORCE RLS; cero usuarios. El rol de aplicación
recibe `42501` al intentar leer el historial de migraciones, como exige su
frontera. El primer intento se detuvo después de crear roles/esquemas por falta
de `dotnet-ef`. Se añadió el manifiesto local fijado a 10.0.14, se restauró la
herramienta y se reanudaron sólo migraciones con las credenciales existentes.
El helper comprueba ahora esa herramienta antes de crear roles. No se borró,
recreó ni adoptó la base tras el fallo.

Regresión TLS real: **2 controles PASS** en transacciones de sólo lectura.
`pg_stat_ssl` devuelve false detrás del proxy Neon; se conserva como
`postgresBackendTls`, separado del `tls` del cliente abierto con VerifyFull y
sin callbacks personalizados. Un callback incluso no-op impide certificar el
bootstrap. No se relaja certificado, hostname ni TLS para aceptar ese resultado.
Recibos: `neon-real-probe.json`, `real-transport-regression.json` e
`initialized-boundary.json` del mismo directorio de evidencia.

Ingreso directo preparado, **no activado**: perfil optativo `RenderEdge` limitado
al hostname asignado `*.onrender.com`, metadata del servicio y puerto Render
coincidentes, sin confianza en cabeceras Forwarded. La suite de ingreso en Release
obtuvo **120 PASS / 0 FAIL / 0 SKIP** (25 s), incluidos perfiles anteriores.
Es TestServer con certificados sintéticos; no prueba HTTPS, cookies ni MCP en
Render. La receta retirada `RenderFreeEdgeOnly` continúa rechazada. El trabajo
NOVA del otro carril no se incluye ni se publica automáticamente.

El usuario fijó el alcance: **base completa**, conservando cuentas, bibliotecas
y permisos. La respuesta no autorizó ejecutar la copia ni cambiar producción.
El [plan de traslado](../../deploy/cloud/TRANSFER.md) restaura primero una base
aislada, conserva origen/claves y propone cambiar sólo dos conexiones de Render.
Ensayo real con datos ficticios: respaldo/restauración nativos, catálogo/ACL,
cifrado DPAPI, promoción y RLS entre dos propietarios **PASS**; nueve guardas
offline **PASS**. El locale original de Render se comprobó en Neon mediante DDL
revertido. Se conservaron los fallos del arnés; no hubo copia privada, suspensión
de Render ni cambio de conexiones. Falta autorización específica y aceptación
productiva. No hay otra meta de cierre creada.

Actualización posterior: el usuario autorizó una ejecución del plan `ba346082…`.
Render se suspendió y drenó; el control de trabajos falló con SQLSTATE 42883
porque usaba ordinales frente a estados persistidos como texto. **FAIL sin copia**;
lector temporal retirado, ambos catálogos intactos, cero lectores residuales.
Se recuperó el servicio con la fuente y commit originales: **Live**, `/health`
**200 / 0,38 s**. Ninguna conexión cambió. Se corrigió la consulta según el
contrato EF y se ejecutó de nuevo el ensayo nativo completo: seis controles PASS,
incluido el negativo Queued/Running, más nueve guardas offline PASS. Plan nuevo
de un uso `30b0328ac1e5c1b320db355034e301611e919031041960061a7da5f3cd1c1de7`,
pendiente de autorización; no se reutiliza el plan consumido. No hay GO comercial.

Pendientes R5/R6: aceptar el ingreso HTTPS directo en el proveedor real, medir
consumo de cola/Companion frente a cuotas,
preparar respaldo/migración y rollback antes de copiar datos o cambiar producción.
La cuenta administrativa Neon no debe usarse como Accounts/Executions: hereda
`neon_superuser`, que omite RLS. No se amplían permisos para facilitar el traslado.

## Historial: configuración Firebase con presupuesto $0 — 01/11/2026

El usuario cambió el destino a Firebase y ratificó $0 estricto. Se conserva R1–R6
y la oferta existente: el cambio de proveedor no cierra ninguno de sus controles.
La [evaluación vigente](../research/CLOUD_FREE_REVIEW_20261101.md) comprueba que
Spark no aloja el contenedor .NET y que su PostgreSQL SQL Connect es temporal.
Cloud Run/Cloud SQL en el mismo proyecto conservarían la arquitectura, pero
requieren facturación y no tienen coste cero permanente demostrado. No se activó
Blaze ni se vinculó facturación. Publicar sólo React o reescribir cuentas/datos
no constituye la aceptación solicitada del MVP.

El proxy Hosting filtra las cookies actuales salvo `__session`; por ello tampoco
se generó una receta de rewrites que se presente como funcional sin comprobarla.
Se mantiene NO-GO de R5 y aceptación pública. La meta Firebase queda BLOQUEADA
tras la auditoría descrita debajo; reutiliza R1–R6 como único registro. Se creó por la consola web el
proyecto exclusivo **QuickPrompt Web**, ID **`quickprompt-web-54498`**, en la cuenta
de infraestructura `jptorres.software@gmail.com`. La consola confirmó el proyecto
listo y el plan **Spark — No-cost ($0/month)**. Gemini y Analytics se desmarcaron
en el asistente de creación. No se vinculó facturación ni se copiaron datos,
secretos o bibliotecas; `cjc-informe-digital` quedó intacto.

| Configuración Firebase | Resultado observado | Qué falta para cerrar |
| --- | --- | --- |
| Proyecto exclusivo y presupuesto | PASS: proyecto creado; consola Spark $0 | Conservar el límite; no activar Blaze. |
| Alojamiento de la aplicación completa | BLOQUEADO: App Hosting muestra «requires a billing account (Blaze)» | Resolver una ruta compatible con el presupuesto y la arquitectura antes de desplegar. Cloud Run también requiere facturación según documentación oficial. |
| PostgreSQL persistente | BLOQUEADO documental: SQL Connect Spark ofrece 90 días de prueba | Un destino durable autorizado; no migrar a una base con caducidad ni sustituir por Firestore. |
| Dominio, Google, usuarios y MCP | PENDIENTE en Firebase: sin backend/origen operativo | Fijar origen y validar contratos, callbacks, cookies, dos cuentas y permisos en el mismo candidato. |
| NOVA/Companion y aceptación | PENDIENTE público | Mantener los controles R1–R6; crear el proyecto no prueba sus recorridos. |

[Consola del proyecto](https://console.firebase.google.com/project/quickprompt-web-54498/overview).
Evidencia local: `.playwright/firebase-configuration-20261101/PROJECT_RECEIPT.json`,
`project-spark.png` y `app-hosting-billing-gate.png`. No se registró una app SDK,
activó una base, desplegó una web estática ni añadió `firebase.json` para aparentar
una configuración completa. No se modificó código de producto en este avance.

El plugin oficial está instalado y la CLI 15.34.2/Node 22.19.0 comprobada, pero
`login:list` continúa sin cuentas autorizadas. El intercambio CLI anteriormente
rechazado no se repitió. El archivo privado de código de un solo uso fue eliminado
manualmente por el usuario; `Test-Path` confirmó su ausencia el 01/11/2026, sin
releer contenido. Recibo: `.playwright/firebase-access-20261101/inventory.json`.
La meta anterior de Railway fue retirada del objetivo activo; se conserva abajo
como auditoría histórica, no como proveedor que deba seguir configurándose.

### Dependencias de configuración comprobadas en el código — 01/11/2026

La continuación inspeccionó los consumidores reales antes de registrar apps o
activar servicios. Es análisis de contratos, no aceptación ejecutada en Firebase:

- `apps/web/src/services/accounts.ts` obtiene CSRF de `/account/csrf` y envía
  mutaciones con `credentials: 'same-origin'`; `api.ts` compone rutas relativas.
  Un dominio Hosting con sólo React no alcanza las cuentas actuales por sí solo.
- `backend/QuickPrompt.Cloud/AccountHost.cs` requiere dos conexiones limitadas a
  la misma base PostgreSQL, configura ASP.NET Identity y sus cookies propias.
  Firebase Authentication no sustituye esa identidad sin trabajo de integración
  y cambio de contratos fuera del cierre autorizado.
- `AccountHosting.cs` exige `SSL Mode=VerifyFull` en ambas conexiones públicas y
  valida origen/ingreso explícitos. No habilitar CORS genérico ni rebajar TLS para
  conectar una web separada.
- `DelegationModule.cs` deriva issuer y recursos del origen; `AccountMcpModule.cs`
  publica `/mcp` protegido. Registrar el proyecto o una app SDK no crea ese servidor.

Secuencia pendiente para R5 y después R1/R3/R4/R6: (1) un runtime autorizado para
la imagen .NET y PostgreSQL durable, con coste compatible; (2) un origen HTTPS e
ingreso comprobado; (3) roles/RLS, custodia de claves y operación de respaldo;
(4) Google `/signin-google`, issuer/callback MCP y correo sobre ese origen;
(5) aceptación con dos cuentas, Companion y ChatGPT en el candidato exacto.
No hay un proceso remoto de despliegue en curso que deba esperarse. La barrera
actual es capacidad del plan y presupuesto, no el archivo de código ya eliminado,
un JSON de Firebase faltante o la ausencia de una clave SDK. No se repite OAuth CLI
para intentar resolverla. Recibo de esta inspección con hashes de los consumidores:
`.playwright/firebase-configuration-20261101/CONTRACT_AUDIT.json`.

### Cierre de la auditoría de bloqueo Firebase — 01/11/2026

La misma incompatibilidad entre oferta completa, Spark y $0 persiste en tres
turnos consecutivos de esta meta: creación y comprobación del plan, inspección de
consumidores y revalidación del bloqueo. En el tercer turno la consola del proyecto
volvió a mostrar **Spark — No-cost ($0/month)** y App Hosting exigió una cuenta de
facturación **Blaze**. Los hashes de los seis consumidores inspeccionados coinciden
con el recibo anterior. No se trata de tres fallos del producto ni de un servicio
que esté arrancando; no se modifica NOVA por esta limitación de alojamiento.

La configuración entregada es parcial: proyecto exclusivo creado y presupuesto
conservado. No hay una tarea independiente de configuración Firebase que complete
el backend actual y PostgreSQL durable en Spark. Activar Auth/Firestore, registrar
un SDK, repetir OAuth CLI o publicar sólo React no satisface los contratos. La meta
se marca **BLOQUEADA, no completada**. No se ejecuta otra continuación del mismo
aprovisionamiento sin una entrada que cambie esta condición.

Para retomar hace falta una capacidad de alojamiento y PostgreSQL durable
comprobada y autorizada dentro de $0, o una decisión explícita que cambie alguna
restricción: presupuesto/facturación, proveedor único o alcance de la arquitectura.
Ninguna alternativa se aplica automáticamente. Se conservan datos, permisos,
trabajo local y objetivo completo; R1–R6 y la aceptación pública siguen abiertos.
Evidencia: `BLOCK_AUDIT.json`, `BLOCK_AUDIT_SOURCE_CHECK.json` y
`block-audit-current.png` en `.playwright/firebase-configuration-20261101/`.

## Meta anterior bloqueada: cierre en Railway — 01/11/2026

### Auditoría de dependencias para continuar

Se conservan los resultados ejecutados y el alcance R1–R6. La misma dependencia
de cuenta Railway se encontró en el turno original y dos continuaciones; la CLI
continúa autenticada y pudo consultar el workspace, pero eso no habilita el plan.
La última observación de planes mostró prueba vencida, crédito cero, despliegues
pausados y factura pendiente. Al intentar actualizar la observación gráfica, la
ventana estaba minimizada y no pudo capturarse; no se supone resuelta la cuenta ni
se presenta aquel estado de facturación como una nueva lectura de pantalla.

| Control | Estado de cierre | Dependencia y siguiente acción habilitada |
| --- | --- | --- |
| R1 Companion | PENDIENTE público; reparación/paquete local verificados | Cuenta y origen final disponibles, conexión NOVA utilizable y petición/cancelación/reconexión reales. Un proceso idle no acredita ese recorrido. |
| R2 NOVA | FAIL histórico 9/20; revisión separada EN CURSO | El otro chat conserva el escritor/modelo y DMR. Esperar implementación integrada y campaña nueva ≥18/20; no invadir sus archivos ni sumar probes al porcentaje histórico. |
| R3 cuentas y biblioteca | PENDIENTE conjunto final | Origen definitivo y correo operativo para alta/recuperación; dos cuentas independientes sobre el mismo candidato. |
| R4 ChatGPT/MCP | PENDIENTE aceptación final | Origen/issuer/callbacks fijados y conexión delegada autorizada allí; lectura antigua del piloto no prueba renovación/revocación en Railway. |
| R5 operación y datos | BLOQUEADO | Habilitación de Railway compatible con $0. Además faltan destino/identidad/custodia y autorización recurrente de respaldo: la plantilla mantiene campos críticos nulos. No programar una copia privada usando aprobaciones puntuales consumidas. |
| R6 candidato y aceptación | PARCIAL local | Imagen y contratos comprobados; falta integrar el trabajo NOVA, congelar un corte, CI remoto y aceptación pública. No publicar todo el árbol en curso ni etiquetarlo estable. |

Decisión: **NO-GO comercial**. El bloqueo no se resuelve pagando sin autorización,
creando otro workspace para eludir facturación, cambiando el proveedor acordado,
rebajando TLS/RLS o sumando pruebas simuladas. Para retomar la ruta crítica hace
falta una señal de cuenta habilitada a $0 o una nueva decisión explícita sobre
presupuesto/destino; el otro chat continúa siendo propietario de R2. No hay un
despliegue remoto ni una ejecución de CI en curso esperando resultado en este
carril. No se creará otro roadmap para sustituir estos controles.

Tras la auditoría del tercer turno consecutivo con esa dependencia, la meta quedó
marcada **BLOQUEADA**, no completada. Se conservan su objetivo íntegro, los cambios
locales y los pendientes. Retomar cuando exista habilitación del destino dentro
del presupuesto o una decisión explícita que cambie esa restricción.

Se creó la meta solicitada para completar R1–R6 y decidir GO/NO-GO sobre un
candidato público exacto. No cambia la oferta ni los umbrales de aceptación de
abajo y no crea otro checklist paralelo. Secuencia habilitada: comprobar cuenta y
cuotas → preparar Docker/PostgreSQL con datos sintéticos → validar ingreso,
persistencia/consumo/recuperación → aceptación conjunta → cierre del candidato.
Si falla una dependencia, conservar FAIL/BLOQUEADO y continuar puntos independientes.

**R5 BLOQUEADO en la cuenta observada:** CLI oficial 5.71.0 instalada y sesión
verificada; el panel existente mostró prueba vencida, crédito $0, despliegues
pausados y factura pendiente que impide cambiar de plan. Sólo se observó la cuenta
y su proyecto existente; no se crearon recursos, realizaron pagos ni copiaron
datos/secretos a Railway. No se declara habilitado Free por su precio publicado.
La CLI autenticada no demuestra capacidad de despliegue. La
[preparación de Railway](../../deploy/cloud/README.md) identifica el manifiesto y
las fronteras de ingreso, salud, TLS PostgreSQL y claves todavía necesarias.

Avance local R5/R6 del mismo corte: la sonda con Host de plataforma devolvía 403
en GET y HEAD. Regresión previa: **10 FAIL / 9 PASS** (dos rechazos HTTP y ocho
valores nuevos ignorados). Se añadió `HealthCheckHost` opcional sólo en Public +
OriginProxy: un DNS exacto puede alcanzar únicamente GET/HEAD `/health` terminal,
sin query, Origin ajeno, sesión o rebasing. No habilita otras rutas ni cambia el
ingreso predeterminado. Después: **95 PASS / 0 FAIL / 0 SKIP** en Release, 35 s,
para `HostingHealthProbeTests`, `OriginProxyHostingTests` y `AccountHostingTests`.
Es HTTP controlado con TestServer y certificados sintéticos; no es aceptación
Railway/PostgreSQL real ni CI del candidato final.

Comando reproducible desde la raíz, usando el SDK de `global.json`:

```powershell
dotnet test backend/QuickPrompt.Accounts.Tests/QuickPrompt.Accounts.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~HostingHealthProbeTests|FullyQualifiedName~OriginProxyHostingTests|FullyQualifiedName~AccountHostingTests'
```

Recibos locales: `.playwright/railway-closure-20261101/health-probe/` conserva
`health-probe-before.trx` y `hosting-after.trx`. El manifiesto
`deploy/cloud/railway.json` pasó `Test-Json` contra el esquema oficial descargado;
su hash y el del esquema están en `manifest-validation.json` del mismo directorio
raíz de evidencia. La primera herramienta de validación Python carecía de
`jsonschema`; no se instaló otra dependencia y se usó PowerShell existente.
Enlaces de 18 documentos vigentes y dos guías adicionales: PASS. Sin publicación,
migración, inferencia, envío de correo ni modificación de permisos cloud.

## Avance de conexión y configuración — 01/11/2026

Continuación R5/R6, carga portable de claves: se cerró en código la alternativa
explícita para materializar el par PEM desde variables privadas en el contenedor.
Conserva archivos montados como otra vía excluyente, bytes estables, directorios
0700/archivos 0600 y validación criptográfica en .NET. No genera claves en arranque
ni altera permisos de MCP/Companion. Parejas incompletas, vacías, mal codificadas o
mezcladas se rechazan antes de ejecutar el host. Las variables base64 consumidas
se retiran del entorno del proceso hijo.

Evidencia: el primer harness no pasó el arranque básico y no se usa para puntuar
seguridad (causa de ese intento sin confirmar). Con la copia del script a tmpfs,
permisos de ejecución explícitos y comprobación de la causa de rechazo, el
contraste anterior al producto dio **10 PASS/9 FAIL**; después, **19 PASS/0 FAIL**.
Docker build real de React/.NET PASS, y el harness sobre el entrypoint incluido en
la imagen volvió a pasar los 19 casos más el guard de runtime no-root, web/backend
presentes y ausencia de archivos PEM/PFX en `/app`. Imagen local:
`sha256:e3df2a50e1ebdb272bfe46b45c7cd7212d022b7b8daa4f33622f75297b281f40`.
Persisten los avisos conocidos del chunk web de 537,10 kB.

`DelegationKeyFileTests`: **4 PASS/0 FAIL/0 SKIP**, Release, 2 s; firma, cifrado e
identidad estables con RSA sintética, rechazo de clave pública/débil/ausente.
Fronteras estáticas y enlaces documentales PASS. El workflow existente incorpora
el job `cloud-container`; no se ejecutó CI remoto ni se hizo push. Recibos y
comandos en `.playwright/railway-closure-20261101/entrypoint/` y en la
[guía del contenedor](../../deploy/cloud/README.md). Es preparación verificada
localmente: todavía faltan custodia/configuración remota autorizadas y aceptación
de cuentas, claves y delegación en el entorno final. No se cargaron claves o datos
reales a Railway ni se modificó DMR, el modelo o su perfil durante este trabajo.

**NO-GO comercial conservado.** Este corte no altera los umbrales R1–R6 ni la
campaña semántica. El equipo de NOVA trabaja R2 por separado; aquí no se ejecutaron
inferencias ni se publicaron cambios.

- **R1, reparación e instalación parcial:** el orden de persistencia de tokens
  consumidos y la pérdida del token inicial tras un fallo de perfil tienen
  regresiones reproducidas. Vinculación pendiente cifrada y sesión validada son
  estados distintos; el perfil sigue siendo obligatorio para trabajar. Suite
  Companion: 102 PASS; export reproducible de fuentes congeladas e instalación
  local realizadas; ciclo portable 9 PASS. El acceso de Prompts alcanzó espera
  real de trabajos. NOVA no completó todavía la conexión/petición pública; no se
  confunde autorización web con credencial privada o proceso disponible.
- **R3/R5, observación pública:** `/account/options` respondió 200 en 446 ms con
  `google=true`, `email=false` y `mailDelivery=unconfigured`. Google permitió
  acceder a la biblioteca principal. Brevo mantiene el aviso de verificación
  telefónica. No se enviaron nuevos correos ni mensajes de soporte ni se habilitó
  alta/recuperación sin entrega comprobada.
- **R5, transporte de datos:** conexión real con el rol limitado existente,
  transacción read-only, VerifyFull y TLS 1.3/256 bits; PostgreSQL 18.6, sin
  SUPERUSER/BYPASSRLS. Apertura 866 ms, observación completa 1108 ms. Sólo catálogo
  y metadatos de la propia conexión; sin filas de negocio, DDL o respaldo. Esto
  no verifica cifrado en reposo, retención ni recuperación recurrente.
- **R5, alojamiento y respaldo:** un proveedor/$0 conservados, sin destino
  persistente gratuito validado. Las aprobaciones puntuales de captura no se
  convirtieron en permiso recurrente; falta fijar destino/custodia/identidad y
  ejecutar la operación autorizada completa.
- **R6, configuración reproducible:** se restauró el inventario ausente
  `deploy/cloud/.env.template`, con valores de ejemplo y las claves M2M del
  candidato; se corrigió una referencia documental inexistente. Tres errores
  de enlaces reproducidos, después 0 en las dos guías de despliegue y PASS del
  chequeo documental vigente. No cambia configuración pública ni incluye secretos.

Artefacto Companion de este corte: SHA-256
`4d00491f6c87860c194fb2a8f41709ba4eb0672e0830b4d02629f63da7190faa`.
Comandos/recibos locales RC2:
`.playwright/priority-closure-20261101/refresh-continuity/` y
`database-transport/`. No es un SHA nuevo publicado ni aceptación final de la web.

Adicional local del mismo corte: el cliente de aprobación descartaba Problem
Details públicos; dos regresiones FAIL lo reprodujeron. Se reutiliza ahora el
lector de errores del cliente HTTP común, sin tratar otros errores OAuth como
rechazo confirmado ni reintentar decisiones. Web completo: **505 PASS**, build
PASS (warning existente de chunk de 537,10 kB). El backend publica ahora sólo un
aviso cerrado para el límite de conexiones: conserva HTTP 400, máximo cinco y
filtros/locks por propietario, compartiendo la constante entre Companion y MCP.
No expone otros mensajes de excepción. Mapper: **14 PASS**; el gate SQL de cinco
conexiones y la comprobación del aviso en producción no se ejecutaron de nuevo.
El permiso de NOVA creado por el primer intento fallido dejó cinco activos; se
preparó revocar sólo ese permiso con confirmación específica del usuario. No se
subió la cuota ni se revocaron permisos MCP u otros perfiles para ocultarlo.

La [comparación ampliada sin servidor propio](../research/CLOUD_FREE_REVIEW_20261101.md)
conserva $0. Northflank es la mayor oferta recurrente examinada, pero mantiene un
incidente abierto. Railway aporta $1 mensual después del trial; Google ofrece
crédito temporal, sujeto a elegibilidad. Ninguno está certificado o migrado por
esta investigación; no se insiste en contratar un servidor como si ya existiera.

## Subchecklists de aceptación funcional del MVP — 01/11/2026

Estos subcontroles concretan R1–R6; no crean otro roadmap ni añaden funciones.
Por petición del usuario, la salida exige utilidad comprobada, no perfección del
modelo. Los resultados anteriores se conservan con su fecha y alcance: esta
revisión de criterios no convierte una prueba fallida o no ejecutada en PASS.

### Regla sencilla de salida

| Control | Mínimo para cerrar |
| --- | --- |
| Seguridad, aislamiento, integridad y aprobación | 100% de los controles críticos ejecutados; cero acceso cruzado, efectos sin aprobación, pérdida o corrupción de datos |
| Recorridos básicos ofrecidos | Cada recorrido obligatorio de abajo termina correctamente al menos una vez en el candidato público; una función ausente no se compensa con otras |
| Calidad de NOVA con IA real | Al menos 18/20 tareas correctas (90%), con 10 conocidas y 10 nuevas; al menos 9/10 en cada grupo y 80% en cada familia |
| Errores recuperables | Conserva el trabajo, comunica el fallo, devuelve control y permite continuar; sin repetición de efectos ni espera ilimitada |
| Detalles menores | Pueden quedar pendientes si no bloquean la tarea, no cambian requisitos y tienen impacto/alternativa documentados |

Un fallo de estilo o una sugerencia mediocre no equivale a un fallo de seguridad.
Una sugerencia incorrecta cuenta como fallo de calidad aunque el usuario la
corrija después. El contenido no aplicado debe conservarse separado del contrato.
Una acción o persistencia no autorizada bloquea la salida aunque el total sea 99%.

### 1. NOVA y cumplimiento de la petición — R2

- [ ] Crear conserva literalmente tarea, variables, requisitos y referencias confirmados; no vuelve a redactarlos con IA.
- [ ] Mejorar/Adaptar propone un cambio útil, conserva los requisitos y muestra la propuesta antes de aplicarla.
- [ ] La selección de prompts respeta la biblioteca/colección elegida; una ambigüedad real permite elegir o aclarar, sin actuar sobre otro prompt.
- [ ] Confirmar y guardar conserva exactamente el contenido elegido; aclarar, revisar o cancelar no autoriza guardar/ejecutar.
- [ ] La campaña real de 20 tareas alcanza el umbral anterior, con primera propuesta, aclaraciones, resultado y latencia conservados por caso.

Pruebas necesarias: regresiones de creación/contratos, proveedor Qwen3:8b/DMR
real y recorrido web hasta lectura posterior de lo guardado. El éxito estructural
de un JSON, un modelo listado o una composición corregida no acredita calidad.

Diagnóstico adicional 01/11: retirar sólo la máscara nativa produjo una respuesta
rechazada por contrato; se detuvo tras dos llamadas, con seis no ejecutadas.
El modo Core `prompted` fallaba antes del modelo: el adaptador DMR rechazaba
`json_object`, formato que usa Pydantic AI. Tres regresiones reprodujeron el fallo;
la corrección en Adaptive-AI-Core pasó 18 pruebas DMR y la suite Python de 79.
Después, cuatro primeras respuestas reales (dos conocidas/dos holdout) validaron
JSON pero copiaron la tarea sin acortarla: **0/4 mejoras útiles**, diagnóstico
parcial y no nueva campaña global. No se cambió el decodificador de NOVA ni se
cerró fidelidad. Recibos RC2: `authoring-decoding-probe-01/`,
`authoring-schema-visibility-probe-01/` y
`authoring-schema-visibility-probe-02/SEMANTIC_REVIEW.json`, bajo
`.playwright/priority-closure-20261101/`. Este contraste descarta atribuir la copia
únicamente a la máscara nativa; no demuestra incapacidad general de Qwen.

R6, mismo incremento: dos exportaciones offline independientes del Companion
incluyen el SDK corregido y son idénticas (ZIP SHA-256
`668799e5fbc5131ef869cce146bc3e2cdec4b7b782582ab666e42e4d3b532b72`).
El SDK extraído fuera del repositorio pasó sus cuatro regresiones de JSON mode,
con manifiesto íntegro antes/después. Recibo RC2:
`.playwright/priority-closure-20261101/core-json-mode/PACKAGE_RECEIPT.json`.
Es artefacto local `1.0.1-rc.2`, no instalado/publicado; no acredita arranque
autenticado, instalación limpia, actualización/desinstalación o aceptación pública.

### 2. Companion disponible y controlable — R1

- [ ] Desde el acceso instalado conecta a la cuenta y completa una petición enviada desde la web pública.
- [ ] Después de quedar inactivo vuelve a recibir y completar otra petición sin reinicio manual.
- [ ] Cancelar devuelve el control y una respuesta tardía no se aplica ni guarda.
- [ ] Detener retira la disponibilidad; desconectar y reconectar muestra el estado vigente sin repetir la tarea.

Pruebas necesarias: instalación real, proceso/conexión/run correlacionados y
resultado visible; los tests .NET/DMR y el doctor son apoyos, no sustitutos.

### 3. Alojamiento y acceso Google — R5

- [ ] La URL HTTPS definitiva abre web, API, callbacks de Google y MCP sin advertencias de certificado.
- [ ] Los datos sobreviven a reiniciar o redesplegar el backend; la base tiene continuidad compatible con la oferta comercial.
- [ ] Google permite el acceso del público ofrecido con origen/callback correctos, privacidad y condiciones accesibles.
- [ ] Recursos, cuotas y condiciones del alojamiento real respetan el presupuesto $0; no depender de un plan que excluya el uso comercial ofrecido.

Pruebas necesarias: entorno realmente contratado/configurado, acceso real,
persistencia posterior a reinicio y revisión de las condiciones vigentes.

### 4. Protección y recuperación de datos — R5

- [ ] Un respaldo automático autorizado termina cifrado, con recibo, hash y clave dedicada bajo custodia definida.
- [ ] La política de retención se aplica al destino aprobado y un fallo no borra la última copia recuperable.
- [ ] Una restauración autorizada recupera los datos y claves necesarios; se comprueban permisos, borrados y revocaciones antes de reabrir.
- [ ] La rotación conserva acceso a las claves todavía necesarias y rechaza una clave o identidad incorrecta.

Pruebas necesarias: ejecución y restauración reales, además de controles
sintéticos negativos. No reutilizar autorizaciones puntuales consumidas.

### 5. ChatGPT y MCP multiusuario — R4

- [ ] En ChatGPT buscar, leer y compilar devuelve el prompt/revisión correctos y solicita los valores faltantes.
- [ ] Crear guarda una plantilla nueva exactamente en la biblioteca autorizada; repetir la misma operación no crea duplicados.
- [ ] Dos cuentas independientes no pueden leer/escribir la biblioteca de la otra ni exceder sus scopes.
- [ ] Renovar mantiene el alcance y revocar impide nuevas operaciones; errores y respuestas quedan visibles en ChatGPT.

Pruebas necesarias: ChatGPT web real con dos identidades sobre la versión final;
Codex/MCP directo y los contratos automatizados no sustituyen ese cliente.

### 6. Cuentas y biblioteca completas — R3 y correo de R5

- [ ] Crear cuenta por correo, recibir confirmación y entrar funciona; Google y cerrar sesión también.
- [ ] Recuperar contraseña entrega el enlace, permite el cambio y rechaza enlaces usados, vencidos o alterados, sin revelar cuentas existentes.
- [ ] Crear, leer, editar, duplicar y borrar un recurso de auditoría funciona y persiste; las variables se detectan, completan y compilan correctamente.
- [ ] Colecciones aparecen en inicio y detalle; historial/versiones y resultados corresponden al prompt y usuario correctos.
- [ ] Compartir una versión, aceptar/importar y revocar funciona; una sesión vencida se recupera sin mezclar cuentas ni repetir escrituras.

Pruebas necesarias: dos cuentas, navegador/API y PostgreSQL reales; datos
sintéticos de auditoría y lectura posterior. No tocar prompts privados ajenos al caso.

### 7. Operación del MVP — R5

- [ ] Dos cuentas concurrentes completan los recorridos manuales; se respeta la concurrencia de inferencia configurada y sus cuotas.
- [ ] Cortar y restablecer SSE/red recupera la ejecución correcta mediante runId y secuencia, sin estados vencidos ni efectos duplicados.
- [ ] Timeout, componente desconectado y proveedor no disponible producen un error claro y devuelven control conservando el trabajo.
- [ ] Fallos críticos y respaldos fallidos emiten una alerta real al responsable operativo definido; se registran latencias y límites observados.

Esta aceptación acotada demuestra el MVP para dos cuentas y el equipo de inferencia
configurado; no es una promesa de escala ilimitada ni un SLA no acordado.

### 8. Candidato final y entrega — R6

- [ ] Un único candidato identifica commit, versión, migraciones, configuración externa y hashes; no contiene secretos.
- [ ] Build, CI y pruebas obligatorias pasan en ese corte, sin exclusiones que oculten fallos críticos.
- [ ] Instalar, actualizar y desinstalar el paquete conserva datos y permisos; el rollback compatible está comprobado.
- [ ] Se ejecuta el recorrido público conjunto y se emite GO/NO-GO con evidencias, limitaciones y pendientes menores.

Avance local 01/11: ciclo portable **9 PASS/0 FAIL** con dos ZIP reales, mismo
JSON externo, diagnósticos offline en versión anterior/candidata/rollback y
retirada de ambos payloads sintéticos. Configuración, archivo DPAPI sintético y
ACL se conservaron; el guard rechazó borrar la configuración externa. Script:
`Tests/companion/portable-lifecycle.ps1`; recibo RC2:
`.playwright/priority-closure-20261101/portable-lifecycle-verified/portable-lifecycle.json`.
Sin credenciales reales, workers autenticados ni peticiones a cuentas/proveedor.
El checkbox completo sigue pendiente: no cubre Windows limpio, renovación/revocación
real, proceso activo, CI del corte final o aceptación pública.

### Cómo puntuar las 20 tareas de NOVA

Congelar casos, fuentes, resultado esperado, modelo/perfil y límites antes de
ejecutarlos. Distribución: 8 Mejorar/Adaptar, 6 Buscar/seleccionar semánticamente y 6 Aplicar/
ejecutar; cada familia contiene mitad de casos conocidos y mitad de casos nuevos.
Crear literal y CRUD se comprueban aparte al 100%; no inflan el porcentaje de IA.
Cada caso de esta muestra debe probar la intervención del proveedor configurado;
la búsqueda literal por MCP, por sí sola, se registra como contrato funcional,
no como acierto del modelo.

Un caso es PASS si completa la petición con el recurso correcto, conserva todos
los requisitos y valores, produce el resultado útil esperado y respeta las
aprobaciones. Una aclaración necesaria sólo puede acabar en PASS al continuar
con la respuesta válida y completar la tarea. Preguntar sin necesidad, abstenerse
sin resolver, inventar datos, omitir variables o requerir corrección semántica
manual cuentan como FAIL de calidad. Un efecto no autorizado es crítico.

Se evalúa la primera propuesta útil y el recorrido completo por separado; la
corrección humana no transforma una primera propuesta fallida en correcta.
Las revisiones finales exigidas por diseño no son correcciones semánticas.
Mantener los presupuestos/reparaciones ya configurados; no reenviar hasta obtener
la respuesta deseada. Un timeout o servicio caído cuenta como fallo de la ejecución
del producto, con causa separada; no se elimina del denominador. Un arnés roto se
corrige con evidencia y conserva su intento, sin seleccionar resultados.

Un caso BLOQUEADO/NO EJECUTADO no suma PASS ni se retira de la muestra.
El 90% es el resultado observado de esa muestra, no una garantía estadística de
todas las peticiones futuras. Antes de cada nueva candidatura se fija la muestra;
no se cambian criterios después de ver respuestas.

### Registro mínimo y decisión

Por subcontrol/caso registrar: candidato, entorno, fecha, PASS/FAIL/BLOQUEADO,
esperado frente a observado, evidencia, latencia y defecto menor o crítico.
No calcular preparación global mezclando miles de unit tests con unos pocos
recorridos públicos. Las suites verdes apoyan la evidencia del flujo que cubren.

Algoritmo: ejecutar controles críticos; si alguno falla o no está comprobado,
NO-GO y continuar el trabajo independiente. Si pasan, exigir todos los recorridos
básicos y al menos 18/20 tareas de NOVA, con los mínimos por grupo/familia.
Sólo entonces GO para el alcance probado, dejando los defectos menores explícitos.
No se exige pulir indefinidamente texto, estética o posibilidades fuera de la oferta.

Los subcontroles de esta nueva matriz están pendientes de aceptación conjunta.
Las evidencias anteriores pueden vincularse si corresponden al mismo contrato y
candidato; no se marcan por inspección de código ni por esta edición documental.

### Ejecución del subchecklist NOVA — muestra real del 01/11/2026

Se fijaron20 casos antes de inferir:10 de familias conocidas y10 holdout,
repartidos en8 adaptación,6 selección y6 ejecución. Qwen3:8b/DMR real,
sin escrituras, cuentas HTTP, navegador ni Companion autenticado.
La captura técnica terminó20/20, pero la evaluación de contenido dio **9/20
correctos (45%):FAIL frente al90% exigido**. Grupos:4/10 conocidos y5/10 holdout;
familias:0/8 adaptación,4/6 selección y5/6 ejecución. Autorrevisión del agente
contra expectativas fijadas, no aceptación humana independiente o pública.

Los ocho fallos de adaptación conservan repeticiones pese a pedir reducción.
Dos búsquedas confunden título exacto con similitud funcional. Un acta convierte
una mención en tarea y contradice un responsable conocido. El test de captura
verde sólo acredita transporte; no transforma estos fallos en PASS.

Causa confirmada de contexto: la proyección al escritor omitía la petición de
edición y enviaba Shortening=false sin haber interpretado esa intención. Se
sustituyó por una orden de edición separada de los campos futuros y unknown=null,
sin keywords, cambio de acción ni efectos nuevos. Tres regresiones iniciales
FAIL; posterior focal45 PASS, backend1495 PASS/1 hardware SKIP y focal final
de aclaraciones/edición23 PASS. Se preserva el warning xUnit2031 ajeno al cambio.

El contraste real posterior con esos8 casos confirma la llegada de la orden,
pero **0/8 acortan la tarea**. El transporte se corrigió; la predicción de utilidad
no se cumplió. No sumar este subconjunto de otro build al9/20 anterior ni anunciar
un nuevo porcentaje global. La familia sigue REFACTOR_OBLIGATORIO/PARCIAL.

Dos probes diagnósticos aislados con el mismo proveedor y contratos más simples
sí acortan:el de campos omite «No añadas ejemplos» y falla fidelidad; el de documento
la conserva en ese único caso. No son un nuevo camino activo ni cierre del escritor.
Se evaluaron después dos prototipos de edición literal sin efectos: el primero
logró 4/8 adaptaciones útiles; el catálogo de fragmentos produjo tres rechazos por
solapamiento y otros resultados que no conservaron la tarea. Ambos se retiraron
del código activo; fuentes y respuestas se conservan como experimentos rechazados.
La reconstrucción y regresión tras retirarlos obtuvo 28 PASS, cero FAIL.
Un contraste pareado del escritor vigente con shortening=null y shortening=true
produjo la misma tarea sin reducción: ese indicador tampoco resuelve por sí solo
la causa. Ninguno de estos experimentos aumenta el 9/20 de la campaña congelada.
No acumular variantes de prompt o reintentos para seleccionar respuestas favorables.

Recibos en el checkout RC2:
`.playwright/priority-closure-20261101/mvp20-01/`,
`editing-order/`, `editing-order-real-01/`, `text-edit-probe-01/`,
`text-edit-catalog-probe-01/` y `text-edit-refactor/`.
Fixture:`Tests/accounts/mvp-semantic-cases.json`, SHA-256
0A758726E2ECE23A84CFDEADA78FA73EBC2FFC88923F29C2D30F86CA687F260D.
Los paquetes de creación literal exportados antes de este cambio no identifican
el nuevo delta de inferencia. No se cambió producción ni se creó un tag.

### R1/R6: supervisión del Companion corregida y export identificable — 01/11/2026

En RC2 se reprodujo un defecto nuevo: una excepción I/O del heartbeat quedaba
fuera del filtro de errores y dejaba inferencia esperando hasta su deadline.
Dos regresiones FAIL antes. El monitor ahora cancela ante cualquier excepción y
la propaga con su stack, sin publicar respuesta tardía ni reclamar otro trabajo;
el lote también se detiene y la cancelación del usuario conserva prioridad.

Suite Companion: **84 PASS, cero FAIL/omitidas**, HTTP/modelo sintéticos. No se
inició el Companion instalado, no se renovaron credenciales reales ni se cambió
el contenido de NOVA. Dos builds offline independientes produjeron ZIP idéntico
SHA-256 `92e43761fffe7d2527cdefe2fd75b26e6cc60a1ee6b73cf7424edf9010452606`,
versión 1.0.1-rc.2, label rc2-heartbeat-20261101 y 68 entradas. Quince controles
portable/doctor offline PASS, cero inicios de worker/servicios/llamadas de modelo.
No es un SO limpio ni aceptación del arranque público. No se instaló/publicó.
Recibos RC2: `.playwright/priority-closure-20261101/companion-heartbeat/`.
El ZIP es un artefacto Companion del delta local, no un RC integral con CI nueva.

### R2/R4: contrastes conservados y MCP público — 01/11/2026

Dos diagnósticos sin candidatos no califican el selector activo: tipo+título
obtuvo 8/10 consistentes (dos tipos contradicen títulos correctos); título/null
obtuvo 6/10. Ambos FAIL frente a los diez casos fijados, sin integrar. La hipótesis
de simplificar ese contrato por sí solo no cerró la frontera. No acumular estas
variantes ni cambiar el 9/20 de la campaña de producto.
Recibos RC2: `reference-criterion-probe-01/` y `reference-title-probe-01/`, dentro de
`.playwright/priority-closure-20261101/`.

MCP conectado desde Codex: buscar → leer revisión 1 → compilar tres variables
produjo exactamente la sustitución esperada, 665 caracteres y cero valores
faltantes, sin escrituras ni IA. No se expone email de identidad por la herramienta;
no se infiere otra cuenta ni aceptación de ChatGPT web, renovación/revocación o
RC2 publicado. Recibo: `mcp-current/smoke.json` en el mismo directorio RC2.

### R5: cambio de alojamiento solicitado — 01/11/2026

**Decisión posterior vigente:** una aplicación desplegable, un origen HTTPS y un
proveedor de alojamiento. React ya está incluido en la imagen .NET y Cloud monta
API/NOVA/cola/MCP; ver [contrato de arquitectura](../ARCHITECTURE.md). PostgreSQL
durable queda en el mismo proveedor con roles y TLS existentes. Se retira la
propuesta distribuida Netlify/Render/Neon como destino. No se retiró el proxy
actual ni se abrió el backend: el cambio de ingreso/origen requiere su aceptación.
Render permite reutilización técnica, pero Free no cierra la duración de la base.
Pruebas locales de fronteras de hosting RC2: 76 PASS, cero FAIL/SKIP; recibo
`single-provider-architecture/hosting-boundaries.trx` en
`.playwright/priority-closure-20261101/`. Son TestServer/contratos, sin base,
proveedor o aceptación pública; no prueban la consolidación desplegada.

Ampliación de arquitectura solicitada por el usuario: base operativa Docker,
composición tipo fábrica y adaptadores por contratos, documentada en
[arquitectura](../ARCHITECTURE.md). Se mantienen los propietarios actuales; no
se creó otro contenedor DI, microservicio o registro global de conexiones.
Hallazgos estáticos: composición local conserva proveedor Ollama implícito,
Companion admite DMR explícito y embeddings sigue ligado al protocolo/endpoint
heredado. Requiere alinear perfiles y demostrar sustitución, no eliminar módulos.
Auditoría RC2: 40 controles estáticos y 58 contratos/composición PASS; sin modelo
real ni infraestructura nueva. Recibos `factory-architecture/` en el directorio
de evidencia del 01/11.

R1 observado en este equipo: cero procesos locales coincidentes del Companion;
ambos accesos públicos referencian el payload `1.0.1-rc.1-dmr.5`, con scripts y
JSON existentes y sin errores de sintaxis. La instalación/referencia del RC2 no
se ejecutó: quedó pendiente al priorizar el análisis de fábrica solicitado.
No se inició un worker ni se leyó una credencial. Esto no determina disponibilidad
de otros equipos o del servidor.

Oracle/OCI queda retirado por decisión del usuario. Su preferencia posterior
es **cloud gratuito, sin servidor propio**. La [comparación vigente](../research/CLOUD_FREE_REVIEW_20261101.md)
propone evaluar Netlify/Render/Neon conservando .NET/PostgreSQL y presupuesto $0,
condicionada a cuotas, continuidad, confianza del proxy y consumo en reposo.
La revisión oficial posterior identificó proxy Netlify de 26 s frente a renovación
de feeds de 30 s y arranque en frío Render del orden de un minuto. Compatibilidad
pendiente; no se generó ni activó otra receta de ingreso. Es lectura de contratos,
no reproducción del fallo en Netlify. La propuesta no autoriza un traslado inmediato.
Coolify queda como alternativa autogestionada documentada, no el destino activo.
Sin cuentas, instalación, migración o cambios públicos nuevos. R5 sigue sin destino
final comprobado; las evaluaciones anteriores se conservan como histórico.

El backend público respondió /health con HTTP 200 en 11.171 ms. /account/options
por Vercel agotó 35.179 ms: no se atribuye causa ni éxito del flujo de cuentas.
El conector Vercel disponible muestra Hobby y tres proyectos, ninguno QuickPrompt;
no prueba ausencia del proyecto en otras cuentas. Oracle mostró Cloud Sign In,
requiriendo nombre de cuenta; no se obtuvo acceso a una tenancy. No se insistirá.

### R2: contraste de referencia de búsqueda retirado — 01/11/2026

El prototipo que interpreta el tipo de referencia una vez y compara títulos en
el host obtuvo 5/6 búsquedas correctas: corrigió K-S01/H-S02, pero regresó H-S01
(función en inglés interpretada como título). Incumplió el criterio congelado de
seis regresiones sin pérdida; se retiró del código activo, conservando fuentes,
peticiones y respuestas en RC2: source-reference-real-01/rejected-source y
SEMANTIC_REVIEW.json. Los siete tests controlados del contrato no demostraban la
calidad semántica. Tras retirar el prototipo, reconstrucción y regresiones del
selector original: 29 PASS, cero FAIL. No cambia la campaña global 9/20.
La separación de interpretación de referencia y evaluación de candidatos quedó
como hipótesis pendiente, no otra implementación ni un arreglo confirmado.

### Meta activa de cierre — 01/11/2026

La meta nueva se limita a las ocho prioridades acordadas y conserva R1–R6 como
único roadmap: fidelidad (R2), Companion instalado (R1), alojamiento/Google,
protección de datos y operación (R5), ChatGPT/MCP (R4), recorrido multiusuario
(R3) y entrega final (R6). Sin funciones nuevas, cambio de modelo ni gasto.

Decisión de producto confirmada: Crear conserva literalmente el contrato
establecido; sólo Mejorar/Adaptar solicita reescritura de IA. La revisión final
y la aprobación de efectos siguen separadas. Implementación en el checkout
QuickPrompt-Web-RC2, base bbefd38, sin publicar ni modificar el código del árbol
original. Regresión focal ampliada: 31 PASS (creación exacta y escritor explícito);
UI focal: 7 PASS. Primera suite amplia: 1366 PASS/126 FAIL/1 SKIP; se conserva
íntegra y se migran sólo las expectativas afectadas por la acción autorizada.
Resultado posterior del delta autorizado: backend Release 1492 PASS/0 FAIL/1
SKIP de hardware en la suite ordinaria; web 502 PASS/0 FAIL y build PASS;
Cloud Release build PASS sin errores ni warnings; 90 pruebas controladas de
fronteras Accounts/MCP/correo PASS, sin PostgreSQL. Vista real del componente
de revisión PASS en navegador con datos sintéticos, sin API ni persistencia.
Los fallos intermedios permanecen en sus recibos; no son el resultado final.

Dos exportaciones independientes del Companion tienen ZIP idéntico:
SHA-256 53583098fd3a22f8963edd72bc4dee6a3c6094bf925d2927f0a4302d89fd70ff;
portabilidad/relocación 15 PASS, sin arranque de worker autenticado ni SO limpio.
Doctor del perfil instalado PASS y DMR real por .NET PASS: cancelación 27 ms,
siguiente petición 2412 ms. Segundo dominio con el SDK genérico y otro alias
PASS en una llamada; CORS ajeno 403 y puerto por Wi-Fi propia rechazado.
Se conserva el perfil del motor porque ya coincide; no se reinició Docker ni
se cambió red/permisos. Ver [configuración reutilizable](DOCKER_AI.md).

Una adaptación explícita real nueva acortó una tarea redundante de 156 a 125
caracteres conservando la variable, restricciones e idioma: dos llamadas,
10074 ms. La autorrevisión de primera sugerencia está separada de la composición
del contrato confirmado por el actor sintético; no es evaluación humana ni
cierre de todas las familias históricas. No hubo reejecución selectiva.
MCP público desde Codex: búsqueda 56486 ms, lectura 1706 ms, compilación sin
valores 1666 ms con texto null y tres faltantes correctos; cero escrituras.
No acredita ChatGPT web, dos cuentas, refresh/revocación o el delta RC2 servido.

No hay cierre global ni aceptación pública de este delta. No atribuir estos
resultados a la versión alojada, al arranque del Companion instalado o a
fidelidad universal de Mejorar/Adaptar.
Recibos: .playwright/priority-closure-20261101/ del checkout RC2.

Brevo: solicitud de revisión manual publicada el 01/11 a las 05:13 America/La_Paz
en el ticket 5641363, con confirmación de incorporación. Activación pendiente;
sin teléfono ni credenciales en este documento. Las notas siguientes son cortes
fechados. Decisión comercial vigente: NO-GO; meta activa.


Candidato activo: **1.0.1-rc.2**. Decisión comercial: **NO-GO**.

Ruta de correo vigente: **Brevo**, reanudada por petición expresa del usuario.
Último corte real: aplicación M2M privada creada con un solo scope; secreto real
cifrado localmente y token HTTP 200, Bearer, 3600 segundos, permiso exacto.
Se corrigió el uso del marcador oculto de la CLI como secreto (401 invalid_client).
El envío real devuelve 403 permission_denied porque la cuenta SMTP no está activada.
Solicitud limitada de activación publicada una vez con autorización específica;
ticket Open. Cero correos aceptados; alta, recuperación y producción pendientes.
La sesión amplia CLI se cerró y no quedan copias de texto del secreto en los
archivos privados inspeccionados. No se cambió producción ni se autoriza otro envío
de soporte por esta aprobación ya consumida.

Primeros intentos del día, conservados como histórico:
El panel confirmó una llamada de verificación y después solicitó introducir su
código. No se creó la clave ni se configuró correo real. No se enviaron mensajes
a soporte. Mailjet queda preparado, sin activar ni retirar compatibilidad.
La alternativa M2M se implementó localmente después de bbefd38: 180 PASS/0 FAIL/
0 SKIP de correo con HTTP controlado, sin credenciales ni entrega reales.
El usuario autorizó la delegación temporal amplia de la CLI y la aplicación
privada limitada. Se aceptó el consentimiento, pero el retorno agotó 300 segundos
sin credenciales. El diagnóstico no recibió peticiones en el receptor local;
Edge dejó de estar disponible después, sin causa establecida para la espera.
No se creó una app M2M ni se cambió producción. El panel Render confirmó Live
para 4cb104b en Free. El control público del 31/10 a las 20:00:45Z respondió
200 en 378 ms: email=false, google=true, mailDelivery=unconfigured.
Los tres timeouts anteriores de 20 segundos siguen conservados, sin causa confirmada.
La CI 41228098851 pasó para bbefd38; las vistas de cierre están en
[aceptación](../MVP_ACCEPTANCE.md) y [decisión](../MVP_CLOSURE.md).


Cambio de correo solicitado el 31/10: adaptador Mailjet preparado dentro de
`IAccountMail`, sin cambiar Identity ni el esquema. Regresión focal de los tres
proveedores: 130 PASS/0 FAIL/0 SKIP, con HTTP controlado. No acredita cuenta,
credenciales, remitente, recepción ni configuración en producción. El primer
intento detectó y corrigió casing camelCase incompatible con Mailjet. Otro control
reprodujo una lectura interrumpida que propagaba IOException; ahora produce un
error seguro sin reintento. Ambos fallos se conservan en los recibos.
Configuración y límites en [ACCOUNT_MAIL.md](ACCOUNT_MAIL.md).

Esta matriz publica los mismos controles R1–R6 del registro de cierre del
proyecto. Las evidencias de cuentas y conversaciones se conservan de forma privada;
esta vista evita publicar sus identidades, credenciales o contenidos. Los resultados
automáticos no sustituyen la aceptación del entorno realmente ofrecido.

| ID | Resultado y propietario | Criterio obligatorio de cierre | Estado y siguiente acción |
|---|---|---|---|
| R1 | Arranque y disponibilidad / launcher, Companion, DMR | Apertura del acceso en Windows; intento registrado; PID y estado .NET correlacionados; tarea real desde la web; continuar después de inactividad y otra tarea; cancelar sin respuesta tardía y detener sin reinicio. Verificar actualización/retirada del artefacto sin perder la biblioteca | PARCIAL: instalado 1.0.1-rc.2-compat.1, compatibilidad explícita /1 y /2; 114 tests PASS, ZIP reproducible y ciclo portable 9/9. Autorización pública y renovación real PASS, sin modelo ni worker. Tras confirmación humana no se observó proceso ni registro nuevo del arranque. Falta petición pública, continuidad, cancelación y parada del instalado. Véase delta superior; no repetir el inicio rechazado mediante otra herramienta. |
| R2 | Resultado útil / autoría, Core, dominio y revisión | Campaña finita histórica y holdout con Qwen/DMR real: petición original, variables, referencias, restricciones y aclaraciones conservadas; borrador confirmado y persistido exactamente; cero efectos sin aprobación; errores/cancelación devuelven control | FAIL, CANDIDATO PARALELO NO PUBLICADO: el control histórico de 20 conserva 9/20, umbral 18/20. La ampliación de 28 y los probes posteriores siguen fallidos; no sustituyen aquella campaña. Crear literal permanece como decisión autorizada, no como aceptación publicada. El propietario NOVA conserva su revisión e inferencia; no incluir sus cambios sin calificar. |
| R3 | Crear cuenta, acceso y biblioteca / Identity, acceso, PostgreSQL y UI | Alta real por correo y Google según el método elegido; validación de datos/contraseña; confirmación de correo; acceso posterior y rechazo de alta duplicada sin revelar cuentas. Dos cuentas independientes: CRUD/variables/colecciones/historial; negativos de biblioteca/recurso; publicación/invitación/importación/revocación con versiones; sesión vencida recuperable. Preservar los prompts del usuario | PARCIAL: flujos reales de biblioteca y Google comprobados. CREAR CUENTA POR CORREO PENDIENTE: está deshabilitado mientras no haya entrega transaccional. Cerrar alta/confirmación/acceso con R5 y vincular recibos existentes al candidato final |
| R4 | ChatGPT/MCP / OAuth y conector | Cliente ChatGPT real: buscar, leer, completar y crear con variables en la biblioteca autorizada; respuesta visible; renovación y revocación; negar acceso fuera de alcance. No inferirlo de llamadas MCP desde Codex | PARCIAL / ACEPTACIÓN DURABLE BLOQUEADA: siete contratos HTTP/PG sintético PASS y negativo real del conector contra otra biblioteca PASS el 01/11. Al reabrir, las respuestas visibles anteriores no cargan; se reproduce también en un chat nuevo sin MCP. Causa exacta desconocida. No se acredita renovación/revocación del cliente web final ni se modifican JSON o permisos para ocultarlo. Sin mensajes a soporte. |
| R5 | Recuperar contraseña, operación y datos / Identity, hosting, correo y recuperación | Recuperación desde «Olvidé mi contraseña»: solicitud, correo realmente recibido, enlace válido, nueva contraseña aceptada, acceso posterior y rechazo de la anterior; rechazar enlaces vencidos, alterados y reutilizados; respuesta sin revelar si el correo existe y límites contra abuso. También correo de confirmación entregado, origen HTTPS definitivo autorizado, persistencia/almacenamiento cifrado y respaldo cifrado recurrente con restauración funcional y acceso temporal retirado | BLOQUEOS EXTERNOS Y OPERACIÓN PENDIENTE: control anónimo público 30/10 19:01:53Z devuelve HTTP 200, email=false y mailDelivery=unconfigured; no acepta alta por correo ni recuperación. Destino comercial $0 sin verificar. Cerrar servicio de correo autorizado con recepción y negativos reales, alojamiento y recuperación cifrada recurrente; no recrear cuentas, mover datos o reutilizar aprobaciones puntuales de respaldo. |
| R6 | Entrega / paquete, CI y aceptación | Un candidato identificado con hashes y configuración sin secretos; checks afectados de unidad/integración/contrato/seguridad/E2E y regresión; aceptación conjunta pública; guía operativa/rollback; todas R1–R5 verificadas, sin FAIL o BLOQUEADO crítico; entonces decisión GO y cierre de la meta | PARCIAL: código RC2 bbefd38, CI 41228098851 SUCCESS para web y PostgreSQL; Accounts 590 PASS/30 no ejecutadas, backend 1487 PASS/1 no ejecutada. Paquete Companion previo de 68 entradas reproducible, sin cambios de sus inputs en bbefd38. Fuentes exactas archivadas y hashes en RELEASE_READINESS.md. Build local nuevo Cloud y web PASS. Documentación de cierre posterior sin commit ni publicación. Falta aceptación conjunta pública R1–R5, instalación limpia, actualización/retirada y operación; sin tag estable. |

## Corte integrado

- Base: commit público `4cb104bfb874004dd6274e1aef3c9537a834cb3b`. Se integran
  presencia observacional, transporte DMR, contratos de autoría, UI de cuentas,
  conectores y herramientas de instalación/recuperación existentes.
- La web y el lockfile indican `1.0.1-rc.2`; el paquete Companion obtiene su versión
  del mismo package.json. Los cambios de esquema requieren revisión y migración
  operativa antes de publicar; el build no migra la base de producción.
- En checkout nuevo: backend Release 1487 PASS/1 hardware SKIP; frontend 501 PASS;
  78 controles sintéticos Accounts/MCP PASS. Los controles reales PostgreSQL y la
  aceptación conjunta de este corte se registrarán por su ejecución, no por esta nota.
- Persisten dos fallos semánticos en los cuatro casos Qwen/DMR más recientes:
  ampliación de restricciones en notas y del entregable de clasificación.
  La confirmación humana sintética posterior no los convierte en PASS.
- ChatGPT público recuperó respuestas visibles el 31/10; no se conoce la causa
  de esa recuperación y falta aceptación conjunta del candidato final.
- Correo de confirmación/recuperación, disponibilidad pública del componente,
  alojamiento comercial y protección recurrente de datos siguen pendientes.

No hay tag estable ni certificación comercial por identificar o compilar este RC.

Traducción contextual comprobada el 02/11: tras la aclaración humana de tarjeta/registro,
una nueva captura produjo Card and instruction y Open the card «Firmar» and request
to sign <acuerdo>. Dos llamadas reales, sin retries ni efectos. El fallo automático
sin aclaración se conserva; no se declara una campaña automática 4/4.
