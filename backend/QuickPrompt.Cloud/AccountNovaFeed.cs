using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Cloud;

/// <summary>Owner-scoped native activity protocol. Events are observations, never an executable transport.</summary>
public sealed class AccountNovaFeed(IServiceScopeFactory scopes, ExecutionQueue queue, AccountNovaCoordinator nova)
{
    private readonly AccountCookieReadScope authority = new(scopes, queue);
    private sealed record Snapshot(LiveRecord Run, LiveEvent[] Events);

    public static async Task<IResult> Open(HttpContext context, Guid libraryId, Guid id, AccountNovaFeed feed, CancellationToken cancellation)
    {
        var session = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!session.Succeeded || session.Properties?.ExpiresUtc is not { } expires) return Results.Unauthorized();
        var raw = context.Request.Headers["Last-Event-ID"].FirstOrDefault() ?? context.Request.Query["after"].FirstOrDefault() ?? "0";
        if (raw.Contains(':'))
        {
            var parts = raw.Split(':');
            if (parts.Length != 2 || parts[0] != id.ToString()) throw new ValidationException("Cursor de eventos inválido.");
            raw = parts[1];
        }
        if (!long.TryParse(raw, out var after) || after < 0) throw new ValidationException("Cursor de eventos inválido.");
        var initial = await feed.Read(session.Principal!, libraryId, id, expires, after, cancellation);
        if (initial is null) return Results.Unauthorized();
        if (after > initial.Run.Sequence) throw new ValidationException("Cursor de eventos inválido.");
        context.Response.OnStarting(() => { context.Response.Headers.CacheControl = "no-store"; return Task.CompletedTask; });
        return TypedResults.ServerSentEvents(feed.Events(session.Principal!, libraryId, id, expires, after, initial, cancellation));
    }

    private Task<Snapshot?> Read(ClaimsPrincipal principal, Guid library, Guid id, DateTimeOffset expires, long after, CancellationToken cancellation) =>
        authority.Read(principal, expires, (_, actor) => new Snapshot(nova.Record(actor, library, id), nova.Events(actor, library, id, after)), cancellation);

    private async IAsyncEnumerable<SseItem<LiveEvent?>> Events(ClaimsPrincipal principal, Guid library, Guid id, DateTimeOffset expires,
        long after, Snapshot initial, [EnumeratorCancellation] CancellationToken cancellation)
    {
        var current = initial;
        // Renew only the observation connection before an external proxy's timeout. Run lifetime, approval and cursor remain in their owners.
        var until = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            foreach (var item in current.Events)
            {
                after = item.Sequence;
                yield return new(item, "activity") { EventId = $"{id}:{item.Sequence}" };
            }
            if (LiveRuns.Terminal(current.Run.Status)) yield break;
            if (DateTimeOffset.UtcNow >= until) { yield return new(null, "renew"); yield break; }
            await Task.Delay(TimeSpan.FromSeconds(1), cancellation);
            Snapshot? next = null; string? failure = null;
            try { next = await Read(principal, library, id, expires, after, cancellation); }
            catch (Exception error) when (error is LibraryUnavailableException or MissingPromptException) { failure = "access-lost"; }
            catch (Exception error) when (error is NpgsqlException or TimeoutException) { failure = "unavailable"; }
            if (next is null) { yield return new(null, failure ?? "access-lost"); yield break; }
            current = next;
            if (current.Events.Length == 0) yield return new(null, "heartbeat");
        }
    }
}
