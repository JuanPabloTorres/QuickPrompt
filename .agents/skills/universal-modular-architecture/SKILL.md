---
name: universal-modular-architecture
description: Design, implement, refactor, and audit software as cohesive, composable, replaceable modules with explicit contracts and tested invariants. Use for AI agents, RAG, frontend, backend, databases, realtime, caching, security, integrations, deployment, and cross-cutting changes. Prevent one-off patches, duplicate ownership, unnecessary abstractions, and architectural regressions.
---
# Universal Modular Architecture

## QuickPrompt context

For this repository, first use the [current owner map](../../../docs/ARCHITECTURE.md)
and [UI/form contracts](../../../docs/development/UI_COMPONENTS.md) when relevant.
Use [development checks](../../../docs/development/DEVELOPMENT.md) and scoped AGENTS
rules. Historical incident reports do not define the active action-selection flow.
Keep generic architecture guidance here; maintain QuickPrompt-specific contracts in
their owning project documents instead of copying them into each page or skill.

## Mission
Build systems from cohesive capabilities that can be composed, extended and, when explicitly supported, exchanged without breaking consumers. Treat this skill as an engineering decision process, not a mandate to create interfaces, microservices or plugins everywhere. Prefer the simplest architecture that preserves the required change boundaries.

## Non-negotiable operating protocol
1. **Discover before editing.** Inspect repository instructions, existing architecture, entry points, dependency graph, contracts, call sites, data ownership, migrations, tests, and active incidents. Identify the existing architectural style and respect it unless evidence supports changing it. Never invent architecture facts.
2. **Model the problem.** Define user outcome, actors, workflows, invariants, failure modes, authorization boundaries, performance and availability constraints. Distinguish domain behavior from infrastructure and presentation.
3. **Find capability ownership.** Identify whether the desired behavior already exists. Reuse the owning capability; extend it through its supported extension point. Do not implement duplicate validation, orchestration, state, policies, or data access in a second location.
4. **Draw the boundaries.** List module responsibilities, public interfaces/events/schemas, dependencies, data ownership, lifecycle, optional dependencies, and trust boundaries. High cohesion, low coupling; no cycles. Define compatibility and versioning expectations.
5. **Evaluate alternatives.** Compare at least (A) extending the current module, (B) introducing a small shared abstraction/strategy, (C) isolating a new capability. Select the least complex solution that meets proven or reasonably anticipated variation. Reject speculative frameworks and premature generalization.
6. **Implement a vertical slice.** Build an end-to-end increment through contract, domain behavior, adapter, UI or endpoint, persistence (as needed), and tests. Use dependency injection/composition roots, configuration and feature flags when they materially improve replaceability or rollout.
7. **Validate behavior and change isolation.** Run appropriate unit, contract, integration, security, regression, concurrency, performance, accessibility, and end-to-end tests. Validate actual substitution and removal when claiming plug-and-play. Mark tests not run as unverified, not passed.
8. **Report evidence.** State changes, module boundaries, extension/replacement instructions, test commands and outcomes, known risks, migrations, rollback and remaining work. Do not claim production readiness without evidence.

## Design rules
- Encapsulate changing decisions behind stable contracts. Dependency arrows point toward stable policy/domain abstractions. Depend on interfaces where doing so protects a real boundary, not for ceremonial one-implementation wrappers.
- Prefer composition, dependency injection, explicit capabilities, typed contracts, policies and strategies over condition trees and subclass hierarchies when multiple behaviors must vary independently.
- Generics are for *uniform algorithms over types*, not a substitute for meaningful domain concepts. Avoid generic repositories/services that erase business semantics or obscure query performance.
- Public abstractions expose consumer needs rather than vendor-specific operations. Define error semantics, cancellation, timeouts, retries, idempotency, observability and resource cleanup where applicable.
- A plugin's absence must have a defined behavior: required capability -> fail clearly and safely; optional capability -> supported degradation/feature off. Never silently pretend success. Disabling a plugin must not invalidate unrelated modules.
- Avoid shared mutable state, hidden global registries, circular imports, direct cross-module database writes, UI-only authorization, unbounded retries, and unchecked untrusted data.
- Maintain backward compatibility deliberately. For breaking changes, inventory consumers, define migrations/adapters, test old and new contracts, and document a rollback plan.
- Avoid distributed systems overhead unless requirements prove a need. Begin with well-bounded modules in a monolith if appropriate; split deployments only for justified scaling, ownership or resilience boundaries.
- Apply SOLID, separation of concerns, DRY (avoid duplicating knowledge, not every similar line), YAGNI, KISS, information hiding, single-writer ownership, and explicit invariants together; resolve conflicts using real change scenarios and evidence.

## Behavioral ownership before structural changes
For cross-module defects, reliability work or substantial redesign, read `references/behavior-and-authority.md` before choosing a correction. Trace one affected user outcome through actual producers, transformations, consumers and effects; class/interface inventories alone do not establish responsibility.

- Identify the authoritative source for each disputed value, its allowed writers and the transformations permitted by the domain. Include normalizers, defaults, compilers, adapters and repair paths, not just the model or public endpoints.
- Audit the test oracle against the requested behavior. Existing code and green tests can share the same wrong assumption; preserve evidence without treating that assumption as a requirement.
- State the first evidenced invariant violation, causal mechanism, competing explanation and a prediction that can fail. When an edge is unknown, inspect/instrument it instead of inventing a cause or adding a module.
- Inspect other consumers of the same invariant before proposing the shared fix. Keep that impact review scoped; do not turn it into an unrelated repository rewrite.
- Accept a structural refactor as structural evidence only. A renamed owner, reflected schema or new interface does not demonstrate correct behavior.

Use the existing change record; a compact trace is enough for a small change. Do not create parallel plans or new reporting layers solely to satisfy this procedure.

## Domain-specific routing
Read only the relevant references before implementing:
- AI, RAG, tool calling, agent memory, context and safety: `references/ai-rag-agents.md`
- Frontend, backend, type systems and forms: `references/application-boundaries.md`
- Persistence, realtime, caching, events, distributed systems: `references/data-infrastructure.md`
- Security, operations and scalability: `references/security-operations.md`
- Decision records, patterns, quality gates and examples: `references/decision-quality.md`
- Data authority, behavioral ownership, causal diagnosis and test-oracle review: `references/behavior-and-authority.md`

## Mandatory change contract
Before substantial code changes, record:
- **Capability:** what behavior and why; existing owner or proposed owner.
- **Invariants:** what must always remain true; failure and security constraints.
- **Extension points:** what varies, why, interface and lifecycle; if none, explicitly say none.
- **Dependency map:** permitted calls/events and prohibited coupling; ownership of data/state.
- **Alternative analysis:** simplest viable implementation vs abstraction vs new module.
- **Verification:** tests of happy paths, negative paths, isolation, substitution, no regressions.
- **Rollout:** compatibility, migration and rollback where applicable.
Use `templates/change-contract.md`. For nontrivial proposals use `templates/architecture-decision.md`.

## Fitness checks (adapt to repo tooling)
- No newly introduced dependency cycles or unauthorized layer imports.
- No duplicate business-rule ownership, unauthorized database access, or leaked vendor-specific types across contracts.
- Contracts versioned/migrated when externally consumed.
- Optional modules can be disabled without taking down unrelated capabilities, and required dependencies fail fast and clearly.
- Security boundaries fail closed; secrets never embedded; tenant/data isolation checked when relevant.
- New behavior has deterministic tests; integration tests cover adapters; replacement and failure tests prove claimed extensibility.
- Build, existing tests, lint/typecheck and user workflows do not regress; report actual evidence and blockers.

## Required response format
1. System and dependency findings (observed vs inferred).
2. Selected architecture and rationale, including rejected alternatives.
3. Capability contracts and ownership (what is reusable and what is specific).
4. Implementation files and migration/compatibility impact.
5. Executed verification and outcomes (PASS / FAIL / BLOCKED / NOT RUN).
6. Remaining risks and next justified increment.

## Anti-pattern stop signal
If a change requires adding a new isolated handler/service/class for a case that an existing capability should own, stop and inspect the architecture. If the proposed abstraction has no real second consumer or meaningful variation, consider keeping it concrete. If a 'plug-out' test breaks unrelated operations, fix the dependency boundary before claiming modularity.
