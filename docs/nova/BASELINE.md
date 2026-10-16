# Baseline y decisiones — 2026-10-16

Se aplicaron `architecture-audit`, `ui-ux-pro-max`, `vercel:react-best-practices` y `security-audit`. No se delegó trabajo a otros agentes.

## Estado observado

Rama inicial `feature/provider-bridge-tailwind`. Cambios previos: `.gitignore`, `QuickPrompt.csproj`, `README.md`; numerosos directorios sin seguimiento, incluidos `src`, `docs`, `Tests/e2e`, `extensions` y `automation`. Se creó la rama independiente solicitada sin commits y sin limpiar ni sobrescribir ese trabajo.

La web existente es Blazor InteractiveServer, no React. `Program.cs` registra Razor y SignalR de presentación. Hay un dominio .NET 8 independiente de MAUI y servicios SQLite/Ollama C# reutilizables. La base versionada `QuickPrompt.db3` y la biblioteca normal fuera del repo **no se inspeccionaron ni modificaron**.

| Hallazgo | Evidencia y decisión |
|---|---|
| Acoplamiento de servidor y presentación | `src/QuickPrompt.Web/QuickPrompt.Web.csproj`; crear API y assembly Infrastructure independientes sin referenciar ese proyecto |
| Servicios aprovechables | Parser determinista, revisiones, migración SQLitev1/v2, import/export, grupos y Ollama; baseline actual **150/150** |
| Riesgo de compilación MAUI por globs | Añadir `backend/**` y `apps/**` a las exclusiones existentes; evaluación MSBuild confirma **0** archivos nuevos incluidos |
| Instancia anterior activa | Primer intento normal bloqueó copias de DLL; se recompiló y probó en `.playwright/nova-baseline`, sin detener la instancia ajena |
| Dependencias React ausentes | Solo Tailwind/CLI presentes en el package raíz. Se preparó manifest separado y se solicitó autorización; usuario autorizó los paquetes propuestos |
| Inicialización directa de una DB antigua | La nueva API revisa solo metadatos en modo read-only y rechaza archivos no compatibles antes de inicializar tablas; prueba sintética verifica bytes intactos |

## Inventario de persistencia, obtenido del código

`SchemaVersion`, `Documents(Kind,Id,Payload JSON)`, `PromptGroups` y `GroupMemberships`, con foreign keys y cascadas de membresías. Índices de título, estado, revisiones y relaciones. `Documents` contiene plantillas, versiones, compilados, ejecuciones, propuestas, preferencias, perfiles y combinaciones; no se añadieron tablas paralelas para esas entidades.

La API reutiliza la migración aditiva v1→v2 existente, con backup previo y rechazo de versiones futuras. La extensión NOVA añade únicamente recibos operativos `nova-use` al almacén documental existente; no cambia la versión de esquema. Estos recibos no forman parte de Archive v2; ver límites en migración.

Captura anterior obtenida con una instancia Blazor y SQLite sintéticos propios: [legacy-desktop.png](../evidence/nova/legacy-desktop.png). No es una captura de datos privados. El esquema real de la DB privada sigue `BLOCKED` por falta de autorización, como pide el SOW.

Ollama observó `qwen3:8b`, `qwen2.5:7b-instruct` y un modelo de embeddings ya instalados. Solo se usó Qwen3 para las evaluaciones nuevas; no se descargaron modelos ni se usaron embeddings.

## Incidente de herramienta

Una consulta `stripe directory --help` intentó instalar automáticamente el plugin Directory, fuera del conjunto npm autorizado. Se interrumpió con Ctrl+C al observarlo. No hay evidencia de instalación completada; su estado final no se verificó. No es dependencia del producto ni se utilizó para aprovisionar servicios. Se informó al usuario y se continuó verificando destinos en páginas oficiales. La lección operativa es evitar ese comando que instala automáticamente plugins en tareas sin autorización de instalación.
