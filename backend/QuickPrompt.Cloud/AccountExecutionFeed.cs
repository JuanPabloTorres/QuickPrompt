using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Cloud;

public sealed record ExecutionFeed(Guid LibraryId, string Signal, ExecutionView[]? Items = null);

/// <summary>
/// Bounded snapshots from the existing queue, never generated progress or result content.
/// Every read uses a fresh Identity scope and the same fence as session invalidation.
/// </summary>
public sealed class AccountExecutionFeed(IServiceScopeFactory scopes, ExecutionQueue queue)
{
    private readonly AccountCookieReadScope authority = new(scopes, queue);
    public static async Task<IResult> Open(HttpContext context, Guid libraryId, AccountExecutionFeed feed, CancellationToken cancellation)
    {
        var session = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!session.Succeeded || session.Properties?.ExpiresUtc is not { } expires) return Results.Unauthorized();
        var principal = session.Principal!;
        // Authorize before starting the response: anonymous/foreign libraries keep 401/404.
        var initial = await feed.Read(principal, libraryId, expires, cancellation);
        if (initial is null) return Results.Unauthorized();
        context.Response.OnStarting(() => { context.Response.Headers.CacheControl = "no-store"; return Task.CompletedTask; });
        return TypedResults.ServerSentEvents(feed.Events(principal, libraryId, expires, initial, cancellation));
    }

    private Task<ExecutionView[]?> Read(ClaimsPrincipal principal, Guid libraryId, DateTimeOffset expires, CancellationToken cancellation) =>
        authority.Read(principal, expires, (services, actor) => services.GetRequiredService<AccountExecutionAccess>().List(actor, libraryId), cancellation);

    private async IAsyncEnumerable<SseItem<ExecutionFeed>> Events(ClaimsPrincipal principal, Guid libraryId, DateTimeOffset expires,
        ExecutionView[] initial, [EnumeratorCancellation] CancellationToken cancellation)
    {
        var until = DateTimeOffset.UtcNow.AddSeconds(30);
        var previous = initial;
        yield return Item("snapshot", initial);
        while (previous.Any(Active))
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellation);
            ExecutionView[]? current = null;
            string? failure = null;
            try { current = await Read(principal, libraryId, expires, cancellation); }
            catch (LibraryUnavailableException) { failure = "access-lost"; }
            catch (Exception error) when (error is NpgsqlException or TimeoutException) { failure = "unavailable"; }
            if (failure is not null || current is null) { yield return Item(failure ?? "access-lost"); yield break; }
            if (!previous.SequenceEqual(current)) { previous = current; yield return Item("snapshot", current); }
            else yield return Item("heartbeat");
            if (previous.Any(Active) && DateTimeOffset.UtcNow >= until) { yield return Item("renew"); yield break; }
        }
        yield return Item("complete");

        SseItem<ExecutionFeed> Item(string signal, ExecutionView[]? items = null) => new(new(libraryId, signal, items), "execution-state");
        static bool Active(ExecutionView job) => job.Status is ExecutionStatus.Queued or ExecutionStatus.Running;
    }
}
