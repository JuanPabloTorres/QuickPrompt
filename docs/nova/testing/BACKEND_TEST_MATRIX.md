# Matriz de pruebas: backend

Domain rules, API, persistence and infrastructure; excludes natural-language comprehension and browser acceptance.

A scoped matrix does not close the release. Run only changed capabilities and their explicit dependencies; use the same frozen candidate. Historical, mocked, component and real-model evidence are distinct.

Este documento valida trazabilidad; no ejecuta las pruebas ni convierte evidencia histórica en aceptación actual.

| Bloque | Estado de evidencia | Requisitos canónicos | Tipo |
|---|---|---|---|
| domain-api | PASS | REL-01, SEC-01 | deterministic_contract_integration |
| persistence | PARTIAL | DAT-01, DAT-02, DAT-06 | sqlite_integration |
| build | PASS | REL-03 | build |
| model-transport-ownership | PASS | REL-01, REL-05 | controlled_http_cache_lifecycle_and_actual_api_factory_not_model_semantics |
| storage-failure-boundary | PASS | REL-03, SEC-01 | controlled_type_boundary_plus_real_sqlite_abort_and_blazor_ui |
| host-module-substitution | PASS | REL-01, REL-03, SEC-01 | actual_host_factory_http_security_and_ui_isolation_not_semantic_accuracy |
| legacy-inference-shared-port | PASS | REL-01, REL-03 | controlled_actual_di_factory_substitution_and_protocol_compatibility_not_model_accuracy |
| public-contract-metadata | PASS | REL-01, REL-03 | real_csharp_metadata_export_and_generation_with_controlled_failure_and_substitution |
| ui-selector-module-isolation | PASS | REL-01, REL-03 | actual_composition_substitution_and_controlled_http_protocol_not_model_comprehension |
| candidate-selection-ownership | PASS | REL-01, REL-03 | actual_factory_and_orchestrator_authority_with_controlled_model_http_and_json_regression |
| input-extraction-ownership | PASS | REL-01, REL-03 | actual_di_factory_and_orchestrator_with_controlled_extraction_and_domain_grounding |
| input-scope-ownership | PASS | REL-01, REL-03 | controlled_scope_composition_context_projection_and_real_sqlite_revision_boundaries |
| fidelity-review-ownership | PASS | REL-01, REL-03 | controlled_factory_protocol_repair_and_authority_compatibility_not_model_fidelity |
| draft-generation-ownership | PASS | REL-01, REL-03 | controlled_factory_protocol_identity_revision_and_concurrency_not_model_fidelity |
| semantic-interpretation-ownership | PASS | REL-01, REL-03 | controlled_actual_factory_and_loopback_http_compatibility_not_model_understanding |
| local-client-resource-ownership | PASS | REL-01, REL-03 | actual_service_factory_disposal_and_loopback_http_controlled_not_model_semantics |
| model-policy-ownership | PASS | REL-01, REL-03 | domain_policy_admission_defaults_real_http_and_old_binary_compatibility_not_model_accuracy |
| semantic-capability-wiring | PASS | REL-01, REL-03 | actual_factory_workflow_capability_wiring_controlled_not_model_fidelity |
| legacy-aggregate-retirement | PASS | REL-01, REL-03 | production_artifact_retirement_and_controlled_qa_compatibility_not_model_fidelity |
| runtime-settings-snapshot | PASS | REL-01, REL-03 | actual_factory_configuration_mutation_and_loopback_endpoint_consistency_not_model_fidelity |

## domain-api

**Propietarios:** `backend/QuickPrompt.Application`, `backend/QuickPrompt.Api`, `backend/QuickPrompt.Infrastructure`, `backend/QuickPrompt.Application/EditorDraftPreparation.cs`, `backend/QuickPrompt.Application/EditorTools.cs`, `backend/QuickPrompt.Application/PreparedDraftCompletion.cs`, `backend/QuickPrompt.Application/PreparedDraftSaving.cs`

**Selección:** dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore For editor preparation increment, select the six classes in docs/evidence/editor-preparation-case-20261020/test-selection.json; do not rerun model campaigns for this deterministic boundary move.

**Aceptación:** Current serializer compatibility campaign: 696/696 controlled backend assembly tests PASS, no skipped. One additional domain-hook test added later passed with its 16-test class separately. Full suite justified by STJ major dependency update. This does not establish Qwen semantics, complete security audit or release readiness; historical cohort remains archived. Historical frozen release candidate: 730 backend, 151 shared tests and API/evaluator/web builds PASS, no skipped tests. Shared hashes equal; sources unchanged. Does not establish semantic task accuracy. Current modular increment: 784/784 backend tests, no skipped; read-only port substitution and existing SQLite revision/confirmation rules preserved. Collection UI and typed-command UI are separate evidence. This does not establish free-language semantics or full security/release acceptance. Archive increment: 789/789 backend PASS, no skipped; final directed archive contracts 5/5 after analyzer-only correction. This does not establish natural-language reliability. Provider increment: 791/791 controlled backend tests pass, no skips; transfer/execution review and preference confirmation remain with domain owners. No model planning acceptance inferred. Local workspace increment: 23 relevant backend boundary tests and 155 shared legacy tests PASS, no skips. Full 791-test cohort was not repeated for unchanged semantics. Organization increment: 23 selected backend boundary checks and 160 shared legacy tests PASS; five new port/retirement tests include reflection proving old unguarded signatures absent. Not model semantics. Runtime contract ownership move: full 791/791 controlled backend tests PASS, no skips, because model/browser interface assembly identities affect consumers across the assembly. Not 791 real model requests. Editor application-boundary increment: HTTP delegates to IEditorDraftPreparation and pure EditorTools.Compile; existing catalog/evaluator/buffer/session owners retained, no model dependency. 88 baseline and 109 final directed backend tests (21 new, zero skipped) PASS. Token-equivalent moved algorithm/validation, public DTO declarations and other routes preserved. Twenty-one real HTTP cases per old/new build have equal normalized responses; four headed editor-context and 15 normal/disabled/replacement host checks PASS. API/evaluator/Blazor web builds zero warnings/errors and common DLL hashes match tested binaries. Four reflected schema/capability artifacts byte-identical; 34 static controls PASS. Unchanged frozen frontend reused, no unrelated unit/model matrix. Existing session Source policy retained without semantic qualification; no cross-store transaction/cancellation claim, canonical 34/54 unchanged. Prepared lifecycle increment: completion/save leave HTTP through two segregated interfaces; saving factory works without LiveRuns/model, while completion requires its live owner. Generic retained-receipt/missing-content defect reproduced through real HTTP (200 empty) and corrected to 409 with no repeated effects; same seven-step HTTP workflow passes. 150 baseline and 164 final scoped backend tests PASS (14 new, zero skipped). Two production-UI binding checks use a controlled preparation stream but real completion/save/SQLite, explicitly not model comprehension. Fifteen host composition checks, three builds with zero warnings/errors, matching DLLs, four identical reflected artifacts and 35 static guards PASS. Other extracted logic, DTOs and endpoints preserved; only declared unavailable-result outcome changes. No unrelated frontend/model suite, no cross-owner transaction claim; canonical 34/54 unchanged.

**Evidencia:** [template-representation-v3-backend-full.trx](../../../docs/evidence/closure-20261020/tests/template-representation-v3-backend-full.trx), [reflected-serializer-backend-final.trx](../../../docs/evidence/closure-20261020/tests/reflected-serializer-backend-final.trx), [review.md](../../../docs/evidence/closure-20261020/reflected-tools-increment/review.md), [verification.json](../../../docs/evidence/release-validation-20261020-a1/verification.json), [backend-collection-modules-final.trx](../../../docs/evidence/role-context-20261020/tests/backend-collection-modules-final.trx), [verification.json](../../../docs/evidence/role-context-20261020/verification.json), [backend-final.trx](../../../docs/evidence/archive-ports-20261020/tests/backend-final.trx), [verification.json](../../../docs/evidence/archive-ports-20261020/verification.json), [verification.json](../../../docs/evidence/provider-ports-20261020/verification.json), [backend-final.trx](../../../docs/evidence/provider-ports-20261020/tests/backend-final.trx), [verification.json](../../../docs/evidence/local-workspace-20261020/verification.json), [backend-boundaries.trx](../../../docs/evidence/local-workspace-20261020/tests/backend-boundaries.trx), [verification.json](../../../docs/evidence/organization-ports-20261020/verification.json), [backend-protected-ports-final.trx](../../../docs/evidence/organization-ports-20261020/tests/backend-protected-ports-final.trx), [verification.json](../../../docs/evidence/runtime-ports-20261020/verification.json), [backend-final.trx](../../../docs/evidence/runtime-ports-20261020/tests/backend-final.trx), [EDITOR_PREPARATION_CASE_20261020.md](../../../docs/nova/EDITOR_PREPARATION_CASE_20261020.md), [verification.json](../../../docs/evidence/editor-preparation-case-20261020/verification.json), [incidents.json](../../../docs/evidence/editor-preparation-case-20261020/incidents.json), [algorithm-equivalence.json](../../../docs/evidence/editor-preparation-case-20261020/algorithm-equivalence.json), [source-freeze.json](../../../docs/evidence/editor-preparation-case-20261020/source-freeze.json), [harness-provenance.json](../../../docs/evidence/editor-preparation-case-20261020/harness-provenance.json), [metadata-equivalence.json](../../../docs/evidence/editor-preparation-case-20261020/metadata-equivalence.json), [test-selection.json](../../../docs/evidence/editor-preparation-case-20261020/test-selection.json), [baseline.trx](../../../docs/evidence/editor-preparation-case-20261020/tests/baseline.trx), [case-tests-v2.trx](../../../docs/evidence/editor-preparation-case-20261020/tests/case-tests-v2.trx), [final.trx](../../../docs/evidence/editor-preparation-case-20261020/tests/final.trx), [module-boundaries.json](../../../docs/evidence/editor-preparation-case-20261020/module-boundaries.json), [results.json](../../../docs/evidence/editor-preparation-http-1792542821431/results.json), [results.json](../../../docs/evidence/editor-draft-ui-1792542853523/results.json), [results.json](../../../docs/evidence/module-host-ui-1792542866461/results.json), [PREPARED_DRAFT_LIFECYCLE_20261020.md](../../../docs/nova/PREPARED_DRAFT_LIFECYCLE_20261020.md), [verification.json](../../../docs/evidence/prepared-draft-lifecycle-20261020/verification.json), [incidents.json](../../../docs/evidence/prepared-draft-lifecycle-20261020/incidents.json), [source-freeze.json](../../../docs/evidence/prepared-draft-lifecycle-20261020/source-freeze.json), [algorithm-equivalence.json](../../../docs/evidence/prepared-draft-lifecycle-20261020/algorithm-equivalence.json), [harness-provenance.json](../../../docs/evidence/prepared-draft-lifecycle-20261020/harness-provenance.json), [metadata-equivalence.json](../../../docs/evidence/prepared-draft-lifecycle-20261020/metadata-equivalence.json), [test-selection.json](../../../docs/evidence/prepared-draft-lifecycle-20261020/test-selection.json), [baseline.trx](../../../docs/evidence/prepared-draft-lifecycle-20261020/tests/baseline.trx), [lifecycle.trx](../../../docs/evidence/prepared-draft-lifecycle-20261020/tests/lifecycle.trx), [final.trx](../../../docs/evidence/prepared-draft-lifecycle-20261020/tests/final.trx), [module-boundaries.json](../../../docs/evidence/prepared-draft-lifecycle-20261020/module-boundaries.json), [results.json](../../../docs/evidence/prepared-draft-lifecycle-1792543256001/results.json), [results.json](../../../docs/evidence/prepared-draft-lifecycle-1792543824767/results.json), [results.json](../../../docs/evidence/prepared-lifecycle-ui-1792543967883/results.json), [results.json](../../../docs/evidence/module-host-ui-1792544058582/results.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- SEC-01: Pruebas de host/origen/CSRF/body-limit/rate-limit/rutas y errores sin filtrado de datos; sin abrir red ni relajar políticas para tests.

## persistence

**Propietarios:** `backend/QuickPrompt.Infrastructure/PreparationStore.cs`, `backend/QuickPrompt.Infrastructure/NovaPromptStore.cs`, `backend/QuickPrompt.Infrastructure/NovaLibraryArchives.cs`, `backend/QuickPrompt.Infrastructure/LocalPromptLibrary.cs`, `backend/QuickPrompt.Infrastructure/Local/LegacyImporter.cs`, `backend/QuickPrompt.Infrastructure/LocalWorkspacePorts.cs`, `backend/QuickPrompt.Application/LocalWorkspace.cs`, `backend/QuickPrompt.Application/OrganizationLibrary.cs`, `backend/QuickPrompt.Infrastructure/OrganizationLibraryPorts.cs`, `backend/QuickPrompt.Infrastructure/Local/OrganizationStore.cs`, `backend/QuickPrompt.Application/WorkspaceRuntime.cs`, `backend/QuickPrompt.Application/PromptPanel.cs`, `backend/QuickPrompt.Infrastructure/WorkspaceRuntimePorts.cs`

**Selección:** Choose existing store/integration tests only when persistence contracts change.

**Aceptación:** Exact revision, authorized write once, unrelated records intact, restart and rollback as required by canonical criteria. Archive port increment passes bounded/cancelled conversion, confirmation, snapshots, verified backup, replay retention and real UI consumers. Canonical restart/rollback requirements remain PARTIAL, not closed by this slice. Server-rendered core ports preserve existing SQL/transactions: preview read-only, compile privacy/composition, atomic proposals and revision conflicts. Broader canonical persistence requirements remain PARTIAL. Organization ports retain one transactional owner, rollback, typed identity by kind/id and independent compiled snapshots. Confirmed/revision-bound deletion proven in two real circuits. Broader release criteria remain unchanged. Runtime ports: 166 shared tests and real JS panel/fallback audit verify no preparation write, stale rejection, metadata-only audit and explicit compile behavior. Canonical persistence status unchanged.

**Evidencia:** [VERSION_CLOSURE_MATRIX.json](../../../docs/nova/VERSION_CLOSURE_MATRIX.json), [results.json](../../../docs/evidence/latency-1792487405488/restart/results.json), [verification.json](../../../docs/evidence/archive-ports-20261020/verification.json), [results.json](../../../docs/evidence/archive-ui-1792504616846/results.json), [shared-library-final.trx](../../../docs/evidence/archive-ports-20261020/tests/shared-library-final.trx), [verification.json](../../../docs/evidence/local-workspace-20261020/verification.json), [results.json](../../../docs/evidence/local-workspace-ui-1792508567673/results.json), [verification.json](../../../docs/evidence/organization-ports-20261020/verification.json), [results.json](../../../docs/evidence/organization-ui-1792509382549/results.json), [retirement.json](../../../docs/evidence/organization-ports-20261020/retirement.json), [verification.json](../../../docs/evidence/runtime-ports-20261020/verification.json), [results.json](../../../docs/evidence/runtime-ui-1792510214768/results.json)

**Criterios originales:**

- DAT-01: UI real con JSON v1/v2/legado conserva todos los campos; conflictos/malformados rechazan sin cambios; backup/restauración y reinicio verificados.
- DAT-02: Reinicio conserva biblioteca, revisiones y privacidad; no resucita aprobaciones ni efectos; recuperar de respaldo en base sintética.
- DAT-06: Restaurar build/config y copia de DB sintética compatibles, comprobar integridad y reversibilidad sin cambiar de rama sobre trabajo sin guardar.

## build

**Propietarios:** `backend/QuickPrompt.Api`, `backend/QuickPrompt.Evaluation`

**Selección:** dotnet build backend/QuickPrompt.Api/QuickPrompt.Api.csproj --no-restore; build evaluator only when its consumer changes.

**Aceptación:** Identifiable binaries and shared assembly hashes. Build success does not pass an agent workflow. Historical frozen release candidate: 730 backend, 151 shared tests and API/evaluator/web builds PASS, no skipped tests. Shared hashes equal; sources unchanged. Does not establish semantic task accuracy. Current API module build passes with 0 warnings/errors and recorded hashes. The role-context evaluator is an isolated rejected experiment, not a replacement release candidate. Archive API/Blazor builds pass with identical shared DLL hashes; web build passes with existing chunk warning. Provider API and Blazor builds pass with matching shared DLLs; frontend retains existing chunk warning. Local workspace API and Blazor builds pass; shared DLL hashes identical. Final organization API/Blazor builds pass after signature retirement, with identical shared DLLs. Runtime API/Blazor builds pass with matching shared DLLs and tested type forwarding. MAUI is not built: SDK9/workloads absent; no native PASS inferred.

**Evidencia:** [authoring-identity-candidate-v3.json](../../../docs/evidence/closure-20261020/authoring-identity-candidate-v3.json), [verification.json](../../../docs/evidence/release-validation-20261020-a1/verification.json), [verification.json](../../../docs/evidence/role-context-20261020/verification.json), [verification.json](../../../docs/evidence/archive-ports-20261020/verification.json), [verification.json](../../../docs/evidence/provider-ports-20261020/verification.json), [verification.json](../../../docs/evidence/local-workspace-20261020/verification.json), [verification.json](../../../docs/evidence/organization-ports-20261020/verification.json), [verification.json](../../../docs/evidence/runtime-ports-20261020/verification.json), [relocated-contracts.json](../../../docs/evidence/runtime-ports-20261020/relocated-contracts.json), [maui-environment.json](../../../docs/evidence/runtime-ports-20261020/maui-environment.json)

**Criterios originales:**

- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## model-transport-ownership

**Propietarios:** `backend/QuickPrompt.Infrastructure/OllamaAgentModelTransport.cs`, `backend/QuickPrompt.Infrastructure/OllamaAgentModelTransport.Reuse.cs`, `backend/QuickPrompt.Infrastructure/IAgentModelTransport.cs`, `backend/QuickPrompt.Api/ApplicationModules.cs`

**Selección:** dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore --filter "FullyQualifiedName~InferenceReuseTests|FullyQualifiedName~InferenceTransportRecoveryTests|FullyQualifiedName~ReflectedSemanticAgentTests|FullyQualifiedName~ModelTransportPortTests|FullyQualifiedName~AgentTransportCompositionTests|FullyQualifiedName~QwenThinkingSamplingTests"

**Aceptación:** 46 directed controls and 794/794 full backend tests PASS, no skips; actual API composition resolves the independent transport when constructing the old semantic interpreter deliberately fails. The base and invalidation ports share one owner; borrowed consumers do not dispose it. Original HTTP/cache algorithms preserved, API/Blazor builds PASS with identical shared DLLs. These are deterministic protocol/factory tests; no real model understanding, UI or latency qualification inferred.

**Evidencia:** [verification.json](../../../docs/evidence/model-transport-20261020/verification.json), [algorithm-preservation.json](../../../docs/evidence/model-transport-20261020/algorithm-preservation.json), [sources.json](../../../docs/evidence/model-transport-20261020/sources.json), [module-boundaries.json](../../../docs/evidence/model-transport-20261020/module-boundaries.json), [transport-directed.trx](../../../docs/evidence/model-transport-20261020/tests/transport-directed.trx), [backend-final.trx](../../../docs/evidence/model-transport-20261020/tests/backend-final.trx)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-05: Intención ≥90%; planes/destino/finalización/herramientas ≥95%; fidelidad/éxito/metamórficas ≥90%; contaminación, no autorizados, IDs inventados y cierre prematuro =0; splits congelados y holdout sin tuning.

## storage-failure-boundary

**Propietarios:** `backend/QuickPrompt.Application/StorageFailures.cs`, `backend/QuickPrompt.Infrastructure/SqliteFailureClassifier.cs`, `backend/QuickPrompt.Infrastructure/LocalPromptLibrary.cs`

**Selección:** `{"shared":"dotnet test src/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore","ui":"Tests/nova/storage-failure-ports.cjs"}`

**Aceptación:** 168/168 shared legacy tests PASS, no skips; six headed Edge checks with real SQLite trigger aborts, safe messages, transaction rollback and same-form recovery for template/version, group and instruction creation. API/Blazor builds PASS, shared DLLs identical; twelve static checks and exact consumer transformations pass. One initial harness uv invocation failure retained; product unchanged for fix. Not all error/operation modes, full security, React or model semantics. Canonical criteria unchanged.

**Evidencia:** [verification.json](../../../docs/evidence/storage-failure-20261020/verification.json), [results.json](../../../docs/evidence/storage-failure-ui-1792512968916/results.json), [shared-final.trx](../../../docs/evidence/storage-failure-20261020/tests/shared-final.trx), [module-boundaries.json](../../../docs/evidence/storage-failure-20261020/module-boundaries.json), [consumer-transformations.json](../../../docs/evidence/storage-failure-20261020/consumer-transformations.json)

**Criterios originales:**

- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.
- SEC-01: Pruebas de host/origen/CSRF/body-limit/rate-limit/rutas y errores sin filtrado de datos; sin abrir red ni relajar políticas para tests.

## host-module-substitution

**Propietarios:** `backend/QuickPrompt.Api/QuickPromptHost.cs`, `backend/QuickPrompt.Api/ApplicationModules.cs`, `backend/QuickPrompt.Evaluation/ModuleHostEvaluation.cs`

**Selección:** `{"backend":"dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore","ui":"Tests/nova/module-host.cjs"}`

**Aceptación:** 794/794 controlled backend tests PASS, no skips; 15 real HTTP/Edge checks across production CLI and two synthetic module compositions. Library CRUD works with inference absent; synthetic interpreter is consumed by the real task planner endpoint. Host/origin/client-header/cross-site checks remain; not all security endpoints/body/rate limits. Failed initial run preserved. No model understanding or full NOVA flow qualification. Current synthetic host profile migrated to explicit scope/review ports: old probe failed 503 before interpretation because its optional scope factory required disabled extraction. Meaning-only fixture now leaves scope absent and forbids production interpreter construction. Same fifteen actual HTTP/Edge criteria pass; first failure retained, production API binaries unchanged. This is QA composition qualification, not model accuracy.

**Evidencia:** [verification.json](../../../docs/evidence/host-composition-20261020/verification.json), [backend-final.trx](../../../docs/evidence/host-composition-20261020/tests/backend-final.trx), [host-sequence.json](../../../docs/evidence/host-composition-20261020/host-sequence.json), [module-boundaries.json](../../../docs/evidence/host-composition-20261020/module-boundaries.json), [canonical-source-migration.json](../../../docs/evidence/host-composition-20261020/canonical-source-migration.json), [results.json](../../../docs/evidence/module-host-ui-1792514289507/results.json), [results.json](../../../docs/evidence/module-host-ui-1792513823993/results.json), [qa-profile-migration.json](../../../docs/evidence/frontend-composition-20261020/qa-profile-migration.json), [results.json](../../../docs/evidence/module-host-ui-1792535284828/results.json), [results.json](../../../docs/evidence/module-host-ui-1792535449881/results.json), [results.json](../../../docs/evidence/module-host-ui-1792537522004/results.json), [compiled-provenance.json](../../../docs/evidence/semantic-capability-wiring-20261020/compiled-provenance.json), [results.json](../../../docs/evidence/module-host-ui-1792537988016/results.json), [compiled-provenance.json](../../../docs/evidence/legacy-inference-retirement-20261020/compiled-provenance.json), [results.json](../../../docs/evidence/module-host-ui-1792538502409/results.json), [compiled-provenance.json](../../../docs/evidence/runtime-settings-snapshot-20261020/compiled-provenance.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.
- SEC-01: Pruebas de host/origen/CSRF/body-limit/rate-limit/rutas y errores sin filtrado de datos; sin abrir red ni relajar políticas para tests.

## legacy-inference-shared-port

**Propietarios:** `backend/QuickPrompt.Infrastructure/NovaInference.cs`, `backend/QuickPrompt.Api/ApplicationModules.cs`

**Selección:** dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore --filter "FullyQualifiedName~NovaInferencePortTests|FullyQualifiedName~AgentTransportCompositionTests|FullyQualifiedName~LibraryIntegrationTests|FullyQualifiedName~NovaProposalGoalTests|FullyQualifiedName~LocalModelPolicyTests"

**Aceptación:** 30/30 final directed tests PASS, no skipped. Actual API service factory consumes borrowed semantic port without private interpreter construction; cancellation/error and original proposals/projection preserved. API and Blazor build without warnings/errors; common assemblies match; Evaluation compiled via tests. Concrete constructor/Dispose migration documented. No new browser, Qwen or full backend qualification; prior 794-test cohort remains historical for its original candidate. Provenance correction: previous frozen fixture did not compile; do not treat its old 30-case TRX as source proof. Fixture corrected and same 30 tests revalidated within 174 rebuilt tests. New build log and shared test/API/Blazor DLL hashes provide current provenance; prior evidence retained.

**Evidencia:** [verification.json](../../../docs/evidence/nova-inference-port-20261020/verification.json), [adapter-final.trx](../../../docs/evidence/nova-inference-port-20261020/tests/adapter-final.trx), [source-freeze.json](../../../docs/evidence/nova-inference-port-20261020/source-freeze.json), [invalidation.json](../../../docs/evidence/nova-inference-port-20261020/invalidation.json), [compiled-provenance.json](../../../docs/evidence/ui-decision-module-20261020/compiled-provenance.json), [ui-final.trx](../../../docs/evidence/ui-decision-module-20261020/tests/ui-final.trx)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## public-contract-metadata

**Propietarios:** `backend/QuickPrompt.ContractExport`, `src/QuickPrompt.Domain/LocalModelPolicy.cs`, `scripts/nova-contract-metadata.cjs`, `scripts/nova-openapi.cjs`

**Selección:** node Tests/nova/contract-metadata.cjs docs/evidence/contract-metadata-20261020/rerun.json

**Aceptación:** Seven final checks PASS, including seven invalid payloads, exporter failure before write, all five schema positions plus live default, repeated byte-identical generation and synthetic metadata substitution restored to original. Same 75-operation public artifact SHA256; 15 static architecture checks PASS. Initial static failure found third unconverted model list/default; retained and corrected with broader substitution coverage. No model discovery/inference, user data or frontend behavior changed; only selected model/enum metadata is reflected, not all schemas.

**Evidencia:** [verification-v2.json](../../../docs/evidence/contract-metadata-20261020/verification-v2.json), [module-boundaries-v2.json](../../../docs/evidence/contract-metadata-20261020/module-boundaries-v2.json), [module-boundaries.json](../../../docs/evidence/contract-metadata-20261020/module-boundaries.json), [source-freeze-v2.json](../../../docs/evidence/contract-metadata-20261020/source-freeze-v2.json), [model-policy-verification-v2.json](../../../docs/evidence/contract-metadata-20261020/model-policy-verification-v2.json), [metadata-equivalence.json](../../../docs/evidence/model-policy-ownership-20261020/metadata-equivalence.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## ui-selector-module-isolation

**Propietarios:** `backend/QuickPrompt.Infrastructure/UiDecisionModel.cs`, `src/QuickPrompt.Domain/LocalModelPolicy.cs`, `backend/QuickPrompt.Api/ApplicationModules.cs`

**Selección:** dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj --no-restore --filter "FullyQualifiedName~UiDecisionCompositionTests|FullyQualifiedName~UiDecisionTransportTests|FullyQualifiedName~UiAgentResourcesTests|FullyQualifiedName~InferenceTransportRecoveryTests|FullyQualifiedName~InferenceReuseTests|FullyQualifiedName~LiveExecutionTests|FullyQualifiedName~SameTabUiTests"

**Aceptación:** Actual module factory resolves and uses UI selector with interpreter and authoring client forbidden; same selection algorithm extracted, one compatibility delegation, borrowed transport. Final 174-test combined directed cohort PASS, including six real loopback HTTP variants through independent and compatibility paths. Source freeze, compile/run log and shared binaries match. No frontend/model semantics qualification. Initial compilation failure in two fixtures preserved and corrected; old adapter cohort revalidated. 16 static checks and seven metadata regressions PASS; public OpenAPI unchanged.

**Evidencia:** [verification.json](../../../docs/evidence/ui-decision-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/ui-decision-module-20261020/compiled-provenance.json), [ui-final.trx](../../../docs/evidence/ui-decision-module-20261020/tests/ui-final.trx), [module-boundaries.json](../../../docs/evidence/ui-decision-module-20261020/module-boundaries.json), [incidents.json](../../../docs/evidence/ui-decision-module-20261020/incidents.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## candidate-selection-ownership

**Propietarios:** `backend/QuickPrompt.Infrastructure/SemanticCandidateSelector.cs`, `backend/QuickPrompt.Infrastructure/AgentResponseContent.cs`, `backend/QuickPrompt.Api/ApplicationModules.cs`, `backend/QuickPrompt.Application/StructuredIntentCompatibility.cs`, `backend/QuickPrompt.Application/AgentOrchestrator.cs`

**Selección:** Use filter in docs/evidence/candidate-selection-module-20261020/test-selection.json; 48 affected class references, rebuild before run.

**Aceptación:** 625/625 directed compiled tests PASS, no skipped. Actual factory resolves and uses independent selector when interpreter/editor construction is forbidden. Eighteen controlled HTTP variants; six new factory/input/authority cases. Ranked compatibility authority removed: absent capability cannot select score999, explicit identity still works, supplied/published membership wins over scores. Shared parser bodies and selection prompt/method unchanged. API/Blazor build without warnings/errors, shared DLLs equal tested binaries. Seventeen static checks pass. Initial 220/222 and later fixture compilation failure preserved; explicit test membership replaces hidden score dependency without weakening assertions. No real-model, browser or latency qualification; C# compatibility behavior change documented.

**Evidencia:** [verification.json](../../../docs/evidence/candidate-selection-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/candidate-selection-module-20261020/compiled-provenance.json), [selection-final.trx](../../../docs/evidence/candidate-selection-module-20261020/tests/selection-final.trx), [test-selection.json](../../../docs/evidence/candidate-selection-module-20261020/test-selection.json), [extraction.json](../../../docs/evidence/candidate-selection-module-20261020/extraction.json), [incidents.json](../../../docs/evidence/candidate-selection-module-20261020/incidents.json), [module-boundaries.json](../../../docs/evidence/candidate-selection-module-20261020/module-boundaries.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## input-extraction-ownership

**Propietarios:** `backend/QuickPrompt.Infrastructure/PromptInputValueExtractor.cs`, `backend/QuickPrompt.Evaluation/Compatibility/PreparationInference.cs`, `backend/QuickPrompt.Application/AgentOrchestrator.cs`, `backend/QuickPrompt.Api/ApplicationModules.cs`

**Selección:** Use filter in docs/evidence/input-extraction-module-20261020/test-selection.json; 44 affected classes, compile before run.

**Aceptación:** 582/582 directed compiled tests PASS, no skipped/warnings. Actual factory resolves extraction with interpreter/editor forbidden; explicit extractor completes/compiles fields while generator does not implement extraction, and domain rejects invented values. Borrowed extractor survives compatibility interpreter disposal. Method/prompt/schema/observation unchanged. API/Blazor build without warnings/errors; shared DLLs equal tested binaries; Evaluation compiled via tests. Eighteen static checks pass and OpenAPI unchanged. Initial 23-case warning retained and corrected only in fixture assertion. Optional constructor extension/source compatibility and binary recompile documented; no new real-model/browser/latency acceptance.

**Evidencia:** [verification.json](../../../docs/evidence/input-extraction-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/input-extraction-module-20261020/compiled-provenance.json), [inputs-final.trx](../../../docs/evidence/input-extraction-module-20261020/tests/inputs-final.trx), [test-selection.json](../../../docs/evidence/input-extraction-module-20261020/test-selection.json), [extraction.json](../../../docs/evidence/input-extraction-module-20261020/extraction.json), [incidents.json](../../../docs/evidence/input-extraction-module-20261020/incidents.json), [module-boundaries.json](../../../docs/evidence/input-extraction-module-20261020/module-boundaries.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## input-scope-ownership

**Propietarios:** `backend/QuickPrompt.Infrastructure/PromptInputScopeInterpreter.cs`, `backend/QuickPrompt.Application/SemanticIntent.cs`, `backend/QuickPrompt.Application/TaskPlanning.cs`, `backend/QuickPrompt.Application/TaskSourceResolver.cs`, `backend/QuickPrompt.Api/ApplicationModules.cs`

**Selección:** Use filter in docs/evidence/input-scope-module-20261020/test-selection.json; 53 affected classes, compile before running.

**Aceptación:** 662/662 directed compiled tests functionally PASS, no skipped. Fifteen new cases cover factory/provider/context and fresh-source revision race; meaning-only planner uses explicit scope port and four HTTP variants preserve bounded repair. Scope body and context projection rules unchanged; old partial retired with archived source. API/Blazor compile without warnings/errors and share tested DLL hashes; Evaluation compiled with tests. Nineteen static checks pass, one historical native source guard is not native qualification. Timing anomaly retained: one controlled timeout test took 3:02:27.57, same binary isolated rerun 1.2705289s; cause unconfirmed, no latency/deadline regularity acceptance. No new Qwen/UI campaign. User selected web-only; MAUI excluded, no installation pending.

**Evidencia:** [verification.json](../../../docs/evidence/input-scope-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/input-scope-module-20261020/compiled-provenance.json), [scope-final.trx](../../../docs/evidence/input-scope-module-20261020/tests/scope-final.trx), [timeout-recheck.trx](../../../docs/evidence/input-scope-module-20261020/tests/timeout-recheck.trx), [test-selection.json](../../../docs/evidence/input-scope-module-20261020/test-selection.json), [extraction.json](../../../docs/evidence/input-scope-module-20261020/extraction.json), [scope-decision.json](../../../docs/evidence/input-scope-module-20261020/scope-decision.json), [module-boundaries.json](../../../docs/evidence/input-scope-module-20261020/module-boundaries.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## fidelity-review-ownership

**Propietarios:** `backend/QuickPrompt.Application/DraftFidelityReview.cs`, `backend/QuickPrompt.Infrastructure/DraftFidelityReviewer.cs`, `backend/QuickPrompt.Infrastructure/AgentStagePacket.cs`, `backend/QuickPrompt.Evaluation/Compatibility/PreparationInference.cs`, `backend/QuickPrompt.Api/ApplicationModules.cs`

**Selección:** Use filter in docs/evidence/fidelity-review-module-20261020/test-selection.json; compile before running, 54 affected classes.

**Aceptación:** 666/666 directed compiled tests PASS, no skips; four new composition cases cover actual factory with interpreter/editor prohibited, both metadata profiles, cancellation/unavailability and borrowed reviewer delegation. Existing HTTP repair/authority checks preserved. Old partial retired with archived source, one reviewer call and unchanged budget/rules. API/Blazor builds have zero warnings/errors and match tested shared DLLs; Evaluation compiled with tests. Twenty static checks pass; historical native source guard does not qualify MAUI. Initial source-comparison and rejected invocation incident retained, no tests executed in that failed invocation. Optional reviewer metadata identity changes, no linguistic qualification inferred. No new UI/Qwen campaign; authoring remains FAIL, canonical 34/54 unchanged. Web-only scope.

**Evidencia:** [verification.json](../../../docs/evidence/fidelity-review-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/fidelity-review-module-20261020/compiled-provenance.json), [review-final.trx](../../../docs/evidence/fidelity-review-module-20261020/tests/review-final.trx), [test-selection.json](../../../docs/evidence/fidelity-review-module-20261020/test-selection.json), [extraction.json](../../../docs/evidence/fidelity-review-module-20261020/extraction.json), [incidents.json](../../../docs/evidence/fidelity-review-module-20261020/incidents.json), [module-boundaries.json](../../../docs/evidence/fidelity-review-module-20261020/module-boundaries.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## draft-generation-ownership

**Propietarios:** `backend/QuickPrompt.Application/Preparation.cs`, `backend/QuickPrompt.Infrastructure/PromptDraftGenerator.cs`, `backend/QuickPrompt.Infrastructure/PromptDraftGenerator.Authoring.cs`, `backend/QuickPrompt.Api/ApplicationModules.cs`

**Selección:** Use filter in docs/evidence/draft-generation-module-20261020/test-selection.json; compile before running, 55 affected classes.

**Aceptación:** 676/676 compiled directed tests PASS, no skips; ten new cases prove actual factory without interpreter, both metadata profiles, editor/extractor/reviewer composition, identity/revision rejection, cancellation/model availability, fidelity rejection, legacy port compatibility and concurrent diagnostic isolation. HTTP repair/authority tests preserved. Full generation body/schema/instructions retained after declared port/callback changes, partial retired and source archived. API/Blazor builds have zero warnings/errors and tested shared DLL hashes; Evaluation compiled with tests and wire capture consumer migrated. Twenty-one static checks PASS, including historical native source guard only. No model understanding, new UI or latency acceptance; authoring FAIL and canonical 34/54 remain. Web-only scope.

**Evidencia:** [verification.json](../../../docs/evidence/draft-generation-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/draft-generation-module-20261020/compiled-provenance.json), [generation-final.trx](../../../docs/evidence/draft-generation-module-20261020/tests/generation-final.trx), [test-selection.json](../../../docs/evidence/draft-generation-module-20261020/test-selection.json), [extraction.json](../../../docs/evidence/draft-generation-module-20261020/extraction.json), [module-boundaries.json](../../../docs/evidence/draft-generation-module-20261020/module-boundaries.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## semantic-interpretation-ownership

**Propietarios:** `backend/QuickPrompt.Infrastructure/SemanticIntentInterpreter.cs`, `backend/QuickPrompt.Infrastructure/SemanticIntentInterpreter.Stages.cs`, `backend/QuickPrompt.Infrastructure/SemanticIntentInterpreter.InstructionUse.cs`, `backend/QuickPrompt.Infrastructure/SemanticIntentInterpreter.Sources.cs`, `backend/QuickPrompt.Evaluation/Compatibility/PreparationInference.cs`, `backend/QuickPrompt.Api/ApplicationModules.cs`

**Selección:** Use filter in docs/evidence/semantic-interpreter-module-20261020/test-selection.json; compile before running, 56 affected classes.

**Aceptación:** 688/688 directed compiled tests PASS, no skips; six new composition cases prove actual semantic factory without facade/generator/editor/field/reviewer capabilities, shared aliases and traits, cancellation/unavailability, borrowed selection/lifecycle and compatibility. Six original authoring scenarios now run both directly and through facade: twelve real loopback HTTP probes with controlled replies and unchanged behavioral expectations. Six old partials retired and archived; semantic bodies, instructions, understanding schema and packet algorithm preserved after owner rename. API/Blazor builds without warnings/errors and match tested shared DLL hashes; Evaluation compiled with tests. Twenty-two static checks PASS, historical native source guard does not qualify MAUI. No Qwen, UI or latency acceptance inferred, no new semantic algorithm promoted; canonical 34/54 and agent failure statuses unchanged. Web-only scope.

**Evidencia:** [verification.json](../../../docs/evidence/semantic-interpreter-module-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/semantic-interpreter-module-20261020/compiled-provenance.json), [semantic-final.trx](../../../docs/evidence/semantic-interpreter-module-20261020/tests/semantic-final.trx), [test-selection.json](../../../docs/evidence/semantic-interpreter-module-20261020/test-selection.json), [extraction.json](../../../docs/evidence/semantic-interpreter-module-20261020/extraction.json), [owner-migration.json](../../../docs/evidence/semantic-interpreter-module-20261020/owner-migration.json), [module-boundaries.json](../../../docs/evidence/semantic-interpreter-module-20261020/module-boundaries.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## local-client-resource-ownership

**Propietarios:** `backend/QuickPrompt.Infrastructure/Local/OllamaClient.cs`, `backend/QuickPrompt.Api/ApplicationModules.cs`, `src/QuickPrompt.Web/Program.cs`

**Selección:** Use project-specific filters in docs/evidence/local-client-lifecycle-20261020/test-selection.json; compile and run backend then shared tests serially.

**Aceptación:** 152/152 backend and 44/44 shared directed compiled tests PASS, no skips; six lifecycle cases. Original actual API service-container disposal test fails before and passes after. Shared owned-client factory preserves real loopback availability/streaming with both host context profiles, borrowed HTTP remains usable, owner disposal cancels an inflight request, construction validation errors preserved. Construction-failure resource cleanup inspected, not instrumented. API/Blazor builds without warnings/errors and shared DLL hashes equal both tested projects; Evaluation compiles with backend. Twenty-three static checks PASS; historical native source guard is not MAUI qualification. Initial product failure and synthetic fixture compilation error retained. Inference bodies/constructor unchanged; no real-model, new UI, latency or full release qualification inferred. Personal services not restarted, canonical 34/54 and agent failure statuses unchanged. Web-only scope.

**Evidencia:** [verification.json](../../../docs/evidence/local-client-lifecycle-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/local-client-lifecycle-20261020/compiled-provenance.json), [lifecycle-before.trx](../../../docs/evidence/local-client-lifecycle-20261020/tests/lifecycle-before.trx), [lifecycle-backend-final.trx](../../../docs/evidence/local-client-lifecycle-20261020/tests/lifecycle-backend-final.trx), [lifecycle-shared-final.trx](../../../docs/evidence/local-client-lifecycle-20261020/tests/lifecycle-shared-final.trx), [test-selection.json](../../../docs/evidence/local-client-lifecycle-20261020/test-selection.json), [algorithm-preservation.json](../../../docs/evidence/local-client-lifecycle-20261020/algorithm-preservation.json), [incidents.json](../../../docs/evidence/local-client-lifecycle-20261020/incidents.json), [module-boundaries.json](../../../docs/evidence/local-client-lifecycle-20261020/module-boundaries.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## model-policy-ownership

**Propietarios:** `src/QuickPrompt.Domain/LocalModelPolicy.cs`, `src/QuickPrompt.Domain/NovaTelemetry.cs`, `backend/QuickPrompt.Application/LiveExecution.cs`, `backend/QuickPrompt.Application/AgentOrchestrator.cs`, `backend/QuickPrompt.Api/PreparationEndpoints.cs`, `backend/QuickPrompt.Infrastructure/ForwardedLocalContracts.cs`, `backend/QuickPrompt.ContractExport`

**Selección:** Use project-specific filters in docs/evidence/model-policy-ownership-20261020/test-selection.json; backend/shared tests serially, then frozen API/Blazor builds and Tests/nova/model-policy.cjs with MODEL_POLICY_API_BUILD.

**Aceptación:** 550 backend and 112 shared directed tests PASS, no skips; 17 new policy cases. Four actual HTTP admission/default checks over owned synthetic SQLite pass without inference. Seven real exporter checks pass, metadata/public 75-operation artifact byte-identical and exporter has only Application/Domain with zero packages. Old compiled consumer unchanged runs against forwarded new types. Namespace/wire/defaults/admission preserved; same Domain/Application/Infrastructure hashes in both tested projects and both hosts, builds zero warnings/errors, Evaluation compiled with backend. 25 static checks PASS. Retired policy source archived; stale test dependency assertion migrated with transitive check. Binary probe retained two MSB3277 warnings, distinct from product builds. No frontend/Qwen/latency qualification, canonical 34/54 unchanged, web-only scope.

**Evidencia:** [verification.json](../../../docs/evidence/model-policy-ownership-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/model-policy-ownership-20261020/compiled-provenance.json), [source-freeze-v2.json](../../../docs/evidence/model-policy-ownership-20261020/source-freeze-v2.json), [binary-compatibility.json](../../../docs/evidence/model-policy-ownership-20261020/binary-compatibility.json), [test-selection.json](../../../docs/evidence/model-policy-ownership-20261020/test-selection.json), [incidents.json](../../../docs/evidence/model-policy-ownership-20261020/incidents.json), [module-boundaries.json](../../../docs/evidence/model-policy-ownership-20261020/module-boundaries.json), [results.json](../../../docs/evidence/model-policy-http-1792536875126/results.json), [model-policy-verification-v2.json](../../../docs/evidence/contract-metadata-20261020/model-policy-verification-v2.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## semantic-capability-wiring

**Propietarios:** `backend/QuickPrompt.Api/ApplicationModules.cs`, `backend/QuickPrompt.Application/AgentOrchestrator.cs`, `backend/QuickPrompt.Application/AgentOrchestrator.Semantic.cs`, `backend/QuickPrompt.Application/AgentOrchestrator.Visible.cs`, `backend/QuickPrompt.Application/TaskPlanning.cs`, `backend/QuickPrompt.Application/TaskSourceResolver.cs`

**Selección:** Use backend filter in docs/evidence/semantic-capability-wiring-20261020/test-selection.json; compile before running and use same frozen hosts for Tests/nova/module-host.cjs.

**Aceptación:** 388/388 directed backend tests PASS across 19 classes, zero skipped, 19 new cases. Four alias checks fail on original composition. Same compiled 17-case cohort fails against old cores and passes against corrected cores. Advice manual/visible, contextual selection, reference and clause review use separate registered ports, preserving identity/revision/subset checks and cancellation. Two tests traverse real registered semantic owner to validated transport with controlled answers. Fifteen actual Edge/HTTP host module isolation/CRUD/protection checks pass; QA profile migrated to four explicit optional ports without production fallback. API/Blazor/evaluator zero warnings/errors and same shared DLL hashes as tested binaries. 26 static checks PASS; historical native guard is not MAUI qualification. Initial fixture compilation error and real failures retained. Algorithms/instructions/public JSON unchanged. No Qwen accuracy, latency, new frontend unit-suite or release acceptance; canonical 34/54 unchanged. Web-only scope.

**Evidencia:** [verification.json](../../../docs/evidence/semantic-capability-wiring-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/semantic-capability-wiring-20261020/compiled-provenance.json), [workflow-regression.json](../../../docs/evidence/semantic-capability-wiring-20261020/workflow-regression.json), [source-freeze-v2.json](../../../docs/evidence/semantic-capability-wiring-20261020/source-freeze-v2.json), [test-selection.json](../../../docs/evidence/semantic-capability-wiring-20261020/test-selection.json), [incidents.json](../../../docs/evidence/semantic-capability-wiring-20261020/incidents.json), [module-boundaries.json](../../../docs/evidence/semantic-capability-wiring-20261020/module-boundaries.json), [wiring-before.trx](../../../docs/evidence/semantic-capability-wiring-20261020/tests/wiring-before.trx), [wiring-old-workflow.trx](../../../docs/evidence/semantic-capability-wiring-20261020/tests/wiring-old-workflow.trx), [wiring-new-workflow-same-binary.trx](../../../docs/evidence/semantic-capability-wiring-20261020/tests/wiring-new-workflow-same-binary.trx), [wiring-final-v2.trx](../../../docs/evidence/semantic-capability-wiring-20261020/tests/wiring-final-v2.trx), [results.json](../../../docs/evidence/module-host-ui-1792537522004/results.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## legacy-aggregate-retirement

**Propietarios:** `backend/QuickPrompt.Api/ApplicationModules.cs`, `backend/QuickPrompt.Infrastructure/AgentStagePacket.cs`, `backend/QuickPrompt.Evaluation/Compatibility/PreparationInference.cs`

**Selección:** Use backend filter in docs/evidence/legacy-inference-retirement-20261020/test-selection.json; compile and use frozen API/evaluator plus unchanged frozen React assets for Tests/nova/module-host.cjs.

**Aceptación:** 457/457 directed backend tests PASS across 46 classes, zero omitted; two new retirement checks fail before and pass after. Legacy aggregate removed from production assembly and DI; byte-identical compatibility source only in Evaluation, no independent algorithms copied. Pure packet codec visibility only changed, no friend/dependency cycle. API/Blazor artifacts contain no Evaluation DLL or deps entry; common DLLs equal tested binaries. API/Blazor/evaluator zero warnings/errors. Fifteen actual Edge/HTTP host normal/disabled/replacement CRUD/protection checks PASS. Four CLI-exported contract/capability files and public 75-operation OpenAPI identical; seven metadata checks and 27 static guards PASS. Evaluator owner hashes corrected; actual model campaigns not rerun. Original absence failures and stale static guard preserved. Intentional documented .NET type/API retirement, HTTP unchanged; no new frontend unit/model/latency qualification, canonical 34/54 unchanged, web-only scope.

**Evidencia:** [verification.json](../../../docs/evidence/legacy-inference-retirement-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/legacy-inference-retirement-20261020/compiled-provenance.json), [source-and-contract-equivalence.json](../../../docs/evidence/legacy-inference-retirement-20261020/source-and-contract-equivalence.json), [source-freeze.json](../../../docs/evidence/legacy-inference-retirement-20261020/source-freeze.json), [test-selection.json](../../../docs/evidence/legacy-inference-retirement-20261020/test-selection.json), [incidents.json](../../../docs/evidence/legacy-inference-retirement-20261020/incidents.json), [module-boundaries.json](../../../docs/evidence/legacy-inference-retirement-20261020/module-boundaries.json), [retirement-before.trx](../../../docs/evidence/legacy-inference-retirement-20261020/tests/retirement-before.trx), [retirement-final.trx](../../../docs/evidence/legacy-inference-retirement-20261020/tests/retirement-final.trx), [results.json](../../../docs/evidence/module-host-ui-1792537988016/results.json), [legacy-retirement-verification.json](../../../docs/evidence/contract-metadata-20261020/legacy-retirement-verification.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

## runtime-settings-snapshot

**Propietarios:** `backend/QuickPrompt.Api/ApplicationModules.cs`, `backend/QuickPrompt.Api/NovaAgentSettings.cs`

**Selección:** Use actual factory cohort in docs/evidence/runtime-settings-snapshot-20261020/test-selection.json; frozen API/evaluator builds with Tests/nova/module-host.cjs, unchanged React assets.

**Aceptación:** 84/84 backend factory-consumer tests PASS across 13 classes, no omitted tests. Four new regression cases fail before and pass after with unchanged expectations: deferred stage flags/metadata, distinct container profiles, actual editor/transport/embeddings HTTP destination after IConfiguration mutation. Snapshot is per module registration, no global options or new service framework; technical options require recomposition while domain preferences retain owner/lifecycle. Fifteen host Edge/HTTP normal/disabled/replacement checks PASS. API/Blazor/evaluator zero warnings/errors, shared DLLs equal tested binaries; Domain/Application/Infrastructure unchanged from previous qualified artifact. Fifteen existing defaults plus budget ranges/messages preserved. 28 static controls PASS; OpenAPI/canonical matrix SHA unchanged, metadata not regenerated. No claim of transactional capture during concurrent registration-time provider mutation. No Qwen fidelity, latency or new frontend unit-suite acceptance; web-only, canonical 34/54 unchanged.

**Evidencia:** [verification.json](../../../docs/evidence/runtime-settings-snapshot-20261020/verification.json), [compiled-provenance.json](../../../docs/evidence/runtime-settings-snapshot-20261020/compiled-provenance.json), [default-preservation.json](../../../docs/evidence/runtime-settings-snapshot-20261020/default-preservation.json), [source-freeze.json](../../../docs/evidence/runtime-settings-snapshot-20261020/source-freeze.json), [test-selection.json](../../../docs/evidence/runtime-settings-snapshot-20261020/test-selection.json), [module-boundaries.json](../../../docs/evidence/runtime-settings-snapshot-20261020/module-boundaries.json), [snapshot-before.trx](../../../docs/evidence/runtime-settings-snapshot-20261020/tests/snapshot-before.trx), [snapshot-after.trx](../../../docs/evidence/runtime-settings-snapshot-20261020/tests/snapshot-after.trx), [snapshot-final.trx](../../../docs/evidence/runtime-settings-snapshot-20261020/tests/snapshot-final.trx), [results.json](../../../docs/evidence/module-host-ui-1792538502409/results.json)

**Criterios originales:**

- REL-01: Repetir si cambia código relevante y en el candidato congelado; no convertir tests de transporte en precisión semántica.
- REL-03: Desde candidato congelado, compilar API/evaluador/TypeScript/Vite y suite legacy compartida sin errores; registrar advertencias, hashes y comandos.

