# Propuesta revisable: herramientas para validar MAUI Windows

Estado: FUERA DEL ALCANCE VIGENTE. El usuario eligió «Continuar sólo con la versión
web». La propuesta queda archivada, sin descarga de SDK/workloads ni aprobación
pendiente. [Decisión de alcance](WEB_SCOPE_DECISION_20261020.md). El host MAUI sigue
sin validar; ese estado no impide por sí mismo la aceptación de la versión web.

El host histórico declara .NET 9, pero el equipo sólo tiene SDK 8.0.425 y cero
workloads. La separación de ciclo nativo ya está implementada y documentada en
[NATIVE_LIFECYCLE_PORTS_20261020.md](NATIVE_LIFECYCLE_PORTS_20261020.md).

## Alcance propuesto

- Descargar SDK **9.0.319 win-x64 ZIP** oficial a `.playwright/tools/dotnet9`,
  comprobando SHA512 contra los metadatos oficiales antes de extraer.
- Usar explícitamente ese dotnet.exe y un DOTNET_CLI_HOME de pruebas; conservar
  el PATH y SDK 8 globales. No ejecutar instaladores con privilegios elevados.
- Instalar únicamente el workload **maui-windows** desde los manifiestos
  suministrados por ese SDK (`--skip-manifest-update`). Registrar packs y versiones.
- Restaurar las referencias ya declaradas por el proyecto para el target Windows;
  no añadir paquetes o retargeting al repositorio. Compilar sin publicar ni firmar,
  con salida/intermedios de pruebas separados. No ejecutar tareas Android, iOS,
  Mac, despliegues, launch de aplicaciones o restauraciones de datos privados.
- Si se requieren otros prerrequisitos o un instalador del sistema, detener esa
  instalación y presentar su necesidad antes de continuar. No prometer que este
  SDK basta para resolver fallos históricos del proyecto.

Comando previsto de workload: `dotnet.exe workload install maui-windows
--skip-manifest-update --disable-parallel`. La compilación sólo seleccionará
`net9.0-windows10.0.19041.0` mediante propiedades de línea de comandos; nunca se
imprimirá la configuración privada de firma. Guardar logs revisados sin secretos.
No se conoce todavía el tamaño completo de las descargas de packs y NuGet; se
registrará el consumo real. El ZIP y los packs ocupan disco además de la extracción.

La versión del SDK procede de los
[metadatos oficiales de .NET 9](https://builds.dotnet.microsoft.com/dotnet/release-metadata/9.0/releases.json).
La opción para conservar manifiestos y la instalación de workloads están descritas
en [Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-install).
Este permiso de herramientas no concede autorización para publicar, firmar o
ejecutar cambios sobre bases personales. La validación real de UI/restore requerirá
un entorno sintético y su resultado independiente, después del build.
