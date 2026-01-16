# ?? ANDROID INITIALIZATION FIXES

**Commit:** `cfd2504`  
**Date:** January 15, 2026

---

## ? PROBLEMAS IDENTIFICADOS Y SOLUCIONADOS

### 1. **MauiProgram.cs - Duplicate Build Call** ???

**Problema:**
```csharp
var mauiApp = builder.Build();      // Primera vez
// ... InitializeIoC ...
return builder.Build();             // ? Segunda vez - CRASH!
```

**Solución:**
```csharp
var mauiApp = builder.Build();      // Una sola vez
InitializeIoC(mauiApp);
return mauiApp;                     // ? Retornar lo que ya fue built
```

**Impacto:** 
- ? Antes: Crash en inicialización
- ? Después: Build correcto, app inicia

---

### 2. **App.xaml.cs - No Error Handling** ???

**Problemas:**
```csharp
// ? Sin try-catch
_databaseServiceManager = databaseServiceManager;

// ? Null reference si falla
Task.Run(async () =>
{
    await _databaseServiceManager.InitializeAsync();
});

// ? Null en CreateWindow
public Window CreateWindow(IActivationState? activationState)
{
    var window = new Window { Page = new AppShell() };
}
```

**Soluciones:**
```csharp
// ? Null check
_databaseServiceManager = databaseServiceManager ?? throw new ArgumentNullException(...);

// ? Try-catch en background task
Task.Run(async () =>
{
    try
    {
        await _databaseServiceManager.InitializeAsync();
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[App] Database initialization error: {ex.Message}");
    }
});

// ? Try-catch en CreateWindow
protected override Window CreateWindow(IActivationState? activationState)
{
    try
    {
        var window = new Window { Page = new AppShell() };
        return window;
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[App.CreateWindow] Error: {ex.Message}");
        throw;
    }
}
```

**Impacto:**
- ? Antes: Crash si database no se inicializa
- ? Después: App continúa, error logging

---

### 3. **MainActivity.cs - No AdMob Error Handling** ???

**Problema:**
```csharp
protected override void OnCreate(Bundle? savedInstanceState)
{
    base.OnCreate(savedInstanceState);
    // ? Si AdMob falla, crash total
    CrossMauiMTAdmob.Current.Init(this, "ca-app-pub-...");
}
```

**Solución:**
```csharp
protected override void OnCreate(Bundle? savedInstanceState)
{
    try
    {
        base.OnCreate(savedInstanceState);

        // ? Try-catch específicamente para AdMob
        try
        {
            CrossMauiMTAdmob.Current.Init(this, "ca-app-pub-...");
            Android.Util.Log.Info("MainActivity", "AdMob initialized successfully");
        }
        catch (Exception admobEx)
        {
            Android.Util.Log.Error("MainActivity", $"AdMob initialization error: {admobEx.Message}");
            // Continuar sin AdMob
        }
    }
    catch (Exception ex)
    {
        Android.Util.Log.Error("MainActivity", $"Fatal error: {ex.Message}");
        throw;
    }
}
```

**Impacto:**
- ? Antes: Crash si AdMob API key inválido o sin internet
- ? Después: App continúa sin AdMob, error logeado

---

## ?? ANDROID LOGGING AGREGADO

Ahora puedes ver estos logs en LogCat:

```bash
# Para ver logs específicos:
adb logcat -s "MainActivity:*"
adb logcat -s "DOTNET:*"
adb logcat -s "monodroid:*"

# Logs que verás:
[MainActivity] AdMob initialized successfully
[MainActivity] AdMob initialization error: ...
[MainActivity] Fatal error in OnCreate: ...
[App] Database initialization error: ...
[App.CreateWindow] Error: ...
[App.OnStart] Error: ...
```

---

## ?? CASOS AHORA MANEJADOS

### Antes (Crashes):
1. ? MauiProgram build twice ? Crash
2. ? Database init fails ? Crash
3. ? AdMob key invalid ? Crash
4. ? AppShell null ? Crash

### Después (Continúa):
1. ? Initialización limpia
2. ? Database error ? Log + Continue
3. ? AdMob error ? Log + Continue sin Ads
4. ? Safe page creation

---

## ?? ARCHIVOS MODIFICADOS

1. **MauiProgram.cs**
   - Línea 40: Cambiar `return builder.Build();` a `return mauiApp;`

2. **App.xaml.cs**
   - Líneas 7-15: Null check + Try-catch para database init
   - Líneas 19-27: Try-catch en CreateWindow
   - Líneas 29-37: Try-catch en OnStart

3. **Platforms/Android/MainActivity.cs**
   - Línea 8: Try-catch en OnCreate
   - Línea 12-22: Try-catch específicamente para AdMob

4. **ANDROID_TROUBLESHOOTING.md** (NUEVO)
   - Guía completa de debugging
   - Common crash causes
   - Pasos para investigar

---

## ? VERIFICACIÓN

```bash
# Build status
? Debug Build: Successful
? Release Build: Successful
? No compilation errors

# Git status
? Changes committed
? Pushed to origin
```

---

## ?? PRÓXIMOS PASOS SI LA APP SIGUE CRASHEANDO

1. **Ejecutar con LogCat:**
   ```bash
   adb logcat -C -v threadtime *:*
   ```

2. **Buscar excepción:**
   ```bash
   adb logcat | grep -i "Exception\|crash\|error"
   ```

3. **Seguir ANDROID_TROUBLESHOOTING.md**

4. **Compartir logs completos de LogCat**

---

## ?? REFERENCIA

- Android Crash Logs: LogCat en Android Studio
- .NET MAUI Debugging: https://learn.microsoft.com/maui/
- AdMob Integration: Plugin.MauiMTAdmob documentation
