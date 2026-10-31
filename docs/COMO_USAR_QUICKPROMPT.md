# Usar QuickPrompt NOVA

Un prompt es una instrucción reutilizable. QuickPrompt guarda esa instrucción y sus versiones; tú aportas los datos de cada uso. Elige una acción en NOVA para buscar o crear contenido con ayuda de IA.

## Abrir

Abre [QuickPrompt](https://project-sxf1z.vercel.app), pulsa **Continuar con Google** y comprueba el correo de la cuenta antes de abrir o crear una biblioteca. Las bibliotecas de otra cuenta no se trasladan al cambiar de sesión. El registro y la recuperación por correo requieren que el operador haya habilitado el correo transaccional; si no están disponibles, usa Google para el acceso autorizado.

En una instalación local, usa la dirección que indique su arranque. Si el navegador muestra conexión rechazada, consulta el [arranque](../README.md). Recarga una pestaña después de actualizar sólo cuando hayas guardado tus cambios.

## Trabajar en tu biblioteca privada

En **Mis bibliotecas**, abre tu biblioteca y pulsa **Crear prompt**. Escribe un título y la plantilla, por ejemplo: `Revisa <nombre_proyecto>, de tipo <tipo>, cuyo propósito es <proposito>.` Guarda y comprueba que aparezcan los tres campos reales. Los marcadores forman parte de la plantilla; sus valores se completan en cada uso.

Pulsa **Usar** para completar los datos. **Preparar para copiar · sin IA** conserva una instrucción compilada en **Preparaciones recientes**; no produce una respuesta del modelo. Puedes volver a abrirla mediante **Ver resultado**. La plantilla guardada conserva sus marcadores.

La ejecución con IA requiere un equipo autorizado para esa misma cuenta y capacidad. Revisa **Conexiones locales** y el [Companion](development/COMPANION.md). Un permiso vigente no demuestra que el proceso esté atendiendo trabajos; conserva abierta su sesión y usa **Ctrl+C** para detenerla. No necesitas vincular un equipo para el CRUD o la preparación sin IA.

Las secciones siguientes describen también los controles del espacio local. Utiliza las acciones disponibles en tu pantalla; un control grabado en una versión anterior no sustituye el flujo de cuentas.

## Guardar una instrucción

Pulsa **Nuevo prompt**, escribe un título y el contenido y guarda. Puedes usar marcadores como `<notas>` o `<destinatario>`. Para pedir ayuda, abre el botón circular NOVA y elige **Crear prompt**; describe la tarea y revisa el borrador. **Revisar y guardar copia** y su confirmación guardan el resultado en la biblioteca.

Puedes corregir el borrador antes de revisar. Tanto el botón de NOVA como **Guardar prompt** revisan la edición actual. Comprueba título, contenido y variables; confirmar permite aplicar exactamente esa versión. La revisión sigue siendo necesaria: el modelo puede omitir una condición del encargo.

En **Crear prompt** o **Actualizar prompt**, activa **Definir las variables ahora** para indicar los nombres, uno por línea. Si no los defines, NOVA abre una aclaración antes de redactar y conserva tu petición. Esta lista crea marcadores; no pide valores de uso. Para `tipo`, las opciones `web/app/AI` pertenecen a la descripción y no se agregan como campos separados. Una lista vacía confirmada crea sin variables o conserva las existentes al actualizar. [Contrato y evidencias](nova/VARIABLES_CONFIRMADAS_20261025.md).

## Encontrar

La búsqueda de la biblioteca filtra por el texto que recuerdes. En NOVA, **Buscar por significado** permite describir para qué sirve el prompt; revisa los resultados. **Buscar y abrir** te permite escoger un registro y abrirlo. Si hay un origen seleccionado que no quieres usar, pulsa **Elegir otro**.

Favoritos, Recientes, Archivo y Colección acotan la biblioteca. Un resultado parecido no autoriza modificarlo ni reemplaza tu elección.

Si NOVA pregunta cuál de varios resultados quieres usar, pulsa el candidato mostrado en la actividad para abrirlo. Esa elección no ejecuta ni modifica el prompt.

## Completar y aplicar

Abre un prompt y completa sus campos. Revisa la **Vista previa del resultado**: representa la instrucción con tus datos.

- **Copiar prompt** copia el texto rellenado. **Plantilla con marcadores** conserva los campos para reutilizarla.
- **Usar en…** ofrece los destinos configurados. Preparar o copiar/abrir un destino no demuestra que el mensaje se haya enviado: tú decides cuándo pegar y enviar.
- **Ejecutar con IA local**, entre las acciones de NOVA, solicita la respuesta al modelo local con los datos del formulario.

El formulario muestra los valores del prompt abierto. Revísalos y completa todos los campos necesarios; borrar uno deja la ejecución deshabilitada hasta rellenarlo. El destino termina de cargar antes de que NOVA lo seleccione. Los errores de esta operación aparecen también dentro del diálogo preparado.

La disponibilidad depende del destino y del modelo instalado. Si una operación falla, lee su mensaje; no la des por realizada por ver un estado final del agente.

## Mejorar

Elige **Actualizar prompt**, indica el origen y el cambio. La propuesta se revisa antes de **Guardar versión** o **Guardar copia**, según el flujo ofrecido. Cambiar un borrador no sustituye por sí solo el registro guardado. Si cambió su revisión, actualiza el origen antes de intentar guardar.

## Archivar o eliminar

Archivar aparta el prompt y permite recuperarlo desde Archivo. Para borrar, elige **Más acciones → Eliminar prompt** o el control de eliminación del prompt. Revisa el nombre y confirma **Eliminar definitivamente**. **Cancelar** conserva el registro.

## Biblioteca de ejemplos y ayuda

**Empieza aquí: guía y videos** permite añadir una biblioteca práctica opcional de 60 prompts y 20 colecciones. Añadirla no es requisito para usar tus propios prompts. [Contenido de la biblioteca](BIBLIOTECA_PRACTICA.md). Los videos muestran recorridos grabados; algunos controles pueden corresponder a una interfaz anterior. Para el flujo vigente sigue esta guía y las acciones disponibles.

Configuración contiene apariencia, preferencias y herramientas de respaldo/importación. Revisa siempre la previsualización antes de importar; restaurar reemplaza la biblioteca y requiere su confirmación. Guarda los respaldos en un lugar privado.

[Documentación vigente](README.md) · [Versión inicial aceptada](releases/NOVA_1.0.0.md).
