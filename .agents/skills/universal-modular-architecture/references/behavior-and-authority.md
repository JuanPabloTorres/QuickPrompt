# Behavioral ownership and causal diagnosis

Use for a failing workflow, cross-module inconsistency or proposed redesign. Scale the record to the affected boundary; reuse the project's existing acceptance/incident record.

## 1. Trace a real path before designing a replacement

Write the requested outcome and prohibited effects from the user's requirement, then follow the observed path through code. Record file/symbol and a concrete input/output or receipt for each relevant edge. Mark unobserved edges UNKNOWN. A diagram inferred from filenames is a hypothesis, not an implementation map.

Investigate unknowns that could change the correction. Unrelated unknowns do not block an evidenced local change or justify a full-system rewrite.

For each changing value, fill only the relevant columns:

| Value/artifact | Authoritative source and provenance | Current owner and lifecycle | Permitted transformation/writer | Consumer and observable postcondition |
|---|---|---|---|---|

Separate the requested goal, interpreted proposal, domain policy, current state and observed result. Record identity/revision when they affect correctness. Mark absence of an owner or competing writers explicitly; do not conceal it with a new interface.

An owner has a purpose and a boundary: what it receives, what it may change, what it must preserve, what it returns, and how callers observe failure/cancellation. A collection of classes with no agreed invariant is not a modular design.

Follow the actual composition root, port dispatch, adapter, configuration/profile and instance lifetime. Verify that the implementation being diagnosed is the one serving the failing caller. A passing compatibility adapter, unused experiment or differently configured factory does not qualify the production path. Match source/artifact identity to the running candidate when stale builds could explain a discrepancy.

## 2. Audit transformations, including convenient ones

Inspect normalization, parsing, canonicalization, default insertion, compilation, schema repair, serialization and UI binding on the affected path. For each, ask:

- Does it preserve representation, or change meaning, required inputs, identity, workflow or permissions?
- What domain rule authorizes that change, and where is that rule owned?
- Are conflicting inputs rejected, reconciled by a declared policy, or silently completed by inference?
- Is a derived hint or description being promoted into authoritative content or an executable argument?
- Can a later pass change an artifact after it was reviewed or hashed?

Choose the source of truth from the domain, not a universal preference for text, schema or database. A schema-driven form may legitimately derive fields from a schema; an artifact-driven editor may derive them from its parsed document. Both need a declared direction of derivation and consistency checks. Defaults and migrations may change values when that is their explicit, tested purpose.

Type correctness, reflection and schema validation establish structure; they do not establish that a value is relevant, true or authorized. Metadata should describe behavior enforced by its owner, rather than substitute for enforcement.

## 3. Establish a causal explanation

Compare original input -> intermediate values -> actual effect. Identify the earliest observed violation, not merely the final exception. Distinguish CONFIRMED facts, INFERRED causes and UNKNOWN edges.

Before changing code, record:

1. Mechanism: the specific rule/transition that produces the wrong result.
2. Alternative: another plausible cause, and the observation that distinguishes it.
3. Prediction: what will change if the proposed correction is right, and what must remain unchanged.
4. Probe: the smallest reproduction or controlled intervention that can disprove that prediction.
5. Impact: callers and other operations sharing the invariant; authorization and recovery constraints.

Use deterministic reproductions for deterministic transformations. A remote/model campaign is unnecessary to prove a local function appends data or changes an ID. If available evidence cannot locate the violation, instrument the missing boundary rather than changing prompts or architecture speculatively. Preserve the original failing artifact before intervening.

## 4. Challenge the oracle

Classify each relevant assertion as user/domain requirement, safety invariant, compatibility requirement, or implementation assumption. An old assertion is not automatically an acceptance criterion. Determine whether a green test would also accept the reported wrong outcome.

Keep input, expected artifact/state/effects and original criteria independent of generated plans and summaries. Compare fields and persisted values, not only status codes, schema validity or self-reported success. Include a positive case that the correction must preserve and a negative/contrast case it must distinguish. Test relevant ambiguity or state changes when those cause the failure.

When an assertion encodes a mistaken policy, explain its source and change the contract deliberately; do not simply delete the assertion or reduce the release criterion. Preserve the previous failure and add evidence that the legitimate behavior still works. Intentional, requested semantic changes need not preserve an old bug as compatibility.

## 5. Repair and acceptance

Compare extending the current owner, a shared abstraction and a separate capability. Select based on the causal mechanism and variation, not on file size or a desire to add factories. Remove duplicated policy only after the replacement's callers and invariants are verified.

Declare a bounded experiment before running it: candidate, invariant family, controls, expected change and stopping rule. A number of attempts is not a reason to continue; require new discriminating evidence. Record rejected changes and remove unaccepted experimental behavior from the release candidate, or keep it explicitly isolated according to project conventions. Do not overwrite unrelated work.

Re-run the affected family and explicit dependencies first. At release acceptance, use one frozen candidate across the required matrices. Evidence from different artifacts or profiles must retain those distinctions.

If the first divergence is corrected but a dependent stage still fails, report causal progress and keep overall acceptance open. Mark stages not reached as such; do not attribute their failure to their implementation without observation. Distinguish a previously masked defect from a regression caused by the correction.
