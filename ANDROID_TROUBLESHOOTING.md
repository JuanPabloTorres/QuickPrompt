# ?? ANDROID TROUBLESHOOTING GUIDE

## SÍNTOMAS OBSERVADOS EN LOGS

```
? App inicia
? Librerías nativas cargan correctamente
? Debugger se conecta
?? Warnings menores (no críticos)
```

---

## ?? POSIBLES CAUSAS DE CRASH

### 1. **En la Inicialización de Shell/Navigation**
**Archivo:** `AppShell.xaml.cs` o `MauiProgram.cs`

**Síntoma:** App inicia pero se queda en blanco

**Verificar:**
```csharp
// En MauiProgram.cs
.ConfigureLifecycles((lifecycleBuilder) =>
{
    lifecycleBuilder
        .AddAppLifecycle()
        .AddLogging(); // ¿Configurado correctamente?
})
```

---

### 2. **En Database Initialization**
**Archivo:** `DatabaseConnectionProvider.cs` o `PromptRepository.cs`

**Síntoma:** App inicia pero se queda en blanco o crash después

**Síntomas típicos:**
- SQLite error
- Permission error
- File system error

**Verificar:**
```csharp
// En PromptRepository constructor
public PromptRepository(DatabaseConnectionProvider provider)
{
    _dbProvider = provider;
    _database = _dbProvider.GetConnection(); // ¿Puede ser null?
}
```

---

### 3. **En AdMob Initialization**
**Archivo:** `MainActivity.cs` o `AdmobService.cs`

**Síntoma:** Crash en onCreate

**Código actual:**
```csharp
CrossMauiMTAdmob.Current.Init(this, "ca-app-pub-...");
```

**Riesgo:** CrossMauiMTAdmob.Current puede ser null

---

### 4. **En Main Page Load**
**Archivo:** `MainPage.xaml.cs`

**Síntoma:** Crash cuando intenta renderizar

**Riesgo en OnAppearing:**
```csharp
protected override void OnAppearing()
{
    _viewModel.Initialize(); // ¿Puede fallar?
    RenderPromptAsChips(_viewModel.PromptText); // ¿Puede ser null?
}
```

---

### 5. **En Navigation Registration**
**Archivo:** `AppShell.xaml` o `MauiProgram.cs`

**Síntoma:** Navigation error

**Posible error:**
- Rutas registradas incorrectamente
- Conflicto entre rutas antiguas y nuevas

---

## ??? SOLUCIONES INMEDIATAS

### A) Agregar Try-Catch Global en MainActivity

```csharp
protected override void OnCreate(Bundle? savedInstanceState)
{
    try
    {
        base.OnCreate(savedInstanceState);
        CrossMauiMTAdmob.Current.Init(this, "ca-app-pub-...");
    }
    catch (Exception ex)
    {
        Android.Util.Log.Error("MainActivity", $"Error: {ex.Message}\n{ex.StackTrace}");
    }
}
```

### B) Agregar Logging en MauiProgram

```csharp
var app = builder
    .ConfigureLogging(logging =>
    {
        logging.AddDebug();
        logging.SetMinimumLevel(LogLevel.Debug);
    })
    .Build();

return app;
```

### C) Verificar Database en Android

```csharp
// En PromptRepository
public PromptRepository(DatabaseConnectionProvider provider)
{
    try
    {
        _dbProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        _database = _dbProvider.GetConnection();
        
        if (_database == null)
            throw new Exception("Database connection is null");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[PromptRepository] Error: {ex.Message}");
        throw;
    }
}
```

---

## ?? PASOS PARA DEBUGGEAR

### 1. **Ejecutar con LogCat completo**
```bash
adb logcat -s "quickprompt:*"
```

### 2. **Buscar EXCEPCIONES específicas**
```bash
adb logcat *:E | grep -i "exception\|crash\|error"
```

### 3. **Ver logs de MAUI**
```bash
adb logcat -s "MAUI"
```

### 4. **Ver logs de .NET**
```bash
adb logcat -s "DOTNET"
```

### 5. **Ver logs de monodroid**
```bash
adb logcat -s "monodroid"
```

---

## ?? RECOMENDACIONES

### Opción 1: Agregar logging extenso
Modificar `MauiProgram.cs` para loguear cada paso de inicialización

### Opción 2: Simplificar Startup
Comentar AdMob temporalmente para descartar esa causa:
```csharp
// CrossMauiMTAdmob.Current.Init(this, "ca-app-pub-...");
```

### Opción 3: Verificar permisos
Revisar `AndroidManifest.xml` - ¿Todos los permisos requeridos?

### Opción 4: Verificar Target SDK
¿Está bien configurado `targetSdkVersion` en proyecto Android?

---

## ?? INFORMACIÓN NECESARIA

Para ayudarte más efectivamente, necesito:

1. **¿Dónde exactamente se queda la app?**
   - En blanco?
   - En splash screen?
   - En MainPage?

2. **¿Hay LogCat error antes del crash?**
   - Paste el último LogCat

3. **¿Qué versión Android estás usando?**
   - API 35 (según los logs)

4. **¿Funciona en Debug o Release?**
   - O ambas fallan?

---

## ?? ARCHIVOS A REVISAR (Por prioridad)

1. **Platforms/Android/MainActivity.cs** - AdMob init
2. **MauiProgram.cs** - Registración de servicios
3. **AppShell.xaml.cs** - Navigation setup
4. **Services/DatabaseConnectionProvider.cs** - DB connection
5. **App.xaml.cs** - App startup
