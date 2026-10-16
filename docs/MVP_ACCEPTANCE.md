# QuickPrompt Web — aceptación local, 2026-10-15

**Estado final: PASS_MVP_LOCAL para la implementación local net8.0 documentada en ADR 0001.** Build, persistencia real, navegador Edge, ausencia de Ollama y modelos locales fueron ejecutados. No es una declaración de disponibilidad en Internet ni de exactitud factual universal de la IA.

**Desviación de plataforma:** el objetivo principal .NET 10 está **BLOQUEADO por SDK ausente**. Solo existe SDK 8.0.425; CODEX_GOAL permite otra versión si se justifica y documenta. Se eligió net8.0 para completar un MVP verificable sin instalar software. No se afirma haber construido ni probado net10. La actualización de target requiere autorización para disponer del SDK y repetir esta aceptación.

## Resultado por criterio original

| # | Criterio | Resultado / evidencia |
|---|---|---|
| 1 | Web abre con dotnet run en localhost sin MAUI | PASS, Blazor Web App InteractiveServer, 127.0.0.1:5188, Edge 155.0.4283.45 |
| 2 | Crear/organizar/completar/copiar sin Ollama | PASS, servidor configurado a puerto loopback sin servicio; portapapeles real y recuperación tras error IA; [offline](evidence/browser-offline.json) |
| 3 | Mejorar/autocompletar/consultar Qwen3 y manejar ausencia | PASS funcional, modelos reales qwen3:8b y qwen2.5:7b-instruct, streaming, detener, reintentar, guardar; [evaluación](evidence/ai-evaluation-v1.json), [UI IA](evidence/browser-ai.json). Límite factual Qwen2.5 abajo |
| 4 | Migración/import-export/backup conservan datos y origen | PASS con DB sintética real seleccionada en navegador, JSON legado/v1, idempotencia, versiones, fechas, favoritos, finalizados, historial, restore transaccional e integrity_check=ok. Bytes del origen iguales. [Pruebas](evidence/unit-integration-contract.json), [UI](evidence/browser-workflows.json) |
| 5 | No sustituir original sin aceptar; versiones auditables | PASS, proponer/rechazar no altera plantilla/versiones; aceptar añade revisión atómicamente y registra estado. Recuperación tras reinicio [verificada](evidence/browser-restart.json) |
| 6 | Sin solicitudes no autorizadas fuera del host | PASS en flujos observados: 0 solicitudes externas del navegador; backend IA restringido a IP loopback, sin proxy/redirect; paquetes del servidor sin Firestore/AdMob/MAUI; no herramientas arbitrarias. [HTTP](evidence/http-security.json), [seguridad](SECURITY.md) |
| 7 | Unit + integración + E2E + adversariales | PASS, **59/59** xUnit, 0 omitidas; batería Edge completa y **30/30** casos reales IA sobre fixture inalterado. No se hereda 77/100% del legado |
| 8 | Responsive/teclado/contraste/errores | PASS en alcance probado: 1440×1000 y 390×844, sin overflow horizontal, Ctrl+K/Escape, foco modal y outline visible, labels, contrastes de texto de biblioteca clara/configuración oscura. [Auditoría](evidence/accessibility.json); no certificación WCAG completa |
| 9 | Documentación y reproducción | PASS: README web, BASELINE, FEATURE_PARITY, DESIGN_AUDIT, ARCHITECTURE, ADR, SECURITY, DATA_MIGRATION y este registro |

No se detuvo el servicio Ollama existente del usuario para probar ausencia: se usó `http://127.0.0.1:11435`, con conexión rechazada real. El mismo flujo se ejecutó después con el servicio real en 11434. Esto prueba independencia frente a un backend inaccesible; no se presenta como una orden de apagado del servicio del sistema.

## Flujos ejecutados en Edge

`node tests/e2e/run.cjs` termina con **PASS_COMPLETE_BROWSER_ACCEPTANCE**. El harness crea una DB ficticia nueva, exige evidencia de arranque de su propio servidor para no operar sobre otra instancia en 5188, cambia de backend indisponible a disponible, reinicia el proceso y lo detiene al finalizar.

- Crear → intentar compilar con campos faltantes y recibir error → completar → generar → copiar → leer y comparar portapapeles real.
- Editar → modo visual/chips → seleccionar variable textual → guardar → favorito y filtro → duplicar → archivar → restaurar → eliminar con confirmación.
- Ctrl+K → buscar → abrir plantilla → Escape/foco; búsqueda léxica sobre contenido.
- Exportar respaldo → importar JSON legado → importar DB sintética seleccionada sin modificar bytes → JSON corrupto controlado → restaurar respaldo → integrity_check=ok.
- ES→EN→ES y claro→oscuro→claro; viewport móvil sin overflow.
- Mejorar → comparación lado a lado → rechazar → original igual → mejorar → aceptar → revisión adicional.
- Sugerir valores con contexto autorizado → campos originales intactos → aplicar ejemplo explícitamente.
- Confirmar texto final → ejecutar Qwen3 real con streaming → copiar respuesta → guardar resultado → ejecutar otra vez y detener → ninguna respuesta parcial guardada.
- Analizar con reglas deterministas separadas de observaciones IA → crear desde intención → aceptar borrador → editar título → guardar explícitamente.
- Reiniciar servidor → recuperar plantilla creada, número exacto de versiones y único resultado guardado.

La copia multilinea se compara normalizando exclusivamente CRLF/LF: Windows convierte finales de línea al escribir al portapapeles. Las demás letras, signos y contenido se comparan completos. No se sustituyó la API Clipboard por un mock.

Evidencias visuales reales: [desktop](evidence/desktop.png), [móvil](evidence/mobile.png), [tema oscuro](evidence/dark-settings.png), [propuesta IA](evidence/ai-proposal-diff.png), [tras reinicio](evidence/after-restart.png). Datos exclusivamente ficticios.

## IA: datos, resultados y métricas

Fixture fijo: `tests/fixtures/ai-evaluation-v1.json`, SHA-256 **EF9C70F2D59A56B0A33B268CFB94BB400C37F06E56270C0DD419BED844AC800A** antes/después. No se modificaron dataset ni sus criterios para obtener PASS. Se conservaron [fallos iniciales](evidence/ai-evaluation-initial.json), [fallos de metadatos](evidence/ai-evaluation-metadata.json), [trazas diagnósticas](evidence/ai-evaluation-metadata.trace.json) y [resultado final](evidence/ai-evaluation-v1.json). Las trazas son solo del fixture ficticio, no del contenido privado del usuario.

Por modelo: mejorar, sugerir, crear, analizar, entrada adversarial y 10 ejecuciones. Las mejoras deben ser distintas del origen, explicar cambios y preservar nombres exactos. La prueba adversarial solicita borrar marcadores/ejecutar shell: propuestas conservan variables; no existe runtime de herramientas. Unitarias complementan timeout, cancelación, formato inválido, respuesta parcial, modelos/URLs no permitidos y confirmación ausente.

Hardware observado: Intel Core i7-13620H, 10 núcleos/16 hilos; 15.6 GiB RAM; NVIDIA RTX 4070 Laptop GPU. Ollama 0.40.4; `ollama ps` mostró Qwen3 100% GPU y contexto 8192. Configuración: think=false, temperatura 0.2, num_ctx 8192, num_predict 1800. Modelos ya instalados; no descargas.

Medición final aislada de otras consultas de navegador, peticiones secuenciales, percentil nearest-rank, ejecución del mismo prompt en español con ejemplo y máximo 80 palabras:

| Modelo | n ejecución | p50 | p95 |
|---|---:|---:|---:|
| qwen3:8b | 10 | 1445 ms | 1684 ms |
| qwen2.5:7b-instruct | 10 | 1328 ms | 1500 ms |

Son muestras pequeñas, de generación corta y principalmente modelo caliente. No son un SLA ni el p95 de todos los flujos de UI. El flujo Edge con un prompt más abierto tardó **26.780 s** en la última respuesta completa; [métrica individual](evidence/browser-ai.json). No se trasladaron números del LocalAI-Audit-Lab.

**Límite de calidad observado:** la revisión manual confirmó español y ≤80 palabras en las 20 respuestas del fixture. Qwen2.5 describió incorrectamente el almacenamiento SQLite como archivos de texto en **7/10** respuestas; el [formato oficial](https://sqlite.org/fileformat.html) es estructurado por páginas. [Revisión registrada](evidence/ai-manual-review.json). El PASS de contrato/flujo no equivale a precisión semántica. Mantener Qwen3 principal y revisar hechos de las respuestas antes de reutilizarlas; no se alteró el dataset ni se ocultaron esas salidas.

## Fallos encontrados y correcciones

| Fallo real | Causa y corrección | Regresión |
|---|---|---|
| Build/test MAUI no ejecutable | SDK 8 no soporta net9, NETSDK1045. Se conserva legado y se crea solución net8 independiente; no instalar workloads | BASELINE, build web |
| Sugerencias inválidas y creación sin variables/metadatos erróneos | Instrucciones mezclaban tareas y esquema JSON insuficiente. Instrucción por función, schema de claves/nombres, pattern sin ángulos y validación C# | Fixture idéntico, 30/30 final |
| Mejorar devolvía original sin cambios | Preservación excesiva de texto en instrucciones genéricas. Exigir reescritura útil/razones/cambios y rechazar no-op | Contratos y UI aceptar/rechazar |
| Clic perdido al arrancar | Controles SSR antes del circuito interactivo. Prerender desactivado para la superficie interactiva | E2E sin sleeps fijos |
| Archivo oculto por búsqueda anterior | Filtro arrastrado entre vistas. Reiniciar búsqueda/categoría al cambiar navegación | Duplicar/archivar/restaurar Edge |
| Contraste durante cambio de tema | Fondo transicionaba antes del color. Quitar transición de fondo; conservar borde/sombra y reduced-motion | 62 textos claros + 42 oscuros sin fallos medidos |
| Código web incluido en build legado | Globs MAUI raíz incluían 20 C# de src. DefaultItemExcludes src/**; evaluación posterior 0 | [Aislamiento](evidence/legacy-isolation.json) |
| Importación podía perder metadatos de intercambio | Preservar versiones, fechas, valores por defecto, finalizados y ejecuciones; prevalidar nulls/duplicados | SQLite y DB seleccionada en Edge |

Los fallos de sincronización del harness (leer portapapeles o contar versiones antes de terminar el evento Blazor) se corrigieron esperando el feedback o la presencia de los elementos. No se redujeron umbrales ni se omitieron escenarios.

## Comandos reproducibles

Desde la raíz, PowerShell; SDK y modelos deben existir. No usar el .sln MAUI para la web:

```powershell
dotnet --info
dotnet --list-sdks
ollama --version
ollama list
ollama ps

# Restauración offline usada en este equipo (solo paquetes ya cacheados):
dotnet restore src/QuickPrompt.Web.sln --source "$env:USERPROFILE/.nuget/packages" -p:NuGetAudit=false
dotnet build src/QuickPrompt.Web.sln --no-restore
dotnet test src/QuickPrompt.Tests --no-restore --logger 'trx;LogFileName=web-tests.trx'

# Ejecución manual independiente de las pruebas:
dotnet run --project src/QuickPrompt.Web --no-build -- --QuickPrompt:Demo=true
# http://127.0.0.1:5188 — detener esta instancia antes del harness

# Playwright ya disponible, sin instalar paquetes ni navegadores:
$env:PLAYWRIGHT_MODULE = 'C:/Users/estju/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'
node tests/e2e/run.cjs

# Evaluación real (no ejecutarla a la vez que otras consultas IA para medir latencias):
dotnet restore src/QuickPrompt.Evaluation --source "$env:USERPROFILE/.nuget/packages" -p:NuGetAudit=false
dotnet run --project src/QuickPrompt.Evaluation --no-restore -- tests/fixtures/ai-evaluation-v1.json docs/evidence/ai-evaluation-v1.json
```

Build final: **0 errores, 0 warnings**. xUnit: **59 PASS, 0 FAIL, 0 SKIP**. El harness SQLite de navegador usa el módulo integrado experimental `node:sqlite` de Node 22.19.0 para crear una DB ficticia; su aviso experimental no implica fallo ni instalación. TRX completo se genera en src/QuickPrompt.Tests/TestResults; resumen durable en [unit-integration-contract.json](evidence/unit-integration-contract.json).

## Riesgos y alcance de entrega

- **Bloqueo de plataforma:** net10 y el build/test MAUI net9 no verificados por SDK ausente. No se afirma compatibilidad no ejecutada.
- **Medio, calidad IA:** alucinación factual observada en Qwen2.5; la revisión humana sigue siendo necesaria. No hay RAG ni validación factual automática.
- **Medio, escala:** búsqueda léxica en snapshot y paginación, sin benchmark de bibliotecas grandes. FTS/BM25 y semántica diferidos; Nomic no se utiliza.
- **Local únicamente:** sin autenticación multiusuario ni cifrado propio. Para LAN/Internet se requiere otro diseño y nueva aceptación; no se habilitó ese acceso.
- **Datos privados:** QuickPrompt.db3 versionada se inventarió sin leer sus prompts, modificarla, sembrarla ni distribuirla. Migración se verificó con DB sintética, no con registros privados reales.
- **Accesibilidad:** comprobaciones de contraste/foco/labels y vista móvil, sin prueba de lector de pantalla externo ni certificación integral. Safari/Firefox no evaluados.
- **Dependencias:** paquetes ya cacheados; no se ejecutó un escaneo actualizado de vulnerabilidades NuGet. No se instaló software.

Rollback local: detener servidor, exportar respaldo antes de cambiar versión y restaurar JSON desde Configuración con confirmación; transacción conserva estado previo si el archivo falla validación. No tocar DB legada. Diagnóstico: consola local; sin prompts en logs de aplicación. El harness termina su propio proceso; no deja un servidor de prueba publicado.

Cambios locales: solución web, dominio reutilizado por enlace, componentes/CSS/fonts locales, SQLite, adaptador Ollama, pruebas, evidencia y docs. Legado: solo exclusión src/** en csproj, README y .gitignore; sin cambios de APIs, DB de origen ni despliegue. Sin commits, push, PR, publicaciones, instalaciones ni descargas de modelos.
