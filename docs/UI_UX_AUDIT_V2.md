# Auditoría UX V2 — Fase 03

Base observada: Blazor Home, app.css y scripts reales, no una reconstrucción desde mockup. [Antes desktop](evidence/phase03/baseline/desktop.png), [antes móvil](evidence/phase03/baseline/mobile.png). Diseño original azul #23486A/#3B6790, teal #4C7B8B, acento #EFB036, fuentes locales OpenSans/Nasa21 y navegación de biblioteca conservados.

## Fricciones observadas y cambios

| Antes / hallazgo | Cambio y validación |
|---|---|
| Baseline visual-chip E2E leyó selección vacía | Se agenda selección en OnAfterRender; el harness espera la condición exacta, no un sleep. La aserción y contenido esperado no cambian |
| Textos secundarios muy pequeños, glyphs mezclados | Escala de tarjetas/campos mejorada, componentes PromptCard/Toast/AppIcon, subconjunto SVG Lucide local con licencia ISC/MIT real conservada |
| Navegación móvil requería scroll horizontal interno | Seis accesos en grid de dos filas, sin scroll horizontal del documento en 320/390/768/1024/1440 |
| Inicio del circuito vacío si demora | LoadingSkeleton real hasta qp.init; a 12 s detiene animación y muestra recuperación/recarga, sin aparentar biblioteca ya cargada |
| Teal textual nuevo dio 4.33:1 | Se oscurece únicamente el teal de texto a #416B7B; tokens de identidad/bordes conservados |
| Transición de fondo con cambio de tema produjo contraste transitorio bajo | Tema cambia atómicamente; animación solo de borde/sombra. No se relaja el umbral AA ni se añade un sleep para esconderlo |
| Externalidad podía contradecir «todo local» | Biblioteca sigue local; textos explican que un editor externo puede almacenar borradores desde Preparar. Consentimiento por operación, envío desactivado por defecto |
| Sin extensión o fallo del proveedor | Texto permanece accesible, fallback explícito copiar/abrir; no se marca como enviado |
| Copiar/abrir falló sin permisos preconcedidos | Reproducción real: la pestaña tomaba foco antes de clipboard vía Blazor. Ahora se inicia escritura en el clic confiable antes de abrir/navegar, y Blazor registra solo el resultado. PASS con permisos por defecto, sin concesión previa de clipboard-write |

Se conserva Home como dueño del flujo de negocio. Se extraen componentes donde existe utilidad concreta; no se fabrican todos los nombres de componentes del documento ni se duplica estado. Modales nativos con Escape/foco y tema, tarjetas y navegación SVG coherentes, paneles contextuales, estados de interacción y reduced-motion. La copia móvil es sticky después de alcanzar su posición natural; márgenes de scroll evitan cubrir campos enfocados.

## Tailwind: compilación local comprobada

El usuario autorizó expresamente las dos dependencias de desarrollo y sus transitivas necesarias. Node 22.19/npm 10.9 ejecutaron el CLI oficial. Se fijan **tailwindcss 4.3.0 y @tailwindcss/cli 4.3.0**, con package-lock.json real. La primera instalación 4.3.3 fijaba @parcel/watcher 2.5.1 → micromatch → braces vulnerable. El CLI 4.3.0 admite watcher 2.6.0 dentro de su rango declarado; el lock resuelve esa versión, sin overrides ni audit fix --force. [Auditoría inicial](evidence/phase03/tailwind/npm-audit.json) conservada; [auditoría final](evidence/phase03/tailwind/npm-audit-final.json): cero hallazgos. [Aviso revisado](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm).

`package.json`, `Styles/input.css` y `tools/css-check.cjs` implementan build/watch/check locales, fuentes Razor explícitas, @theme static de identidad y capas legacy/components/utilities. Se conservan app.css y modern.css como fuentes importadas; App.razor sirve únicamente **quickprompt.css compilado**, sin duplicar hojas ni incorporar preflight que altere formularios existentes. Los componentes usan utilidades mediante @apply. css:check recompila con el CLI local en archivo temporal y compara todos los bytes. npm ci --ignore-scripts --offline reconstruyó la instalación desde el lock/caché y css:check volvió a pasar. La app arranca con el CSS entregado, sin Node/npm/red en runtime.

Fuentes: [CLI oficial](https://tailwindcss.com/docs/installation/tailwind-cli), [detección de clases](https://tailwindcss.com/docs/detecting-classes-in-source-files). La compilación está ejecutada y comprobada con Blazor .NET 8 y Edge 155. La búsqueda Python de ui-ux-pro-max no pudo ejecutarse (Python/py ausentes); se aplicaron sus reglas generales de contraste, teclado, estados y fuentes observadas, sin atribuir recomendaciones a una búsqueda inexistente.

## Evidencia y límites

[UI y métricas](evidence/phase03/ui-regression.json), capturas ui-320/390/768/1024/1440-light.png y ui-1440-dark.png bajo evidence/phase03. Medición automatizada seleccionar/completar/compilar/copiar: n=5, p50=139 ms, p95=220 ms; no representa tiempo de escritura humana. Arranque con DB desechable: n=1, 2073 ms hasta readiness propio. CSS servido en esa corrida: 23 129 bytes, sin gzip. No extrapolar estos números a Tailwind.

Auditoría automática propia de contraste/labels, teclado/foco, Escape, reduced-motion, viewports y captura real; no equivale a certificación WCAG completa ni prueba con lector de pantalla humano. Pruebas sobre DB/perfiles desechables, sin datos privados.

La regresión posterior a los cambios de sesiones/deadlines volvió a aprobar: [UI actual](evidence/phase03/races-final/ui/ui-regression.json). Otra muestra automatizada n=5: seleccionar/completar/compilar/copiar p50=126 ms, p95=230 ms; startup fresco n=1 de 323 ms. Son corridas distintas; no se fusionan percentiles ni se extrapolan a Tailwind. Se conservaron el texto editable tras compilar y el fallback sin permiso clipboard-write preconcedido.

## Verificación con Tailwind servido

[Corrida Tailwind](evidence/phase03/tailwind/ui/ui-regression.json): seleccionar/completar/compilar/copiar n=5, muestras 178/126/125/109/129 ms, **p50=126 ms, p95=178 ms**; arranque fresco n=1 **992 ms**. CSS local **24 849 bytes**, sin gzip. El navegador verificó HTTP 200, igualdad byte por byte con quickprompt.css, versión del compilador y token de identidad calculado. Cinco anchos sin overflow; ES/EN, dark/light, teclado/Escape, reduced-motion, recuperación de skeleton y fallback con permisos normales PASS. Capturas reales en tailwind/ui; revisión visual de escritorio y 320 px sin cambiar identidad ni navegación. Estas muestras pequeñas describen esa corrida automatizada, sin probar mejora estadística de velocidad.
