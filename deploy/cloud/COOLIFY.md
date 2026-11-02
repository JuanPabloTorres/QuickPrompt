# QuickPrompt en infraestructura abierta

Decisión del usuario, 01/11/2026: no usar Oracle. Esta guía prepara **Coolify
autogestionado**, con el Dockerfile existente, .NET 10, React y PostgreSQL 18.
No es otro roadmap: pertenece a R5 del [checklist](../../docs/development/GO_TO_MARKET.md).
Estado: **PREPARADO DOCUMENTALMENTE; NO INSTALADO NI DESPLEGADO**.

Actualización posterior: el usuario eligió evaluar servicios cloud gratuitos,
sin servidor propio. Coolify se conserva como alternativa; la [evaluación cloud
vigente](../../docs/research/CLOUD_FREE_REVIEW_20261101.md) orienta R5.

## Elección y coste real

Coolify ofrece una interfaz web para desplegar imágenes, configurar secretos,
dominios, bases y operaciones. Su repositorio actual usa Apache 2.0. Se elige
por la operación accesible, no porque aporte nuevas capacidades al dominio de
QuickPrompt. La aplicación sigue siendo portable: no importa librerías de Coolify.

La instalación propia no exige una suscripción al panel. Requiere un servidor
Linux con SSH: mínimo publicado de 2 CPU, 2 GB de RAM y 10 GB libres para Coolify;
la aplicación, PostgreSQL, builds y respaldos necesitan recursos adicionales.
Para empezar la evaluación conjunta se proponen 2 CPU, 4 GB RAM y 40 GB de disco;
es un presupuesto de recursos, no una medición de capacidad ni una oferta contratada.

El software abierto no proporciona por sí solo un servidor público gratuito.
El presupuesto vigente sigue siendo $0. No se ha identificado un servidor Linux
propio autorizado ni se ha contratado un VPS. No instalar el panel en el Windows
de desarrollo ni publicarlo como sustituto de disponibilidad comercial.

Alternativas examinadas: Docker Compose directo evita el panel pero exige mayor
operación manual; Dokploy también necesita un servidor y no elimina ese coste.
No se elige Northflank Sandbox como solución de producción: su propia guía
desaconseja ese uso y contradice la página de precios sobre la necesidad de tarjeta.
No crear cuentas de otros proveedores para perseguir una etiqueta «free».

## Configuración preparada para el destino

| Recurso/campo | Valor o condición |
| --- | --- |
| Panel | Coolify self-hosted, en servidor Linux autorizado; acceso administrativo privado y restringido |
| Fuente | Repositorio QuickPrompt; candidato exacto identificado y publicado con autorización; sin desplegar automáticamente «latest» |
| Build Pack | Dockerfile |
| Base Directory | `/` del repositorio |
| Dockerfile Location | `/deploy/cloud/Dockerfile` relativo a esa base; comprobar la ruta resuelta en la versión del panel instalada |
| Puerto interno | `10000`; sin mapear el puerto de la aplicación directamente al host público |
| Entrypoint | El del Dockerfile: `deploy/cloud/entrypoint.sh` |
| Frontend | El build React ya se copia a `wwwroot`; `AccountHost.MapAccountPage` declara el modo cuentas |
| Origen | Una sola URL HTTPS definitiva para web, API, Google y MCP; aún sin hostname aprobado |
| Base | PostgreSQL 18, red interna y volumen persistente; en PG18 el montaje es `/var/lib/postgresql` |
| Identidades SQL | Roles separados de aplicación, dispatcher y migración; el proceso web nunca usa `postgres` |
| TLS SQL | `SSL Mode=VerifyFull` para ambas conexiones, CA y nombre de servidor comprobados incluso en red privada |
| Claves | Certificado PFX del key ring y PEM de delegación en secretos privados del destino aprobado; sin credenciales de build |
| Correo | Reutilizar el adaptador autorizado; confirmar entrega real antes de habilitar alta y recuperación |
| Modelo | Qwen3:8b en Docker Model Runner del equipo emparejado; no instalar pesos ni GPU en el servidor por esta migración |

El [Dockerfile](Dockerfile) fija versiones/digests y usuario no root; reutilizar su
allowlist. No añadir otra API, generador, sistema de cuentas ni capa de persistencia.

## Ingreso: conservar la frontera de confianza

`AccountHosting` ya admite `TrustedProxy` y `OriginProxy`. Son perfiles excluyentes.
Para el proxy TLS local de Coolify, preparar `TrustedProxy` **sólo después de
observar y fijar su IP o red Docker dedicada**. Configurar:

| Variable | Configuración |
| --- | --- |
| `QuickPrompt__Hosting__Mode` | `Public` |
| `QuickPrompt__Hosting__IngressProfile` | `TrustedProxy` |
| `QuickPrompt__Hosting__ListenUrl` | `http://0.0.0.0:10000` |
| `QuickPrompt__Accounts__Origin` | Origen HTTPS definitivo aprobado |
| `QuickPrompt__Hosting__TrustedProxies__0` | IP real del proxy fijada y verificada; no copiar un ejemplo |
| `ConnectionStrings__Accounts` | Conexión limitada de la aplicación, VerifyFull |
| `ConnectionStrings__Executions` | Conexión limitada del dispatcher a la misma base, VerifyFull |

No usar redes universales, auto-forwarding ni IPs del proveedor supuestas.
Si la infraestructura no permite fijar esta confianza, detener esa configuración;
evaluar el perfil autenticado existente con un proxy controlado, sin debilitarlo.
El acceso directo al contenedor debe quedar cerrado por red. Conservar las
pruebas de Host, Origin, cookies, CSRF y cabeceras falsificadas.

## Corte de datos y rollback: todavía no ejecutados

1. Identificar servidor, origen, almacenamiento, administrador y presupuesto.
   Revisar el acceso administrativo antes de instalar o abrir puertos.
2. Instalar una versión identificada de Coolify, restringir el panel y preparar
   PostgreSQL/TLS, volumen y roles. Verificar el entorno con datos sintéticos.
   No arrancar ni reemplazar contenedores previamente rechazados por política.
3. Congelar el candidato. Aprobar antes del efecto el destino de sus secretos,
   la copia de datos y un respaldo nuevo; no reutilizar permisos de respaldo consumidos.
4. Frenar escrituras durante el corte; copiar y comprobar tablas, secuencias,
   roles, FORCE RLS, recibos, tumbas, publicaciones y revocaciones. No aplicar
   bootstrap de base vacía sobre datos existentes.
5. Mantener PFX/PEM compatibles y verificar continuidad del key ring. Cambiar
   origen cambia el nombre criptográfico ligado a origen/base: no prometer que
   sobrevivan cookies o tokens antiguos; planificar acceso y reconexión explícitos.
6. Configurar Google, issuer y callbacks MCP para el mismo origen. Emparejar los
   componentes autorizados; no ampliar sus permisos ni iniciar un Companion
   por una vía alternativa si su arranque ya fue rechazado.
7. Ejecutar los subcontroles públicos R1–R6: dos cuentas, recepción de correo,
   recuperación, CRUD, NOVA/DMR, cancelación y renovación/revocación de MCP.
   Health, un panel verde o un dump creado no sustituyen estos recorridos.
8. Activar respaldo cifrado recurrente y retención bajo una política aprobada;
   demostrar restauración y alertas. Los backups nativos del panel no acreditan
   por sí solos el cifrado y recuperación exigidos por QuickPrompt.
9. Abrir tráfico sólo después de los controles críticos. Un rollback debe
   conservar esquema, claves, recibos y borrados. No devolver tráfico a una base
   antigua después de admitir escrituras nuevas sin reconciliación revisada.

El [procedimiento PostgreSQL](POSTGRES.md), la [operación](../operations/README.md)
y el propietario [`AccountHosting`](../../backend/QuickPrompt.Cloud/AccountHosting.cs)
siguen siendo los contratos técnicos. No borrar Render, Vercel o respaldos
históricos como parte de esta preparación.

## Fuentes comprobadas el 01/11/2026

- [Coolify: requisitos y self-hosting](https://coolify.io/docs/start-with-self-hosted).
- [Licencia Apache 2.0 del repositorio actual](https://github.com/coollabsio/coolify/blob/main/LICENSE).
- [Build desde Dockerfile y contexto](https://coolify.io/docs/applications/builds/dockerfile).
- [PostgreSQL, red privada y montaje PG18](https://coolify.io/docs/databases/postgresql).
- [Modelo de seguridad de Coolify](https://coolify.io/docs/core/security-model).
- [Dokploy: instalación y requisitos](https://docs.dokploy.com/docs/core/installation).
- [Northflank: límites de producción del Sandbox](https://northflank.com/docs/v1/application/billing/pricing-on-northflank).

La consulta documental y Context7 confirman capacidades publicadas; no prueban
una instalación, precio del servidor, seguridad o rendimiento del destino final.
