# Application and UI Boundaries
## Backend
- Distinguish domain rules, application use cases, inbound ports (HTTP/jobs/messages) and outbound adapters (DB/provider/cache).
- Controllers transport input; application services coordinate use cases; domain policies own invariants. Avoid anemic 'pass-through' layers with no clear responsibility.
- Validate at trust boundaries; make authorization and transactions explicit. Domain objects must not depend on framework request/response types.
- Use typed DTOs and schema versioning across process boundaries; avoid leaking ORM entities.
- Map cross-cutting capabilities (telemetry, retries, errors) with middleware/decorators only where semantics are consistent.
## Frontend and forms
- Treat design-system controls (field, input, select, validation display) as reusable primitives and workflows (checkout, onboarding) as domain composition.
- Keep business invariants server-enforced; client validation supports UX only.
- Separate state ownership: local ephemeral UI, server state, URL/navigation, persistent state; do not duplicate authority.
- Form schema may drive rendering where field types and interactions truly align; allow explicit exceptions rather than opaque megaconfigurations.
- Preserve accessibility, keyboard/screen-reader behavior, responsive layout, loading/empty/error states and i18n.
- Contract-test frontend/backend boundaries; use realistic user journeys in browser tests.
## Type and polymorphism rules
- Generic components and interfaces should state constraints and invariants. Avoid abstractions that collapse distinct business rules into `object`, `any` or string dictionaries.
- Prefer small interfaces defined around consumer needs, typed adapters and composition roots.
