# NOVA — propuesta de dependencias

Estado: instalación autorizada explícitamente por el usuario el 2026-10-16 y ejecutada mediante `npm install --ignore-scripts` exclusivamente en `apps/web`. Consulta de metadatos npm realizada antes de solicitar autorización. Lockfile independiente generado; el package/lock raíz se conservaron.

Manifest revisable: `apps/web/package.json`. No se descargaron navegadores, SDK ni modelos mediante esta instalación.

| Paquete | Necesidad | Licencia declarada |
|---|---|---|
| React / React DOM 19.3.1 | Presentación solicitada | MIT |
| Vite 8.3.4 / plugin React 6.1.2 | Build estático y desarrollo | MIT |
| TypeScript 5.9.3 | Modo estricto; versión conservadora frente a un salto mayor | Apache-2.0 |
| Tailwind / plugin Vite 4.3.0 | Plugin oficial, misma versión que herramientas previas | MIT |
| Motion 14.3.0 | Transiciones de estados, reduced motion | MIT |
| Lucide React 1.58.0 | Iconos consistentes | ISC |
| Radix Dialog 1.3.1 | Confirmaciones accesibles y foco | MIT |
| Zod 4.6.7 | Validación de respuestas HTTP | MIT |
| Vitest 5.0.5 / Testing Library 16.3.3 | Comportamiento de UI | MIT |
| jsdom 27.4.0 | DOM de pruebas compatible con Node instalado | MIT |
| Tipos React/DOM/Node; Testing Library DOM | Tipado y peer de pruebas | MIT |

Node instalado: 22.19.0. Vite requiere >=22.12 en la rama 22; Vitest también acepta esa rama. Se evita jsdom 30.1.3 porque exige Node >=22.22.2. Resolución, typecheck, build y tests ejecutados correctamente. `npm audit` reportó cero vulnerabilidades conocidas. El inventario de 150 paquetes instalados y sus licencias declaradas está en [dependencies.json](../evidence/nova/dependencies.json); incluye cuatro dependencias MPL-2.0. Ese inventario no equivale a una auditoría legal de fuentes ni elimina riesgos desconocidos.

No se incluyen TanStack Query, Router ni estado global: el primer flujo tiene un shell y solicitudes simples. Playwright y Edge ya existen en el entorno; se reutilizan sin instalar navegadores.

La API reutiliza paquetes NuGet de SQLite y pruebas ya presentes en caché. `dotnet list QuickPrompt.Nova.sln package --vulnerable --include-transitive --source https://api.nuget.org/v3/index.json --format json` no reportó paquetes vulnerables: [resultado](../evidence/nova/nuget-audit.json). Las declaraciones de licencia Nuspec se inventarían por separado en [nuget-licenses.json](../evidence/nova/nuget-licenses.json). Los análisis de avisos conocidos no sustituyen la revisión del código ni garantizan ausencia de vulnerabilidades desconocidas.

Fuentes: [React](https://react.dev/versions), [Vite](https://vite.dev/guide/), [Tailwind/Vite](https://tailwindcss.com/docs/installation/using-vite), metadatos públicos npm (`npm view ... version license engines peerDependencies`). Context7 no está disponible entre las herramientas de esta sesión.
