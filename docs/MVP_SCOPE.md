# Alcance congelado del MVP

Referencia: **1.0.1-rc.2**, código `bbefd38626e82c4ce389cd3735a657b0a2f929f0`,
31/10/2026. Este documento describe el alcance; no certifica funcionamiento.
El único roadmap y registro de pendientes es [R1–R6](development/GO_TO_MARKET.md).

## Capacidades incluidas

- Biblioteca privada por cuenta: crear, editar, duplicar, eliminar, buscar,
  favoritos, archivo, versiones y colecciones; persistencia y autorización por recurso.
- Recorrido **buscar → seleccionar → completar → compilar → copiar/usar**,
  con variables explícitas y compilación de dominio sin inferencia.
- Acceso Google y alta por correo, confirmación y recuperación de contraseña.
  Que el correo esté deshabilitado actualmente no lo excluye de la aceptación.
- Compartir versiones mediante invitación, importar y revocar; dos identidades
  independientes y aislamiento también en API/PostgreSQL.
- NOVA con acción elegida por el usuario, tarea/requisitos establecidos antes de
  redactar, sugerencias separadas y confirmación exacta antes de efectos.
- Qwen3:8b administrado por Docker Model Runner. El Companion emparejado y
  revocable conecta el equipo del usuario; el servidor no usa su propio localhost
  como sustituto del equipo. La presencia sólo informa disponibilidad.
- MCP autenticado por cuenta/biblioteca, con permisos explícitos y revocación;
  aceptación real en ChatGPT, sin heredar permisos del Companion.
- Instalación/actualización/retirada reproducibles, configuración sin secretos,
  recuperación comprobada y operación en el alojamiento realmente ofrecido.

## Límites conservados

React compone la interfaz; .NET/Application/Accounts conservan contratos,
autorización y efectos; Inference y Core/Python poseen inferencia; Companion posee
el dispositivo; los conectores transportan capacidades. No se cambia ese reparto.

No se incorporan ventas, pagos, nuevos proveedores de IA, nuevas acciones
autónomas ni una migración de identidad para cerrar esta entrega. La inspección
no localizó una extensión MV3 mantenida en este checkout; integración con Claude
o Codex mediante una extensión no se anuncia como capacidad certificada del RC.
Si se exige como entrega, requiere inventario y aceptación específicos antes de GO.

El cierre comercial no se obtiene eliminando pruebas, renombrando fallos,
confundiendo una sugerencia con autorización o excluyendo una capacidad acordada.
Véanse [aceptación](MVP_ACCEPTANCE.md) y [decisión](MVP_CLOSURE.md).
