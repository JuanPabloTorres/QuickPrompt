using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;

namespace QuickPrompt.Accounts.Persistence;

/// <summary>One actor and one database transaction per operation; never spans model inference.</summary>
internal static class AccountTransaction
{
    /// <summary>Bounded synchronous application ports use their own context; never span inference.</summary>
    internal static T Run<T>(AccountDatabase database, LibraryActor actor, Func<T> operation)
    {
        using var transaction = database.Database.BeginTransaction();
        try
        {
            database.Database.ExecuteSqlInterpolated($"SELECT set_config('quickprompt.actor_id', {actor.Id.ToString()}, true)");
            var result = operation();
            transaction.Commit();
            return result;
        }
        finally { database.ChangeTracker.Clear(); }
    }

    internal static async Task<T> Run<T>(AccountDatabase database, LibraryActor actor, Func<Task<T>> operation,
        CancellationToken cancellation)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellation);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('quickprompt.actor_id', {actor.Id.ToString()}, true)", cancellation);
            var result = await operation();
            await transaction.CommitAsync(cancellation);
            return result;
        }
        finally { database.ChangeTracker.Clear(); }
    }
}
