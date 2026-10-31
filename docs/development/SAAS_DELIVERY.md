# Entrega SaaS de QuickPrompt

La decisión comercial y la aceptación se mantienen en [GO_TO_MARKET](GO_TO_MARKET.md).
El RC2 integra el código existente; su publicación comercial requiere cerrar R1–R6.

## Fronteras

- La web compone servicios; dominio/.NET valida identidad, biblioteca, revisión
  y efectos. PostgreSQL mantiene aislamiento de aplicación y RLS.
- MCP usa identidad delegada y scopes por biblioteca. Leer contenido no autoriza
  ejecutar sus instrucciones, ampliar permisos o escoger otra biblioteca.
- Companion usa una conexión revocable por capacidad. Docker Model Runner
  conserva Qwen3:8b y el perfil explícito; el servidor alojado no usa el localhost
  del navegador como si fuera el suyo.
- Core/Python posee el transporte de inferencia. La presencia informa estado y
  no concede autoridad, repite herramientas o valida la corrección semántica.

## Preparar despliegue

1. Identificar commit/candidato, dependencias y hashes. Construir web y backend
   del mismo corte; revisar [Dockerfile](../../deploy/cloud/Dockerfile) y su
   allowlist. El contenedor restaura con lock y publica sin otro restore implícito.
2. Preparar configuración externa de PostgreSQL, origen HTTPS, Google, correo
   y claves protegidas mediante el procedimiento autorizado del operador.
   Mantener secretos y bibliotecas personales fuera de Git, imagen y paquete.
3. Revisar migraciones y permisos del rol limitado antes de aplicar el esquema.
   El respaldo privado necesita autorización concreta y restauración comprobada;
   una aprobación anterior consumida no cubre otro procedimiento.
4. Actualizar el backend y distribuir el Companion compatible como un despliegue
   coordinado. El usuario conserva control de emparejamiento, revocación y parada.
5. Validar dos cuentas independientes, correo, CRUD, compartir, NOVA, ejecución,
   cancelación y ChatGPT/MCP en el entorno ofrecido. Registrar FAIL y omisiones.

## Operación

- [Companion](COMPANION.md), [presencia](COMPANION_PRESENCE.md), [MCP](MCP.md).
- [Correo](ACCOUNT_MAIL.md) y [PostgreSQL](../../deploy/cloud/POSTGRES.md).
- [Política de respaldo](../../deploy/operations/README.md).

Los adaptadores de correo y scripts de instalación no acreditan entrega real,
instalación limpia ni recuperación. El despliegue público sigue en 4cb104b hasta
que se publique y acepte otro candidato; compilar RC2 no lo sustituye.
