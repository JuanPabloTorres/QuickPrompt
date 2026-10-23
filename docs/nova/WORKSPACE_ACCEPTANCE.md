# Biblioteca y acceso permanente a NOVA

Estado: PASS en los recorridos verificados el 17 de octubre de 2026.

La biblioteca permite tarjetas y filas con el mismo prompt y revisión, favoritos y acciones independientes con iconos: abrir, editar, duplicar y revisar la eliminación. Eliminar sigue requiriendo confirmación. Los filtros y la paginación utilizan los endpoints existentes.

Un único lanzador circular permanece en la esquina inferior derecha al navegar entre biblioteca, detalle y editor y al desplazarse. Empieza cerrado. Al abrirlo despliega un panel compacto con el input, cuatro acciones principales, más acciones y el registro de actividad. Conserva la solicitud al cerrarlo. Ctrl+K abre el panel y enfoca el input sin cambiar de pantalla. El panel se superpone al contenido y el lienzo reserva únicamente espacio para el círculo.

El encabezado fijo reúne cambio de tema y Configuración. Allí se encuentran vista, modo de preparación, velocidad, consentimiento de conservación, preferencias de idioma/formato/destino y respaldos. Las preferencias de interfaz se validan y se guardan localmente sin almacenar el texto de la solicitud. Las preferencias del modelo conservan su confirmación existente.

El inicio de NOVA sincroniza el borrador y los valores de la pantalla actual. El contexto del origen guardado solo incluye sus variables válidas; los campos del borrador se sincronizan mediante el contexto del editor. Una respuesta del modo resultado no reemplaza campos si la pantalla o sus datos han cambiado mientras esperaba.

## Comprobaciones

- React: **61/61**. Incluye continuidad del mismo input, Ctrl+K, contexto del detalle, snapshot del editor antes del inicio, configuración, persistencia y acciones de cada vista.
- Compilación TypeScript/React: PASS. Permanece el aviso existente de tamaño del paquete principal de Vite.
- Edge visible, SQLite sintético y API real: tarjetas/filas, favoritos, edición con revisión, duplicado, cancelar y confirmar eliminación, configuración, tema claro/oscuro, persistencia y viewport de 375 y 1280 px sin desbordamiento horizontal.
- Qwen3 real: el acceso «Completar variables» actuó desde el detalle actual, completó el control real, terminó sin pausa y conservó exactamente la versión guardada. Se verificó en dos sesiones.
- Interfaz final: nueva pasada completa de los recorridos de biblioteca/configuración/móvil después de los últimos ajustes de contexto; cero errores de navegador.

La evidencia local conserva las sesiones reales en `docs/evidence/workspace-1792275397310/` y `docs/evidence/workspace-1792275558420/`. La pasada final de interfaz está en `docs/evidence/workspace-1792275758584/`. Cada sesión sirve una copia fija de React y utiliza su propia SQLite. Las ejecuciones fallidas de preparación de QA se conservan con su estado original. Los vídeos, capturas y bases sintéticas quedan fuera de Git.

Reproducción, con herramientas ya instaladas:

```powershell
npm.cmd test --prefix apps/web
npm.cmd run build --prefix apps/web
node Tests/nova/workspace.cjs --real-qwen
```

Las instancias locales 5190 y 5192 sirven los assets actuales y responden `health=ok`. Recarga la pestaña para cargar los nuevos controles.

## Actividad comprobada de NOVA

El panel flotante distingue interpretación, redacción, extracción de datos y elección de la siguiente acción. Las acciones reales muestran hora, estado, prompt y revisión; las escrituras muestran los valores observados y su valor anterior. Localizar un control o comenzar una escritura no se presenta como un resultado confirmado. El recibo debe ser aceptado por el backend antes de publicar campos comprobados. Los reintentos actualizan la misma acción y no vuelven a escribir.

El registro abierto conserva el resultado final; cerrado, no muestra un indicador de trabajo inactivo. El icono de historial del asistente abre la última actividad. La descarga JSON es local e incluye los valores mostrados; no contiene la capacidad del documento ni razonamiento interno. Se mantienen hasta 100 acciones por ejecución y tres ejecuciones en memoria, sin persistir los valores. Recargar conserva solo las etapas recuperables del servidor.

Validación adicional del 17 de octubre: Edge visible con Qwen3 real, API y SQLite sintética en 5194. Se verificó igualdad exacta entre el valor de la variable, su registro comprobado y el informe descargado; no hubo duplicados, pausas ni cambios en el prompt guardado. Se abrió el registro tras completar, se comprobó su tamaño a 375 px y se cerró con Escape devolviendo el foco al historial. **Ocho controles PASS y cero errores de navegador**. Evidencia: `docs/evidence/workspace-1792276127161/validation.json`, capturas, vídeo e informe local en esa misma carpeta. Los assets de la API y React permanecieron fijos durante la prueba.

Las pruebas React incluyen la escritura mediante el driver real en formularios controlados, pérdida y reenvío de un recibo, valores exactos y previos, aislamiento entre ejecuciones, límite del registro, resultados interrumpidos y reapertura de historial sin controles obsoletos. La compilación TypeScript/Vite pasa; permanece el aviso conocido de tamaño del paquete principal.

## Cancelar eliminación sin navegar

La confirmación mantiene un destino de eliminación independiente del prompt abierto. Solicitar una eliminación desde biblioteca no abre el detalle ni sustituye el contexto actual; Cancelar, Escape y cerrar el diálogo solo descartan ese destino. Confirmar conserva la comprobación de identidad y revisión del destino elegido.

Regresión reproducida antes de corregir en tarjetas y filas. Después: **70/70 pruebas React**, compilación PASS y Edge visible con SQLite sintética: seis cancelaciones (tres formas de cierre en cada vista), sin navegación ni solicitudes DELETE, conservando el texto del asistente y la biblioteca. La eliminación confirmada posterior envió un único DELETE al elemento elegido. Evidencia local: `docs/evidence/workspace-1792276367317/validation.json` y `cancel-delete-stays-in-library.png`; también pasan los recorridos de configuración y móvil de esa sesión.

## Lanzador circular y panel compacto

El círculo usa el icono de destellos de NOVA: 60 px en escritorio y 56 px en móvil. Cerrado oculta input, accesos y actividad; abierto muestra un panel de hasta 420 px, ajustado al viewport y con animación que respeta movimiento reducido. La X, el círculo y Escape cierran sin borrar la solicitud ni detener el agente. Ctrl+K abre y enfoca el input. Si hay un menú o registro abierto, Escape cierra primero ese nivel y devuelve el foco a su control.

El indicador pequeño distingue trabajo, pausa y espera de revisión. El registro reside dentro del panel y conserva el portal accesible al abrir un diálogo. Al cerrar el asistente no se renderizan sus detalles ni se suscribe el registro a las actualizaciones de valores; el executor y los recibos siguen funcionando. Un guardado ya aprobado puede abrir el asistente para pulsar su control real sin robar el foco al formulario.

La regresión de interacción conserva la posición de los controles: pulsar el encabezado del asistente no contrae la actividad en `pointerdown`, evitando que el botón cambie de posición antes de completar el clic. Las pruebas React cubren este caso, cierre/reapertura, foco, Escape por niveles y conservación del input: **73/73 PASS**. Compilación TypeScript/Vite PASS.

Recorrido final con Edge visible y Qwen real: **11 controles PASS, cero errores de navegador**, en `docs/evidence/workspace-1792278067333/validation.json`. Incluye círculo cerrado en escritorio/móvil, panel responsive, pausa explícita, reabrir y continuar, completar el campo real con el panel cerrado, consultar el valor comprobado, descargar el informe y Escape con restauración del foco. El prompt persistido conserva exactamente su versión original. Las capturas y el vídeo están en esa misma carpeta. Los intentos fallidos anteriores se conservan; el último recorrido utiliza API y React fijos y pasa completo. Las instancias 5190 y 5192 responden `health=ok` y sirven este frontend.
