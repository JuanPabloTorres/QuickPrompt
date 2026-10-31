# Native agent — historical evaluation only

Moved out of QuickPrompt.Infrastructure on 2026-10-21. These sources preserve
regression controls and compatibility experiments. They are not registered by the
production host and are not referenced by the production dependency graph.

Production uses AdaptiveCoreInterpreter + AdaptiveCoreModelTransport. Domain
content extraction has its own PromptContentInterpreter. Do not restore this
engine as a silent fallback. See docs/nova/CORE_HOST_BOUNDARY_20261021.md.
