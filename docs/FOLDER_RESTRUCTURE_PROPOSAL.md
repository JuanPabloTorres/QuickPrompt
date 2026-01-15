# ??? PROPUESTA DE REESTRUCTURACIÓN - QuickPrompt

**Fecha:** Diciembre 2024  
**Estado Actual:** Carpetas desorganizadas, difícil mantenimiento  
**Objetivo:** Estructura clara siguiendo principios de Clean Architecture

---

## ?? PROBLEMAS ACTUALES

### 1. **Mezcla de Concerns**
```
? Services/ contiene lógica mezclada:
   - AdmobService (third-party)
   - PromptRepository (data)
   - SharePromptService (feature)
   - SessionService (authentication)

? Tools/ es un "catch-all" con:
   - AlertService
   - GenericToolBox
   - BlockHandler
   - Messages/
```

### 2. **ViewModels sin Separación**
```
? ViewModels/ plano (todos mezclados):
   - MainPageViewModel
   - SettingsViewModel
   - PromptDetailsPageViewModel
   - Prompts/PromptTemplateViewModel (única carpeta)
```

### 3. **Recursos Desorganizados**
```
? Resources/Styles/ con código C#:
   - AppColors.xaml.cs
   - ButtonStyles.xaml.cs
   - (No deberían tener code-behind)
```

### 4. **Models sin Agrupar**
```
? Models/ plano con:
   - PromptTemplate
   - FinalPrompt
   - NavigationParams
   - VariableInput
   - DTO/FinalPromptDTO (única carpeta)
```

---

## ?? NUEVA ESTRUCTURA PROPUESTA

### **Nivel 1: Feature-Based Architecture**

```
QuickPrompt/
?
??? ?? Core/                          # ? Core business logic
?   ??? Models/
?   ?   ??? Domain/                   # Entities principales
?   ?   ?   ??? PromptTemplate.cs
?   ?   ?   ??? FinalPrompt.cs
?   ?   ?   ??? BaseModel.cs
?   ?   ??? DTOs/                     # Data Transfer Objects
?   ?   ?   ??? FinalPromptDTO.cs
?   ?   ?   ??? ImportablePrompt.cs
?   ?   ??? Enums/
?   ?       ??? PromptCategory.cs
?   ?       ??? StepType.cs
?   ?
?   ??? Interfaces/                   # Contratos principales
?   ?   ??? IPromptRepository.cs
?   ?   ??? IFinalPromptRepository.cs
?   ?   ??? ISessionService.cs
?   ?   ??? ISettingsService.cs
?   ?
?   ??? Services/                     # Business logic services
?   ?   ??? Data/
?   ?   ?   ??? PromptRepository.cs
?   ?   ?   ??? FinalPromptRepository.cs
?   ?   ?   ??? DatabaseConnectionProvider.cs
?   ?   ??? Settings/
?   ?   ?   ??? SettingsService.cs
?   ?   ?   ??? SettingsModel.cs
?   ?   ?   ??? SessionService.cs
?   ?   ??? Cache/
?   ?       ??? PromptVariableCache.cs
?   ?       ??? PromptCacheCleanupService.cs
?   ?
?   ??? Utilities/                    # Herramientas core
?       ??? Validation/
?       ?   ??? PromptValidator.cs
?       ??? Text/
?       ?   ??? AngleBraceTextHandler.cs
?       ??? Pagination/
?       ?   ??? BlockHandler.cs
?       ??? Helpers/
?           ??? AlertService.cs
?           ??? GenericToolBox.cs
?           ??? TabBarHelperTool.cs
?
??? ?? Features/                      # ? Feature modules
?   ??? Prompts/
?   ?   ??? Pages/
?   ?   ?   ??? QuickPromptPage.xaml/.cs
?   ?   ?   ??? PromptDetailsPage.xaml/.cs
?   ?   ?   ??? EditPromptPage.xaml/.cs
?   ?   ??? ViewModels/
?   ?   ?   ??? QuickPromptViewModel.cs
?   ?   ?   ??? PromptDetailsPageViewModel.cs
?   ?   ?   ??? EditPromptPageViewModel.cs
?   ?   ?   ??? PromptTemplateViewModel.cs
?   ?   ??? Models/
?   ?       ??? VariableInput.cs
?   ?       ??? PromptPart.cs
?   ?       ??? VariableSuggestionSelection.cs
?   ?
?   ??? PromptBuilder/
?   ?   ??? Pages/
?   ?   ?   ??? PromptBuilderPage.xaml/.cs
?   ?   ??? ViewModels/
?   ?   ?   ??? PromptBuilderPageViewModel.cs
?   ?   ??? Models/
?   ?       ??? StepModel.cs
?   ?
?   ??? Settings/
?   ?   ??? Pages/
?   ?   ?   ??? SettingPage.xaml/.cs
?   ?   ??? ViewModels/
?   ?       ??? SettingViewModel.cs
?   ?       ??? SettingsViewModel.cs
?   ?
?   ??? Onboarding/
?   ?   ??? Pages/
?   ?   ?   ??? GuidePage.xaml/.cs
?   ?   ??? Models/
?   ?       ??? GuideStep.cs
?   ?
?   ??? Launcher/
?       ??? Pages/
?       ?   ??? MainPage.xaml/.cs
?       ??? ViewModels/
?           ??? MainPageViewModel.cs
?           ??? AiLauncherViewModel.cs
?
??? ?? Infrastructure/                # ? External integrations
?   ??? Engines/                      # AI Engine integration
?   ?   ??? Abstractions/
?   ?   ?   ??? IAiEngine.cs
?   ?   ??? Descriptors/
?   ?   ?   ??? AiEngineDescriptor.cs
?   ?   ??? Execution/
?   ?   ?   ??? EngineExecutionRequest.cs
?   ?   ??? Injection/
?   ?   ?   ??? IWebViewInjectionService.cs
?   ?   ?   ??? WebViewInjectionService.cs
?   ?   ??? Registry/
?   ?   ?   ??? AiEngineRegistry.cs
?   ?   ??? WebView/
?   ?       ??? EngineWebViewPage.xaml/.cs
?   ?       ??? EngineWebViewViewModel.cs
?   ?
?   ??? History/                      # Execution history tracking
?   ?   ??? Models/
?   ?   ?   ??? ExecutionHistoryEntry.cs
?   ?   ??? Repositories/
?   ?   ?   ??? IExecutionHistoryRepository.cs
?   ?   ?   ??? IExecutionHistoryCloudRepository.cs
?   ?   ?   ??? SqliteExecutionHistoryRepository.cs
?   ?   ?   ??? FirestoreExecutionHistoryCloudRepository.cs
?   ?   ?   ??? NullExecutionHistoryCloudRepository.cs
?   ?   ??? Sync/
?   ?   ?   ??? SyncService.cs
?   ?   ??? ExecutionHistoryIntegration.cs
?   ?
?   ??? ThirdParty/                   # External services
?       ??? AdMob/
?       ?   ??? AdmobService.cs
?       ?   ??? Models/
?       ?   ?   ??? AdMobSettings.cs
?       ?   ??? Views/
?       ?   ?   ??? AdmobBannerView.xaml/.cs
?       ?   ??? ViewModels/
?       ?       ??? AdmobBannerViewModel.cs
?       ?
?       ??? Share/
?           ??? SharePromptService.cs
?
??? ?? Presentation/                  # ? UI Components
?   ??? Controls/                     # Reusable UI controls
?   ?   ??? SkeletonView.xaml/.cs
?   ?   ??? EmptyStateView.xaml/.cs
?   ?   ??? ErrorStateView.xaml/.cs
?   ?   ??? PromptCard.xaml/.cs
?   ?   ??? AIProviderButton.xaml/.cs
?   ?   ??? StatusBadge.xaml/.cs
?   ?
?   ??? Views/                        # Shared views
?   ?   ??? ReusableLoadingOverlay.xaml/.cs
?   ?   ??? PromptFilterBar.xaml/.cs
?   ?   ??? TitleHeader.xaml/.cs
?   ?
?   ??? Converters/                   # Value converters
?   ?   ??? BooleanToStarIconConverter.cs
?   ?   ??? CategoryToDisplayNameConverter.cs
?   ?   ??? FinalPromptVisibilityConverter.cs
?   ?   ??? InverseBoolConverter.cs
?   ?   ??? ... (otros converters)
?   ?
?   ??? ViewModels/                   # Base ViewModels
?   ?   ??? BaseViewModel.cs
?   ?   ??? RootViewModel.cs
?   ?
?   ??? Popups/
?   ?   ??? LottieMessagePopup.xaml/.cs
?   ?
?   ??? Resources/                    # Estilos y recursos
?       ??? Styles/
?       ?   ??? AppColors.xaml
?       ?   ??? Colors.xaml
?       ?   ??? ButtonStyles.xaml
?       ?   ??? LabelStyles.xaml
?       ?   ??? EntryEditorStyles.xaml
?       ?   ??? Borders.xaml
?       ?   ??? VisualElementsStyles.xaml
?       ?   ??? Converters.xaml
?       ?   ??? Styles.xaml
?       ??? Fonts/
?       ??? Images/
?       ??? Raw/
?
??? ?? Shared/                        # ? Cross-cutting concerns
?   ??? Constants/
?   ?   ??? NavigationRoutes.cs
?   ?   ??? AppMessagesEng.cs
?   ?   ??? PromptDateFilterLabels.cs
?   ?   ??? PromptDefaults.cs
?   ?
?   ??? Extensions/
?   ?   ??? CollectionExtensions.cs
?   ?
?   ??? Messages/                     # Messaging patterns
?   ?   ??? UpdatedPromptMessage.cs
?   ?   ??? GuideMessages.cs
?   ?
?   ??? Configuration/
?       ??? AppSettings.cs
?
??? ?? Platforms/                     # Platform-specific code
?   ??? Android/
?   ??? iOS/
?   ??? MacCatalyst/
?   ??? Windows/
?
??? App.xaml/.cs                      # App entry point
??? AppShell.xaml/.cs                 # Shell navigation
??? MauiProgram.cs                    # DI registration
??? QuickPrompt.csproj
```

---

## ?? BENEFICIOS DE LA NUEVA ESTRUCTURA

### 1. **Separación Clara de Responsabilidades**
- ? **Core/** = Business logic puro
- ? **Features/** = Módulos de UI por funcionalidad
- ? **Infrastructure/** = Integraciones externas
- ? **Presentation/** = Componentes UI reutilizables
- ? **Shared/** = Utilidades compartidas

### 2. **Escalabilidad**
```
Agregar nueva feature:
Features/
  ??? NuevaFeature/
      ??? Pages/
      ??? ViewModels/
      ??? Models/
```

### 3. **Testabilidad**
```
Core/ ? Fácil de testear (sin dependencias UI)
Features/ ? Test por módulo
Infrastructure/ ? Mock de servicios externos
```

### 4. **Onboarding**
- Nuevo developer encuentra código rápidamente
- Estructura auto-documentada
- Convenciones claras

### 5. **Mantenibilidad**
- Cambios aislados por feature
- Menos merge conflicts
- Código relacionado junto

---

## ?? PLAN DE MIGRACIÓN

### Fase 1: Core (Sin romper nada)
1. Crear carpeta `Core/`
2. Mover `Models/` ? `Core/Models/Domain/`
3. Mover `Services/` ? `Core/Services/`
4. Mover `Tools/` ? `Core/Utilities/`

### Fase 2: Features (Agrupar por módulo)
1. Crear `Features/Prompts/`
2. Mover páginas relacionadas
3. Mover ViewModels relacionados
4. Repetir para otras features

### Fase 3: Infrastructure
1. Crear `Infrastructure/`
2. Mover `Engines/`
3. Mover `History/`
4. Mover servicios third-party

### Fase 4: Presentation
1. Crear `Presentation/`
2. Mover `Controls/`
3. Mover `Views/`
4. Mover `Converters/`
5. Reorganizar `Resources/`

### Fase 5: Shared
1. Crear `Shared/`
2. Mover constantes
3. Mover extensiones
4. Mover mensajería

---

## ? ARCHIVOS A ELIMINAR (Ya identificados)

### Using Statements Innecesarios
```
? Resources/Styles/*.xaml.cs - Code-behind vacíos
```

### Código Legacy
```
? Ya eliminado en commits anteriores
```

---

## ?? CHECKLIST DE IMPLEMENTACIÓN

- [ ] Fase 1: Core (30 min)
- [ ] Fase 2: Features/Prompts (45 min)
- [ ] Fase 3: Infrastructure (30 min)
- [ ] Fase 4: Presentation (30 min)
- [ ] Fase 5: Shared (15 min)
- [ ] Actualizar namespaces (auto con IDE)
- [ ] Actualizar .csproj si necesario
- [ ] Build & Test
- [ ] Commit & Push

**Tiempo total estimado:** 2.5 horas

---

## ?? PRINCIPIOS APLICADOS

- ? **Single Responsibility Principle** (SRP)
- ? **Separation of Concerns** (SoC)
- ? **Feature Folders** (organización por feature)
- ? **Clean Architecture** (layers claras)
- ? **Convention over Configuration** (estructura predecible)
