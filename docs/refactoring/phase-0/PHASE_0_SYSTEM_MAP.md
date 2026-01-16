# ??? PHASE 0 - SYSTEM MAP

**Date:** 15 de Enero, 2025  
**Phase:** 0 - Diagnostic & Freeze  
**Status:** ? Complete  
**Project:** QuickPrompt Refactoring

---

## ?? EXECUTIVE SUMMARY

QuickPrompt is a .NET MAUI 9 cross-platform application for creating, managing, and executing AI prompts. The system follows a **hybrid architecture** mixing MVVM, feature-based organization, and layered patterns.

### System Statistics
- **Total C# Files:** 118
- **Total XAML Files:** ~25
- **Active Pages:** 8
- **Active ViewModels:** 9
- **Services:** 15+
- **Repositories:** 4
- **Lines of Code:** ~15,000+

---

## ??? ARCHITECTURAL OVERVIEW

### Current Architecture Pattern
**Hybrid: Feature-Based + Layer-Based**

```
QuickPrompt/
??? ?? Presentation Layer (Mixed)
?   ??? Pages/ (root level - legacy)
?   ??? Features/*/Pages/ (feature-based)
?   ??? Presentation/ (modern)
?   ??? ViewModels/ (root level - mixed)
?
??? ?? Business Logic (Implicit - in ViewModels)
?   ??? (No explicit Application Layer)
?
??? ?? Domain Layer (Partial)
?   ??? Core/Models/Domain/
?   ??? Core/Models/Enums/
?   ??? Core/Interfaces/
?
??? ??? Infrastructure Layer
    ??? Core/Services/
    ??? Infrastructure/
    ??? Shared/
```

### Architecture Issues Identified
1. **No Application Layer** - Business logic embedded in ViewModels
2. **Inconsistent Organization** - Mix of feature-based and layer-based
3. **Scattered Services** - Core/, Infrastructure/, Shared/ all contain services
4. **Static Classes** - 3+ static helper classes (anti-pattern for DI/testing)

---

## ?? ACTIVE PAGES MAP

### Tab Pages (Shell Routes)

| Page | Route | ViewModel | Location | Status | Usage |
|------|-------|-----------|----------|--------|-------|
| **QuickPromptPage** | `/Quick` | QuickPromptViewModel | `Features/Prompts/Pages/` | ? Active | Primary feature - Execute existing prompts |
| **MainPage** | `/Create` | MainPageViewModel | `Pages/` | ? Active | Create new prompts |
| **AiLauncherPage** | `/AIWeb` | AiLauncherViewModel | `Features/AI/Pages/` | ? Active | Launch AI engines with prompts |
| **SettingPage** | `/Setting` | SettingViewModel | `Pages/` | ? Active | App settings & preferences |

### Modal/Navigation Pages

| Page | Route | ViewModel | Navigation From | Status |
|------|-------|-----------|-----------------|--------|
| **PromptDetailsPage** | `PromptDetailsPage` | PromptDetailsPageViewModel | QuickPromptPage | ? Active |
| **EditPromptPage** | `EditPromptPage` | EditPromptPageViewModel | PromptDetailsPage | ? Active |
| **GuidePage** | `GuidePage` | None (code-behind) | AppShell (first launch) | ? Active |
| **PromptBuilderPage** | `PromptBuilder` | PromptBuilderPageViewModel | MainPage | ? Active |
| **EngineWebViewPage** | `EngineWebView` | EngineWebViewViewModel | AiLauncherPage | ? Active |

---

## ?? VIEWMODEL ARCHITECTURE

### Active ViewModels

| ViewModel | LOC | Responsibilities | Issues |
|-----------|-----|------------------|--------|
| **MainPageViewModel** | ~300 | Prompt creation, variable handling, import | ?? Too large, mixed concerns |
| **QuickPromptViewModel** | ~250 | Prompt execution, variable filling, caching | ?? Business logic embedded |
| **PromptDetailsPageViewModel** | ~150 | Display prompt details, navigation | ? Reasonable |
| **EditPromptPageViewModel** | ~120 | Edit existing prompts | ? Reasonable |
| **AiLauncherViewModel** | ~180 | AI engine selection & launch | ?? Some business logic |
| **SettingViewModel** | ~100 | Settings management | ? Clean |
| **SettingsViewModel** | ~80 | (Duplicate?) Settings | ?? Duplication concern |
| **PromptBuilderPageViewModel** | ~150 | Advanced prompt building | ?? Complex logic |
| **EngineWebViewViewModel** | ~100 | WebView management | ? Reasonable |

### ViewModel Inheritance

```csharp
BaseViewModel (in Presentation/ViewModels/)
??? RootViewModel (unused?)
??? All other ViewModels inherit from BaseViewModel

// BaseViewModel provides:
- INotifyPropertyChanged
- Loading state management
- Database service access
- Navigation helpers
```

---

## ??? DATA LAYER ARCHITECTURE

### Active Repositories

| Repository | Interface | Location | Database | Status |
|------------|-----------|----------|----------|--------|
| **PromptRepository** | IPromptRepository | Core/Services/Data/ | SQLite | ? Active |
| **FinalPromptRepository** | IFinalPromptRepository | Core/Services/Data/ | SQLite | ? Active |
| **SqliteExecutionHistoryRepository** | IExecutionHistoryRepository | Infrastructure/History/Repositories/ | SQLite | ? Active |
| **FirestoreExecutionHistoryCloudRepository** | IExecutionHistoryCloudRepository | Infrastructure/History/Repositories/ | Firestore | ?? Not configured |
| **NullExecutionHistoryCloudRepository** | IExecutionHistoryCloudRepository | Infrastructure/History/Repositories/ | None (stub) | ? Active (fallback) |

### Database Entities

| Entity | Table | Fields | Relationships |
|--------|-------|--------|---------------|
| **PromptTemplate** | prompts | Id, Title, Description, Template, Category, IsFavorite, CreatedDate | None |
| **FinalPrompt** | final_prompts | Id, PromptTemplateId, FilledTemplate, Variables (JSON), CreatedDate | ? PromptTemplate |
| **ExecutionHistoryEntry** | execution_history | Id, PromptId, EngineType, Timestamp, DeviceId | ? PromptTemplate |

### Data Flow

```
UI (Page)
  ?
ViewModel
  ?
Repository (Interface)
  ?
Repository (Implementation)
  ?
SQLite / Firestore
```

**Issue:** No Use Case layer means ViewModels directly call repositories, mixing orchestration with data access.

---

## ?? SERVICES ARCHITECTURE

### Core Services

| Service | Interface | Type | Location | DI Scope | Issues |
|---------|-----------|------|----------|----------|--------|
| **DatabaseConnectionProvider** | None | Class | Core/Services/Data/ | Singleton | ? Clean |
| **DatabaseServiceManager** | None | Class | Core/Services/Data/ | Singleton | ? Clean |
| **SettingsService** | ISettingsService | Class | Core/Services/Settings/ | Singleton | ? Clean |
| **SessionService** | ISessionService | Class | Core/Services/Settings/ | Singleton | ? Clean |
| **PromptVariableCache** | None | **Static Class** | Core/Services/Cache/ | N/A | ? Static (not testable) |
| **PromptCacheCleanupService** | None | Static Class | Core/Services/Cache/ | N/A | ? Static |

### Infrastructure Services

| Service | Interface | Location | Purpose |
|---------|-----------|----------|---------|
| **AdmobService** | None | Infrastructure/ThirdParty/AdMob/ | Ad integration |
| **SharePromptService** | None | Infrastructure/ThirdParty/Share/ | Share functionality |
| **WebViewInjectionService** | IWebViewInjectionService | Infrastructure/Engines/Injection/ | Inject prompts into WebView |
| **SyncService** | None | Infrastructure/History/Sync/ | Cloud sync logic |
| **ExecutionHistoryIntegration** | None | Infrastructure/History/ | History tracking |

### Helper Classes (Should be Services)

| Helper | Type | Location | Issue |
|--------|------|----------|-------|
| **AlertService** | Static | Core/Utilities/Helpers/ | ? Should be IDialogService |
| **GenericToolBox** | Static | Core/Utilities/Helpers/ | ? Should be IDialogService |
| **TabBarHelperTool** | Static | Core/Utilities/Helpers/ | ? Should be ITabBarService |
| **DebugLogger** | Static | Shared/Tools/ | ? Should use ILogger |

---

## ?? NAVIGATION FLOW

### Declared Routes (MauiProgram.cs)

```csharp
Routing.RegisterRoute(NavigationRoutes.PromptDetails, typeof(PromptDetailsPage));
Routing.RegisterRoute(NavigationRoutes.EditPrompt, typeof(EditPromptPage));
Routing.RegisterRoute(NavigationRoutes.Guide, typeof(GuidePage));
Routing.RegisterRoute(NavigationRoutes.PromptBuilder, typeof(PromptBuilderPage));
Routing.RegisterRoute(NavigationRoutes.EngineWebView, typeof(EngineWebViewPage));
```

### Actual Navigation Patterns

#### Pattern 1: Direct Shell Navigation (? Not ideal)
```csharp
// Found in: GuidePage.xaml.cs, multiple ViewModels
await Shell.Current.GoToAsync("//AIWeb");
await Shell.Current.GoToAsync("..");
```

#### Pattern 2: BaseViewModel Helper (? Better)
```csharp
// Found in: BaseViewModel
protected async Task NavigateToAsync(string route)
{
    await Shell.Current.GoToAsync(route);
}

// Usage in ViewModels:
await NavigateToAsync(nameof(PromptBuilderPage));
```

#### Pattern 3: Hardcoded Routes (? Anti-pattern)
```csharp
// Found in various places
await Shell.Current.GoToAsync($"/{nameof(PromptDetailsPage)}?id={promptId}");
```

#### Pattern 4: Using Constants (? Best current practice)
```csharp
// Found in some ViewModels
await Shell.Current.GoToAsync(NavigationRoutes.PromptDetails);
```

**Issue:** Inconsistent navigation patterns across the app. No centralized NavigationService.

### Navigation Parameter Passing

```csharp
// Pattern 1: Query string
await Shell.Current.GoToAsync($"{nameof(PromptDetailsPage)}?PromptId={id}");

// Pattern 2: Dictionary
var parameters = new Dictionary<string, object>
{
    { "PromptId", promptId }
};
await Shell.Current.GoToAsync(route, parameters);

// Receiving (IQueryAttributable):
public void ApplyQueryAttributes(IDictionary<string, object> query)
{
    if (query.TryGetValue("PromptId", out var value))
        PromptId = (int)value;
}
```

---

## ?? UI COMPONENTS

### Reusable Controls

| Control | Location | Used In | Purpose |
|---------|----------|---------|---------|
| **PromptCard** | Presentation/Controls/ | QuickPromptPage | Display prompt summary |
| **AIProviderButton** | Presentation/Controls/ | AiLauncherPage | AI engine button |
| **StatusBadge** | Presentation/Controls/ | Various | Status indicators |
| **TitleHeader** | Presentation/Views/ | Multiple pages | Page header with back button |
| **PromptFilterBar** | Presentation/Views/ | QuickPromptPage | Filter prompts |
| **ReusableLoadingOverlay** | Presentation/Views/ | Multiple | Loading indicator |
| **EmptyStateView** | Presentation/Controls/ | List pages | Empty state UI |
| **ErrorStateView** | Presentation/Controls/ | List pages | Error state UI |
| **SkeletonView** | Presentation/Controls/ | List pages | Loading skeleton |
| **AdmobBannerView** | Infrastructure/ThirdParty/AdMob/Views/ | Multiple | Ad banner |

### XAML Converters

Located in `Presentation/Converters/`:

| Converter | Purpose |
|-----------|---------|
| BooleanToColorConverter | Bool ? Color |
| BooleanToStarIconConverter | Bool ? Star/empty star icon |
| InverseBoolConverter | Invert boolean |
| StringNotNullOrEmptyConverter | String validation ? bool |
| StringEqualsConverter | String comparison ? bool |
| CategoryToDisplayNameConverter | Enum ? readable string |
| FilterToColorConverter | Filter type ? color |
| FinalPromptVisibilityConverter | Visibility logic |
| PromptReadyToShowConverter | Display readiness |
| SelectedPromptsVisibilityConverter | Selection state |
| IntConverters | Int ? bool/visibility |
| IsFocusedAndHasSuggestionsConverter | Multi-binding |

**Issue:** Some converters registered in `Resources/Styles/Converters.xaml`, others used inline. Inconsistent pattern.

---

## ?? DEPENDENCY INJECTION SETUP

### Registered Services (MauiProgram.cs)

```csharp
// ? Repositories
builder.Services.AddSingleton<IPromptRepository, PromptRepository>();
builder.Services.AddSingleton<IFinalPromptRepository, FinalPromptRepository>();
builder.Services.AddSingleton<DatabaseServiceManager>();

// ? Infrastructure Services
builder.Services.AddSingleton<AdmobService>();
builder.Services.AddSingleton<IWebViewInjectionService, WebViewInjectionService>();

// ? History Services
builder.Services.AddSingleton<IExecutionHistoryRepository>(...);
builder.Services.AddSingleton<IExecutionHistoryCloudRepository>(...);
builder.Services.AddSingleton<SyncService>(...);
builder.Services.AddSingleton<ExecutionHistoryIntegration>(...);

// ? Settings Services
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<ISessionService, SessionService>();

// ? ViewModels
builder.Services.AddTransient<MainPageViewModel>();
builder.Services.AddTransient<SettingViewModel>();
builder.Services.AddScoped<PromptDetailsPageViewModel>();
builder.Services.AddScoped<EditPromptPageViewModel>();
builder.Services.AddScoped<QuickPromptViewModel>();
builder.Services.AddTransient<AiLauncherViewModel>();
builder.Services.AddTransient<PromptBuilderPageViewModel>();
builder.Services.AddTransient<SettingsViewModel>();

// ? Pages
builder.Services.AddTransient<MainPage>();
builder.Services.AddTransient<PromptDetailsPage>();
builder.Services.AddTransient<EditPromptPage>();
builder.Services.AddTransient<SettingPage>();
builder.Services.AddScoped<QuickPromptPage>();
builder.Services.AddTransient<EngineWebViewPage>();
builder.Services.AddTransient<AiLauncherPage>();
```

### Missing from DI (Should be registered)

```
? AlertService (static)
? GenericToolBox (static)
? TabBarHelperTool (static)
? PromptVariableCache (static)
? DebugLogger (static)
? GuidePage (page not registered)
```

---

## ?? DOMAIN MODEL

### Core Entities

#### PromptTemplate
```csharp
public class PromptTemplate : BaseModel
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string Template { get; set; }
    public PromptCategory Category { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime CreatedDate { get; set; }
    
    // ?? Has factory method (good) but also has public setters (bad)
    public static PromptTemplate CreatePromptTemplate(...) { }
}
```

#### FinalPrompt
```csharp
public class FinalPrompt : BaseModel
{
    public int PromptTemplateId { get; set; }
    public string FilledTemplate { get; set; }
    public string Variables { get; set; } // JSON
    public DateTime CreatedDate { get; set; }
    public bool IsFavorite { get; set; }
}
```

#### ExecutionHistoryEntry
```csharp
public class ExecutionHistoryEntry
{
    public string Id { get; set; }
    public int PromptId { get; set; }
    public string EngineType { get; set; }
    public DateTime Timestamp { get; set; }
    public string DeviceId { get; set; }
    public bool IsSynced { get; set; }
}
```

### Enums

```csharp
public enum PromptCategory
{
    General,
    Writing,
    Translation,
    Marketing,
    Development,
    Education,
    Business,
    Creative
}

public enum StepType
{
    Text,
    Suggestion,
    Final
}
```

### Value Objects / DTOs

```csharp
// DTOs
public class ImportablePrompt { ... }
public class FinalPromptDTO { ... }

// Models (should be value objects?)
public class VariableInput { ... }
public class PromptPart { ... }
public class VariableSuggestionSelection { ... }
public class NavigationParams { ... }
```

**Issue:** No clear distinction between Entities, Value Objects, and DTOs. All in `Core/Models/Domain/` or `Core/Models/DTOs/`.

---

## ?? FEATURE ANALYSIS

### Feature 1: Prompt Management (Core Feature)

**Pages:**
- MainPage (Create)
- QuickPromptPage (Execute)
- PromptDetailsPage (View)
- EditPromptPage (Edit)

**ViewModels:**
- MainPageViewModel
- QuickPromptViewModel  
- PromptDetailsPageViewModel
- EditPromptPageViewModel

**Services:**
- PromptRepository
- FinalPromptRepository
- PromptVariableCache

**Status:** ? Fully functional but complex. Business logic embedded in ViewModels.

---

### Feature 2: AI Engine Integration

**Pages:**
- AiLauncherPage (Select engine)
- EngineWebViewPage (Execute in WebView)

**ViewModels:**
- AiLauncherViewModel
- EngineWebViewViewModel

**Services:**
- AiEngineRegistry
- WebViewInjectionService

**Status:** ? Functional. Clean separation.

---

### Feature 3: Settings & Preferences

**Pages:**
- SettingPage

**ViewModels:**
- SettingViewModel
- SettingsViewModel (duplicate?)

**Services:**
- SettingsService
- SessionService

**Status:** ?? Possible duplication between SettingViewModel and SettingsViewModel.

---

### Feature 4: Execution History (Partially implemented)

**Services:**
- ExecutionHistoryIntegration
- SqliteExecutionHistoryRepository
- SyncService

**Status:** ?? Backend ready, no UI implemented yet.

---

### Feature 5: Onboarding

**Pages:**
- GuidePage

**ViewModels:**
- None (code-behind)

**Status:** ? Functional but uses code-behind instead of MVVM.

---

## ?? EXTERNAL INTEGRATIONS

### AdMob (Ads)
- **Service:** AdmobService
- **Views:** AdmobBannerView
- **ViewModel:** AdmobBannerViewModel
- **Status:** ? Integrated

### SQLite (Database)
- **Provider:** DatabaseConnectionProvider
- **Package:** sqlite-net-pcl
- **Status:** ? Working

### Firestore (Cloud Sync - Optional)
- **Service:** FirestoreExecutionHistoryCloudRepository
- **Status:** ?? Not configured (using NullExecutionHistoryCloudRepository stub)

### Material Icons (Fonts)
- **Fonts:** MaterialIconsOutlined-Regular, MaterialIcons-Regular
- **Status:** ? Working (fixed in recent commits)

### Community Toolkit
- **MVVM:** CommunityToolkit.Mvvm
- **UI:** CommunityToolkit.Maui
- **Status:** ? Integrated

---

## ?? MESSAGING SYSTEM

### WeakReferenceMessenger Usage

**Messages Defined:**
- `UpdatedPromptMessage` (Shared/Messages/)
- `GuideMessages` (Shared/Messages/)

**Usage Pattern:**
```csharp
// Sending
WeakReferenceMessenger.Default.Send(new UpdatedPromptMessage(prompt));

// Receiving
WeakReferenceMessenger.Default.Register<UpdatedPromptMessage>(this, (r, m) => 
{
    // Handle message
});
```

**Status:** ? Used sparingly. Not documented which components communicate via messages.

---

## ?? KEY OBSERVATIONS

### Strengths ?
1. Clean dependency injection setup
2. Repository pattern properly implemented
3. MVVM structure present
4. SQLite data layer stable
5. Good separation between Infrastructure and Domain (partial)
6. Third-party integrations isolated

### Weaknesses ?
1. **No Application Layer** - Business logic in ViewModels
2. **Static classes** - Violates DI principles
3. **Inconsistent folder structure** - Mix of feature-based and layer-based
4. **ViewModels too large** - Some >250 LOC with mixed concerns
5. **No centralized NavigationService**
6. **No Result Pattern** - Try-catch everywhere
7. **Duplicate code** - AlertService vs GenericToolBox
8. **Incomplete features** - History tracking backend exists but no UI

### Risks ??
1. **Memory leaks** - WebView not properly disposed
2. **Race conditions** - Sync service not thread-safe
3. **State inconsistency** - Cache vs database
4. **Hardcoded routes** - Navigation fragility
5. **Untestable code** - Static helpers

---

## ?? COMPLEXITY METRICS

| Metric | Value | Status |
|--------|-------|--------|
| Avg ViewModel LOC | ~180 | ?? High |
| Max ViewModel LOC | ~300 | ? Too high |
| Static Classes | 5 | ? Should be 0 |
| Navigation Patterns | 4 | ?? Should be 1 |
| Folder Depth (max) | 5 | ? OK |
| Active Pages | 8 | ? Reasonable |
| Active Services | 15+ | ?? Growing |

---

## ?? DATA FLOW DIAGRAM

```
???????????????????????????????????????????????????????????????
?                         USER                                 ?
???????????????????????????????????????????????????????????????
                            ?
                            ?
???????????????????????????????????????????????????????????????
?                      XAML PAGE                               ?
?  - Data Binding                                              ?
?  - Event Handlers                                            ?
?  - IQueryAttributable (Navigation Parameters)                ?
???????????????????????????????????????????????????????????????
                            ?
                            ?
???????????????????????????????????????????????????????????????
?                     VIEW MODEL                               ?
?  - INotifyPropertyChanged                                    ?
?  - RelayCommand                                              ?
?  - ?? Business Logic (SHOULD NOT BE HERE)                    ?
?  - Navigation Logic                                          ?
?  - Try-Catch Error Handling                                  ?
???????????????????????????????????????????????????????????????
              ?                            ?
              ?                            ?
????????????????????????????   ????????????????????????????????
?     REPOSITORY           ?   ?    STATIC HELPERS             ?
?  - IPromptRepository     ?   ?  - AlertService (static)      ?
?  - IFinalPromptRepo      ?   ?  - GenericToolBox (static)    ?
?  - IExecutionHistory     ?   ?  - PromptVariableCache        ?
????????????????????????????   ?????????????????????????????????
             ?
             ?
???????????????????????????????????????????????????????????????
?                      SQLITE DATABASE                         ?
?  - PromptTemplate                                            ?
?  - FinalPrompt                                               ?
?  - ExecutionHistory                                          ?
???????????????????????????????????????????????????????????????
```

**Issue:** No Use Case / Application layer between ViewModel and Repository.

---

## ?? NAVIGATION MAP

```
AppShell
?
??? TabBar
?   ?
?   ??? /Quick ? QuickPromptPage
?   ?   ?
?   ?   ???? PromptDetailsPage
?   ?   ?    ?
?   ?   ?    ???? EditPromptPage
?   ?   ?    ?
?   ?   ?    ???? EngineWebViewPage
?   ?   ?
?   ?   ???? PromptBuilderPage
?   ?
?   ??? /Create ? MainPage
?   ?   ?
?   ?   ???? PromptBuilderPage
?   ?
?   ??? /AIWeb ? AiLauncherPage
?   ?   ?
?   ?   ???? EngineWebViewPage
?   ?
?   ??? /Setting ? SettingPage
?
??? Modal (First Launch)
    ?
    ???? GuidePage
```

---

## ?? CRITICAL PATHS

### Path 1: Create New Prompt
```
User opens app 
? MainPage (Create tab)
? User enters title, description, template
? User adds variables with <brackets>
? User selects category
? User clicks Save
? MainPageViewModel.SavePromptAsync()
? PromptRepository.SavePromptAsync()
? SQLite insert
? InterstitialAd shown
? Success animation
? Clear form
```

### Path 2: Execute Existing Prompt
```
User opens app
? QuickPromptPage (Quick tab - default)
? List of saved prompts displayed
? User clicks prompt
? Navigate to PromptDetailsPage
? User fills variables
? User clicks "Use Prompt"
? Prompt copied to clipboard
? OR Navigate to EngineWebViewPage
? WebView loads with prompt injected
```

### Path 3: AI Engine Launch
```
User opens AI tab
? AiLauncherPage
? User selects AI engine (ChatGPT, Gemini, etc.)
? User optionally enters prompt
? Navigate to EngineWebViewPage
? WebViewInjectionService injects prompt
? User interacts with AI in WebView
```

---

## ?? CONFIGURATION

### appsettings.json
```json
{
  "AppSettings": {
    "Version": "1.0.0"
  },
  "AdMobSettings": {
    "InterstitialAdId": "...",
    "Android": "...",
    "iOs": "..."
  }
}
```

### Preferences (Key-Value Storage)
- `HasSeenGuide` - First launch tracking
- `DeviceId` - Device identification
- `CloudSyncEnabled` - Sync preference
- (Other settings in SettingsModel)

### Constants
- `AppMessagesEng` - User-facing messages
- `PromptDateFilterLabels` - Filter labels
- `PromptDefaults` - Default values
- `NavigationRoutes` - Route constants

---

## ?? DEAD CODE CANDIDATES

### Confirmed Dead
- `ViewModels/HistoryViewModel.cs` - Empty file (0 logic)
- `Models/StepModel.cs` - Duplicate of `GuideStep`
- `Tests/Engines/` - Empty folder
- `Presentation/Popups/PopUps/` - Typo folder (should be `Popups/`)

### Potentially Unused
- `Presentation/ViewModels/RootViewModel.cs` - No references found
- `Features/Prompts/ViewModels/PromptTemplateViewModel.cs` - Unclear usage
- `Core/Utilities/Pagination/BlockHandler.cs` - No pagination implemented

### Duplicate Functionality
- `AlertService` vs `GenericToolBox` - Both show alerts/dialogs
- `SettingViewModel` vs `SettingsViewModel` - Possible duplication

---

## ? NEXT STEPS (Phase 1)

Based on this system map, Phase 1 will focus on:

1. **Create Application Layer**
   - Extract business logic from ViewModels
   - Implement Use Cases
   - Define clear interfaces

2. **Target ViewModels for Refactoring:**
   - MainPageViewModel (highest priority - 300 LOC)
   - QuickPromptViewModel (high priority - 250 LOC)
   - AiLauncherViewModel (medium priority - 180 LOC)

3. **Convert Static Classes to Services:**
   - PromptVariableCache ? IPromptCacheService
   - AlertService + GenericToolBox ? IDialogService
   - TabBarHelperTool ? ITabBarService

---

**Document Status:** ? COMPLETE  
**Next Document:** PHASE_0_RISKS.md  
**Phase Status:** In Progress
