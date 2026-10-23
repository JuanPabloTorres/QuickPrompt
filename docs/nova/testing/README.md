# Selección de pruebas por capacidad

Tres matrices separan [backend](BACKEND_TEST_MATRIX.md), [frontend](FRONTEND_TEST_MATRIX.md) y [agente](AGENT_TEST_MATRIX.md). La [matriz de cierre](../VERSION_CLOSURE_MATRIX.md) sigue siendo la única autoridad sobre los 88 requisitos. Estas vistas no cambian criterios ni cierran el release.

1. Identificar el propietario y el bloque modificado; seleccionar sólo sus contratos y consumidores directos.
2. Conservar fuente/configuración/modelo/fixtures del candidato. Compilar el consumidor afectado si cambió su código; no usar un binario anterior para validar fuentes nuevas.
3. Ejecutar la selección de esa matriz. Los tests de transporte de interpretación pertenecen a **agente**, aunque se ejecuten mediante `dotnet test`.
4. Para agente, distinguir contratos controlados, Qwen real de componente y UI real. Revisar fidelidad externamente contra la petición original; ni un autoveredicto del modelo ni igualdad entre formulario y propuesta son suficientes.
5. Si cambia un contrato compartido, ejecutar sólo la dependencia indicada y documentar el motivo. La campaña integral y las suites completas corresponden al candidato final de release, no a cada experimento semántico.
6. Fallo de creación significa que su continuación todavía no fue probada; no atribuirle una aprobación o guardado no alcanzado. Mantener desarrollo, validación y holdout separados. No ajustar casos/umbrales para lograr verde.

Validar trazabilidad de un bloque, sin lanzar otras matrices:

```powershell
node scripts/Check-TestMatrices.cjs --area agent --block authoring
```

Regenerar la vista de una categoría:

```powershell
node scripts/Check-TestMatrices.cjs --area agent --write-report
```

El validador comprueba manifiestos, referencias y evidencia existente. Su código 0 significa **manifiesto válido**, no agente funcional. El arnés UI usa `Tests/nova/acceptance.cjs`: código 0 sólo para todos los casos esperados verificados, 2 para FAIL y 3 para NOT_TESTED. El arnés conserva `controlCorrect` y exige revisión semántica independiente cuando se genera contenido. Las capturas de copia del navegador no prueban el portapapeles nativo.

Una revisión externa se conserva en `independent-content-review.json`, con atribución, `humanReview`, índice/escenario y SHA256 exacto de `measurements.json`. La revisión de Codex se identifica como tal, sin presentarla como validación humana. Para componer el gate después de revisar:

```powershell
node scripts/Review-AgentEvidence.cjs docs/evidence/latency-1792475525468
```

Ese ejemplo **ya fue ejecutado y produjo FAIL** por contenido; no se vuelve a escribir su `acceptance.json`. El gate rechaza evidencia de otra campaña, no permite que una revisión positiva supere un control UI fallido y conserva los resultados originales. No decide intención ni forma parte del runtime del agente.

En el incremento de autoría v4 se ejecutaron `agent/authoring` y su dependencia `agent/fields-copy`. La continuación de recuperación del revisor selecciona `agent/authoring` y los consumidores directos indicados en su filtro; no repite los recorridos UI anteriores. Backend/frontend generales conservan sus resultados históricos; no se presentan como comprobados otra vez.

El contraste más reciente invoca el revisor **real de creación**, con cuerpos ya fijados para aislarlo. A diferencia de las regresiones con respuestas controladas, sí usa Qwen local; a diferencia de una campaña UI, no genera ni guarda contenido. Su acta está en [recuperación independiente del revisor](../../evidence/closure-20261020/fidelity-independent-repair/review.json). Mantiene FAIL por dos aceptaciones indebidas, pese a pasar los controles de recuperación.

Para reproducir tras un cambio justificado, compilando antes el evaluador correspondiente y eligiendo una ruta de salida nueva:

```powershell
dotnet .playwright/closure-20261020/fidelity-group-repair-evaluator-final/QuickPrompt.Evaluation.dll --authoring-review Tests/nova/datasets/authoring-authority-v1/development.json docs/evidence/closure-20261020/NUEVA_CAMPAÑA.json production
```

El perfil debe ser explícito. No se reutilizan ni sobrescriben los reportes fallidos, no se prueban semillas hasta obtener verde y no se abre validación/holdout antes de calificar desarrollo. Una coincidencia de etiqueta con un motivo contrario a la petición se conserva como fallo de diagnóstico.
