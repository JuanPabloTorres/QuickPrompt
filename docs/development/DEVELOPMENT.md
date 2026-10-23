# Desarrollo y mantenimiento

## Dependencias y ejecución

[Arranque](../../README.md) y [Core](../../agents/nova-core/README.md). El repositorio no incluye el SDK externo, el entorno Python, modelos, bases privadas ni paquetes instalados. Configurar explícitamente las rutas y el proveedor. No instalar ni descargar para ocultar un fallo de configuración.

## Comprobaciones

Desde la raíz y con las dependencias existentes:

```powershell
node scripts/Check-Documentation.cjs
node --test Tests/tooling/documentation.test.cjs
node scripts/Check-ModuleBoundaries.cjs
npm run test:release --prefix apps/web
npm run build --prefix apps/web
node Tests/nova/prompt-variable-fields.cjs
dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore
```

Ejecutar sólo lo pertinente al cambio y los checks requeridos. Los cambios visuales se prueban en navegador; los de inferencia requieren el proveedor real configurado y resultados independientes. Los arneses de `Tests/nova` tienen perfiles, builds y bibliotecas propios: leer su configuración antes de invocarlos. No reutilizar una base privada para QA.

`Check-Documentation` verifica enlaces a archivos locales de las guías vigentes y las reglas del repositorio. No verifica contenido, anchors internos, URLs remotas ni todos los informes históricos. `Check-ModuleBoundaries` verifica arquitectura estática, no fiabilidad universal de la IA.

## Documentación

Actualizar [el índice](../README.md) y el documento propietario de la capacidad. Evitar repetir comandos, estados y contratos en cada informe. Si una guía queda obsoleta, inspeccionar sus referencias antes de retirarla; mantener un aviso histórico cuando sea evidencia. Los textos retirados del README siguen disponibles en el tag `nova-v1.0.0`.

El número visible pertenece a `apps/web/package.json`; los componentes lo importan. Un informe de mantenimiento no cambia por sí solo la versión, el tag publicado ni la aceptación anterior. Commit/push/publicación requieren una petición del usuario para ese trabajo.

## Skills, agentes y reglas

[AGENTS raíz](../../AGENTS.md) define las fronteras del proyecto y [AGENTS web](../../apps/web/AGENTS.md) las de UI. La [skill local](../../.agents/skills/universal-modular-architecture/SKILL.md) aplica el método de arquitectura y enlaza los propietarios actuales. La skill global se mantiene genérica; las particularidades de QuickPrompt se documentan aquí.

El validador básico de la skill comprueba estructura y recursos locales. El validador oficial depende de PyYAML; si no existe en el entorno, registrar la limitación sin instalar o simular esa dependencia. La validación de estructura no califica el comportamiento de una skill.
