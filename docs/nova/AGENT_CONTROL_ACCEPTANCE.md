# NOVA: continuidad, campos y decisiones útiles

La base funcional se publicó en `main` mediante `74f0f40`. Esta revisión cambia la política de interacción solicitada después de esa publicación: tocar la pantalla no significa tomar el control ni pausar NOVA.

## Causas comprobadas

- El listener global de eventos confiables trataba cualquier clic o tecla fuera de la actividad como intervención. Llamaba a `intervene()`, que detenía permanentemente el driver. Continuar reanudaba el servidor, pero no ese driver; por eso podía quedar el formulario vacío.
- La visibilidad bloqueaba el avance en React, en el driver y en el dispatcher. Además, un monitor convertía 15 segundos sin polling en una pausa que exigía intervención del usuario, incluso durante la interpretación.
- Una desconexión transitoria también exigía Continuar. Los plazos de espera de controles incluían el tiempo en pausa explícita.
- El orquestador leía la interfaz antes de cada decisión y verificaba cada efecto, pero ofrecía al modelo herramientas para repetir esas mismas lecturas. En la sesión preliminar se observó una decisión redundante de lectura de aproximadamente 15 segundos.

## Corrección

Los clics, el foco, la selección, el desplazamiento y la visibilidad no solicitan pausa. Pausar y Continuar siguen controlando la misma ejecución. La conexión se recupera automáticamente usando la misma identidad, secuencia y recibo; no se repite una acción DOM para recuperar una respuesta perdida. El dispatcher ya no necesita monitores periódicos que lean SQLite. Su cierre cancela las esperas pendientes y libera las superficies.

La escritura sigue pasando por el `onChange` real de React, con comprobación exacta del valor. Un cambio real de contenido u objetivo conserva la protección contra sobrescrituras; no se confunde con un clic sin cambio. La espera de controles excluye el tiempo de pausa explícita. Se mantienen la cancelación, los límites globales, la autorización por documento y la aprobación ligada a contenido/revisión antes de guardar.

El modelo sigue interpretando la petición y eligiendo las acciones funcionales. Se eliminan de su menú las lecturas que el controlador realiza obligatoriamente. Cuando ya se cumplen los efectos pedidos, tampoco se ofrecen inspecciones opcionales ni nuevas preguntas. No se añaden reglas basadas en frases del usuario, dependencias ni contenido prefabricado.

## Validación

- Backend: 255/255; incluye pausa explícita, pestaña no visible, cierre repetido con una acción pendiente y una interrupción de polling de 16 segundos sin crear otra acción ni una pausa.
- React: 55/55; incluye recuperación de conexión, escritura con documento oculto, conservación de edición concurrente, Unicode y reanudación sin consumir el plazo del control.
- Compilaciones API y React correctas. Persiste el aviso existente sobre tamaño del paquete principal de Vite.

Los **seis recorridos finales pasaron en una única sesión de Edge visible**, con Qwen3 real, una API/React inmutable y SQLite sintético: creación y aprobación con igualdad exacta entre modelo/formulario/guardado; adaptación con aclaración de origen y aprobación; variables con pérdida de un recibo sin repetir la escritura; pausa/continuación/cancelación durante los fragmentos; clics en la pantalla inicial y foco/teclas/desplazamiento durante la escritura; preparación para Claude sin envío externo.

El [resumen portable](AGENT_CONTROL_VALIDATION.json) conserva los hashes y las decisiones observadas. La evidencia local completa está en `docs/evidence/same-window/acceptance-1792273780681/`: informe PASS, vídeo, capturas, eventos y recibos. Ninguna decisión final solicita releer o verificar el estado ya observado. Se inspeccionó visualmente la captura de creación tras las interacciones.

Las instancias locales `5190` y `5192` se reiniciaron con esos mismos ensamblados y assets, comprobados por SHA-256 y por HTTP. Ambas responden `health=ok`. La biblioteca habitual se conserva; los recorridos se realizaron únicamente sobre SQLite sintético.

Al minimizar Edge, el navegador siguió informando `visibilityState=visible`: esa maniobra no se presenta como prueba de un documento oculto real. La ruta de documento oculto se comprobó en React y en el dispatcher. Las bases de QA, vídeos y registros permanecen fuera del commit.

La eliminación de inferencias redundantes reduce trabajo evitable; no implica una reducción porcentual universal del tiempo. La interpretación y generación reales de Qwen siguen dependiendo del equipo y de la petición.
