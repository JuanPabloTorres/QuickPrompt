# Documentación de QuickPrompt

La versión de referencia es NOVA 1.0.0, aceptada por el usuario para el flujo por acciones. La fuente del número visible es `apps/web/package.json`.

## Documentos vigentes

| Necesidad | Documento propietario |
| --- | --- |
| Arrancar la aplicación | [README raíz](../README.md) y [configuración de Core](../agents/nova-core/README.md) |
| Abrir desde el escritorio | [Acceso directo, icono y perfil de arranque](development/DESKTOP.md) |
| Conectar clientes MCP, buscar por colección y preparar ChatGPT | [Herramientas, colecciones, captura y transporte MCP](development/MCP.md) |
| Registrar usos reales de prompts en cuentas | [Preparación, copia y recibos de uso](development/ACCOUNT_PROMPT_USE.md) |
| Revisar hechos y conservar documentos privados | [Transformación factual e historial](development/FACTUAL_REVIEW_UI.md) |
| Confirmar los nombres de variables de NOVA | [Entradas explícitas de NOVA en cuentas](development/ACCOUNT_NOVA_INPUTS.md) |
| Responder una pregunta de contenido y recuperar un envío incierto | [Aclaraciones de NOVA en cuentas](development/ACCOUNT_NOVA_CLARIFICATION.md) |
| Usar biblioteca, variables y NOVA | [Guía de uso](COMO_USAR_QUICKPROMPT.md) |
| Definir campos sin confundir nombres y valores | [Flujo confirmado y evidencias](nova/VARIABLES_CONFIRMADAS_20261025.md) |
| Responsabilidades y dependencias | [Arquitectura](ARCHITECTURE.md) |
| Crear formularios y componentes | [UI modular](development/UI_COMPONENTS.md) |
| Ejecutar checks y mantener documentación | [Desarrollo](development/DEVELOPMENT.md) |
| Transición a cuentas, PostgreSQL y bibliotecas compartidas | [Goal y criterios por incremento](development/SAAS_DELIVERY.md) |
| Verificar el piloto y la salida comercial | [Checklist Go To Market y candidato reproducible](development/GO_TO_MARKET.md) |
| Desplegar la ruta web Vercel y backend persistente | [Proxy y web Vercel](../deploy/vercel/README.md), [backend .NET](../deploy/cloud/README.md) y [PostgreSQL del piloto](../deploy/cloud/POSTGRES.md) |
| Vincular y usar el componente de IA local | [Companion Windows y límites verificados](development/COMPANION.md) |
| Instrucciones de agentes | [AGENTS raíz](../AGENTS.md) y [reglas web](../apps/web/AGENTS.md) |
| Revisión actual de interfaz | [Cierre r2 y límites](releases/NOVA_1.0.0_R2.md) y [manifiesto](releases/nova-v1.0.0-r2.json) |
| Aceptación de la versión inicial y correcciones | [Cierre r1](releases/NOVA_1.0.0_R1.md) y [manifiesto r1](releases/nova-v1.0.0-r1.json); [acta inicial](releases/NOVA_1.0.0.md) |
| Auditoría del agente tras el rediseño | [Resultados, arquitectura y dos hallazgos pendientes](nova/AUDITORIA_AGENTE_UI_20261023.md) |
| Conservar resultados de ejecución | [Política de evidencia](evidence/README.md) |

Cada capacidad tiene una guía vigente. Los informes fechados conservan qué ocurrió, con qué candidato y sus límites; no sustituyen el contrato actual ni conceden permisos.

## Historial y documentación retirada

[Índice histórico](archive/README.md). Los informes GOAL/PHASE, el legado Blazor/MAUI y las campañas antiguas de `nova/` son evidencia contextual. Se mantienen las rutas referenciadas por matrices o pruebas. Las instrucciones de comandos libres y del modal antiguo salen de la ruta vigente y se archivan con una advertencia de alcance.

La matriz ampliada anterior conserva criterios no certificados; el cierre de NOVA 1.0.0 y la aceptación humana están en el acta de release. No interpretar el estado histórico de otro perfil como el estado de la entrega actual.
