using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Local;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class ExecutionQueueTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static LibraryActor Actor(Guid id) => LibraryActor.FromPrincipal(new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test-authenticated")));
    private ExecutionDataSource Source() => new(NpgsqlDataSource.Create(postgres.ExecutionConnection));
    private AccountExecutionAccess Access(ExecutionQueue queue) => new(new AccountAgentMemory(postgres.Application), queue);
    private async Task<(LibraryActor Actor, Guid Library, AccountPrompt Prompt)> Fixture()
    {
        var actor = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var libraries = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var library = await libraries.CreateLibrary(actor, "QA ejecuciones");
        return (actor, library.Id, await libraries.SavePrompt(actor, library.Id, null, 0, "Instrucción", "Explica <tema>."));
    }
    private static CompileSubmission Input(Guid? operation = null, string value = "RLS") => new(operation ?? Guid.NewGuid(), 1, new() { ["tema"] = value });
    private static Task<ExecutionOutput> Process(ExecutionLease lease) => new CompileExecutionHandler().Execute(lease, CancellationToken.None);

    [Fact]
    public async Task PrivateResultIsAtomicPersistentAndIdempotentEvenAfterSourceChanges()
    {
        var a = await Fixture(); var b = await Fixture();
        await using var source = Source(); var queue = new ExecutionQueue(source, new()); var access = Access(queue);
        var request = Input(value: "<literal>\nUnicode ñ");
        var job = await access.Compile(a.Actor, a.Library, a.Prompt.Id, request);
        Assert.Equal(job.Id, (await access.Compile(a.Actor, a.Library, a.Prompt.Id, request)).Id);
        Assert.Throws<LibraryUnavailableException>(() => access.Read(b.Actor, a.Library, job.Id));
        Assert.Throws<LibraryUnavailableException>(() => access.Read(b.Actor, b.Library, job.Id));
        var lease = (await queue.Claim())!; Assert.Equal(job.Id, lease.Id);
        Assert.True(await queue.Finish(lease, await Process(lease)));
        Assert.False(await queue.Finish(lease, await Process(lease)));
        var restarted = Access(new ExecutionQueue(source, new()));
        Assert.Equal("Explica <literal>\nUnicode ñ.", restarted.Result(a.Actor, a.Library, job.Id).Text);
        Assert.Equal(ExecutionStatus.Completed, restarted.Read(a.Actor, a.Library, job.Id).Status);
        await using var scope = postgres.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LibraryAccess>().SavePrompt(a.Actor, a.Library, a.Prompt.Id, 1, "v2", "Otro <dato>.");
        Assert.Equal(job.Id, (await restarted.Compile(a.Actor, a.Library, a.Prompt.Id, request)).Id);
        await Assert.ThrowsAsync<ConflictException>(() => restarted.Compile(a.Actor, a.Library, a.Prompt.Id, request with { Values = new() { ["tema"] = "Otro" } }));
        Assert.Throws<LibraryUnavailableException>(() => restarted.Result(b.Actor, b.Library, job.Id));
    }

    [Fact]
    public async Task ArchivedPromptRejectsNewExecutionButPreservesAnExistingReceiptAndResult()
    {
        var a = await Fixture();
        await using var source = Source(); var queue = new ExecutionQueue(source, new()); var access = Access(queue);
        var request = Input(); var job = await access.Compile(a.Actor, a.Library, a.Prompt.Id, request);
        var lease = (await queue.Claim())!; Assert.Equal(job.Id, lease.Id);
        Assert.True(await queue.Finish(lease, await Process(lease)));
        var library = new AccountAgentMemory(postgres.Application).Library(a.Actor, a.Library);
        var archived = library.Archive(a.Prompt.Id, 1, true);
        Assert.Throws<LibraryUnavailableException>(() => access.Preview(a.Actor, a.Library, a.Prompt.Id, new(archived.Revision, request.Values)));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.Compile(a.Actor, a.Library, a.Prompt.Id, request with { OperationId = Guid.NewGuid(), Revision = archived.Revision }));
        Assert.Equal(job.Id, (await access.Compile(a.Actor, a.Library, a.Prompt.Id, request)).Id);
        Assert.Equal("Explica RLS.", access.Result(a.Actor, a.Library, job.Id).Text);
        Assert.All(access.List(a.Actor, a.Library), receipt => Assert.Equal(job.Id, receipt.Id));
    }

    [Fact]
    public async Task ConcurrentAdmissionHonorsOwnerGlobalAndDailyLimitsWithoutChargingRetries()
    {
        var a = await Fixture(); var b = await Fixture(); var c = await Fixture();
        await using var source = Source(); var queue = new ExecutionQueue(source, new(OwnerQueued: 1, GlobalQueued: 2, DailyAdmissions: 1)); var access = Access(queue);
        var request = Input();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        { try { return await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()); } catch (ExecutionQuotaException) { return null; } }));
        var admitted = Assert.Single(attempts, x => x is not null)!;
        var second = await access.Compile(b.Actor, b.Library, b.Prompt.Id, request);
        Assert.Equal(second.Id, (await access.Compile(b.Actor, b.Library, b.Prompt.Id, request)).Id);
        Assert.Equal("QUEUE_FULL", (await Assert.ThrowsAsync<ExecutionQuotaException>(() => access.Compile(c.Actor, c.Library, c.Prompt.Id, Input()))).Code);
        access.Cancel(a.Actor, a.Library, admitted.Id); access.Cancel(b.Actor, b.Library, second.Id);
        Assert.Null(await queue.Claim());
        Assert.Equal("DAILY_LIMIT", (await Assert.ThrowsAsync<ExecutionQuotaException>(() => access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()))).Code);
        Assert.Empty(access.List(c.Actor, c.Library));
    }

    [Fact]
    public async Task TwoDispatchersShareGlobalAndPerOwnerConcurrencyAndDoNotClaimOneJobTwice()
    {
        var a = await Fixture(); var b = await Fixture();
        await using var first = Source(); await using var second = Source();
        var queue = new ExecutionQueue(first, new()); var other = new ExecutionQueue(second, new()); var access = Access(queue);
        await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input());
        await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input());
        await access.Compile(b.Actor, b.Library, b.Prompt.Id, Input());
        var leases = await Task.WhenAll(queue.Claim(), other.Claim());
        Assert.All(leases, x => Assert.NotNull(x)); Assert.NotEqual(leases[0]!.Id, leases[1]!.Id);
        Assert.NotEqual(leases[0]!.OwnerId, leases[1]!.OwnerId); Assert.Null(await other.Claim());
        foreach (var lease in leases) Assert.True(await queue.Finish(lease!, await Process(lease!)));
        var remaining = (await other.Claim())!; Assert.Equal(a.Actor.Id, remaining.OwnerId);
        Assert.True(await other.Finish(remaining, await Process(remaining))); Assert.Null(await queue.Claim());
    }

    [Fact]
    public async Task AnOwnerWhoHasNeverRunGetsATurnBeforeAnOwnersSecondQueuedJob()
    {
        var a = await Fixture(); var b = await Fixture(); await using var source = Source();
        var queue = new ExecutionQueue(source, new(GlobalRunning: 1)); var access = Access(queue);
        await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input());
        await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input());
        await access.Compile(b.Actor, b.Library, b.Prompt.Id, Input());
        var first = (await queue.Claim())!; Assert.Equal(a.Actor.Id, first.OwnerId); await queue.Finish(first, await Process(first));
        var next = (await queue.Claim())!; Assert.Equal(b.Actor.Id, next.OwnerId); await queue.Finish(next, await Process(next));
        var last = (await queue.Claim())!; Assert.Equal(a.Actor.Id, last.OwnerId); await queue.Finish(last, await Process(last));
    }

    [Fact]
    public async Task CancellationBeforeClaimOrCompletionNeverProducesAResult()
    {
        var a = await Fixture(); await using var source = Source(); var queue = new ExecutionQueue(source, new()); var access = Access(queue);
        var queued = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()); access.Cancel(a.Actor, a.Library, queued.Id);
        Assert.Null(await queue.Claim()); Assert.Equal(ExecutionStatus.Cancelled, access.Read(a.Actor, a.Library, queued.Id).Status);
        var running = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()); var lease = (await queue.Claim())!;
        access.Cancel(a.Actor, a.Library, running.Id); Assert.True((await queue.Heartbeat(lease)).CancelRequested);
        Assert.True(await queue.Finish(lease, await Process(lease)));
        Assert.Equal(ExecutionStatus.Cancelled, access.Read(a.Actor, a.Library, running.Id).Status);
        Assert.Throws<LibraryUnavailableException>(() => access.Result(a.Actor, a.Library, running.Id));
    }

    [Fact]
    public async Task ExpiredLeaseIsInterruptedAndCannotCommitOrAutomaticallyRepeat()
    {
        var a = await Fixture(); await using var source = Source(); var queue = new ExecutionQueue(source, new()); var access = Access(queue);
        var job = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()); var lease = (await queue.Claim())!;
        Assert.False(await queue.Finish(lease with { Token = Guid.NewGuid() }, await Process(lease)));
        await using (var connection = await source.Data.OpenConnectionAsync())
        {
            await using var expire = new NpgsqlCommand("UPDATE qp_operations.executions SET \"StartedAt\"=clock_timestamp()-interval '1 minute', \"LeaseUntil\"=clock_timestamp()-interval '1 second' WHERE \"Id\"=@id", connection);
            expire.Parameters.AddWithValue("id", job.Id); Assert.Equal(1, await expire.ExecuteNonQueryAsync());
        }
        Assert.False((await queue.Heartbeat(lease)).Owned); Assert.Null(await queue.Claim());
        Assert.Equal(ExecutionStatus.Interrupted, access.Read(a.Actor, a.Library, job.Id).Status);
        Assert.Equal("LEASE_EXPIRED", access.Read(a.Actor, a.Library, job.Id).ErrorCode);
        Assert.False(await queue.Finish(lease, await Process(lease))); Assert.Null(await queue.Claim());
        Assert.Throws<LibraryUnavailableException>(() => access.Result(a.Actor, a.Library, job.Id));
    }

    [Fact]
    public async Task AnOldQueuedRequestExpiresWithoutStartingAfterADispatcherReturns()
    {
        var a = await Fixture(); await using var source = Source(); var queue = new ExecutionQueue(source, new(QueueLifetimeSeconds: 1)); var access = Access(queue);
        var job = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input());
        await Task.Delay(1100);
        Assert.Null(await queue.Claim());
        var expired = access.Read(a.Actor, a.Library, job.Id);
        Assert.Equal(ExecutionStatus.Interrupted, expired.Status); Assert.Equal("QUEUE_EXPIRED", expired.ErrorCode);
        Assert.Null(expired.StartedAt); Assert.Throws<LibraryUnavailableException>(() => access.Result(a.Actor, a.Library, job.Id));
    }

    [Fact]
    public async Task LeaseIsRecheckedAfterWaitingForTheResultRowLock()
    {
        var a = await Fixture();
        var settings = new NpgsqlConnectionStringBuilder(postgres.ExecutionConnection) { ApplicationName = "qp-lease-fence-" + Guid.NewGuid().ToString("N") };
        await using var source = new ExecutionDataSource(NpgsqlDataSource.Create(settings.ConnectionString));
        var queue = new ExecutionQueue(source, new()); var access = Access(queue);
        var job = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()); var lease = (await queue.Claim())!;
        await using (var connection = await source.Data.OpenConnectionAsync())
        {
            await using var shorten = new NpgsqlCommand("UPDATE qp_operations.executions SET \"LeaseUntil\"=clock_timestamp()+interval '2 seconds' WHERE \"Id\"=@id", connection);
            shorten.Parameters.AddWithValue("id", job.Id); await shorten.ExecuteNonQueryAsync();
        }
        await using var blocker = await postgres.Application.OpenConnectionAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var actor = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id', @actor, true)", blocker))
        { actor.Parameters.AddWithValue("actor", a.Actor.Id.ToString()); await actor.ExecuteNonQueryAsync(); }
        await using (var hold = new NpgsqlCommand("SELECT \"Id\" FROM qp_operations.executions WHERE \"Id\"=@id FOR UPDATE", blocker))
        { hold.Parameters.AddWithValue("id", job.Id); Assert.Equal(job.Id, await hold.ExecuteScalarAsync()); }
        var completing = queue.Finish(lease, await Process(lease));
        await using (var probe = await source.Data.OpenConnectionAsync())
        {
            await using var waiting = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE application_name=@name AND wait_event_type='Lock' AND query LIKE '%FOR UPDATE%')", probe);
            waiting.Parameters.AddWithValue("name", settings.ApplicationName!);
            var observed = false;
            for (var attempt = 0; attempt < 40 && !observed; attempt++) { observed = await waiting.ExecuteScalarAsync() is true; if (!observed) await Task.Delay(25); }
            Assert.True(observed, "The finisher must really be waiting on the protected row, not merely delayed by the test.");
        }
        await Task.Delay(2200); await transaction.RollbackAsync(); await blocker.CloseAsync();
        Assert.False(await completing);
        Assert.Throws<LibraryUnavailableException>(() => access.Result(a.Actor, a.Library, job.Id));
        Assert.Null(await queue.Claim()); Assert.Equal(ExecutionStatus.Interrupted, access.Read(a.Actor, a.Library, job.Id).Status);
    }

    [Fact]
    public async Task AHandlerCannotSubstitutePayloadOrResultEvenWithAValidLease()
    {
        var a = await Fixture(); await using var source = Source(); var queue = new ExecutionQueue(source, new()); var access = Access(queue);
        await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()); var lease = (await queue.Claim())!;
        var actual = await Process(lease); var changed = actual with { Text = "Texto sustituido" };
        var forged = lease with { Payload = lease.Payload.Replace("RLS", "Texto sustituido") };
        await Assert.ThrowsAsync<ValidationException>(() => queue.Finish(forged, changed));
        Assert.Equal(ExecutionStatus.Running, access.Read(a.Actor, a.Library, lease.Id).Status);
        Assert.True(await queue.Finish(lease, actual));
    }

    [Fact]
    public async Task FailureDuringResultInsertRollsBackTerminalStateAndAllowsOnlyTheSameLiveLease()
    {
        var a = await Fixture(); await using var source = Source(); var queue = new ExecutionQueue(source, new()); var access = Access(queue);
        var job = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()); var lease = (await queue.Claim())!;
        var suffix = Guid.NewGuid().ToString("N"); var trigger = "qa_execution_atomic_" + suffix;
        await using var migration = new NpgsqlConnection(postgres.MigrationConnection); await migration.OpenAsync();
        // Generated identifier and fixture UUID only. The fault is limited to this synthetic execution.
        await using (var install = new NpgsqlCommand($"""
            CREATE FUNCTION qp_operations.{trigger}() RETURNS trigger LANGUAGE plpgsql AS $fault$
            BEGIN IF NEW."ExecutionId"='{job.Id}'::uuid THEN RAISE EXCEPTION 'synthetic result storage fault'; END IF; RETURN NEW; END $fault$;
            CREATE TRIGGER {trigger} BEFORE INSERT ON qp_operations.execution_results FOR EACH ROW EXECUTE FUNCTION qp_operations.{trigger}();
            """, migration)) await install.ExecuteNonQueryAsync();
        try
        {
            await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => queue.Finish(lease, new(a.Prompt.Id, 1, a.Prompt.Title, "Explica RLS.")));
            Assert.Equal(ExecutionStatus.Running, access.Read(a.Actor, a.Library, job.Id).Status);
            Assert.Throws<LibraryUnavailableException>(() => access.Result(a.Actor, a.Library, job.Id));
        }
        finally
        {
            await using var remove = new NpgsqlCommand($"DROP TRIGGER {trigger} ON qp_operations.execution_results; DROP FUNCTION qp_operations.{trigger}();", migration);
            await remove.ExecuteNonQueryAsync();
        }
        Assert.True(await queue.Finish(lease, await Process(lease)));
        Assert.Equal(ExecutionStatus.Completed, access.Read(a.Actor, a.Library, job.Id).Status);
    }

    [Fact]
    public async Task RoleBoundariesPreventOtherOwnersForgedCompletionRetargetingAndLibraryReads()
    {
        var a = await Fixture(); var b = await Fixture(); await using var source = Source(); var queue = new ExecutionQueue(source, new()); var access = Access(queue);
        var job = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input());
        await using (var connection = await postgres.Application.OpenConnectionAsync())
        {
            await using var none = new NpgsqlCommand("SELECT count(*) FROM qp_operations.executions WHERE \"Id\"=@id", connection);
            none.Parameters.AddWithValue("id", job.Id); Assert.Equal(0L, await none.ExecuteScalarAsync());
            await using var transaction = await connection.BeginTransactionAsync();
            await using var actor = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id', @actor, true)", connection);
            actor.Parameters.AddWithValue("actor", b.Actor.Id.ToString()); await actor.ExecuteNonQueryAsync();
            Assert.Equal(0L, await none.ExecuteScalarAsync());
            await using var cancel = new NpgsqlCommand("UPDATE qp_operations.executions SET \"CancelRequested\"=true WHERE \"Id\"=@id", connection);
            cancel.Parameters.AddWithValue("id", job.Id); Assert.Equal(0, await cancel.ExecuteNonQueryAsync());
            await transaction.RollbackAsync();
            await using var forge = new NpgsqlCommand("UPDATE qp_operations.executions SET \"Status\"='Completed' WHERE \"Id\"=@id", connection);
            forge.Parameters.AddWithValue("id", job.Id);
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => forge.ExecuteNonQueryAsync())).SqlState);
        }
        await using (var connection = await source.Data.OpenConnectionAsync())
        {
            foreach (var sql in new[] { "SELECT * FROM qp_library.prompts", "SELECT * FROM qp_identity.\"AspNetUsers\"", "UPDATE qp_operations.executions SET \"Payload\"='{}'", "UPDATE qp_operations.execution_results SET \"Text\"='Otro'" })
            {
                await using var denied = new NpgsqlCommand(sql, connection);
                Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync())).SqlState);
            }
        }
        access.Cancel(a.Actor, a.Library, job.Id); Assert.Null(await queue.Claim());
    }
}
