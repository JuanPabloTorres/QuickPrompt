# NOVA Living Interface

> Actualización posterior: el logo oficial enviado por el usuario sustituye al isotipo Orbit en todos los puntos de marca activos. Ver [integración del logo oficial](OFFICIAL_LOGO.md). Las capturas y métricas de este informe corresponden a la etapa anterior.

## Auditoría previa — 17 de octubre de 2026

Referencia funcional: 73/73 pruebas React y 260/260 backend. Edge real, SQLite sintética y copia inmutable de los assets; no se utiliza la biblioteca personal. [Referencia visual](../evidence/identity-before-1792279738399/validation.json), [ejecución Qwen previa](../evidence/workspace-1792279420596/validation.json).

| Hallazgo | Clasificación | Evidencia y corrección |
|---|---|---|
| Marca «qp», Sparkles en asistente, cruz orbital antigua y «✦» en cursor | Consistencia | Tres identidades distintas en el código; unificar en Orbit |
| Iconos a 13/14/15/17/18/19/21/26 px | Consistencia | Normalizar Lucide a 16/20/24/32 y trazo 1.75 |
| Instrucciones largas en Cascadia/Consolas | Legibilidad | `pre` global; usar fuente proporcional |
| Colores fijos en cursor | Consistencia | HEX/fallbacks fuera de tokens; usar semántica por tema |
| Bordes anidados y header de 100 px | Pulido visual | Capturas Discover; superficies más ligeras y header de 76 px |
| Escape en selector de destino pierde el foco de «Usar en…» | Accesibilidad | Medición previa `providerDialogRestoresFocus: false`; restaurar opener real |
| Solo reducción de movimiento del SO | Accesibilidad | Añadir preferencia propia conservando estados y controles |

Dependencias instaladas verificadas: React 19.3.1, Tailwind 4.3.0, Motion 14.3.0, Lucide 1.58.0 y Radix Dialog 1.3.1. Licencias locales inspeccionadas: Lucide ISC y derivados Feather MIT; Motion/Tailwind MIT. Se conservan sus avisos de distribución. Orbit es SVG original escrito para este proyecto, sin assets de terceros ni marcas de proveedores.

Se evaluaron Inter/Geist, pero no existen sus archivos en el proyecto. Se conserva Segoe UI/system-ui instalada, sin redistribuir ni descargar fuentes. El buscador Python de ui-ux-pro-max no está disponible (python/python3/py ausentes o alias); se usan sus reglas locales como orientación general, sin presentar resultados de catálogo inexistentes.

## Sistema visual

Tokens únicos en `apps/web/src/design/tokens.css`, expuestos también por `@theme inline` de Tailwind 4. Se conserva sRGB explícito: las dos paletas están fijadas por el encargo y no necesitan interpolación OKLCH. Las intensidades suaves se derivan con `color-mix`.

| Uso | Aurora | Daylight |
|---|---|---|
| Fondo | #0C1524 | #F6F8F7 |
| Superficie | #162639 | #FFFFFF |
| Elevada / hover | #203348 / #263D52 | #EDF1F5 |
| Texto | #ECF8F7 | #151F2B |
| Secundario | #A9BECA | #58717B |
| Acción | #5DE2D6 | #3259E8 |
| Inteligencia | #AEAAFF | #6553B8 |
| Acento secundario | #B7EDBA | #B7EDBA |

Otros tokens: action-hover/foreground/soft, intelligence-soft, border subtle/active/input, focus-ring, success/warning/error y sus superficies, disabled, selection, scrim, sombras pequeña/elevada. El lavanda claro se oscureció para asegurar legibilidad. El hover claro inicial #E4EAF2 se descartó: su texto secundario daba 4.27:1; #EDF1F5 supera 4.5:1. Las superficies suaves usan 12% de intensidad en Aurora y 6% en Daylight: una primera mezcla del 12% también en claro daba entre 4.29 y 4.36:1 para texto secundario y fue corregida.

Escala: hero clamp 28–40 px, página 28–44, sección 18, prompt/cuerpo/input 16, metadatos 14, caption 12. Los prompts usan fuente proporcional y line-height 1.75; solo código y atajos conservan mono.

## Identidad e iconografía

Orbit usa núcleo sólido, órbita abierta con retorno y satélite contextual. Fuentes vectoriales: [logo oficial actual](../../Resources/Branding/quickprompt_official.png) y [favicon adaptable](../../apps/web/public/favicon.svg). Misma geometría en `NovaOrbit.tsx`, tamaños 16/24/32/64/128. La aplicación no tiene instalación PWA: no se añade un manifiesto innecesario.

`AppIcon` normaliza tamaño, trazo, currentColor, alineación, estado disabled y etiqueta accesible; decorativo por defecto. Iconos con texto permanecen ocultos a lectores. Los botones sin texto mantienen aria-label. Familia única Lucide, además del isotipo propio.

| Función | Iconos |
|---|---|
| Biblioteca y búsqueda | Library, Search, FileText, LayoutGrid, List |
| Crear/editar | Plus, Pencil, Braces |
| Reutilizar/copiar/guardar | Copy, Check, Save |
| Colecciones | Folder, FolderPlus |
| IA local/destino | Bot, Play, ArrowUpRight; proveedores con nombre explícito |
| Historial | History, Clock3, Undo2 |
| Estado/control | LoaderCircle, CircleAlert, Square, Pause, Check, X |
| Configuración | Settings, Palette, Monitor, Sun, Moon |
| Acciones auxiliares | Download, ShieldCheck, Archive, Trash2, MoreHorizontal, flechas, CircleHelp |

## Componentes y movimiento

Se conservan App, PromptFocus, InlinePromptEditor, PreparationCanvas, ProviderPicker, GroupSelector, Dialog y el drawer circular persistente. Se añaden únicamente AppBrand, AppIcon, NovaOrbit y feedback compartido (NovaActivity, NovaProgress, InlineSpinner, PromptSkeleton, SaveIndicator, TransferStatus, EmptyState, ErrorState). El skeleton solo aparece tras 250 ms de carga real. No se modifican backend, contratos ni AgentUiDriver en este trabajo.

Variantes `canvasMotion` y `panelMotion`: entrada 180 ms y salida 100 ms, transform/opacity. Tokens CSS compartidos para controles y actividad. Estados derivados de run/status, eventos y herramienta reales; sin porcentajes ni progreso por temporizador:

| Estado | Presentación |
|---|---|
| IDLE | Orbit estable |
| SEARCHING | Segmento orbital |
| THINKING | Pulso lavanda tenue |
| GENERATING | Satélite activo mientras generación reportada |
| NAVIGATING | Identidad estable y cursor sobre el elemento real del driver |
| VALIDATING | Órbita lenta mientras verificación |
| READY | Asentamiento breve y estado estable |
| ERROR | Color discreto más mensaje textual |
| OFFLINE | Identidad secundaria y explicación de indisponibilidad |

Pausa/cancelación dejan de representar trabajo aunque el evento anterior fuera generación. Movimiento reducido del SO o de Configuración desactiva efectos; nunca altera ejecución, cancelación, verificación ni posición real del cursor. No hay sombras animadas, blur ni partículas. Se inspeccionó el código instalado de Motion: su hook toma una instantánea de la preferencia del SO. `useMotionPreference` escucha también los cambios posteriores y configura las variantes estáticas (incluida opacidad) sin recargar.

## Validación final

La [matriz final en Edge](../evidence/identity-after-1792280441675/validation.json) pasa sobre 1.002 registros sintéticos: ambos temas, anchos 320/390/768/1024/1440, altura de 480, movimiento reducido, búsqueda, selección, variables, copiar, editar, crear, guardar, destinos y restauración de foco. Solo se renderizan 12 elementos por página. Cero errores JavaScript observados. Se comprobó también persistencia del ajuste de movimiento y desaparición del skeleton al terminar una carga con latencia inyectada.

| Vista | Antes | Después |
|---|---|---|
| Discover Aurora | [Captura](../evidence/identity-before-1792280234263/discover-dark-1440.png) | [Captura](../evidence/identity-after-1792280441675/discover-dark-1440.png) |
| Discover Daylight | [Captura](../evidence/identity-before-1792280234263/discover-light-1440.png) | [Captura](../evidence/identity-after-1792280441675/discover-light-1440.png) |
| NOVA móvil | [Captura](../evidence/identity-before-1792280234263/assistant-light-390.png) | [Captura](../evidence/identity-after-1792280441675/assistant-light-390.png) |
| Focus | [Captura](../evidence/identity-before-1792280234263/focus-light.png) | [Captura](../evidence/identity-after-1792280441675/focus-light.png) |
| Create | [Captura](../evidence/identity-before-1792280234263/create-light.png) | [Captura](../evidence/identity-after-1792280441675/create-light.png) |
| Ejecución | [Qwen previo](../evidence/workspace-1792279420596/live-progress.png) | [Qwen posterior](../evidence/workspace-1792279809155/live-progress.png) |

[Plancha SVG 16–128 px](../evidence/identity-after-1792280441675/orbit-sizes.png), [preview vectorial local](NOVA_ORBIT_PREVIEW.html), [fallo Ollama inyectado](../evidence/identity-after-1792280441675/ollama-error.png), [destino sin capacidad/modelo ausente](../evidence/identity-after-1792280441675/unavailable-provider-and-model.png), [carga sostenida](../evidence/identity-after-1792280441675/sustained-library-load.png). Los videos de los recorridos se conservan en las carpetas de evidencia.

### Accesibilidad

- [92 combinaciones de color](../evidence/identity-contrast.json) pasan: mínimo texto 4.55:1, mínimo contorno funcional 3.20:1. No se atribuye contraste funcional a divisores decorativos ni a controles deshabilitados.
- Tab/Shift+Tab permanecen dentro del diálogo; Escape devuelve foco a «Usar en…»; el baseline no lo hacía. Se conserva el [árbol accesible real de Chromium](../evidence/identity-after-1792280441675/dialog-accessibility-tree.json).
- Foco visible, controles principales de al menos 44 px, labels y estados textuales/aria además de color; marca móvil ajustada para no competir con los tres controles del header.
- Se probó reflow a 640 px (equivalente geométrico a 1280 al 200%) y ampliación de contenido con CSS zoom 2. Los atajos de zoom nativo enviados por Playwright no cambiaron DPR/viewport: **zoom nativo de Edge pendiente de comprobación manual**. No se presenta CSS zoom como sustituto idéntico.
- No hay un lector de pantalla controlable disponible: se inspeccionaron semántica y árbol accesible, **no se verificó voz real con NVDA/JAWS**. No se declara certificación WCAG completa.

### Pruebas y rendimiento

- React: **77/77**, 11 archivos, incluido retorno de foco, migración de preferencias existentes y cambio del movimiento del SO durante la sesión.
- Backend NOVA: **260/260**; suite web anterior .NET 8: **150/150**, con salida de compilación aislada para no detener el servidor antiguo que tenía DLL bloqueadas.
- Build TypeScript/Vite correcto. Persiste el aviso previo de chunk principal superior a 500 KB; no se ocultó ni se cambió arquitectura para eliminarlo.
- [Workspace real](../evidence/workspace-1792279809155/validation.json): 11 controles, tarjetas/filas/favoritos, edición/duplicado/eliminación, cancelar sin navegación, configuración, drawer persistente, Qwen real completando variables en la pantalla actual, pausa explícita, continuación con panel cerrado, valores observados exactos y descarga de actividad.
- [Feedback local real](../evidence/local-feedback-1792281018516/validation.json): carga visible con Ollama real, cancelación sin respuesta parcial copiable ni indicador persistente, reintento, respuesta completa y copia. [En curso](../evidence/local-feedback-1792281018516/real-qwen-loading.png), [cancelado](../evidence/local-feedback-1792281018516/cancelled-cleanly.png), [completado](../evidence/local-feedback-1792281018516/real-qwen-complete.png). La comparación de portapapeles normaliza únicamente CRLF de Windows a LF. No se repite la petición para obtener evidencia del cuerpo SSE que Chromium descarta al cancelarse su lector tras `complete`.
- [Creación con cursor real](../evidence/same-window/acceptance-1792280150545/A-real-cursor-writing.png): el caso A pasa, con 50 eventos de escritura, cursor sobre los controles reales, revisión y aprobación, igualdad entre propuesta/formulario/texto guardado y borrador limpio posterior. La tanda completa se interrumpió luego en B por timeout del modelo durante una aclaración; no fue una aprobación de todos sus casos.

| Medición | Antes | Después |
|---|---:|---:|
| JS principal minificado | 548.429 B | 552.660 B (+0,77%) |
| JS principal gzip (Node zlib) | 168.018 B | 169.429 B (+1.411 B) |
| CSS minificado | 41.621 B | 46.352 B |
| CSS gzip (Node zlib) | 9.325 B | 10.142 B (+817 B) |
| Solicitudes de fuentes | 0 | 0 |
| Elementos DOM de biblioteca / total | 12 / 1.002 | 12 / 1.002 |
| Búsqueda, mediana de 5 consultas | 275 ms | 279 ms |
| Búsqueda, rango | 238–310 ms | 262–303 ms |

Consultas repetidas sin fallos inyectados: [antes](../evidence/identity-before-1792280576419/validation.json), [después](../evidence/identity-after-1792280530152/validation.json). Incluyen el debounce existente de 180 ms. Una medición del recorrido con inyección de red dio 1.397 ms; se conserva en la matriz, pero no se atribuye a una mejora/regresión de render sin repetir el mismo protocolo. Los tiempos de red constan en los JSON.

Diagnóstico rAF de 40 intervalos durante apertura (pantalla de alta frecuencia): mediana 7 ms en ambos; p95 41,7→27,7 ms; máximo 215,2→97,2 ms. [Baseline](../evidence/identity-before-1792280234263/validation.json) y [final](../evidence/identity-after-1792280441675/validation.json). Hubo ventanas y Qwen concurrentes: no es un benchmark aislado ni garantiza FPS en todos los dispositivos. La muestra posterior específica de performance perdió callbacks rAF al quedar en segundo plano; se conserva y no se usa para afirmar FPS.

CLS del recorrido completo previo: 0,084; después de recargar preferencias: 0. Son ventanas de observación distintas, por lo que **no se afirma una mejora causal de CLS**. El checkpoint común de primera biblioteca/panel registró long tasks de 65/113 ms antes y 53/91 ms después; sin tareas nuevas atribuibles a efectos continuos, pero con ruido de ejecución concurrente. La medición inicial hasta abrir el panel también estuvo afectada por foco entre ventanas; no equivale a tiempo de carga del producto.

Una [comprobación adicional](../evidence/identity-after-1792280793495/validation.json) registró biblioteca lista en 725 ms y cinco búsquedas en 212–280 ms. Su muestreo rAF sufrió una discontinuidad de 32,6 s al perder continuidad de presentación, sin una long task de esa duración; esa muestra **no es válida como medición de FPS** y no se utiliza para aprobar fluidez. El JSON conserva tanto la discontinuidad como los tiempos de red y las dos long tasks de 51/126 ms. No se presenta esta tanda como benchmark de laboratorio.

### Fallos y límites conservados

- Primeros intentos del runner antiguo esperaban actividad flotante independiente y ocultación automática del historial terminado. Se ajustaron esas aserciones a su ubicación dentro del drawer y al cierre explícito; los fallos originales permanecen en `same-window/acceptance-1792279940986` y `acceptance-1792280034151`. En el segundo ya se había guardado correctamente la propuesta antes de fallar el cierre de prueba.
- La adaptación por búsqueda/clarificación del caso B recibió `TIMEOUT`, sin propuesta ni acciones UI en ese intento. No se modificaron límites, inferencia, backend ni navegación para forzar éxito. Ver [registro completo](../evidence/same-window/acceptance-1792280150545/acceptance.json).
- El proyecto histórico Windows .NET 9 no pudo ejecutarse con el SDK 8.0.425 instalado (`NETSDK1045`). No se instaló otro SDK. Esto no es un resultado de la suite React/NOVA.
- Recursos de evidencia generados siguen ignorados por Git; están conservados localmente y los runners reproducen los escenarios con bases nuevas.

La [tanda separada D/E/L/S](../evidence/same-window/acceptance-1792280452846/acceptance.json) pasó pausa/continuación/cancelación, dos consultas con distinta redacción y ejecución Qwen local con respuesta DOM igual a la respuesta verificada. La adaptación S falló con `UI_DRAFT_REQUIREMENTS_MISMATCH`: se conservan [fallo y traza](../evidence/same-window/acceptance-1792280452846/failures.json). El texto sí se escribió por fragmentos y conservó «arquitectura» y «No inventes datos»; el backend infirió `format: MARKDOWN` y su validador exige que aparezca literalmente ese término en el prompt, que no lo contenía. Esa condición es suficiente para rechazar el borrador independientemente del estilo de la interfaz (`PromptPreparationPipeline.cs`, comprobación FORMAT). No se aprobó ni comprometió guardado. Se registra como defecto de interpretación/validación que esta entrega visual **no corrige**, respetando el alcance de no cambiar el agente/backend.

Por tanto, **no se declara verde toda la suite E2E del agente**: A/D/E/L y el workspace pasan; B y S permanecen con los fallos documentados. La identidad visual y sus pruebas de interfaz están implementadas; la aceptación total del motor requiere resolver esos dos casos en un trabajo de dominio separado.

Servidor local comprobado: `http://127.0.0.1:5190/` devuelve 200 y sirve `index-C5Gr_ZFh.js`; `/health` devuelve `status: ok`, esquema 2. Basta recargar la aplicación para ver la identidad nueva. No se instalaron dependencias, descargaron modelos/fuentes, modificaron datos privados, crearon commits ni publicaron cambios.

Reproducción (PowerShell, desde la raíz; requiere las dependencias y Edge ya instalados):

```powershell
npm --prefix apps/web test
npm --prefix apps/web run build
dotnet test backend/QuickPrompt.Tests --no-restore
dotnet test src/QuickPrompt.Tests --no-restore -p:BaseOutputPath=C:\Projects\Repos\QuickPrompt-Web\.nova-build\identity-legacy\
node Tests/nova/identity-contrast.cjs
node Tests/nova/visual-identity.cjs --large-library
node Tests/nova/workspace.cjs --real-qwen
node Tests/nova/local-feedback.cjs
$env:NOVA_TEST_BUILD='backend/QuickPrompt.Api/bin/Debug/net8.0/QuickPrompt.Api.dll'
$env:NOVA_SAME_TAB_CASES='A,B,C,C2,D,E,F,P,L,R,S'
node Tests/nova/same-window.cjs
```

El runner semántico conserva sus fallos y aborta la tanda al primer caso fallido. Las evidencias separadas D/E/L/S se obtuvieron seleccionando esos casos mediante la variable indicada, sin cambiar las instrucciones del modelo ni su validación para aprobarlos.
