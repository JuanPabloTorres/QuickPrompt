# ?? SCRIPT DE REESTRUCTURACIÓN AUTOMATIZADA
# QuickPrompt - Folder Restructuring
# Ejecutar desde: C:\Users\juanp\source\repos\QuickPrompt\

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  QuickPrompt - Folder Restructuring   " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar que estamos en el directorio correcto
if (-not (Test-Path "QuickPrompt.csproj")) {
    Write-Host "? ERROR: QuickPrompt.csproj no encontrado" -ForegroundColor Red
    Write-Host "Ejecuta este script desde el directorio raíz del proyecto" -ForegroundColor Yellow
    exit 1
}

Write-Host "? Directorio correcto detectado" -ForegroundColor Green
Write-Host ""

# Función para mover archivos con logging
function Move-WithLog {
    param($Source, $Destination)
    
    if (Test-Path $Source) {
        $destDir = Split-Path -Parent $Destination
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        Move-Item -Path $Source -Destination $Destination -Force
        Write-Host "  ? $Source ? $Destination" -ForegroundColor Gray
        return $true
    }
    else {
        Write-Host "  ? SKIP: $Source (no existe)" -ForegroundColor DarkGray
        return $false
    }
}

# ============================================================================
# FASE 1: CORE
# ============================================================================
Write-Host "?? FASE 1: Reorganizando CORE..." -ForegroundColor Yellow

# Crear estructura Core
$coreDirs = @(
    "Core/Models/Domain",
    "Core/Models/DTOs",
    "Core/Models/Enums",
    "Core/Interfaces",
    "Core/Services/Data",
    "Core/Services/Settings",
    "Core/Services/Cache",
    "Core/Utilities/Validation",
    "Core/Utilities/Text",
    "Core/Utilities/Pagination",
    "Core/Utilities/Helpers"
)

foreach ($dir in $coreDirs) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

# Mover Models ? Core/Models
Move-WithLog "Models/PromptTemplate.cs" "Core/Models/Domain/PromptTemplate.cs"
Move-WithLog "Models/FinalPrompt.cs" "Core/Models/Domain/FinalPrompt.cs"
Move-WithLog "Models/BaseModel.cs" "Core/Models/Domain/BaseModel.cs"
Move-WithLog "Models/DTO/FinalPromptDTO.cs" "Core/Models/DTOs/FinalPromptDTO.cs"
Move-WithLog "Models/ImportablePrompt.cs" "Core/Models/DTOs/ImportablePrompt.cs"
Move-WithLog "Models/Enums/PromptCategory.cs" "Core/Models/Enums/PromptCategory.cs"
Move-WithLog "Models/Enums/StepType.cs" "Core/Models/Enums/StepType.cs"

# Mover Interfaces ? Core/Interfaces
Move-WithLog "Services/ServiceInterfaces/IPromptRepository.cs" "Core/Interfaces/IPromptRepository.cs"
Move-WithLog "Services/ServiceInterfaces/IFinalPromptRepository.cs" "Core/Interfaces/IFinalPromptRepository.cs"
Move-WithLog "Services/ServiceInterfaces/ISessionService.cs" "Core/Interfaces/ISessionService.cs"
Move-WithLog "Settings/ISettingsService.cs" "Core/Interfaces/ISettingsService.cs"

# Mover Services ? Core/Services
Move-WithLog "Services/PromptRepository.cs" "Core/Services/Data/PromptRepository.cs"
Move-WithLog "Services/FinalPromptRepository.cs" "Core/Services/Data/FinalPromptRepository.cs"
Move-WithLog "Services/DatabaseConnectionProvider.cs" "Core/Services/Data/DatabaseConnectionProvider.cs"
Move-WithLog "Services/DatabaseServiceManager.cs" "Core/Services/Data/DatabaseServiceManager.cs"
Move-WithLog "Settings/SettingsService.cs" "Core/Services/Settings/SettingsService.cs"
Move-WithLog "Settings/SettingsModel.cs" "Core/Services/Settings/SettingsModel.cs"
Move-WithLog "Services/SessionService.cs" "Core/Services/Settings/SessionService.cs"
Move-WithLog "Tools/PromptVariableCache.cs" "Core/Services/Cache/PromptVariableCache.cs"
Move-WithLog "Services/PromptCacheCleanupService.cs" "Core/Services/Cache/PromptCacheCleanupService.cs"

# Mover Tools ? Core/Utilities
Move-WithLog "Tools/PromptValidator.cs" "Core/Utilities/Validation/PromptValidator.cs"
Move-WithLog "Tools/AngleBraceTextHandler.cs" "Core/Utilities/Text/AngleBraceTextHandler.cs"
Move-WithLog "Tools/BlockHandler.cs" "Core/Utilities/Pagination/BlockHandler.cs"
Move-WithLog "Tools/AlertService.cs" "Core/Utilities/Helpers/AlertService.cs"
Move-WithLog "Tools/GenericToolBox.cs" "Core/Utilities/Helpers/GenericToolBox.cs"
Move-WithLog "Tools/TabBarHelperTool.cs" "Core/Utilities/Helpers/TabBarHelperTool.cs"

Write-Host "? FASE 1 completada" -ForegroundColor Green
Write-Host ""

# ============================================================================
# FASE 2: FEATURES/PROMPTS
# ============================================================================
Write-Host "?? FASE 2: Reorganizando FEATURES/PROMPTS..." -ForegroundColor Yellow

$featureDirs = @(
    "Features/Prompts/Pages",
    "Features/Prompts/ViewModels",
    "Features/Prompts/Models"
)

foreach ($dir in $featureDirs) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

# Mover Pages
Move-WithLog "Pages/QuickPromptPage.xaml" "Features/Prompts/Pages/QuickPromptPage.xaml"
Move-WithLog "Pages/QuickPromptPage.xaml.cs" "Features/Prompts/Pages/QuickPromptPage.xaml.cs"
Move-WithLog "Pages/PromptDetailsPage.xaml" "Features/Prompts/Pages/PromptDetailsPage.xaml"
Move-WithLog "Pages/PromptDetailsPage.xaml.cs" "Features/Prompts/Pages/PromptDetailsPage.xaml.cs"
Move-WithLog "Pages/EditPromptPage.xaml" "Features/Prompts/Pages/EditPromptPage.xaml"
Move-WithLog "Pages/EditPromptPage.xaml.cs" "Features/Prompts/Pages/EditPromptPage.xaml.cs"

# Mover ViewModels
Move-WithLog "ViewModels/QuickPromptViewModel.cs" "Features/Prompts/ViewModels/QuickPromptViewModel.cs"
Move-WithLog "ViewModels/PromptDetailsPageViewModel.cs" "Features/Prompts/ViewModels/PromptDetailsPageViewModel.cs"
Move-WithLog "ViewModels/EditPromptPageViewModel.cs" "Features/Prompts/ViewModels/EditPromptPageViewModel.cs"
Move-WithLog "ViewModels/Prompts/PromptTemplateViewModel.cs" "Features/Prompts/ViewModels/PromptTemplateViewModel.cs"

# Mover Models
Move-WithLog "Models/VariableInput.cs" "Features/Prompts/Models/VariableInput.cs"
Move-WithLog "Models/PromptPart.cs" "Features/Prompts/Models/PromptPart.cs"
Move-WithLog "Models/VariableSuggestionSelection.cs" "Features/Prompts/Models/VariableSuggestionSelection.cs"
Move-WithLog "Models/NavigationParams.cs" "Features/Prompts/Models/NavigationParams.cs"

Write-Host "? FASE 2 completada" -ForegroundColor Green
Write-Host ""

# ============================================================================
# FASE 3: INFRASTRUCTURE
# ============================================================================
Write-Host "?? FASE 3: Reorganizando INFRASTRUCTURE..." -ForegroundColor Yellow

# Engines ya está bien organizado, solo mover a Infrastructure
if (Test-Path "Engines") {
    Move-WithLog "Engines" "Infrastructure/Engines"
}

# History ya está bien organizado
if (Test-Path "History") {
    Move-WithLog "History" "Infrastructure/History"
}

# Crear Infrastructure/ThirdParty
$thirdPartyDirs = @(
    "Infrastructure/ThirdParty/AdMob/Models",
    "Infrastructure/ThirdParty/AdMob/Views",
    "Infrastructure/ThirdParty/AdMob/ViewModels",
    "Infrastructure/ThirdParty/Share"
)

foreach ($dir in $thirdPartyDirs) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

Move-WithLog "Services/AdmobService.cs" "Infrastructure/ThirdParty/AdMob/AdmobService.cs"
Move-WithLog "Models/AdMobSettings.cs" "Infrastructure/ThirdParty/AdMob/Models/AdMobSettings.cs"
Move-WithLog "Views/AdmobBannerView.xaml" "Infrastructure/ThirdParty/AdMob/Views/AdmobBannerView.xaml"
Move-WithLog "Views/AdmobBannerView.xaml.cs" "Infrastructure/ThirdParty/AdMob/Views/AdmobBannerView.xaml.cs"
Move-WithLog "ViewModels/AdmobBannerViewModel.cs" "Infrastructure/ThirdParty/AdMob/ViewModels/AdmobBannerViewModel.cs"
Move-WithLog "Services/SharePromptService.cs" "Infrastructure/ThirdParty/Share/SharePromptService.cs"

Write-Host "? FASE 3 completada" -ForegroundColor Green
Write-Host ""

# ============================================================================
# FASE 4: PRESENTATION
# ============================================================================
Write-Host "?? FASE 4: Reorganizando PRESENTATION..." -ForegroundColor Yellow

$presentationDirs = @(
    "Presentation/Controls",
    "Presentation/Views",
    "Presentation/Converters",
    "Presentation/ViewModels",
    "Presentation/Popups"
)

foreach ($dir in $presentationDirs) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

# Mover Controls
if (Test-Path "Controls") {
    Get-ChildItem "Controls" | ForEach-Object {
        Move-WithLog $_.FullName "Presentation/Controls/$($_.Name)"
    }
}

# Mover Views compartidas
Move-WithLog "Views/ReusableLoadingOverlay.xaml" "Presentation/Views/ReusableLoadingOverlay.xaml"
Move-WithLog "Views/ReusableLoadingOverlay.xaml.cs" "Presentation/Views/ReusableLoadingOverlay.xaml.cs"
Move-WithLog "Views/PromptFilterBar.xaml" "Presentation/Views/PromptFilterBar.xaml"
Move-WithLog "Views/PromptFilterBar.xaml.cs" "Presentation/Views/PromptFilterBar.xaml.cs"
Move-WithLog "Views/TitleHeader.xaml" "Presentation/Views/TitleHeader.xaml"
Move-WithLog "Views/TitleHeader.xaml.cs" "Presentation/Views/TitleHeader.xaml.cs"

# Mover Converters
if (Test-Path "Converters") {
    Get-ChildItem "Converters" | ForEach-Object {
        Move-WithLog $_.FullName "Presentation/Converters/$($_.Name)"
    }
}

# Mover ViewModels base
Move-WithLog "ViewModels/BaseViewModel.cs" "Presentation/ViewModels/BaseViewModel.cs"
Move-WithLog "ViewModels/RootViewModel.cs" "Presentation/ViewModels/RootViewModel.cs"

# Mover Popups
if (Test-Path "PopUps") {
    Move-WithLog "PopUps" "Presentation/Popups"
}

Write-Host "? FASE 4 completada" -ForegroundColor Green
Write-Host ""

# ============================================================================
# FASE 5: SHARED
# ============================================================================
Write-Host "?? FASE 5: Reorganizando SHARED..." -ForegroundColor Yellow

$sharedDirs = @(
    "Shared/Constants",
    "Shared/Extensions",
    "Shared/Messages",
    "Shared/Configuration"
)

foreach ($dir in $sharedDirs) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

Move-WithLog "Constants/NavigationRoutes.cs" "Shared/Constants/NavigationRoutes.cs"
Move-WithLog "Tools/AppMessagesEng.cs" "Shared/Constants/AppMessagesEng.cs"
Move-WithLog "Tools/PromptDateFilterLabels.cs" "Shared/Constants/PromptDateFilterLabels.cs"
Move-WithLog "Models/PromptDefaults.cs" "Shared/Constants/PromptDefaults.cs"
Move-WithLog "Extensions/CollectionExtensions.cs" "Shared/Extensions/CollectionExtensions.cs"
Move-WithLog "Tools/Messages/UpdatedPromptMessage.cs" "Shared/Messages/UpdatedPromptMessage.cs"
Move-WithLog "Tools/Messages/GuideMessages.cs" "Shared/Messages/GuideMessages.cs"
Move-WithLog "Models/AppSettings.cs" "Shared/Configuration/AppSettings.cs"

Write-Host "? FASE 5 completada" -ForegroundColor Green
Write-Host ""

# ============================================================================
# LIMPIEZA FINAL
# ============================================================================
Write-Host "?? Limpiando carpetas vacías..." -ForegroundColor Yellow

$emptyDirs = @("Models", "Services", "Tools", "ViewModels", "Views", "Controls", "Converters", "Constants", "Extensions", "Settings", "PopUps")

foreach ($dir in $emptyDirs) {
    if ((Test-Path $dir) -and ((Get-ChildItem $dir -Recurse | Measure-Object).Count -eq 0)) {
        Remove-Item $dir -Recurse -Force
        Write-Host "  ? Eliminada carpeta vacía: $dir" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ? REESTRUCTURACIÓN COMPLETADA       " -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "??  PRÓXIMOS PASOS:" -ForegroundColor Yellow
Write-Host "1. Ejecutar: dotnet build" -ForegroundColor White
Write-Host "2. Verificar errores de namespace" -ForegroundColor White
Write-Host "3. Usar Visual Studio para actualizar namespaces automáticamente" -ForegroundColor White
Write-Host "4. Ejecutar: git add -A && git commit -m 'refactor: Restructure folders for better maintainability'" -ForegroundColor White
Write-Host ""
