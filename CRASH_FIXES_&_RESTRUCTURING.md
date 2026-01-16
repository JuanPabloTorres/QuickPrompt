# ?? CRASH FIXES & RESTRUCTURING GUIDE

## PROBLEMAS IDENTIFICADOS

### 1. **PromptBuilderPageViewModel - UpdateNavigationState() IndexOutOfRangeException**
```csharp
// ? RIESGO: Si Steps está vacío o CurrentStep >= Steps.Count
var step = Steps[CurrentStep];  // CRASH aquí

// ? FIX: Validar primero
if (Steps == null || Steps.Count == 0 || CurrentStep >= Steps.Count)
    return;
var step = Steps[CurrentStep];
```

### 2. **AiLauncherViewModel - FinalPrompts null reference**
```csharp
// ? FinalPrompts es ObservableCollection pero puede no estar inicializado
public ObservableCollection<string> FinalPrompts { get; } // ¿Inicializado en BaseViewModel?

// ? FIX: Verificar que FinalPrompts esté inicializado en el constructor
```

### 3. **GuidePage - GuideCarousel puede ser null**
```csharp
// ? RIESGO en UpdateButtonStates()
bool isFinal = GuideSteps[GuideCarousel.Position].IsFinalStep;
// Si GuideCarousel no está inicializado o GuideSpaghetti está vacío

// ? FIX: Validar antes
if (GuideCarousel == null || GuideSteps.Count == 0 || GuideCarousel.Position >= GuideSteps.Count)
    return;
```

### 4. **MainPage - RenderPromptAsChips con null PromptText**
```csharp
// ? RenderPromptAsChips puede recibir null en OnAppearing
RenderPromptAsChips(_viewModel.PromptText);  // PromptText puede ser null aquí

// ? FIX: Validar primero
if (!string.IsNullOrEmpty(_viewModel.PromptText))
    RenderPromptAsChips(_viewModel.PromptText);
```

### 5. **Async/Await Issues - FireAndForget sin error handling**
```csharp
// ? FireAndForget ignora excepciones
_.FireAndForget()  // Las excepciones se pierden silenciosamente

// ? FIX: Agregar logging
public static void FireAndForget(this Task task)
{
    _ = task.ContinueWith(t => 
    {
        if (t.IsFaulted)
        {
            System.Diagnostics.Debug.WriteLine($"[FireAndForget] Error: {t.Exception?.Message}");
        }
    }, TaskScheduler.FromCurrentSynchronizationContext());
}
```

---

## REESTRUCTURACIÓN PENDIENTE

Las siguientes carpetas aún tienen archivos en rutas antiguas:
- Models/ ? Core/Models/ o Features/*/Models/
- Pages/ ? Features/*/Pages/
- ViewModels/ ? Features/*/ViewModels/

### Mapeo de archivos:

**Models/**
- GuideStep.cs ? Features/Onboarding/Models/
- StepModel.cs ? Features/PromptBuilder/Models/

**Pages/**
- GuidePage.xaml/.cs ? Features/Onboarding/Pages/
- MainPage.xaml/.cs ? Features/Launcher/Pages/
- PromptBuilderPage.xaml/.cs ? Features/PromptBuilder/Pages/
- SettingPage.xaml/.cs ? Features/Settings/Pages/

**ViewModels/**
- AiLauncherViewModel.cs ? Features/Launcher/ViewModels/
- MainPageViewModel.cs ? Features/Launcher/ViewModels/
- PromptBuilderPageViewModel.cs ? Features/PromptBuilder/ViewModels/
- SettingsViewModel.cs ? Features/Settings/ViewModels/
- SettingViewModel.cs ? Features/Settings/ViewModels/

---

## PLAN DE ACCIÓN

### Fase 1: Crash Fixes (HIGH PRIORITY)
1. Fix PromptBuilderPageViewModel.UpdateNavigationState()
2. Fix PromptBuilderPageViewModel.UpdatePreviewStep()
3. Fix GuidePage.UpdateButtonStates()
4. Fix MainPage.RenderPromptAsChips()
5. Improve FireAndForget error handling

### Fase 2: Restructuring (MEDIUM PRIORITY)
1. Create Features/Onboarding/
2. Create Features/Launcher/
3. Create Features/PromptBuilder/
4. Create Features/Settings/
5. Move files accordingly

### Fase 3: Testing
1. Run full build
2. Test on Android/iOS
3. Verify no crashes

---

## FILES TO FIX (In priority order)

1. ViewModels/PromptBuilderPageViewModel.cs
2. Pages/GuidePage.xaml.cs
3. Pages/MainPage.xaml.cs
4. ViewModels/AiLauncherViewModel.cs
5. Tools/BlockHandler.cs (optional - improve)
