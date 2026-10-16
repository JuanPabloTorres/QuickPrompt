# ADR 0001 — Blazor local con servidor único

Aceptado para implementación local. Objetivo principal: .NET 10 LTS. Desviación explícita: **net8.0**, único SDK instalado, para construir y verificar sin instalar software. La aceptación del target net10 permanece bloqueada hasta disponer del SDK autorizado. No se afirma equivalencia de soporte ni validación net10.

ASP.NET Core Blazor Web App InteractiveServer. Un backend loopback; Razor/CSS propios, sin frontend duplicado. Proyecto Domain enlaza entidades y enums puros originales; Web separa Application/Infrastructure/Components; Tests separado y solución `src/QuickPrompt.Web.sln`. SQLite mediante Microsoft.Data.Sqlite.Core 8.0.29 y SQLitePCLRaw.bundle_e_sqlite3 2.1.6 ya cacheados; no EF ni servicios adicionales. xUnit 2.9.2 y Test SDK 17.12.0 cacheados para pruebas.

No proveedores externos ni herramientas IA. HttpClient Ollama solo backend y loopback. Sin embeddings: baseline léxico suficiente para MVP pequeño; medir recuperación antes de decidir FTS/BM25 o semántica. Preferir tokens compilados sobre Replace recursivo. Conservar snapshots versionados y consentimiento explícito para propuestas/ejecución.

Documentación primaria consultada: [Interactive Server](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-8.0), [Ollama structured outputs](https://github.com/ollama/ollama/blob/main/docs/capabilities/structured-outputs.mdx), [thinking](https://ollama.com/blog/thinking). Context7 no está disponible en esta sesión.
