# Button Components

**Category:** Action Components  
**Design System:** 100% Compliant  
**Status:** Production Ready

---

## ?? Components

### 1. **PrimaryButton** ?
Primary call-to-action button for main actions.

**Props:**
| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `Text` | string | "" | Button text label |
| `Command` | ICommand | null | Command to execute on tap |
| `CommandParameter` | object | null | Parameter for command |
| `IsEnabled` | bool | true | Enable/disable button |
| `BackgroundColor` | Color | PrimaryBlue | Button background |
| `TextColor` | Color | White | Text color |
| `CornerRadius` | double | RadiusMd (8) | Border radius |
| `FontSize` | double | From ButtonTextStyle | Text size |
| `Padding` | Thickness | ThicknessMd (16) | Internal padding |
| `HeightRequest` | double | ButtonHeightMedium (44) | Button height |
| `ImageSource` | ImageSource | null | Optional icon |

**Usage:**
```xaml
xmlns:buttons="clr-namespace:QuickPrompt.Components.Buttons"

<!-- Basic -->
<buttons:PrimaryButton Text="Save" Command="{Binding SaveCommand}" />

<!-- With Icon -->
<buttons:PrimaryButton Text="Create" Command="{Binding CreateCommand}">
    <buttons:PrimaryButton.ImageSource>
        <FontImageSource 
            FontFamily="MaterialIconsOutlined-Regular"
            Glyph="&#xe145;"
            Color="White" />
    </buttons:PrimaryButton.ImageSource>
</buttons:PrimaryButton>

<!-- Custom Style -->
<buttons:PrimaryButton 
    Text="Custom Action"
    BackgroundColor="{StaticResource PrimaryTeal}"
    Command="{Binding CustomCommand}" />
```

**Variants:**
- Default: Blue background (`PrimaryBlue`)
- Disabled: Reduced opacity, `StateDisabledBackground`
- Pressed: Slight scale animation

---

### 2. **SecondaryButton** ?
Secondary actions (cancel, back, etc.)

**Status:** Planned  
**Props:** Similar to PrimaryButton with different styling

---

### 3. **DangerButton** ?
Destructive actions (delete, remove)

**Status:** Planned  
**Props:** Similar to PrimaryButton with danger color (`Danger`)

---

### 4. **GhostButton** ?
Subtle tertiary actions

**Status:** Planned  
**Props:** Transparent background, colored text

---

## ?? Design System Tokens Used

**Colors:**
- `PrimaryBlue` - Default background
- `White` - Default text color
- `StateDisabledBackground` - Disabled state
- `StateDisabledText` - Disabled text

**Typography:**
- `ButtonTextStyle` - Text styling

**Spacing:**
- `ThicknessMd` - Button padding (16)

**Sizing:**
- `ButtonHeightMedium` - Button height (44dp)
- `RadiusMd` - Corner radius (8)

**Animation:**
- `AnimationDurationFast` - Press animation (200ms)

---

## ? Accessibility

All buttons include:
- ? **Minimum touch target:** 44dp height
- ? **AutomationId:** For UI testing
- ? **SemanticProperties:** Screen reader support
- ? **Visual feedback:** Opacity change on disable
- ? **Keyboard support:** Command-based navigation

---

## ?? Testing

```csharp
// Unit Test Example
[Test]
public void PrimaryButton_WhenDisabled_HasReducedOpacity()
{
    var button = new PrimaryButton { IsEnabled = false };
    Assert.That(button.Opacity, Is.LessThan(1.0));
}
```

---

## ?? Status

| Component | Implementation | Design System | Documentation | Tests |
|-----------|----------------|---------------|---------------|-------|
| PrimaryButton | ? Complete | ? 100% | ? Yes | ? Planned |
| SecondaryButton | ? Planned | - | - | - |
| DangerButton | ? Planned | - | - | - |
| GhostButton | ? Planned | - | - | - |

---

**Next Steps:**
1. Implement SecondaryButton
2. Implement DangerButton
3. Implement GhostButton
4. Add unit tests
