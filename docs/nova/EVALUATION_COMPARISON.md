# Performance quality gate

Defined before changing the measured A0 implementation (2026-10-17).

The frozen A0 is the current working implementation, not the last Git commit. The performance probe copies API assemblies and the built React application before starting its synthetic SQLite library. Model-response reuse is disabled for all inference experiments to prevent replay being mistaken for inference acceleration.

## Gates

- Zero unauthorized writes, accepted invented IDs, stale-revision saves, false executed claims, or unapproved external sends.
- All existing domain, contract, approval, cancellation and UI-driver tests must pass.
- Each compared natural-language task must retain its source, transformation, literal safety constraints, requested destination and supplied inputs. Ambiguous requests must clarify instead of choosing arbitrarily.
- No decrease in correct completion on the same directed comparison cases. Failures and timeouts remain in the report; latency percentiles for correct results are accompanied by the completion rate.
- A performance improvement is provisional below 30 independent observations per scenario. Exact repeated response-cache hits are reported separately.
- Existing holdout is not used to design prompts or optimize parameters. Directed synthetic cases are for development; any subsequent holdout run is evaluation only.

Primary mandatory scenario: find the saved correspondence prompt, adapt it for an interview request and prepare it for copying, in the original React tab. Additional directed cases: paraphrase, source lookup, new instruction, bound variables, pause/cancel and approval/revision protection. The synthetic library contains no user data.

Reproduction: `node Tests/nova/latency.cjs`; options and exact model/build metadata are written next to each evidence file. A0 starts with the actual resident-model state, recorded before the test. No explicit model unload, service restart or global Ollama setting change is permitted.

No 20% p95 claim is established by this gate.

## Independent semantic evaluation

The existing holdout and thresholds were not edited. Dataset SHA256:
`B3E1B4AA8B2F6E26DCE6B32E90F379F9F97CF5E15DC310665B8730D871E1EC3A`.
Qwen3 digest matches the baseline in PERFORMANCE_BASELINE.md. Both profiles use the same current semantic implementation, synthetic repository-backed context and original authoring/reference checks, with response reuse disabled. Only TextGoal thinking differs. Contextual IDs are created independently by the existing evaluator; these timings are descriptive, not a controlled population speedup estimate.

Baseline interpretation profile (`SemanticThinking=true`): 16/16 attempted, **9/16 correct**, **15/16 valid**, critical accepted effects 0. **FAIL** against the existing .85 semantic accuracy / .95 valid-plan thresholds. p50 25315 ms, p95 63272 ms, 130 model calls, 123121 prompt tokens, 10290 generated tokens. Evidence: `docs/evidence/performance-holdout-20261017/baseline.json`.

These are real interpretation/plan results, not execution tests. A rejected forbidden operation is not an accepted unauthorized effect. The evaluator does not execute plans or claim that generation/UI quality passed. Requests and complete model responses are not written to the new reports; only plan categories, IDs, hashes, counters and errors are retained.

Reproduction (sequential, using an already built evaluation DLL):

```powershell
$env:NOVA_EVAL_SEMANTIC_THINKING='true'
dotnet .nova-build/latency/evaluation/QuickPrompt.Evaluation.dll --semantic Tests/evaluation/semantic-v1.json docs/evidence/performance-holdout-20261017/baseline.json holdout
$env:NOVA_EVAL_SEMANTIC_THINKING='false'
dotnet .nova-build/latency/evaluation/QuickPrompt.Evaluation.dll --semantic Tests/evaluation/semantic-v1.json docs/evidence/performance-holdout-20261017/candidate.json holdout
```

No prompt, schema, threshold or semantic decision is tuned after inspecting the holdout. A profile that fails the quality gate is not made the default merely because it is faster.

The `SemanticThinking=false` candidate completed 16/16 attempts: **8/16 correct**, **15/16 valid**, critical accepted effects 0. **FAIL**. p50 12528 ms, p95 35608 ms, 133 model calls, 123640 prompt tokens, 4354 generated tokens. The advice case `sem-060` passed the baseline and failed this candidate; other per-case correctness outcomes match. Both reports have identical implementation hashes. Evidence: `docs/evidence/performance-holdout-20261017/candidate.json`.

Neither holdout report contains metamorphic groups. The existing evaluator serializes equivalence as 1 for an empty group set; this is **not evidence of metamorphic equivalence**. That dimension is unmeasured here. Directed paraphrases remain failures in the E2E comparison; no thresholds, cases or evaluator gate rules were weakened to turn these reports green.

**Decision:** do not enable the faster TextGoal profile by default. Keep `SemanticThinking=true`, the independent authoring check, target/constraint reviews and complex-reference reasoning unchanged. The lower interpretation time does not offset the observed correctness loss. The disabled option remains available for explicit process-local experiments and rollback; it is not the selected production profile. No semantic instruction was changed in response to these holdout results.

## Headed E2E with selected defaults

The final API defaults enable grounded navigation/values and verified completion, while keeping original semantic thinking. The API's Infrastructure/Application/Domain hashes match the baseline semantic evaluation assemblies. These tests use real Qwen, native controlled fields, one Edge Page per scenario and synthetic SQLite only. The inference memo is disabled. No personal database or external destination is used.

`docs/evidence/latency-1792287823707`:

| Scenario | Wall ms | Observed result |
|---|---:|---|
| Primary adaptation | 118986 | Draft matches actual editor; invariant preserved; original unchanged |
| Prepare Claude | 104294 | Actual picker selects Claude; no send or navigation |
| Local execution from Home | 47281 | Source clarification; **not completed** |
| Longer adaptation from Home | 13123 | `INVALID_DECISION` before UI execution; **not completed** |
| Open action-like title without editing | 64190 | Correct identity/content; no adaptation |
| Ambiguous source | 38622 | Concrete question; no arbitrary selection |
| Cancel during inference | 2054 | Cancelled; 1314 ms from cancel click to observed terminal state; library unchanged |
| One transient HTTP 503 | 71882 | Read recovered and completed; initial QA oracle incorrectly expected permanent failure |
| Approve exact version | 159141 | Ready at 157911 ms; actual review/confirm clicks; revision +1 and exact approved content; other records unchanged |

The transient-error oracle was corrected in the runner and retested separately, without rewriting the original failed report. Recovery from a single injected HTTP error is an expected bounded retry, not a product failure. The persistent-error scenario separately requires two rejected attempts, typed `MODEL_UNAVAILABLE`, no UI actions and no mutation. Fault injection is not described as a real-model generation quality sample.

`docs/evidence/latency-1792288494863` reuses the original two-record fixture/IDs/revisions: primary **166585 ms**, correct, 22 calls; variables **105426 ms**, correct, 14 calls. The slower final primary result is retained. No global latency improvement or 20% p95 reduction is established.

`docs/evidence/latency-1792288798591` separately selects a real library card before sending the same request: local execution **55832 ms**, exact `LISTO` response from Qwen; longer adaptation **114592 ms**, actual changed form and no save. Preselection is an explicit scenario precondition, not a fallback that hides the Home failures. The selected IDs are checked against the library and observed result. Local streaming first content appears 52801.901 ms after RunId start, approximately 419.041 ms after the execution request scope starts. Structured classifiers remain non-streaming; their first-token time is unknown.

Visual review checked the official logos, actual content fields, local response and approved version 02. The draft oracle checks identity, purpose, invariants, exact DOM/observation equality and persistence boundaries; it does **not** prove perfect editorial fidelity. Screenshots also show workflow wording retained in some reusable draft bodies (for example, the instruction to leave a draft unsaved). This remains a semantic/editorial limitation; no phrase-based cleanup was introduced to mask it. These results are not an unrestricted semantic acceptance PASS.

Source fixtures are approximately 140 characters for correspondence and 1.1K characters for the longer review; they do not establish behavior at the maximum context/template limits. Every non-approval scenario compares the complete synthetic library before/after; approval checks exactly the expected record and revision. Browser request monitoring checks no external HTTP request, popup or departure from the application origin.

The corrected recovery run `latency-1792288798591` is **4/4 PASS** for its declared checks: selected local execution 55832 ms; selected longer draft 114592 ms; recovery after exactly one injected 503 **96868 ms** with correct source/content and no writes; persistent 503 **287 ms**, exactly two rejected requests, typed failure and no UI dispatch. The 503 cases are fault-injection checks; no percentage of generation speed is calculated from them. All four have complete model request attribution, one Page and zero external browser requests.
