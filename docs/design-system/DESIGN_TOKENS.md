# ?? QuickPrompt - Design Token Specification

**Version:** 1.0  
**Date:** January 15, 2025  
**Project:** Visual Modernization & UI/UX Refactoring  
**Phase:** 1 - Design Direction & Planning  
**Deliverable:** D1.2 - Design Token Specification  
**Status:** Final - Ready for XAML Implementation

---

## ?? PURPOSE

This document provides **XAML-ready specifications** for all design tokens defined in the Design Direction document. All values are formatted for direct implementation in .NET MAUI ResourceDictionaries.

**Target:** Phase 2 - Foundation (XAML ResourceDictionary creation)  
**Format:** Copy-paste ready for `Colors.xaml`, `Typography.xaml`, `Spacing.xaml`, etc.

---

## 1. COLOR TOKENS

### 1.1 Primary Palette (Brand Amber/Gold)

**Base:** `#EFB036` (Preserve existing brand color)

```xml
<!-- Resources/Styles/Colors.xaml -->
<!-- Primary Color Scale -->
<Color x:Key="Primary50">#FEF7ED</Color>
<Color x:Key="Primary100">#FDE8CC</Color>
<Color x:Key="Primary200">#FCD19A</Color>
<Color x:Key="Primary300">#FAB668</Color>
<Color x:Key="Primary400">#F69D3B</Color>
<Color x:Key="Primary500">#EFB036</Color>  <!-- BASE - Current brand color -->
<Color x:Key="Primary600">#D68A1F</Color>
<Color x:Key="Primary700">#B36915</Color>
<Color x:Key="Primary800">#8F4D0F</Color>
<Color x:Key="Primary900">#6B340A</Color>

<!-- Primary - Semantic Aliases -->
<Color x:Key="PrimaryColor">{StaticResource Primary500}</Color>
<Color x:Key="PrimaryLight">{StaticResource Primary400}</Color>
<Color x:Key="PrimaryDark">{StaticResource Primary600}</Color>
<Color x:Key="PrimaryText">{StaticResource Primary700}</Color>
```

**Usage Guidelines:**
- **Primary500:** Primary buttons, key actions, brand moments
- **Primary600:** Primary button pressed state
- **Primary400:** Primary button hover state
- **Primary50-200:** Subtle backgrounds, highlights
- **Primary700:** Text on light backgrounds (if using primary color for text)

**Contrast Requirements:**
- Use `White` on Primary400-900
- Use `Gray900` on Primary50-300

---

### 1.2 Secondary Palette (Professional Blue-Gray)

**Base:** Evolved from `#23486A` to more versatile blue-gray scale

```xml
<!-- Secondary Color Scale -->
<Color x:Key="Secondary50">#F0F4F8</Color>
<Color x:Key="Secondary100">#D9E2EC</Color>
<Color x:Key="Secondary200">#BCCCDC</Color>
<Color x:Key="Secondary300">#9FB3C8</Color>
<Color x:Key="Secondary400">#829AB1</Color>
<Color x:Key="Secondary500">#627D98</Color>
<Color x:Key="Secondary600">#486581</Color>
<Color x:Key="Secondary700">#334E68</Color>  <!-- Closest to original #23486A -->
<Color x:Key="Secondary800">#243B53</Color>
<Color x:Key="Secondary900">#102A43</Color>

<!-- Secondary - Semantic Aliases -->
<Color x:Key="SecondaryColor">{StaticResource Secondary600}</Color>
<Color x:Key="SecondaryLight">{StaticResource Secondary500}</Color>
<Color x:Key="SecondaryDark">{StaticResource Secondary700}</Color>
<Color x:Key="SecondaryText">{StaticResource Secondary800}</Color>
```

**Usage Guidelines:**
- **Secondary600:** Secondary buttons, navigation
- **Secondary700:** Headings, important text (preserves brand recognition)
- **Secondary800:** High contrast text
- **Secondary200-300:** Borders, dividers
- **Secondary50-100:** Subtle backgrounds

**Contrast Requirements:**
- Use `White` on Secondary600-900
- Use `Gray900` on Secondary50-500

---

### 1.3 Semantic Colors - Success (Green)

```xml
<!-- Success Color Scale -->
<Color x:Key="Success50">#ECFDF5</Color>
<Color x:Key="Success100">#D1FAE5</Color>
<Color x:Key="Success200">#A7F3D0</Color>
<Color x:Key="Success300">#6EE7B7</Color>
<Color x:Key="Success400">#34D399</Color>
<Color x:Key="Success500">#10B981</Color>  <!-- BASE -->
<Color x:Key="Success600">#059669</Color>
<Color x:Key="Success700">#047857</Color>
<Color x:Key="Success800">#065F46</Color>
<Color x:Key="Success900">#064E3B</Color>

<!-- Success - Semantic Aliases -->
<Color x:Key="SuccessColor">{StaticResource Success500}</Color>
<Color x:Key="SuccessLight">{StaticResource Success400}</Color>
<Color x:Key="SuccessDark">{StaticResource Success600}</Color>
<Color x:Key="SuccessText">{StaticResource Success700}</Color>
```

**Usage:** Success toasts, confirmation messages, valid input states, checkmarks

---

### 1.4 Semantic Colors - Error (Red)

```xml
<!-- Error Color Scale -->
<Color x:Key="Error50">#FEF2F2</Color>
<Color x:Key="Error100">#FEE2E2</Color>
<Color x:Key="Error200">#FECACA</Color>
<Color x:Key="Error300">#FCA5A5</Color>
<Color x:Key="Error400">#F87171</Color>
<Color x:Key="Error500">#EF4444</Color>  <!-- BASE -->
<Color x:Key="Error600">#DC2626</Color>
<Color x:Key="Error700">#B91C1C</Color>
<Color x:Key="Error800">#991B1B</Color>
<Color x:Key="Error900">#7F1D1D</Color>

<!-- Error - Semantic Aliases -->
<Color x:Key="ErrorColor">{StaticResource Error500}</Color>
<Color x:Key="ErrorLight">{StaticResource Error400}</Color>
<Color x:Key="ErrorDark">{StaticResource Error600}</Color>
<Color x:Key="ErrorText">{StaticResource Error700}</Color>
```

**Usage:** Error messages, validation failures, destructive actions, alerts

---

### 1.5 Semantic Colors - Warning (Amber)

```xml
<!-- Warning Color Scale -->
<Color x:Key="Warning50">#FFFBEB</Color>
<Color x:Key="Warning100">#FEF3C7</Color>
<Color x:Key="Warning200">#FDE68A</Color>
<Color x:Key="Warning300">#FCD34D</Color>
<Color x:Key="Warning400">#FBBF24</Color>
<Color x:Key="Warning500">#F59E0B</Color>  <!-- BASE -->
<Color x:Key="Warning600">#D97706</Color>
<Color x:Key="Warning700">#B45309</Color>
<Color x:Key="Warning800">#92400E</Color>
<Color x:Key="Warning900">#78350F</Color>

<!-- Warning - Semantic Aliases -->
<Color x:Key="WarningColor">{StaticResource Warning500}</Color>
<Color x:Key="WarningLight">{StaticResource Warning400}</Color>
<Color x:Key="WarningDark">{StaticResource Warning600}</Color>
<Color x:Key="WarningText">{StaticResource Warning700}</Color>
```

**Usage:** Warning messages, caution states, important notices

---

### 1.6 Semantic Colors - Info (Blue)

```xml
<!-- Info Color Scale -->
<Color x:Key="Info50">#EFF6FF</Color>
<Color x:Key="Info100">#DBEAFE</Color>
<Color x:Key="Info200">#BFDBFE</Color>
<Color x:Key="Info300">#93C5FD</Color>
<Color x:Key="Info400">#60A5FA</Color>
<Color x:Key="Info500">#3B82F6</Color>  <!-- BASE -->
<Color x:Key="Info600">#2563EB</Color>
<Color x:Key="Info700">#1D4ED8</Color>
<Color x:Key="Info800">#1E40AF</Color>
<Color x:Key="Info900">#1E3A8A</Color>

<!-- Info - Semantic Aliases -->
<Color x:Key="InfoColor">{StaticResource Info500}</Color>
<Color x:Key="InfoLight">{StaticResource Info400}</Color>
<Color x:Key="InfoDark">{StaticResource Info600}</Color>
<Color x:Key="InfoText">{StaticResource Info700}</Color>
```

**Usage:** Informational messages, tips, helpful guidance, links

---

### 1.7 Neutral Colors (Gray Scale)

**Complete 9-shade scale for backgrounds, text, borders**

```xml
<!-- Neutral/Gray Color Scale -->
<Color x:Key="Gray50">#F9FAFB</Color>
<Color x:Key="Gray100">#F3F4F6</Color>
<Color x:Key="Gray200">#E5E7EB</Color>
<Color x:Key="Gray300">#D1D5DB</Color>
<Color x:Key="Gray400">#9CA3AF</Color>
<Color x:Key="Gray500">#6B7280</Color>
<Color x:Key="Gray600">#4B5563</Color>
<Color x:Key="Gray700">#374151</Color>
<Color x:Key="Gray800">#1F2937</Color>
<Color x:Key="Gray900">#111827</Color>

<!-- Pure Colors -->
<Color x:Key="White">#FFFFFF</Color>
<Color x:Key="Black">#000000</Color>
```

**Usage Guidelines:**
- **Gray50-200:** Backgrounds (app, card, elevated)
- **Gray200-400:** Borders, dividers, disabled states
- **Gray400-600:** Secondary text, placeholders, subtle icons
- **Gray700-900:** Primary text, headings, labels, icons

---

### 1.8 Surface Colors (Light Mode)

```xml
<!-- Surface Colors for Light Mode -->
<Color x:Key="SurfaceBackground">{StaticResource Gray50}</Color>
<Color x:Key="SurfaceCard">{StaticResource White}</Color>
<Color x:Key="SurfaceElevated">{StaticResource White}</Color>
<Color x:Key="SurfaceInput">{StaticResource White}</Color>
<Color x:Key="SurfaceOverlay">rgba(0, 0, 0, 0.5)</Color>
```

**Usage:**
- **SurfaceBackground:** App-level background
- **SurfaceCard:** Card/container backgrounds
- **SurfaceElevated:** Elevated elements (with shadow)
- **SurfaceInput:** Input field backgrounds
- **SurfaceOverlay:** Modal/dialog overlays

---

### 1.9 Text Colors

```xml
<!-- Text Colors -->
<Color x:Key="TextPrimary">{StaticResource Gray900}</Color>        <!-- rgba(17, 24, 39, 1.0) -->
<Color x:Key="TextSecondary">{StaticResource Gray600}</Color>      <!-- rgba(75, 85, 99, 1.0) -->
<Color x:Key="TextTertiary">{StaticResource Gray500}</Color>       <!-- rgba(107, 114, 128, 1.0) -->
<Color x:Key="TextDisabled">{StaticResource Gray400}</Color>       <!-- rgba(156, 163, 175, 1.0) -->
<Color x:Key="TextInverse">{StaticResource White}</Color>          <!-- For dark backgrounds -->
<Color x:Key="TextLink">{StaticResource Info600}</Color>
<Color x:Key="TextLinkHover">{StaticResource Info700}</Color>
<Color x:Key="TextLinkVisited">{StaticResource Info800}</Color>
```

---

### 1.10 Border Colors

```xml
<!-- Border Colors -->
<Color x:Key="BorderDefault">{StaticResource Gray300}</Color>
<Color x:Key="BorderLight">{StaticResource Gray200}</Color>
<Color x:Key="BorderDark">{StaticResource Gray400}</Color>
<Color x:Key="BorderFocus">{StaticResource Primary500}</Color>
<Color x:Key="BorderError">{StaticResource Error500}</Color>
<Color x:Key="BorderSuccess">{StaticResource Success500}</Color>
<Color x:Key="BorderWarning">{StaticResource Warning500}</Color>
```

---

### 1.11 State Colors (Disabled, Hover, Pressed)

```xml
<!-- State Colors -->
<Color x:Key="StateDisabledBackground">{StaticResource Gray200}</Color>
<Color x:Key="StateDisabledText">{StaticResource Gray400}</Color>
<Color x:Key="StateDisabledBorder">{StaticResource Gray300}</Color>

<Color x:Key="StateHoverBackground">{StaticResource Gray100}</Color>
<Color x:Key="StatePressedBackground">{StaticResource Gray200}</Color>
```

---

## 2. TYPOGRAPHY TOKENS

### 2.1 Font Families

```xml
<!-- Resources/Styles/Typography.xaml -->
<!-- Font Families -->
<x:String x:Key="FontFamilyPrimary">OpenSans-Regular</x:String>
<x:String x:Key="FontFamilySemiBold">OpenSans-SemiBold</x:String>
<x:String x:Key="FontFamilyDisplay">Designer</x:String>
```

**Font Registration (Already in MauiProgram.cs or App.xaml):**
```csharp
// Verify these fonts are registered:
.ConfigureFonts(fonts =>
{
    fonts.AddFont("OpenSans-Regular.ttf", "OpenSans-Regular");
    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSans-SemiBold");
    fonts.AddFont("Designer.otf", "Designer");
});
```

---

### 2.2 Font Sizes

```xml
<!-- Font Sizes (in Device-Independent Pixels) -->
<x:Double x:Key="FontSizeDisplay">40</x:Double>
<x:Double x:Key="FontSizeHeading1">28</x:Double>
<x:Double x:Key="FontSizeHeading2">22</x:Double>
<x:Double x:Key="FontSizeHeading3">18</x:Double>
<x:Double x:Key="FontSizeBodyLarge">16</x:Double>
<x:Double x:Key="FontSizeBody">15</x:Double>
<x:Double x:Key="FontSizeBodySmall">13</x:Double>
<x:Double x:Key="FontSizeCaption">12</x:Double>
<x:Double x:Key="FontSizeButton">15</x:Double>
<x:Double x:Key="FontSizeButtonSmall">14</x:Double>
<x:Double x:Key="FontSizeButtonLarge">16</x:Double>
```

---

### 2.3 Line Heights

```xml
<!-- Line Heights (multipliers of font size) -->
<x:Double x:Key="LineHeightDisplay">1.2</x:Double>    <!-- Tight for headlines -->
<x:Double x:Key="LineHeightHeading">1.3</x:Double>    <!-- Headings -->
<x:Double x:Key="LineHeightBody">1.5</x:Double>       <!-- Body text (readable) -->
<x:Double x:Key="LineHeightCaption">1.4</x:Double>    <!-- Small text -->
<x:Double x:Key="LineHeightButton">1.0</x:Double>     <!-- Tight for buttons -->
```

**Note:** MAUI calculates line height as `FontSize * LineHeight`. Example: 15sp × 1.5 = 22.5sp line height.

---

### 2.4 Letter Spacing

```xml
<!-- Letter Spacing (in EM units) -->
<x:Double x:Key="LetterSpacingTight">-0.02</x:Double>   <!-- Display text -->
<x:Double x:Key="LetterSpacingNormal">0.0</x:Double>    <!-- Default -->
<x:Double x:Key="LetterSpacingWide">0.01</x:Double>     <!-- Captions, buttons -->
```

---

### 2.5 Text Styles (Complete XAML Styles)

```xml
<!-- Display / Hero Text -->
<Style x:Key="DisplayTextStyle" TargetType="Label">
    <Setter Property="FontFamily" Value="{StaticResource FontFamilySemiBold}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeDisplay}" />
    <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    <Setter Property="LineHeight" Value="{StaticResource LineHeightDisplay}" />
    <Setter Property="CharacterSpacing" Value="{StaticResource LetterSpacingTight}" />
</Style>

<!-- Heading 1 -->
<Style x:Key="Heading1TextStyle" TargetType="Label">
    <Setter Property="FontFamily" Value="{StaticResource FontFamilySemiBold}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeHeading1}" />
    <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    <Setter Property="LineHeight" Value="{StaticResource LineHeightHeading}" />
    <Setter Property="CharacterSpacing" Value="{StaticResource LetterSpacingTight}" />
</Style>

<!-- Heading 2 -->
<Style x:Key="Heading2TextStyle" TargetType="Label">
    <Setter Property="FontFamily" Value="{StaticResource FontFamilySemiBold}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeHeading2}" />
    <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    <Setter Property="LineHeight" Value="{StaticResource LineHeightHeading}" />
    <Setter Property="CharacterSpacing" Value="{StaticResource LetterSpacingNormal}" />
</Style>

<!-- Heading 3 -->
<Style x:Key="Heading3TextStyle" TargetType="Label">
    <Setter Property="FontFamily" Value="{StaticResource FontFamilySemiBold}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeHeading3}" />
    <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    <Setter Property="LineHeight" Value="{StaticResource LineHeightHeading}" />
    <Setter Property="CharacterSpacing" Value="{StaticResource LetterSpacingNormal}" />
</Style>

<!-- Body Large -->
<Style x:Key="BodyLargeTextStyle" TargetType="Label">
    <Setter Property="FontFamily" Value="{StaticResource FontFamilyPrimary}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeBodyLarge}" />
    <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    <Setter Property="LineHeight" Value="{StaticResource LineHeightBody}" />
    <Setter Property="CharacterSpacing" Value="{StaticResource LetterSpacingNormal}" />
</Style>

<!-- Body (Default) -->
<Style x:Key="BodyTextStyle" TargetType="Label">
    <Setter Property="FontFamily" Value="{StaticResource FontFamilyPrimary}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeBody}" />
    <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    <Setter Property="LineHeight" Value="{StaticResource LineHeightBody}" />
    <Setter Property="CharacterSpacing" Value="{StaticResource LetterSpacingNormal}" />
</Style>

<!-- Body Secondary (gray text) -->
<Style x:Key="BodySecondaryTextStyle" TargetType="Label">
    <Setter Property="FontFamily" Value="{StaticResource FontFamilyPrimary}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeBody}" />
    <Setter Property="TextColor" Value="{StaticResource TextSecondary}" />
    <Setter Property="LineHeight" Value="{StaticResource LineHeightBody}" />
    <Setter Property="CharacterSpacing" Value="{StaticResource LetterSpacingNormal}" />
</Style>

<!-- Body Small -->
<Style x:Key="BodySmallTextStyle" TargetType="Label">
    <Setter Property="FontFamily" Value="{StaticResource FontFamilyPrimary}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeBodySmall}" />
    <Setter Property="TextColor" Value="{StaticResource TextSecondary}" />
    <Setter Property="LineHeight" Value="{StaticResource LineHeightBody}" />
    <Setter Property="CharacterSpacing" Value="{StaticResource LetterSpacingNormal}" />
</Style>

<!-- Caption -->
<Style x:Key="CaptionTextStyle" TargetType="Label">
    <Setter Property="FontFamily" Value="{StaticResource FontFamilyPrimary}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeCaption}" />
    <Setter Property="TextColor" Value="{StaticResource TextSecondary}" />
    <Setter Property="LineHeight" Value="{StaticResource LineHeightCaption}" />
    <Setter Property="CharacterSpacing" Value="{StaticResource LetterSpacingWide}" />
</Style>

<!-- Button Text -->
<Style x:Key="ButtonTextStyle" TargetType="Label">
    <Setter Property="FontFamily" Value="{StaticResource FontFamilySemiBold}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeButton}" />
    <Setter Property="LineHeight" Value="{StaticResource LineHeightButton}" />
    <Setter Property="CharacterSpacing" Value="{StaticResource LetterSpacingWide}" />
    <Setter Property="HorizontalTextAlignment" Value="Center" />
    <Setter Property="VerticalTextAlignment" Value="Center" />
</Style>
```

---

## 3. SPACING TOKENS

### 3.1 Spacing Scale (4px Base Unit)

```xml
<!-- Resources/Styles/Spacing.xaml -->
<!-- Spacing Constants (double values) -->
<x:Double x:Key="SpacingXs">4</x:Double>
<x:Double x:Key="SpacingSm">8</x:Double>
<x:Double x:Key="SpacingMd">16</x:Double>
<x:Double x:Key="SpacingLg">24</x:Double>
<x:Double x:Key="SpacingXl">32</x:Double>
<x:Double x:Key="Spacing2xl">48</x:Double>
<x:Double x:Key="Spacing3xl">64</x:Double>

<!-- Zero spacing (explicit) -->
<x:Double x:Key="SpacingNone">0</x:Double>
```

---

### 3.2 Thickness Presets (for Margin/Padding)

```xml
<!-- Uniform Thickness -->
<Thickness x:Key="ThicknessXs">4</Thickness>
<Thickness x:Key="ThicknessSm">8</Thickness>
<Thickness x:Key="ThicknessMd">16</Thickness>
<Thickness x:Key="ThicknessLg">24</Thickness>
<Thickness x:Key="ThicknessXl">32</Thickness>
<Thickness x:Key="Thickness2xl">48</Thickness>
<Thickness x:Key="Thickness3xl">64</Thickness>

<!-- Horizontal Only (Left, Right) -->
<Thickness x:Key="ThicknessHorizontalMd">16,0,16,0</Thickness>
<Thickness x:Key="ThicknessHorizontalLg">24,0,24,0</Thickness>

<!-- Vertical Only (Top, Bottom) -->
<Thickness x:Key="ThicknessVerticalMd">0,16,0,16</Thickness>
<Thickness x:Key="ThicknessVerticalLg">0,24,0,24</Thickness>

<!-- Asymmetric (Common patterns) -->
<Thickness x:Key="ThicknessPageMargin">16,20,16,16</Thickness>  <!-- Mobile: Left, Top, Right, Bottom -->
<Thickness x:Key="ThicknessCardPadding">16</Thickness>
<Thickness x:Key="ThicknessCardPaddingLarge">24</Thickness>
```

---

### 3.3 Component-Specific Spacing

```xml
<!-- Button Padding -->
<Thickness x:Key="ButtonPaddingSmall">12,6,12,6</Thickness>      <!-- Horizontal, Vertical -->
<Thickness x:Key="ButtonPaddingMedium">16,12,16,12</Thickness>
<Thickness x:Key="ButtonPaddingLarge">20,14,20,14</Thickness>

<!-- Input Padding -->
<Thickness x:Key="InputPadding">12,10,12,10</Thickness>

<!-- Card Spacing -->
<x:Double x:Key="CardGap">16</x:Double>  <!-- Gap between cards in grid/list -->

<!-- List Item Spacing -->
<x:Double x:Key="ListItemPadding">12</x:Double>
<x:Double x:Key="ListItemGap">8</x:Double>  <!-- Vertical gap between items -->
```

---

## 4. SHADOW & ELEVATION TOKENS

### 4.1 Shadow Definitions

```xml
<!-- Resources/Styles/Shadows.xaml -->
<!-- Shadow objects for different elevation levels -->

<!-- Level 0: Flat (No Shadow) -->
<Shadow x:Key="ShadowNone" 
        Radius="0" 
        Opacity="0" 
        Offset="0,0" />

<!-- Level 1: Raised (Subtle elevation) -->
<Shadow x:Key="ShadowLevel1" 
        Radius="2" 
        Opacity="0.05" 
        Offset="0,1"
        Brush="{StaticResource Black}" />

<!-- Level 2: Floating -->
<Shadow x:Key="ShadowLevel2" 
        Radius="6" 
        Opacity="0.10" 
        Offset="0,4"
        Brush="{StaticResource Black}" />

<!-- Level 3: Overlay -->
<Shadow x:Key="ShadowLevel3" 
        Radius="15" 
        Opacity="0.15" 
        Offset="0,10"
        Brush="{StaticResource Black}" />

<!-- Level 4: Modal -->
<Shadow x:Key="ShadowLevel4" 
        Radius="25" 
        Opacity="0.20" 
        Offset="0,20"
        Brush="{StaticResource Black}" />
```

**Usage:**
- **ShadowNone:** App background, flat surfaces
- **ShadowLevel1:** Cards, subtle elevation
- **ShadowLevel2:** Floating elements, modals, hover states
- **ShadowLevel3:** Dialogs, bottom sheets
- **ShadowLevel4:** Full-screen overlays, critical modals

---

## 5. BORDER RADIUS TOKENS

```xml
<!-- Border Radius Values -->
<x:Double x:Key="RadiusSm">4</x:Double>
<x:Double x:Key="RadiusMd">8</x:Double>
<x:Double x:Key="RadiusLg">12</x:Double>
<x:Double x:Key="RadiusXl">16</x:Double>
<x:Double x:Key="RadiusPill">9999</x:Double>  <!-- Pill shape (fully rounded) -->

<!-- Component-Specific Radius -->
<x:Double x:Key="ButtonRadius">{StaticResource RadiusMd}</x:Double>
<x:Double x:Key="InputRadius">{StaticResource RadiusSm}</x:Double>
<x:Double x:Key="CardRadius">{StaticResource RadiusMd}</x:Double>
<x:Double x:Key="ModalRadius">{StaticResource RadiusLg}</x:Double>
```

---

## 6. SIZING TOKENS

### 6.1 Component Heights

```xml
<!-- Component Heights -->
<x:Double x:Key="ButtonHeightSmall">36</x:Double>
<x:Double x:Key="ButtonHeightMedium">44</x:Double>   <!-- Default, meets 44dp touch target -->
<x:Double x:Key="ButtonHeightLarge">52</x:Double>

<x:Double x:Key="InputHeight">44</x:Double>  <!-- Standard input field -->
<x:Double x:Key="InputHeightSmall">36</x:Double>

<x:Double x:Key="IconSizeXs">16</x:Double>
<x:Double x:Key="IconSizeSm">20</x:Double>
<x:Double x:Key="IconSizeMd">24</x:Double>
<x:Double x:Key="IconSizeLg">32</x:Double>
<x:Double x:Key="IconSizeXl">48</x:Double>
```

### 6.2 Layout Widths

```xml
<!-- Layout Width Constraints -->
<x:Double x:Key="ContentMaxWidth">1200</x:Double>  <!-- Desktop max content width -->
<x:Double x:Key="ContentMaxWidthNarrow">680</x:Double>  <!-- For readable text -->
```

---

## 7. ANIMATION TOKENS

### 7.1 Duration

```xml
<!-- Animation Durations (in milliseconds) -->
<x:Int32 x:Key="AnimationDurationInstant">100</x:Int32>
<x:Int32 x:Key="AnimationDurationFast">200</x:Int32>
<x:Int32 x:Key="AnimationDurationMedium">300</x:Int32>
<x:Int32 x:Key="AnimationDurationSlow">500</x:Int32>
```

**Usage:**
- **Instant (100ms):** Hover, button press
- **Fast (200ms):** Most transitions, state changes
- **Medium (300ms):** Page navigation, modals
- **Slow (500ms):** Emphasis animations

### 7.2 Easing

```xml
<!-- Easing Functions -->
<Easing x:Key="EaseOut">SinOut</Easing>  <!-- Most common, elements enter decisively -->
<Easing x:Key="EaseIn">SinIn</Easing>    <!-- Elements exit -->
<Easing x:Key="EaseInOut">SinInOut</Easing>  <!-- Symmetrical -->
<Easing x:Key="Linear">Linear</Easing>   <!-- Progress indicators -->
```

---

## 8. OPACITY TOKENS

```xml
<!-- Opacity Levels -->
<x:Double x:Key="OpacityDisabled">0.38</x:Double>
<x:Double x:Key="OpacitySecondary">0.60</x:Double>
<x:Double x:Key="OpacityTertiary">0.87</x:Double>
<x:Double x:Key="OpacityFull">1.0</x:Double>

<!-- Overlay Opacity -->
<x:Double x:Key="OpacityOverlayLight">0.3</x:Double>
<x:Double x:Key="OpacityOverlayDark">0.5</x:Double>
```

---

## 9. Z-INDEX / LAYERING

```xml
<!-- Z-Index for Layering (Informal, MAUI doesn't have explicit z-index) -->
<!-- Use these as conceptual guides for element ordering -->
<x:Int32 x:Key="ZIndexBase">0</x:Int32>          <!-- Default layer -->
<x:Int32 x:Key="ZIndexCard">1</x:Int32>          <!-- Cards above background -->
<x:Int32 x:Key="ZIndexDropdown">10</x:Int32>     <!-- Dropdowns -->
<x:Int32 x:Key="ZIndexModal">100</x:Int32>       <!-- Modals -->
<x:Int32 x:Key="ZIndexToast">200</x:Int32>       <!-- Toasts/snackbars -->
<x:Int32 x:Key="ZIndexLoading">300</x:Int32>     <!-- Full-screen loading -->
```

**Note:** In MAUI, use `ZIndex` property on views or Grid row/column ordering.

---

## 10. IMPLEMENTATION CHECKLIST

### Phase 2 - Foundation Tasks

**Week 1: ResourceDictionary Creation**

- [ ] Create `Resources/Styles/Colors.xaml`
  - [ ] Add all color tokens (Section 1)
  - [ ] Add semantic aliases
  - [ ] Add state colors

- [ ] Create `Resources/Styles/Typography.xaml`
  - [ ] Add font family tokens (Section 2.1)
  - [ ] Add font size tokens (Section 2.2)
  - [ ] Add line height tokens (Section 2.3)
  - [ ] Add letter spacing tokens (Section 2.4)
  - [ ] Add all text styles (Section 2.5)

- [ ] Create `Resources/Styles/Spacing.xaml`
  - [ ] Add spacing constants (Section 3.1)
  - [ ] Add thickness presets (Section 3.2)
  - [ ] Add component-specific spacing (Section 3.3)

- [ ] Create `Resources/Styles/Shadows.xaml`
  - [ ] Add shadow definitions (Section 4)

- [ ] Create `Resources/Styles/Tokens.xaml` (Misc)
  - [ ] Add border radius tokens (Section 5)
  - [ ] Add sizing tokens (Section 6)
  - [ ] Add animation tokens (Section 7)
  - [ ] Add opacity tokens (Section 8)

- [ ] Update `App.xaml` to merge all dictionaries
  ```xml
  <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
          <ResourceDictionary Source="Resources/Styles/Colors.xaml" />
          <ResourceDictionary Source="Resources/Styles/Typography.xaml" />
          <ResourceDictionary Source="Resources/Styles/Spacing.xaml" />
          <ResourceDictionary Source="Resources/Styles/Shadows.xaml" />
          <ResourceDictionary Source="Resources/Styles/Tokens.xaml" />
      </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
  ```

**Week 2: Hardcoded Value Elimination**

- [ ] Search for `Color.FromArgb(` in C# files
  - [ ] Replace with `Application.Current.Resources["ColorName"]`
  
- [ ] Search for hardcoded hex colors in XAML
  - [ ] Replace with `{StaticResource ColorName}`
  
- [ ] Search for arbitrary spacing values (10, 15, 20, etc.)
  - [ ] Replace with spacing tokens

- [ ] Verify all text uses defined styles
  - [ ] Replace `FontSize="16"` with `Style="{StaticResource BodyTextStyle}"`

- [ ] Run verification script (count hardcoded values = 0)

---

## 11. VERIFICATION SCRIPT

**PowerShell Script to Count Hardcoded Values:**

```powershell
# Save as: verify-tokens.ps1

Write-Host "?? Verifying Design Token Usage..." -ForegroundColor Cyan

$hardcodedColors = Select-String -Path "*.xaml","*.cs" -Pattern "#[0-9A-Fa-f]{6}" -Exclude "Colors.xaml" -Recurse
$hardcodedSizes = Select-String -Path "*.xaml" -Pattern 'FontSize="[0-9]+"' -Exclude "Typography.xaml" -Recurse
$hardcodedMargins = Select-String -Path "*.xaml" -Pattern 'Margin="[0-9]+"' -Exclude "Spacing.xaml" -Recurse

$colorCount = ($hardcodedColors | Measure-Object).Count
$sizeCount = ($hardcodedSizes | Measure-Object).Count
$marginCount = ($hardcodedMargins | Measure-Object).Count

Write-Host "`n?? Results:" -ForegroundColor Yellow
Write-Host "  Hardcoded Colors: $colorCount" -ForegroundColor $(if($colorCount -eq 0){"Green"}else{"Red"})
Write-Host "  Hardcoded Font Sizes: $sizeCount" -ForegroundColor $(if($sizeCount -eq 0){"Green"}else{"Red"})
Write-Host "  Hardcoded Margins: $marginCount" -ForegroundColor $(if($marginCount -eq 0){"Green"}else{"Red"})

$total = $colorCount + $sizeCount + $marginCount
Write-Host "`n  TOTAL VIOLATIONS: $total" -ForegroundColor $(if($total -eq 0){"Green"}else{"Red"})

if($total -eq 0) {
    Write-Host "`n? SUCCESS: Zero hardcoded values detected!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n? FAIL: Hardcoded values found. See above." -ForegroundColor Red
    exit 1
}
```

**Run:** `.\verify-tokens.ps1` in project root

---

## 12. TOKEN USAGE EXAMPLES

### Example 1: Using Color Tokens in XAML

**Before (Hardcoded):**
```xml
<Button Text="Save" 
        BackgroundColor="#EFB036" 
        TextColor="White" />
```

**After (Using Tokens):**
```xml
<Button Text="Save" 
        BackgroundColor="{StaticResource Primary500}" 
        TextColor="{StaticResource White}" />
```

---

### Example 2: Using Typography Styles

**Before (Inline):**
```xml
<Label Text="Welcome" 
       FontFamily="OpenSans-SemiBold"
       FontSize="28"
       TextColor="#111827" />
```

**After (Using Style):**
```xml
<Label Text="Welcome" 
       Style="{StaticResource Heading1TextStyle}" />
```

---

### Example 3: Using Spacing Tokens

**Before (Arbitrary):**
```xml
<StackLayout Spacing="15">
    <Label Text="Title" />
    <Label Text="Description" Margin="0,10,0,0" />
</StackLayout>
```

**After (Using Tokens):**
```xml
<StackLayout Spacing="{StaticResource SpacingMd}">
    <Label Text="Title" />
    <Label Text="Description" Margin="{StaticResource ThicknessVerticalSm}" />
</StackLayout>
```

---

### Example 4: Using Shadow Tokens

**Before:**
```xml
<Frame Shadow="0,4,6,0.1" />
```

**After:**
```xml
<Frame Shadow="{StaticResource ShadowLevel2}" />
```

---

## 13. ACCEPTANCE CRITERIA

This Design Token Specification will be accepted when:

- [x] All color tokens defined (Primary, Secondary, Semantic, Neutral)
- [x] All typography tokens defined (sizes, weights, line heights, styles)
- [x] All spacing tokens defined (7 levels + component-specific)
- [x] All shadow tokens defined (4 elevation levels)
- [x] All border radius tokens defined
- [x] All sizing tokens defined (heights, widths, icons)
- [x] All animation tokens defined (duration, easing)
- [x] XAML format ready for copy-paste
- [x] Usage examples provided
- [x] Verification script included
- [x] Implementation checklist complete

**Status:** ? **COMPLETE - READY FOR PHASE 2**

---

## 14. SUMMARY

**Total Tokens Defined:**
- **Colors:** 80+ tokens (4 palettes × 10 shades + semantic aliases)
- **Typography:** 15+ tokens + 10 complete styles
- **Spacing:** 7 base tokens + 10+ thickness presets
- **Shadows:** 5 elevation levels
- **Radius:** 5 size variants
- **Sizing:** 15+ component sizes
- **Animation:** 4 duration + 4 easing

**Total:** 150+ design tokens ready for implementation

**Next Step:** Phase 2 - Create XAML ResourceDictionaries using these specifications

---

**Document Status:** ? **FINAL - APPROVED FOR IMPLEMENTATION**  
**Phase:** 1 - Design Direction (D1.2 Complete)  
**Next Deliverable:** D1.3 - Component Inventory  
**Next Phase:** Phase 2 - Foundation (XAML Implementation)

