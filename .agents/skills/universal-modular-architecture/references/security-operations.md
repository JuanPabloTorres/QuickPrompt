# Security and Operations
- Threat model sensitive paths: principals, assets, entry points, trust boundaries, abuse cases, mitigations and residual risk.
- Enforce authn/authz on the server and per action/resource; default deny and least privilege. Check multitenant isolation at database and application boundaries.
- Adapters may be configured per environment, but secrets are injected securely and never logged or committed.
- Validate untrusted input, file uploads, serialized messages and tool results. Protect against prompt injection when AI connects to tools/data.
- Preserve trace/correlation IDs and structured telemetry across modules without logging sensitive payloads by default.
- Use health checks, readiness, liveness and dependency-specific circuit breakers. Optional dependency failure should degrade only its capability where policy allows.
- Feature flags must have owner, expiration/removal plan and tested states. Rollout should include migrations, rollback, canary/shadow checks when relevant.
- Set explicit quality targets: latency percentiles, error budget, throughput, memory/CPU, recovery time, accessibility and security checks appropriate to project.
