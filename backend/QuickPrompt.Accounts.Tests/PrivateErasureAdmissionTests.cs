using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class PrivateErasureAdmissionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ErasureWinningTheFenceRejectsACompileSnapshotAlreadyReadBeforeAdmission()
    {
        var actor = LibraryActor.FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, (await postgres.CreateUser()).ToString())], "synthetic")));
        var name = "qa-erasure-admission-" + Guid.NewGuid().ToString("N");
        await using var deletePool = Pool(postgres.ApplicationConnection, name + "-delete");
        await using var readerPool = Pool(postgres.ApplicationConnection, name + "-read");
        await using var workerPool = new ExecutionDataSource(Pool(postgres.ExecutionConnection, name + "-admit"));
        await using var holdPool = Pool(postgres.ApplicationConnection, name + "-hold");
        await using var database = new AccountDatabase(new DbContextOptionsBuilder<AccountDatabase>().UseNpgsql(deletePool).Options);
        var libraries = new LibraryAccess(database); var library = await libraries.CreateLibrary(actor, "Synthetic admission erasure race");
        var prompt = await libraries.SavePrompt(actor, library.Id, null, 0, "Private source", "Explain <topic>.");
        var queue = new ExecutionQueue(workerPool, new()); var executions = new AccountExecutionAccess(new AccountAgentMemory(readerPool), queue);
        await using var blocker = await holdPool.OpenConnectionAsync(); await using var held = await blocker.BeginTransactionAsync();
        await using (var identify = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,true)", blocker))
        { identify.Parameters.AddWithValue("actor", actor.Id.ToString()); await identify.ExecuteNonQueryAsync(); }
        await using (var protect = new NpgsqlCommand("SELECT \"Id\" FROM qp_library.prompts WHERE \"Id\"=@id FOR UPDATE", blocker))
        { protect.Parameters.AddWithValue("id", prompt.Id); Assert.Equal(prompt.Id, await protect.ExecuteScalarAsync()); }

        // Erasure owns fence 5283 but must wait for the source row. A plain source
        // SELECT still observes its committed definition, so compilation captures it.
        var deleting = libraries.DeletePrompt(actor, library.Id, prompt.Id, new(1, true));
        await WaitForLock(deletePool, name + "-delete");
        var submitting = executions.Compile(actor, library.Id, prompt.Id, new(Guid.NewGuid(), 1, new() { ["topic"] = "synthetic" }));
        await WaitForLock(workerPool.Data, name + "-admit");
        await held.CommitAsync(); await blocker.CloseAsync(); await deleting;
        try
        {
            await Assert.ThrowsAsync<MissingPromptException>(() => submitting);
            Assert.Empty(executions.List(actor, library.Id)); Assert.Null(await queue.Claim());
        }
        finally
        {
            // Preserve a discriminating baseline failure without leaving a private
            // queued payload behind when this test is run against an older binary.
            if (submitting.IsCompletedSuccessfully)
            {
                var admitted = await submitting;
                executions.Cancel(actor, library.Id, admitted.Id); await queue.Claim();
                executions.Forget(actor, library.Id, admitted.Id);
            }
        }
    }

    private static NpgsqlDataSource Pool(string connection, string name) => NpgsqlDataSource.Create(
        new NpgsqlConnectionStringBuilder(connection) { ApplicationName = name, MaxPoolSize = 4 }.ConnectionString);

    private static async Task WaitForLock(NpgsqlDataSource pool, string name)
    {
        await using var connection = await pool.OpenConnectionAsync();
        await using var wait = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE application_name=@name AND wait_event_type='Lock')", connection);
        wait.Parameters.AddWithValue("name", name);
        for (var attempt = 0; attempt < 160; attempt++)
        { if (await wait.ExecuteScalarAsync() is true) return; await Task.Delay(20); }
        Assert.Fail("The owning operation did not reach its real database lock barrier.");
    }
}
