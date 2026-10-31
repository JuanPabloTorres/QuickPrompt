# QuickPrompt — estado del Release Candidate

Candidato activo: **1.0.1-rc.2**. Decisión comercial: **NO-GO**.

Esta matriz publica los mismos controles R1–R6 del registro de cierre del
proyecto. Las evidencias de cuentas y conversaciones se conservan de forma privada;
esta vista evita publicar sus identidades, credenciales o contenidos. Los resultados
automáticos no sustituyen la aceptación del entorno realmente ofrecido.

| ID | Resultado y propietario | Criterio obligatorio de cierre | Estado y siguiente acción |
|---|---|---|---|
| R1 | Arranque y disponibilidad / launcher, Companion, DMR | Apertura del acceso en Windows; intento registrado; PID y estado .NET correlacionados; tarea real desde la web; continuar después de inactividad y otra tarea; cancelar sin respuesta tardía y detener sin reinicio. Verificar actualización/retirada del artefacto sin perder la biblioteca | PARCIAL: modo paired y guardas conservados; inferencia DMR real y segundo consumidor comprobados el 30/10. Reconfiguración implícita del adaptador corregida con FAIL→PASS; cancelación .NET 84 ms y continuación 2,273 s. Falta arranque y resultado público correlacionados del Companion instalado; no repetir el inicio rechazado mediante otra herramienta |
| R2 | Resultado útil / autoría, Core, dominio y revisión | Campaña finita histórica y holdout con Qwen/DMR real: petición original, variables, referencias, restricciones y aclaraciones conservadas; borrador confirmado y persistido exactamente; cero efectos sin aprobación; errores/cancelación devuelven control | PARCIAL, FLUJO LOCAL NO PUBLICADO: E2 conserva 9 PASS/1 FAIL semántico. E3 fue retirada. Reparación determinista posterior liga la cita literal a la tarea y conserva requisitos: backend 1487 PASS/1 SKIP y 28 MCP/arnés Accounts PASS, sin SQL. Cuatro casos reales posteriores: 4 composiciones PASS, 2 primeras propuestas PASS/2 FAIL semánticos, ocho llamadas. Notas repite la restricción añadida y clasificación amplía el entregable. No se ejecutó LITERAL con Qwen en esos cuatro casos. La confirmación exacta protege efectos, sin certificar fidelidad inicial. REFACTOR_OBLIGATORIO semántico sigue abierto, al igual que aceptación HTTP/persistencia pública conjunta; no repetir campañas sin intervención causal. |
| R3 | Crear cuenta, acceso y biblioteca / Identity, acceso, PostgreSQL y UI | Alta real por correo y Google según el método elegido; validación de datos/contraseña; confirmación de correo; acceso posterior y rechazo de alta duplicada sin revelar cuentas. Dos cuentas independientes: CRUD/variables/colecciones/historial; negativos de biblioteca/recurso; publicación/invitación/importación/revocación con versiones; sesión vencida recuperable. Preservar los prompts del usuario | PARCIAL: flujos reales de biblioteca y Google comprobados. CREAR CUENTA POR CORREO PENDIENTE: está deshabilitado mientras no haya entrega transaccional. Cerrar alta/confirmación/acceso con R5 y vincular recibos existentes al candidato final |
| R4 | ChatGPT/MCP / OAuth y conector | Cliente ChatGPT real: buscar, leer, completar y crear con variables en la biblioteca autorizada; respuesta visible; renovación y revocación; negar acceso fuera de alcance. No inferirlo de llamadas MCP desde Codex | PARCIAL / ACEPTACIÓN FAIL: parche 4cb104b publicado en Render Live y CI PASS; PostgreSQL 460 PASS/21 SKIP. Una lectura nueva en ChatGPT falla tras 9 s. Recibos get_prompt/HTTP 200 SSE correlacionados entre sí, sin traza expuesta por el cliente. SDK sintético produce wire válido; no demuestra el intercambio público. App apunta al endpoint correcto pero no carga sus ajustes. Falta identificar la divergencia y aceptar buscar/leer/completar/crear visibles del candidato final; renovación/revocación históricas no sustituyen esa aceptación. Sin soporte ni nuevas consultas hasta una intervención causal. |
| R5 | Recuperar contraseña, operación y datos / Identity, hosting, correo y recuperación | Recuperación desde «Olvidé mi contraseña»: solicitud, correo realmente recibido, enlace válido, nueva contraseña aceptada, acceso posterior y rechazo de la anterior; rechazar enlaces vencidos, alterados y reutilizados; respuesta sin revelar si el correo existe y límites contra abuso. También correo de confirmación entregado, origen HTTPS definitivo autorizado, persistencia/almacenamiento cifrado y respaldo cifrado recurrente con restauración funcional y acceso temporal retirado | BLOQUEOS EXTERNOS Y OPERACIÓN PENDIENTE: control anónimo público 30/10 19:01:53Z devuelve HTTP 200, email=false y mailDelivery=unconfigured; no acepta alta por correo ni recuperación. Destino comercial $0 sin verificar. Cerrar servicio de correo autorizado con recepción y negativos reales, alojamiento y recuperación cifrada recurrente; no recrear cuentas, mover datos o reutilizar aprobaciones puntuales de respaldo. |
| R6 | Entrega / paquete, CI y aceptación | Un candidato identificado con hashes y configuración sin secretos; checks afectados de unidad/integración/contrato/seguridad/E2E y regresión; aceptación conjunta pública; guía operativa/rollback; todas R1–R5 verificadas, sin FAIL o BLOQUEADO crítico; entonces decisión GO y cierre de la meta | PARCIAL: corte privado quickprompt-1.0.1-rc.1-fields-mcp-frozen-20261030 recuperable con 1736 fuentes y 45/45 artefactos Cloud idénticos; 63 regresiones focales desde copia fuera de Git. Companion G/H: 67 entradas ZIP idénticas y 15 checks de traslado. E/F queda histórico. Alcance Windows/SDK/caché actuales, sin instalación limpia ni publicación del corte integral. Parche MCP aislado 4cb104b publicado con CI verde sobre base 656101c; no representa el corte privado de autoría. Faltan aceptación pública conjunta R1-R5, instalación/actualización/retirada y operación; ningún tag estable. Las notas posteriores no modifican el snapshot archivado. |

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
- ChatGPT real mantiene una respuesta sin cargar; HTTP 200 y un recibo de
  herramienta anterior a la serialización no prueban lo que recibió el cliente.
- Correo de confirmación/recuperación, disponibilidad pública del componente,
  alojamiento comercial y protección recurrente de datos siguen pendientes.

No hay tag estable ni certificación comercial por identificar o compilar este RC.
