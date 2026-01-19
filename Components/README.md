# QuickPrompt Component Library

**Version:** 1.0.0  
**Phase:** 3 - Component Library  
**Design System:** Fully integrated with Phase 2 Design Tokens

---

## ?? Overview

This folder contains production-ready, reusable UI components built with the QuickPrompt Design System. All components follow consistent patterns and use design tokens from `Resources/Styles/`.

---

## ?? Design System Integration

All components use:
- ? **Colors** - `Resources/Styles/Colors.xaml` (50+ tokens)
- ? **Typography** - `Resources/Styles/Typography.xaml` (10+ styles)
- ? **Spacing** - `Resources/Styles/Spacing.xaml` (10+ tokens)
- ? **Shadows** - `Resources/Styles/Shadows.xaml` (3 levels)
- ? **Tokens** - `Resources/Styles/Tokens.xaml` (radius, sizing, animation)

---

## ?? Component Categories

### **Buttons/** - Action Components
- `PrimaryButton.xaml` - Primary actions (save, submit, create)
- `SecondaryButton.xaml` - Secondary actions (cancel, back)
- `DangerButton.xaml` - Destructive actions (delete, remove)
- `GhostButton.xaml` - Subtle actions (tertiary)

### **Inputs/** - Form Controls
- `TextInput.xaml` - Standard text input with label
- `PasswordInput.xaml` - Password input with visibility toggle
- `EmailInput.xaml` - Email input with validation
- `SearchInput.xaml` - Search input with icon

### **Cards/** - Container Components
- `StandardCard.xaml` - Basic card with content
- `ElevatedCard.xaml` - Card with shadow elevation
- `OutlinedCard.xaml` - Card with border outline

### **Badges/** - Status Indicators
- Uses existing `StatusBadge` from Presentation/Controls/
- Already 100% Design System compliant

### **Modals/** - Overlay Components
- `Dialog.xaml` - Standard modal dialog
- `BottomSheet.xaml` - Bottom sheet modal
- `FullscreenModal.xaml` - Fullscreen overlay

### **Feedback/** - User Feedback
- `Toast.xaml` - Temporary notification
- `Alert.xaml` - Dismissible alert banner
- `ProgressIndicator.xaml` - Loading states

---

## ?? Component Standards

### **Required Props**
Every component must support:
- `Style` - Allow custom style override
- `IsEnabled` - Enable/disable state
- `Opacity` - Visual feedback

### **Design Token Usage**
- ? **NO hardcoded colors** - Use `{StaticResource PrimaryBlue}` etc.
- ? **NO inline FontSize** - Use `{StaticResource Heading1TextStyle}` etc.
- ? **NO magic numbers** - Use `{StaticResource SpacingMd}` etc.
- ? **USE Design System tokens** for all styling

### **Accessibility**
- Semantic HTML (AutomationId, SemanticProperties)
- Keyboard navigation support
- Screen reader compatible
- Touch target minimum 44dp

### **Documentation**
Each component folder must include:
- `README.md` - Component documentation
- Props table
- Usage examples
- Variants showcase

---

## ?? Usage Example

```xaml
<!-- ? Good: Using component with Design System -->
<components:PrimaryButton 
    Text="Save Changes"
    Command="{Binding SaveCommand}"
    Style="{StaticResource PrimaryButtonStyle}" />

<!-- ? Bad: Hardcoded styling -->
<Button 
    Text="Save Changes"
    BackgroundColor="#007AFF"
    FontSize="16"
    Padding="20,12" />
```

---

## ?? Getting Started

1. **Import Component Namespace**
   ```xaml
   xmlns:components="clr-namespace:QuickPrompt.Components.Buttons"
   ```

2. **Use Component**
   ```xaml
   <components:PrimaryButton Text="Click Me" />
   ```

3. **Customize with Design System**
   ```xaml
   <components:PrimaryButton 
       Text="Custom"
       BackgroundColor="{StaticResource PrimaryTeal}"
       TextStyle="{StaticResource Heading3TextStyle}" />
   ```

---

## ?? Component Status

| Component | Status | Design System | Documentation |
|-----------|--------|---------------|---------------|
| PrimaryButton | ? Complete | ? 100% | ? Yes |
| SecondaryButton | ? Planned | - | - |
| TextInput | ? Planned | - | - |
| StandardCard | ? Planned | - | - |
| Dialog | ? Planned | - | - |

---

## ?? Development

### Adding New Component
1. Create component folder in appropriate category
2. Create `.xaml` and `.xaml.cs` files
3. Use Design System tokens exclusively
4. Add `README.md` with documentation
5. Update this main README status table

### Testing Checklist
- ? Uses Design System tokens (no hardcoded values)
- ? Supports all standard props (IsEnabled, Opacity, etc.)
- ? Accessible (AutomationId, SemanticProperties)
- ? Responsive (works on all screen sizes)
- ? Documented (README with examples)

---

**Built with ?? using QuickPrompt Design System**
