# Semantic recovery — baseline and frozen criteria

Scope: synthetic local data, Qwen3:8b digest `500a1f067a9f782620b40bee6f7b0c89e17ae61f686b92c24933e4ca4b2b8b41`, context 12288, SemanticThinking=true. No installs, global Ollama changes, personal writes, commits or publication.

## First mandatory real browser request

“Encuentra el prompt que utilizo para redactar correos, conviértelo en una versión breve para solicitar una entrevista y déjalo listo para copiar. No modifiques el original.”

Evidence: `docs/evidence/latency-1792289725643/{environment.json,measurements.json,validation.json}` plus screenshots and videos. Same two-record fixture as the previous baseline: `.playwright/latency-1792283279133/synthetic.sqlite`. Reuse disabled; fast mode; one real headed Edge Page per run. Original artifacts are retained.

| Run | Model state | Wall ms | Calls | Result |
|---|---|---:|---:|---|
| 1c83e416-4985-49ce-977b-752017650190 | cold | 164505 | 23 | FAILED / UI_REPORTED_ERROR |
| eb55e6eb-4324-4700-af1d-b4ed2bc0507e | warm | 89568 | 22 | AWAITING_APPROVAL, oracle passed |
| 51c9a02b-7b8a-4eb3-8466-4d2257605573 | warm | 88744 | 22 | AWAITING_APPROVAL, oracle passed |

Cold failure is **POSTCONDITION_ERROR**, not a broken click: the screenshot shows “La propuesta no redujo la longitud del prompt. El original se conserva.” The domain rejected two generations after restoring source constraints. No write occurred. The UI driver correctly propagated an error, but the generic outward error code hides its origin. Model load on first call was 13075.8 ms. Do not count this failed run as a fast successful result or remove it from latency reporting.

Resource observation before campaign: RAM total 16395244 KiB, free 1128016 KiB, GPU total 8188 MiB, used 0 MiB, temperature 50°C. This is constrained memory; snapshots do not establish a sampled peak or prove absence of paging. Existing unrelated applications were left running.

## Principal causes before candidate tuning

1. **Repeated semantic decisions:** named-source resolution reruns the entire interpreter. Warm run 2 spends 16343.6 + 21919.3 ms on two content-goal decisions. Source loading supplies real body/slots; it does not justify discarding and reclassifying already authorized user goals.
2. **Generation / retries:** warm run 2 generation costs 20503.1 ms; cold run's two editor calls cost 32887 and 54338 ms. Restoring protected constraints can invalidate requested shortening. A bounded domain rejection is necessary; retries are not successes.
3. **Independent classifiers and UI decisions:** 19 interpretation/selection calls precede two tool decisions (3349.7 and 2879.6 ms) in warm run 2. Existing identity/revision/available-tool facts already determine those UI transitions.

The full per-call tokens, duration, model options, timeline and tool identity are in `measurements.json`, joined by RunId. No model content or thinking was written to these production-like traces.

| Call / stage | New information | Decision | Treatment |
|---|---|---|---|
| ResolveReference | request/session | source role | merge into coherent goal interpretation |
| ExtractNamedReference | same request | retrieval query | merge |
| InterpretDestination | same request | provider/readiness/execution | merge; retain typed authorization evidence |
| ReviewExecutionScope | same request | permission narrowing | validate scoped evidence + typed goal; genuine new ambiguity may need repair |
| InterpretTextGoal | same request | draft delta | keep semantic inference, combined contract |
| ReviewAuthoringScope | same request | second draft classification | merge; validate clause evidence |
| InterpretInputUse | request + actual names | input role | merge; literal assignment and extraction remain domain-validated |
| InterpretWorkflow | same request | read/save/control | merge without erasing prior axes |
| AdditionalArtifactRequested | same request | draft vs separate identity | merge |
| ExtractContentCore | same request | content constraints | merge; exact evidence validation |
| Select | retrieved real records | source ID | needed only when first goal had no authorized candidates; exact IDs checked |
| Repeated destination/text/scope/input/workflow/artifact/content calls | loaded source body/slots | reinterpret entire task | avoid goal reset; bind fresh source revision instead |
| Decide (editor/open/update) | fresh UI state | next already planned transition | deterministic typed executor, RULE_SELECTED provenance |
| Propose | fresh source + scoped content delta | generated instruction | keep real Qwen; bounded domain correction only on concrete rejection |
| Input extraction in variables tasks | real fields + literal material | bind user values | only when existing literal resolver cannot bind them; no new invented values |

Historical failures also remain in `PERFORMANCE_ACCEPTANCE.md`: Home references (REFERENCE_RESOLUTION_ERROR), lost transformations (INTENT_ERROR / PLAN_ERROR), opening-only termination (POSTCONDITION_ERROR), and workflow text inside generated drafts (OUTPUT_CONTAMINATION). The concrete contamination boundary passed the entire mixed user request to the editor as content authority. A phrase-removal list would not fix that boundary.

## Frozen evaluation policy

Before candidate implementation, independent versioned development, validation and holdout datasets and thresholds were written under `Tests/nova/datasets/recovery-v1`. Each split has 16 cases and two nonempty paraphrase groups. Holdout outcomes must not drive tuning. The old exposed 16 cases remain historical.

Warm baseline range is 824 ms across two samples; descriptive median 89156 ms. Frozen initial target after these observations: at least 25% lower warm median (≤66867 ms), no reduction in correct-task rate, all mandatory-task candidate runs correct. This tiny baseline cannot establish population variance or a general speedup; cold observations and failures are reported separately. Full acceptance also requires the frozen semantic gates and broader real workflow regressions. Missing dimensions are NOT_TESTED.
