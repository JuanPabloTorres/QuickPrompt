# Container Components

**Category:** Layout Components  
**Design System:** 100% Compliant  
**Status:** ? Production Ready - Complete Container System

---

## ?? Components

### 1. **StandardCard** ?
Basic card container with optional border.

**Usage:**
```xaml
<containers:StandardCard>
    <VerticalStackLayout Spacing="8">
        <Label Text="Card Title" Style="{StaticResource Heading2TextStyle}" />
        <Label Text="Card content goes here..." />
    </VerticalStackLayout>
</containers:StandardCard>
```

**Features:**
- Background color (`CardBackground`)
- Border with color and thickness
- Rounded corners (`RadiusMd`)
- Padding (`ThicknessMd`)

---

### 2. **ElevatedCard** ?
Card container with shadow elevation (no border).

**Usage:**
```xaml
<containers:ElevatedCard>
    <Label Text="Elevated card with shadow" />
</containers:ElevatedCard>
```

**Features:**
- Background color (`CardBackground`)
- Shadow elevation (12dp radius, 4dp offset)
- Rounded corners (`RadiusMd`)
- Padding (`ThicknessMd`)
- No border

---

### 3. **OutlinedCard** ?
Card container with prominent border outline (no shadow).

**Usage:**
```xaml
<containers:OutlinedCard>
    <Label Text="Outlined card with border" />
</containers:OutlinedCard>
```

**Features:**
- Background color (`CardBackground`)
- 2px border (`BorderLight`)
- Rounded corners (`RadiusMd`)
- Padding (`ThicknessMd`)
- No shadow

---

## ?? Container Component Status

| Component | Status | Design System | Documentation | Tests |
|-----------|--------|---------------|---------------|-------|
| StandardCard | ? Complete | ? 100% | ? Yes | ? Planned |
| ElevatedCard | ? Complete | ? 100% | ? Yes | ? Planned |
| OutlinedCard | ? Complete | ? 100% | ? Yes | ? Planned |

---

## ?? Design System Tokens Used

All containers use identical token structure:

**Colors:**
- `CardBackground` - Background color (default: white/surface)
- `BorderLight` - Border color

**Spacing:**
- `ThicknessMd` - Padding (16dp)

**Sizing:**
- `RadiusMd` - Corner radius (8dp)

**Shadow:**
- Custom shadow: Radius 12dp, Offset (0,4), Opacity 0.15

---

## ?? Common Props

### **StandardCard Props:**

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `BackgroundColor` | Color | CardBackground | Card background |
| `BorderColor` | Color | BorderLight | Border color |
| `BorderThickness` | double | 1.0 | Border width |
| `Padding` | Thickness | ThicknessMd (16) | Internal padding |
| `CornerRadius` | double | RadiusMd (8) | Corner radius |

### **ElevatedCard Props:**

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `BackgroundColor` | Color | CardBackground | Card background |
| `Padding` | Thickness | ThicknessMd (16) | Internal padding |
| `CornerRadius` | double | RadiusMd (8) | Corner radius |

### **OutlinedCard Props:**

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `BackgroundColor` | Color | CardBackground | Card background |
| `BorderColor` | Color | BorderLight | Border color |
| `BorderThickness` | double | 2.0 | Border width (thicker) |
| `Padding` | Thickness | ThicknessMd (16) | Internal padding |
| `CornerRadius` | double | RadiusMd (8) | Corner radius |

---

## ?? Usage Guide

### **Card Hierarchy:**

1. **ElevatedCard** - Primary content, featured items
2. **StandardCard** - General content, lists
3. **OutlinedCard** - Secondary content, alternatives

### **List Example:**

```xaml
<CollectionView ItemsSource="{Binding Items}">
    <CollectionView.ItemTemplate>
        <DataTemplate>
            <containers:StandardCard>
                <VerticalStackLayout Spacing="8">
                    <Label Text="{Binding Title}" 
                           Style="{StaticResource Heading3TextStyle}" />
                    <Label Text="{Binding Description}" 
                           Style="{StaticResource BodySmallTextStyle}" />
                </VerticalStackLayout>
            </containers:StandardCard>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

### **Featured Content:**

```xaml
<containers:ElevatedCard>
    <VerticalStackLayout Spacing="12">
        <Image Source="{Binding ImageUrl}" 
               HeightRequest="200" 
               Aspect="AspectFill" />
        <Label Text="{Binding Title}" 
               Style="{StaticResource Heading2TextStyle}" />
        <Label Text="{Binding Description}" 
               Style="{StaticResource BodyTextStyle}" />
        <buttons:PrimaryButton Text="Learn More" 
                               Command="{Binding LearnMoreCommand}" />
    </VerticalStackLayout>
</containers:ElevatedCard>
```

### **Form Section:**

```xaml
<containers:OutlinedCard>
    <VerticalStackLayout Spacing="16">
        <Label Text="Personal Information" 
               Style="{StaticResource Heading3TextStyle}" />
        
        <inputs:TextInput Label="Name" 
                          Text="{Binding Name}" />
        <inputs:TextInput Label="Email" 
                          Text="{Binding Email}" 
                          Keyboard="Email" />
        
        <buttons:PrimaryButton Text="Save" 
                               Command="{Binding SaveCommand}" />
    </VerticalStackLayout>
</containers:OutlinedCard>
```

---

## ? Accessibility

All containers include:
- ? **SemanticProperties** for screen readers
- ? **AutomationId** for UI testing
- ? **Content wrapping** for any child controls

---

## ? Phase 3.4 Complete

**Container System:** 3/3 cards implemented  
**Design System Compliance:** 100%  
**Ready for Production:** ? Yes

**Components:**
- ? StandardCard - Basic card with border
- ? ElevatedCard - Card with shadow
- ? OutlinedCard - Card with outline

**Total Components (Phase 3):** 16  
- 4 Buttons (Phase 3.2)
- 3 Inputs (Phase 3.3)
- 3 Containers (Phase 3.4)
- 6 Existing components
