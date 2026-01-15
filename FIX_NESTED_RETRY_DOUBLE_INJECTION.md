# FIX: Nested Retry Logic Causing Double Injection

## Problem Identification

Even after fixing the double injection bug in `TryInjectWithRetryAsync` (returning stored result instead of calling `TryInjectAsync` again), the prompt was **still being inserted twice**.

**User Observation:** "Aun persiste son los attempts que hace que se vuelva a insertar"

## Root Cause: Nested Retries

The double injection was caused by **nested retry logic** - retries happening at TWO levels:

1. **Outer Level:** `TryInjectWithRetryAsync` (up to 3 attempts)
2. **Inner Level:** `TryPersistentInjectionAsync` (internal retry after verification)

### The Problem Flow:

```
EngineWebViewPage.OnWebViewNavigated
    ?
TryInjectWithRetryAsync (attempt 1) ? OUTER RETRY
    ?
    InjectionService.TryInjectAsync
        ?
        TryPersistentInjectionAsync ? INNER LOGIC
            ?
            BuildPersistentInjectionScript
                ? FIRST INJECTION ?
            ?
            await Task.Delay(500)
            ?
            VerifyInjectionPersistedAsync
                ? Checks if content is still there
            ?
            IF content cleared:
                ?
                BuildSlowInjectionScript
                    ? SECOND INJECTION ? (NESTED RETRY)
```

### Why Verification Failed:

After a successful prompt injection and submit:
1. Input field gets the prompt value
2. Submit button is clicked
3. Page processes the request
4. **Page clears the input field** (normal behavior after submit)
5. Our verification checks if content is still there
6. Verification returns `failed:content-cleared`
7. We interpret this as injection failure
8. We retry with `BuildSlowInjectionScript` ? **DOUBLE INJECTION**

**The mistake:** Treating "input cleared after successful submit" as a failure.

## The Solution

### 1. Remove Nested Retry

**Before:**
```csharp
private async Task<InjectionResult> TryPersistentInjectionAsync(...)
{
    var result = await webView.EvaluateJavaScriptAsync(js);
    
    if (result.StartsWith("success"))
    {
        await Task.Delay(500);
        var verifyResult = await VerifyInjectionPersistedAsync(...);
        
        if (verifyResult.StartsWith("verified"))
        {
            return Success;
        }
        else
        {
            // ? PROBLEM: Inner retry
            await Task.Delay(300);
            var retryResult = await webView.EvaluateJavaScriptAsync(
                BuildSlowInjectionScript(...)); // SECOND INJECTION
            
            if (retryResult.StartsWith("success"))
            {
                return Success;
            }
        }
    }
    
    return Failed;
}
```

**After:**
```csharp
private async Task<InjectionResult> TryPersistentInjectionAsync(...)
{
    var js = BuildPersistentInjectionScript(descriptor, escapedPrompt);
    
    var result = await webView.EvaluateJavaScriptAsync(js);
    
    // ? Trust the injection script's result
    if (result != null && result.StartsWith("success"))
    {
        Debug.WriteLine("[Injection] Script executed successfully");
        return new InjectionResult { Status = InjectionStatus.Success };
    }
    
    return new InjectionResult 
    { 
        Status = InjectionStatus.Failed, 
        ErrorMessage = result ?? "Unknown error" 
    };
}
```

### 2. Trust the Injection Script

Instead of verifying if content persists, we now **trust the injection script's return value**:

- `success:value-set` ? **Consider it successful**
- The script handles all necessary steps internally:
  - Focus input (triggers framework handlers)
  - Set value (after 100ms delay)
  - Trigger events (input, change, keydown, keyup)
  - Click submit (after 300ms delay)

If the script returns "success", it means all steps completed without JavaScript errors.

### 3. Let Outer Retry Handle Failures

If injection truly fails (e.g., input not found, script error), `TryPersistentInjectionAsync` returns `Failed`, and the **outer loop** (`TryInjectWithRetryAsync`) will retry with exponential backoff (1s, 2s, 3s).

This provides proper retry logic without nested retries causing double injections.

## Code Changes Summary

### File: `Engines/Injection/WebViewInjectionService.cs`

#### Removed Methods:
- ? `BuildSlowInjectionScript` (inner retry script)
- ? `VerifyInjectionPersistedAsync` (verification check)

#### Simplified Method:
```csharp
private async Task<InjectionResult> TryPersistentInjectionAsync(
    MauiWebView webView, 
    AiEngineDescriptor descriptor, 
    string prompt, 
    CancellationToken cancellationToken)
{
    var escapedPrompt = EscapeJs(prompt);
    var js = BuildPersistentInjectionScript(descriptor, escapedPrompt);
    
    try
    {
        var result = await webView.EvaluateJavaScriptAsync(js);
        Debug.WriteLine($"[Injection] Script result: {result}");
        
        // Trust the script - if it says success, it's success
        if (result != null && result.StartsWith("success"))
        {
            Debug.WriteLine("[Injection] Script executed successfully");
            return new InjectionResult { Status = InjectionStatus.Success };
        }
        
        return new InjectionResult 
        { 
            Status = InjectionStatus.Failed, 
            ErrorMessage = result ?? "Unknown error" 
        };
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[Injection] Script exception: {ex.Message}");
        return new InjectionResult 
        { 
            Status = InjectionStatus.Failed, 
            ErrorMessage = ex.Message 
        };
    }
}
```

**Key Principle:** **One level of retry (outer), one injection attempt per call.**

## Complete Fixed Flow

```
User taps "Send to ChatGPT"
    ?
Navigation to EngineWebViewPage
    ?
OnWebViewNavigated
    ?
TryInjectWithRetryAsync (maxRetries: 3)
    ?
Attempt 1:
    ?
    TryInjectAsync
        ?
        await Task.Delay(2000) // Wait for page to load
        ?
        TryPersistentInjectionAsync
            ?
            BuildPersistentInjectionScript
                Focus input
                setTimeout(100ms) ? Set value + Events
                setTimeout(300ms) ? Click submit
                Return "success:value-set"
            ?
            EvaluateJavaScriptAsync returns "success:value-set"
            ?
            Return InjectionResult { Status = Success } ?
        ?
        Return Success
    ?
    lastResult = Success
    ?
    Return lastResult immediately (exit loop) ?
    ?
SetExecutionResult(Success)
    ?
InstallInputCleanerAsync (prevent re-injection)
    ?
RecordExecutionAsync (save to history)
    ?
DONE - Single injection, no duplicates ?
```

## Testing

### Expected Debug Output:

```
[EngineWebViewPage] Received: Engine=ChatGPT, Prompt=Write...
[EngineWebViewPage] WebView navigated to: https://chat.openai.com/
[EngineWebViewPage] Attempting injection for ChatGPT
[EngineWebViewPage] Injection attempt 1/3
[Injection] Starting injection for ChatGPT
[Injection] Prompt length: 245 characters
[Injection] Script result: success:value-set
[Injection] Script executed successfully
[Injection] SUCCESS for ChatGPT
[EngineWebViewPage] Injection succeeded on attempt 1
[EngineWebViewPage] Final injection result: Success
[EngineWebViewPage] Installing input cleaner to prevent re-injection
[QuickPrompt] Installing input cleaner
[QuickPrompt] Input cleaner installed successfully
[EngineWebViewPage] Execution recorded in history
```

**Key observation:** Only **ONE** `[Injection] Script result` log entry.

### Before Fix (Double Injection):

```
[Injection] Script result: success:value-set
[Injection] Persistence verification: failed:content-cleared ? Content cleared after submit
[Injection] Content was cleared, re-injecting... ? Inner retry triggered
[Injection] Script result: success:retry-complete ? SECOND INJECTION
```

### After Fix (Single Injection):

```
[Injection] Script result: success:value-set
[Injection] Script executed successfully ? Trusted immediately
[Injection] SUCCESS for ChatGPT
```

## Why This Fix Works

### 1. Single Level of Retry
- Only outer loop (`TryInjectWithRetryAsync`) handles retries
- Inner logic (`TryPersistentInjectionAsync`) does **one attempt**
- No nested retries = no double injection

### 2. Trust the Script
- Injection script is comprehensive (focus, set, events, submit)
- If script returns "success", all steps completed
- No need to verify - verification was causing false negatives

### 3. Proper Separation of Concerns
- **Injection Service:** Execute ONE injection attempt, return result
- **Retry Logic:** Handle failures with exponential backoff
- **Clean separation:** No overlap, no double execution

## Performance Impact

### Before:
- **Successful case:** 2 injections (original + verification retry)
- **Failed case:** Up to 3 + 3 = 6 injections (3 outer attempts × 2 inner retries)
- **Time:** ~3-5 seconds (extra retry delays)

### After:
- **Successful case:** 1 injection
- **Failed case:** Up to 3 injections (3 outer attempts)
- **Time:** ~2-3 seconds (no extra retry delays)

**Improvement:** 50-66% fewer injection attempts, faster overall flow.

## Summary

? **Fixed:** Removed nested retry logic
? **Removed:** `BuildSlowInjectionScript` method
? **Removed:** `VerifyInjectionPersistedAsync` method
? **Simplified:** `TryPersistentInjectionAsync` (trust script result)
? **Maintained:** Outer retry logic (up to 3 attempts with exponential backoff)
? **Result:** Single injection per attempt, no duplicates

**Root Cause:** Nested retries at two levels (outer loop + inner verification retry)

**Solution:** Remove inner retry, trust injection script result, let outer loop handle failures

**Key Insight:** "Content cleared after successful submit" is NORMAL, not a failure. Don't retry when the injection actually succeeded.

The fix ensures **exactly one injection attempt per call** to `TryInjectAsync`, eliminating the double injection completely.
