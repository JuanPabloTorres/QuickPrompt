using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class CaptureTransactionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private sealed class RejectReceiptSave : SaveChangesInterceptor
    {
        private int saves;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (++saves == 2) throw new InvalidOperationException("QA receipt fault");
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task ReceiptFailureRollsBackPromptAndVersionAndLimitedRoleCannotAlterReceiptsOrCapabilities()
    {
        var owner = await postgres.CreateUser(confirmed: true);
        var other = await postgres.CreateUser(confirmed: true);
        var actor = LibraryActor.FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.ToString())], "fixture")));
        var options = new DbContextOptionsBuilder<AccountDatabase>().UseNpgsql(postgres.Application).Options;
        await using var normal = new AccountDatabase(options); var libraries = new LibraryAccess(normal);
        var library = await libraries.CreateLibrary(actor, "Capturas atómicas");
        var input = new AccountCaptureInput(Guid.NewGuid(), "Atómico", "Explica <tema>.");
        await using (var failing = new AccountDatabase(new DbContextOptionsBuilder<AccountDatabase>().UseNpgsql(postgres.Application).AddInterceptors(new RejectReceiptSave()).Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new LibraryAccess(failing).Capture(actor, library.Id, input));
        Assert.Empty(await libraries.ListPrompts(actor, library.Id));
        var created = await libraries.Capture(actor, library.Id, input); Assert.False(created.Replayed);
        Assert.Single(await libraries.ListVersions(actor, library.Id, created.Prompt.Id));
        await using var connection = await postgres.Application.OpenConnectionAsync();
        await using (var noActor = new NpgsqlCommand("SELECT count(*) FROM qp_library.captures", connection)) Assert.Equal(0L, await noActor.ExecuteScalarAsync());
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using var context = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id', @actor, true)", connection, transaction);
            context.Parameters.AddWithValue("actor", other.ToString()); await context.ExecuteScalarAsync();
            await using var invisible = new NpgsqlCommand("SELECT count(*) FROM qp_library.captures", connection, transaction);
            Assert.Equal(0L, await invisible.ExecuteScalarAsync());
            await transaction.CommitAsync();
        }
        await using (var invalid = new NpgsqlCommand("UPDATE qp_library.captures SET \"ContentHash\"=repeat('A',64)", connection))
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => invalid.ExecuteNonQueryAsync())).SqlState);
        await using (var invalid = new NpgsqlCommand("UPDATE qp_operations.connections SET \"McpCapabilities\"=7", connection))
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => invalid.ExecuteNonQueryAsync())).SqlState);
    }
}
