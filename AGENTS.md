# Guía de arquitectura del proyecto

Para cambios de lógica, fiabilidad y arquitectura, aplicar siempre el skill local
[Universal Modular Architecture](.agents/skills/universal-modular-architecture/SKILL.md),
junto con `algorithmic-engineering` y, cuando intervenga NOVA/LLM, `adaptive-ai-systems`.

- Identificar el propietario existente de cada capacidad antes de modificarla.
- Mantener reglas, estado y autorización en un único propietario; extender sus contratos antes de crear otro módulo.
- No añadir manejadores por frase, routing de intención por keywords/regex ni decisiones de autoridad por similitud.
- Documentar entradas/salidas, dependencias, invariantes, cancelación y errores; implementar el cambio mínimo coherente.
- Comparar extensión del módulo actual, abstracción compartida y nueva capacidad; evitar frameworks o capas especulativas.
- Validar aislamiento, compatibilidad y comportamiento real. Conservar fallos y experimentos rechazados; no confundir pruebas de componente con fiabilidad general del agente.

Esta guía complementa las instrucciones globales y no sustituye permisos, políticas de seguridad ni instrucciones explícitas del usuario.
