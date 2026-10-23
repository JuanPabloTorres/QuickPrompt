# Semantic stabilization — S01

Status: in progress; experimental interpretation remains disabled. No acceptance claim.

## Frozen evidence and diagnosis

S01 preserves R53 source, compiled API/evaluation/web, dataset/threshold hashes and model profile in `docs/evidence/stabilization-v1/S01-baseline-manifest.json` and `.nova-build/stabilization/baseline`. Baseline checks: backend 375, legacy 150, React 86 passed. Real-Qwen headed creation reproduction: `docs/evidence/latency-1792321312092`, failed, 62,414 ms, three calls. Its semantic plan contains CREATE_DRAFT **and unrequested COPY_PROMPT**; native creation is not the earliest defect.

R51 development failures have different immediate causes: library lookup was classified as opening a recalled candidate; content preservation was misclassified as an application editing prohibition; a missing conversational antecedent became a library query and the selector chose an unrelated fallback record. R52 real output also repeated an incomplete source constraint, and its independent reviewer accepted workflow contamination. R53 additionally had an editor destination-revision mismatch, already corrected and proven only by controlled approval/rejection replay; the real semantic run still failed before approval.

Shared boundaries needing correction:

1. Interpretation mixes requested results, implementation steps, authorship and denied capabilities. An actual quote proves provenance, not semantic entailment: reusing a creation quote cannot prove copy readiness.
2. `RequiredEffects` currently contains compiler dependencies. There is no independent requested-root ceiling against which to verify a later plan.
3. Retrieval fallback proves existence, not reference grounding. A real ID can still be an arbitrary antecedent.
4. Visible conversation recording returns immediately for source-free drafts, and the proposal endpoint assumes every working draft has a saved source ID. This breaks unsaved follow-ups independently of interpretation.
5. A favorable semantic review is not sufficient evidence of faithful generated prose. Literal/source invariants, exact artifact identity, native observations and approval receipts remain independently necessary.

## Falsifiable candidate H1

A smaller result-oriented interpretation, with application dependencies and denied capabilities derived by code, should eliminate unrequested readiness in the reproduced creation, improve the complete frozen development split, and preserve search, adaptation and multistage readiness. This is a hypothesis, not a proven remedy. No phrase exceptions, keyword routers or request regexes will be added.

The candidate uses the same inference/repair budget and model profile. Each requested result owns its evidence and, only for authoring, its content instruction; provider identity belongs to the provider result. The model no longer enumerates application prohibitions from content constraints. Unrequested terminal effects are denied by the domain. Multiple explicitly requested results remain representable. The existing decoder is retained for controlled comparison and the default production interpreter remains unchanged.

Separately, process-owned `RequestedEffects` must distinguish roots from deterministic dependencies; completion must reject both extra and omitted effects. Reference resolution must not promote unrelated fallback records into authorized sources. Session checkpoints must retain an unsaved artifact without inventing a persisted identity or revision. Existing hash, approval, single-use and fresh-revision checks remain mandatory.

## Evaluation protocol

Finish all 16 frozen development cases for baseline and candidate, including both metamorphic groups. Preserve original metrics/thresholds; additionally measure unsolicited roots separately because the old forbidden-subset oracle does not detect every extra action. Unexecuted browser/content/multiturn groups remain NOT_TESTED. No validation or holdout investigation before development passes. No latency comparison without stable resources. Recorded replies and mocks prove mechanics only, never Qwen comprehension.

Required final gates remain separate: semantic, multiturn, content fidelity, approval, performance. No global success or experimental enablement until the existing gates pass.

## S03 observations and next falsifiable change

The frozen S03 campaign continues through all 16 cases. Its first lookup returned the correct SOURCE_FOUND interpretation, but the newly added positive-literal-score requirement blocked a real plural/singular semantic match. That requirement is refuted: literal retrieval scores are recall signals, not proof of reference authority. Keep semantic selection over bounded actual records; reject foreign IDs, multiple matches and unresolved references rather than requiring literal word overlap. Initial recall hints still exclude unrelated recent/favorite fallback rows.

S03 also declared NEW_DRAFT with a library lookup describing the new authored topic, and declared a redundant lookup alongside an edit. H2 is a typed-contract/compiler correction, not another phrase-specific prompt: independent NEW_DRAFT must have no source; expressly source-derived creation has its own result kind. Reject inconsistent combinations within the existing repair budget. Reduce requested roots by the compiler's dependency graph, retaining the complete executable plan and original model contracts for audit. A lookup needed by adaptation is a dependency; copy readiness, execution, provider preparation and saving cannot be removed as dependencies of simple creation. Compare the complete frozen development split again. No threshold or historical metric changes.

S03 mechanics: 392 backend, 150 legacy and 87 React checks passed; API/evaluation/frontend builds passed. Initial concurrent React execution hit an existing workspace-test timeout; isolated full rerun passed without changing timeouts. The initial stricter content check rejected a complete, single protected clause unnecessarily; it now rejects actual duplication and truncated suffixes while preserving complete valid source clauses. Both failures and passing runs are retained under `docs/evidence/stabilization-checks`.

Full S03 result: **rejected**, intent 12/16 (75%), valid plans 15/16 (93.75%), targets 13/16 (81.25%), metamorphic groups 1/2. The new contract alone is worse than the frozen S01 baseline; it is not enabled. Failed cases and raw contracts remain in `docs/evidence/stabilization-v1/S03-development.json`.

H2 additionally closes the boundaries exposed by this full campaign:

| Failure class | Code/contract rule | Independent evidence required |
|---|---|---|
| Optional question silently blocks a defined action | Only CLARIFICATION owns a question; other result shapes cannot contain it | Actual missing information, never an optional preference |
| Repair turns preparation into creation/execution | Repair result kinds are capped by the initial kinds plus safe clarification | Same original directive/context; no new permission from feedback |
| Multiple input fields become duplicate capabilities | Non-authoring goals are idempotent sets; conflicting authoring/destinations still fail | Actual field names and literal grounded values |
| Procedural search/open choice distracts from the requested read | SOURCE_READ requests the artifact; code derives lookup/navigation; MATCHES_ONLY forbids opening | Actual reference and observed source/results |
| Missing pronoun resolves against a recent library item | Existing semantic selector first resolves the reference type; a candidate list does not create an antecedent | Exact actual source mention or code-bound current/working context |
| Cross-language or inflected lookup has no literal score | Semantic reference selection remains available over bounded real records | No invented IDs, verified ambiguity and fresh revision |

Reference verification replaces the existing selection stage; it adds no extra inference stage. Its meaning judgment remains probabilistic and must pass real evaluation. Deterministic checks bind quoted mentions and context roles; they do not pretend to prove semantic entailment. Existing production decoding remains the fallback, with both experimental flags false by default.

## S04 integration gate: rejected

Full frozen S04 result: intention **8/16**, valid plans **11/16**, targets **8/16**, both search/adaptation metamorphic groups failed. S04 is worse than S01 and S03. The six source-reference selection calls cost 22.647 s cumulatively; four rejected quotes copied library bodies rather than user evidence, and one selected the wrong contextual result. The typed contract also allowed EDITED_DRAFT without a source, confused contextual adaptation with new/derived creation, and prepared a provider instead of local use. Repair capped result categories but did not generally freeze source or retain all requested results.

Following the user's S04 integration gate, stop architectural expansion and compare the existing legacy decoder on all sixteen unchanged development cases. Do not add another inference layer, keyword/regex exception, vectors or a new orchestrator. Keep the failed candidate frozen and disabled. [The integration report](S04_INTEGRATION_GATE.md) records the implemented workflow, contracts, alternatives, browser evidence and remaining acceptance gaps. Validation and holdout remain untouched.

Phase 5 continues that causal audit using the installed personal `algorithmic-engineering` skill. [The causal report](PHASE5_CAUSAL_ISOLATION.md) contains first-divergence findings, synchronized workflow/pseudocode, complete snapshot comparisons, original nine-call profiling, mechanical replay, real UI results and remaining coverage gaps. It preserves S01/S03/S04 and the rejected Phase 5a result rather than replacing them with unit-test totals. Three boundary corrections use four existing production files; no new interpreter, dependency or public endpoint is introduced.
