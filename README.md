# QuickPrompt NOVA 1.0.0 — versión inicial

**Versión inicial cerrada y aceptada por el usuario.** Flujo principal: elegir una acción y usar IA para buscar o generar contenido; revisar antes de guardar o eliminar. REL-09 cerrado por confirmación del usuario: «lo probé y funcionó». [Acta y alcance de 1.0.0](docs/releases/NOVA_1.0.0.md). Tag de esta entrega: `nova-v1.0.0`; los tags sin prefijo pertenecen al historial anterior.

**NOVA actual:** React/TypeScript + ASP.NET Core API, con Qwen local y agente en la misma pestaña. Arranque: `./scripts/Start-Nova.ps1`. [Guía actual](docs/nova/README.md) · [matriz de cierre](docs/nova/VERSION_CLOSURE_MATRIX.md).

Estructura actual: `apps/mobile/` contiene MAUI y sus pruebas históricas;
`apps/web/` contiene React; `backend/` contiene API, aplicación e infraestructura;
`agents/nova-core/` contiene el adaptador y los lanzadores de Adaptive Core;
`src/` conserva el dominio compartido y el consumidor Blazor de compatibilidad.
`Tests/` aloja los arneses web/agente; `docs/` y `scripts/` conservan documentación
y herramientas. La base privada de raíz no se mueve ni se utiliza en QA sintético. Su copia e
importación local autorizadas están documentadas en el registro actual de cierre.

El arranque normal selecciona Core explícitamente y exige Python/SDK existentes.
Se pueden configurar `-CorePython` y `-CoreSdkRoot`. `-AgentRuntime native`
conserva la comparación con los consumidores anteriores; nunca se activa por
un fallo de Core. Las tareas de VS Code usan las rutas nuevas.

**Registro histórico anterior al cierre 1.0.0:** la revisión 2026-10-21 estaba abierta y conservaba fallos de interpretación libre y autoría. El usuario eligió posteriormente el flujo por acciones y aceptó su prueba humana. La [matriz histórica de alcance ampliado](docs/nova/VERSION_CLOSURE_MATRIX.md) conserva los criterios no certificados; no se convierten en PASS ni en garantía de fiabilidad universal. Las cifras y guías Blazor que siguen son históricas.

**Para usar NOVA:** abre `http://127.0.0.1:5190` y pulsa **Empieza aquí: guía y videos**. Incluye 60 prompts prácticos, 20 colecciones y cuatro videos cortos. [Guía paso a paso](docs/COMO_USAR_QUICKPROMPT.md) · [Biblioteca de tareas](docs/BIBLIOTECA_PRACTICA.md) · [Arrancar NOVA](docs/nova/README.md).

**GOAL 06 implementado y validado localmente:** editor contextual (crear/deshacer/renombrar variables y Undo/Redo), compartir plantilla JSON sin valores/historial, importación con preview, Quick Assist con Qwen3 y revisión, panel MV3 y runner Playwright tipado. **150/150 pruebas**, recorridos Edge, Qwen3 real y fixtures de extensión Edge/Chrome. [Aceptación y límites](docs/GOAL06_ACCEPTANCE.md), [auditoría](docs/GOAL06_AUDIT.md), [solicitud](docs/GOAL06.md). Proveedores auténticos: BLOCKED; piloto humano, docking visual del panel y Web Share nativo: NOT_TESTED. No se instalaron dependencias nuevas ni se modificaron datos privados.

La instancia que ya estuviera abierta debe reiniciarse, después de guardar su trabajo, para cargar el nuevo binario. Las pruebas GOAL06 usan output y datos aislados.

Aplicación **web ASP.NET Core / Blazor InteractiveServer**, con biblioteca de plantillas, variables, versiones, historial y Ollama local. El código MAUI original queda como referencia histórica y no se necesita para ejecutar la web.

```powershell
dotnet run --project src/QuickPrompt.Web --no-restore
# Abrir http://127.0.0.1:5188
```

Se verificó con SDK **8.0.425** (único instalado); la desviación respecto a .NET 10 se explica en [ADR 0001](docs/ADR/0001-web-stack.md). No se instaló SDK ni descargaron modelos; en Fase 03 se autorizaron únicamente las dependencias de desarrollo Tailwind/CLI. Datos nuevos en `%LOCALAPPDATA%/QuickPromptWeb/quickprompt.sqlite`; nunca usa `QuickPrompt.db3` versionada. Sin MAUI, Electron, nube, anuncios, telemetría ni APIs remotas desde el backend. Instancia local de un solo usuario.

El arranque normal crea una biblioteca vacía y persiste los datos del usuario. Los ejemplos son opcionales con `--QuickPrompt:Demo=true`. Estado de estabilización: [PRODUCT_READINESS](docs/PRODUCT_READINESS.md), [migración .NET 10](docs/MIGRATION_NET10.md), [calidad IA](docs/AI_QUALITY.md).

**Estado histórico GOAL 05: PASS local; proveedores auténticos BLOCKED.** [Aceptación, evidencia y reproducción](docs/GOAL05_ACCEPTANCE.md), [auditoría antes/después](docs/GOAL05_UX_AUDIT.md), [paridad](docs/FEATURE_PARITY.md) y [dirección del producto](docs/VISION_ALIGNMENT.md). 115/115 pruebas y flujos reales Edge, sin reconstruir el sistema. Las cifras previas y el [informe GOAL04](docs/GOAL04_ACCEPTANCE.md) conservan su alcance histórico. No hay validación humana, comercial ni remota; ChatGPT sin sesión autorizada y Claude automatizado siguen bloqueados. Los fixtures no son validación externa.

Uso diario: **buscar → seleccionar → completar campos → Copiar prompt**. No hace falta Generar antes ni configurar IA. **Usar en…** ofrece Qwen local o copiar/abrir ChatGPT/Claude; pegar/enviar manualmente si el puente no está disponible. Crear pide solo título y texto, con variables `<nombre>`. Ctrl/Cmd+K y Enter encuentran/seleccionan; Ctrl/Cmd+Enter dentro del formulario copia, nunca envía.

[Guía de tareas reales y registro de fricción](docs/DAILY_USE_PILOT.md): preparación del piloto humano, todavía sin resultados; no sustituye las pruebas automatizadas.

Seguimiento de uso: corregida la búsqueda por palabras recordadas en cualquier orden, con **127/127 pruebas** y recorridos reales en Edge sobre datos de prueba propios. [Causa, evidencia antes/después y reproducción](docs/DAILY_USE_FINDINGS.md). La tarea personal y el piloto humano siguen pendientes; las cifras115/115 de GOAL05 son su baseline histórico.

Todos/Favoritos/Recientes están en la biblioteca; Filtros contiene categoría/grupo. Opciones permite editar, organizar y revisar versiones; Opciones avanzadas contiene perfiles/combinaciones. Configuración conserva historial/archivo/ayuda y, en sus opciones avanzadas, modelos, instrucciones generales, respaldos y diagnóstico de extensión. En móvil se vuelve a la lista con **← Biblioteca**. [Grupos](docs/PROMPT_GROUPS.md), [instrucciones](docs/INSTRUCTION_PROFILES.md), [Qwen](docs/LOCAL_AI_WORKFLOWS.md) y [límites de proveedores](docs/PROVIDER_INTEGRATION_STATUS.md).

SQLite migra de v1 a v2 de forma aditiva/transaccional, con copia consistente `.before-goal04-<uuid>.sqlite` junto al archivo antes de migrar. Exporta JSON v2 y sigue leyendo v1; límite JSON simétrico de 32 MiB. Restaurar reemplaza datos; importar es aditivo. Para rollback detener el servidor y usar copia/código anterior; no ejecutar código v1 contra schema v2. No se lee QuickPrompt.db3 ni se valida importación privada sin autorización.

Fase 03: [aceptación y bloqueos](docs/PHASE03_ACCEPTANCE.md), [puente de proveedores](docs/PROVIDER_BRIDGE.md), [instalación local de extensión Edge/Chrome](extensions/quickprompt-browser/README.md), [auditoría UX](docs/UI_UX_AUDIT_V2.md). El backend sigue local; solo una acción y consentimiento explícitos permiten trasladar texto al editor de otra web. Los fixtures no certifican sitios reales. Tailwind **4.3.0** está compilado en quickprompt.css: la web arranca sin npm/red. ChatGPT real permanece sin validación por HTTP 403/sesión ausente; Claude automatizado está bloqueado por restricciones del proveedor. Copiar/abrir manual está disponible.

Para editar estilos, con las dependencias de desarrollo ya autorizadas:

```powershell
npm ci --ignore-scripts
npm run css:build
npm run css:check
# Durante edición: npm run css:watch
```

El lockfile fija el árbol de Tailwind/CLI; no ejecutar instalaciones si no están autorizadas. No se usa CDN. Se conservan app.css/modern.css y simplicity.css como fuentes y se sirve solo el resultado compilado. El comando de arranque presupone las dependencias existentes; no instala ni restaura paquetes.

Ollama es opcional para crear, organizar, completar y copiar. Para IA: servicio ya instalado en `http://127.0.0.1:11434`, modelos `qwen3:8b` o `qwen2.5:7b-instruct`. Elegir modelo en Configuración. Propuestas requieren aceptación; ejecutar texto final requiere confirmación. No hay descargas automáticas.

GOAL 04 corrigió el listener de Ollama a loopback y desactivó su nube mediante OLLAMA_HOST/OLLAMA_NO_CLOUD de usuario; reiniciar procesos existentes para heredar esos valores. [Detalle operativo y reversión](docs/LOCAL_AI_WORKFLOWS.md). No hay prueba de reinicio de Windows.

```powershell
dotnet test src/QuickPrompt.Web.sln
dotnet run --project src/QuickPrompt.Evaluation -- tests/fixtures/ai-evaluation-v1.json docs/evidence/ai-evaluation-v1.json
# E2E usa Edge y un módulo Playwright YA disponible; no lo instala.
$env:PLAYWRIGHT_MODULE = 'C:/ruta/al/modulo/playwright'
$env:QP_EVIDENCE_DIR = 'docs/evidence/rerun-' + (Get-Date -Format yyyyMMdd-HHmmss)
node tests/e2e/run.cjs
```

El harness E2E crea una DB ficticia aislada, inicia/reinicia el servidor y lo detiene al finalizar. Detener antes otra instancia que use 5188. En este equipo Playwright ya existe en el runtime Codex y el script lo detecta por su ruta local de fallback. En otros equipos proporcionar `PLAYWRIGHT_MODULE`; no ejecutar comandos de instalación sin autorización.

Configuración por argumentos o variables de entorno .NET:

- `--QuickPrompt:Port=5188` (siempre loopback).
- `--QuickPrompt:DataPath=C:/ruta/privada/quickprompt.sqlite`.
- `--QuickPrompt:Ollama=http://127.0.0.1:11434` (solo IP loopback HTTP).
- `--QuickPrompt:Demo=true` (solo ejemplos ficticios al estar vacía).
- `--QuickPrompt:AiTimeoutSeconds=180` (admite 5..180, solo IA; no condiciona biblioteca/copia).

[Resultado de aceptación y limitaciones](docs/MVP_ACCEPTANCE.md) · [Línea base](docs/BASELINE.md) · [Paridad](docs/FEATURE_PARITY.md) · [Diseño](docs/DESIGN_AUDIT.md) · [Arquitectura](docs/ARCHITECTURE.md) · [Seguridad](docs/SECURITY.md) · [Migración/respaldo](docs/DATA_MIGRATION.md).

---

## Documentación histórica del original MAUI

Lo siguiente describe el legado. Sus comandos de build, proveedores externos y cifras históricas de cobertura **no corresponden a la solución web ni son resultados actuales**.

# 🚀 QuickPrompt - AI Prompt Management Made Simple

[![.NET MAUI](https://img.shields.io/badge/.NET%20MAUI-9.0-blue.svg)](https://dotnet.microsoft.com/apps/maui)
[![Tests](https://img.shields.io/badge/tests-77%20passing-brightgreen.svg)](apps/mobile/tests/windows)
[![Coverage](https://img.shields.io/badge/coverage-100%25%20Use%20Cases-brightgreen.svg)]()
![License](https://img.shields.io/badge/license-MIT-blue.svg)

> A modern cross-platform application for creating, organizing, and executing AI prompts with ChatGPT, Gemini, Grok, and Copilot.

---

## ✨ Key Features

### 📝 Core Capabilities
- **Advanced Prompt Templates** - Create reusable prompts with dynamic `<variable>` placeholders
- **Smart Variable Detection** - Automatic extraction and management of template variables
- **Category Organization** - Organize prompts by Marketing, Writing, Programming, and custom categories
- **Quick Execution** - Fill variables and generate prompts instantly
- **AI Integration** - Direct launch to ChatGPT, Gemini, Grok, or Copilot via WebView
- **Import/Export** - Share prompts using JSON format
- **Advanced Search** - Find prompts quickly by category, keyword, or date
- **Usage History** - Track and reuse recently generated prompts

### 📅 Smart Filtering
- View prompts used **Today**
- Browse prompts from the **Last 7 Days**
- Filter by **Favorites** for quick access
- Sort by usage frequency and date

### 💾 Finalized Prompts
- Save completed prompts with filled-in values
- Mark final prompts as favorites
- Reuse successful prompt variations
- Track prompt execution history

### 🎯 Smart Suggestions
- Cache system remembers previous variable values
- Auto-suggestions based on usage patterns
- Context-aware variable recommendations

### 🎨 Modern UI/UX
- Clean, intuitive interface built with .NET MAUI
- Visual mode with interactive chips
- Responsive design for all screen sizes
- Smooth animations with Lottie

---

## 🛠️ Technologies & Architecture

### Tech Stack
- **Frontend:** .NET MAUI 9.0 (C#)
- **Data Persistence:** SQLite
- **Architecture:** Clean Architecture with MVVM
- **Dependency Injection:** Built-in .NET DI Container
- **Messaging:** WeakReferenceMessenger (CommunityToolkit.Mvvm)
- **Testing:** xUnit with Moq
- **Libraries:** 
  - CommunityToolkit.Mvvm
  - Lottie for animations
  - System.Text.Json

### Architecture Highlights
- ✅ **Clean Architecture** - Clear separation of concerns
- ✅ **MVVM Pattern** - Modern ViewModel-based UI
- ✅ **Use Cases** - Single-responsibility business logic
- ✅ **Result Pattern** - Explicit error handling
- ✅ **Dependency Injection** - Testable and maintainable
- ✅ **100% Test Coverage** - Comprehensive unit tests for all use cases

---

## 📁 Project Structure

```text
QuickPrompt-Web/
├── apps/
│   ├── web/                         # React / TypeScript, UI actual
│   └── mobile/                      # MAUI, recursos y tests/windows + tests/portable
├── backend/                         # API .NET, aplicación, infraestructura y pruebas
├── agents/nova-core/                # Worker y puente del SDK Adaptive Core existente
├── src/                             # Dominio compartido y consumidor Blazor compatible
├── Tests/nova/                      # Evaluación y recorridos reales del agente web
├── scripts/                         # Arranque y verificaciones; archive/ no ejecutable
├── docs/                            # Guías, decisiones y evidencias
└── .vscode/                         # Tareas y depuración con las rutas actuales
```

Los comandos MAUI históricos se ejecutan desde `apps/mobile`. La versión web
usa .NET 8; compilar MAUI requiere .NET 9 y sus workloads, fuera del alcance web.
La base privada de raíz conserva su ubicación. La copia autorizada se importó
en un almacén nuevo, conservando el respaldo y las limitaciones documentadas. `Check-RepositoryLayout.cjs` verifica soluciones, referencias,
entradas y exclusiones privadas tras los movimientos.

---

## 🎯 Usage Examples

### Creating a Prompt Template

**Template:**
```
Write a professional email to <recipient> explaining the <issue> and proposing <solution>.
```

**Variable Detection:**
The app automatically extracts:
- `<recipient>`
- `<issue>`
- `<solution>`

**Fill Variables:**
- `recipient` → "the customer support team"
- `issue` → "a billing discrepancy"
- `solution` → "a refund or invoice correction"

**Final Prompt:**
```
Write a professional email to the customer support team explaining the billing discrepancy and proposing a refund or invoice correction.
```

### Programmatic Usage

```csharp
// Create a new prompt
var request = new CreatePromptRequest
{
    Title = "Marketing Email Generator",
    Description = "Generate marketing emails for products",
    Template = "Write a <type> for <product> targeting <audience>",
    Category = "Marketing"
};

var result = await createPromptUseCase.ExecuteAsync(request);

if (result.IsSuccess)
{
    var prompt = result.Value;
    Console.WriteLine($"Created: {prompt.Title}");
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

---

## 🚦 Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (v17.8+) or [VS Code](https://code.visualstudio.com/)
- Platform-specific requirements:
  - **Android:** JDK 17
  - **iOS:** Xcode 15+ (macOS only)
  - **Windows:** Windows 10/11 with WinAppSDK

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/JuanPabloTorres/QuickPrompt.git
   cd QuickPrompt
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the project**
   ```bash
   dotnet build
   ```

4. **Run tests**
   ```bash
   dotnet test
   ```

5. **Run the app**
   ```bash
   # Android
   dotnet build -f net9.0-android -t:Run
   
   # iOS (macOS only)
   dotnet build -f net9.0-ios -t:Run
   
   # Windows
   dotnet build -f net9.0-windows10.0.19041.0 -t:Run
   
   # MacCatalyst
   dotnet build -f net9.0-maccatalyst -t:Run
   ```

---

## 🧪 Testing

### Test Coverage

We maintain **100% coverage** of all Use Cases with comprehensive unit tests:

| Test Suite | Tests | Coverage |
|------------|-------|----------|
| CreatePromptUseCaseTests | 19 | 100% |
| UpdatePromptUseCaseTests | 19 | 100% |
| ExecutePromptUseCaseTests | 17 | 100% |
| DeletePromptUseCaseTests | 7 | 100% |
| GetPromptByIdUseCaseTests | 10 | 100% |
| **Total** | **77** | **100%** |

### Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test suite
dotnet test --filter "FullyQualifiedName~CreatePromptUseCaseTests"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Quality Standards

- ⚡ **Fast** - All tests complete in ~100ms
- 🔒 **Isolated** - Each test is completely independent
- 🎯 **Reliable** - Zero flaky tests
- 📖 **Readable** - Clear AAA pattern (Arrange/Act/Assert)
- 🌐 **Comprehensive** - Covers success, validation, and error paths

---

## 🤝 Contributing

We welcome contributions! Here's how you can help:

### Ways to Contribute

- 🐛 **Report Bugs** - Open an issue with details and reproduction steps
- 💡 **Suggest Features** - Share your ideas for improvements
- 📝 **Improve Documentation** - Help make our docs clearer
- ✅ **Add Tests** - Increase test coverage
- 🔧 **Submit Pull Requests** - Fix bugs or implement features

### Development Workflow

1. **Fork the repository**
2. **Create a feature branch**
   ```bash
   git checkout -b feature/amazing-feature
   ```
3. **Make your changes**
4. **Run tests to ensure nothing breaks**
   ```bash
   dotnet test
   ```
5. **Commit with conventional commits**
   ```bash
   git commit -m "feat: add amazing feature"
   ```
6. **Push to your fork**
   ```bash
   git push origin feature/amazing-feature
   ```
7. **Open a Pull Request**

### Commit Convention

We follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` - New features
- `fix:` - Bug fixes
- `docs:` - Documentation changes
- `style:` - Code style/formatting
- `refactor:` - Code refactoring
- `test:` - Test updates
- `chore:` - Build/tooling changes

---

## 📊 Project Statistics

| Metric | Value |
|--------|-------|
| **Lines of Code** | ~15,000 |
| **Test Coverage** | 100% (Use Cases) |
| **Unit Tests** | 77 |
| **Platforms** | 4 (Android, iOS, Windows, MacCatalyst) |
| **Code Quality** | A+ |
| **.NET Version** | 9.0 |

---

## 🗺️ Roadmap

### Version 5.1 (Upcoming)
- [ ] Cloud synchronization for prompts
- [ ] Collaborative prompt sharing
- [ ] AI-powered prompt suggestions
- [ ] Advanced search with filters
- [ ] Export to multiple formats

### Version 6.0 (Future)
- [ ] Custom AI engine integration
- [ ] Prompt analytics and insights
- [ ] Team workspaces
- [ ] Public API for integrations
- [ ] Browser extension

---

## 📄 License

The historical README declares the MIT License; its referenced LICENSE file is not included in this checkout.

---

## 👨‍💻 Author

**Juan Pablo Torres**

- GitHub: [@JuanPabloTorres](https://github.com/JuanPabloTorres)
- Website: [QuickPrompt Official](https://estjuanpablotorres.wixsite.com/quickprompt)

---

## 🙏 Acknowledgments

- [.NET MAUI Team](https://github.com/dotnet/maui) - Outstanding cross-platform framework
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - Excellent MVVM helpers
- [xUnit](https://xunit.net/) - Powerful testing framework
- [Moq](https://github.com/moq/moq4) - Flexible mocking library

---

## 💬 Support

Need help or have questions?

- 📧 **Issues:** [GitHub Issues](https://github.com/JuanPabloTorres/QuickPrompt/issues)
- 💭 **Discussions:** [GitHub Discussions](https://github.com/JuanPabloTorres/QuickPrompt/discussions)

---

<div align="center">

**Made with ❤️ using .NET MAUI**

⭐ Star us on GitHub — it motivates us!

[Report Bug](https://github.com/JuanPabloTorres/QuickPrompt/issues) · [Request Feature](https://github.com/JuanPabloTorres/QuickPrompt/issues/new) · [View Releases](https://github.com/JuanPabloTorres/QuickPrompt/releases)

</div>

# GOAL 07 — preparación autónoma local

Desde una intención, NOVA busca primero en SQLite y prepara una instrucción con título automático, texto final o campos indispensables. El resultado se abre en un modal que puedes cerrar y retomar. También puedes pedir abrir o actualizar un prompt existente. [Guía de uso](docs/COMO_USAR_QUICKPROMPT.md), [comandos locales](docs/NOVA_LOCAL_COMMANDS.md), [aceptación y límites de GOAL 07](docs/GOAL07_ACCEPTANCE.md), [arquitectura](docs/nova/GOAL07_ARCHITECTURE.md).

