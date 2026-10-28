using System.Text.Json;

namespace QuickPrompt.Web.Infrastructure;

// Application-owned inference port. Namespace retained for existing consumers.
// Adapters may execute locally or await a paired device; neither grants domain/UI effects.
public interface IAgentModelTransport
{
    Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose);
    bool UsesThinking(string model);
    void Observe(int stage, string content, string? error);
}

// Semantic validation can reject a structurally valid answer. Invalidation belongs
// to the same transport/cache owner; callers without that capability use the base port.
public interface IValidatedAgentModelTransport : IAgentModelTransport
{
    void Reject(string? answer);
}
