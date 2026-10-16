# Uso cotidiano — primeras correcciones

16 de octubre de 2026. Prioridad confirmada: probar tareas reales y corregir fricción. **La primera tarea concreta del usuario sigue pendiente de recibir**; los resultados de esta página proceden de una reproducción técnica con datos propios, no de participantes humanos ni de una biblioteca privada.

## DU-001 — recordar dos palabras no encontraba la plantilla

**Estado: PASS de corrección. Severidad: problema importante de usabilidad.**

Con la plantilla «Mensaje para coordinar una reunión», buscar `REUNION mensaje` devolvía cero resultados. Obligar a recordar una frase exacta dificultaba encontrar y reutilizar. [Observación antes](evidence/daily-use/search-before/search-observation.json), [captura antes](evidence/daily-use/search-before/search.png).

Causa: tanto PromptSearch como la consulta SQLite buscaban la consulta completa como una subcadena continua. La normalización de acentos existía, pero no separaba los términos. Los espacios adicionales y cambiar el orden impedían la coincidencia.

Corrección pequeña: normalizar y separar por espacios en blanco, ignorar términos repetidos y exigir cada término literal en título, descripción o contenido. SQLite usa parámetros por término y conserva COUNT/LIMIT/OFFSET, filtros, orden y límites de consulta. El lanzador CtrlK usa el mismo catálogo. La consulta de varias palabras admite ahora distintos órdenes y campos; no se incorpora búsqueda semántica, fuzzy matching ni procesamiento por IA. Los caracteres `%` y comillas siguen siendo literales.

Solo se modificaron dos archivos de producción: PromptSearch.cs y OrganizationStore.cs. No cambian interfaz, modelos, compilador, contrato CompiledPrompt, esquema SQLite, backup, permisos MV3 ni dependencias.

## Verificación ejecutada

| Comprobación | Resultado |
|---|---|
| Caso original en Edge155, modo normal sin Demo | FAIL antes: cero resultados; PASS después: una plantilla correcta |
| Doce regresiones .NET nuevas antes de corregir | Ocho fallaban y cuatro controles pasaban; [log](evidence/daily-use/search-before/dotnet-tests.log) |
| Suite .NET completa tras corregir | **127/127 PASS**, cero omitidas; [log](evidence/daily-use/search-fix/dotnet-tests.log) |
| Buscar → seleccionar → completar → copiar → cambiar valores → copiar | PASS clipboard exacto, incluido móvil390; [E2E](evidence/daily-use/search-verified/result.json) |
| CtrlK/Enter y reinicio real | PASS mismas reglas de búsqueda; texto original y versiones intactos |
| Recorridos diarios previos A–F/I/J | PASS; [regresión](evidence/daily-use/regression/goal05-users/users.json), Ollama inaccesible, fallback externo con targets locales |
| Grupos/perfiles/combinaciones/backup/reinicio | PASS; [organización](evidence/daily-use/regression/goal04-organization/organization.json) |
| Identidad de campos y borradores | PASS; [regresión](evidence/daily-use/regression/goal04-input-state/input-state.json) |
| Datos/contratos protegidos y evidencia anterior | [Comprobación de preservación](evidence/daily-use/search-fix/preservation.json) |
| Servicio normal después de aplicar | [HTTP200, sin leer cuerpo; listeners loopback](evidence/daily-use/search-fix/normal-start.json) |

[Captura de búsqueda corregida](evidence/daily-use/search-verified/search.png) y [copia en móvil](evidence/daily-use/search-verified/mobile-copy.png), inspeccionadas además de las aserciones.

Una primera ejecución después de corregir pasó búsqueda/copia/móvil, pero falló al comprobar el conteo inmediatamente tras reiniciar, antes de que Blazor aplicara la búsqueda. [FAIL conservado](evidence/daily-use/search-after/result.json). El harness ahora espera el cambio visible del conteo; conserva el caso, los datos y la aserción de un único resultado. Los hashes del script antes/después están en la evidencia; no se afirma que el archivo sea idéntico. La reproducción inicial falla antes de alcanzar ese paso de reinicio.

No se ejecutó otra evaluación de Qwen ni otra prueba autenticada de proveedores: sus códigos y modelos no cambiaron. Los PASS externos de regresión corresponden únicamente a fallback con targets locales. No se revalidan como humanos los tiempos automáticos. El piloto de cinco personas continúa **NOT_TESTED**.

## Reproducir

Tooling ya disponible, sin instalaciones. Puerto5189 libre; el harness crea y cierra su propio proceso, perfil y DB. Puede mantenerse la aplicación habitual en5188. Desde la raíz:

```powershell
dotnet test src/QuickPrompt.Web.sln --no-restore
$run = 'docs/evidence/daily-use/rerun-' + (Get-Date -Format yyyyMMdd-HHmmss)
foreach ($case in @('daily-search','goal05-users','goal04-organization','goal04-input-state')) {
 $env:QP_EVIDENCE_DIR = "$run/$case"
 node "Tests/e2e/$case.cjs"
 if ($LASTEXITCODE -ne 0) { throw "Falló $case" }
}
Remove-Item Env:QP_EVIDENCE_DIR
```

El build puede requerir detener la instancia propia para reemplazar binarios en Windows; volver a iniciarla después con `dotnet run --project src/QuickPrompt.Web --no-restore`. No detener procesos ajenos ni usar DB privada como destino de pruebas. La copia de los dos archivos previos está en `.playwright/daily-search-source`; no contiene datos del usuario. Los hashes de fuente de GOAL05 corresponden a esa entrega histórica: esta corrección modifica explícitamente PromptSearch.cs y OrganizationStore.cs, sin reescribir aquella evidencia.

Siguiente paso: recibir la tarea real y observar el resultado esperado, dificultad y corrección concreta mediante la [guía del piloto](DAILY_USE_PILOT.md). No añadir funciones para justificar una versión.
