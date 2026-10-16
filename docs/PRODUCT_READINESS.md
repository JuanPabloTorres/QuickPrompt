# QuickPrompt Web — GOAL 02, 2026-10-15

**Estado: APROBADO para uso local supervisado en .NET 8, con fallos y bloqueos explícitos.** Build, 70 pruebas y regresión integral real en Edge aprobados. GOAL 02 no está aprobado íntegramente: la calidad de Qwen2.5 presenta fallos y las puertas .NET 10/datos privados siguen bloqueadas. No se declara producción comercial ni disponibilidad remota; se conserva el PASS_MVP_LOCAL histórico.

## Decisión por área

| Área | Resultado | Evidencia / límite |
|---|---|---|
| Arranque normal sin Demo | **APROBADO** | DB del perfil inicialmente inexistente; biblioteca vacía, sin seed implícito. Crear/completar/copiar sin Ollama accesible |
| Persistencia tras cerrar y reiniciar | **APROBADO** | Plantilla de trabajo, favorito, historial y versiones recuperados en el perfil normal |
| Datos y restauración | **APROBADO en alcance probado** | Restore exige respaldo versionado, rollback tras fallo real SQLite, preferencias aplicadas inmediatamente, DB futura rechazada sin modificar bytes |
| Uso cotidiano y diseño | **APROBADO** | Diseño conservado, búsqueda sin tildes, Ctrl+K, foco en campos/título, completar/copiar realmente en móvil |
| Dominio/integración/contratos | **70/70 PASS** | 59 existentes y 11 nuevas regresiones; cero omitidas. [Resumen](evidence/goal02/unit-integration-contract.json) |
| E2E integral existente | **PASS_COMPLETE_BROWSER_ACCEPTANCE** | Edge 155, escritorio/móvil: offline, edición/importación/restauración, seguridad, accesibilidad, IA Qwen3 real y recuperación de versiones/respuestas tras reiniciar |
| IA Qwen3 principal | **APROBADO con revisión humana, alcance limitado** | Casos de trabajo autorizados, propuestas sin cambios silenciosos, análisis acotado, cancelación y recuperación |
| IA Qwen2.5 alternativa | **FALLOS de calidad registrados** | v2: JSON/checks pueden aprobar y revisión semántica fallar. v1: 29/30 global; borrador alternativo inválido rechazado, original intacto |
| Migración .NET 10 | **BLOQUEADO** | SDK no instalado; plan incremental/rollback listo. Ninguna compatibilidad declarada sin compilar/ejecutar |
| Importación de datos privados reales | **BLOQUEADO por alcance autorizado** | El usuario eligió continuar sin una copia privada autorizada. QuickPrompt.db3 no se leyó ni modificó |
| Seguridad local / dependencias | **APROBADO en controles ejecutados** | Loopback/Host/Origin, bloqueo de overrides Kestrel, CSP, sin solicitudes externas del navegador; consulta oficial NuGet sin advisories reportados |

La aprobación de uso local no implica precisión factual de toda respuesta IA ni cubre cambios futuros del entorno. .NET 8 llega a fin de soporte el 10 de noviembre de 2026 según [Microsoft](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/); resolver la puerta net10 antes de depender del producto a largo plazo.

## Arranque y datos del usuario

```powershell
dotnet run --project src/QuickPrompt.Web
# http://127.0.0.1:5188
```

No requiere QuickPrompt:Demo=true. Crea `%LOCALAPPDATA%/QuickPromptWeb/quickprompt.sqlite` si no existe; Demo es opt-in. Se verificó el perfil real normal, no una ruta DataPath simulada: estaba vacío antes de iniciar. Se dejó una plantilla nueva de trabajo sobre revisión de cambios .NET, creada durante esta comprobación; no se importó contenido privado. El servidor se cerró y volvió a iniciar para verificar persistencia, sin depender de la memoria del navegador. [Línea base normal](evidence/goal02/normal-baseline.json), [flujo diario](evidence/goal02/normal-daily.json).

Cerrar una pestaña termina su circuito, no el servidor iniciado desde terminal; usar Ctrl+C en la terminal para cerrar la instancia. Los guardados se realizan durante cada acción, no al cerrar. Ollama se probó inaccesible en 127.0.0.1:11435 sin detener el servicio existente del usuario. Biblioteca, edición, búsqueda, favoritos, categorías, completar, copiar, archivo/restauración y borrado de una plantilla propia continuaron disponibles.

## Cambios acotados y causas comprobadas

- Se rechaza schema futuro **antes** de DDL y se incluye inicialización de preferencias en la transacción. La reproducción anterior cambió bytes de una DB futura; ahora los conserva.
- Restaurar ya no trata un objeto legado como respaldo completo. Importar ese mismo formato continúa siendo aditivo. Exportar usa snapshot SQLite transaccional; restore se probó con un trigger que aborta una inserción tras el DELETE y conserva todo el estado previo.
- Tras restore se recargan idioma/tema/modelo/opt-in y se limpia estado temporal anterior. [Antes](evidence/goal02/restore-preferences-before.json), [después](evidence/goal02/restore-preferences-after.json).
- Se normaliza solo la comparación de búsqueda para que revision encuentre Revisión. No cambia texto, nombres de variables, serialización ni datos.
- Foco tras render al título o primera variable y desplazamiento del panel en móvil; aria-pressed usa true/false explícitos. Sin pantallas nuevas ni sustitución de diseño.
- Enlace bidireccional Blazor de variables para sincronizar cambios programáticos y entrada rápida; se reprodujo duplicación tras una sugerencia y el E2E ahora comprueba valores exactos antes de compilar.
- Respuestas Ollama con tipos JSON incorrectos se convierten en errores controlados. Análisis pide solo observaciones cortas; el código conserva texto/variables, evitando regenerar una copia del original y agotar salida.
- Ejecución pide concisión salvo que el usuario solicite detalle. Una consulta abierta agotó el límite y se rechazó sin guardar parcial; se conserva la evidencia y no se aumentó el presupuesto ni se relajó la validación. La regresión final completó, copió y guardó una respuesta real.
- Kestrel:Endpoints se rechaza al arrancar: se reprodujo que sobreescribía UseUrls usando únicamente 127.0.0.1 y otro puerto. [Antes](evidence/goal02/kestrel-override-before.json), [guardia](evidence/goal02/kestrel-override-after.json). Solo QuickPrompt:Port configura el puerto soportado.

No se alteran contratos de plantilla, versiones o JSON v1, ni categorías originales, fuentes, paleta o esquema de DB. No hay dependencias nuevas. El único cambio del contrato interno de análisis evita pedir al LLM un eco de texto que el código ya posee; el DTO público sigue siendo el mismo.

## Pruebas y alcance de seguridad

Reproducciones fallidas se conservan: [dos fallos de datos](evidence/goal02/reproduction.json), [cinco fallos de tipos JSON](evidence/goal02/format-reproduction.json), [análisis truncado](evidence/goal02/analysis-truncated-before.png). Las nuevas pruebas pasan sin cambiar las 59 condiciones anteriores. El harness de navegador exige arranque de su propio proceso antes de operar; la nueva prueba destructiva de preferencias también crea DB y servidor propios en 5189.

[HTTP](evidence/goal02/http-security.json), [accesibilidad](evidence/goal02/accessibility.json), [flujo web](evidence/goal02/browser-workflows.json), [IA real](evidence/goal02/browser-ai.json), [reinicio](evidence/goal02/browser-restart.json). Capturas finales: [biblioteca desktop](evidence/goal02/primary-desktop.png), [móvil](evidence/goal02/daily-mobile.png), [revisión IA](evidence/goal02/ai-proposal-diff.png), [datos recuperados](evidence/goal02/after-restart.png). Build final: cero errores y cero advertencias. Se mantiene no-LAN, sin sincronización externa ni telemetría. NuGet directo/transitivo de la solución: [consulta](evidence/goal02/dependency-audit-solution.json), comando con código 0 y ningún paquete vulnerable reportado a la fecha consultada. Se descargaron metadatos públicos de seguridad, **no paquetes ni software**. No cubre por sí solo framework, Ollama, SO, vulnerabilidades no publicadas ni una auditoría externa. No es autenticación multiusuario ni protección frente a un host comprometido.

## Calidad IA separada de contratos

[Informe de calidad](AI_QUALITY.md): fixture v2 de trabajo nuevo autorizado, hashes inalterados y revisión manual. La última corrida v2 tiene 18 contratos válidos y 18 checks automáticos, pero la revisión humana falla el borrador Qwen2.5 que omite una prohibición. Una corrida anterior omitió una variable. La regresión del fixture v1 tiene 29/30: Qwen2.5 convirtió el límite de 80 palabras en marcador y el adaptador lo rechazó. No se cambia el fixture ni se reintenta selectivamente para fabricar PASS. [Regresión v1](evidence/goal02/ai-evaluation-v1-regression.json).

Qwen3 conserva el papel principal; Qwen2.5 requiere especial revisión y muestra su limitación en Configuración. Preguntar/proponer no aplica cambios. La mejora exige aceptación explícita y revisión válida; guardar respuesta es otra acción. Ningún modelo ejecuta herramientas ni recibe autoridad para mutar plantillas. La exactitud general sigue sin demostrarse, aunque las seis preguntas factuales acotadas de v2 sean correctas.

## Reproducción sin instalar

```powershell
dotnet --list-sdks
dotnet build src/QuickPrompt.Web.sln --no-restore
dotnet test src/QuickPrompt.Tests --no-restore --logger 'trx;LogFileName=goal02-final.trx'

# Detener antes una instancia manual que ocupe 5188:
$env:QP_EVIDENCE_DIR='docs/evidence/goal02-rerun'
node tests/e2e/run.cjs
node tests/e2e/goal02.cjs
node tests/e2e/endpoint-guard.cjs

# Evaluación de calidad separada; no ejecutar consultas IA en paralelo:
dotnet run --project src/QuickPrompt.Evaluation --no-restore -- --quality tests/fixtures/ai-quality-v2.json docs/evidence/goal02-rerun/quality.json
dotnet run --project src/QuickPrompt.Evaluation --no-build -- tests/fixtures/ai-evaluation-v1.json docs/evidence/goal02-rerun/v1.json

# Consulta de metadatos, sin instalar/actualizar paquetes:
dotnet list src/QuickPrompt.Web.sln package --vulnerable --include-transitive --source https://api.nuget.org/v3/index.json --format json
```

Playwright/Edge y paquetes ya existentes; no se instalaron. `PLAYWRIGHT_MODULE` permite indicar el módulo existente en otro equipo. El E2E usa node:sqlite integrado de Node 22, que emite aviso experimental. Las evaluaciones reales pueden devolver código 1 por fallos; conservarlos y revisar, no cambiar los asserts. Las pruebas normal-baseline/normal-daily son probes de esta verificación sobre un perfil inicialmente vacío y la plantilla de trabajo propia; no ejecutarlas contra un perfil ajeno o con datos privados. Las pruebas generales y restore usan DB desechables.

## Bloqueos y siguiente decisión

1. SDK .NET 10 autorizado y disponible: ejecutar [MIGRATION_NET10](MIGRATION_NET10.md); no retargetear a ciegas. La estrategia contempla cambios actuales sin commit y rollback sobre copia.
2. Copia privada de DB/JSON y permiso explícito: verificar migración real, integridad, fechas, favoritos e historial. Por decisión del usuario no se realizó en esta sesión.
3. Qwen2.5: el comportamiento de creación no tiene aprobación semántica consistente. Mantener revisión manual; ampliar casos antes de reclamar equivalencia con Qwen3.

Registro completo: [problemas y decisiones](ISSUES_DECISIONS.md), [matriz funcional](FEATURE_PARITY.md). Se preservaron por hash las 25 evidencias originales del MVP: [comprobación](evidence/goal02/mvp-evidence-preserved.json). Sin commits, push, PR, instalación, descarga de modelos ni publicación.

[Resumen final reproducible](evidence/goal02/final-summary.json): resultados, bloqueos y hashes de los principales archivos verificados.
