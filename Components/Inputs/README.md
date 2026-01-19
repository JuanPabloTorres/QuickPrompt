# Input Components

**Category:** Form Controls  
**Design System:** 100% Compliant  
**Status:** ? Production Ready - Complete Input System

---

## ?? Components

### 1. **TextInput** ?
Standard text input field with label, validation, and error states.

**Usage:**
```xaml
<inputs:TextInput 
    Label="Email"
    Placeholder="Enter your email"
    Text="{Binding Email}"
    ErrorText="{Binding EmailError}"
    HasError="{Binding HasEmailError}"
    Keyboard="Email" />
```

**Features:**
- Label (optional)
- Placeholder text
- Error state with message
- Helper text
- Clear button
- Focus/unfocus borders
- Keyboard type customization
- Max length validation

---

### 2. **PasswordInput** ?
Password input field with visibility toggle.

**Usage:**
```xaml
<inputs:PasswordInput 
    Label="Password"
    Placeholder="Enter your password"
    Text="{Binding Password}"
    ErrorText="{Binding PasswordError}"
    HasError="{Binding HasPasswordError}"
    HelperText="Min 8 characters" />
```

**Features:**
- All TextInput features
- Password visibility toggle (eye icon)
- Show/hide password text
- Password requirements helper text

---

### 3. **SearchInput** ?
Optimized search input field.

**Usage:**
```xaml
<inputs:SearchInput 
    Placeholder="Search prompts..."
    Text="{Binding SearchQuery}"
    SearchCommand="{Binding SearchCommand}" />
```

**Features:**
- Search icon (left)
- Pill-shaped border
- Clear button (right)
- Return key triggers search
- Optimized for search UX

---

## ?? Input Component Status

| Component | Status | Design System | Documentation | Tests |
|-----------|--------|---------------|---------------|-------|
| TextInput | ? Complete | ? 100% | ? Yes | ? Planned |
| PasswordInput | ? Complete | ? 100% | ? Yes | ? Planned |
| SearchInput | ? Complete | ? 100% | ? Yes | ? Planned |

---

## ?? Design System Tokens Used

All inputs use identical token structure:

**Colors:**
- `PrimaryBlue` - Focus border
- `Danger` - Error border
- `BorderLight` - Default border
- `SurfaceCard` - Background
- `TextPrimary` - Text color
- `TextSecondary` - Helper text
- `Gray400` - Placeholder, icons

**Typography:**
- `BodySmallTextStyle` - Label
- `CaptionTextStyle` - Helper/error text
- `BodyTextStyle` - Input text (16sp)

**Spacing:**
- `SpacingXs` - Vertical spacing (4dp)
- `SpacingSm` - Icon spacing (8dp)
- `ThicknessMd` - Input padding (16dp)

**Sizing:**
- `InputHeightMedium` - Input height (48dp)
- `RadiusMd` - Border radius (8dp)
- `RadiusPill` - Pill radius (9999)
- `IconSizeSm` - Icon size (20dp)

---

## ?? Common Props

### **TextInput Props:**

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `Label` | string | "" | Input label |
| `Text` | string | "" | Input value (two-way binding) |
| `Placeholder` | string | "" | Placeholder text |
| `ErrorText` | string | "" | Error message |
| `HelperText` | string | "" | Helper text |
| `HasError` | bool | false | Error state |
| `ShowLabel` | bool | true | Show/hide label |
| `ShowClearButton` | bool | false | Show/hide clear button |
| `Keyboard` | Keyboard | Default | Keyboard type (Email, Numeric, etc.) |
| `IsPassword` | bool | false | Password mode |
| `MaxLength` | int | int.MaxValue | Maximum character length |

### **PasswordInput Props:**

Same as TextInput plus:

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `IsPasswordVisible` | bool | false | Password visibility state |

### **SearchInput Props:**

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `Text` | string | "" | Search query (two-way binding) |
| `Placeholder` | string | "Search..." | Placeholder text |
| `SearchCommand` | ICommand | null | Command to execute on search |
| `ShowClearButton` | bool | false | Show/hide clear button |

---

## ?? Usage Guide

### **Form Example:**

```xaml
<VerticalStackLayout Spacing="16">
    <!-- Email -->
    <inputs:TextInput 
        Label="Email"
        Placeholder="email@example.com"
        Text="{Binding Email}"
        ErrorText="{Binding EmailError}"
        HasError="{Binding HasEmailError}"
        Keyboard="Email" />

    <!-- Password -->
    <inputs:PasswordInput 
        Label="Password"
        Placeholder="Enter password"
        Text="{Binding Password}"
        ErrorText="{Binding PasswordError}"
        HasError="{Binding HasPasswordError}"
        HelperText="Minimum 8 characters" />

    <!-- Submit -->
    <buttons:PrimaryButton 
        Text="Sign In"
        Command="{Binding SignInCommand}" />
</VerticalStackLayout>
```

### **Search Example:**

```xaml
<inputs:SearchInput 
    Placeholder="Search prompts..."
    Text="{Binding SearchQuery}"
    SearchCommand="{Binding SearchCommand}" />

<CollectionView ItemsSource="{Binding FilteredPrompts}" />
```

### **Validation Example:**

```csharp
// ViewModel
public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty]
    private string email;

    [ObservableProperty]
    private string emailError;

    [ObservableProperty]
    private bool hasEmailError;

    partial void OnEmailChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            EmailError = "Email is required";
            HasEmailError = true;
        }
        else if (!IsValidEmail(value))
        {
            EmailError = "Invalid email format";
            HasEmailError = true;
        }
        else
        {
            EmailError = string.Empty;
            HasEmailError = false;
        }
    }
}
```

---

## ? Accessibility

All inputs include:
- ? **Minimum touch target:** 48dp height
- ? **AutomationId** for UI testing
- ? **SemanticProperties** for screen readers
- ? **Clear visual feedback** (focus, error states)
- ? **Keyboard type** optimization
- ? **Label association** for screen readers

---

## ?? Visual States

### **Default State:**
- Border: `BorderLight`
- Background: `SurfaceCard`
- Text: `TextPrimary`

### **Focus State:**
- Border: `PrimaryBlue`
- Background: `SurfaceCard`
- Clear button appears

### **Error State:**
- Border: `Danger`
- Error message visible (red text)
- Helper text hidden

### **Disabled State:**
- Opacity: 0.38
- Not interactive

---

## ? Phase 3.3 Complete

**Input System:** 3/3 inputs implemented  
**Design System Compliance:** 100%  
**Ready for Production:** ? Yes  

**Components:**
- ? TextInput - Standard text entry
- ? PasswordInput - Password with visibility toggle
- ? SearchInput - Optimized search field

**Total Components (Phase 3):** 10  
- 4 Buttons (Phase 3.2)
- 3 Inputs (Phase 3.3)
- 3 Existing (StatusBadge, EmptyStateView, ErrorStateView)
