# Migración incremental a .NET 10

Estado: **BLOQUEADO para ejecución**, **estrategia preparada**. `dotnet --list-sdks` vuelve a mostrar únicamente 8.0.425; no se instaló SDK, runtime, workload ni paquete. La web permanece net8.0. La revisión de código no equivale a compatibilidad ejecutada.

La política oficial establece fin de soporte de .NET 8 el **10 de noviembre de 2026**; .NET 10 LTS tiene soporte hasta noviembre de 2028. Esta es una prioridad temporal para el uso continuo, no una afirmación de vulnerabilidad detectada. [Política Microsoft](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [anuncio de fin de soporte](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/).

## Auditoría estática

| Área actual | Evaluación y prueba requerida |
|---|---|
| Web InteractiveServer, sin WebAssembly ni Identity | Mantener AddRazorComponents/AddInteractiveServerComponents, circuito, antiforgery, CSP y prerender desactivado. Probar conexión, recarga y foco. No adoptar passkeys, WASM ni nuevas pantallas |
| Domain puro net8 y entidades originales enlazadas | Sin referencias MAUI; conservar enlaces ../../Domain y contratos Guid/UTC. Regresión de parser, búsqueda Unicode y serialización |
| SQLite 8.0.29 y SQLitePCLRaw 2.1.6/2.1.12 | No asumir compatibilidad nativa por compilación. Probar carga win-x64, schema v1, transacciones, fallos de escritura, backup/restore y bytes de origen. No mezclar actualización ORM/esquema con cambio de runtime |
| HttpClient, JSON y streaming Ollama | Revalidar JSON, cancelación NDJSON, error parcial, timeout y esquema. Medir de nuevo; los tiempos net8 no son tiempos net10 |
| Tests xUnit/VSTest actuales | Conservar runner y criterios; no migrar simultáneamente a otro motor de pruebas. Añadir ejecución net10 real antes de afirmarla |
| CLI/harness | El harness actual apunta bin/Debug/net8.0. Actualizar únicamente la ruta de binarios al probar net10 en copia aislada; conservar sus asserts y guardia de propiedad del proceso |
| Legado MAUI | No retargetear QuickPrompt.csproj ni mezclar soluciones; conservar exclusión src/** |

Fuentes primarias: [migración ASP.NET 9→10](https://learn.microsoft.com/en-us/aspnet/core/migration/90-to-100?view=aspnetcore-10.0), [cambios incompatibles .NET 10](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10). Hay que revisar también los cambios 8→9; no asumir que una guía 9→10 cubre el salto completo. Las secciones de WASM/passkeys de la guía no justifican añadirlos a esta aplicación. Context7 no está disponible en esta sesión; se consultaron las fuentes oficiales.

## Secuencia con puertas de aceptación

1. **Precondición autorizada:** disponer de SDK 10 y packs necesarios. Comprobar `dotnet --list-sdks`. Si faltan dependencias en cache, parar y listar exactamente cuáles requieren autorización. No usar `dotnet-install`, `winget`, actualizaciones masivas ni restore online implícito.
2. **Congelar línea base:** guardar hashes de fuentes, fixtures y evidencias; exportar datos desde UI y comprobar respaldo sobre copia autorizada. Los cambios actuales no están comprometidos en Git: una worktree desde HEAD no contiene por sí sola el MVP. Crear una copia aislada de las fuentes actuales, incluyendo archivos nuevos y enlaces Domain; excluir DB, backups privados, bin/obj, secretos y configuración privada. No hacer commit, checkout destructivo ni reset para preparar la prueba.
3. **SDK primero:** en la copia, usar un global.json local a src que seleccione la versión 10 instalada. Compilar inicialmente net8 con el SDK 10 para separar cambios del compilador. Esto aún no valida el runtime net10.
4. **Consumidores antes que dominio:** en esa copia cambiar Web, Tests y Evaluation a net10.0, manteniendo Domain net8.0 inicialmente. Restaurar solo desde cache autorizada. Ejecutar build, unitarias, SQLite, contratos, navegador y evaluación IA. Esta compatibilidad entre targets es una hipótesis a verificar, no un PASS anticipado.
5. **Dominio:** retargetear Domain a net10.0 y repetir la suite completa. Mantener schema de DB y JSON v1. Cambiar versiones de paquetes solo si la evidencia de incompatibilidad lo exige; cada cambio separado y con regresión.
6. **Aceptación:** arranque normal sin demo sobre copia, reinicio y recuperación, offline, guardar/borrar/restaurar, Edge desktop/móvil, estado ARIA, privacidad/loopback, cancelación y no aplicación silenciosa. Fixtures v1/v2 y umbrales intactos. Conservar fallos y comparar antes/después. No probar una DB privada sin autorización.
7. **Promoción local:** solo después de todos los controles y una revisión de diferencias, aplicar el cambio mínimo de targets a la solución de trabajo. Nada de publicación, LAN ni producción comercial.

Comandos a adaptar **en la copia**, solo cuando los prerrequisitos estén disponibles:

```powershell
dotnet --list-sdks
dotnet restore src/QuickPrompt.Web.sln --source "$env:USERPROFILE/.nuget/packages" -p:NuGetAudit=false
dotnet build src/QuickPrompt.Web.sln --no-restore
dotnet test src/QuickPrompt.Tests --no-restore --logger 'trx;LogFileName=net10.trx'
$env:QP_EVIDENCE_DIR='docs/evidence/net10'
node tests/e2e/run.cjs # cambiar en la copia su ruta bin/Debug/net8.0 a net10.0
dotnet run --project src/QuickPrompt.Evaluation --no-restore -- --quality tests/fixtures/ai-quality-v2.json docs/evidence/net10/quality.json
```

Estos comandos net10 **no se ejecutaron**. Los csproj activos no se modificaron a net10.

## Rollback

Guardar la copia net8 funcional y su evidencia antes del cambio; detener exclusivamente la instancia propia, volver a sus binarios y configuración. Con schema v1 sin cambios, abrir la copia de DB compatible y verificar integridad; si hubo cambios de datos durante el piloto, restaurar el respaldo autorizado y probado con confirmación. No abrir con una versión vieja una DB futura: GOAL 02 añadió rechazo previo a cualquier escritura y una regresión de igualdad de bytes. Si posteriormente se necesita schema nuevo, diseñar su rollback por separado; no asumir downgrade automático.
