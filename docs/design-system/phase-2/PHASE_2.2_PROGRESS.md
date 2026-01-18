# ?? PHASE 2.2: HARDCODED VALUE ELIMINATION - IN PROGRESS

**Phase:** 2.2 - Hardcoded Value Elimination  
**Status:** ?? **IN PROGRESS** (1% complete)  
**Date:** January 15, 2025  
**Total Violations Found:** 262  
**Violations Fixed:** 4 (1.5%)  
**Remaining:** 258 (98.5%)

---

## ?? VERIFICATION RESULTS

### Tool Created: `verify-design-tokens.ps1`

**Comprehensive PowerShell script that scans for:**
- Hardcoded colors in C# files (`Color.FromArgb`, `Colors.ColorName`)
- Hardcoded colors in XAML files (inline hex values)
- Inline font sizes in XAML files  
- Arbitrary spacing/margin/padding values

---

## ?? VIOLATION BREAKDOWN

### **Total: 262 Violations**

| Category | Count | Percentage | Status |
|----------|-------|------------|--------|
| **C# Hardcoded Colors** | 31 | 12% | ?? 4 fixed (13%) |
| **XAML Hardcoded Colors** | 7 | 3% | ? Not started |
| **Inline Font Sizes** | 86 | 33% | ? Not started |
| **Arbitrary Spacing** | 138 | 53% | ? Not started |

---

## ?? 1. HARDCODED COLORS IN C# (31 violations)

### **By File:**

| File | Violations | Status |
|------|------------|--------|
| `Pages/GuidePage.xaml.cs` | 4 | ? **FIXED** |
| `ViewModels/PromptBuilderPageViewModel.cs` | 8 | ? To fix |
| `Presentation/Controls/StatusBadge.xaml.cs` | 7 | ? To fix |
| `Pages/MainPage.xaml.cs` | 3 | ? To fix |
| `Pages/EditPromptPage.xaml.cs` | 3 | ? To fix |
| `Presentation/Converters/BooleanToColorConverter.cs` | 2 | ? To fix |
| `Presentation/Converters/FilterToColorConverter.cs` | 2 | ? To fix |
| `Presentation/Controls/AIProviderButton.xaml.cs` | 1 | ? To fix |
| `Presentation/Controls/PromptCard.xaml.cs` | 1 | ? To fix |

### **Violations:**

**Fixed (4):**
- ? `GuidePage.xaml.cs` Line 113: `Color.FromArgb("#23486A")` ? `Application.Current.Resources["PrimaryBlueDark"]`
- ? `GuidePage.xaml.cs` Line 125: `Color.FromArgb("#EFB036")` ? `Application.Current.Resources["PrimaryYellow"]`
- ? `GuidePage.xaml.cs` Line 114: `Colors.White` ? `Application.Current.Resources["White"]`
- ? `GuidePage.xaml.cs` Line 123: `Colors.White` ? `Application.Current.Resources["White"]`

**Remaining (27):**
- ? MainPage.xaml.cs, EditPromptPage.xaml.cs (chip background colors)
- ? StatusBadge.xaml.cs (7 status colors)
- ? PromptBuilderPageViewModel.cs (8 step indicator colors)
- ? Converters (4 color conversions)
- ? Controls (2 default colors)

---

## ?? 2. HARDCODED COLORS IN XAML (7 violations)

### **By File:**

| File | Violations | Fix Strategy |
|------|------------|--------------|
| `AiLauncherPage.xaml` | 4 | Replace with semantic colors |
| `PromptDetailsPage.xaml` | 3 | Replace with surface/border colors |

### **Violations:**

**AI Provider Colors (4):**
- ? Line 39: `Color="#10A37F"` (ChatGPT green)
- ? Line 51: `Color="#8AB4F8"` (Gemini blue)
- ? Line 63: `Color="#000000"` (Grok black)
- ? Line 75: `Color="#0078D4"` (Copilot blue)

**Surface/Border Colors (3):**
- ? Line 105: `BackgroundColor="#F6F6F6"` ? `{StaticResource SurfaceBackground}`
- ? Line 106: `Stroke="#DDDDDD"` ? `{StaticResource BorderLight}`

---

## ?? 3. INLINE FONT SIZES (86 violations)

### **Top Offenders:**

| File | Violations | Priority |
|------|------------|----------|
| `PromptBuilderPage.xaml` | 16 | High |
| `AiLauncherPage.xaml` | 15 | High |
| `PromptCard.xaml` | 9 | High |
| `PromptDetailsPage.xaml` | 8 | Medium |
| `GuidePage.xaml` | 7 | Medium |
| `MainPage.xaml` | 5 | Medium |
| `AIProviderButton.xaml` | 5 | Medium |
| `QuickPromptPage.xaml` | 4 | Low |
| `EditPromptPage.xaml` | 4 | Low |
| Other files | 13 | Low |

### **Common Patterns:**

| Font Size | Count | Recommended Style |
|-----------|-------|-------------------|
| `FontSize="14"` | ~20 | `BodyTextStyle` (15sp) or `BodySmallTextStyle` (13sp) |
| `FontSize="16"` | ~15 | `BodyLargeTextStyle` (16sp) |
| `FontSize="18"` | ~8 | `Heading3TextStyle` (18sp) |
| `FontSize="20"` | ~5 | `Heading2TextStyle` (22sp) - adjust |
| `FontSize="22"` | ~3 | `Heading2TextStyle` (22sp) |
| `FontSize="24"` | ~3 | `Heading1TextStyle` (28sp) - adjust |
| `FontSize="28"` | ~2 | `Heading1TextStyle` (28sp) |
| Other sizes | ~30 | Context-specific styles |

---

## ?? 4. ARBITRARY SPACING (138 violations)

### **Top Offenders:**

| File | Violations | Priority |
|------|------------|----------|
| `AiLauncherPage.xaml` | 22 | High |
| `PromptBuilderPage.xaml` | 18 | High |
| `PromptDetailsPage.xaml` | 17 | High |
| `QuickPromptPage.xaml` | 12 | Medium |
| `EditPromptPage.xaml` | 11 | Medium |
| `MainPage.xaml` | 10 | Medium |
| `GuidePage.xaml` | 9 | Medium |
| `PromptCard.xaml` | 8 | Medium |
| `PromptFilterBar.xaml` | 7 | Low |
| Other files | 24 | Low |

### **Common Patterns:**

| Pattern | Count | Recommended Token |
|---------|-------|-------------------|
| `Padding="16"` or `Margin="16"` | ~30 | `{StaticResource ThicknessMd}` |
| `Padding="10"` or `Margin="10"` | ~25 | `{StaticResource SpacingSm}` (8) or custom |
| `Spacing="12"` | ~15 | `{StaticResource SpacingMd}` (16) - adjust |
| `Padding="20"` | ~10 | `{StaticResource SpacingLg}` (24) - adjust |
| `Margin="0,X,0,0"` (top only) | ~20 | Custom thickness or spacing |
| Other patterns | ~38 | Context-specific |

---

## ? FIXES APPLIED (4 violations)

### **GuidePage.xaml.cs** ?

**Before:**
```csharp
NextButton.BackgroundColor = Color.FromArgb("#23486A");
NextButton.TextColor = Colors.White;
```

**After:**
```csharp
NextButton.BackgroundColor = (Color)Application.Current.Resources["PrimaryBlueDark"];
NextButton.TextColor = (Color)Application.Current.Resources["White"];
```

**Impact:** Eliminated 4 hardcoded colors, now uses Design System tokens

---

## ?? FIX STRATEGY

### **Phase 1: C# Hardcoded Colors** (27 remaining)

**Priority Order:**
1. **High:** PromptBuilderPageViewModel.cs (8 violations) - Step indicator colors
2. **High:** StatusBadge.xaml.cs (7 violations) - Status colors
3. **Medium:** MainPage.xaml.cs, EditPromptPage.xaml.cs (6 violations) - Chip colors
4. **Medium:** Converters (4 violations) - Color conversions
5. **Low:** Controls (2 violations) - Default colors

**Estimated Time:** 2-3 hours

---

### **Phase 2: XAML Hardcoded Colors** (7 remaining)

**Priority Order:**
1. **High:** AiLauncherPage.xaml (4 violations) - AI provider brand colors
2. **Medium:** PromptDetailsPage.xaml (3 violations) - Surface/border colors

**Estimated Time:** 30 minutes

---

### **Phase 3: Inline Font Sizes** (86 remaining)

**Approach:** File-by-file replacement with Typography styles

**Priority Order:**
1. **High Priority Files** (40 violations):
   - PromptBuilderPage.xaml (16)
   - AiLauncherPage.xaml (15)
   - PromptCard.xaml (9)

2. **Medium Priority Files** (33 violations):
   - PromptDetailsPage.xaml (8)
   - GuidePage.xaml (7)
   - MainPage.xaml (5)
   - AIProviderButton.xaml (5)
   - QuickPromptPage.xaml (4)
   - EditPromptPage.xaml (4)

3. **Low Priority Files** (13 violations):
   - Remaining controls and views

**Estimated Time:** 4-5 hours

---

### **Phase 4: Arbitrary Spacing** (138 remaining)

**Approach:** Batch replacement with Spacing tokens

**Priority Order:**
1. **High Priority Files** (57 violations):
   - AiLauncherPage.xaml (22)
   - PromptBuilderPage.xaml (18)
   - PromptDetailsPage.xaml (17)

2. **Medium Priority Files** (51 violations):
   - QuickPromptPage.xaml (12)
   - EditPromptPage.xaml (11)
   - MainPage.xaml (10)
   - GuidePage.xaml (9)
   - PromptCard.xaml (8)

3. **Low Priority Files** (30 violations):
   - Remaining files

**Estimated Time:** 6-8 hours

---

## ?? TIMELINE

**Total Estimated Time:** 12-16 hours

**Day 1 (Today):**
- [x] Create verification script (1 hour) ?
- [x] Run initial scan (30 min) ?
- [x] Fix GuidePage.xaml.cs (30 min) ?
- [ ] Fix remaining C# colors (2-3 hours) ?

**Day 2:**
- [ ] Fix XAML hardcoded colors (30 min)
- [ ] Fix high-priority font sizes (3 hours)
- [ ] Fix medium-priority font sizes (2 hours)

**Day 3:**
- [ ] Fix low-priority font sizes (1 hour)
- [ ] Fix high-priority spacing (4 hours)
- [ ] Fix medium-priority spacing (3 hours)

**Day 4:**
- [ ] Fix low-priority spacing (2 hours)
- [ ] Run verification script ? Target: 0 violations
- [ ] Test app thoroughly
- [ ] Commit and document

---

## ?? SUCCESS CRITERIA

Phase 2.2 will be complete when:

- [ ] Zero hardcoded colors in C# files (31 ? 0)
- [ ] Zero hardcoded colors in XAML files (7 ? 0)
- [ ] Zero inline font sizes (86 ? 0)
- [ ] Zero arbitrary spacing values (138 ? 0)
- [ ] Verification script passes with 0 violations
- [ ] Build successful
- [ ] Visual parity maintained (no UI regressions)
- [ ] All 77 unit tests passing

---

## ?? PROGRESS TRACKING

```
C# Hardcoded Colors:     ???????????????????? 13% (4/31)
XAML Hardcoded Colors:   ????????????????????  0% (0/7)
Inline Font Sizes:       ????????????????????  0% (0/86)
Arbitrary Spacing:       ????????????????????  0% (0/138)

OVERALL PHASE 2.2:       ????????????????????  1.5% (4/262)
```

---

## ?? LESSONS LEARNED

### **What's Working Well:**
- ? Verification script provides clear visibility
- ? Application.Current.Resources pattern works perfectly
- ? Build remains stable after fixes

### **Challenges:**
- ?? Large number of violations (262) requires systematic approach
- ?? Some colors are AI provider brand colors (need decision: preserve or map to Design System?)
- ?? Font size mapping not always 1:1 (14sp ? 15sp BodyTextStyle)
- ?? Spacing values don't always match token scale (10 ? use 8 or 12?)

### **Decisions Needed:**
- **AI Provider Colors:** Preserve brand colors or map to semantic colors?
- **Font Size Adjustments:** Accept minor size changes (14?15, 20?22) or create custom tokens?
- **Non-Standard Spacing:** Create custom tokens or round to nearest standard value?

---

## ?? PROJECT STATUS UPDATE

**Overall Project Progress:**

```
Phase 1: Design Direction      ???????????????????? 100% ?
Phase 2: Foundation            ????????????????????  55% ?
  D2.1: ResourceDictionaries   ???????????????????? 100% ?
  D2.2: Hardcoded Elimination  ????????????????????   1% ?
Phase 3: Components            ????????????????????   0% ?
Phase 4: Pages                 ????????????????????   0% ?
Phase 5: Accessibility         ????????????????????   0% ?
Phase 6: Validation            ????????????????????   0% ?

OVERALL PROJECT:               ???????????????????? 19%
```

---

## ?? NEXT STEPS

### **Immediate (Next 2-3 hours):**

1. **Fix PromptBuilderPageViewModel.cs** (8 violations)
   - Step indicator colors (Context, Task, Examples, Format, Limits)
   
2. **Fix StatusBadge.xaml.cs** (7 violations)
   - Status colors (default, success, warning, error, info)

3. **Fix MainPage/EditPromptPage chip colors** (6 violations)
   - Variable chip background colors

4. **Fix Converters** (4 violations)
   - BooleanToColorConverter
   - FilterToColorConverter

5. **Run verification ? Target: 31 ? 10 violations**

---

**Report Status:** ?? **IN PROGRESS - 1.5% COMPLETE**  
**Next Update:** After C# color fixes complete  
**Phase 2.2 Completion:** Estimated Day 4

