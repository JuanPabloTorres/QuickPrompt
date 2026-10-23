using System.Text.Json;

namespace QuickPrompt.Web.Infrastructure;

// Local inference protocol only: no interpretation, domain reads, writes or UI effects.
// Keeps the semantic module substitutable without exposing the legacy service API.
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
