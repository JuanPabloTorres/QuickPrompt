# Cierre — continuidad de decisiones entre autoría y workflow

2026-10-20. Hipótesis distinta de la guía de objetos rechazada: no añadir
metadatos de ciclo de vida ni otro juicio, sino conservar la decisión operativa
ya validada por el propietario actual InstructionUse. En el caso 14 la evidencia
registra PROPOSE_ADAPTATION, la declaración de contenido es revisión y después
el workflow agrega REQUEST_SAVE_COPY. Su paquete actual incluye destino y datos
interpretados pero omite la decisión de autoría en el perfil predeterminado.

Propietarios existentes: InstructionUseResult produce Request tras su validación
y reparación acotada; PreparationInference.Interpret compone el paquete de
workflow; RequestedWorkflow sigue decidiendo acceso/control/guardado.

```text
interpretar y validar uso de instrucción con presupuesto existente
proyectar RequestType y ContinuesPendingTask del resultado final validado
añadir ese hecho tipado al mismo paquete de workflow, sin cuerpo ni valores
workflow interpreta sólo sus objetivos independientes desde la petición original
no filtrar permisos por una lista de capacidades inferida ni borrar guardados
validar y compilar plan con los propietarios actuales
```

Invariantes: ninguna llamada, estado, árbitro, API, fuente de autoridad o permiso
nuevo. No se restringe el enum de guardados para forzar el caso 14. Una petición
compuesta conserva guardados explícitos y el usuario sigue aprobando escrituras.
No se propaga el texto generado como instrucción; sólo la clasificación y la
continuación ya comprobadas. Datos/títulos no son autorizaciones.

Alternativas: otro revisor añade latencia y duplica autoridad; convertir todas
las operaciones de ReviewAuthoringScope en techo del plan omitiría efectos
legítimos (ya se observaron declaraciones incompletas en otros casos). Se
extiende la composición actual, sin abstracción compartida o módulo nuevo.

Verificación: test de paquete ausente antes, presencia después y aislamiento
de datos/cuerpos; límites de autoría/campos/workflow y guardado solicitado;
desarrollo original Qwen con umbrales intactos antes de validation/holdout.
Variante 1, máximo dos sólo con nueva evidencia. Se rechaza si pierde metas
legítimas o conserva efectos extra. Rollback: retirar sólo la proyección y su
guía, sin migración ni cambio de modelo/configuración productiva.

## Rechazo ejecutado

46 contratos controlados PASS. Qwen real del caso original 14 sigue fallando: la
declaración de autoría es REVISION_REQUEST, pero el workflow cambia a
REVIEW_PERSIST_EXISTING y agrega REQUEST_SAVE_VERSION. Es peor que la copia
extra y contradice conservar el original. Se retiraron la proyección y guía de
producto y las aserciones exclusivas del experimento; fuentes/binarios/TRX
rechazados quedan archivados con sus hashes. No se ejecuta toda la matriz para
un candidato ya rechazado ni otra repetición sin nueva evidencia. No se atribuye
a este resultado mejora semántica, rapidez o cierre de requisitos.
