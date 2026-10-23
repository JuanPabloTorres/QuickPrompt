# Reglas de frontend

Aplicar las reglas raíz y [la guía de UI modular](../../docs/development/UI_COMPONENTS.md).

- Antes de crear un formulario, localizar controles, hooks, servicios y consumidores existentes. Una página compone capacidades; no duplica validación, parseo, persistencia ni autorización.
- `components/ui` contiene presentación accesible independiente del dominio. Los componentes compartidos de prompts pertenecen a `features/prompts`; no introducirlos en un kit genérico si conocen variables o revisiones de prompts.
- Reutilizar `PromptVariableFields` para valores, `Dialog` para modales y los propietarios existentes de preview, recibos, historial y eliminación. No fusionar flujos de copia con efectos diferentes sólo por compartir una llamada al portapapeles.
- El formulario es dueño de sus valores; los campos son controlados y emiten cambios. Un vacío explícito prevalece frente al default. IDs, etiquetas, foco y distinción workspace/diálogo son contratos de interacción del agente.
- Usar `ApplicationServices`/`ServiceContext` y clientes existentes; E/S y contratos fuera de la presentación. Reutilizar snapshots/cancelación para operaciones ligadas a contexto sin duplicar autoridad del servidor.
- Extraer una capacidad pequeña cuando existe conocimiento compartido; conservar diferencias explícitas. No construir un motor universal por flags ni añadir dependencias de formularios o estado sin necesidad demostrada.
- Verificar consumidores reales y comportamiento negativo pertinente, tipos/build y navegador para cambios visuales. Actualizar la guía propietaria cuando cambia su frontera.
