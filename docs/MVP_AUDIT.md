# Auditoría de defectos y correcciones

Referencia: **1.0.1-rc.2 / bbefd38**, 31/10/2026. Estado: NO-GO.
Pendientes y responsables: [roadmap R1–R6](development/GO_TO_MARKET.md).

Las cifras verdes de esta tabla conservan la fecha y el alcance de su ejecución.
En particular, los 1504 PASS son el corte del 01/11 a las 10:09:33 a. m.
(UTC−04:00), no la validación del productor posterior. El snapshot de autoría
registra después 105 PASS/11 FAIL de 116, con integración pendiente. Véanse
[procedencia y hashes](MVP_ACCEPTANCE.md#procedencia-de-las-cifras--01112026).

| Hallazgo | Causa demostrada y corrección | Evidencia / estado |
|---|---|---|
| La fábrica local ignoraba una conexión Core explícita | `NovaAgentSettings` no leía Provider/Endpoint/PhysicalModel; `ApplicationModules` usaba el endpoint legacy y defaults. Se captura y valida el binding en esos propietarios existentes. | 8 FAIL de reproducción, 73 PASS focales y 1504 PASS/2 SKIP en el corte histórico del 01/11 a las 10:09:33 a. m. Delta local sin publicar. |
| Retirar el HTTP del wrapper local rompía disponibilidad | `OllamaClient.Available()` también usa ese HTTP; sustituirlo por el wrapper prestado de cuentas dejó un cliente nulo. Se retiró esa sustitución experimental y se conservó el contrato local. | Un FAIL real en `LocalClientLifetimeTests`, después 73 PASS. No afirmar independencia completa de disponibilidad/embeddings. |
| Un nuevo escritor de reemplazos completos sigue alterando condiciones | IDs de campo completo eliminan localización/solapamientos/borrados vacíos, pero sus primeras propuestas aún amplían una condición y cambian unidad por elemento. | 10 llamadas acotadas; contraste rechazado sin cambios de producto. `field-transaction-probe-01/ACCEPTANCE_DECISION.json`. Causa interna del modelo desconocida. |
| Un selector alternativo sigue confundiendo título y función | Las ramas correlacionadas eliminan contradicciones sintácticas pero no impiden elegir FUNCTION para una referencia nominal. | 9/10 conjuntos, 6/6 en subconjunto congelado; criterio completo no satisfecho, sin integrar. `selector-set-probe-01/ACCEPTANCE_DECISION.json`. |
| Mailjet serializaba `Messages` como `messages` | JsonContent usaba defaults web; el adaptador nuevo fija serialización General. | 127 PASS/2 FAIL antes; regresión posterior preservada. Código corregido; entrega real no probada. |
| Un cuerpo HTTP de correo interrumpido propagaba IOException | Faltaba traducir el fallo de lectura en la frontera de Mailjet. Ahora devuelve código seguro, preserva cancelación y no reintenta. | FAIL controlado antes; 130 tests de correo PASS después. No credenciales reales. |
| Una cita existente podía sustituir la tarea y borrar requisitos | El compilador aceptaba identidad de cita sin ligarla a la tarea establecida. Catálogo/schema y compilador exigen igualdad y conservan listas. | Regresiones FAIL→PASS en su corte histórico; no atribuir ese verde al árbol posterior. No certifica semántica de INSTRUCTION. |
| El launcher no registraba un helper de paquete ausente | Importación fuera del bloque protegido y anterior al registro. Módulo de arranque mínimo registra antes de cargar el paquete. | 13 controles de diagnóstico PASS, paquete 58 PASS, traslado 15 PASS. No demuestra la causa del «no abrió nada» real. |
| NOVA añade una restricción o amplía un entregable al crear | Primera divergencia en la salida del escritor que volvía a redactar el contrato humano. Por decisión expresa del usuario, Crear conserva literalmente ese contrato, con revisión final separada, sin discovery/escritor. | Corrección local 01/11: regresión FAIL→PASS, suite backend 1492 PASS/1 SKIP, web 502 PASS. No publicada. Mejorar/Adaptar sigue permitiendo propuestas separadas y requiere validación por familia; un caso útil real PASS no cierra los fallos históricos del escritor. |
| Lectura interrumpida en Brevo y SMTP2GO | El lector compartido no clasificaba IOException; el error escapaba del puerto IAccountMail. La clasificación y prioridad de cancelación se fijan una sola vez en ReadReceipt. | 6 FAIL/3 PASS de reproducción y 139 PASS finales de los tres proveedores. Cambio local posterior a bbefd38; no desplegado. |
| Brevo no entrega una credencial utilizable | Verificación telefónica exigida por la cuenta antes de crear clave. La llamada oficial se solicitó una vez y el panel quedó esperando el código. | BLOCKED externo. No se envió soporte ni se habilitó correo ficticio. |
| HTTP público excedió 20 segundos al iniciar esta revisión | Tres observaciones agotaron su plazo. Después la UI cargó acceso Google y biblioteca. No hay causa confirmada ni evidencia suficiente para afirmar caída permanente. | Demora conservada; no atribuida automáticamente a frío de Render. |

El delta M2M posterior a bbefd38 se limita al adaptador de correo y su composición.
La configuración real tuvo un fallo de aprovisionamiento: el JSON de creación de
la CLI oculta el secreto y se tomó ese marcador como credencial. Se reprodujo 401
invalid_client; la lectura interactiva oficial del secreto real y su almacenamiento
DPAPI corrigieron esa causa. Token posterior: 200, scope exacto, 3600 segundos.
El adaptador llegó entonces al envío y recibió 403 permission_denied por cuenta
SMTP sin activar. Ese bloqueo externo está separado del fallo de configuración.
Solicitud de activación publicada una vez con aprobación específica; no es entrega.

Los 180 controles HTTP finales pasan; no se añadieron dependencias, endpoints,
esquema, autoridad del agente ni permisos de usuarios/MCP. Un control OAuth falló
al recibir un token válido sin campo scope; se corrigió conforme a RFC 6749 §5.1
y se conserva ese FAIL. La credencial M2M real ya funciona; la entrega sigue bloqueada.

El fallo de ChatGPT «This response couldn’t load» permanece en el histórico.
El 31/10 se observó lectura durable y nuevas operaciones visibles sin reparación
atribuible: recuperación observada, causa desconocida. El JSON sintético válido
no se usa como prueba del intercambio público ni como explicación causal.

No se han alterado permisos, modelo, retención, esquema o datos privados para
obtener PASS. Los cambios de esta revisión de cierre documental no se publican
automáticamente. Véanse [aceptación](MVP_ACCEPTANCE.md) y
[seguridad](SECURITY_VALIDATION.md).
