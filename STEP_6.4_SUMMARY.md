# STEP 6.4 — CONNECT NEW ENGINE PIPELINE (MAKE IT OFFICIAL)

## Branch
`feature/webview-engine-architecture`

## Objective
Make `EngineWebViewPage` + `AiEngineRegistry` + `WebViewInjectionService` the default engine execution flow, replacing legacy pages.

## Changes Made

### 1. Updated BaseViewModel.SendPromptToAsync
**File:** `ViewModels/BaseViewModel.cs`

**Added Import:**
```csharp
using QuickPrompt.Engines.Execution;
```

**Updated Method:**
```csharp
protected async Task SendPromptToAsync(string pageName, string toastMessage, Guid promptID, string finalPrompt)
{
    await ExecuteWithLoadingAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(finalPrompt))
        {
            await AlertService.ShowAlert("Error", "No prompt generated.");
            return;
        }

        var toast = Toast.Make($"Opening {toastMessage}...", ToastDuration.Short);
        await toast.Show();

        // ?? Map legacy page names to engine names
        var engineName = MapPageNameToEngineName(pageName);

        // ?? Create EngineExecutionRequest for new architecture
        var request = new EngineExecutionRequest
        {
            EngineName = engineName,
            Prompt = finalPrompt
        };

        // ?? Navigate to EngineWebViewPage instead of legacy pages
        await NavigateToAsync("EngineWebViewPage", new Dictionary<string, object>
        {
            { "Request", request },
            { "PromptId", promptID }
        });
    });
}
```

**Added Helper Method:**
```csharp
/// <summary>
/// Maps legacy page names to engine names for new architecture.
/// </summary>
private string MapPageNameToEngineName(string pageName)
{
    return pageName switch
    {
        "ChatGptPage" => "ChatGPT",
        "GeminiPage" => "Gemini",
        "GrokPage" => "Grok",
        "CopilotChatPage" => "Copilot",
        _ => pageName // Fallback: use page name as-is
    };
}
```

## Flow Overview

### Before (Legacy)
```
PromptDetailsPage (XAML)
    ?
Button CommandParameter: NavigationParams { PageName = "ChatGptPage", ToolName = "ChatGPT" }
    ?
PromptDetailsPageViewModel.SendPromptToCommand
    ?
BaseViewModel.SendPromptToAsync("ChatGptPage", "ChatGPT", promptId, finalPrompt)
    ?
NavigateToAsync("ChatGptPage", { "TemplateId": promptId, "FinalPrompt": finalPrompt })
    ?
ChatGptPage.xaml.cs (legacy WebView with manual JS injection)
```

### After (New Architecture)
```
PromptDetailsPage (XAML)
    ?
Button CommandParameter: NavigationParams { PageName = "ChatGptPage", ToolName = "ChatGPT" }
    ?
PromptDetailsPageViewModel.SendPromptToCommand
    ?
BaseViewModel.SendPromptToAsync("ChatGptPage", "ChatGPT", promptId, finalPrompt)
    ?
MapPageNameToEngineName("ChatGptPage") ? "ChatGPT"
    ?
Create EngineExecutionRequest { EngineName = "ChatGPT", Prompt = finalPrompt }
    ?
NavigateToAsync("EngineWebViewPage", { "Request": request, "PromptId": promptId })
    ?
EngineWebViewPage.ApplyQueryAttributes receives Request
    ?
EngineWebViewViewModel obtains AiEngineDescriptor from AiEngineRegistry
    ?
WebView navigates to Descriptor.BaseUrl
    ?
OnWebViewNavigated triggers
    ?
WebViewInjectionService.TryInjectAsync (JS injection + fallback)
    ?
ExecutionHistoryIntegration.RecordExecutionAsync (save to SQLite + enqueue for sync)
```

## Page Name to Engine Name Mapping

| Legacy Page Name | Engine Name | BaseUrl |
|------------------|-------------|---------|
| **ChatGptPage** | ChatGPT | https://chat.openai.com/ |
| **GeminiPage** | Gemini | https://gemini.google.com/ |
| **GrokPage** | Grok | https://grok.x.ai/ |
| **CopilotChatPage** | Copilot | https://copilot.microsoft.com/ |

## UI Integration Points

### PromptDetailsPage.xaml (No Changes Required)
```xaml
<!-- Buttons still use legacy page names in XAML -->
<Button Text="GPT">
    <Button.CommandParameter>
        <models:NavigationParams PageName="ChatGptPage" ToolName="ChatGPT" />
    </Button.CommandParameter>
</Button>

<Button Text="Gemini">
    <Button.CommandParameter>
        <models:NavigationParams PageName="GeminiPage" ToolName="Gemini" />
    </Button.CommandParameter>
</Button>

<Button Text="Grok">
    <Button.CommandParameter>
        <models:NavigationParams PageName="GrokPage" ToolName="Grok" />
    </Button.CommandParameter>
</Button>

<Button Text="Copilot">
    <Button.CommandParameter>
        <models:NavigationParams PageName="CopilotChatPage" ToolName="Copilot" />
    </Button.CommandParameter>
</Button>
```

**Why no XAML changes?**
- XAML continues to use legacy page names for backwards compatibility
- `BaseViewModel.SendPromptToAsync` transparently maps to new architecture
- This allows incremental migration without breaking UI

## Legacy Pages Status

### ChatGptPage, GeminiPage, GrokPage, CopilotChatPage
- ? **Still exist in codebase**
- ? **Still registered in routing** (MauiProgram.ConfigureRouting)
- ? **No longer used** (navigation bypasses them)
- ?? **Can be deprecated/removed** in future cleanup

**Why keep them?**
- Safety net: if new architecture fails, can revert by changing one line
- Documentation: reference implementation for WebView patterns
- Testing: can compare behavior between old and new

## Execution History Integration

### When User Taps "Send to ChatGPT"

1. **Navigation:**
   ```csharp
   await NavigateToAsync("EngineWebViewPage", new Dictionary<string, object>
   {
       { "Request", request },
       { "PromptId", promptID }
   });
   ```

2. **WebView Loads:**
   ```csharp
   public void ApplyQueryAttributes(IDictionary<string, object> query)
   {
       if (query.TryGetValue("Request", out var requestObj) && requestObj is EngineExecutionRequest request)
       {
           _viewModel = new EngineWebViewViewModel(request);
           // ...
       }
   }
   ```

3. **JS Injection:**
   ```csharp
   private async void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
   {
       var result = await _injectionService.TryInjectAsync(
           webView,
           _viewModel.Descriptor,
           _viewModel.Request.Prompt,
           CancellationToken.None);
   }
   ```

4. **History Recording:**
   ```csharp
   await _historyIntegration.RecordExecutionAsync(
       new EngineExecutionResult
       {
           EngineName = _viewModel.Request.EngineName,
           Success = result.Status == InjectionStatus.Success,
           UsedFallback = result.Status == InjectionStatus.FallbackClipboard,
           Status = result.Status.ToString()
       },
       _viewModel.Request.EngineName,
       _viewModel.Request.Prompt
   );
   ```

5. **SQLite + Sync Queue:**
   ```csharp
   // ExecutionHistoryIntegration.RecordExecutionAsync
   var entry = new ExecutionHistoryEntry
   {
       Id = Guid.NewGuid(),
       EngineId = engineId,
       PromptCompiled = prompt,
       ExecutedAt = DateTime.UtcNow,
       Status = result.Status,
       UsedFallback = result.UsedFallback,
       DeviceId = _deviceId,
       UpdatedAt = DateTime.UtcNow,
       IsDeleted = false,
       IsSynced = false // Will be synced later
   };
   
   await _localRepo.AddAsync(entry); // Save to SQLite
   _syncService.Enqueue(entry); // Queue for cloud sync
   ```

## Benefits of New Architecture

### 1. Unified WebView Host
- ? Single `EngineWebViewPage` for all engines
- ? No code duplication across engine pages
- ? Centralized WebView configuration

### 2. Configurable Engines
- ? `AiEngineRegistry` centralized config
- ? Easy to add new engines (just add descriptor)
- ? Easy to update engine URLs/selectors

### 3. Robust Injection
- ? `WebViewInjectionService` with retry logic
- ? Automatic fallback to clipboard
- ? Cancellation support
- ? Debug logging

### 4. Execution History
- ? Every execution tracked in SQLite
- ? Sync queue for cloud backup
- ? Retry logic for failed syncs
- ? Device tracking for multi-device scenarios

### 5. Maintainability
- ? Clear separation of concerns
- ? Testable components (services injected via DI)
- ? Easy to extend (add new engines, injection strategies, etc.)

## Validation

? **Build successful** - No compilation errors
? **Navigation updated** - Uses `EngineWebViewPage` instead of legacy pages
? **Page name mapping** - Legacy names mapped to engine names
? **Backward compatible** - XAML unchanged, transparent migration
? **History integration** - Executions recorded and queued for sync
? **Legacy pages preserved** - Safety net for rollback

## Testing Checklist

### Manual Testing
- [ ] Navigate to PromptDetailsPage
- [ ] Generate a final prompt
- [ ] Tap "GPT" button ? verify EngineWebViewPage opens
- [ ] Verify WebView loads chat.openai.com
- [ ] Verify prompt auto-injected (or clipboard fallback shown)
- [ ] Check SQLite: ExecutionHistoryEntry created with IsSynced=false
- [ ] Repeat for Gemini, Grok, Copilot

### Edge Cases
- [ ] Empty prompt ? verify error alert shown
- [ ] Network offline ? verify WebView shows offline message
- [ ] JS injection fails ? verify clipboard fallback works
- [ ] Rapid navigation ? verify no crashes

### History Verification
- [ ] After execution, query SQLite:
  ```sql
  SELECT * FROM ExecutionHistoryEntry ORDER BY ExecutedAt DESC LIMIT 1;
  ```
- [ ] Verify fields populated correctly:
  - `EngineId` = "ChatGPT" (or other engine)
  - `PromptCompiled` = final prompt text
  - `Status` = "Success" or "FallbackClipboard"
  - `UsedFallback` = true/false
  - `IsSynced` = false
  - `DeviceId` = persistent device ID

## Future Improvements

### 1. Remove Legacy Pages (Low Priority)
```csharp
// MauiProgram.ConfigureRouting() - Remove these lines:
// Routing.RegisterRoute(nameof(ChatGptPage), typeof(ChatGptPage));
// Routing.RegisterRoute(nameof(GeminiPage), typeof(GeminiPage));
// Routing.RegisterRoute(nameof(GrokPage), typeof(GrokPage));
// Routing.RegisterRoute(nameof(CopilotChatPage), typeof(CopilotChatPage));

// Delete files:
// - Pages/ChatGptPage.xaml(.cs)
// - Pages/GeminiPage.xaml(.cs)
// - Pages/GrokPage.xaml(.cs)
// - Pages/CopilotChatPage.xaml(.cs)
```

### 2. Update XAML to Use Engine Names Directly
```xaml
<!-- Future: Pass engine name directly -->
<Button Text="GPT">
    <Button.CommandParameter>
        <engines:EngineExecutionRequest EngineName="ChatGPT" Prompt="{Binding FinalPrompt}" />
    </Button.CommandParameter>
</Button>
```

### 3. Add "Open External" Button
```csharp
// In EngineWebViewPage, add toolbar button:
<ToolbarItem Text="Open External" Command="{Binding OpenExternalCommand}" />

// In ViewModel:
[RelayCommand]
private async Task OpenExternal()
{
    await Browser.OpenAsync(_viewModel.Descriptor.BaseUrl);
}
```

### 4. Add History View UI
- Page to list all execution history
- Filter by engine, date, success/fallback
- Retry failed executions
- Delete history entries

## Notes

- **Shell.Current usage preserved:** Still uses `Shell.Current.GoToAsync` in `RootViewModel.NavigateToAsync`. Will be replaced with `INavigationService` in future sprint (Step 7).
  
- **Toast notifications:** Uses `Toast.Make` for "Opening {engine}..." feedback. Could be replaced with custom overlay for consistency.

- **ExecutionHistoryIntegration called from code-behind:** Currently called in `EngineWebViewPage.xaml.cs.OnWebViewNavigated`. This is acceptable for event handlers, but could be moved to ViewModel if preferred.

- **PromptId passed but not used:** `PromptId` is passed in navigation params but not currently used in `EngineWebViewPage`. Could be used for linking history to source prompt in future.

- **DeviceId generation:** Device ID is generated on first use and persisted in Preferences. Consider using `DeviceInfo.Name` or similar for better device identification.

## Summary

The new engine pipeline is now **official and active**:
- ? All "Send to AI" buttons use `EngineWebViewPage`
- ? Centralized configuration via `AiEngineRegistry`
- ? Robust injection with `WebViewInjectionService`
- ? Execution history automatically recorded
- ? Sync queue populated for cloud backup
- ? Legacy pages preserved but unused
- ? Zero breaking changes to UI/XAML

**Next steps:** Step 7 will implement `INavigationService` and `IDialogService` to eliminate remaining `Shell.Current` and `DisplayAlert` violations.
