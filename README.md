# QuickPrompt

Biblioteca de prompts con variables, colecciones, versiones y resultados. NOVA
ayuda a buscar, redactar y ejecutar instrucciones con revisión y permisos explícitos.
La edición SaaS usa React/TypeScript, .NET 10 y PostgreSQL; MCP delega acceso a una
biblioteca concreta y Companion conecta el modelo local del usuario.

**QuickPrompt 1.0.1** es la versión inicial de la aplicación web y Companion.
La campaña de NOVA con Qwen en Docker Model Runner pasó 19 de 20 casos de
compresión, búsqueda y ejecución. Consulta el [acta de versión](docs/releases/INITIAL_1.0.1.md)
y los [controles pendientes para la operación pública](docs/development/GO_TO_MARKET.md).

## Preparar el entorno

- SDK .NET 10 indicado en global.json, Node 22 y PostgreSQL para las integraciones.
- Docker Model Runner con Qwen3:8b para la inferencia local, configurado de forma
  explícita. El repositorio no incluye modelos, bibliotecas personales o credenciales.
- Instalar dependencias con `npm ci --ignore-scripts` en la raíz y apps/web.
  `dotnet restore backend/QuickPrompt.Cloud --locked-mode` verifica el grafo Cloud.

## Verificar

```powershell
dotnet test backend/QuickPrompt.Tests --configuration Release
npm test --prefix apps/web -- --run
npm run build --prefix apps/web
node scripts/Check-ModuleBoundaries.cjs
node scripts/Check-Documentation.cjs
```

Los tests que requieren hardware, PostgreSQL o servicios reales deben ejecutarse
con configuración explícita y registrar las omisiones. Un build no acredita
inferencia, entrega de correo o uso desde ChatGPT.

## Operar y desarrollar

- [Arquitectura y propietarios](docs/ARCHITECTURE.md)
- [Companion y Docker Model Runner](docs/development/COMPANION.md)
- [Correo de cuentas](docs/development/ACCOUNT_MAIL.md)
- [MCP y autorización](docs/development/MCP.md)
- [Desarrollo y checks](docs/development/DEVELOPMENT.md)
- [Construcción del contenedor](deploy/cloud/README.md)

La configuración privada vive fuera de Git. Las actas históricas de la UI local
conservan su alcance original y no certifican el SaaS actual.

## Entrega y CI/CD

El workflow [QuickPrompt CI and delivery](.github/workflows/nova-web.yml) comprueba
backend, UI, MCP, Companion, seguridad de cuentas/PostgreSQL y restauración. Un
trabajo separado ejecuta el recurso lingüístico nativo fijado; una VM nueva de
Windows instala los prerrequisitos explícitos, exporta dos ZIP reproducibles y
comprueba instalación reubicada, rollback y retirada de payloads. La prueba en
Windows usa datos sintéticos y no afirma instalar Docker Model Runner ni ejecutar
Qwen en el runner. La inferencia local tiene evidencia propia con el proveedor real.

El snapshot mínimo de Adaptive Core está en `packaging/companion/core-sdk`, con
identidad por archivo y avisos originales. No incluye pesos, entornos, configuración
privada o implementaciones de dependencias externas. El exportador lo usa por
defecto y permite seleccionar explícitamente una instalación propia compatible.

Los tags `v1.*` activan la publicación del ZIP con su SHA-256 y de la imagen
versionada en GHCR cuando pasan los controles. La promoción explícita de `main` usa el deploy hook del
servicio Render existente, guardado como secreto de GitHub; fija el SHA comprobado.
El secreto todavía debe configurarse antes de habilitar la promoción. Un commit,
un tag o una ejecución de CI no sustituyen aceptación pública de NOVA/MCP.

[Descargas de la versión 1.0.1](https://github.com/JuanPabloTorres/QuickPrompt/releases/tag/v1.0.1).
