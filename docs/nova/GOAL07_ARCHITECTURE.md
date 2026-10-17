# GOAL 07 — núcleo de preparación

Documento de la entrega anterior. La evolución vigente de presupuestos, checkpoints y memoria está en [NOVA Agentic Engineering 2.0](AGENTIC_2_RUNTIME.md); las evidencias originales se conservan.

La entrada natural de Discover ahora llama a `AgentOrchestrator`; una propuesta llega preparada a React con título automático, texto compilado o los campos que faltan. El editor manual y las APIs anteriores siguen disponibles para la biblioteca. Esta entrega corresponde al hito interno exigido antes de ampliar el worker o las conexiones externas.

## Auditoría inicial

| Evidencia | Hallazgo | Decisión |
|---|---|---|
| `NovaAssistant`, `NovaAssist`, `App` | Clasificar, generar y abrir el editor eran acciones separadas; la creación antigua exigía variables. | Añadir preparación finita e independiente; preservar rutas anteriores. |
| `TemplateCompiler`, `LocalStore` | Compilador determinista, revisiones y transacciones probadas; SQLite usa Documents y organización v2. | Reutilizar servicios y almacenamiento existentes. Sin vectores ni migraciones nuevas. |
| `App.editorDrafts` | Los borradores manuales no sobrevivían al proceso. | Borrador nuevo recuperable con consentimiento; contenido transitorio por defecto. |
| MV3 / worker | Origen anterior 5188; worker genérico cerrado, sin integración al modelo. Claude continúa bloqueado. | No ampliar permisos ni conectarlo al nuevo agente antes de la revisión requerida. |
| Ollama `/api/tags` | Qwen3:8b Q4_K_M instalado, capacidades completion/tools/thinking. | Modelo principal existente; sin descargas ni paquetes nuevos. |

## Flujo y límites

`RECEIVED → UNDERSTANDING → SEARCHING → SELECTING_OR_GENERATING → FILLING → VALIDATING → READY / NEEDS_INFORMATION`.

La preparación termina ahí. Guardar, copiar y ejecutar son acciones de usuario separadas. `USER_ACTION` y `COMPLETED` están reservados en el contrato de estados; no se afirma que exista aún un historial persistente de todo ese ciclo. Cancelar desconecta HTTP y cancela Ollama. Un error conserva el resultado anterior en React; no produce un borrador parcial utilizable.

Los puertos pequeños son `IIntentInterpreter`, `IPromptRetriever`, `IPromptGenerator`, `IVariableResolver`, `IPromptEvaluator` e `IPreparedDraftStore`. Son servicios dentro del mismo proceso. El orquestador recibe opciones de hasta 16 herramientas, timeout por etapa y presupuesto total; valores por defecto 12 pasos, 90 s por herramienta, 180 s total. Una petición posee su registro/presupuesto; la API admite una operación IA simultánea y rechaza otra con 429.

Qwen propone una llamada a `search_prompts`, con intención cerrada, consulta y aclaración. El código valida nombre único, estructura y argumentos antes de consultar SQLite. Si faltan tool calls o sus argumentos no cumplen el esquema, hay un único reintento con salida JSON restringida. Una herramienta desconocida se rechaza sin fallback. La generación tiene un intento; no hay bucle de autocorrección ni planificación ilimitada.

Los contratos estructurados de Ollama se comprueban también localmente, siguiendo [Structured Outputs](https://docs.ollama.com/capabilities/structured-outputs) y [Tool calling](https://docs.ollama.com/capabilities/tool-calling). Temperatura cero, presupuestos de contexto/tokens fijos; no se ejecuta texto libre del modelo.

## Recuperación y decisiones

SQLite busca hasta seis términos por título, contenido, descripción, categoría y colección. Se unen resultados con favoritos y recientes; se entregan hasta seis candidatos reales. La puntuación pondera título/contenido y usa prioridad pequeña por favorito/uso. El origen explícito se verifica por revisión. FTS/BM25 no se introduce sin un corpus que demuestre su necesidad. La recuperación todavía es léxica y su Recall@K general no está certificado.

La propuesta decide `REUSE_EXISTING`, `ADAPT_EXISTING`, `CREATE_NEW` o `ASK_CLARIFICATION`, con razón visible. Reutilizar fija el texto al snapshot real; adaptar nunca cambia el origen. El esquema de generación limita sourceId a los UUID exactos recuperados (al origen seleccionado, cuando lo hay); el validador independiente vuelve a comprobarlos. En guardado, contenido idéntico salvo CRLF y espacios exteriores reutiliza el prompt activo existente; no se usa una puntuación del modelo para autorizar deduplicación.

Las intenciones que dependen de un origen (adaptar, mejorar, completar, guardar, usar o ejecutar) exigen selección explícita o alguna coincidencia léxica relevante. Si solo hay resultados recientes ajenos a la consulta, el código pide identificar el prompt en una pregunta, antes de generar. No se considera el resultado reciente como contexto autorizado implícito.

## Variables y privacidad

`VariableDefinition`, `VariableValue`, `valueSource`, `validationStatus` y `CompiledText` son conceptos separados. Orden implementado: valores explícitos → citas literales validadas de la solicitud → rótulos literales como `Texto: …` → defaults válidos del origen. Una sugerencia requiere evidencia literal dentro de la solicitud y valor literal dentro de esa evidencia. Un rótulo repetido no se resuelve automáticamente. Valores sin evidencia no se incorporan; quedan como campos faltantes. No se infieren datos personales ni se consultan archivos/contexto privado adicional.

El resolvedor también reconoce la etiqueta literal delante de un marcador (`Texto: <documento>`) y equivalencias text/documento para un único insumo de material. Exige una sola extracción inequívoca y no asigna el mismo texto a varios documentos pendientes. Esto resuelve diferencias de nombre introducidas por el modelo sin inferir contenido.

En ranuras de extensión/longitud de resúmenes, una extracción debe expresar longitud o brevedad; una frase como «idea central» no es un valor válido de extensión aunque aparezca literalmente en la solicitud. Los valores introducidos explícitamente por el usuario y defaults existentes conservan su autoridad. Esta comprobación puntual no certifica la relevancia semántica de todos los campos posibles; esa precisión necesita evaluación humana adicional.

Los campos se derivan del compilador C#, que decide los faltantes y el texto final. Si una creación/adaptación declara `requiredInputs` pero omite todos los marcadores, el código añade sus campos de entrada vacíos; nunca inventa su contenido. Si ya hay marcadores, ellos son la autoridad, evitando duplicarlos por sinónimos de metadatos. Reutilizar nunca añade ni cambia campos del origen. Las tareas sin insumos pueden tener cero variables. La corrección semántica de que el modelo declaró todos los insumos sigue necesitando evaluación y revisión humana; no se equipara validez JSON con calidad.

Sin consentimiento, los borradores completos quedan en una caché del proceso de hasta 100 entradas y dos horas; SQLite conserva únicamente el hash de solicitud/operationId. Activar «Conservar este borrador y sus valores en este equipo» permite guardar contenido, valores y candidatos para recuperación. «Olvidar» elimina el contenido, conservando recibos de idempotencia sin texto. Los campos conocidos no se convierten en defaults al guardar la plantilla; una adaptación conserva los defaults compatibles ya guardados en el origen.

Guardar es explícito, transaccional e idempotente, incluso tras reinicio o solicitudes paralelas. Se preservan versiones de plantillas. El borrador completado crea otra identidad y conserva el anterior. «Volver al borrador anterior» recupera hasta veinte resultados anteriores durante esta vista, sin revertir guardados explícitos. Los borradores conservados se recuperan tras reinicio. No hay aún comparación completa de versiones ni árbol persistente de ramas de preparación.

Los nuevos documentos `nova-preparation`, `nova-draft` y `nova-prepared-save` aprovechan Documents v2; no se altera la base original ni el esquema público. El respaldo físico SQLite los conserva. Archive JSON v2 mantiene su contrato anterior y NO incluye borradores/recibos operativos; restaurarlo elimina esos documentos, previo respaldo físico verificado. Recuperar borradores requiere el respaldo físico, no exportar JSON.

## Herramientas y acciones

`PreparationTools` registra contratos tipados, JSON Schema de argumentos, permisos, timeout, resultado y errores `UNKNOWN_TOOL`, `PERMISSION_DENIED`, `STEP_LIMIT`, `TIMEOUT`. Las herramientas activas del núcleo son `interpret_intent`, `search_prompts`, `propose_prompt`, `resolve_variables`, `validate_prompt`, `compile_prompt`. Las cuatro últimas se enlazan al contexto validado de la solicitud, con argumentos vacíos; el modelo no puede sustituir ese contexto ni invocarlas libremente. `save_draft` y `save_prompt` son servicios de dominio controlados y endpoints explícitos; aún no forman una interfaz genérica de herramientas del modelo.

La lista ampliada del SOW (perfiles, organización, recent/get como herramientas independientes, adaptadores generales y browser worker) sigue pendiente. Esta distinción evita presentar nombres documentados como capacidades implementadas. No existen herramientas de shell, archivos arbitrarios, SQL, HTTP externo ni navegador general.

Copiar, Guardar, Usar en… y Ajustar aparecen junto al resultado. Usar conserva la ejecución local con confirmación y el fallback existente de copiar/abrir ChatGPT; no integra APIs remotas ni automatización de terceros. Las respuestas locales parciales no se pueden copiar como completas. La telemetría del orquestador solo contiene operación, herramienta, estado y duración; no registra solicitudes, plantillas, valores o respuestas.

Pendiente: uso/historial del nuevo flujo preparado, perfiles/instrucciones generales en composición preparada, preferencias autorizadas adicionales, inferencias no literales verificables, versiones/undo de borradores, registro completo de herramientas y el worker separado. La copia/ejecución del flujo de biblioteca conserva sus recibos previos; no se afirma paridad de historial para el flujo preparado.
