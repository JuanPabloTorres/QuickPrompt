# QuickPrompt NOVA 1.0.0 — revisión de interfaz r2

Estado: **CERRADA COMO REVISIÓN DE INTERFAZ**, 2026-10-23, America/La_Paz.
Tag local: **`nova-v1.0.0-r2`**. Versión visible: **1.0.0 · Versión inicial**,
desde `apps/web/package.json`. Se conservan los tags anteriores sin moverlos.

El usuario solicitó commit y cierre condicionado a las comprobaciones. El cierre
cubre las mejoras de interfaz validadas. Los hallazgos A01 y A02 de la
auditoría del agente (referencia histórica conservada en el expediente original) siguen pendientes:
pérdida de un hecho en una respuesta y espera deficiente ante error del proveedor.
Son limitaciones medias conocidas, sin reparación en esta revisión. No se declara
fidelidad autónoma del contenido ni certificación global del agente. La aceptación
humana REL-09 previa se conserva en el [acta r1](NOVA_1.0.0_R1.md); no se atribuye
una nueva prueba humana a este cierre.

## Cambios incluidos

- Detalle del prompt con cabecera controlada, metadatos y acciones organizadas,
  contexto y preview diferenciados, incluyendo prompts sin variables y archivados.
- Burbuja NOVA sólo con icono, más visible; conserva ocultamiento durante control
  del agente y la política de apertura, suspensión y retorno.
- Historial con lista seleccionable y panel de lectura, instrucción y respuestas
  separadas, metadatos, estados de carga/error/vacío y descargas diferenciadas.
  Se conserva el texto literal y no se registra otro uso al recuperarlo.
- Diseño adaptable, tema claro y foco de revisión/cancelación del borrado. Se
  corrige la barra lateral que tapaba el historial mediante capas de esa capacidad.

Los propietarios y contratos se describen en [UI modular](../development/UI_COMPONENTS.md).
No cambia el esquema SQLite, la API, el proveedor, las dependencias ni el número
visible. No hay cambios de fuentes backend respecto de r1. Los cambios anteriores
de presencia del agente y acceso de escritorio permanecen documentados en sus
guías y commits.

## Comprobaciones del cierre

| Comprobación | Resultado y alcance |
| --- | --- |
| Frontend, reejecutado para r2 | 277/277 tests, 41 archivos, incluidos 9 del historial |
| Build TypeScript/Vite | Correcto; aviso preexistente del chunk principal >500 kB |
| Arquitectura estática | 36 controles aprobados; no califican inferencia |
| Documentación | Enlaces locales de 14 documentos y 3 tests del comprobador |
| Candidato local | 29 archivos del frontend reconstruido idénticos a los desplegados; `/health` 200 |
| Navegador en este cierre | Versión visible 1.0.0, historial vacío cargado y burbuja NOVA presente en 5277 |
| Backend | Sin cambios de fuente; los 1.163 tests de la auditoría anterior no se reejecutan ni se presentan como nuevos |

Los recorridos de navegador de las consultas de implementación, inmediatamente
anteriores al cierre, son evidencia previa: detalle con y sin variables, copia
manual y por el agente, archivo/recuperación; burbuja en biblioteca/detalle/editor
y móvil; historial con selección, paginación, copia exacta, descargas, revisión y
cancelación del borrado, teclado y temas. El historial se comprobó a 390 y 320 px
sin overflow horizontal. La respuesta de su fixture se identifica explícitamente
como sintética: no prueba generación IA. No se ejecutó borrado irreversible real.

El [manifiesto r2](nova-v1.0.0-r2.json) distingue estos recorridos previos de las
pruebas reejecutadas y registra hashes de fuentes, artefactos y evidencias locales.
Las capturas y logs permanecen en `.playwright/`, excluidos de Git; sus hashes son
referencias para la instalación de trabajo, no archivos incluidos en la entrega.
La evidencia conserva los fallos de selectores del arnés, la lectura prematura
del portapapeles y el solapamiento visual reparado; no se reescriben como PASS.

## Ejecución y recuperación

La instancia local 5277 conserva la API y su perfil. Sólo se actualizaron los
artefactos de frontend durante las consultas de implementación; el rebuild del
cierre coincide con ellos. Este snapshot se guarda mediante commit y tag local;
no incluye push, publicación remota ni instalador independiente.

El último despliegue de historial conserva el índice anterior en
`.nova-build/ui-history-polish-20261023/index-before-history.html` y los assets
antiguos, para revertir esa activación sin tocar SQLite. Para recuperar la UI de
la revisión r1 completa, reconstruir el frontend desde `nova-v1.0.0-r1` en un
checkout separado y utilizar el perfil existente, según la [guía de desarrollo](../development/DEVELOPMENT.md).
No se requiere migración de datos ni mover tags.
