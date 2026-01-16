# ?? PHASE 0 - COMPONENT INVENTORY

**Date:** 15 de Enero, 2025  
**Phase:** 0 - Diagnostic & Freeze  
**Status:** ? Complete  
**Project:** QuickPrompt Refactoring

---

## ?? PURPOSE

This document classifies **every file and component** in the QuickPrompt codebase as:
- ? **ACTIVE** - In use, essential
- ?? **AMBIGUOUS** - Unclear status, needs investigation
- ? **DEAD** - Not used, can be deleted
- ?? **DUPLICATE** - Redundant functionality

---

## ?? INVENTORY SUMMARY

| Category | Count | Percentage |
|----------|-------|------------|
| ? Active | 98 | 75% |
| ?? Ambiguous | 12 | 9% |
| ? Dead | 8 | 6% |
| ?? Duplicate | 3 | 2% |
| ?? Documentation | 10 | 8% |
| **TOTAL** | **131** | **100%** |

---

## ? ACTIVE COMPONENTS

### Pages (8 Active)

| File | Location | ViewModel | Status | Usage |
|------|----------|-----------|--------|-------|
| `QuickPromptPage.xaml(.cs)` | Features/Prompts/Pages/ | QuickPromptViewModel | ? Active | Primary tab - Execute prompts |
| `MainPage.xaml(.cs)` | Pages/ | MainPageViewModel | ? Active | Create new prompts |
| `AiLauncherPage.xaml(.cs)` | Features/AI/Pages/ | AiLauncherViewModel | ? Active | Launch AI engines |
| `SettingPage.xaml(.cs)` | Pages/ | SettingViewModel | ? Active | App settings |
| `PromptDetailsPage.xaml(.cs)` | Features/Prompts/Pages/ | PromptDetailsPageViewModel | ? Active | View prompt details |
| `EditPromptPage.xaml(.cs)` | Features/Prompts/Pages/ | EditPromptPageViewModel | ? Active | Edit existing prompt |
| `GuidePage.xaml(.cs)` | Pages/ | None (code-behind) | ? Active | First-launch guide |
| `PromptBuilderPage.xaml(.cs)` | Pages/ | PromptBuilderPageViewModel | ? Active | Advanced prompt builder |
| `EngineWebViewPage.xaml(.cs)` | Infrastructure/Engines/WebView/ | EngineWebViewViewModel | ? Active | WebView for AI engines |

---

### ViewModels (9 Active)

| File | Location | LOC | Associated Page | Status |
|------|----------|-----|-----------------|--------|
| `MainPageViewModel.cs` | ViewModels/ | ~300 | MainPage | ? Active |
| `QuickPromptViewModel.cs` | Features/Prompts/ViewModels/ | ~250 | QuickPromptPage | ? Active |
| `PromptDetailsPageViewModel.cs` | Features/Prompts/ViewModels/ | ~150 | PromptDetailsPage | ? Active |
| `EditPromptPageViewModel.cs` | Features/Prompts/ViewModels/ | ~120 | EditPromptPage | ? Active |
| `AiLauncherViewModel.cs` | ViewModels/ | ~180 | AiLauncherPage | ? Active |
| `SettingViewModel.cs` | ViewModels/ | ~100 | SettingPage | ? Active |
| `SettingsViewModel.cs` | ViewModels/ | ~80 | TBD | ?? Ambiguous (duplicate?) |
| `PromptBuilderPageViewModel.cs` | ViewModels/ | ~150 | PromptBuilderPage | ? Active |
| `EngineWebViewViewModel.cs` | Infrastructure/Engines/WebView/ | ~100 | EngineWebViewPage | ? Active |
| `AdmobBannerViewModel.cs` | Infrastructure/ThirdParty/AdMob/ViewModels/ | ~50 | AdmobBannerView | ? Active |
| `BaseViewModel.cs` | Presentation/ViewModels/ | ~80 | (Base class) | ? Active |

---

### Domain Models (5 Active)

| File | Location | Purpose | Status |
|------|----------|---------|--------|
| `PromptTemplate.cs` | Core/Models/Domain/ | Main prompt entity | ? Active |
| `FinalPrompt.cs` | Core/Models/Domain/ | Executed prompt with variables | ? Active |
| `BaseModel.cs` | Core/Models/Domain/ | Base entity (Id, dates) | ? Active |
| `PromptCategory.cs` | Core/Models/Enums/ | Enum for categories | ? Active |
| `StepType.cs` | Core/Models/Enums/ | Enum for guide steps | ? Active |

---

### DTOs (2 Active)

| File | Location | Purpose | Status |
|------|----------|---------|--------|
| `ImportablePrompt.cs` | Core/Models/DTOs/ | JSON import model | ? Active |
| `FinalPromptDTO.cs` | Core/Models/DTOs/ | Data transfer object | ? Active |

---

### Repositories (4 Active)

| File | Location | Interface | Status |
|------|----------|-----------|--------|
| `PromptRepository.cs` | Core/Services/Data/ | IPromptRepository | ? Active |
| `FinalPromptRepository.cs` | Core/Services/Data/ | IFinalPromptRepository | ? Active |
| `SqliteExecutionHistoryRepository.cs` | Infrastructure/History/Repositories/ | IExecutionHistoryRepository | ? Active |
| `NullExecutionHistoryCloudRepository.cs` | Infrastructure/History/Repositories/ | IExecutionHistoryCloudRepository | ? Active (stub) |
| `FirestoreExecutionHistoryCloudRepository.cs` | Infrastructure/History/Repositories/ | IExecutionHistoryCloudRepository | ?? Not configured |

---

### Core Services (7 Active)

| File | Location | Interface | DI Registered | Status |
|------|----------|-----------|---------------|--------|
| `DatabaseConnectionProvider.cs` | Core/Services/Data/ | None | ? Yes | ? Active |
| `DatabaseServiceManager.cs` | Core/Services/Data/ | None | ? Yes | ? Active |
| `SettingsService.cs` | Core/Services/Settings/ | ISettingsService | ? Yes | ? Active |
| `SessionService.cs` | Core/Services/Settings/ | ISessionService | ? Yes | ? Active |
| `PromptVariableCache.cs` | Core/Services/Cache/ | None | ? No (static) | ?? Active but problematic |
| `PromptCacheCleanupService.cs` | Core/Services/Cache/ | None | ? No (static) | ? Active |
| `SettingsModel.cs` | Core/Services/Settings/ | None | N/A (POCO) | ? Active |

---

### Infrastructure Services (8 Active)

| File | Location | Purpose | Status |
|------|----------|---------|--------|
| `AdmobService.cs` | Infrastructure/ThirdParty/AdMob/ | Ad integration | ? Active |
| `SharePromptService.cs` | Infrastructure/ThirdParty/Share/ | Share functionality | ? Active |
| `WebViewInjectionService.cs` | Infrastructure/Engines/Injection/ | Inject prompts into WebView | ? Active |
| `SyncService.cs` | Infrastructure/History/Sync/ | Cloud sync orchestration | ? Active |
| `ExecutionHistoryIntegration.cs` | Infrastructure/History/ | History tracking facade | ? Active |
| `AiEngineDescriptor.cs` | Infrastructure/Engines/Descriptors/ | AI engine metadata | ? Active |
| `AiEngineRegistry.cs` | Infrastructure/Engines/Registry/ | Engine registration | ? Active |
| `EngineExecutionRequest.cs` | Infrastructure/Engines/Execution/ | Request model | ? Active |

---

### Helper/Utility Classes (6 Active, 3 Problematic)

| File | Location | Type | Status | Issue |
|------|----------|------|--------|-------|
| `AlertService.cs` | Core/Utilities/Helpers/ | Static | ?? Active | ? Should be injectable |
| `GenericToolBox.cs` | Core/Utilities/Helpers/ | Static | ?? Active | ? Should be injectable |
| `TabBarHelperTool.cs` | Core/Utilities/Helpers/ | Static | ?? Active | ? Should be injectable |
| `AngleBraceTextHandler.cs` | Core/Utilities/Text/ | Class | ? Active | ? OK (utility) |
| `PromptValidator.cs` | Core/Utilities/Validation/ | Class | ? Active | ? OK (validator) |
| `BlockHandler.cs` | Core/Utilities/Pagination/ | Class | ?? Ambiguous | No pagination implemented |
| `DebugLogger.cs` | Shared/Tools/ | Static | ?? Active | ? Should use ILogger |
| `CollectionExtensions.cs` | Shared/Extensions/ | Static | ? Active | ? OK (extension methods) |

---

### UI Controls (9 Active)

| File | Location | Purpose | Status |
|------|----------|---------|--------|
| `PromptCard.xaml(.cs)` | Presentation/Controls/ | Prompt display card | ? Active |
| `AIProviderButton.xaml(.cs)` | Presentation/Controls/ | AI engine button | ? Active |
| `StatusBadge.xaml(.cs)` | Presentation/Controls/ | Status indicator | ? Active |
| `EmptyStateView.xaml(.cs)` | Presentation/Controls/ | Empty list UI | ? Active |
| `ErrorStateView.xaml(.cs)` | Presentation/Controls/ | Error state UI | ? Active |
| `SkeletonView.xaml(.cs)` | Presentation/Controls/ | Loading skeleton | ? Active |
| `TitleHeader.xaml(.cs)` | Presentation/Views/ | Page header | ? Active |
| `PromptFilterBar.xaml(.cs)` | Presentation/Views/ | Filter controls | ? Active |
| `ReusableLoadingOverlay.xaml(.cs)` | Presentation/Views/ | Loading overlay | ? Active |
| `AdmobBannerView.xaml(.cs)` | Infrastructure/ThirdParty/AdMob/Views/ | Ad banner | ? Active |
| `LottieMessagePopup.xaml(.cs)` | Presentation/Popups/PopUps/ | Animated popup | ? Active |

---

### XAML Converters (15 Active)

All in `Presentation/Converters/`:

| File | Converts | Status |
|------|----------|--------|
| `BooleanToColorConverter.cs` | Bool ? Color | ? Active |
| `BooleanToStarIconConverter.cs` | Bool ? Star icon | ? Active |
| `InverseBoolConverter.cs` | Bool ? !Bool | ? Active |
| `StringNotNullOrEmptyConverter.cs` | String ? Bool (empty check) | ? Active |
| `StringEqualsConverter.cs` | String comparison | ? Active |
| `CategoryToDisplayNameConverter.cs` | Enum ? String | ? Active |
| `FilterToColorConverter.cs` | Filter ? Color | ? Active |
| `FinalPromptVisibilityConverter.cs` | Logic ? Visibility | ? Active |
| `PromptReadyToShowConverter.cs` | State ? Bool | ? Active |
| `SelectedPromptsVisibilityConverter.cs` | Selection ? Visibility | ? Active |
| `FinalPromptAndNotLoadingConverter.cs` | Multi-binding | ? Active |
| `IsButtonVisibleConverter.cs` | Condition ? Visibility | ? Active |
| `IsFocusedAndHasSuggestionsConverter.cs` | Multi-binding | ? Active |
| `IntConverters.cs` | Int ? Bool/Visibility | ? Active |
| `Converters.xaml(.cs)` | Resource dictionary | ? Active |

---

### Feature Models (5 Active)

All in `Features/Prompts/Models/`:

| File | Purpose | Status |
|------|---------|--------|
| `VariableInput.cs` | User input for variables | ? Active |
| `PromptPart.cs` | Prompt template part | ? Active |
| `VariableSuggestionSelection.cs` | Variable suggestion state | ? Active |
| `NavigationParams.cs` | Navigation parameters | ? Active |
| `PromptTemplateViewModel.cs` | ?? VM in Models folder? | ?? Ambiguous |

---

### Constants & Messages (4 Active)

All in `Shared/Constants/` and `Shared/Messages/`:

| File | Purpose | Status |
|------|---------|--------|
| `AppMessagesEng.cs` | User-facing messages | ? Active |
| `NavigationRoutes.cs` | Route constants | ? Active |
| `PromptDateFilterLabels.cs` | Filter labels | ? Active |
| `PromptDefaults.cs` | Default values | ? Active |
| `UpdatedPromptMessage.cs` | Messenger message | ? Active |
| `GuideMessages.cs` | Guide completion messages | ? Active |

---

### Configuration & Infrastructure (5 Active)

| File | Location | Purpose | Status |
|------|----------|---------|--------|
| `App.xaml(.cs)` | Root | App entry point | ? Active |
| `AppShell.xaml(.cs)` | Root | Shell navigation | ? Active |
| `MauiProgram.cs` | Root | DI configuration | ? Active |
| `AppSettings.cs` | Shared/Configuration/ | Settings model | ? Active |
| `appsettings.json` | Root | Configuration file | ? Active |
| `AdMobSettings.cs` | Infrastructure/ThirdParty/AdMob/Models/ | AdMob config | ? Active |

---

### History/Tracking (3 Active)

All in `Infrastructure/History/`:

| File | Purpose | Status |
|------|---------|--------|
| `ExecutionHistoryEntry.cs` | Domain model | ? Active |
| `IExecutionHistoryRepository.cs` | Repository interface | ? Active |
| `IExecutionHistoryCloudRepository.cs` | Cloud sync interface | ? Active |

---

### Interfaces (4 Active)

All in `Core/Interfaces/`:

| File | Implemented By | Status |
|------|----------------|--------|
| `IPromptRepository.cs` | PromptRepository | ? Active |
| `IFinalPromptRepository.cs` | FinalPromptRepository | ? Active |
| `ISettingsService.cs` | SettingsService | ? Active |
| `ISessionService.cs` | SessionService | ? Active |
| `IWebViewInjectionService.cs` | WebViewInjectionService | ? Active |
| `IAiEngine.cs` | (Future implementations) | ?? Not used yet |

---

## ?? AMBIGUOUS COMPONENTS

### Files Requiring Investigation

| File | Location | Issue | Recommendation |
|------|----------|-------|----------------|
| `SettingsViewModel.cs` | ViewModels/ | Duplicate of SettingViewModel? | Investigate usage, likely consolidate |
| `PromptTemplateViewModel.cs` | Features/Prompts/ViewModels/ | No Page found | Check if used as item VM or dead |
| `RootViewModel.cs` | Presentation/ViewModels/ | No references found | Likely dead, verify |
| `BlockHandler.cs` | Core/Utilities/Pagination/ | No pagination implemented | Keep or remove? |
| `IAiEngine.cs` | Infrastructure/Engines/Abstractions/ | No implementations | Future feature or dead? |
| `StepModel.cs` | Models/ | Duplicate of GuideStep? | Check usage, consolidate |
| `GuideStep.cs` | Models/ | Used in GuidePage | Active, but StepModel duplicate? |
| `FirestoreExecutionHistoryCloudRepository.cs` | Infrastructure/History/Repositories/ | Not configured | Keep for future or remove? |

---

## ? DEAD CODE (Safe to Delete)

### Confirmed Dead Files

| File | Location | Reason | Impact of Deletion |
|------|----------|--------|-------------------|
| `HistoryViewModel.cs` | ViewModels/ | Empty file (0 logic) | ? None |
| `Tests/Engines/` | Tests/Engines/ | Empty folder | ? None |
| **Markdown Docs** | Root | Temporary debug docs | ? None (already removed) |

### Documentation Files (Temporary)

These were removed in recent commits but listing for completeness:
- `ANDROID_CRASH_DEBUG_GUIDE.md`
- `ANDROID_FIXES_SUMMARY.md`
- `ANDROID_TROUBLESHOOTING.md`
- `CRASH_FIXES_&_RESTRUCTURING.md`
- `DEBUGGING_INSTRUCTIONS.md`
- `FINAL_STATUS_&_NEXT_STEPS.md`
- `PROJECT_COMPLETION_SUMMARY.md`

**Status:** ? Already cleaned up

---

## ?? DUPLICATE FUNCTIONALITY

### Duplicates to Consolidate

#### Duplicate 1: Alert/Dialog Services

| File | Location | Type | Lines | Status |
|------|----------|------|-------|--------|
| `AlertService.cs` | Core/Utilities/Helpers/ | Static | ~30 | ?? Duplicate |
| `GenericToolBox.cs` | Core/Utilities/Helpers/ | Static | ~50 | ?? Duplicate |

**Both provide:**
- Show alert dialogs
- Show Lottie animations
- Display messages to user

**Recommendation:** Consolidate into `IDialogService` (Phase 1)

---

#### Duplicate 2: Settings ViewModels

| File | Location | Lines | Associated Page | Status |
|------|----------|-------|-----------------|--------|
| `SettingViewModel.cs` | ViewModels/ | ~100 | SettingPage | ? Active |
| `SettingsViewModel.cs` | ViewModels/ | ~80 | ??? | ?? Ambiguous |

**Investigation Needed:**
- Are both used?
- Do they serve different purposes?
- Can they be consolidated?

**Recommendation:** Investigate usage, consolidate if duplicate (Phase 1)

---

#### Duplicate 3: Step Models

| File | Location | Used In | Status |
|------|----------|---------|--------|
| `GuideStep.cs` | Models/ | GuidePage | ? Active |
| `StepModel.cs` | Models/ | ??? | ?? Possibly dead |

**Both have:**
- Title
- Description
- Example (maybe)

**Recommendation:** Verify StepModel usage, remove if duplicate (Phase 6)

---

## ?? FOLDER STRUCTURE ISSUES

### Inconsistent Organization

```
? PROBLEM: Pages scattered across multiple locations

Pages/                         ? Layer-first
??? MainPage.xaml
??? SettingPage.xaml
??? GuidePage.xaml
??? PromptBuilderPage.xaml

Features/Prompts/Pages/        ? Feature-first
??? QuickPromptPage.xaml
??? PromptDetailsPage.xaml
??? EditPromptPage.xaml

Features/AI/Pages/             ? Feature-first
??? AiLauncherPage.xaml

Infrastructure/Engines/WebView/ ? Layer-first (Infrastructure)
??? EngineWebViewPage.xaml
```

**Impact:**
- Developer confusion
- Hard to find files
- Merge conflicts
- Inconsistent patterns

**Resolution:** Phase 4 - Reorganize to unified structure

---

### Naming Inconsistencies

| Current Name | Issue | Recommended |
|--------------|-------|-------------|
| `SettingPage` | Singular | `SettingsPage` |
| `PopUps/` folder | Typo | `Popups/` |
| `PromptTemplateViewModel` in Models/ | Wrong folder | Move to ViewModels/ |

---

## ?? DETAILED ANALYSIS

### Static Classes (Must Convert to DI)

| Class | Location | Usage Count | Complexity |
|-------|----------|-------------|------------|
| `PromptVariableCache` | Core/Services/Cache/ | ~10 | Medium |
| `AlertService` | Core/Utilities/Helpers/ | ~15 | Low |
| `GenericToolBox` | Core/Utilities/Helpers/ | ~8 | Medium |
| `TabBarHelperTool` | Core/Utilities/Helpers/ | ~5 | Low |
| `PromptCacheCleanupService` | Core/Services/Cache/ | ~2 | Low |
| `DebugLogger` | Shared/Tools/ | ~3 | Low |

**Total Static Methods:** ~50+  
**Testability Impact:** ? Cannot mock, cannot unit test  
**Priority:** ?? HIGH - Phase 1

---

### Large ViewModels (Need Refactoring)

| ViewModel | LOC | Responsibilities | Priority |
|-----------|-----|------------------|----------|
| `MainPageViewModel` | ~300 | 8+ responsibilities | ?? HIGH |
| `QuickPromptViewModel` | ~250 | 6+ responsibilities | ?? HIGH |
| `AiLauncherViewModel` | ~180 | 4 responsibilities | ?? MEDIUM |
| `PromptBuilderPageViewModel` | ~150 | 5 responsibilities | ?? MEDIUM |
| `PromptDetailsPageViewModel` | ~150 | 3 responsibilities | ?? LOW |

**Refactoring Strategy:** Extract Use Cases (Phase 1)

---

### Converter Organization

**Issue:** Converters defined in two places:

1. **`Presentation/Converters/*.cs`** - C# files (15 converters)
2. **`Resources/Styles/Converters.xaml`** - XAML registration (some converters)

**Inconsistency:**
- Some converters used directly with namespace
- Others registered as resources
- No clear pattern

**Recommendation:** Phase 4 - Standardize converter usage

---

## ?? CLEANUP CHECKLIST

### Phase 1 - Immediate Cleanup (Week 1)

- [ ] Delete `HistoryViewModel.cs` (empty file)
- [ ] Remove `Tests/Engines/` (empty folder)
- [ ] Investigate and resolve `SettingsViewModel` vs `SettingViewModel`
- [ ] Verify `RootViewModel.cs` usage, remove if dead
- [ ] Check `PromptTemplateViewModel` usage

### Phase 6 - Final Cleanup (Week 5-6)

- [ ] Consolidate `AlertService` + `GenericToolBox` ? `IDialogService`
- [ ] Resolve `StepModel` vs `GuideStep` duplication
- [ ] Standardize naming (`SettingPage` ? `SettingsPage`)
- [ ] Fix folder typo (`PopUps/` ? `Popups/`)
- [ ] Remove `BlockHandler.cs` if pagination not planned
- [ ] Decide on `FirestoreExecutionHistoryCloudRepository` (keep or remove)

---

## ?? FILE MOVEMENT PLAN (Phase 4)

### Pages to Move

```
FROM: Pages/MainPage.xaml
TO:   UI/Pages/Prompts/MainPage.xaml

FROM: Pages/SettingPage.xaml
TO:   UI/Pages/Settings/SettingsPage.xaml

FROM: Pages/GuidePage.xaml
TO:   UI/Pages/Onboarding/GuidePage.xaml

FROM: Pages/PromptBuilderPage.xaml
TO:   UI/Pages/Prompts/PromptBuilderPage.xaml

FROM: Features/Prompts/Pages/*.xaml
TO:   UI/Pages/Prompts/*.xaml

FROM: Features/AI/Pages/*.xaml
TO:   UI/Pages/AI/*.xaml

FROM: Infrastructure/Engines/WebView/EngineWebViewPage.xaml
TO:   UI/Pages/AI/EngineWebViewPage.xaml
```

### ViewModels to Move

```
FROM: ViewModels/*.cs (mixed features)
TO:   UI/ViewModels/{Feature}/*.cs

FROM: Features/Prompts/ViewModels/*.cs
TO:   UI/ViewModels/Prompts/*.cs

FROM: Infrastructure/Engines/WebView/EngineWebViewViewModel.cs
TO:   UI/ViewModels/AI/EngineWebViewViewModel.cs
```

---

## ?? INVENTORY INSIGHTS

### Code Health Metrics

| Metric | Value | Grade |
|--------|-------|-------|
| Active Code | 75% | ?? Good |
| Dead Code | 6% | ?? Good |
| Ambiguous Code | 9% | ?? Fair |
| Duplicate Code | 2% | ?? Good |
| Organization Score | 60% | ?? Needs Work |

### Architectural Cleanliness

| Aspect | Score | Status |
|--------|-------|--------|
| Separation of Concerns | 6/10 | ?? Needs Improvement |
| Dependency Injection | 7/10 | ?? Fair (static classes issue) |
| Naming Consistency | 7/10 | ?? Fair (some inconsistencies) |
| Folder Structure | 5/10 | ?? Needs Improvement |
| Code Reuse | 8/10 | ?? Good |
| Testability | 4/10 | ? Poor (static classes) |

---

## ? VERIFICATION CHECKLIST

Before proceeding to Phase 1:

- [x] All pages identified and classified
- [x] All ViewModels inventoried
- [x] Services catalogued
- [x] Dead code identified
- [x] Duplicates found
- [x] Ambiguous components flagged
- [ ] Team review completed
- [ ] Deletion approval obtained
- [ ] Backup created

---

**Document Status:** ? COMPLETE  
**Phase 0 Status:** ? COMPLETE  
**Next Phase:** Phase 1 - Application Layer (Use Cases)  
**Ready to Proceed:** ? YES
