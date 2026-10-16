# QuickPrompt NOVA — entrega ejecutable 1

**Empieza por la [guía de uso con cuatro videos](../COMO_USAR_QUICKPROMPT.md).** La biblioteca práctica aporta 60 prompts en 20 colecciones. Dentro de la aplicación, pulsa **Empieza aquí: guía y videos**.

Reconstrucción independiente en React/TypeScript y ASP.NET Core Web API. La biblioteca funciona sin Ollama. Esta entrega incluye además una primera integración NOVA/Qwen local y destinos de copiar/abrir. **No representa la aceptación de todo el SOW ni autoriza retirar Blazor.**

Desarrollo en `rebuild/quickprompt-nova`, preparado para integración local en `main`. Se conservan MAUI, Blazor, extensión y documentación histórica. Los datos privados y las evidencias generadas quedan fuera del código versionado; consulta [la política de evidencias](../evidence/README.md).

## Arranque en Windows

Desde la raíz:

```powershell
.\scripts\Start-Nova.ps1
```

El script compila con las dependencias ya instaladas, inicia la API en loopback y abre `http://127.0.0.1:5190`. Ctrl+C detiene la instancia que inició. No instala paquetes ni modelos. Para usar los artefactos existentes: `-NoBuild`. Para no abrir navegador: `-NoBrowser`.

Para mantener NOVA disponible al cerrar la terminal, usa `.\scripts\Start-Nova.ps1 -NoBuild -Background`. Primero comprueba que su propia API esté lista y después termina el lanzador sin detenerla. Muestra el PID y el comando para detenerla. No configura inicio automático con Windows.

La biblioteca normal de NOVA se guarda en `%LOCALAPPDATA%/QuickPromptNova/quickprompt.sqlite`. En este equipo ya se añadieron los 60 prompts prácticos; en una instalación nueva puedes añadirlos desde la guía. No abre automáticamente la biblioteca Blazor ni `QuickPrompt.db3`. Para una prueba independiente:

```powershell
.\scripts\Start-Nova.ps1 -Port 5192 -DataPath C:/Projects/Repos/QuickPrompt-Web/.playwright/mi-prueba.sqlite
```

Si faltan dependencias en otro equipo, instalarlas exige autorización previa conforme al SOW. Las versiones exactas están en `apps/web/package-lock.json`; el comando de reproducción es `npm ci --ignore-scripts` dentro de `apps/web`. Los proyectos .NET referencian únicamente versiones ya presentes en el entorno original. En esta sesión se restauraron desde `C:/Users/estju/.nuget/packages`, sin nuevas descargas. SDK observado: 8.0.425; Node: 22.19.0.

## Uso

1. Busca por título, contenido, descripción o colección. También hay filtros de favoritos, recientes y archivo.
2. Selecciona un prompt. Completa las variables en ese mismo espacio y pulsa **Copiar prompt**.
3. Crea uno con **Nuevo prompt**: solo título y contenido son obligatorios. `<tema>` crea una variable; los nombres y escapes los valida el parser C# existente.
4. **Usar en…** ejecuta con un modelo local instalado o copia y abre un destino para pegado/envío manual.
5. NOVA propone; **Revisar en el editor** y luego **Guardar** son acciones distintas. Interpretar o generar propuestas no escribe en SQLite.
6. **Datos y respaldos** exporta, respalda o importa un archivo que elijas. Restaurar exige revisión, confirmación y una biblioteca sin cambios desde la revisión.

En Windows, Edge transforma LF en CRLF en el portapapeles nativo. La descarga `.txt` conserva los bytes UTF-8 del compilado; esta diferencia está registrada como `EXECUTED_FAIL` para igualdad nativa byte a byte.

## Desarrollo y validación

```powershell
dotnet build QuickPrompt.Nova.sln --no-restore
dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore
npm test --prefix apps/web
npm run build --prefix apps/web
node Tests/nova/acceptance.cjs
$env:NOVA_AI='1'
node Tests/nova/acceptance.cjs
```

El E2E usa Edge instalado, puertos y SQLite propios. Su resolución de Playwright usa el runtime existente; `PLAYWRIGHT_MODULE` permite indicar otra instalación ya autorizada. No instala navegadores. El segundo recorrido usa Ollama real y puede tardar varios minutos. El primero apunta deliberadamente a un Ollama ausente.

En este Windows, el E2E informa `EXECUTED_FAIL` global y sale con código 2 por la conversión LF→CRLF observada en el portapapeles. Los recorridos funcionales aprobados y esa diferencia se registran por separado; no se omite el criterio de igualdad.

Vite es opcional durante desarrollo (`npm run dev` desde `apps/web`); su proxy está limitado a `127.0.0.1:5190` y reescribe únicamente el origen exacto de desarrollo. La ejecución normal usa un único origen desde ASP.NET Core, con archivos React compilados.

- [Aceptación y límites](ACCEPTANCE.md)
- [Baseline y auditoría](BASELINE.md)
- [Arquitectura y seguridad](ARCHITECTURE.md)
- [Matriz de paridad](PARITY.md)
- [Migración y rollback](MIGRATION.md)
- [Diseño e interacción](DESIGN.md)
- [Proveedores](PROVIDERS.md)
- [Dependencias](DEPENDENCIES.md)
- [SOW original](SOW.md)
- [OpenAPI v1](../../contracts/openapi/nova-v1.json), también servido en `/api/v1/openapi.json`

![Living Canvas](../evidence/nova/review-desktop.png)

# GOAL 07

La entrada principal prepara instrucciones desde una intención natural: busca en SQLite antes de reutilizar/adaptar/generar, propone título y resuelve valores citados. Guarda el resultado con **Guardar**, sin abrir el editor. Para recuperar contenido y valores tras reiniciar, activa el consentimiento antes de preparar. [Arquitectura y límites actuales](GOAL07_ARCHITECTURE.md), [aceptación GOAL 07](../GOAL07_ACCEPTANCE.md).

![Prompt preparado por Qwen3 desde React](../evidence/goal07/prepared-desktop.png)
