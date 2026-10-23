# NOVA: mapa y análisis de latencia

## Recorrido real

`PreparationCanvas → POST /nova/live → AgentOrchestrator.RunVisible → SemanticPlanning.Interpret → SemanticPlanning.Resolve → SQLite → interpretación con el origen recuperado → browser.Start (SameTabUiDispatcher) → observación React → decisión de herramienta → autorización → acción DOM → recibo → VerifyVisible → revisión humana o resultado.`

`ui_prepare_update` pulsa el control real del editor. Ese control llama a `/nova/propose`; `NovaEndpoints.Observed` vincula la inferencia hija con el RunId autorizado. La creación invoca el pipeline de generación y escribe título/contenido en el formulario. Ejecutar localmente usa `/nova/drafts/{id}/execute` y streaming. Ninguno de estos caminos publica ni envía a proveedores externos por sí mismo.

## Inventario de consultas al modelo

En `PreparationInference` / `SemanticInference`:

| Consulta | Motivo | Condición |
|---|---|---|
| ResolveReference | Base de la tarea: selección, conversación, candidato u origen nombrado | Sin selección explícita |
| ExtractNamedReference | Consulta literal de recuperación | Referencia nombrada |
| InterpretDestination | Destino y preparación frente a ejecución | Interpretación |
| ReviewExecutionScope / ReviewPresentationTarget | Atribuir permiso a la directiva, no a título/datos | Destino propuesto |
| InterpretTextGoal (+ una reparación acotada) | Revisar/crear/explicar/usar sin editar, con evidencia literal | Interpretación |
| ReviewAuthoringScope | Segunda clasificación independiente de autoría | Autoría propuesta sobre un origen |
| InterpretInputUse | Completar parámetros frente a autoría o prohibición | Interpretación |
| InterpretWorkflow | Lectura, controles y guardado | Interpretación |
| AdditionalArtifactRequested | Nueva copia frente a modificación del artefacto actual | Autoría/contexto relevante |
| ExplainAdvice | Respuesta explicativa sin propuesta | Consejo |
| ExtractContentCore | Restricciones de contenido | Autoría |
| ReviewContentConstraints | Comprobar idioma/formato explícitos | Restricción propuesta |
| ReviewTargets / ReviewClauseScopes | Distinguir tema, instrucción al editor e invariantes literales | Objetivos/cláusulas candidatos |
| Select | Elegir IDs exclusivamente entre candidatos recuperados | Recuperación no inequívoca |
| Generate | Propuesta estructurada nueva | Creación |
| ExtractInputs | Valores citados en datos autorizados | Valores pendientes |
| Decide (+ un reintento de contrato) | Una herramienta del catálogo permitido | Estado observado |

El rebind de un origen nombrado repite parte de la interpretación: antes no se conocían su cuerpo ni sus variables. No se elimina esta comprobación basándose en palabras de la petición.

En `OllamaClient`: `Propose` genera o adapta y puede solicitar una única corrección; `Analyze`, `Suggest`, `PlanAssist` sirven las acciones explícitas heredadas. `Execute` transmite el resultado local. `NovaInference`/`NovaAssistant` delegan en ese cliente. Las consultas `Available`/`ReuseIdentity` leen metadatos (`/api/tags`); no son inferencias. Copiar, compilar, seleccionar proveedor, CRUD confirmado y validar contratos son deterministas.

## Medición inicial

- Primer intento frío: 133.183 s, 21 llamadas completadas, fallo `UI_TARGET_TIMEOUT`; carga inicial 26.939 s. Hubo un build durante ese diagnóstico: se conserva, pero no es una comparación limpia.
- Repeticiones residentes de la petición obligatoria: 134.430 s y 161.434 s, ambas listas para revisión, con 25 llamadas cada una. El texto del formulario coincidió con el observado; el original permaneció intacto.
- En la primera repetición residente, las interpretaciones con thinking consumieron 33.917 s + 15.806 s; el editor, 20.589 s. SQLite midió 149 ms en la primera recuperación y lecturas posteriores de 8–13 ms.

Los mayores costes observados son la generación de razonamiento en clasificaciones estructuradas, las clasificaciones secuenciales repetidas al cargar el origen y la generación del editor. Las esperas de escritura son menores, pero eliminables. No se atribuye el tiempo completo al LLM ni se suman duraciones anidadas como si fueran independientes.

Una paráfrasis A0 abrió el origen sin aplicar el cambio, pese a terminar `COMPLETED`. La evaluación comprueba el texto y el propósito, no solo ese estado. Esta observación motiva evaluar la composición de autoría; no justifica excepciones por frase.

## Cambios acotados y efectos observados

1. La interpretación TextGoal permite comparar `think:false` sin retirar el razonamiento del editor o de referencias complejas. Las demás clasificaciones estructuradas ya usaban `think:false`. El A1 aislado completa el principal en 90504 ms frente a las dos observaciones A0 de 134430/161434 ms; faltan repeticiones para inferir p95.
2. El plan ya resuelto puede buscar el título real, abrir solo su ID/revisión visible y llenar variables con valores fundamentados. Cada decisión atraviesa el mismo filtro de herramientas, validación, límites, recibo y verificación. Si falta una identidad/valor conocido se vuelve al modelo. Nunca se interpreta una frase mediante palabras clave. Esto evita el bucle observado de búsquedas con datos de variables añadidos al título.
3. Cuando el estado verificado solo permite terminar o pedir revisión, no se pide a Qwen otra decisión de finalización. Los eventos atribuyen esa elección a código (`RULE_SELECTED`), sin fingir una elección del modelo. Un borrador sigue esperando aprobación humana.
4. La lectura por ID evita ranking innecesario; la UI rápida escribe cada valor nativo completo, espera resultados actuales y compilación actual. SSE despierta el polling autenticado: nunca transporta una acción ejecutable por sí solo. Hay fallback acotado y recibos idempotentes; perder una confirmación no repite efectos.

En A7 conservando semántica (`latency-1792286706220`), el caso principal usa 22 llamadas frente a 25 en A0; 18255 prompt tokens / 1496 output tokens frente a 22402/2237 y 22435/2460. Tiempo LLM observado: 64843 ms de 69491 ms totales. Primera acción reconocida: 33442 ms frente a 93602/90035 ms A0. El cursor real del navegador aparece a 33913 ms; A0 no tenía esa sonda, por lo que no se equiparan ambas mediciones. Son muestras pequeñas, no estimaciones de ganancia poblacional.

No se elimina la segunda interpretación al recuperar un origen: aporta texto y variables que la petición inicial no tenía. Los intentos de modificar revisiones semánticas/contexto no conservaron la calidad y se retiraron. No se usa el holdout para diseñar nuevas instrucciones.

La selección final conserva `SemanticThinking=true`: el holdout detectó una regresión de consejo al desactivarlo (8/16 frente a 9/16 base). En el recorrido funcional ampliado, el principal final tarda 118986 ms, con 22 solicitudes y el contenido exacto en el formulario. Se preserva la reducción determinista de tres decisiones, pero no se presenta el tiempo de 69491 ms del perfil rechazado como rendimiento del producto final. Los costes dominantes siguen siendo la interpretación semántica y el editor; SQLite y el driver no explican esas decenas de segundos.

## Observabilidad local

`NovaTelemetry` usa ActivitySource/Meter, AsyncLocal para la ejecución y Attach para peticiones hijas. Retiene hasta 64 ejecuciones terminadas y 256 observaciones por ejecución; los contadores siguen contando si se alcanza el límite. No usa exportadores, disco ni contenido del usuario. RunId aparece en trazas/snapshots, nunca como etiqueta de métricas.

`GET /api/v1/nova/live/{id}/performance` permite inspeccionar métricas locales del run existente. `ResultReadyMs` separa el resultado preparado del tiempo humano esperando aprobación. Las duraciones de planning/UI contienen sus operaciones hijas. `ProcessCpuMs` y working set son del proceso completo, no atribución exclusiva al run.

Las llamadas estructuradas no transmiten tokens: TTFT queda desconocido, no se inventa a partir del primer byte. El streaming local registra el primer contenido no vacío. Los cached tokens quedan null si Ollama no los informa. Event delivery del servidor mide escritura/flush; el probe de navegador mide aparte recepción SSE y primera aparición del cursor.

## Caché, concurrencia y recursos

El contexto ya estaba acotado: hasta seis candidatos con ID/revisión y extractos de 2000 caracteres marcados como truncados, extracto de observación para decisiones de hasta 2500 caracteres y solo herramientas disponibles en esa etapa. Los paquetes demasiado grandes se rechazan en vez de cortar valores literales. No se reenvía toda la biblioteca. El origen y la última propuesta de sesión conservan su identidad y revisión, y los cambios de restricciones se revisan con su destinatario. No se redujeron esos límites ni el presupuesto de contexto; los experimentos de cambiar la revisión contextual no pasaron calidad.

Se auditó el memo de inferencia ya presente antes de este trabajo: local al proceso, hasta 64 respuestas estructuradas/4 MiB y cinco minutos, sin conservar thinking. La clave incluye el paquete completo (contrato, herramientas disponibles, estado, revisión, valores y petición) y el digest instalado. Sin identidad de modelo fiable, un resultado inválido/incompleto, cancelación o cambio del paquete, se hace inferencia normal. Una respuesta recuperada sigue pasando por los validadores del dominio y nunca evita observar la UI o pedir aprobación. Los tests verifican misses al cambiar revisión, valores, herramientas, origen, petición y modelo, además de expulsión por capacidad. No hay caché nueva de cuerpos, búsqueda, aprobación, sesión o efectos. Este trabajo no presenta una tasa de hits de sesiones reales; todos los A/B usan `ReuseInference=false`.

El compilador de plantillas sigue siendo determinista. La vista previa se vincula a los inputs actuales en el propio render: un marcador preparado de una versión anterior ya no puede confirmar texto recién escrito. Esto permite retirar sleeps sin aceptar observaciones obsoletas.

Las acciones dependientes permanecen secuenciales. Solo se mantiene una ejecución NOVA activa y la inferencia no se paraleliza. Progreso visual y telemetría son best effort: no bloquean la acción ni su recibo definitivo. Los avisos SSE se coalescen; despertar una consulta no elimina la autenticación de la superficie ni ejecuta lo recibido por SSE.

La memoria GPU de Qwen residente reportada por Ollama es 5225374496 bytes antes/después de los perfiles comparados. No es un muestreo de pico de VRAM. El snapshot NVIDIA de 6185 MiB utilizados incluye aplicaciones ajenas; no se atribuye a NOVA. La presión de RAM y los builds/navegador iniciales limitan las comparaciones frías. No se forzó residencia adicional ni se cambió Flash Attention, KV, partición GPU/CPU o concurrencia global para maquillar resultados.
