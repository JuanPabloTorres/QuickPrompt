using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Memory;

/// <summary>Shared storage bounds and codec, not a policy for approvals or pending work.</summary>
internal static class AccountSessionData
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { IgnoreReadOnlyProperties = true, MaxDepth = 64 };
    internal static string Encode(AccountDatabase database, Guid libraryId, Guid id, AgentSession session)
    {
        if (session.Id != id || id == Guid.Empty || session.DraftRevision < 1 || session.Values is null || session.Values.Count > 60
            || session.Values.Any(x => x.Key.Length > 80 || x.Value is null || x.Value.Length > 8000)
            || session.Draft?.SessionId is { } draftSession && draftSession != id)
            throw new ValidationException("Contexto de sesión inválido.");
        var references = (session.Candidates ?? []).Select(x => x.Id).Concat(session.Draft?.Candidates.Select(x => x.Prompt.Id) ?? [])
            .Concat(session.Source is { } source ? [source.Id] : []).Concat(session.Draft?.SourceId is { } draftSource ? [draftSource] : []).Distinct().ToArray();
        if (references.Length > 12 || database.Prompts.Count(x => x.LibraryId == libraryId && references.Contains(x.Id)) != references.Length)
            throw new LibraryUnavailableException();
        var payload = JsonSerializer.Serialize(session, Json);
        if (Encoding.UTF8.GetByteCount(payload) > 512000) throw new ValidationException("El contexto supera el límite permitido.");
        return payload;
    }
    internal static AgentSession Decode(string payload, Guid id) => JsonSerializer.Deserialize<AgentSession>(payload, Json) is { } session && session.Id == id
        ? session : throw new InvalidOperationException("Stored context identity is inconsistent.");
}
