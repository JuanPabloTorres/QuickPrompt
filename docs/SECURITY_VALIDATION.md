# Validación de seguridad del candidato

Referencia: **1.0.1-rc.2 / bbefd38**, 31/10/2026. Alcance técnico y pendientes,
sin declaración de certificación universal. Roadmap: [R3–R5](development/GO_TO_MARKET.md).

## Evidencia ejecutada

[CI 41228098851](https://github.com/JuanPabloTorres/QuickPrompt/actions/runs/41228098851)
levantó PostgreSQL aislado con roles limitados y datos sintéticos. Accounts:
590 PASS/30 no ejecutadas. El recibo de restauración registra 23 tablas con RLS
forzada, cero bibliotecas visibles al actor anónimo y conservación de contratos
de delegación, memoria, colecciones, presencia y documentos factuales.

El contraste de catálogo detectó políticas, grants, índices, claves foráneas y
restricciones alterados deliberadamente; verificó retirada de las bases sintéticas.
No es restauración de producción, prueba de disponibilidad ni RTO comercial.

| Frontera | Control conservado | Límite de aceptación |
|---|---|---|
| Identidad | Identity posee contraseña, confirmación, reset y sesiones; Google no concede acceso a la biblioteca de otra persona. | Correo y recuperación reales pendientes. |
| Biblioteca/recurso | Actor y biblioteca en aplicación, relaciones compuestas y FORCE RLS en PostgreSQL. | Dos cuentas públicas del candidato final todavía pendientes. |
| NOVA | Establecimiento y revisión de contenido distintos de aprobación de guardar/ejecutar; hashes/revisiones invalidados ante cambios. | La revisión no certifica fidelidad de la primera sugerencia. |
| Presencia | Observación con identidad/caducidad; retirada CAS, sin reintentar trabajos ni autorizar herramientas. | Falta aceptación del Companion instalado. |
| Companion | OAuth por perfil, grant revocable, lease y credencial local protegida con DPAPI. | Ningún inicio autenticado se ejecutó para eludir el bloqueo anterior. |
| MCP | OAuth/PKCE, biblioteca y scopes delegados; los grants existentes no se amplían por añadir herramientas. | Renovación/revocación y negativos públicos conjuntos pendientes. |
| Correo | Origen canónico, propósito confirm/reset, un destinatario, sin redirects/cookies, sin logs de claves o tokens, envío único. M2M privado con un scope y token real de una hora, secreto DPAPI local. | El envío real devuelve 403: cuenta SMTP sin activar. Aún no hay entrega o recuperación real. |

No se copiaron datos privados a QA ni se crearon nuevos roles administrativos,
backups o restauraciones privadas en esta revisión. Los permisos puntuales
históricos no autorizan procedimientos recurrentes. Continúan pendientes el
respaldo cifrado recurrente, custodia/rotación y la restauración operativa ofrecida.

Auditoría de dependencias del 31/10: npm audit del frontend
reportó cero avisos conocidos (incluye desarrollo); NuGet no reportó paquetes
vulnerables en el grafo resuelto de Cloud, incluidas transitivas. No es un análisis
de todos los componentes ni prueba de ausencia de vulnerabilidades.

Un acceso cruzado confirmado o una autorización omitida obliga a NO-GO aunque
otros controles pasen. No se reducen assertions, RLS, validación TLS ni aprobación
humana para cerrar la entrega.
# Corte actual del 02/11/2026

La [matriz de aceptación](MVP_ACCEPTANCE.md) y R1–R6 conservan el estado vigente:
**NO-GO**. Hay negativos reales con dos cuentas sintéticas, RLS/restauración,
revocación de publicación y persistencia tras reinicio; no equivalen a aceptación
de dos cuentas públicas, scopes/refresh/revocación de ChatGPT o backup privado
recurrente. El ZIP actual pasa controles de exclusión de credenciales; el barrido
acotado de patrones no encontró coincidencias de alto riesgo y npm audit de
producción informa cero vulnerabilidades. Ninguno certifica ausencia universal
de secretos o vulnerabilidades. Recibos en `.playwright/closure-20261102/`.
Los informes siguientes conservan su fecha y candidato originales.

