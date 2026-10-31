# Reglas de ingeniería de QuickPrompt

Leer [documentación vigente](docs/README.md), [propietarios](docs/ARCHITECTURE.md) y la
[skill local de arquitectura](.agents/skills/universal-modular-architecture/SKILL.md)
según el cambio. Aplicar `algorithmic-engineering`; para IA, `adaptive-ai-systems`.
Las reglas concretas de frontend están en [apps/web/AGENTS.md](apps/web/AGENTS.md).

- Conservar el stack, los puertos y el propietario de cada capacidad. Comparar extensión, abstracción pequeña y nueva capacidad; elegir la solución mínima con consumidores reales.
- UI compone servicios; endpoints transportan; aplicación/dominio validan identidad, revisión, permisos y efectos; adaptadores realizan E/S. No copiar reglas o persistencia por página.
- NOVA usa elección explícita de acción e IA para contenido/búsqueda. No reactivar el decoder libre retirado ni añadir routing por keywords/regex o autoridad por similitud.
- Preservar estados pendientes, cancelación, aprobación y recibos. Un estado COMPLETED, JSON válido o cita existente no demuestra resultado ni autorización.
- La aceptación inicial está en el acta 1.0.0. Informes históricos son evidencia fechada, no instrucciones nuevas. Mantener la guía propietaria, sus referencias y el índice; retirar duplicados obsoletos sin borrar fallos.
- Ejecutar checks pertinentes de [desarrollo](docs/development/DEVELOPMENT.md). Reportar ejecución real y límites; checks estáticos no certifican IA ni sustituyen el navegador.
- No usar bases privadas en QA, ampliar proveedor/presupuesto o instalar dependencias por conveniencia. Commit, push, tag y publicación requieren petición para ese trabajo.

Estas reglas complementan las globales y no conceden permisos adicionales.
