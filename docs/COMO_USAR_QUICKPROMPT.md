# Empieza a usar QuickPrompt

Un **prompt** es la instrucción que le das a una IA. QuickPrompt conserva esa instrucción para que la reutilices con tus datos. **NOVA prepara la instrucción; Qwen responde cuando pulsas Usar en…**

Abre **http://127.0.0.1:5190**. Si ya tenías la aplicación abierta, guarda lo que estés editando y recarga la página para ver los botones nuevos. Pulsa **Empieza aquí: guía y videos**, debajo del título principal. También puedes abrir la guía con **Ayuda**.

Si aparece **ERR_CONNECTION_REFUSED**, el servidor local está detenido. Desde la terminal del proyecto ejecuta `.\scripts\Start-Nova.ps1 -NoBuild -Background`: abre NOVA y lo deja en segundo plano. Si lo inicias sin `-Background`, debes mantener abierta esa terminal.

## Tu primera tarea: escribir un correo

1. Baja a **Tu biblioteca**. Si escribiste algo arriba y no aparece la lista, pulsa **Borrar búsqueda**.
2. En **Colección**, elige **Correos y comunicación**.
3. Abre **Escribir un correo profesional**.
4. Completa: destinatario «equipo de soporte», objetivo «consultar el estado de mi pedido», contexto «pedido 123, se esperaba ayer y necesito una fecha estimada».
5. Pulsa **Usar en… → Ejecutar en este equipo**. Verás la respuesta de Qwen.
6. También puedes pulsar **Copiar prompt**, pegarlo en tu IA habitual y enviarlo tú.

No necesitas crear una plantilla para usar la biblioteca. Hay **60 prompts en 20 colecciones**, que cubren las 29 categorías disponibles. [Consulta las tareas incluidas](BIBLIOTECA_PRACTICA.md).

## Pedirle una instrucción a NOVA

En la entrada principal escribe, por ejemplo: «Necesito un prompt para resumir un documento». Pulsa la flecha. NOVA buscará una instrucción compatible o preparará una nueva con título automático. Si necesita un documento u otro dato, aparecerá su campo. **Guardar** añade la instrucción a tu biblioteca; **Usar en…** permite obtener una respuesta a esa instrucción.

El resultado se abre en una ventana. Puedes cerrarla con la X, **Cerrar resultado**, Escape o pulsando fuera. **Ver resultado de NOVA** la abre otra vez sin repetir la petición y conserva los campos que estabas completando durante esta sesión. Dentro de la ventana, la vista previa permanece visible. Cerrar la ventana no guarda el prompt en la biblioteca.

También puedes escribir acciones sobre tu biblioteca:

- **«Ve al prompt “Escribir un correo profesional”»**: abre ese prompt.
- **«Edita el prompt “Escribir un correo profesional”»**: abre su editor.
- **«Actualiza el prompt “Escribir un correo profesional” para que use un tono más cercano»**: Qwen prepara el cambio y abre el editor del prompt original. Revisa y pulsa **Guardar versión** para aplicarlo.
- **«Actualiza la biblioteca»** o **«Ve a favoritos»**: refresca la lista o cambia el filtro.

Si hay varios prompts parecidos, NOVA muestra opciones. Si no sabe qué prompt o qué cambio necesitas, te lo pregunta. Navegar funciona sin modelo; preparar una nueva redacción requiere Qwen local. Estos comandos actúan dentro de QuickPrompt.

## Crear y quitar una variable

Una variable es la parte del prompt que cambiarás cada vez. Por ejemplo: «Explica inteligencia artificial con un ejemplo».

1. Abre **Nuevo prompt** o **Editar** uno existente.
2. Selecciona «inteligencia artificial» en el campo **Contenido**.
3. Pulsa **Crear / quitar variable**. Verás `Explica <inteligencia artificial> con un ejemplo`.
4. Con la variable seleccionada, vuelve a pulsar **el mismo botón**. Recuperas el texto normal.
5. Si guardas la plantilla con la variable, al abrirla aparecerá un campo donde puedes poner cualquier otro tema.

El texto original seleccionado se conserva para revertir la operación durante esa edición. En variables que ya venían guardadas, quitar la variable deja su nombre como texto. Los cambios solo se guardan cuando pulsas **Guardar prompt** o **Guardar versión**.

## Cuatro videos cortos

Son grabaciones de la aplicación real, con datos de ejemplo y texto explicativo en español. No tienen narración de voz. Usa **Ampliar video** dentro de la guía para verlos cómodamente.

| Video | Duración aproximada | Ver |
|---|---:|---|
| Usar la biblioteca y obtener una respuesta real de Qwen | 28 s | [Reproducir](http://127.0.0.1:5190/tutoriales/01-biblioteca.webm) |
| Pedir a NOVA una instrucción y guardarla sin formulario | 30 s | [Reproducir](http://127.0.0.1:5190/tutoriales/02-nova.webm) |
| Crear y quitar variables con un solo botón | 14 s | [Reproducir](http://127.0.0.1:5190/tutoriales/03-variables.webm) |
| Ver el worker Playwright recorrer la aplicación | 16 s | [Reproducir](http://127.0.0.1:5190/tutoriales/04-playwright.webm) |

El video 4 muestra el worker de pruebas real: abre su propio navegador, busca un prompt, rellena campos y copia. No es una conexión automática con ChatGPT ni un envío externo. Para repetirlo **en una ventana visible**, desde la terminal del proyecto ejecuta:

```powershell
.\scripts\Ver-Playwright.ps1
```

La demostración crea su propia biblioteca de prueba y cierra únicamente su navegador. No controla tu pestaña personal.

## Volver a añadir o guardar la biblioteca

Los 60 prompts se añadieron tanto a la biblioteca abierta como a la biblioteca habitual de inicio de NOVA. Se conservaron los prompts anteriores. En otra biblioteca puedes usar **Guía y videos → Añadir biblioteca práctica**. No reemplaza tus versiones: si detecta conflictos te pide revisarlos. [Descarga el archivo reutilizable](../apps/web/public/library/biblioteca-practica.es.json).

Para conservar una copia de tus cambios personales, utiliza **Datos y respaldos → Exportar respaldo JSON**. Los videos y la biblioteca práctica se sirven desde tu equipo.
