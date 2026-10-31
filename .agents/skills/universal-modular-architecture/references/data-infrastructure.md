# Data, Events, Realtime, Cache and Scale
- Assign each data entity an authoritative owner. Other modules read via contracts, snapshots or published integration events; forbid uncontrolled cross-module writes.
- Schema migrations must be forward/backward compatible for rolling deployments when needed; verify constraints, indexes and query plans.
- Do not assume a generic CRUD repository solves domain-specific queries. Use explicit read models when warranted.
- Define transaction boundary and consistency model. For cross-service updates, consider outbox/inbox, idempotency and compensation; avoid pretending a distributed transaction is atomic.
- Events have versioned schemas, causal ordering assumptions, deduplication and retention; distinguish domain events from public integration events.
- Cache is an acceleration layer, not default source of truth. Define cache key namespacing, tenant scoping, TTL, invalidation, stampede protection, consistency and cold-start behavior.
- Realtime transport (WebSocket/SSE/pubsub) is an adapter. Keep message contracts and authorization independent of transport; plan reconnect, replay, ordering, backpressure and eventual consistency.
- Scale from measured bottlenecks: SLOs, capacity model, profiling, index optimization, bounded concurrency, queues, horizontal scaling and graceful degradation.
- Plugin-in/out must have explicit persistence consequences. Removing an optional adapter cannot delete authoritative records or silently lose mandatory messages.
