# Matriz de pruebas: agent

Interpretation through actual requested effects and independently checked content with the local model. Transport tests belong here when they protect an agent boundary.

A scoped matrix does not close the release. Run only changed capabilities and their explicit dependencies; use the same frozen candidate. Historical, mocked, component and real-model evidence are distinct.

Este documento valida trazabilidad; no ejecuta las pruebas ni convierte evidencia histórica en aceptación actual.

| Bloque | Estado de evidencia | Requisitos canónicos | Tipo |
|---|---|---|---|
| authoring | FAIL | AI-01, AI-07, AUT-01, AUT-02, AUT-03, AUT-07, AUT-08 | controlled_contract_plus_real_model_ui |
| fields-copy | FAIL | CTX-02, CTX-03, CTX-06, AI-05, AI-07 | real_model_same_tab |
| source-navigation | FAIL | AI-02, AI-03, AI-04, CTX-07 | real_model_same_tab |
| continuity | FAIL | CTX-04, CTX-05, CTX-07, AUT-04, AUT-05 | contract_plus_real_model_multiturn |
| approval-save | FAIL | SAV-01, SAV-02, SAV-03, AUT-06 | real_model_ui_sqlite_restart |
| execution-control | FAIL | EXE-02, EXE-03, EXE-04, EXE-05, EXE-06, EXE-07, EXT-01 | real_model_ui_effects |
| deletion | PASS | SAV-04 | real_model_ui_sqlite |
| latency | NOT_TESTED | PER-01, PER-02, PER-03, REL-05, REL-06 | controlled_quality_and_latency |
| reflected-tools | PASS | AI-01, AI-02, REL-01 | controlled_contract_and_real_local_model_read_component |
| ui-agent-resources | PARTIAL | AI-01, AI-02, REL-01 | controlled_boundaries_and_scoped_real_model_ui_with_separate_entry_points |
| semantic-core-replacement | FAIL | AI-01, AUT-01, REL-05 | unqualified_prototype_real_model_application_planning_plus_controlled_boundaries |
| typed-agent-context | PARTIAL | AI-01, AI-02, REL-01 | strict_typed_json_reflected_metadata_plus_scoped_real_model_and_ui |

## authoring

**Propietarios:** `backend/QuickPrompt.Infrastructure/SemanticIntentInterpreter.InstructionUse.cs`, `backend/QuickPrompt.Infrastructure/SemanticIntentInterpreter.cs`, `backend/QuickPrompt.Infrastructure/DraftFidelityReviewer.cs`, `backend/QuickPrompt.Application/TaskGoal.cs`, `backend/QuickPrompt.Infrastructure/PromptDraftGenerator.cs`, `backend/QuickPrompt.Infrastructure/PromptDraftGenerator.Authoring.cs`

**Selección:** `{"contractFilter":"FullyQualifiedName~AuthoringDecisionContractTests|FullyQualifiedName~InstructionUseBoundaryTests","uiScenarios":["create","unsavedAdapt"],"development":"Tests/nova/datasets/recovery-v1/development.json","validationAndHoldout":"Frozen; do not inspect/tune before development qualification.","reviewerDevelopment":"Tests/nova/datasets/authoring-authority-v1/development.json","reviewerRepairFilter":"FullyQualifiedName~FidelityRepairContractTests|FullyQualifiedName~AuthoringDecisionContractTests|FullyQualifiedName~EditorConstraintTests|FullyQualifiedName~ProtectedCompositionTests|FullyQualifiedName~TemplateSourceBindingTests|FullyQualifiedName~EditorWorkflowBoundaryTests|FullyQualifiedName~NovaProposalGoalTests","authorityBindingFilter":"FullyQualifiedName~ReviewAuthorityBindingTests","inputDeclarationFilter":"FullyQualifiedName~PromptInputDeclarationTests"}`

**Aceptación:** No session orders in body; faithful original task/title/language/inputs/constraints; exact content in editor/preview/save. Independent content review required; process/model COMPLETED is insufficient. Ownership migration: same controlled semantic algorithms now in independent SemanticIntentInterpreter, compatibility facade delegates. No new real-model/UI qualification or status improvement inferred. Canonicalization must not author body/inputs from metadata. Current real creation remains FAIL; deterministic contract tests are not semantic qualification.

**Evidencia:** [template-representation-v3-contracts.trx](../../../docs/evidence/closure-20261020/tests/template-representation-v3-contracts.trx), [measurements.json](../../../docs/evidence/latency-1792474435293/measurements.json), [AGENT_RELIABILITY_ROOT_CAUSE.md](../../../docs/nova/AGENT_RELIABILITY_ROOT_CAUSE.md), [authoring-literal-content-v4-request-limit.trx](../../../docs/evidence/closure-20261020/tests/authoring-literal-content-v4-request-limit.trx), [authoring-identity-candidate-v4.json](../../../docs/evidence/closure-20261020/authoring-identity-candidate-v4.json), [acceptance.json](../../../docs/evidence/latency-1792475525468/acceptance.json), [authoring-capability-boundary-review.json](../../../docs/evidence/closure-20261020/authoring-capability-boundary-review.json), [shared-authoring-development.json](../../../docs/evidence/closure-20261020/shared-authoring-development.json), [authoring-zero-input-and-owner-compatibility.trx](../../../docs/evidence/closure-20261020/tests/authoring-zero-input-and-owner-compatibility.trx), [review.json](../../../docs/evidence/closure-20261020/fidelity-independent-repair/review.json), [fidelity-independent-repair-locked-groups.trx](../../../docs/evidence/closure-20261020/tests/fidelity-independent-repair-locked-groups.trx), [actual-creation-review-production.json](../../../docs/evidence/closure-20261020/actual-creation-review-production.json), [actual-creation-review-native-sampling.json](../../../docs/evidence/closure-20261020/actual-creation-review-native-sampling.json), [actual-creation-review-partial-repair-production.json](../../../docs/evidence/closure-20261020/actual-creation-review-partial-repair-production.json), [FIDELITY_REPAIR_CONTRACT.md](../../../docs/nova/FIDELITY_REPAIR_CONTRACT.md), [review.json](../../../docs/evidence/closure-20261020/review-authority-binding/review.json), [review-authority-before.trx](../../../docs/evidence/closure-20261020/tests/review-authority-before.trx), [review-authority-after.trx](../../../docs/evidence/closure-20261020/tests/review-authority-after.trx), [native-content-wire-development.json](../../../docs/evidence/closure-20261020/native-content-wire-development.json), [native-content-protocol-review.json](../../../docs/evidence/closure-20261020/native-content-protocol-review.json), [REVIEW_AUTHORITY_BINDING.md](../../../docs/nova/REVIEW_AUTHORITY_BINDING.md), [NATIVE_CONTENT_PROTOCOL_DIAGNOSTIC.md](../../../docs/nova/NATIVE_CONTENT_PROTOCOL_DIAGNOSTIC.md), [system-knowledge-content-development.json](../../../docs/evidence/closure-20261020/system-knowledge-content-development.json), [system-knowledge-review-development.json](../../../docs/evidence/closure-20261020/system-knowledge-review-development.json), [SEMANTIC_SYSTEM_KNOWLEDGE.md](../../../docs/nova/SEMANTIC_SYSTEM_KNOWLEDGE.md), [acceptance.json](../../../docs/evidence/latency-1792487405488/acceptance.json), [RELEASE_VALIDATION_20261020.md](../../../docs/nova/RELEASE_VALIDATION_20261020.md), [content-owner-v2-review.json](../../../docs/evidence/core-replacement-20261020/content-owner-v2-review.json), [v1-review.json](../../../docs/evidence/role-context-20261020/v1-review.json), [ROLE_CONTEXT_REFACTOR_20261020.md](../../../docs/nova/ROLE_CONTEXT_REFACTOR_20261020.md), [verification.json](../../../docs/evidence/fidelity-review-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/fidelity-review-module-20261020/compiled-provenance.json), [verification.json](../../../docs/evidence/draft-generation-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/draft-generation-module-20261020/compiled-provenance.json), [verification.json](../../../docs/evidence/semantic-interpreter-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/semantic-interpreter-module-20261020/compiled-provenance.json), [AGENT_CLOSURE_NAVIGATION_20261020.md](../../../docs/nova/AGENT_CLOSURE_NAVIGATION_20261020.md), [audit-summary.json](../../../docs/evidence/agent-input-authority-20261020/audit-summary.json), [retained-source-freeze.json](../../../docs/evidence/agent-input-authority-20261020/retained-source-freeze.json), [input-authority.trx](../../../docs/evidence/agent-input-authority-20261020/tests/input-authority.trx)

**Criterios originales:**

- AI-01: Superar development→validation→holdout congelados con perfil final; intención ≥90%, sin rutas por keywords/regex de intención.
- AI-07: Cero finalizaciones prematuras y efectos adicionales en campaña final; éxito se compara con petición original, no sólo plan del modelo.
- AUT-01: Prompt útil y ejecutable con tarea futura correcta; cero controles actuales de crear/navegar/guardar/no ejecutar ahora en el cuerpo.
- AUT-02: Cumplir metadatos literales y requisitos solicitados sin entradas, formatos o condiciones inventadas; comprobar ES/EN y Unicode.
- AUT-03: Conservar prohibiciones y aprobación futuras reales; excluir sólo controles actuales, incluso dentro de una misma oración.
- AUT-07: Validar positivos y negativos independientes; no usar el mismo juicio LLM como única prueba de fidelidad; medir falsas aceptaciones/rechazos. Un error técnico/de contrato nunca cuenta como acierto semántico.
- AUT-08: Una reparación conserva autoridad, destino, requisitos y efectos; agotamiento termina claro, sin bucles ni éxito parcial oculto.

## fields-copy

**Propietarios:** `backend/QuickPrompt.Application/UserInputScope.cs`, `backend/QuickPrompt.Infrastructure/PromptInputScopeInterpreter.cs`, `backend/QuickPrompt.Infrastructure/SemanticIntentInterpreter.InstructionUse.cs`, `backend/QuickPrompt.Infrastructure/PromptInputValueExtractor.cs`

**Selección:** `{"uiScenarios":["variables","boundVariables"],"contractFilter":"FullyQualifiedName~InputScopeTransportTests|FullyQualifiedName~AuthoringDecisionContractTests","runtime":"QA only: Qwen3:8b real; AuthoringUseContract=false, TaskGoals=false, ExecuteGroundedPlan=false, StopAfterVerifiedResult=false. Production defaults unchanged."}`

**Aceptación:** Original ID/revision/template unchanged, literal authorized values in real fields, deterministic compiled text, copy payload exact, no extra authoring/destination/write. Native OS clipboard remains VAR-04, not proved by browser capture. Modular increment: independent extractor is explicitly injected in orchestration and shared with legacy authoring/scope. 582 compiled controlled tests include generator without extraction, exact compiled output and rejection of invented values. No new Qwen/browser/OS clipboard acceptance; original PASS retains its original limited scope. Scope ownership increment: explicit provider in planner/source resolver; former partial retired and shared context projection preserved. 662 compiled controlled checks include meaning-only interpreter, ID/revision binding and rejection of source changes during extraction. No new model/browser/clipboard acceptance; functional PASS does not qualify timing anomaly retained separately. Ownership migration: same controlled semantic algorithms now in independent SemanticIntentInterpreter, compatibility facade delegates. No new real-model/UI qualification or status improvement inferred. Current retained audit: one original formulation passes and the separate-line original fails before field filling; historical two-case PASS does not close this candidate.

**Evidencia:** [measurements.json](../../../docs/evidence/latency-1792474435293/measurements.json), [measurements.json](../../../docs/evidence/latency-1792475525468/measurements.json), [typed-json-v3-freeze.json](../../../docs/evidence/closure-20261020/typed-json-v3-freeze.json), [measurements.json](../../../docs/evidence/latency-1792486471301/measurements.json), [validation.json](../../../docs/evidence/latency-1792486471301/validation.json), [INSTRUCTION_USE_GROUNDING.md](../../../docs/nova/INSTRUCTION_USE_GROUNDING.md), [acceptance.json](../../../docs/evidence/latency-1792487405488/acceptance.json), [RELEASE_VALIDATION_20261020.md](../../../docs/nova/RELEASE_VALIDATION_20261020.md), [acceptance.json](../../../docs/evidence/latency-1792494258858/acceptance.json), [INPUT_EXTRACTION_MODULE_20261020.md](../../../docs/nova/INPUT_EXTRACTION_MODULE_20261020.md), [verification.json](../../../docs/evidence/input-extraction-module-20261020/verification.json), [inputs-final.trx](../../../docs/evidence/input-extraction-module-20261020/tests/inputs-final.trx), [INPUT_SCOPE_MODULE_20261020.md](../../../docs/nova/INPUT_SCOPE_MODULE_20261020.md), [verification.json](../../../docs/evidence/input-scope-module-20261020/verification.json), [scope-final.trx](../../../docs/evidence/input-scope-module-20261020/tests/scope-final.trx), [verification.json](../../../docs/evidence/semantic-interpreter-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/semantic-interpreter-module-20261020/compiled-provenance.json), [AGENT_CLOSURE_NAVIGATION_20261020.md](../../../docs/nova/AGENT_CLOSURE_NAVIGATION_20261020.md), [audit-summary.json](../../../docs/evidence/agent-input-authority-20261020/audit-summary.json), [retained-source-freeze.json](../../../docs/evidence/agent-input-authority-20261020/retained-source-freeze.json)

**Criterios originales:**

- CTX-02: Conservar ambos casos y añadir formulaciones independientes en la campaña final; estos PASS no representan todo lenguaje natural.
- CTX-03: No ejecutar ni consumir prompt incompleto; pregunta debe corresponder al resultado pedido.
- CTX-06: Mantener tests y cubrir en UI casos ambiguos y contradictorios sin aceptar datos del modelo como permiso.
- AI-05: Conservar todos y sólo los efectos solicitados y su destino; preparación nunca implica ejecución/envío/guardado.
- AI-07: Cero finalizaciones prematuras y efectos adicionales en campaña final; éxito se compara con petición original, no sólo plan del modelo.

## source-navigation

**Propietarios:** `backend/QuickPrompt.Application/TaskSourceResolver.cs`, `backend/QuickPrompt.Infrastructure/SemanticIntentInterpreter.Sources.cs`, `backend/QuickPrompt.Infrastructure/SemanticCandidateSelector.cs`

**Selección:** `{"uiScenarios":["search","readOnly","ambiguity","notFound"]}`

**Aceptación:** Actual expected source/revision or relevant clarification; no invented record, edit, provider or execution. Free-text questions require independent review. Controlled module increment: membership selector is independent of interpreter/editor; ranked compatibility authority removed. 625 rebuilt directed checks include absent capability, explicit identity and membership overriding scores. No new real-model UI navigation acceptance; retained status remains FAIL. Ownership migration: same controlled semantic algorithms now in independent SemanticIntentInterpreter, compatibility facade delegates. No new real-model/UI qualification or status improvement inferred.

**Evidencia:** [SOURCE_SELECTION_RECOVERY.md](../../../docs/nova/SOURCE_SELECTION_RECOVERY.md), [acceptance.json](../../../docs/evidence/latency-1792487405488/acceptance.json), [RELEASE_VALIDATION_20261020.md](../../../docs/nova/RELEASE_VALIDATION_20261020.md), [semantic-development.json](../../../docs/evidence/release-validation-20261020-a1/semantic-development.json), [reference.json](../../../docs\evidence\debug-points-20261020-a6\reference.json), [AGENT_DEBUGGING.md](../../../docs/nova/AGENT_DEBUGGING.md), [CLOSURE_WAVE3.md](../../../docs/nova/CLOSURE_WAVE3.md), [acceptance.json](../../../docs/evidence/latency-1792492824884/acceptance.json), [validation.json](../../../docs/evidence/latency-1792493002752/validation.json), [CLOSURE_WAVE4.md](../../../docs/nova/CLOSURE_WAVE4.md), [development.json](../../../docs/evidence/closure-goal-20261020/wave4/development.json), [CANDIDATE_SELECTION_MODULE_20261020.md](../../../docs/nova/CANDIDATE_SELECTION_MODULE_20261020.md), [verification.json](../../../docs/evidence/candidate-selection-module-20261020/verification.json), [selection-final.trx](../../../docs/evidence/candidate-selection-module-20261020/tests/selection-final.trx), [verification.json](../../../docs/evidence/semantic-interpreter-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/semantic-interpreter-module-20261020/compiled-provenance.json), [AGENT_CLOSURE_NAVIGATION_20261020.md](../../../docs/nova/AGENT_CLOSURE_NAVIGATION_20261020.md), [audit-summary.json](../../../docs/evidence/agent-input-authority-20261020/audit-summary.json), [retained-source-freeze.json](../../../docs/evidence/agent-input-authority-20261020/retained-source-freeze.json)

**Criterios originales:**

- AI-02: Nombres, descripciones, sinónimos y referencias eligen ID correcto; no inventar ni sustituir por recientes/favoritos no pedidos.
- AI-03: Pregunta sólo por incertidumbre real; no opera sobre destino arbitrario ni inventa creación ante una referencia sin antecedente.
- AI-04: Una pregunta sobre borrar/cancelar y una prohibición no autorizan efectos; lectura no edita, ejecuta ni guarda.
- CTX-07: Cambio de campo/prompt durante inferencia, reanudación y pestañas detectan conflicto; no sobrescribir intervención válida ni operar sobre revisión antigua.

## continuity

**Propietarios:** `backend/QuickPrompt.Application/TaskPlanning.cs`, `backend/QuickPrompt.Application/TaskConversationContext.cs`, `backend/QuickPrompt.Application/TaskGoal.cs`

**Selección:** `{"uiScenarios":["unsavedAdapt","recoverUnsaved"]}`

**Aceptación:** Follow-up actually reached; same session draft/source and current delta, valid pending hash; no inherited unrelated authority. Creation failure cannot count as a tested follow-up. Wave1: unsavedAdapt and recoverUnsaved controls/content reviewed PASS, temporary identity and interruption retained. Unsaved save-copy and full multi-turn workflow still pending; no general continuity closure.

**Evidencia:** [measurements.json](../../../docs/evidence/latency-1792474435293/measurements.json), [acceptance.json](../../../docs/evidence/latency-1792487405488/acceptance.json), [RELEASE_VALIDATION_20261020.md](../../../docs/nova/RELEASE_VALIDATION_20261020.md), [debug-points.json](../../../docs/evidence/latency-1792489956051/debug-points.json), [measurements.json](../../../docs/evidence/latency-1792489956051/measurements.json), [AGENT_DEBUGGING.md](../../../docs/nova/AGENT_DEBUGGING.md), [acceptance.json](../../../docs/evidence/latency-1792491236043/acceptance.json), [reviewed-bodies.json](../../../docs/evidence/latency-1792491236043/reviewed-bodies.json), [CLOSURE_WAVE6.md](../../../docs/nova/CLOSURE_WAVE6.md), [acceptance.json](../../../docs/evidence/latency-1792494774943/acceptance.json), [results.json](../../../docs/evidence/latency-1792494774943/restart-unsavedSaveCopy/results.json), [results.json](../../../docs/evidence/latency-1792494774943/rollback-unsavedSaveCopy/results.json), [AGENT_CLOSURE_NAVIGATION_20261020.md](../../../docs/nova/AGENT_CLOSURE_NAVIGATION_20261020.md), [audit-summary.json](../../../docs/evidence/agent-input-authority-20261020/audit-summary.json), [retained-source-freeze.json](../../../docs/evidence/agent-input-authority-20261020/retained-source-freeze.json)

**Criterios originales:**

- CTX-04: Conservar plan, origen, revisión y destino sin reinterpretar datos como permiso nuevo.
- CTX-05: Crear→modificar→rellenar→ejecutar→revisar/guardar y cambio explícito de prompt conservan contexto correcto en varios turnos.
- CTX-07: Cambio de campo/prompt durante inferencia, reanudación y pestañas detectan conflicto; no sobrescribir intervención válida ni operar sobre revisión antigua.
- AUT-04: Cambio exacto pedido, resto/invariantes preservados; fidelidad ≥90% y contaminación cero en campaña final.
- AUT-05: Base sólo si se pide; borrador mantiene identidad temporal, sin ID persistido ficticio ni valores del prompt anterior.

## approval-save

**Propietarios:** `backend/QuickPrompt.Application/AgentOrchestrator.Visible.cs`, `backend/QuickPrompt.Application/LiveExecution.cs`

**Selección:** `{"uiScenarios":["saveVersion","saveCopy","rejectSave","unsavedSaveCopy"]}`

**Aceptación:** Write only after exact human approval, correct version/copy identity, same approved text persisted once, stale/rejected proposals unchanged. Run after a changed approval/save boundary or final accepted authoring candidate. Version approval/persistence/restart and replay rejection PASS; copy and free rejection blocked upstream. Never count unvisited approval paths as PASS. One legacy Quick Assist Favorite request used installed Qwen3:8b and persisted only after explicit review; 21,722 ms plan latency. Does not qualify modern NOVA authoring/save or broad semantics; status unchanged.

**Evidencia:** [SAVE_RECOVERY.md](../../../docs/nova/SAVE_RECOVERY.md), [acceptance.json](../../../docs/evidence/latency-1792487405488/acceptance.json), [RELEASE_VALIDATION_20261020.md](../../../docs/nova/RELEASE_VALIDATION_20261020.md), [results.json](../../../docs/evidence/latency-1792487405488/restart/results.json), [CLOSURE_WAVE6.md](../../../docs/nova/CLOSURE_WAVE6.md), [acceptance.json](../../../docs/evidence/latency-1792494258858/acceptance.json), [save-boundary-final.trx](../../../docs/evidence/closure-goal-20261020/wave6/tests/save-boundary-final.trx), [acceptance.json](../../../docs/evidence/latency-1792494774943/acceptance.json), [results.json](../../../docs/evidence/latency-1792494774943/restart-unsavedSaveCopy/results.json), [results.json](../../../docs/evidence/latency-1792494774943/rollback-unsavedSaveCopy/results.json), [verification.json](../../../docs/evidence/runtime-ports-20261020/verification.json), [results.json](../../../docs/evidence/runtime-ui-1792510214768/results.json), [AGENT_CLOSURE_NAVIGATION_20261020.md](../../../docs/nova/AGENT_CLOSURE_NAVIGATION_20261020.md), [audit-summary.json](../../../docs/evidence/agent-input-authority-20261020/audit-summary.json), [retained-source-freeze.json](../../../docs/evidence/agent-input-authority-20261020/retained-source-freeze.json)

**Criterios originales:**

- SAV-01: Crear con Qwen→formulario fiel→revisar→aprobar→un INSERT→reabrir tras reinicio con igualdad exacta.
- SAV-02: Actualizar elemento correcto y preservar original antes de aprobar; versión/copia con identidad, revisión y texto esperados tras reiniciar.
- SAV-03: Rechazar/cancelar no escribe; cambio de cuerpo/revisión/destino invalida aprobación; replay no repite efecto.
- AUT-06: Hash y texto canónico final iguales en revisión, formulario, preview, aprobación y SQLite; un cambio invalida aprobación/revisión previa.

## execution-control

**Propietarios:** `backend/QuickPrompt.Application/LiveExecution.cs`, `backend/QuickPrompt.Application/LocalCommands.cs`

**Selección:** `{"uiScenarios":["local","provider","pauseResume","cancel","fault"]}`

**Aceptación:** Exact compiled input sent once only when authorized, real response observed, explicit pause/cancel/recovery; not selected for an authoring-only probe. EXE-07 scoped closure: absent/timeout usable UI, transient recovery, persistent two attempts, no writes/false completion. Other execution/control criteria unchanged. Provider port increment proves direct manual real-model completion/copy and explicit execution input, not agent goal selection. Literal synthetic response oracle false; existing semantic status remains unchanged.

**Evidencia:** [VERSION_CLOSURE_MATRIX.json](../../../docs/nova/VERSION_CLOSURE_MATRIX.json), [acceptance.json](../../../docs/evidence/latency-1792487405488/acceptance.json), [RELEASE_VALIDATION_20261020.md](../../../docs/nova/RELEASE_VALIDATION_20261020.md), [debug-points.json](../../../docs/evidence/latency-1792489956051/debug-points.json), [validation.json](../../../docs/evidence/latency-1792489956051/validation.json), [AGENT_DEBUGGING.md](../../../docs/nova/AGENT_DEBUGGING.md), [acceptance.json](../../../docs/evidence/closure-goal-20261020/wave1/feedback/acceptance.json), [verification.json](../../../docs/evidence/provider-ports-20261020/verification.json), [results.json](../../../docs/evidence/provider-ui-1792506768311/results.json)

**Criterios originales:**

- EXE-02: Desde Home/detalle/borrador, respuesta real del texto vigente, una ejecución autorizada, sin reutilizar respuesta anterior.
- EXE-03: Separar entrega técnica de calidad: respuestas en casos congelados respetan datos/restricciones sin promesas inventadas; preservar fallos.
- EXE-04: Respuesta parcial nunca se copia/guarda como completa ni registra uso; cancelar libera recursos y no deja efectos tardíos.
- EXE-05: Conservar comportamiento; diferenciar conflicto de revisión real de un simple toque.
- EXE-06: Ocultar/volver o desconectar no provoca éxito falso ni duplica acciones; mensaje temporal contextual; reanudar sólo con identidad/revisión válidas.
- EXE-07: Biblioteca sigue utilizable; pregunta/error útil, cancelación y reintentos acotados; sin guardar/ejecutar contenido incompleto.
- EXT-01: Proveedor y texto correctos, allowlist, copia fallida/popup bloqueado recuperables; no incluir contenido en URL ni auto-submit.

## deletion

**Propietarios:** `backend/QuickPrompt.Application/LiveDeletion.cs`

**Selección:** `{"harness":"Tests/nova/agent-deletion.cjs"}`

**Aceptación:** Historical scoped deletion acceptance preserved. Repeat only when deletion/shared authority boundary changes or during final release campaign. Current final six cases: 5 PASS, explanatory question FAIL with invalid capability evidence binding; prior fail-fast run retained. Actual cancel/confirmed delete and other safety negatives pass; not full agent acceptance. Diagnostic replay retained: EXPLAIN repeated with valid supporting passages 0 and 1; duplicate-operation rejection, not an invalid passage or an attempted deletion. No semantic fix promoted. Wave1 scoped qualification: all original six UI/Qwen cases PASS, including explanation-only. Earlier failures retained; later semantic candidates require regression before release.

**Evidencia:** [AGENT_DELETION_ACCEPTANCE.md](../../../docs/nova/AGENT_DELETION_ACCEPTANCE.md), [results.json](../../../docs/evidence/agent-deletion-20261019/1792488455686/results.json), [results.json](../../../docs/evidence/agent-deletion-20261019/1792488637724/results.json), [results.json](../../../docs/evidence/agent-deletion-20261019/1792489873247/results.json), [AGENT_DEBUGGING.md](../../../docs/nova/AGENT_DEBUGGING.md), [results.json](../../../docs/evidence/agent-deletion-20261019/1792491113136/results.json)

**Criterios originales:**

- SAV-04: Capacidad tipada de eliminación con identidad/revisión, confirmación vigente y verificación de ausencia; cancelar conserva navegación y datos; sin borrar implícitamente.

## latency

**Propietarios:** `backend/QuickPrompt.Infrastructure/OllamaAgentModelTransport.Reuse.cs`, `backend/QuickPrompt.Evaluation/RecoveryEvaluation.cs`, `Tests/nova/latency.cjs`

**Selección:** `{"qualityFirst":true,"repeats":"Use unchanged original performance thresholds and stable comparable resources; one observation is not p50/p95."}`

**Aceptación:** Quality before speed, frozen model/build/data, resources comparable, original thresholds and validation/holdout gates retained. Incorrect or unreviewed output cannot be a latency success.

**Evidencia:** [PERFORMANCE_ACCEPTANCE.md](../../../docs/nova/PERFORMANCE_ACCEPTANCE.md), [acceptance.json](../../../docs/evidence/latency-1792487405488/acceptance.json), [RELEASE_VALIDATION_20261020.md](../../../docs/nova/RELEASE_VALIDATION_20261020.md), [AGENT_CLOSURE_NAVIGATION_20261020.md](../../../docs/nova/AGENT_CLOSURE_NAVIGATION_20261020.md), [audit-summary.json](../../../docs/evidence/agent-input-authority-20261020/audit-summary.json), [retained-source-freeze.json](../../../docs/evidence/agent-input-authority-20261020/retained-source-freeze.json)

**Criterios originales:**

- PER-01: Registrar frío/caliente, recursos y tres baselines comparables; fijar metas por etapa y total antes de comparar, con fallos y esperas humanas separados.
- PER-02: Campaña estable del mismo candidato: éxito correcto, llamadas, p50/p95, tokens/s y recursos; sin reducir controles o contexto para ganar velocidad.
- PER-03: Sesiones repetidas, SSE/monitores/cancelaciones y llamadas secuenciales liberan recursos; medir presión RAM/VRAM sin cerrar apps del usuario ni confundir instalación con residencia.
- REL-05: Intención ≥90%; planes/destino/finalización/herramientas ≥95%; fidelidad/éxito/metamórficas ≥90%; contaminación, no autorizados, IDs inventados y cierre prematuro =0; splits congelados y holdout sin tuning.
- REL-06: Identificar fuentes+artefactos+config efectiva+modelo/digest+fixture; no mezclar perfiles rechazados; preservar trabajo ajeno y preparar commit sólo cuando se solicite.

## reflected-tools

**Propietarios:** `backend/QuickPrompt.Application/PreparationTools.cs`, `backend/QuickPrompt.Application/PreparationToolAttribute.cs`, `backend/QuickPrompt.Infrastructure/ReflectedPreparationToolFactory.cs`, `backend/QuickPrompt.Infrastructure/PreparationReadTools.cs`

**Selección:** `{"contractFilter":"FullyQualifiedName~ReflectedPreparationToolTests|FullyQualifiedName~CapabilityDiscoveryTests|FullyQualifiedName~AgenticRuntimeTests|FullyQualifiedName~TaskGoalSourceTransportTests","realModel":"dotnet run --project backend/QuickPrompt.Evaluation/QuickPrompt.Evaluation.csproj -- --reflected-tools docs/evidence/NEW_REPORT.json","scope":"Read tool schemas and execution only; no new semantic planner, authoring or UI qualification."}`

**Aceptación:** Schemas generated from actual trusted DTOs/methods; strict arguments, domain hook, permission, shared budget and cancellation retained. Real model reads historical/current SQLite records and answers with actual titles. PASS applies only to this tool boundary; canonical semantic requirements remain open.

**Evidencia:** [review.md](../../../docs/evidence/closure-20261020/reflected-tools-increment/review.md), [freeze.json](../../../docs/evidence/closure-20261020/reflected-tools-increment/freeze.json), [reflected-tools-flat-and-consumers-final.trx](../../../docs/evidence/closure-20261020/tests/reflected-tools-flat-and-consumers-final.trx), [reflected-tools-domain-hook.trx](../../../docs/evidence/closure-20261020/tests/reflected-tools-domain-hook.trx), [reflected-read-tools-local.json](../../../docs/evidence/closure-20261020/reflected-read-tools-local.json), [reflected-read-tools-flat-local.json](../../../docs/evidence/closure-20261020/reflected-read-tools-flat-local.json)

**Criterios originales:**

- AI-01: Superar development→validation→holdout congelados con perfil final; intención ≥90%, sin rutas por keywords/regex de intención.
- AI-02: Nombres, descripciones, sinónimos y referencias eligen ID correcto; no inventar ni sustituir por recientes/favoritos no pedidos.
- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.

## ui-agent-resources

**Propietarios:** `backend/QuickPrompt.Application/UiAgentResources.cs`, `backend/QuickPrompt.Application/AgentOrchestrator.Visible.cs`, `backend/QuickPrompt.Infrastructure/UiDecisionModel.cs`, `backend/QuickPrompt.Application/UiToolCatalog.cs`, `backend/QuickPrompt.Application/SameTabUiDispatcher.cs`

**Selección:** `{"contractFilter":"FullyQualifiedName~UiAgentResourcesTests|FullyQualifiedName~InferenceReuseTests|FullyQualifiedName~UiDecisionTransportTests|FullyQualifiedName~LiveExecutionTests|FullyQualifiedName~SemanticSystemKnowledgeTests|FullyQualifiedName~SameTabUiTests|FullyQualifiedName~TaskGoalTests","uiScenarios":["boundVariables","quickVariables"],"runtime":"QA only: real local Qwen, ExecuteGroundedPlan=false and StopAfterVerifiedResult=false to exercise model decisions. Existing production defaults unchanged."}`

**Aceptación:** Actual guide metadata, current fields/source/revision, eligible catalog permissions, original task/authorized continuation and checked effect receipts reach the model. Exact write checks, cache freshness and task isolation remain enforced. 101 controlled checks PASS and quick action UI PASS; retained free request FAIL in initial authoring interpretation. No semantic replacement or release qualification; no unrelated frontend/backend matrix rerun. Subsequent typed-json instruction-scope-3 increment fixes the two original free requests in the initial owner: both final UI cases PASS, 194 scoped checks PASS. Earlier failure remains evidence; general resource coverage remains PARTIAL. UI decision ownership extracted to UiDecisionModel; direct factory isolation and controlled packet/transport/resource tests pass in rebuilt 174-test cohort. No new real-model or browser campaign; semantic coverage remains PARTIAL.

**Evidencia:** [UI_AGENT_RESOURCES.md](../../../docs/nova/UI_AGENT_RESOURCES.md), [ui-agent-resources-freeze.json](../../../docs/evidence/closure-20261020/ui-agent-resources-freeze.json), [ui-agent-resources-qualified-boundaries.trx](../../../docs/evidence/closure-20261020/tests/ui-agent-resources-qualified-boundaries.trx), [measurements.json](../../../docs/evidence/latency-1792484501499/measurements.json), [validation.json](../../../docs/evidence/latency-1792484501499/validation.json), [measurements.json](../../../docs/evidence/latency-1792484718655/measurements.json), [validation.json](../../../docs/evidence/latency-1792484718655/validation.json), [TYPED_AGENT_CONTEXT.md](../../../docs/nova/TYPED_AGENT_CONTEXT.md), [measurements.json](../../../docs/evidence/latency-1792486471301/measurements.json), [validation.json](../../../docs/evidence/latency-1792486471301/validation.json), [acceptance.json](../../../docs/evidence/latency-1792487405488/acceptance.json), [RELEASE_VALIDATION_20261020.md](../../../docs/nova/RELEASE_VALIDATION_20261020.md), [UI_DECISION_MODULE_20261020.md](../../../docs/nova/UI_DECISION_MODULE_20261020.md), [verification.json](../../../docs/evidence/ui-decision-module-20261020/verification.json), [ui-final.trx](../../../docs/evidence/ui-decision-module-20261020/tests/ui-final.trx)

**Criterios originales:**

- AI-01: Superar development→validation→holdout congelados con perfil final; intención ≥90%, sin rutas por keywords/regex de intención.
- AI-02: Nombres, descripciones, sinónimos y referencias eligen ID correcto; no inventar ni sustituir por recientes/favoritos no pedidos.
- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.

## semantic-core-replacement

**Propietarios:** `backend/QuickPrompt.Evaluation/Experiments/ReflectedSemanticAgent.cs`, `backend/QuickPrompt.Evaluation/Experiments/ReflectedSemanticAgent.Content.cs`, `backend/QuickPrompt.Application/TaskPlanning.cs`, `backend/QuickPrompt.Application/SemanticIntent.cs`, `backend/QuickPrompt.Application/TaskSourceResolver.cs`, `backend/QuickPrompt.Evaluation/Experiments/RoleScopedContext.cs`

**Selección:** `{"contractFilter":"FullyQualifiedName~ReflectedSemanticAgentTests|FullyQualifiedName~TaskPlanningInputScopeTests|FullyQualifiedName~GroundedUiPlanTests|FullyQualifiedName~UserInputScopeTests|FullyQualifiedName~ReferenceFirstPlanningTests|FullyQualifiedName~SemanticPlanTests","development":"Tests/nova/datasets/recovery-v1/development.json","testedCases":["recovery-v1-development-01","recovery-v1-development-03","recovery-v1-development-05","recovery-v1-development-09"],"runtime":"Unqualified module isolated in Evaluation/Experiments. Production ApplicationModules resolves PreparationInference; no new fallback or model activation."}`

**Aceptación:** Reuses actual capability owners with no phrase handlers or duplicate action registry; preserved user request and observed context; qualified real-model task completion before replacing composition. The same four retained cases fail in both probes; 75 controlled boundary tests do not promote the prototype. No UI/holdout acceptance claimed. Latest role-context experiment: actual Prepare on three original retained creation requests; one content accepted only for that request, two failures preserved. No candidate UI, validation or holdout qualified; no promotion. Current recovery-history projection has controlled tests only, not a fresh semantic acceptance. Fixed canonical demonstration pack evaluated once with original AgentOrchestrator and unchanged three development requests: two content-only acceptable outputs; literal-title case still contaminates the body and review invents evidence/demands title in body. REJECTED, no promotion, no UI/holdout; resource interposition, tests and selector removed from active compilation, sources/binaries retained. 31 archived protocol checks do not establish semantic reliability or current suite size.

**Evidencia:** [PRODUCTION_AGENT_ARCHITECTURE.md](../../../docs/nova/PRODUCTION_AGENT_ARCHITECTURE.md), [reflected-core-development-initial.json](../../../docs/evidence/closure-20261020/reflected-core-development-initial.json), [reflected-core-development-protocol.json](../../../docs/evidence/closure-20261020/reflected-core-development-protocol.json), [review.md](../../../docs/evidence/closure-20261020/reflected-core-protocol-rejected/review.md), [semantic-core-production-review-boundaries.trx](../../../docs/evidence/closure-20261020/tests/semantic-core-production-review-boundaries.trx), [v1-review.json](../../../docs/evidence/role-context-20261020/v1-review.json), [ports-and-history-final.trx](../../../docs/evidence/role-context-20261020/tests/ports-and-history-final.trx), [ROLE_CONTEXT_REFACTOR_20261020.md](../../../docs/nova/ROLE_CONTEXT_REFACTOR_20261020.md), [AUTHORING_RESOURCES_EXPERIMENT_20261020.md](../../../docs/nova/AUTHORING_RESOURCES_EXPERIMENT_20261020.md), [freeze.json](../../../docs/evidence/authoring-resources-20261020/freeze.json), [v1-preparation.json](../../../docs/evidence/authoring-resources-20261020/v1-preparation.json), [v1-review.json](../../../docs/evidence/authoring-resources-20261020/v1-review.json), [retirement.json](../../../docs/evidence/authoring-resources-20261020/retirement.json), [resources-context-contracts.trx](../../../docs/evidence/authoring-resources-20261020/tests/resources-context-contracts.trx)

**Criterios originales:**

- AI-01: Superar development→validation→holdout congelados con perfil final; intención ≥90%, sin rutas por keywords/regex de intención.
- AUT-01: Prompt útil y ejecutable con tarea futura correcta; cero controles actuales de crear/navegar/guardar/no ejecutar ahora en el cuerpo.
- REL-05: Intención ≥90%; planes/destino/finalización/herramientas ≥95%; fidelidad/éxito/metamórficas ≥90%; contaminación, no autorizados, IDs inventados y cierre prematuro =0; splits congelados y holdout sin tuning.

## typed-agent-context

**Propietarios:** `backend/QuickPrompt.Infrastructure/ValidatedAgentJson.cs`, `backend/QuickPrompt.Infrastructure/SemanticIntentInterpreter.InstructionUse.cs`, `backend/QuickPrompt.Application/CapabilityDiscovery.cs`, `backend/QuickPrompt.Application/SemanticIntent.cs`

**Selección:** `{"contractFilter":"FullyQualifiedName~ValidatedAgentJsonTests|FullyQualifiedName~InstructionUseBoundaryTests|FullyQualifiedName~SemanticWorkflowTransportTests|FullyQualifiedName~GroundedCatalogReferenceTransportTests|FullyQualifiedName~GroundedConstraintTransportTests|FullyQualifiedName~SourceConstraintStageTests|FullyQualifiedName~CapabilityDiscoveryTests|FullyQualifiedName~WorkflowRefinementTransportTests|FullyQualifiedName~SourceReferenceScopeTransportTests|FullyQualifiedName~ReferenceFirstPlanningTests|FullyQualifiedName~TaskPlanningInputScopeTests|FullyQualifiedName~GroundedUiPlanTests|FullyQualifiedName~UserInputScopeTests|FullyQualifiedName~LiveExecutionTests|FullyQualifiedName~UiDecisionTransportTests|FullyQualifiedName~UiAgentResourcesTests|FullyQualifiedName~CapabilityVectorTests|FullyQualifiedName~CapabilityKnowledgeTransportTests|FullyQualifiedName~SemanticRetryTransportTests|FullyQualifiedName~SemanticSystemKnowledgeTests","uiScenarios":["boundVariables","variables"],"testedCases":["recovery-v1-development-04","recovery-v1-development-07","recovery-v1-development-11"],"runtime":"Qwen3:8b real; application planning and isolated synthetic SQLite; same final frozen assemblies. UI QA-only model navigation profile, production configuration unchanged."}`

**Aceptación:** Scope guide/context/response JSON Schema generated from trusted C# types; strict typed serialization/deserialization and owner coherence checks; reflected Writes/Requires without phrase routing. Missing resources retain uncertainty. 194 scoped checks, three selected real-Qwen plan regressions and two original UI fill/copy cases PASS. Source/template/revision, literal values and browser copy verified. Does not qualify all language, creation/fidelity, all legacy model stages, native OS clipboard, live vector retrieval, holdout or global latency. Canonical release requirements remain unchanged. Broader final negative question fails capability-to-passage binding. Prior scoped PASS remains historical evidence; strict rejection is safe but full requested behavior is not qualified. Instrumented replay identifies duplicate EXPLAIN with two valid passage indices. The same existing guard still rejects it; instrumentation does not qualify semantic completion. Ownership migration: same controlled semantic algorithms now in independent SemanticIntentInterpreter, compatibility facade delegates. No new real-model/UI qualification or status improvement inferred.

**Evidencia:** [TYPED_AGENT_CONTEXT.md](../../../docs/nova/TYPED_AGENT_CONTEXT.md), [INSTRUCTION_USE_GROUNDING.md](../../../docs/nova/INSTRUCTION_USE_GROUNDING.md), [context.schema.json](../../../docs/nova/contracts/instruction-scope-v3/context.schema.json), [guide.schema.json](../../../docs/nova/contracts/instruction-scope-v3/guide.schema.json), [response.schema.json](../../../docs/nova/contracts/instruction-scope-v3/response.schema.json), [example.input.json](../../../docs/nova/contracts/instruction-scope-v3/example.input.json), [example.response.json](../../../docs/nova/contracts/instruction-scope-v3/example.response.json), [typed-json-v3-qualified-boundaries.trx](../../../docs/evidence/closure-20261020/tests/typed-json-v3-qualified-boundaries.trx), [typed-json-v3-freeze.json](../../../docs/evidence/closure-20261020/typed-json-v3-freeze.json), [typed-json-v3-semantic-regression.json](../../../docs/evidence/closure-20261020/typed-json-v3-semantic-regression.json), [measurements.json](../../../docs/evidence/latency-1792486471301/measurements.json), [validation.json](../../../docs/evidence/latency-1792486471301/validation.json), [acceptance.json](../../../docs/evidence/latency-1792487405488/acceptance.json), [RELEASE_VALIDATION_20261020.md](../../../docs/nova/RELEASE_VALIDATION_20261020.md), [results.json](../../../docs/evidence/agent-deletion-20261019/1792489873247/results.json), [boundary-tests.trx](../../../docs\evidence\debug-points-20261020-a6\boundary-tests.trx), [AGENT_DEBUGGING.md](../../../docs/nova/AGENT_DEBUGGING.md), [verification.json](../../../docs/evidence/semantic-interpreter-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/semantic-interpreter-module-20261020/compiled-provenance.json)

**Criterios originales:**

- AI-01: Superar development→validation→holdout congelados con perfil final; intención ≥90%, sin rutas por keywords/regex de intención.
- AI-02: Nombres, descripciones, sinónimos y referencias eligen ID correcto; no inventar ni sustituir por recientes/favoritos no pedidos.
- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.

