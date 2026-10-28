using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Cloud;

/// <summary>Fresh cookie/stamp authority for each streaming read, ordered with revocation. Never spans inference.</summary>
internal sealed class AccountCookieReadScope(IServiceScopeFactory scopes, ExecutionQueue queue)
{
    internal Task<T?> Read<T>(ClaimsPrincipal principal, DateTimeOffset expires, Func<IServiceProvider, LibraryActor, T> read,
        CancellationToken cancellation) where T : class => queue.Fence<T?>(async () =>
    {
        if (expires <= DateTimeOffset.UtcNow) return null;
        await using var scope = scopes.CreateAsyncScope();
        if (await scope.ServiceProvider.GetRequiredService<SignInManager<AccountUser>>().ValidateSecurityStampAsync(principal) is null) return null;
        cancellation.ThrowIfCancellationRequested();
        return read(scope.ServiceProvider, LibraryActor.FromPrincipal(principal));
    }, cancellation);
}
