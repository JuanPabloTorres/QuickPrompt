# Cierre — aprobación de borradores entre turnos y copia sin Goal opcional

2026-10-20. Evidencia real retenida: `latency-1792494258858` crea y adapta,
pero la tercera petición REQUEST_SAVE_COPY queda BLOCKED: falta un borrador
generado en ese mismo run. Ambos completar/copiar originales pasan.

Propietarios: AgentSessions conserva y valida DraftRef; AgentOrchestrator
restaura/observa; LiveRuns crea propuestas y consume aprobación; PromptLibrary
persiste. Sin nuevo almacenamiento, árbitro ni llamadas al modelo.

```text
si se pide guardado y el estado visible cumple sus postcondiciones:
  registrar observación y revalidar checkpoint con AgentSessions
  LiveRuns admite borrador previo sólo si coincide ID/sesión/revisión/texto
  mantener GeneratedDraft como candidato de revisión dentro de este run
  clasificar copia/version desde Goal cuando existe, o del plan ya validado
  guardar en la propuesta la identidad/revisión del origen de una copia
  rechazar cambios de origen antes de aprobar y antes de commit
  tras aprobación exacta, ejecutar UI con revisión esperada de esa propuesta
  verificar SQLite y estado visible; cancelar/replay no repiten escritura
```

La política no acepta un borrador arbitrario desde HTTP. El DraftRef es argumento
C# interno y se revalida justo antes de revisar. SourceBasis es interno/JsonIgnore;
no migra SQLite ni cambia JSON público. Un perfil opcional no puede decidir si
el efecto copia significa actualizar el original. RequestsApprovalFor conserva
la autoridad de Goal cuando está presente y usa el plan validado cuando falta.

Alternativas: regenerar contenido añade deriva y latencia; otro almacén duplica
identidad/aprobación. Se amplían los propietarios actuales. Variante 1.
Pruebas previas: cuatro fallos reproducidos (borrador previo con/sin Goal, copia
sin Goal y cambio de origen sin Goal), cinco controles existentes PASS.
Verificar negativos de referencia/cambio de formulario y consumidores de revisión;
repetir UI real sólo después, sin alterar tareas u oráculos.
Rollback: retirar transferencia de checkpoint y metadatos internos, sin migración
de datos/modelo/API. No certifica interpretación ni calidad de autoría general.

## Resultado de la variante 1

97 contratos dirigidos PASS, API/evaluador sin avisos/errores y tres ensamblados
compartidos idénticos. Los seis negativos añadidos primero fallaron en el setup
por motivo vacío del fixture; se corrigió sólo su precondición, conservando TRX.

Qwen/UI `latency-1792494774943`: crear → adaptar → pedir aprobación → guardar
copia temporal COMPLETED, un INSERT, revisión 1, originales intactos y texto
exacto. Revisión de contenido independiente Codex, no humana, PASS para este
encargo; incluye priorización complementaria y redacción mejorable, sin certificar
exactitud general de adaptaciones. 193784 ms, 24 llamadas: no mejora de rapidez
aceptada. Otro guardado desde origen falla UNGROUNDED_CONTENT_CLAUSE antes de
revisión; tanda combinada FAIL conservada, SAV-02 sigue abierto.

Se detectó que el arnés exigía revisión sólo si el último turno era de autoría.
El helper de aceptación ahora inspecciona todos los turnos; productor, revisor y
reapertura comparten esa regla. Prueba antes 1 FAIL / 6 PASS, después 7 PASS.
La evidencia cruda anterior no se modifica; la revisión externa deriva esa
obligación de los turnos retenidos y no confía en su flag incompleto.

Reapertura: respaldo SQLite real de esa base QA, mismo hash, biblioteca/título/
cuerpo/revisión exactos y rechazo 409 al replay. Rollback al build wave4 anterior
con hashes comprobados, mismas opciones y copia independiente del respaldo,
también PASS. Ningún servicio o DB del usuario cambió. SAV-01 y DAT-06 cerrados
en el alcance probado; se revalidan junto al candidato final.

Evidencia: `docs/evidence/closure-goal-20261020/wave6/manifest.json`,
`docs/evidence/latency-1792494774943/acceptance.json`,
`restart-unsavedSaveCopy/results.json` y `rollback-unsavedSaveCopy/results.json`.
