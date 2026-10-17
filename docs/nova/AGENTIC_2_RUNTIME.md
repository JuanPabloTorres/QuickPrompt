# NOVA Agentic Engineering 2.0

Evolución de `AgentOrchestrator`, sin otro chatbot ni pantallas de agentes. La biblioteca, el compilador, las revisiones, el editor local, React y el Living Canvas existentes siguen siendo las bases del recorrido.

## Uso desde la entrada principal

- «Busca mi prompt de arquitectura y ábrelo»: navegación determinista si hay un único título compatible. Los resultados ambiguos requieren elección.
- «Busca mi prompt de arquitectura, adáptalo para una API .NET y déjalo listo para utilizar»: recupera una fuente real y prepara una copia. Si hay varias fuentes o faltan insumos esenciales, pregunta.
- «Actualiza ese prompt para revisar seguridad, pero no guardes todavía»: usa el origen de esta sesión y conserva el original. «Ese/este prompt» se refiere al seleccionado; «el que tengo para cartas» busca en la biblioteca.
- «Completa este prompt con los datos que ya te proporcioné»: usa valores explícitos de esa fuente y sesión, o defaults autorizados. Nunca rellena datos por conjetura.
- «Ejecuta este prompt con Qwen»: ejecución local explícita con respuesta progresiva y Detener. Preparar por sí solo no ejecuta.
- «Prepara este prompt para ChatGPT»: prepara el texto y el destino. **Usar en…** ofrece copiar y abrir; pegar y enviar siguen siendo decisiones del usuario.

El resultado permanece en el modal que se puede cerrar y volver a abrir. Copiar y Usar en… requieren compilación completa. Guardar una adaptación ofrece **Guardar como copia** o **Confirmar nueva versión**. La segunda opción comprueba la revisión de origen y conserva el mismo ID y su historial. Los valores de la tarea no se convierten automáticamente en defaults. La conservación de borrador, valores y respuesta local es opcional y se indica expresamente en la casilla; no se guarda una conversación continua.

## Runtime finito

`RECEIVE → INTERPRET → RETRIEVE → PLAN → EXECUTE_TOOLS → COMPOSE → VALIDATE → READY`, con salidas `NEEDS_CLARIFICATION`, `AWAITING_APPROVAL`, `FAILED`, `CANCELLED` y `BLOCKED`.

Cinco llamadas a herramientas por petición por defecto (`QuickPrompt:Preparation:MaxSteps`, rango 3–10), 90 segundos por etapa y 180 segundos totales. Interpretación: una propuesta de herramienta tipada y un único fallback JSON. La búsqueda literal puede interpretar una consulta una vez si no encuentra coincidencias relevantes. Generación/revisión: una única revisión automática, dentro del presupuesto. No hay recursión ni bucles de optimización. Si una revisión consume el último paso, la ejecución posterior requiere otra acción desde Usar en… y el resultado lo indica.

`PromptPreparationPipeline` separa extracción de requisitos, selección, generación, resolución, compilación, validación y preparación final. El generador no es el evaluador. Las adaptaciones reutilizan el editor Ollama existente, que conserva nombres de variables y exige una reescritura real; código liga ID y revisión. La petición de edición se distingue del texto fuente en el contrato de inferencia. Las creaciones usan JSON Schema y candidatos reales. La evaluación comprueba estructura, identidad, marcadores del parser, límites, restricciones literales, instrucción de idioma/formato, contexto explícito solicitado y cambios reales de una adaptación. No interpreta HTML o código aportado como una variable nueva. El material tras rótulos de entrada no se convierte en requisito. Estas comprobaciones son conservadoras y no certifican semántica arbitraria ni ausencia universal de invenciones; las evaluaciones y la revisión humana siguen siendo necesarias.

## Recuperación y memoria

SQLite Documents v2 recibe clases aditivas `nova-run`, `nova-run-receipt` y `nova-preferences`; no cambia la versión de esquema ni se abre la base privada de MAUI.

Cada checkpoint contiene RunId, SessionId, intención clasificada, estado, plan, IDs/revisiones recuperados, herramientas/resultados/duraciones, presupuesto, aprobación, error tipado, referencia al resultado y contrato/modelo. No guarda la solicitud ni el contenido en registros generales. Los checkpoints interrumpidos quedan bloqueados al reiniciar y no repiten efectos. Recuperar contenido completo tras reiniciar requiere conservar el borrador de forma explícita.

Los recibos mínimos de idempotencia no desaparecen al borrar diagnósticos ni al restaurar la biblioteca; borrar metadatos no concede permiso para repetir una ejecución. La restauración detecta nuevos recibos o preferencias desde su vista previa. El respaldo físico SQLite conserva los documentos operativos. JSON de biblioteca mantiene su contrato anterior: no exporta borradores, contexto ni preferencias de agente. La restauración repone la biblioteca y limpia ese contexto persistido, pero conserva recibos de no repetición.

La sesión temporal conserva el origen, el último borrador y valores autorizados durante hasta dos horas y 100 sesiones; no guarda conversaciones. Los borradores de diagnóstico inicial/revisado también son temporales y privados. La cancelación natural descarta el borrador pendiente de esa sesión y no revierte versiones aprobadas. Detener una generación conserva el resultado anterior.

En **Datos y respaldos → Preferencias de NOVA** se pueden confirmar, revisar o eliminar idioma, formato y destino. No se infieren preferencias. La solicitud explícita tiene prioridad. El diagnóstico exportable está en el apartado de desarrollo de esa misma ventana y excluye texto, valores y huellas de solicitud.

## Herramientas y permisos

Lectura: `search_prompts`, `get_prompt`, `get_prompt_revision`, `list_groups`, `inspect_variables`. Preparación: `propose_new_prompt`, `propose_prompt_update`, `resolve_variable_values`, `compile_prompt`, `validate_prompt`. Capacidades controladas: `create_recoverable_draft`, `prepare_local_execution`, `execute_local_llm`, `prepare_provider_transfer`.

El registro comprueba tipos C#, esquema sin argumentos extra, permisos, presupuesto, timeout y CancellationToken. No expone SQL libre, shell, HTTP arbitrario ni envío externo. El modelo solo propone la consulta de lectura y los contenidos admitidos; no elige permisos ni escrituras. Guardar y ejecutar pasan por los límites de dominio y recibos correspondientes.

## Recuperación y medición

SQL léxico existente con filtros por colección, categoría y favoritos, puntuación por título/cuerpo/grupo, seis candidatos como máximo, IDs/revisiones reales y detección de contenido duplicado sin colapsar identidades. Una expansión bilingüe acotada mejora términos frecuentes. Cuando una búsqueda no tiene coincidencia relevante, una selección local por significado puede elegir exclusivamente entre esos candidatos reales; dos o más elecciones producen una aclaración. No busca fuera de esa ventana ni inventa registros ausentes. La comparación FTS5/BM25 se ejecuta sobre un índice en memoria de fixtures, sin migrar la biblioteca. Embeddings y almacenes adicionales quedan fuera hasta demostrar una mejora relevante en el benchmark.

[Auditoría inicial](AGENTIC_2_AUDIT.md) · [Evaluaciones y umbrales](AGENTIC_2_MEASUREMENTS.md). Las evidencias de Qwen real y los fallos con Ollama simulado están separadas y nunca se intercambian como prueba de calidad.
