# Decision Framework and Fitness Functions
## Scope decisions
1. Is the behavior already owned? Extend the owning module if compatible.
2. Does variation truly exist? Keep concrete until real variation justifies interface/strategy.
3. Do distinct stakeholders or change cadences justify a bounded context? Isolate ownership.
4. Does deployment independence add value? Prefer modular monolith unless isolation is justified by teams, resilience or scale.
5. Is a cross-cutting concern coherent? Apply a standardized decorator/middleware only if it preserves domain-specific semantics.

## Architecture smell examples
- Single business policy copied to UI, API and agents: choose server/domain owner; UI preview from contract.
- Each LLM provider reimplements RAG pipeline: provider port plus independent retrieval pipeline.
- 'Universal service' with dozens of nullable parameters: split into cohesive use cases and explicit composition.
- More modules than independently meaningful capabilities: collapse speculative boundaries.
- Giant base classes or universal generic repository: prefer composition, specific queries and narrow interfaces.
- One infrastructure outage crashes all optional features: separate failure boundary and degraded-mode contract.

## Evidence levels
- Static inspection: dependency and ownership evidence, no runtime guarantee.
- Unit tests: local behavioral evidence.
- Contract/integration: boundary evidence.
- System/e2e/chaos/load: real workflow or resilience evidence.
Never use a weaker evidence level to claim a stronger one.

## Example: notifications
Stable contract `NotificationDispatcher.send(notification, deliveryPolicy)`; transport adapters email/push/SMS. Business use case owns when to notify. Adapter failures follow documented retry and dead-letter rules. Removing SMS leaves email intact if SMS is optional. Do not create a mega dispatcher if only email is currently required and no meaningful variation is planned.
