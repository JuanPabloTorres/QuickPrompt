# NOVA — aceptación del control en la misma pestaña

Estado: **PASS en los escenarios medidos**, validación completada el 17 de octubre de 2026. Se completaron los recorridos pendientes: creación, adaptación, cumplimentación, guardado con aprobación, cancelación e intervención manual.

El resultado reúne **13 grupos**, con **12 recorridos de interfaz**, cuatro comprobaciones semánticas principales y nueve controles de restricciones/destinos. Se utilizaron Qwen real, SQLite sintético y Edge visible. Los resultados proceden de **tres sesiones grabadas con hashes idénticos de API y React**; no se presentan como una única pasada continua sin interrupciones.

## Comprobaciones automáticas

| Comprobación | Resultado |
|---|---:|
| Backend: orquestación, contratos, concurrencia, aprobación y cierre | 254/254 |
| Dominio existente | 150/150 |
| React, driver y actividad | 52/52 |
| Contratos de automatización y extensión | 9/9 |
| Compilación API y React | PASS |

Total: **465 pruebas automáticas aprobadas**. El cierre del dispatcher espera la terminación de sus monitores antes de liberar recursos; las pruebas de su fixture esperan ese cierre antes de limpiar SQLite. La compilación API termina sin advertencias. Se mantiene el aviso existente de Vite sobre el tamaño del paquete principal.

## Recorridos verificados

| Grupo | Resultado comprobado |
|---|---|
| A — crear y aprobar | Escritura por fragmentos, cursor sobre el control real y propuesta = formulario = versión guardada. También revisión en móvil y limpieza del borrador tras guardar. |
| B — adaptar y aprobar | Selección del origen tras una aclaración, original intacto antes de confirmar y nueva versión igual a la propuesta aprobada. |
| C — completar con recuperación | Pérdida deliberada de un recibo y recuperación sin repetir la escritura del campo. |
| C2 — completar sin editar | Datos completos y plantilla guardada exactamente conservada. |
| D — pausar, continuar y cancelar | Escritura detenida y reanudada; texto estable durante los 1200 ms observados después de cancelar, sin guardar un prompt nuevo. |
| E — consultar | Dos paráfrasis abren el prompt sin editarlo ni preguntar si se quiere modificar. |
| F — intervención manual | Título introducido por la persona y cuerpo preservados durante los 1500 ms observados; el agente se detiene y no guarda. El estado bloqueado de esa ejecución es el resultado esperado. |
| P — preparar Claude | Destino correcto, sin envío externo y sin modificar la plantilla guardada. |
| L — ejecutar Qwen local | Respuesta completa real y texto del diálogo igual al resultado registrado, sin modificar el prompt. |
| R — borrador REST | Plantilla generada igual al formulario, sin guardar; cancelación de la modificación y propuesta conservada tras restaurar la ventana de QA. |
| S — guardar y restaurar destino | Nueva versión igual a la aprobada y diálogo preparado otra vez para Claude después de guardar, sin enviar. |

Q comprueba las cuatro intenciones principales; N comprueba nueve separaciones entre contenido, datos, prohibiciones, preparación y ejecución. Los nombres de modelos dentro de títulos y las instrucciones citadas dentro de datos no conceden permisos.

Durante las tareas verificadas no se abrieron ventanas, pestañas ni procesos raíz adicionales de navegador. Cada sesión utiliza una única Page de QA. El cursor comprobado es el indicador visual del agente y las acciones son DOM verificadas; no se afirma movimiento físico del ratón del sistema.

## Regresión encontrada y corregida

La primera tanda expuso una contradicción del contrato: Qwen podía devolver `NO_CONTENT_REQUEST` junto con evidencia de edición no vacía. Esto convertía una consulta clara en una pregunta innecesaria sobre modificar el texto.

El esquema de la clasificación ahora ofrece ramas incompatibles: consulta/asesoría exige evidencia de autoría y transformación vacías; revisión/creación exige una cita literal no vacía de las directivas autorizadas. El tipo de petición se declara antes de su evidencia. La intención sigue siendo elegida por el modelo: no se añadieron condiciones para una frase concreta ni se autorizaron operaciones por defecto.

Dos pruebas de transporte en español e inglés comprueban las combinaciones válidas e inválidas. Las dos consultas se repitieron con Qwen real y pasaron. Se conserva el [fallo original](../evidence/same-window/acceptance-1792249882476/failures.json) y la [repetición aislada](../evidence/same-window/acceptance-1792250623310/acceptance.json).

## Sesiones y límites

- [Sesión 1](../evidence/same-window/acceptance-1792250749178/acceptance.json): se aceptan Q, A, B, C, C2 y D. Su informe completo sigue siendo FAIL: se canceló E después de que la ventana quedara oculta y NOVA se pausara por seguridad.
- [Sesión 2](../evidence/same-window/acceptance-1792251545625/acceptance.json): se aceptan E, F, P y L. Su informe completo sigue siendo FAIL: una comprobación añadida al observador suponía incorrectamente que minimizar Edge siempre cambia `document.visibilityState` a `hidden`. R no se acepta de esa sesión.
- [Sesión 3](../evidence/same-window/acceptance-1792251972141/acceptance.json): PASS para R, N y S. Comprueba la visibilidad que informa realmente el navegador. En esta ejecución siguió siendo visible al solicitar minimizar, por lo que la subprueba de pausa por ocultación consta como **NOT_APPLICABLE**, no como aprobada. La restauración visible y la conservación exacta de la propuesta sí se comprobaron.

El observador restaura únicamente su propia ventana y usa el botón real Continuar cuando detecta una pausa por ocultación sin intervención manual. No modifica `document.visibilityState` ni desactiva los controles de la aplicación. Los métodos de ventana utilizados pertenecen al [protocolo oficial CDP Browser](https://raw.githubusercontent.com/ChromeDevTools/devtools-protocol/master/pdl/domains/Browser.pdl).

La agregación acepta solo grupos que pasaron y compara los hashes de los binarios antes de reunirlos. Los intentos fallidos se conservan como tales. Las observaciones finitas de cancelación, permisos e interpretación no garantizan todas las peticiones posibles.

## Evidencia y uso

- [Resumen portable: resultados, hashes, sesiones, capturas y videos](SAME_WINDOW_VALIDATION_SUMMARY.json).
- [Informe agregado local](../evidence/same-window/acceptance-1792251972141/combined-validation.json).
- [Creación y vista previa](../evidence/same-window/acceptance-1792250749178/A-creation-original-tab.png).
- [Intervención manual](../evidence/same-window/acceptance-1792251545625/F-manual-intervention.png).
- [Versión guardada y destino restaurado](../evidence/same-window/acceptance-1792251972141/S-save-restores-requested-provider.png).
- [Grabación de R, N y S](../evidence/same-window/acceptance-1792251972141/video/page@b6eb18be80b317c2427f08966e64bc22.webm). Las otras dos grabaciones están enlazadas en el resumen.

Las instancias locales de los puertos **5190** y **5192** quedaron actualizadas con los mismos cuatro ensamblados que se probaron, comprobados por SHA-256. Ambas responden correctamente a `/health`. La biblioteca personal no se utilizó como fixture. [Comprobación de la instancia local](../evidence/same-window/acceptance-1792251972141/local-runtime.json).

Recarga <http://127.0.0.1:5190/> para usar el sistema. Para reproducir la aceptación completa en Windows con las herramientas ya instaladas: `.\scripts\Test-SameWindow.ps1`. El modo visible es el predeterminado; `-HeadlessQa` permite QA grabado sin ventana. La prueba no instala paquetes ni modelos y se detiene ante el primer fallo. Consulta la [guía de uso](../COMO_USAR_QUICKPROMPT.md) y los [límites del runtime](SAME_WINDOW_RUNTIME.md).
