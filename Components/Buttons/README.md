# Button Components

**Category:** Action Components  
**Design System:** 100% Compliant  
**Status:** ? Production Ready - Complete Button System

---

## ?? Components

### 1. **PrimaryButton** ?
Primary call-to-action button for main actions.

**Usage:**
```xaml
<buttons:PrimaryButton Text="Save" Command="{Binding SaveCommand}" />
```

**Style:** Filled background (PrimaryBlue), white text

---

### 2. **SecondaryButton** ?
Secondary actions (cancel, back, etc.)

**Usage:**
```xaml
<buttons:SecondaryButton Text="Cancel" Command="{Binding CancelCommand}" />
```

**Style:** Outlined border (PrimaryBlue), transparent background, colored text

---

### 3. **DangerButton** ?
Destructive actions (delete, remove)

**Usage:**
```xaml
<buttons:DangerButton Text="Delete" Command="{Binding DeleteCommand}" />
```

**Style:** Filled background (Danger red), white text

---

### 4. **GhostButton** ?
Subtle tertiary actions

**Usage:**
```xaml
<buttons:GhostButton Text="Learn More" Command="{Binding LearnCommand}" />
```

**Style:** Transparent background, colored text only

---

## ?? Button System Status

| Component | Status | Design System | Documentation | Tests |
|-----------|--------|---------------|---------------|-------|
| PrimaryButton | ? Complete | ? 100% | ? Yes | ? Planned |
| SecondaryButton | ? Complete | ? 100% | ? Yes | ? Planned |
| DangerButton | ? Complete | ? 100% | ? Yes | ? Planned |
| GhostButton | ? Complete | ? 100% | ? Yes | ? Planned |

---

## ?? Design System Tokens Used

All buttons use identical token structure:

**Colors:**
- `PrimaryBlue`, `Danger`, `White`
- `StateDisabledBackground`, `StateDisabledText`

**Typography:**
- `ButtonTextStyle`

**Spacing:**
- `ThicknessMd` (16dp padding)
- `SpacingSm` (8dp icon spacing)

**Sizing:**
- `ButtonHeightMedium` (44dp)
- `RadiusMd` (8dp)
- `IconSizeSm` (20dp)

**Animation:**
- `AnimationDurationFast` (200ms)
- `OpacityDisabled` (0.38)
- `OpacityFull` (1.0)

---

## ? Accessibility

All buttons include:
- ? **44dp minimum touch target**
- ? **AutomationId** for UI testing
- ? **SemanticProperties** for screen readers
- ? **Visual feedback** on press and disable
- ? **Command-based** navigation

---

## ?? Common Props

All button components share these properties:

| Prop | Type | Description |
|------|------|-------------|
| `Text` | string | Button label |
| `Command` | ICommand | Tap command |
| `CommandParameter` | object | Command parameter |
| `ImageSource` | ImageSource | Optional icon |
| `IsEnabled` | bool | Enable/disable state |
| `Opacity` | double | Visual opacity |

**Button-Specific Props:**
- **PrimaryButton**: `BackgroundColor`, `TextColor`
- **SecondaryButton**: `BorderColor`, `TextColor`
- **DangerButton**: `BackgroundColor`, `TextColor`
- **GhostButton**: `TextColor`

---

## ?? Usage Guide

### **Button Hierarchy**

Use buttons according to action priority:

1. **Primary** - Main action (Save, Submit, Create)
2. **Secondary** - Alternative action (Cancel, Back)
3. **Danger** - Destructive action (Delete, Remove)
4. **Ghost** - Subtle action (Learn More, Skip)

### **Example Dialog**

```xaml
<VerticalStackLayout Spacing="12">
    <buttons:PrimaryButton Text="Confirm" Command="{Binding ConfirmCommand}" />
    <buttons:SecondaryButton Text="Cancel" Command="{Binding CancelCommand}" />
</VerticalStackLayout>
```

### **Example Form**

```xaml
<Grid ColumnDefinitions="*,*" ColumnSpacing="12">
    <buttons:SecondaryButton Grid.Column="0" Text="Cancel" />
    <buttons:PrimaryButton Grid.Column="1" Text="Save" />
</Grid>
```

### **Example Destructive Action**

```xaml
<buttons:DangerButton 
    Text="Delete Account"
    Command="{Binding DeleteAccountCommand}">
    <buttons:DangerButton.ImageSource>
        <FontImageSource 
            FontFamily="MaterialIconsOutlined-Regular"
            Glyph="&#xe92b;"
            Color="White" />
    </buttons:DangerButton.ImageSource>
</buttons:DangerButton>
```

---

## ? Phase 3.2 Complete

**Button System:** 4/4 buttons implemented  
**Design System Compliance:** 100%  
**Ready for Production:** ? Yes
