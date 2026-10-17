# Recorrido visual de NOVA — 17 de octubre de 2026

El sistema principal está disponible en <http://127.0.0.1:5190/>. La demostración utiliza <http://127.0.0.1:5192/> y una biblioteca SQLite independiente en `.playwright/visual-lesson-20261017/quickprompt.sqlite`. Los ejemplos no se añadieron a la biblioteca personal.

## Cómo repetir lo que vimos

1. Pulsa **Crear prompt**. Describe la tarea y el resultado que necesitas. En la demostración: responder correos con claridad y amabilidad, usando el correo recibido y el objetivo de la respuesta.
2. Abre el botón flotante **NOVA** para observar la actividad. **Pausar** detiene los siguientes pasos; **Continuar** los reanuda.
3. Revisa el borrador y sus variables. **Guardar prompt** abre una revisión; solo **Confirmar guardado de esta versión** lo conserva.
4. Completa los campos directamente o utiliza **Más acciones → Completar variables**. La vista previa muestra los datos incorporados; la plantilla sigue siendo reutilizable.
5. Utiliza **Más acciones → Ejecutar con IA local → Ejecutar ahora**. Si NOVA pregunta si quieres modificar o utilizar la instrucción, aclara: «Utilízalo tal como está y ejecútalo con Qwen local para generar la respuesta del correo. No modifiques la plantilla».
6. Revisa la respuesta completa antes de copiarla. Generar el texto de un correo no lo envía.
7. Vuelve a **Biblioteca** y usa su buscador para abrirlo otra vez. **Eliminar prompt → Revisar eliminación → Cancelar** conserva el elemento.

La ayuda **Empieza aquí: guía y videos** contiene tres tutoriales cortos. El primer video se amplió y reprodujo con los controles reales del navegador; se comprobó que avanzaba y no presentaba errores de reproducción.

## Resultado observado

La creación llegó a revisión y se guardó después de probar tanto clic como Enter en **Guardar prompt**. La cumplimentación de dos variables terminó en `COMPLETED` con ambos valores completos. La ejecución posterior a la aclaración llegó a `COMPLETED`, con `responseComplete: true` y el prompt todavía en la versión 1.

Qwen produjo este ejemplo real:

> Asunto: Confirmación de reunión y solicitud de agenda
>
> Hola,
>
> Confirmo la reunión del jueves a las 10. ¿Podrías compartir la agenda para prepararnos mejor?
>
> Saludos,
> [Tu nombre]

Se comprobó la búsqueda, reapertura y cancelación de la eliminación. La generación y la interpretación local tardaron decenas de segundos; algunas peticiones requirieron una aclaración. No se presenta esta sesión como una evaluación exhaustiva de todas las instrucciones posibles.

## Incidencias encontradas y correcciones verificadas

- **Arranque en PowerShell:** el envoltorio de `npm` interpretaba mal la llamada del script. `Start-Nova.ps1` utiliza ahora `npm.cmd`, comprobado con la compilación real y el arranque de ambas instancias.
- **Revisión al guardar:** el detector de intervención manual pausaba la tarea antes de enviar la propuesta. El botón de guardar pertenece ahora al ámbito de aprobación existente. Se verificaron clic, Enter y confirmación, sin guardar antes de aprobar.
- **Varias acciones del modelo:** la respuesta con varias llamadas abortaba la cumplimentación. Se rechaza el lote y se permite un único reintento mediante el esquema de acciones disponibles. El modelo selecciona el siguiente paso; no se ejecuta ni se elige implícitamente ninguna acción del lote. Las acciones no disponibles siguen rechazándose. La repetición real completó ambos campos y utilizó `json_schema_retry` en su primer paso.
- **Ejecución del prompt ya abierto:** la primera acción de una petición contextual podía ejecutarse antes de registrar su identidad y revisión observadas. Ahora se vincula el estado validado antes del despacho. La validación del endpoint permanece intacta. Se repitió la petición desde el prompt abierto y se obtuvo una respuesta completa.

## Validación y evidencia

- Compilación React/TypeScript correcta; continúa el aviso existente de tamaño de un paquete JavaScript.
- Compilación API correcta, sin advertencias ni errores.
- **252 pruebas .NET y 52 pruebas React aprobadas.** Los casos nuevos cubren la selección estructurada tras un lote, el rechazo de herramientas no disponibles y la identidad antes de ejecutar desde contexto.
- Playwright con Edge **visible**, usando React, API, SQLite y el modelo Qwen reales. Se amplió el caso de regresión de `Tests/nova/quick-actions.cjs` para revisar y confirmar el guardado con clic y teclado; en esta sesión ese recorrido se verificó en la ventana visible, sin volver a ejecutar todo ese archivo.
- Ambos servidores respondieron correctamente a `/health`.

Evidencia local de esta sesión, excluida del control de versiones:

- [Grabación de ejecución, búsqueda y cancelación de eliminación](../evidence/visual-tour-1792248807157/demostracion-respuesta-local.webm).
- [Respuesta completa](../evidence/visual-tour-1792248807157/10-correo-completo.png).
- [Variables completas](../evidence/visual-tour-1792248807157/08-variables-completas.png).
- [Resultado de cumplimentación](../evidence/visual-tour-1792248807157/fill-run-result.json).
- [Resultado de ejecución](../evidence/visual-tour-1792248807157/execution-result.json).

Para iniciar el sistema principal en otra sesión: `.\scripts\Start-Nova.ps1 -Background -NoBrowser`. La [guía general](../COMO_USAR_QUICKPROMPT.md) incluye los tutoriales y la biblioteca práctica.
