# Línea base — 2026-10-15

Fuente local: `origin https://github.com/JuanPabloTorres/QuickPrompt.git`, commit `474f2b203af207d7310a60787a5b946974817087`, rama `feature/web-local-first`. Árbol inicial: únicamente `?? CODEX_GOAL.md`; se conserva intacto.

`origin/master` coincide con ese commit. Aislamiento posterior: la evaluación MSBuild de items (forzando net8/UseMaui=false solo para inspeccionar globs, no para compilar MAUI) detectó 20 C# nuevos de src/ incluidos por el csproj raíz. Se agregó únicamente DefaultItemExcludes=src/** al proyecto legado para preservar su frontera; segunda evaluación incluye 0. Evidencia: docs/evidence/legacy-isolation.json. Ningún archivo de dominio, UI, repositorios ni datos legados se modificó.

## Inventario y flujos observados

| Superficie | Evidencia / función efectiva |
|---|---|
| `AppShell.xaml` | Tabs AI, Quick, Create, Settings; navegación MAUI y Material Icons |
| `Pages/MainPage.xaml`, `ViewModels/MainPageViewModel.cs` | Crear e importar un objeto JSON Title/Template/Description/VariablesJson |
| `Pages/PromptBuilderPage.xaml` | Editor textual/visual y selección de texto como chips |
| `Features/Prompts/**` | Biblioteca paginada, filtros, favoritos, edición, completar variables, compartir |
| `ApplicationLayer/**` | Casos de uso CRUD y Execute; interfaces, Result; Execute sustituye con Replace sucesivo y guarda/cachea automáticamente |
| `Domain/**` | Entidades puras Guid/UTC, categorías, favorito, borrado lógico; reutilizables sin MAUI |
| `Core/**` | Segundo modelo acoplado MVVM/SQLite/Newtonsoft; repositorios y parser legado permisivo |
| `Infrastructure/**` | Adaptadores DTO, historial SQLite/Firestore, sincronización, AdMob, WebView e inyección de terceros |
| `Resources/Styles/**` | Tokens reales azul/teal/amarillo, Nasa21 actual, OpenSans disponible, tarjetas, bordes, espaciado |
| `QuickPrompt.csproj` | MAUI net9 Android/iOS/MacCatalyst/Windows, anuncios/Firestore; excluido de la solución nueva |
| `QuickPrompt.Tests/**`, `Tests/**` | xUnit/Moq acoplados por ProjectReference a MAUI net9; no constituyen pruebas web |

Arquitectura original: Shell → ViewModel → use case → adapter → repositorio SQLite. Coexisten modelos Domain y Core; los ViewModels aún dependen de AdMob, Shell y servicios MAUI. Es válido conservar las reglas de creación, categorías, identidad y favoritos; no reutilizar la sustitución sucesiva ni los límites de plataforma.

Hallazgos: (alto) cache de datos automáticamente y salidas externas WebView; (alto) render dependiente del orden de sustitución, validación incompleta en servidor; (medio) restauraciones legadas borran DB; (medio) búsquedas cargan toda la tabla; (medio) documentación y ejecución discrepan. README dice OpenSans/100%/77 tests; Typography.xaml usa Nasa21. El informe histórico afirma producción y métricas sin evidencia actual. No se heredan esas conclusiones.

## Entorno comprobado

`dotnet --info`, `dotnet --list-sdks`: Windows x64, SDK **8.0.425**, runtime ASP.NET **8.0.31**; sin workloads. `dotnet new blazor --help` solo soporta net8.0.

`dotnet build QuickPrompt.csproj -f net9.0-windows10.0.19041.0 --no-restore` y `dotnet test QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore`: **NETSDK1045**, SDK actual no soporta .NET 9. No se instalaron SDK/workloads.

`ollama --version`: **0.40.4**. `ollama list`: qwen3:8b (500a1f067a9f), qwen2.5:7b-instruct (845dbda0ea48), nomic-embed-text-v2-moe:latest (ff9c2f10ef5e). `ollama ps`: vacío al inicio. No descargas.

Node 22.19.0; Edge instalado. Python/py no están en PATH, pero existe Python del runtime Codex. Paquetes NuGet SQLite/xUnit disponibles en cache; se usarán referencias locales ya presentes.

`C:\Projects\Repos\LocalAI-Audit-Lab` existe; contiene harness PowerShell de auditoría. No se altera su dataset, informes ni criterios; no es dependencia del runtime web. Sus resultados no sustituyen las evaluaciones de esta interfaz.

## Datos privados

`git ls-files '*db*'` confirma `QuickPrompt.db3` versionada. No se leen sus prompts, no se copia, modifica, distribuye ni usa como seed. La web crea otra DB fuera del repo. Solo importación explícita de archivo seleccionado por usuario o JSON compatible. Evidencia usa datos ficticios.
