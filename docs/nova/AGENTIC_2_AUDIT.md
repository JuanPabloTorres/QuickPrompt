# NOVA Agentic Engineering 2.0 — auditoría inicial

Fecha: 2026-10-16. Base recuperable: `424414e` en `main` y `origin/main`. Árbol limpio al comenzar. No se consultaron las bases personales ni la credencial histórica.

## Arquitectura observada

React `PreparationCanvas` consume SSE de `/nova/prepare`. `AgentOrchestrator` coordina `PreparationInference`, `SqlitePromptRetriever`, `GroundedVariableResolver`, `DeterministicPromptEvaluator` y `PreparedDraftStore`. La infraestructura comparte el compilador y `LocalStore` existentes; SQLite guarda documentos JSON transaccionales y versiones. `/nova/command` resuelve navegación y edición clara antes de invocar el modelo. Las propuestas de edición se aplican únicamente al pulsar Guardar versión con la revisión original.

Ollama está limitado a loopback. El intérprete propone una sola llamada tipada `search_prompts`, con un fallback JSON acotado; el generador usa JSON Schema con IDs enumerados. No existe SQL, shell ni HTTP arbitrario a disposición del modelo. El ejecutor local existente ofrece streaming y cancelación. El registro de proveedores externos permite copiar y abrir; no enviar.

## Matriz inicial

| Capacidad | Estado observado | Evidencia / carencia |
| --- | --- | --- |
| Abrir, modificar y guardar versión | Existe y reproducida | Edge real + Qwen3, 7 casos en `docs/evidence/agentic-2/baseline/acceptance.json` |
| Cancelar modificación | Existe y reproducida | Misma revisión y número de versiones tras cancelar |
| Compilar y completar variables | Existe | Compilador de dominio y tests de preparación; evidencia literal y defaults autorizados |
| Borradores recuperables | Parcial | Contenido opt-in y recibos hash; falta checkpoint de ejecución interrumpida |
| Orquestación finita | Parcial | Secuencia fija; presupuesto 12 mezcla interpretación, validación y compilación duplicada |
| Recuperación y desambiguación | Parcial | OR SQL acotado y títulos; grupos recuperan pero no puntúan; no filtros de preparación ni métricas Recall/MRR |
| Planificación multietapa | Falta | Intención única; no encadena adaptación y ejecución explícita |
| Contexto de sesión | Falta | Valores de formulario conservados en React, pero referencias como «ese prompt» no llegan al workflow |
| Evaluación independiente | Parcial | Eval determinista separada; faltan restricciones explícitas, una revisión acotada y diagnóstico de ambos borradores |
| Herramientas | Parcial | Tipadas, timeout y cancelación; registro incompleto para nuevas operaciones |
| Evaluaciones | Parcial | 60 casos de intención; no prueba tareas completas ni mide retrieval |
| Observabilidad | Parcial | Duraciones por herramienta sin contenido; faltan RunId/SessionId/plan/checkpoint exportable |
| Preferencias | Parcial | Configuración explícita existente; no se infieren preferencias ni se guardan conversaciones |

## Reproducción previa a implementar

Compilación aislada `.playwright/agentic-baseline`, sin instalar ni restaurar paquetes. `Tests/nova/local-commands.cjs --headed --record` con `NOVA_TEST_BUILD` y `NOVA_EVIDENCE` separados de evidencias anteriores: 7/7 PASS. Verifica navegación mediante worker cerrado, títulos ambiguos, modificación real con Qwen, original intacto antes de guardar, revisión 1→2 y dos versiones tras confirmación, cancelación sin efectos, aclaración y foco a 390 px. SQLite sintética en `.playwright/commands-*`; sin uso de datos personales.

## Evolución y riesgos

Extender el orquestador y sus adaptadores, manteniendo rutas y contratos compatibles. Checkpoints nuevos como clases de documento SQLite aditivas; sin alterar tablas públicas ni migrar la biblioteca. Cinco herramientas por solicitud por defecto, interpretación acotada y validación/compilación sin duplicación. Mantener sesión temporal separada de biblioteca y contenido recuperable opt-in. Un checkpoint interrumpido no repite ejecución ni escritura; exige una nueva operación o recupera el resultado conservado.

Riesgos principales: ambigüedad de referencias, un modelo que elija IDs ajenos, pérdida de restricciones al adaptar, replay tras desconexión, contexto excesivo y falso PASS de evaluaciones. Verificar cada uno con asserts sobre datos persistidos, casos sintéticos separados y Qwen real. Comparar FTS5/BM25 con el SQL actual antes de añadir índices o embeddings. Las métricas semánticas son mediciones, no certificación de calidad.
