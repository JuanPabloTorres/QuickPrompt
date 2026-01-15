# STEP 6.1 — REGISTER MISSING SERVICES IN DI

## Branch
`feature/webview-engine-architecture`

## Objective
Register all new architecture services in DI to prevent runtime crashes when enabling new flows.

## Changes Made

### 1. Created NullExecutionHistoryCloudRepository
**File:** `History/Repositories/NullExecutionHistoryCloudRepository.cs`
- Null object pattern implementation for when Firestore is not configured
- All operations are no-ops to prevent crashes
- Returns empty results from queries

### 2. Created SessionService (Placeholder)
**File:** `Services/SessionService.cs`
- Placeholder implementation of `ISessionService`
- `IsLoggedIn` returns `false` by default
- All methods are no-ops until full Firebase Auth implementation (next step)

### 3. Updated MauiProgram.cs
**Added usings:**
- `QuickPrompt.Engines.Injection`
- `QuickPrompt.History.Repositories`
- `QuickPrompt.History.Sync`
- `QuickPrompt.History`
- `QuickPrompt.Settings`
- `QuickPrompt.Engines.WebView`

**Registered Services:**

#### Engine Services (Steps 1-2)
- `IWebViewInjectionService` ? `WebViewInjectionService` (Singleton)

#### History Services (Step 3)
- `IExecutionHistoryRepository` ? `SqliteExecutionHistoryRepository` (Singleton)
  - Uses `QuickPrompt.db3` path from `FileSystem.AppDataDirectory`
- `IExecutionHistoryCloudRepository` ? `NullExecutionHistoryCloudRepository` (Singleton)
  - Guarded: returns Null implementation until Firestore is configured
  - TODO: Replace with real Firestore repo when Firebase is configured
- `SyncService` (Singleton)
  - Depends on both repos, `ISessionService`, and `ISettingsService`
  - `isUserLoggedIn` func checks `SessionService.IsLoggedIn`
  - `isSyncEnabled` func reads `CloudSyncEnabled` from settings
- `ExecutionHistoryIntegration` (Singleton)
  - Depends on local repo and `SyncService`
  - Uses `DeviceId` from Preferences (generates new GUID if not exists)

#### Settings Services (Step 4)
- `ISettingsService` ? `SettingsService` (Singleton)

#### Session Service (Step 4, placeholder)
- `ISessionService` ? `SessionService` (Singleton)
  - Placeholder implementation, will be replaced with Firebase Auth

**Registered ViewModels:**
- `SettingsViewModel` (Transient) — NEW

**Registered Pages:**
- `EngineWebViewPage` (Transient) — NEW

**Registered Routes:**
- `EngineWebViewPage` — NEW

### 4. Updated EngineWebViewPage.xaml.cs
- Changed constructor to accept DI services:
  - `IWebViewInjectionService`
  - `ExecutionHistoryIntegration`
- Implemented `IQueryAttributable` to receive `EngineExecutionRequest` via navigation parameters
- Integrated history recording in `OnWebViewNavigated` after injection
- Fixed using statements and added `MauiWebView` type alias

### 5. Updated EngineExecutionRequest.cs
- Added `Status` property to `EngineExecutionResult` to match usage

## Service Lifetimes Rationale

| Service | Lifetime | Reason |
|---------|----------|--------|
| `IWebViewInjectionService` | Singleton | Stateless, can be shared |
| `IExecutionHistoryRepository` | Singleton | Manages SQLite connection, should be shared |
| `IExecutionHistoryCloudRepository` | Singleton | Manages Firestore connection, should be shared |
| `SyncService` | Singleton | Maintains sync queue state, must be shared |
| `ExecutionHistoryIntegration` | Singleton | Coordinates history recording, should be shared |
| `ISettingsService` | Singleton | Manages settings persistence, should be shared |
| `ISessionService` | Singleton | Manages auth state, must be shared |
| `SettingsViewModel` | Transient | Recreated per navigation |
| `EngineWebViewPage` | Transient | Recreated per navigation |

## Validation

? **Build successful** — no compilation errors
? **All new services registered** — no runtime DI exceptions expected
? **Firestore guarded** — uses Null implementation if not configured
? **Session service placeholder** — app can run without auth
? **Existing functionality preserved** — no changes to existing Pages/ViewModels logic

## Next Steps

- **Step 6.2:** Implement full `ISessionService` with Firebase Authentication
- **Step 6.3:** Configure Firestore and replace `NullExecutionHistoryCloudRepository`
- **Step 6.4:** Connect navigation to use `EngineWebViewPage` instead of legacy pages
- **Step 6.5:** Implement `INavigationService` and `IDialogService` to remove MVVM violations

## Notes

- `SyncService` factory uses `.GetAwaiter().GetResult()` to call async settings method in sync context. This is acceptable since settings are cached after first load.
- `DeviceId` is persisted in Preferences to maintain consistent identity across app sessions.
- All new services are backward-compatible: app can run without enabling new flows.
