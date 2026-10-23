# Cierre — autoría conserva su operación; identidad no la sustituye

2026-10-20. Traza real `latency-1792495341569` conserva la primera divergencia:
ReviewAuthoringScope propone PROPOSE_ADAPTATION y REQUEST_SAVE_COPY; TextGoal
devuelve REVISION_REQUEST. Después AdditionalArtifactRequested recibe el encargo
completo, donde guardar copia es identidad adicional, y el coordinador sustituye
la revisión por NEW_PROMPT_FROM_SOURCE. El generador de creación escribe las
órdenes de adaptar/guardar en el cuerpo; su guardia lo bloquea. No es un cambio
a solicitar al usuario ni una ausencia de fuente.

Propietarios conservados: InstructionUse define la operación de contenido;
TaskSourceResolver liga identidad de origen; workflow y LiveRuns gobiernan nueva
identidad persistida/revisión/aprobación. La misma decisión de revisión validada
no debe sustituirse por el juicio de otro propietario.

```text
interpretar uso de contenido y validar su evidencia
si ya es revisión -> conservar revisión y operar sobre el origen resuelto
si declara contenido nuevo basado en origen -> conservar distinción de identidad
workflow mantiene copia/versión/controles independientes
validar y ejecutar con los propietarios de origen y aprobación existentes
```

No hay routing por frases, otra capa, juez, estado, permisos, modelo o esquema
nuevo. Se elimina una llamada redundante para revisiones; no se declara mejora
de latencia global. Las creaciones independientes y las nuevas desde origen
conservan su ruta y sus validaciones; no se fuerzan como revisión.
Alternativas: enseñar más frases o añadir otra revisión mantiene el conflicto
de propietarios. Se modifica sólo el coordinador que permitía esa sustitución.
Variante 1, máximo dos con nueva evidencia. Antes: cuatro contratos fallan porque
se consulta otra vez identidad tras validar revisión; cinco controles pasan.
Verificación: uso de instrucción, workflow, nuevas identidades/orígenes y UI del
mismo guardado de copia. No se cambian tarea/fixture/oráculo.

## Resultado

90 contratos dirigidos PASS. Qwen/UI conserva PROPOSE_ADAPTATION y
REQUEST_SAVE_COPY sobre el ID/revisión reales; ya no transforma la revisión en
creación. El editor retira órdenes de guardar del cuerpo en su corrección, pero
el revisor exige esas órdenes de sesión dentro del correo de entrevista y lo
rechaza. Tarea completa FAIL conservada en `latency-1792495675500`. Se mantiene
la conservación de propiedad; no certifica fidelidad, ahorro global ni SAV-02.
La omisión de contexto de workflow en el revisor se documenta en wave9.
