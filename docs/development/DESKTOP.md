# Acceso de escritorio de QuickPrompt NOVA

El acceso abre una entrega ya compilada con la biblioteca y el entorno Core
seleccionados. Conserva el logo oficial; el ICO incluye siete tamaños entre
16 y 256 píxeles. No instala dependencias, modelos ni inicio automático.

## Instalar

Desde la raíz, después de compilar y arrancar la entrega elegida, indicar sus
rutas reales. No usar una base privada para pruebas:

```powershell
./scripts/Install-DesktopShortcut.ps1 `
  -Port 5190 `
  -DataPath C:/ruta/biblioteca.sqlite `
  -CorePython C:/ruta/Adaptive-AI-Core/.venv/Scripts/python.exe `
  -CoreSdkRoot C:/ruta/Adaptive-AI-Core `
  -ApiDllPath ./backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll `
  -WebRootPath ./apps/web/dist `
  -CoreWorkerPath ./agents/nova-core/nova_worker.py
```

El instalador crea `QuickPrompt NOVA.lnk` en el escritorio real del usuario,
incluido un escritorio redirigido a OneDrive. El perfil y el icono quedan en
`%LOCALAPPDATA%/QuickPromptNova/`. Si ya existen, se respaldan antes de actualizar.
El servicio de esta entrega se inicia en Production, sin funciones QA del navegador.

## Qué ocurre al abrirlo

`Open-NovaDesktop.ps1` valida el perfil y delega en `Start-Nova.ps1`, propietario
del arranque. `-ReuseExisting` compara el proceso que escucha, DLL, puerto,
web root y biblioteca antes de consultar su salud y abrirlo. Si coincide,
reutiliza el proceso. Un conflicto produce un error visible sin detenerlo.
Si el puerto está libre, inicia esa misma entrega y espera su propia señal de salud.

Para comprobar el perfil sin abrir ventanas:

```powershell
./scripts/Open-NovaDesktop.ps1 -NoBrowser
```

Los errores quedan en `%LOCALAPPDATA%/QuickPromptNova/desktop-errors.log`. Los logs
del servidor permanecen en `.nova-build/logs/`. El perfil no se sube a Git.
No elimina ni migra bibliotecas. Para retirar el acceso basta quitar el `.lnk`;
la aplicación, las bases y la configuración permanecen disponibles.

## Compatibilidad y mantenimiento

El arranque normal del README conserva sus opciones y valores por defecto.
Las rutas `-ApiDllPath`, `-WebRootPath` y `-CoreWorkerPath` permiten reutilizar
una entrega congelada y exigen `-NoBuild`. Al trasladar una entrega, reinstalar
el acceso con las rutas nuevas; no apunta a una base descubierta automáticamente.
