# Cierre — contexto operativo de las decisiones semánticas

2026-10-20. Se conserva el propietario `PreparationInference` y la fuente
`SemanticSystemKnowledge`; no se incorpora un árbitro ni se activan contratos
rechazados, otro modelo o retrieval como autoridad.

Hipótesis de referencia, variante 1: los roles de selección omitían la definición
real de la entidad. Prueba de entrada antes FAIL; 27 contratos después PASS.
Dos casos retenidos con Qwen ahora eligen `CURRENT_UI_INSTRUCTION` y el ID
correcto. El segundo aún falla la aceptación por un efecto extra de guardado de
copia. Se conserva el FAIL; corregir referencia no equivale a completar la tarea.

Hipótesis de workflow, variante 1: el catálogo de objetivos no enseña los dos
objetos reales ni su ciclo de vida. Preparar/editar un borrador deja el registro
persistido intacto; conservarlo no requiere solicitar persistencia. La guía debe
derivarse de las declaraciones de `PromptDocument` y `PreparedDraft`, no de
ejemplos de peticiones ni de otro juicio LLM. Se enviará como conocimiento
estructural, separado del trabajo solicitado y de los datos de usuario.

```text
describir los tipos reales con metadatos de responsabilidad/ciclo de vida
SemanticSystemKnowledge refleja sólo esos tipos de confianza, sin instancias
workflow recibe el recurso JSON y la petición original en campos distintos
modelo declara sus objetivos; dominio conserva validación y aprobación
evaluar casos retenidos y luego desarrollo completo si cumplen el techo de efectos
como máximo dos variantes justificadas; conservar todo fallo, sin tocar holdout
```

Invariantes: sin permisos derivados de la guía, cambios de esquema SQLite/API,
llamadas adicionales, sobreescritura de originales ni persistencia por preparar.
Extender el recurso existente es menor que duplicar objetos/políticas en otro
servicio. Los metadatos de tipos son la fuente única de sus descripciones.

Variante 1 de workflow ejecutada: el recurso de objetos por sí solo no elimina
`REQUEST_SAVE_COPY` en el caso retenido; FAIL conservado. Variante 2: el mismo
propietario deja de forzar `think=false` y respeta la opción existente
`SemanticThinking` y la capacidad del modelo mediante `TaskThinking(model)`.
Presupuesto de salida acotado de 2000 tokens en ese modo; sin llamadas ni
intentos adicionales. Se medirá su coste; no se declara mejora de rapidez.
No habrá una tercera variante de esta hipótesis si falla.

Resultado: variante 2 también FAIL; sustituye el efecto extra por
`REQUEST_SAVE_VERSION`, contrario a conservar el original. Ambas variantes de
objetos/razonamiento se archivaron y retiraron del código de producto; se conserva
el perfil de workflow anterior. No se promueve el recurso por pasar sus tests de
metadatos. Estado: hipótesis agotada, sin tercera ejecución ni holdout.

La mejora de datos de referencia sí conserva su evidencia acotada: ambos casos
identifican el origen correcto, pero no acredita el workflow completo. El
siguiente cambio corresponde a otra divergencia observada: `CATALOG_ENTRY_1`
correcto se convierte en consulta textual y el siguiente selector pierde esa
identidad. Propietario: `TaskSourceResolver`, con lectura fresca y protección de
duplicados/revisión; no otro árbitro ni ajuste de instrucciones por frase.
