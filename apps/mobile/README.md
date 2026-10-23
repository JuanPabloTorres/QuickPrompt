# QuickPrompt móvil

La aplicación MAUI original vive aquí. Se conservaron namespaces, recursos,
identificadores y contratos de datos. La reorganización no migra bases privadas.

- Entrada: `QuickPrompt.csproj`, `MauiProgram.cs`, `App.xaml`.
- Capas: `ApplicationLayer`, `Domain`, `Infrastructure`, `Presentation`.
- UI y recursos: `Components`, `Pages`, `Features`, `Resources`, `Platforms`.
- Pruebas históricas separadas: `tests/windows` y `tests/portable`.
- Solución desde la raíz: `QuickPrompt.sln`.

Requiere el SDK .NET 9 y los workloads MAUI de la plataforma que se compila.
No se instalan automáticamente. La máquina de esta reorganización solo tiene
SDK 8.0.425: la compilación móvil devuelve `NETSDK1045` y queda sin verificar.

```powershell
dotnet build apps/mobile/QuickPrompt.csproj -f net9.0-windows10.0.19041.0
```

El dominio web sigue compilando los archivos compatibles de `Domain/Entities`
y `Domain/Enums` mediante enlaces explícitos; no se duplicaron sus fuentes.
Las reglas del servidor y la integración de IA están fuera de esta app.
