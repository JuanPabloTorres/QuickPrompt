# AI, RAG and Agentic Systems
Model an agent as a composition of capabilities, not a giant prompt or hard-coded keyword router.

## Separable capabilities
- Intent/task interpretation with typed task contracts and uncertainty handling.
- Context assembly: ranked sources, scope, token budgets, provenance, user and tenant isolation, expiration.
- Retrieval: source connectors -> normalization -> chunking/indexing -> retrieval -> reranking -> evidence package; interchange adapters with measurable relevance and citations.
- Model gateway: capabilities (tools, context size, structured outputs), routing policy, errors, telemetry, local/remote implementations; do not leak provider message types into domain logic.
- Planner/orchestrator: explicit state transitions, tool eligibility, human approvals for sensitive side effects, budgets and termination constraints. Dynamic planning is not permission to bypass policy.
- Tool contracts: JSON schemas or typed parameters, pre/postconditions, access controls, idempotency, audit trails and retries appropriate to side effects.
- Memory: explicit write/read/retention policy, provenance, eviction, consent and isolation; distinguish working context, conversation and durable knowledge.
- Evaluation: semantic task correctness, groundedness, refusal/abstention, citation quality, tool precision, latency, cost, leak checks and adversarial scenarios.

## Rules
- LLM output is untrusted input. Validate schema and authorization before tool execution.
- Separate prompting from domain business rules. A prompt cannot replace deterministic enforcement of permissions, money movement or tenant boundaries.
- Do not route core functionality solely by fragile keywords or assume LLM output is always deterministic.
- Make provider substitution a tested contract with capability negotiation. A fallback model may need explicit degraded behavior, not assumed parity.
- Retrieval is optional only if the product defines an acceptable no-retrieval mode. No evidence -> honest abstention when grounding is required.
- Use recorded or synthetic fixtures in offline tests; real-provider tests are distinct gates.
- In concurrent agents, assign ownership of state, lock or serialize effects as required, and prevent duplicate actions.
