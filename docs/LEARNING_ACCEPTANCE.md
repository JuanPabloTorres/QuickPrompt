# Biblioteca práctica, tutoriales y variable reversible

Solicitud: explicar el uso real, aportar prompts útiles por categorías, grabar videos cortos, crear/quitar variables con un botón y hacer visible Playwright.

## Entregado

- 60 prompts redactados en español, 20 colecciones, cobertura de las 29 categorías del enum existente. Cada prompt usa entre uno y tres campos, con resultados y límites concretos. [Catálogo](BIBLIOTECA_PRACTICA.md).
- Importación aditiva tanto en la biblioteca abierta de 5190 (3 existentes + 60) como en la biblioteca habitual de NOVA (vacía + 60). Respaldo físico previo y comprobación de reimportación sin duplicados. No se abrió `QuickPrompt.db3` ni se alteraron preferencias. [Evidencia de instalación](evidence/learning/install-5190.json), [inicio habitual](evidence/learning/install-5203.json).
- Guía dentro de la app con explicación de prompt/respuesta, instrucciones, descarga/importación de biblioteca y cuatro reproductores con pantalla completa.
- Cuatro WebM reales, subtítulos VTT locales y posters. Grabación en Edge visible con DB/perfil separados; Qwen real en los dos primeros recorridos. [Grabaciones y duraciones](evidence/learning/recordings.json).
- Botón **Crear / quitar variable** dentro del editor. El servidor utiliza `TemplateEditor`/`TemplateCompiler` existentes, acepta selección/caret dentro de una variable y valida límites, escapes, nombres y solapamientos. La operación es pura; guardar sigue siendo explícito. El texto original se mantiene en memoria de edición para restauración exacta.
- `--headed` y `--record` en el runner cerrado, con las mismas restricciones de origen/acciones. `Ver-Playwright.ps1` abre una demostración local en ventana, sin tocar datos ni perfiles personales. No se habilitaron proveedores externos nuevos ni herramientas libres al modelo.

## Verificación

- Backend: 36 pruebas, incluidas importación real SQLite, las 60 compilaciones, categorías, preferencias, duplicados y reversibilidad de variables.
- Frontend: 16 pruebas; botón único, restauración, guía accesible y regresiones anteriores.
- [Recorrido Edge de aprendizaje](evidence/learning/acceptance.json): colecciones, borrar búsqueda, Unicode/puntuación en toggle, guía sin perder borrador, importación repetida y decodificación de los cuatro videos con seek y fotogramas visibles. Se verifica también móvil de 390 px y ausencia de errores de consola.
- [Worker visible real](evidence/learning/recordings.json): búsqueda, apertura, campos, copia, uso registrado una vez y plantilla conservada.
- Worker: dos pruebas de política y cuatro recorridos de regresión (fallo, cancelación, recuperación y timeout), sin ampliación de dominios ni permisos externos. [Resultados](evidence/learning/worker-regression/automation.json).
- La instancia activa de 5190 se actualizó conservando su SQLite: biblioteca disponible, crear/quitar variable y archivos de video/subtítulos comprobados por HTTP. No se recargó la pestaña del usuario ni se descartó su editor. [Servicio activo](evidence/learning/active-service.json).

Correcciones detectadas al verificar: guardar el video después de cerrar el contexto y antes de cerrar el navegador; registrar `.vtt` explícitamente como `text/vtt` en ASP.NET sin habilitar archivos de tipos desconocidos. Los fallos previos se conservan en [iterations](evidence/learning/iterations).

No se instalaron paquetes, navegadores, modelos ni codificadores. Se reutilizaron Edge, Playwright y ffmpeg ya disponibles. Videos con texto explicativo, sin voz. La revisión no certifica servicios externos ni cambia la limitación histórica de LF/CRLF del clipboard Windows.
