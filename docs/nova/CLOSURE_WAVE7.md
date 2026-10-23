# Cierre — reparación de restricciones sin repetir la propuesta rechazada

2026-10-20. Origen observado: `latency-1792494774943`, saveCopy. La extracción
entrega varias órdenes de sesión como cláusulas literales y una cita inventada
con errata. La validación detecta UNGROUNDED_CONTENT_CLAUSE, pero el siguiente
paquete contiene rejectedConstraints y el modelo reproduce exactamente el mismo
array inválido. El diagnóstico preserva ambos intentos.

Propietario: PreparationInference.ExtractGroundedConstraints; mismo máximo de
dos extracciones y mismo deadline de 90 s. Se aplica el principio ya utilizado
en recuperación de texto: diagnóstico fuera del contexto de autoridad.

```text
extraer y validar con petición original, contexto previo y política save fijada
si válida -> devolver al propietario del plan
si inválida -> conservar respuesta/código en diagnóstico y desalojar caché
si queda intento -> reconstruir desde autoridad original, código y requisito
no reenviar la propuesta inválida como contexto, datos de usuario o restricción
si vuelve a fallar -> propagar fallo con presupuesto agotado; no ejecutar efectos
```

Sin llamadas adicionales, cambios en formatos/identidades/plan/permisos ni reglas
por frase. PreserveOriginal sigue fijado por el workflow existente. No se borra
el fallo ni se relaja grounding para aceptar una cita parecida. No hay migración.
Alternativas: corregir palabras concretas sería parche; otro revisor duplica
autoridad/latencia. Se modifica sólo el feedback del propietario actual.
Variante 1, máximo dos con nueva evidencia; no se ejecutará desarrollo completo
para volver a comprobar un fallo de workflow inalterado. Validación dirigida
a restricciones/recuperación y al recorrido UI de copia desde origen.

## Resultado ejecutado

19 contratos PASS. Qwen real deja de repetir la cita inventada: la reparación
devuelve restricciones válidas y el recorrido entra en UI. Tarea completa FAIL:
una decisión posterior sustituye REVISION_REQUEST por creación, cuyo cuerpo
contiene órdenes de sesión. El guardia rechaza ese cuerpo; no hay guardado ni
éxito declarado. Se conserva la corrección acotada del paquete y el nuevo fallo
con su primera divergencia en CLOSURE_WAVE8.md; no se cierra SAV-02 ni autoría.
