# QuickPrompt ↔ Adaptive Core

Las capacidades de IA de producción usan Adaptive Core. La elección de la acción
es explícita y pertenece a QuickPrompt; Core no decide sus permisos. El motor nativo
anterior y su transporte/cache viven en `backend/QuickPrompt.Evaluation/Compatibility/NativeAgent`;
no se registran ni se empaquetan en la API. `Runtime=native` se rechaza explícitamente.

## Responsabilidades

| Propietario | Responsabilidad |
| --- | --- |
| Adaptive-AI-Core / Python | Inferencia genérica con Pydantic AI, proveedor Ollama, validación JSON Schema, reparación estructural acotada, presupuestos y uso real. |
| QuickPrompt / .NET | Catálogo y esquemas de negocio, petición y contexto autorizado, referencias/revisiones, permisos y aprobaciones, coordinación del caso de uso, ejecución y comprobación de efectos. |
| React / navegador | Controles y observaciones reales; el servidor verifica sus recibos. |

`nova_worker.py` es únicamente un lanzador de `adaptive_core.host`: no conoce
prompts, fuentes, variables, SQL ni operaciones de QuickPrompt. El puente lector
alternativo `bridge.py`/`run.py` fue retirado. El esquema y binding de encargos
pertenecen a `LiveCommand`, `UserActionRequest` y `TaskIntentContract`; el contrato
experimental `QuickPromptCoreContract.cs` fue retirado. Sin elección,
`ActionChoiceInterpreter` solicita elegir sin llamar al modelo ni recuperar datos.
La compilación conserva los invariantes del dominio. El puerto de contenido es `PromptContentInterpreter`, sin
planificación, identidad ni ejecución.

## Instalar el runtime Python

En el repositorio **Adaptive-AI-Core**, no en el entorno Python global:

```powershell
python -m venv .venv
.venv/Scripts/python.exe -m pip install -r requirements-inference.lock.txt
```

El extra opcional `inference` de pyproject.toml declara Pydantic AI Slim 2.59.0 y
jsonschema 4.26.0. No requiere modelos nuevos ni credenciales de nube. No se habilita
telemetría externa. El transporte nativo conserva `num_ctx`, `num_predict` y `think`;
Pydantic AI conserva el bucle de inferencia y reparación. No hay reintento HTTP oculto.

Desde QuickPrompt, declarar explícitamente las rutas de la instalación existente:

```powershell
dotnet run --project backend/QuickPrompt.Api -- `
  --QuickPrompt:Agent:Runtime=adaptive-core `
  --QuickPrompt:AdaptiveCore:Python=C:/Projects/Repos/Adaptive-AI-Core/.venv/Scripts/python.exe `
  --QuickPrompt:AdaptiveCore:SdkRoot=C:/Projects/Repos/Adaptive-AI-Core `
  --QuickPrompt:AdaptiveCore:Worker=C:/Projects/Repos/QuickPrompt-Web/agents/nova-core/nova_worker.py `
  --QuickPrompt:Ollama=http://127.0.0.1:11434
```

El modelo configurado de este proyecto sigue siendo Qwen local según
`LocalModelPolicy`. Si faltan dependencias, la petición IA falla explícitamente y la
biblioteca manual sigue disponible. No se cambia a otro motor. El consumidor
Blazor también inyecta Core en sus operaciones IA.

## Protocolo adaptive-inference/1

Un objeto JSON UTF-8 por stdin y una respuesta por stdout; stderr no es el protocolo.
Campos de solicitud: version, requestId, model, mode (text/structured/tools),
messages, outputSchema y limits; `decoding` opcional selecciona `native` o `prompted`
sin retirar la validación del esquema ni ampliar límites. Los esquemas contienen solo las capacidades
ofrecidas. Los límites admiten 1–2 solicitudes, timeout 1–180 s, contexto y salida
acotados; temperatura y muestreo explícitos. Se prohíben referencias de esquema
externas y endpoints no loopback en este adaptador local.

Respuesta: version, requestId idéntico, status proposed/failed, output, usage
(requests/inputTokens/outputTokens) y error. **proposed nunca significa tarea
completada**. Solo .NET confirma los efectos. Core no abre una base del anfitrión ni
mantiene una segunda sesión de negocio. Los diagnósticos y recibos pertenecen al
run de QuickPrompt; no se crea otra base `.adaptive.sqlite` para esta frontera.

Máximo una reparación de estructura por inferencia, cero replays de escrituras.
La cancelación cierra el hijo propio. Datos, instrucciones de plantillas y respuestas
LLM no conceden permisos. La interpretación semántica incorrecta continúa siendo
un posible fallo aunque el esquema sea válido.

`AdaptiveCoreModelTransport` contiene el codec del puerto interno anterior para
migrar consumidores; no implementa HTTP al modelo, parser de herramientas LLM ni
reparación. La API pública de QuickPrompt y la base de datos no cambian. En esta
versión la ejecución textual vía Core entrega la respuesta final al callback; no
ofrece streaming por token.

## Verificación

```powershell
../Adaptive-AI-Core/.venv/Scripts/python.exe agents/nova-core/test_worker.py -v
# Desde Adaptive-AI-Core:
.venv/Scripts/python.exe -m unittest discover -s tests -q
# Desde QuickPrompt:
dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj
node scripts/Check-ModuleBoundaries.cjs
```

El arnés `Tests/nova/clipboard-agent.cjs --real-qwen --adaptive-core` usa la API,
SQLite y Edge reales con datos sintéticos. El flag adaptive-core ahora comprueba
un segundo contrato de cliente/notas con el mismo runtime, sin el antiguo agente
lector paralelo. Deben seleccionarse build, worker, SDK y Python explícitamente
con NOVA_TEST_BUILD, NOVA_AGENT_OPTIONS, NOVA_CORE_SDK y PYTHON.

Diseño/migración: `docs/nova/CORE_HOST_BOUNDARY_20261021.md`. Fallos conservados:
`docs/nova/ORDERED_AGENT_CLOSURE_20261020.md`. La separación modular y las pruebas
de contrato no declaran cerrada la versión ni sustituyen la aceptación semántica.
