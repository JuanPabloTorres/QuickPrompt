# QuickPrompt — uso real antes de ampliar funciones

Estado: **NOT_TESTED** por personas. Este documento prepara el piloto; no contiene resultados ni modifica los criterios o datasets de GOAL05.

La visión recibida nuevamente el 16 de octubre de 2026 coincide byte por byte con [PRODUCT_DIRECTION.md](PRODUCT_DIRECTION.md). QuickPrompt Simple ya tiene [aceptación local automatizada](GOAL05_ACCEPTANCE.md). El siguiente paso es comprobar su utilidad con tareas reales antes de priorizar panel lateral, proveedores o más IA.

## Empezar hoy

Abre **http://127.0.0.1:5188**. Elige una tarea que realmente necesites realizar, por ejemplo un mensaje, una explicación o una planificación. Usa tu biblioteca habitual; no es necesario compartir su contenido ni exportarla.

1. Encuentra un prompt guardado, completa sus campos y copia el resultado. Si no existe, créalo con título y texto, parametrizando únicamente lo que cambiará la próxima vez.
2. Utiliza el resultado donde trabajas normalmente. Para un sitio externo, copiar/abrir requiere pegar y enviar manualmente; no presupone una integración directa validada.
3. Reutiliza la misma plantilla con otros valores cuando vuelva a surgir la necesidad. Comprueba que el resultado sea correcto y que el original permanezca intacto.
4. Anota cualquier punto donde tengas que detenerte, repetir un paso o averiguar qué hacer. Registra también si lograste terminar, aunque haya habido fricción.

No hace falta usar Qwen para este recorrido. Su evaluación es separada del valor de encontrar, completar y copiar.

## Registro mínimo

Una fila por intento. Usa una descripción anónima del objetivo, sin textos privados, respuestas, nombres ni credenciales. No registrar un dato equivale a **no medido**, nunca a cero.

| Fecha / intento | Objetivo anónimo | Resultado esperado | ¿Terminó sin ayuda? | Acciones / cambios de sección | Tiempo observado | Fricción y pasos para repetirla |
|---|---|---|---|---|---|---|
| Pendiente | | | | | | |

Cuenta clics, rellenos de campos, selecciones y confirmaciones. Anota aparte la escritura/lectura humana si afecta al tiempo. Para un defecto de copia, describe qué difiere: variables pendientes, saltos de línea, duplicación o instrucciones inesperadas. Basta un ejemplo sintético equivalente para reproducirlo sin revelar el contenido real.

Una corrección debe partir del fallo reproducible, conservar el resultado esperado y volver a probar el recorrido completo. Después se añade una regresión relevante. No ampliar la aplicación para resolver una dificultad que todavía no se observó.

## Piloto con cinco personas

Preparar una biblioteca de prueba separada de los datos personales, con tareas familiares para los participantes. El facilitador puede crear los ejemplos antes de medir; no debe explicar la navegación o los controles durante el intento. La preparación no es parte de la medición. No usar ni restaurar bases privadas para preparar el piloto.

Proponer, en este orden, sin indicar qué botones pulsar:

1. «Encuentra el texto guardado para esta tarea y déjalo listo para pegar». Usar una plantilla sin variables.
2. «Adapta el mensaje guardado a este destinatario y motivo, y déjalo listo para pegar». Usar dos variables con valores dados.
3. «Guarda este texto para poder reutilizarlo y úsalo una vez». Dar título y texto, sin exigir categoría ni grupo.

Cronometrar desde que la persona termina de leer la consigna hasta verificar el texto copiado. Si pide ayuda, registrar cuándo y qué ayuda recibió; no contar ese intento como completado sin ayuda. Mantener los intentos fallidos y los que excedan la meta. Presentar todos los tiempos individuales; cinco personas no prueban usabilidad general.

Las metas propuestas en la visión son **menos de 10 segundos** para encontrar/copiar sin variables, **menos de 20 segundos** con dos variables y **al menos cuatro de cinco personas** completando cada uno de esos flujos sin ayuda. Registrar los dos flujos por separado. La creación debe seguir requiriendo solo título y texto. Estos objetivos permanecen pendientes; los milisegundos del harness no cuentan como resultados humanos.

## Decidir la siguiente versión

- Corregir primero copia incorrecta, pérdida de cambios, controles inaccesibles o envíos no solicitados.
- Priorizar después dificultades repetidas para encontrar, completar o reutilizar.
- Evaluar el panel lateral cuando las tareas observadas muestren que cambiar de pestaña interrumpe el trabajo. Su aceptación debe incluir la misma biblioteca local y copia exacta, sin automatizar autenticación ni conversaciones.
- Activar APIs o destinos nuevos únicamente con alcance y configuración explícitos. Su ausencia no debe impedir el uso básico.

No se han reclutado participantes, cronometrado personas ni validado sitios externos auténticos en esta preparación. [Alineación actual y pendientes](VISION_ALIGNMENT.md).
