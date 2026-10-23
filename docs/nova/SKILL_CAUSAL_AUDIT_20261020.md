# Revisión del método: propietarios, autoridad y pruebas

20 de octubre de 2026. Alcance: mejorar los dos skills solicitados y comprobar sus
recursos/validador. **No es una corrección ni aceptación del agente.**

## Deficiencia del método anterior

Las guías ya pedían causa raíz, SOLID, contratos y verificación. Faltaba exigir la
evidencia que une esas declaraciones: quién es autoridad sobre cada valor, qué
transformaciones tiene permitidas cada propietario y si el oráculo de una prueba
acepta precisamente el resultado incorrecto. Extraer clases y ejecutar pruebas
compatibles con una regla equivocada no detecta esa regla.

Se actualizan únicamente el skill modular del repositorio y el skill adaptativo
global indicado por el usuario. Se conserva el resto de sus instrucciones. Skill
Creator guía el alcance y la validación; no se modifica ese skill.

## Cambios operativos

- Antes de rediseñar: recorrer una ruta real, su composición activa y sus valores,
  con archivo/símbolo y entradas/salidas; marcar lo desconocido sin inventarlo.
- Para cada valor disputado: procedencia, autoridad, escritores permitidos,
  derivaciones, consumidor y postcondición. Incluir normalizadores, defaults,
  compilación, reparación y binding; no limitar el diagnóstico al LLM.
- Auditar el oráculo: distinguir requisito del usuario, seguridad, compatibilidad
  y supuesto de implementación. Conservar los controles válidos y documentar
  explícitamente un cambio de política equivocada.
- Antes de una variante: mecanismo causal, alternativa, predicción refutable,
  control positivo/negativo, consumidores del mismo invariante y límite de intento.
- Separar errores deterministas, contexto, recuperación, decisión del modelo,
  contenido, efecto y finalización. JSON válido y un resumen convincente no prueban
  significado, autorización ni cumplimiento.
- Elegir recursos para agentes/RAG por su función observada; una base vectorial,
  otro juez o más interfaces requieren una deficiencia que realmente puedan resolver.
- Aceptar sólo el alcance demostrado; identificar dependencias no alcanzadas y
  distinguir defectos antes ocultos de regresiones introducidas por el cambio.

No se fija que el texto sea siempre autoridad sobre un esquema ni lo contrario.
La dirección depende del dominio y debe quedar acordada y verificada. Un formulario
derivado de un esquema o un default explícito siguen siendo diseños legítimos.

## Aplicación a evidencia existente (inspección, no nueva campaña)

| Evidencia | Conclusión permitida | Decisión del método |
|---|---|---|
| `DeterministicPromptEvaluator.Canonicalize`, `Preparation.cs`, añade `<campos>` cuando sólo existen en `RequiredInputs`; `PreparationTests` espera esa adición | **CONFIRMADO:** una transformación determinista modifica el cuerpo a partir de metadatos. No hace falta otra inferencia para demostrar esa transformación. Su legitimidad requiere el contrato del dominio y el encargo real. | Revisar autoridad y expectativas antes de intentar corregir al generador. Los nuevos tests RED conservados no equivalen a corrección implementada. |
| `latency-1792545751556`, `create`: propuesta y formulario exactos; aparecen agenda/objetivos/acuerdos como campos añadidos | El control UI pasa, pero no acredita fidelidad de entradas. El estado `COMPLETED` no cierra autoría. | Comparar petición, propuesta, normalización y resultado; no usar el autoveredicto del revisor como único oráculo. |
| Misma tanda, `saveVersion`: el paquete contiene `persistenceReviewOptions`; la respuesta elige `REVIEW_PERSIST_COPY` y se persiste una copia | **CONFIRMADO:** la guía está presente y esa variante sigue fallando. La hipótesis de que bastaba añadir descripciones es insuficiente. La causa de elegir la operación equivocada sigue sin demostrarse por completo. | Conservar FAIL; no promover la variante por sus 198 pruebas controladas ni añadir otra guía sin una observación discriminante. |
| `latency-1792545159985` y `navigation-review.json`: los tres recorridos de navegación pasan con contexto de referencia | Mejora acotada de navegación; no acredita autoría, persistencia ni toda petición libre. | Mantener la distinción entre el bloque demostrado y la versión global. |
| Contraste de diseño: formulario cuyos campos están definidos por un esquema de dominio aprobado | Derivar los campos desde ese esquema puede ser la responsabilidad correcta del renderer. | El nuevo método permite esa derivación explícita; no generaliza el fallo de NOVA a una prohibición universal sobre metadatos. |

Esta lectura aplica las instrucciones nuevas a artefactos conservados. No es una
evaluación independiente de otro agente ni demuestra fiabilidad futura del skill.

## Validación ejecutada

- Checker local: estructura básica y **8 recursos locales** del skill modular,
  **1 recurso local** del skill adaptativo, correctos.
- **6 comprobaciones ejecutables del checker**: acepta recursos existentes,
  rechaza referencia ausente y nombre incorrecto, rechaza un archivo existente
  fuera del skill, admite ambas raíces reales y conserva la llamada sin argumentos.
- **9 archivos de producto/contratos/tests comparados por SHA256 sin cambios** en
  esta intervención. No se ejecutaron nuevas inferencias ni pruebas de producto.
- El validador oficial `quick_validate.py` de Skill Creator se intentó en ambas
  raíces y no pudo iniciar porque falta `PyYAML`. No se instaló ni descargó nada.
  El checker alternativo declara su alcance básico; no sustituye validación YAML
  completa ni demuestra decisiones correctas del agente.
- Los procesos de los recorridos previos terminaron; los puertos propios 5236/5237
  ya no estaban escuchando al comenzar la edición de los skills.

Resultados técnicos: `.playwright/skills-causal-audit-20261020/validator-results.json`.
Copias anteriores: `.playwright/skills-causal-audit-20261020/before`.

La versión permanece abierta. Las hipótesis y modificaciones de producto del turno
anterior conservan su estado experimental/no aceptado donde corresponda. Actualizar
las guías no convierte esas modificaciones ni sus fallos en requisitos cerrados.
