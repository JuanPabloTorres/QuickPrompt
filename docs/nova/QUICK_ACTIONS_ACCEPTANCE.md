# Acciones rápidas de NOVA

Estado: **PASS en los recorridos medidos**, 2026-10-17T14:49:39.218216+00:00.

En inicio y al abrir un prompt aparecen Crear prompt, Actualizar prompt, Buscar y abrir y Eliminar prompt. Más acciones ofrece variables, preparación de destino y ejecución local. Los formularios piden solo los detalles de la acción. Al elegir una plantilla, sus variables reales determinan los campos; los destinos proceden del registro de la API. En el detalle se reutiliza la selección actual y Elegir otro permite cambiarla.

La petición lleva identidad, revisión y valores como datos tipados del contrato existente. La instrucción contiene la acción y el cambio solicitado, sin convertir el título en parte de la tarea ni añadir órdenes de guardado. NOVA utiliza el ejecutor visual y la actividad flotante existentes. Eliminar abre la confirmación de biblioteca ligada al prompt y revisión elegidos; no se añadió una herramienta de borrado al modelo. Crear y actualizar mantienen el original hasta la aprobación.

## Comprobaciones

| Comprobación | Resultado |
|---|---:|
| Backend y contratos | 247 / 247 |
| React, formularios y transporte | 52 / 52 |
| Builds API y React | PASS |

| Navegador | Resultado |
|---|---|
| cancel-preserves-independent-inputs | PASS |
| mobile-dark | PASS |
| mobile-light | PASS |
| delete-selection-review-confirm | PASS |
| real-update-review-no-save | PASS |
| real-create-draft-no-save | PASS |
| real-open-exact-selection | PASS |
| real-fill-template-fields | PASS |

El navegador usa React compilado, API real, Qwen3:8b instalado y una biblioteca sintética. La actualización conserva la revisión persistida; la creación no inserta un prompt al generar el borrador; los valores observados en los campos coinciden con lo solicitado; cancelar la eliminación conserva el elemento y confirmar elimina únicamente el elegido. La prueba verifica la independencia de entrada libre y búsqueda, Escape, móvil en ambos temas y ninguna Page adicional. Es QA headless grabado, sin afirmar interacción física del ratón ni contar ventanas personales.

La preparación de destinos y el formulario de ejecución local tienen pruebas de React que comprueban el registro dinámico, los valores y la diferencia entre preparar y ejecutar. Esta tanda no hace envíos externos ni repite una ejecución local completa desde ese botón. Las métricas corresponden a estos casos, no a todas las formulaciones naturales.

## Correcciones generales encontradas durante la integración

El generador podía devolver marcadores completos en los metadatos de nombres; el esquema ahora exige nombres sin delimitadores y el parser sigue validando el resultado. Los accesos rápidos ligan explícitamente el origen, su revisión y los valores al run. Las peticiones escritas libremente conservan la selección como contexto sin fijar su destino, para poder pedir otro prompt. La separación de citas descartaba una orden completa si contenía un título citado: conserva ahora los fragmentos literales de la orden y excluye el título. La evidencia de autoría se selecciona de esos fragmentos, evitando citas inventadas. Por último, los enums de texto comparaban JSON escapado en vez del texto decodificado; la comparación conserva Unicode y rechaza valores distintos. Los tests incluyen acentos, otros alfabetos, comillas escapadas y rechazo de contenido citado como permiso.

Las tandas fallidas permanecen como fallidas en la evidencia local. Se mantuvieron las mismas necesidades concretas y se corrigieron los contratos y la envoltura de las acciones; no se añadieron rutas por frases, categorías o títulos.

## Evidencia y uso

- [Reporte del navegador](../evidence/quick-actions/1792248322671/report.json).
- [Móvil oscuro](../evidence/quick-actions/1792248322671/mobile-dark.png) y [claro](../evidence/quick-actions/1792248322671/mobile-light.png).
- [Actualización pendiente de revisión](../evidence/quick-actions/1792248322671/real-update-review-no-save.png).
- [Resumen y hashes](QUICK_ACTIONS_VALIDATION_SUMMARY.json).
- [Vídeo de la sesión](../evidence/quick-actions/1792248322671/video/page@dde843f930448a290b0959c2d9d7e462.webm)

Los artefactos voluminosos son locales e ignorados por Git. Para repetir con herramientas ya instaladas, desde la raíz:

```powershell
dotnet build backend/QuickPrompt.Api/QuickPrompt.Api.csproj --no-restore -m:1 -p:BaseOutputPath="$PWD/.playwright/same-tab/"
npm run build --prefix apps/web
node Tests/nova/quick-actions.cjs
```

Usa API 5240, proxy de evidencia local 5241 y perfiles/bases propios; no instala modelos, paquetes ni navegadores. La instancia personal no fue utilizada como fixture ni reiniciada. Para cargar la función, detén el servidor anterior, ejecuta `.\scripts\Start-Nova.ps1 -NoBrowser` y recarga la misma pestaña. [Guía de uso](../COMO_USAR_QUICKPROMPT.md).
