# QuickPrompt

Biblioteca de prompts con variables, colecciones, versiones y resultados. NOVA
ayuda a buscar, redactar y ejecutar instrucciones con revisión y permisos explícitos.
La edición SaaS usa React/TypeScript, .NET 10 y PostgreSQL; MCP delega acceso a una
biblioteca concreta y Companion conecta el modelo local del usuario.

Candidato **1.0.1-rc.2**, en validación. Estado comercial: **NO-GO**; consultar
[controles y fallos abiertos](docs/development/GO_TO_MARKET.md).

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
