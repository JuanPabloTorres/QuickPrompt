# DIAGNOSTIC: Gemini Clipboard Fallback Investigation

## Problem

Gemini continues to fall back to clipboard despite multiple selector improvements.
Need detailed diagnostic information to identify the correct selectors.

## Diagnostic Approach

### Added Comprehensive Logging

The injection script now includes detailed diagnostic information to help identify:
1. **What contenteditable elements exist** on the page
2. **Which selector patterns match** (if any)
3. **Element visibility status** (dimensions, attributes)
4. **Exact failure point** in the injection flow

### Diagnostic Output Format

```
[QuickPrompt] ========== DIAGNOSTIC START ==========
[QuickPrompt] DIAGNOSTIC: Found X contenteditable elements
[QuickPrompt] DIAGNOSTIC: Contenteditable 0: {
    tagName: "DIV",
    className: "...",
    ariaLabel: "...",
    dataPlaceholder: "...",
    offsetWidth: 500,
    offsetHeight: 60
}
[QuickPrompt] Test selector [0]: ... => FOUND/NOT FOUND
[QuickPrompt] Test selector [1]: ... => FOUND/NOT FOUND
...
[QuickPrompt] Input visibility check: {
    offsetWidth: X,
    offsetHeight: Y,
    isVisible: true/false
}
[QuickPrompt] ========== DIAGNOSTIC END ==========
```

### What to Check

1. **Run the app and navigate to Gemini**
2. **Check Debug Output window** for diagnostic logs
3. **Look for:**
   - How many contenteditable elements are found
   - What attributes they have (className, ariaLabel, etc.)
   - Which selector patterns match
   - If element is visible (offsetWidth/Height > 0)

### Expected Findings

**If logging shows:**

#### Case 1: No contenteditable elements found
```
[QuickPrompt] DIAGNOSTIC: Found 0 contenteditable elements
```
**Solution:** Gemini changed to a different input type (iframe, shadow DOM, etc.)

#### Case 2: Contenteditable exists but wrong selector
```
[QuickPrompt] DIAGNOSTIC: Found 1 contenteditable elements
[QuickPrompt] DIAGNOSTIC: Contenteditable 0: {
    ariaLabel: "Type something different",  ? Not "Enter"
    dataPlaceholder: null
}
[QuickPrompt] Test selector [0]: ... => NOT FOUND
[QuickPrompt] Test selector [3]: div[contenteditable="true"] => FOUND
```
**Solution:** Update InputSelector to simpler pattern without aria-label filter

#### Case 3: Element found but not visible
```
[QuickPrompt] Input visibility check: {
    offsetWidth: 0,  ? Hidden!
    offsetHeight: 0
}
[QuickPrompt] ERROR: Input found but not visible
```
**Solution:** Element exists but is hidden, need to find the actual visible input

#### Case 4: Element found but in different location
```
[QuickPrompt] DIAGNOSTIC: Contenteditable 0: {
    className: "some-new-class",
    ariaLabel: "Chat with Gemini"  ? New aria-label
}
```
**Solution:** Update selector to match new attributes

## Next Steps

### 1. Collect Diagnostic Data

Run the app with this diagnostic version and collect the output:

```bash
# Look for these logs in Debug Output
[QuickPrompt] ========== DIAGNOSTIC START ==========
...
[QuickPrompt] ========== DIAGNOSTIC END ==========
```

### 2. Analyze the Data

Based on the diagnostic output:
- Identify which selector pattern matches
- Check element attributes (className, ariaLabel, data-*)
- Verify visibility (offsetWidth/Height)

### 3. Update Selectors

Based on findings, update `AiEngineRegistry.cs`:

```csharp
["Gemini"] = new AiEngineDescriptor
{
    // Update based on diagnostic findings
    InputSelector = "NEW_SELECTOR_HERE",
    // ...
}
```

### 4. Test Without Diagnostics

Once correct selector is found:
- Update selector in registry
- Remove diagnostic logging (or keep for future debugging)
- Test that injection works

## Common Gemini Selector Patterns

Based on Gemini UI history, try these patterns if diagnostic shows:

### Pattern 1: Simple Contenteditable
```javascript
'div[contenteditable="true"]'  // Most generic, should match
```

### Pattern 2: With Aria-Label
```javascript
'div[contenteditable="true"][aria-label]'  // Has any aria-label
```

### Pattern 3: With Specific Class
```javascript
'div.ql-editor[contenteditable="true"]'  // Quill editor (common in Gemini)
```

### Pattern 4: Inside Specific Container
```javascript
'.input-container div[contenteditable="true"]'  // Within input container
```

### Pattern 5: With Data Attributes
```javascript
'div[contenteditable="true"][data-test-id]'  // Has test ID
```

## Temporary Workaround

While investigating, you can manually test selectors in browser console:

1. Open https://gemini.google.com/ in Chrome
2. Press F12 (Developer Tools)
3. Go to Console tab
4. Try selectors one by one:

```javascript
// Test each selector
document.querySelector('div[contenteditable="true"][aria-label*="Enter"]')
document.querySelector('div[contenteditable="true"]')
document.querySelector('[contenteditable="true"]')

// Check attributes of found element
var input = document.querySelector('[contenteditable="true"]');
if (input) {
    console.log({
        tagName: input.tagName,
        className: input.className,
        ariaLabel: input.getAttribute('aria-label'),
        dataAttrs: Array.from(input.attributes)
            .filter(a => a.name.startsWith('data-'))
            .map(a => a.name + '=' + a.value)
    });
}
```

## Manual Injection Test

Once you find the correct selector, test manually:

```javascript
// In Chrome console on gemini.google.com
var input = document.querySelector('YOUR_SELECTOR_HERE');
if (input) {
    input.focus();
    input.textContent = 'Test prompt';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    console.log('SUCCESS: Value set, length=' + input.textContent.length);
} else {
    console.log('FAILED: Input not found');
}
```

## Implementation Priority

Once diagnostic data is collected:

1. **Immediate:** Update InputSelector with working pattern
2. **Short-term:** Add working selector to Strategy 1
3. **Long-term:** Keep diagnostic logging for future UI changes
4. **Optional:** Create selector test suite for all providers

## Success Criteria

Diagnostic is successful when you can identify:
- ? Exact selector that matches Gemini input
- ? Element attributes (for future-proofing)
- ? Visibility status
- ? Location in DOM (parent elements)

This information allows updating the selector to match current Gemini UI.

## Cleanup

After finding correct selector and confirming it works:

**Option 1: Keep Diagnostics (Recommended)**
- Useful for future UI changes
- Can be disabled with a flag
- Minimal performance impact

**Option 2: Remove Diagnostics**
- Cleaner logs
- Slightly faster execution
- Need to re-add if UI changes again

**Recommended:** Keep diagnostic logs but wrap in conditional:
```csharp
private bool _enableDiagnostics = false; // Set to true when debugging

if (_enableDiagnostics) {
    console.log('[QuickPrompt] DIAGNOSTIC: ...');
}
```
