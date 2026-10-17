# Empieza a usar QuickPrompt

Un **prompt** es la instrucción que le das a una IA. QuickPrompt conserva esa instrucción para que la reutilices con tus datos. **NOVA prepara la instrucción; Qwen responde cuando pulsas Usar en…**

Abre **http://127.0.0.1:5190**. Si ya tenías la aplicación abierta, guarda lo que estés editando y recarga la página para ver los botones nuevos. Pulsa **Empieza aquí: guía y videos**, debajo del título principal. También puedes abrir la guía con **Ayuda**.

Si aparece **ERR_CONNECTION_REFUSED**, el servidor local está detenido. Desde la terminal del proyecto ejecuta `.\scripts\Start-Nova.ps1 -NoBrowser -Background` y recarga tu misma pestaña. Si ya se ejecuta una versión antigua, detén ese servidor antes de volver a iniciarlo. Si lo inicias sin `-Background`, debes mantener abierta la terminal.

## Tu primera tarea: escribir un correo

1. Baja a **Tu biblioteca**. Si la lista está filtrada, limpia **Buscar en la biblioteca**. La petición que escribes a NOVA es independiente de esta búsqueda.
2. En **Colección**, elige **Correos y comunicación**.
3. Abre **Escribir un correo profesional**.
4. Completa: destinatario «equipo de soporte», objetivo «consultar el estado de mi pedido», contexto «pedido 123, se esperaba ayer y necesito una fecha estimada».
5. Pulsa **Usar en… → Ejecutar en este equipo**. Verás la respuesta de Qwen.
6. También puedes pulsar **Copiar prompt**, pegarlo en tu IA habitual y enviarlo tú.

No necesitas crear una plantilla para usar la biblioteca. Hay **60 prompts en 20 colecciones**, que cubren las 29 categorías disponibles. [Consulta las tareas incluidas](BIBLIOTECA_PRACTICA.md).

## Pedirle una instrucción a NOVA

Puedes empezar con los **botones de acciones rápidas**, sin escribir la instrucción completa:

- **Crear prompt**: describe qué necesitas; NOVA prepara el borrador.
- **Actualizar prompt**: elige el prompt y cuenta qué quieres cambiar.
- **Buscar y abrir**: busca y selecciona el prompt que quieres ver.
- **Eliminar prompt**: elige el elemento y revisa su nombre en la confirmación. Solo **Eliminar definitivamente** lo borra; cancelar lo conserva.
- **Más acciones**: completa las variables reales, prepara el prompt para una IA o solicita una ejecución local.

También están disponibles al abrir un prompt; allí ya conocen el elemento seleccionado. Puedes pulsar **Elegir otro** para cambiarlo. Los accesos de NOVA trabajan en la misma pestaña, conservan la revisión antes de guardar y preguntan si falta información. Preparar un destino no envía nada; **Ejecutar ahora** sí solicita una respuesta del modelo local. El formulario de detalles se puede cancelar sin iniciar ninguna tarea. La eliminación usa la confirmación de biblioteca, sin intervención del modelo.

En la entrada principal escribe, por ejemplo: «Necesito un prompt para resumir un documento». Pulsa la flecha. NOVA puede buscar, abrir, completar o preparar una instrucción nueva según lo que pidas. Verás los controles y la escritura en la misma pestaña. Si falta información, te preguntará.

Durante una tarea aparece el botón **NOVA** en la esquina inferior derecha. Ábrelo para ver lo que está haciendo, ajustar la velocidad, pausar, continuar o detener. Cierra el panel con la X, Escape o pulsando fuera: el formulario sigue disponible. Cuando la tarea termina o la cancelas, el indicador desaparece. Una pausa o un borrador que espera revisión conserva los controles necesarios para continuar.

El borrador no se guarda automáticamente. Revisa el contenido y pulsa **Guardar prompt**, **Guardar versión** o **Actividad de NOVA → Revisar y guardar versión**; confirma el texto que quieras conservar. **Usar en…** permite completar los datos y obtener una respuesta. Rechazar o detener no cambia el original guardado.

Si prefieres ver solo el resultado final, despliega **Opciones de preparación** y elige **Solo mostrar el resultado**. En ese modo el resultado se abre en un diálogo que puedes cerrar y recuperar sin repetir la petición. La vista previa permanece visible.

También puedes escribir acciones sobre tu biblioteca:

- **«Ve al prompt “Escribir un correo profesional”»**: abre ese prompt.
- **«Edita el prompt “Escribir un correo profesional”»**: abre su editor.
- **«Actualiza el prompt “Escribir un correo profesional” para que use un tono más cercano»**: Qwen prepara el cambio y abre el editor del prompt original. Revisa y pulsa **Guardar versión** para aplicarlo.
- **«Actualiza la biblioteca»** o **«Ve a favoritos»**: refresca la lista o cambia el filtro.

Si hay varios prompts parecidos, NOVA muestra opciones. Si no sabe qué prompt o qué cambio necesitas, te lo pregunta. Los botones de biblioteca y copia funcionan sin IA. Las peticiones naturales a NOVA requieren el modelo local disponible. Estas peticiones actúan dentro de QuickPrompt.

## Crear y quitar una variable

Una variable es la parte del prompt que cambiarás cada vez. Por ejemplo: «Explica inteligencia artificial con un ejemplo».

1. Abre **Nuevo prompt** o **Editar** uno existente.
2. Selecciona «inteligencia artificial» en el campo **Contenido**.
3. Pulsa **Crear / quitar variable**. Verás `Explica <inteligencia artificial> con un ejemplo`.
4. Con la variable seleccionada, vuelve a pulsar **el mismo botón**. Recuperas el texto normal.
5. Si guardas la plantilla con la variable, al abrirla aparecerá un campo donde puedes poner cualquier otro tema.

El texto original seleccionado se conserva para revertir la operación durante esa edición. En variables que ya venían guardadas, quitar la variable deja su nombre como texto. Los cambios solo se guardan cuando pulsas **Guardar prompt** o **Guardar versión**.

## Tres videos de uso

Son grabaciones de la aplicación real, con datos de ejemplo y texto explicativo en español. No tienen narración de voz. Usa **Ampliar video** dentro de la guía para verlos cómodamente.

| Video | Duración aproximada | Ver |
|---|---:|---|
| Usar la biblioteca y obtener una respuesta real de Qwen | 28 s | [Reproducir](http://127.0.0.1:5190/tutoriales/01-biblioteca.webm) |
| Preparar una instrucción y guardarla | 30 s | [Reproducir](http://127.0.0.1:5190/tutoriales/02-nova.webm) |
| Crear y quitar variables con un solo botón | 14 s | [Reproducir](http://127.0.0.1:5190/tutoriales/03-variables.webm) |

El video de preparación muestra el flujo de **Solo mostrar el resultado**, que sigue disponible en las opciones. La preparación visible actual permite observar a NOVA escribiendo en el formulario y consultar su actividad desde el botón flotante.

## Volver a añadir o guardar la biblioteca

Para añadir los 60 prompts a la biblioteca que estés usando, abre **Guía y videos → Añadir biblioteca práctica**. No reemplaza tus versiones: si detecta conflictos te pide revisarlos. [Descarga el archivo reutilizable](../apps/web/public/library/biblioteca-practica.es.json).

Para conservar una copia de tus cambios personales, utiliza **Datos y respaldos → Exportar respaldo JSON**. Los videos y la biblioteca práctica se sirven desde tu equipo.
