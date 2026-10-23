# NOVA: flujo actual

**QuickPrompt NOVA 1.0.0**, aceptada por el usuario también después de las correcciones. [Cierre r1](../releases/NOVA_1.0.0_R1.md) y [acta inicial](../releases/NOVA_1.0.0.md). Esta guía describe el flujo por acciones; los informes fechados conservan investigaciones y resultados anteriores.

## Uso

Abre el asistente NOVA y elige la acción antes de escribir los detalles. Crear y actualizar producen contenido revisable; Buscar por significado conserva tu descripción original. Otras acciones permiten abrir, completar, preparar, ejecutar localmente o copiar. Los guardados y las eliminaciones mantienen sus confirmaciones e identidades.

Los textos de una plantilla y los datos de sus campos no autorizan operaciones adicionales. Preparar, copiar, ejecutar, guardar y enviar son efectos diferentes. Los sitios externos pueden requerir intervención manual; preparar un texto no demuestra que se envió.

## Desarrollo

- [Arranque y estructura](../../README.md).
- [Propietarios y contratos](../ARCHITECTURE.md).
- [Formularios y componentes compartidos](../development/UI_COMPONENTS.md).
- [Checks y mantenimiento](../development/DEVELOPMENT.md).
- [Adaptador de Core](../../agents/nova-core/README.md).

La selección inicial usa `LiveCommand`. Si no hay elección, `ActionChoiceInterpreter` solicita una aclaración sin buscar ni inferir efectos. Core se usa donde se requiere contenido o recuperación/selección semántica; el dominio autoriza y verifica los efectos. Los decodificadores retirados sólo pertenecen a Evaluation.

## Evidencia anterior

[Auditoría de uso real del 23 de octubre](AUDITORIA_USUARIO_REAL_20261023.md): biblioteca manual y tareas útiles comprobadas, con cinco hallazgos conservados. La [reparación y revalidación posterior](CORRECCION_AUDITORIA_20261023.md) corrige sus mecanismos; mantiene límites adicionales de fidelidad del modelo. No constituye certificación universal.

[Acta técnica del ciclo comprobado](CIERRE_TECNICO_ACCIONES_20261022.md), [registro de continuación](CONTINUACION_QUICKPROMPT_20261022_01A1645A.md) y [matriz histórica ampliada](VERSION_CLOSURE_MATRIX.md). REL-09 se cerró por el testimonio directo del usuario, registrado en el release. Los resultados históricos fallidos no se reescriben como PASS.
