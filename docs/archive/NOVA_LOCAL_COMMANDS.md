> HISTÓRICO: este documento describe un perfil retirado. Para el producto actual, consultar [documentación vigente](../README.md).

# Abrir y actualizar prompts desde inicio

La entrada de NOVA ahora resuelve acciones sobre elementos reales de la biblioteca:

- `Ve al prompt «Escribir un correo profesional»` abre el prompt.
- `Edita el prompt «Escribir un correo profesional»` abre el editor de su versión actual.
- `Actualiza el prompt «Escribir un correo profesional» para que sea más breve` solicita a Qwen una propuesta vinculada a ese prompt y abre su editor. **Guardar versión** aplica el cambio al mismo ID y conserva el historial.
- Si se pide actualizar sin describir el cambio, pregunta qué cambiar. Si hay varios candidatos, pide elegir uno. Si no encuentra el elemento, pide su título.
- `Actualiza la biblioteca`, `Ve a favoritos`, `Ve a recientes` y `Ve al archivo` actualizan la lista o el filtro.

## Causa y corrección

La entrada principal llamaba únicamente a `/nova/prepare`: encontraba o generaba una instrucción, pero no despachaba acciones de navegación y edición. El runner Playwright era una herramienta separada de QA. Además, sus localizadores esperaban como máximo 10 segundos por defecto, una espera corta para algunos recorridos con inferencia local.

Se añadió `/api/v1/nova/command`, un resolvedor de lectura con acciones cerradas e IDs recuperados de SQLite. No guarda cambios, no ejecuta texto del modelo y no navega a URLs externas. Los comandos explícitos de navegación se resuelven sin Ollama; las intenciones de generación continúan por el preparador existente.

React conecta la resolución con la navegación y el editor existentes. Actualizar llama a `/nova/propose` con ID/revisión del origen y comprueba ambos en la respuesta. El cambio se guarda mediante el PUT de dominio existente, después de revisar y pulsar **Guardar versión**. Una edición pendiente no se sobrescribe con otra propuesta. La cancelación impide aplicar respuestas tardías.

Se respeta el requisito del SOW de usar la API de dominio para operaciones internas. Playwright comprueba la experiencia en navegador; no se utiliza como herramienta libre del modelo para pulsar controles internos.

El worker admite `locatorTimeoutMs` o `--locator-timeout=90000`, siempre limitado a 1000..90000 ms y al presupuesto total del workflow. Se conservan el valor por defecto de 10 segundos, máximo 20 pasos, límite total de 120 segundos, cancelación, acciones cerradas y allowlist.

## Evidencia

- Backend: **46 pruebas aprobadas**, con navegación por títulos reales, acentos, variantes de peticiones, títulos que contienen «para», ambigüedad, actualización sin mutaciones y archivo.
- Frontend: **19 pruebas aprobadas**, incluidos despacho y cancelación sin generar otro prompt.
- Worker: **3 pruebas de contratos** y [regresión de fallo, cancelación, recuperación y timeout](../evidence/local-commands/runner-regression/automation.json).
- [Edge visible y Qwen real](../evidence/local-commands/acceptance.json): siete comprobaciones, navegación con el worker cerrado, aclaración, propuesta sin guardar, actualización del mismo ID con dos versiones, cancelación de una segunda propuesta, elemento inexistente y móvil.
- [Worker y grabación](../evidence/local-commands/worker/result.json): navegación real desde una petición natural; `recorrido.webm` y capturas en ese directorio.
- [Instancia activa](../evidence/local-commands/active-service.json): API actualizada, misma biblioteca antes/después y sin recargar la pestaña del usuario.

Los recorridos usan SQLite y perfiles de navegador propios, con datos ficticios. No se instalaron dependencias ni modelos. No se alteraron políticas de proveedores externos ni la CSP.

Reproducción, con las dependencias y Qwen ya instalados:

```powershell
dotnet build backend/QuickPrompt.Api/QuickPrompt.Api.csproj --no-restore -p:BaseOutputPath=C:/Projects/Repos/QuickPrompt-Web/.playwright/command-build/
node Tests/nova/local-commands.cjs --headed --record
```
