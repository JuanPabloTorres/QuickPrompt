# ?? ANDROID CRASH DEBUG - PLAN SISTEMÁTICO

## SÍNTOMAS DEL CRASH

```
? App inicia (todas las librerías cargan)
? Debugger se conecta
? Thread[2] signal 3
? Stack traces written to tombstoned
? APP CRASHES
```

---

## PASO 1: OBTENER STACK TRACE COMPLETO

### Ejecutar estos comandos para obtener el crash log:

```bash
# Ver tombstones disponibles
adb shell ls -la /data/anr/

# Ver el última anr/crash
adb shell cat /data/anr/traces.txt

# O ver logcat completo filtrado
adb logcat -b all -v threadtime > crash_log.txt

# Buscar EXCEPCIONES
adb logcat | grep -i "exception\|at \|thrown\|fatal"
```

---

## PASO 2: PUNTOS CRÍTICOS DE CRASH (Más probables)

### A) **AppShell Navigation** ??
```csharp
// En App.xaml.cs CreateWindow
var window = new Window { Page = new AppShell() };
// Si AppShell falla aquí, crash inmediato
```

**Solución:**
```csharp
protected override Window CreateWindow(IActivationState? activationState)
{
    try
    {
        System.Diagnostics.Debug.WriteLine("[App] Creating AppShell...");
        var shell = new AppShell();
        System.Diagnostics.Debug.WriteLine("[App] AppShell created successfully");
        
        var window = new Window { Page = shell };
        System.Diagnostics.Debug.WriteLine("[App] Window created successfully");
        return window;
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[App.CreateWindow] FATAL ERROR: {ex}");
        throw;
    }
}
```

### B) **RootViewModel Initialization** ??
```csharp
// En AppShell.xaml.cs
public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        // Si RootViewModel falla, crash
    }
}
```

### C) **Database Initialization** ??
```csharp
// En App.xaml.cs
Task.Run(async () =>
{
    try
    {
        await _databaseServiceManager.InitializeAsync();
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[App] DB Error: {ex}");
        // IMPORTANTE: No throw, dejar que continúe
    }
});
```

### D) **Shell Routes Registration** ??
```csharp
// En MauiProgram.cs ConfigureRouting()
private static void ConfigureRouting()
{
    try
    {
        Routing.RegisterRoute(NavigationRoutes.PromptDetails, typeof(PromptDetailsPage));
        Routing.RegisterRoute(NavigationRoutes.EditPrompt, typeof(EditPromptPage));
        // Si hay rutas mal registradas, crash
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[MauiProgram] Routing error: {ex}");
    }
}
```

---

## PASO 3: AÑADIR LOGGING EXTENSO

Crear archivo `DebugLogger.cs`:

```csharp
using System;
using System.Diagnostics;

namespace QuickPrompt.Tools
{
    public static class DebugLogger
    {
        public static void Log(string source, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string log = $"[{timestamp}] [{source}] {message}";
            Debug.WriteLine(log);
            
#if DEBUG
            // También escribir en archivo si es necesario
            try
            {
                var logsPath = Path.Combine(FileSystem.AppDataDirectory, "debug.log");
                File.AppendAllText(logsPath, log + Environment.NewLine);
            }
            catch { }
#endif
        }

        public static void Error(string source, Exception ex)
        {
            Log(source, $"ERROR: {ex.GetType().Name}: {ex.Message}");
            Log(source, $"StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Error(source, ex.InnerException);
            }
        }
    }
}
```

---

## PASO 4: MODIFICAR PUNTOS CRÍTICOS

### App.xaml.cs - Más logging:
```csharp
public partial class App : Application
{
    DatabaseServiceManager _databaseServiceManager;

    public App(DatabaseServiceManager databaseServiceManager)
    {
        DebugLogger.Log("App", "Constructor starting...");
        
        try
        {
            InitializeComponent();
            DebugLogger.Log("App", "InitializeComponent completed");

            _databaseServiceManager = databaseServiceManager ?? throw new ArgumentNullException(nameof(databaseServiceManager));
            DebugLogger.Log("App", "DatabaseServiceManager assigned");

            Task.Run(async () =>
            {
                try
                {
                    DebugLogger.Log("App", "Database initialization starting...");
                    await _databaseServiceManager.InitializeAsync();
                    DebugLogger.Log("App", "Database initialization completed");
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("App.DatabaseInit", ex);
                }
            });

            DebugLogger.Log("App", "Constructor completed");
        }
        catch (Exception ex)
        {
            DebugLogger.Error("App.Constructor", ex);
            throw;
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        DebugLogger.Log("App", "CreateWindow starting...");
        
        try
        {
            DebugLogger.Log("App", "Creating AppShell...");
            var shell = new AppShell();
            DebugLogger.Log("App", "AppShell created");

            var window = new Window { Page = shell };
            DebugLogger.Log("App", "Window created");
            
            return window;
        }
        catch (Exception ex)
        {
            DebugLogger.Error("App.CreateWindow", ex);
            throw;
        }
    }

    protected override void OnStart()
    {
        DebugLogger.Log("App", "OnStart");
        try
        {
            PromptCacheCleanupService.RunCleanupIfDue();
        }
        catch (Exception ex)
        {
            DebugLogger.Error("App.OnStart", ex);
        }
    }
}
```

### AppShell.xaml.cs - Logging:
```csharp
public partial class AppShell : Shell
{
    public AppShell()
    {
        DebugLogger.Log("AppShell", "Constructor starting...");
        
        try
        {
            InitializeComponent();
            DebugLogger.Log("AppShell", "InitializeComponent completed");
        }
        catch (Exception ex)
        {
            DebugLogger.Error("AppShell.Constructor", ex);
            throw;
        }
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        DebugLogger.Log("AppShell", $"Navigating to: {args.Target.Location}");
        base.OnNavigating(args);
    }
}
```

---

## PASO 5: EJECUTAR Y CAPTURAR CRASH

```bash
# 1. Limpieza previa
adb uninstall com.companyname.quickprompt
adb shell rm -rf /data/user/0/com.companyname.quickprompt

# 2. Deploy e instalar
dotnet build -f net9.0-android -c Release

# 3. Ejecutar app
adb shell am start -a "android.intent.action.MAIN" \
  -c "android.intent.category.LAUNCHER" \
  -n "com.companyname.quickprompt/crc64afde101c72d7e268.MainActivity"

# 4. Capturar logs en tiempo real
adb logcat -C -v threadtime *:* > logcat_full.txt

# 5. Cuando crash, presionar Ctrl+C y revisar logcat_full.txt

# 6. Buscar excepciones:
grep -i "exception\|error\|fatal\|crash" logcat_full.txt
```

---

## PASO 6: POTENCIALES PROBLEMAS ESPECÍFICOS

### ?? Si ves "InvalidOperationException in Shell routing":
- **Causa:** Ruta registrada incorrectamente
- **Fix:** Ver MauiProgram ConfigureRouting()

### ?? Si ves "NullReferenceException in DatabaseServiceManager":
- **Causa:** Database no inicializa
- **Fix:** Agregar try-catch en database init

### ?? Si ves "TypeInitializationException":
- **Causa:** Static field initialization fails
- **Fix:** Buscar static initializers en modelos/servicios

### ?? Si ves "OperationCanceledException":
- **Causa:** Task timeout en background
- **Fix:** Aumentar timeout o remover operación de background

### ?? Si ves "Permission denied":
- **Causa:** Falta de permisos Android
- **Fix:** Revisar AndroidManifest.xml

---

## ?? CHECKLIST PARA DEBUGGEAR

- [ ] Obtener stack trace completo de crash
- [ ] Identificar línea exacta del error
- [ ] Revisar excepción y mensaje
- [ ] Aplicar try-catch en ese punto
- [ ] Agregar logging exhaustivo
- [ ] Rebuild y test

---

## ?? COMANDOS ÚTILES RÁPIDOS

```bash
# Ver crash report
adb bugreport > bugreport.zip

# Ver stack traces específicos
adb shell dumpsys meminfo com.companyname.quickprompt

# Ver logcat durante crash
adb logcat -s "DOTNET:*" "monodroid:*"

# Reinstalar limpio
adb shell pm uninstall com.companyname.quickprompt && \
dotnet build -f net9.0-android -c Release && \
adb install -r bin/Release/net9.0-android/com.companyname.quickprompt-Signed.apk
```

---

## ?? SIGUIENTE PASO

1. **Ejecuta el app con logging habilitado**
2. **Captura el LogCat completo cuando crash**
3. **Busca "Exception" o "Error" en los logs**
4. **Comparte aquí la excepción exacta**

Con la excepción exacta podré ayudarte a fixear el problema específico.
