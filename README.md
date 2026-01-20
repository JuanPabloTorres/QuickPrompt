# 🚀 QuickPrompt - AI Prompt Management Made Simple

[![.NET MAUI](https://img.shields.io/badge/.NET%20MAUI-9.0-blue.svg)](https://dotnet.microsoft.com/apps/maui)
[![Tests](https://img.shields.io/badge/tests-77%20passing-brightgreen.svg)](QuickPrompt.Tests)
[![Coverage](https://img.shields.io/badge/coverage-100%25%20Use%20Cases-brightgreen.svg)]()
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

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

```plaintext
QuickPrompt/
│
├── ApplicationLayer/           # Business Logic Layer
│   ├── Common/
│   │   ├── Result.cs          # Result Pattern Implementation
│   │   └── Interfaces/        # Service Contracts
│   └── Prompts/
│       └── UseCases/          # Business Operations
│           ├── CreatePromptUseCase.cs
│           ├── UpdatePromptUseCase.cs
│           ├── DeletePromptUseCase.cs
│           ├── ExecutePromptUseCase.cs
│           └── GetPromptByIdUseCase.cs
│
├── Infrastructure/             # Infrastructure Layer
│   └── Services/
│       ├── UI/                # UI Services
│       │   ├── DialogService.cs
│       │   └── TabBarService.cs
│       └── Cache/             # Caching Services
│           └── PromptCacheService.cs
│
├── Features/                   # Presentation Layer
│   └── Prompts/
│       ├── ViewModels/        # MVVM ViewModels
│       └── Pages/             # XAML Pages
│
├── Core/                       # Core Domain
│   ├── Models/                # Domain Models
│   │   ├── PromptTemplate.cs
│   │   ├── FinalPrompt.cs
│   │   └── PromptVariableCache.cs
│   ├── Services/              # Core Services
│   └── Utilities/             # Helper Classes
│
├── Components/                 # Reusable UI Components
│   ├── Buttons/
│   ├── Containers/
│   └── Inputs/
│
├── Resources/                  # Application Resources
│   ├── Styles/
│   ├── LottieAnimations/
│   └── Icons/
│
└── QuickPrompt.Tests/         # Unit Tests
    └── UseCases/              # Use Case Tests (77 tests, 100% coverage)
```

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

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

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

