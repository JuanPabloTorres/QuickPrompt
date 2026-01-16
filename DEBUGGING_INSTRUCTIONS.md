# ?? CRASH DEBUGGING - INSTRUCCIONES PARA TI

**Commit:** `cf72dc6`

---

## ?? QUÉ HACER AHORA

### PASO 1: Rebuild con logging habilitado

```bash
# Limpiar build anterior
dotnet clean

# Rebuild en Debug (tiene más logging)
dotnet build -f net9.0-android -c Debug
```

### PASO 2: Instalar en Android

```bash
# Uninstall version anterior
adb uninstall com.companyname.quickprompt

# Instalar nueva versión
dotnet build -f net9.0-android -c Debug -p:AndroidPackageFormat=apk

# O manualmente:
adb install -r bin/Debug/net9.0-android/com.companyname.quickprompt-Signed.apk
```

### PASO 3: Ejecutar app y capturar crash

```bash
# Abrir terminal 1 - Capturar LogCat
adb logcat -C -v threadtime > crash_log.txt

# Abrir terminal 2 - Lanzar app
adb shell am start -a "android.intent.action.MAIN" \
  -c "android.intent.category.LAUNCHER" \
  -n "com.companyname.quickprompt/crc64afde101c72d7e268.MainActivity"

# Esperar hasta que crash
# Presionar Ctrl+C en Terminal 1
```

### PASO 4: Analizar logs

```bash
# Buscar nuestro logging
cat crash_log.txt | grep "\[App"

# Buscar excepciones
cat crash_log.txt | grep -i "exception\|error\|fatal"

# Ver archivo de debugging (si se creó)
adb shell cat /data/data/com.companyname.quickprompt/files/debug.log
```

---

## ?? QUÉ BUSCAR EN LOS LOGS

### Esperaré ver esto:
```
[09:15:23.456] [App.Constructor] Starting...
[09:15:23.457] [App.Constructor] InitializeComponent completed
[09:15:23.458] [App.Constructor] DatabaseServiceManager injected
[09:15:23.459] [App] Database initialization starting...
[09:15:23.460] [App.Constructor] Constructor completed successfully
[09:15:23.461] [App.CreateWindow] Starting...
[09:15:23.462] [App.CreateWindow] Creating AppShell...
[09:15:23.463] [App.CreateWindow] AppShell created successfully
[09:15:23.464] [App.CreateWindow] Creating Window...
[09:15:23.465] [App.CreateWindow] Window created successfully
```

### Si ves una excepción:
```
[09:15:23.456] [App.CreateWindow] FATAL ERROR: NullReferenceException: Object reference not set to an instance of an object
[09:15:23.457] [App.CreateWindow] StackTrace: at QuickPrompt.AppShell..ctor()
```

---

## ?? CASOS COMUNES Y SOLUCIONES

### ? Si se queda en blanco sin logs:
- **Causa:** Crash antes de App.xaml.cs
- **Solución:** Revisar MauiProgram.cs CreateMauiApp()

### ? Si ves "NullReferenceException in AppShell":
- **Causa:** AppShell.xaml tiene un binding null
- **Solución:** Revisar AppShell.xaml.cs y binding en XAML

### ? Si ves "ArgumentNullException in MauiProgram":
- **Causa:** Inyección de dependencias falla
- **Solución:** Revisar MauiProgram RegisterServices()

### ? Si ves "SQLite exception":
- **Causa:** Base de datos no se inicializa
- **Solución:** Revisar DatabaseConnectionProvider

### ? Si ves "AdMob error":
- **Causa:** AdMob key inválido o no hay internet
- **Solución:** Ya está manejado, debería continuar

---

## ?? DESPUÉS DE OBTENER EL ERROR

1. **Comparte conmigo:**
   - La excepción completa
   - El StackTrace
   - El nombre de la clase/método donde ocurre

2. **Ejemplo de lo que necesito:**
   ```
   Exception: NullReferenceException
   Message: Object reference not set to an instance of an object
   StackTrace:
     at QuickPrompt.Pages.AppShell..ctor()
     at QuickPrompt.App.CreateWindow(IActivationState activationState)
     at Microsoft.Maui.Controls.Application.CreateWindow(IActivationState activationState)
   ```

3. **Con eso podré:**
   - Identificar el problema exacto
   - Crear fix específico
   - Testear la solución

---

## ? BUILD & RUN COMMANDS (Copia y pega)

```bash
# Limpiar todo
dotnet clean

# Build Debug con logging
dotnet build -f net9.0-android -c Debug

# Ver si compila
echo Build completed

# Uninstall anterior
adb uninstall com.companyname.quickprompt

# Install nueva versión
adb install -r bin/Debug/net9.0-android/com.companyname.quickprompt-Signed.apk

# Ver logs en tiempo real
adb logcat -C -v threadtime | grep -E "\[App\]|\[Error\]|\[Exception\]|Thrown"
```

---

## ?? TIPS

- **Usar FilteredLogCat:** Abre Android Studio > Logcat y filtra por "com.companyname.quickprompt"
- **Usar Visual Studio:** Debug > Android Device Monitor también muestra LogCat
- **File logging:** El archivo `/files/debug.log` se crea automáticamente si el app tiene permisos

---

## ?? TIMELINE

1. **Ahora:** Tú ejecutas app y capturas logs
2. **Cuando crash:** Compartir excepción exacta conmigo
3. **Yo:** Creo fix específico
4. **Tú:** Tests fix nuevo
5. **Repeat** hasta que funcione

---

## ?? ESTOY LISTO

Cuando tengas el crash log con la excepción exacta, comparte aquí y crearé el fix.

Los archivos que agregué hoy son:
- ? `DebugLogger.cs` - Sistema de logging
- ? `ANDROID_CRASH_DEBUG_GUIDE.md` - Guía completa
- ? `App.xaml.cs mejorado` - Con logging en puntos críticos

Esto debería darte suficiente información para identificar dónde exactamente se queda la app.
