using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Local;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class ExecutionQueueTests
{
    [Fact]
    public async Task ForgetErasesContentButPreservesIdentityQuotaAndTheOtherAccount()
    {
        var a = await Fixture(); var b = await Fixture();
        await using var source = Source();
        var queue = new ExecutionQueue(source, new(DailyAdmissions: 1)); var access = Access(queue);
        var input = Input(value: "dato privado A");
        var job = await access.Compile(a.Actor, a.Library, a.Prompt.Id, input);
        Assert.Throws<ConflictException>(() => access.Forget(a.Actor, a.Library, job.Id));
        var lease = (await queue.Claim())!;
        Assert.Equal(job.Id, lease.Id);
        Assert.Throws<ConflictException>(() => access.Forget(a.Actor, a.Library, job.Id));
        Assert.True(await queue.Finish(lease, await Process(lease)));
        var other = await access.Compile(b.Actor, b.Library, b.Prompt.Id, Input(value: "dato privado B"));
        var otherLease = (await queue.Claim())!;
        Assert.Equal(other.Id, otherLease.Id);
        Assert.True(await queue.Finish(otherLease, await Process(otherLease)));

        Assert.Throws<LibraryUnavailableException>(() => access.Forget(b.Actor, a.Library, job.Id));
        Assert.Throws<LibraryUnavailableException>(() => access.Forget(b.Actor, b.Library, job.Id));
        var erased = access.Forget(a.Actor, a.Library, job.Id);
        Assert.Equal(ExecutionStatus.Completed, erased.Status); Assert.NotNull(erased.ForgottenAt);
        Assert.Equal(erased, access.Forget(a.Actor, a.Library, job.Id));
        Assert.Equal(erased, await access.Compile(a.Actor, a.Library, a.Prompt.Id, input));
        Assert.Equal(erased, Assert.Single(access.List(a.Actor, a.Library)));
        Assert.Throws<LibraryUnavailableException>(() => access.Result(a.Actor, a.Library, job.Id));
        Assert.Equal("Explica dato privado B.", access.Result(b.Actor, b.Library, other.Id).Text);
        Assert.False(await queue.Finish(lease, await Process(lease)));
        Assert.Null(await queue.Claim());
        Assert.Equal("DAILY_LIMIT", (await Assert.ThrowsAsync<ExecutionQuotaException>(() => access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()))).Code);
        await using var connection = await source.Data.OpenConnectionAsync();
        await using var row = new NpgsqlCommand("SELECT \"Payload\"::text='{}' AND \"ForgottenAt\" IS NOT NULL AND \"RequestHash\"<>'' AND \"Attempts\"=1 FROM qp_operations.executions WHERE \"Id\"=@id", connection);
        row.Parameters.AddWithValue("id", job.Id); Assert.Equal(true, await row.ExecuteScalarAsync());
    }

    [Fact]
    public async Task RetentionHidesExpiredResultsAndErasesBothAccountsWithoutTouchingRecentOrActiveWork()
    {
        var a = await Fixture(); var b = await Fixture(); await using var source = Source();
        var queue = new ExecutionQueue(source, new()); var access = Access(queue);
        var oldA = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()); var leaseA = (await queue.Claim())!;
        await queue.Finish(leaseA, await Process(leaseA));
        var oldB = await access.Compile(b.Actor, b.Library, b.Prompt.Id, Input()); var leaseB = (await queue.Claim())!;
        await queue.Finish(leaseB, await Process(leaseB));
        var recent = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()); var recentLease = (await queue.Claim())!;
        await queue.Finish(recentLease, await Process(recentLease));
        var running = await access.Compile(b.Actor, b.Library, b.Prompt.Id, Input()); var live = (await queue.Claim())!;
        await using (var connection = await source.Data.OpenConnectionAsync())
        {
            await using var age = new NpgsqlCommand("UPDATE qp_operations.executions SET \"FinishedAt\"=clock_timestamp()-interval '8 days' WHERE \"Id\"=ANY(@ids)", connection);
            age.Parameters.AddWithValue("ids", new[] { oldA.Id, oldB.Id }); Assert.Equal(2, await age.ExecuteNonQueryAsync());
        }
        // Even before the dispatcher physically prunes, the account read boundary denies old content.
        Assert.Throws<LibraryUnavailableException>(() => access.Result(a.Actor, a.Library, oldA.Id));
        Assert.Null(await queue.Claim());
        Assert.NotNull(access.Read(a.Actor, a.Library, oldA.Id).ForgottenAt);
        Assert.NotNull(access.Read(b.Actor, b.Library, oldB.Id).ForgottenAt);
        Assert.Null(access.Read(a.Actor, a.Library, recent.Id).ForgottenAt);
        Assert.NotNull(access.Result(a.Actor, a.Library, recent.Id));
        Assert.Null(access.Read(b.Actor, b.Library, running.Id).ForgottenAt);
        Assert.True(await queue.Finish(live, await Process(live)));
        await using var check = await source.Data.OpenConnectionAsync();
        await using var actor = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,false)", check);
        actor.Parameters.AddWithValue("actor", a.Actor.Id.ToString()); await actor.ExecuteNonQueryAsync();
        await using var results = new NpgsqlCommand("SELECT count(*) FROM qp_operations.execution_results WHERE \"ExecutionId\"=ANY(@ids)", check);
        results.Parameters.AddWithValue("ids", new[] { oldA.Id, oldB.Id }); Assert.Equal(0L, await results.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ErasureFunctionsKeepActorAndRoleBoundariesAndRollbackTogetherOnStorageFailure()
    {
        var a = await Fixture(); await using var source = Source(); var queue = new ExecutionQueue(source, new()); var access = Access(queue);
        var job = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()); var lease = (await queue.Claim())!;
        await queue.Finish(lease, await Process(lease));
        await using (var app = await postgres.Application.OpenConnectionAsync())
        {
            await using var anonymous = new NpgsqlCommand("SELECT qp_operations.forget_execution_content(@library,@id)", app);
            anonymous.Parameters.AddWithValue("library", a.Library); anonymous.Parameters.AddWithValue("id", job.Id);
            Assert.Equal(0, await anonymous.ExecuteScalarAsync());
            foreach (var sql in new[] { "SELECT qp_operations.expire_execution_content()", "SELECT qp_operations.erase_execution_content(gen_random_uuid())",
                "UPDATE qp_operations.executions SET \"Payload\"='{}'", "UPDATE qp_operations.executions SET \"ForgottenAt\"=clock_timestamp()", "DELETE FROM qp_operations.execution_results" })
            { await using var denied = new NpgsqlCommand(sql, app); Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync())).SqlState); }
        }
        await using (var worker = await source.Data.OpenConnectionAsync())
        {
            foreach (var sql in new[] { "SELECT qp_operations.forget_execution_content(gen_random_uuid(),gen_random_uuid())", "SELECT qp_operations.erase_execution_content(gen_random_uuid())" })
            { await using var denied = new NpgsqlCommand(sql, worker); Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync())).SqlState); }
        }
        // A failure after deleting the result must roll back the result as well as the input erasure.
        var trigger = "qa_forget_atomic_" + Guid.NewGuid().ToString("N");
        await using var migration = new NpgsqlConnection(postgres.MigrationConnection); await migration.OpenAsync();
        await using (var install = new NpgsqlCommand($"""
            CREATE FUNCTION qp_operations.{trigger}() RETURNS trigger LANGUAGE plpgsql AS $fault$
            BEGIN IF NEW."Id"='{job.Id}'::uuid AND NEW."ForgottenAt" IS NOT NULL THEN RAISE EXCEPTION 'synthetic forget storage fault'; END IF; RETURN NEW; END $fault$;
            CREATE TRIGGER {trigger} BEFORE UPDATE ON qp_operations.executions FOR EACH ROW EXECUTE FUNCTION qp_operations.{trigger}();
            """, migration)) await install.ExecuteNonQueryAsync();
        try
        {
            Assert.Throws<PostgresException>(() => access.Forget(a.Actor, a.Library, job.Id));
            Assert.Null(access.Read(a.Actor, a.Library, job.Id).ForgottenAt);
            Assert.Equal("Explica RLS.", access.Result(a.Actor, a.Library, job.Id).Text);
        }
        finally
        {
            await using var remove = new NpgsqlCommand($"DROP TRIGGER {trigger} ON qp_operations.executions; DROP FUNCTION qp_operations.{trigger}();", migration);
            await remove.ExecuteNonQueryAsync();
        }
        Assert.NotNull(access.Forget(a.Actor, a.Library, job.Id).ForgottenAt);
    }

    [Fact]
    public async Task ExpirationIsBoundedAndItsPrivateWriterCannotRestoreAResult()
    {
        var a = await Fixture(); await using var source = Source();
        await using var connection = await source.Data.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var actor = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,true)", connection))
        { actor.Parameters.AddWithValue("actor", a.Actor.Id.ToString()); await actor.ExecuteNonQueryAsync(); }
        // Large synthetic backlog through the real limited dispatcher role; no private database.
        await using (var create = new NpgsqlCommand("""
            INSERT INTO qp_operations.executions ("Id","OwnerId","LibraryId","OperationId","Kind","Payload","RequestHash","Status","CancelRequested","CreatedAt","Attempts")
            SELECT gen_random_uuid(),@actor,@library,gen_random_uuid(),'CompilePrompt','{"synthetic":"expired private values"}'::jsonb,repeat('A',64),'Queued',false,clock_timestamp()-interval '9 days',0
            FROM generate_series(1,103);
            UPDATE qp_operations.executions SET "Status"='Cancelled',"FinishedAt"=clock_timestamp()-interval '8 days' WHERE "OwnerId"=@actor;
            """, connection))
        { create.Parameters.AddWithValue("actor", a.Actor.Id); create.Parameters.AddWithValue("library", a.Library); await create.ExecuteNonQueryAsync(); }
        await using (var expire = new NpgsqlCommand("SELECT qp_operations.expire_execution_content()", connection))
        { Assert.Equal(100, await expire.ExecuteScalarAsync()); Assert.Equal(3, await expire.ExecuteScalarAsync()); Assert.Equal(0, await expire.ExecuteScalarAsync()); }
        await using (var remaining = new NpgsqlCommand("SELECT count(*) FROM qp_operations.executions WHERE \"OwnerId\"=@actor AND (\"Payload\"<>'{}'::jsonb OR \"ForgottenAt\" IS NULL)", connection))
        { remaining.Parameters.AddWithValue("actor", a.Actor.Id); Assert.Equal(0L, await remaining.ExecuteScalarAsync()); }
        await using (var forge = new NpgsqlCommand("""
            INSERT INTO qp_operations.execution_results ("ExecutionId","OwnerId","LibraryId","PromptId","PromptRevision","Title","Text","CreatedAt")
            SELECT "Id","OwnerId","LibraryId",@prompt,1,'Forbidden restore','Erased content',clock_timestamp()
            FROM qp_operations.executions WHERE "OwnerId"=@actor LIMIT 1
            """, connection))
        {
            forge.Parameters.AddWithValue("actor", a.Actor.Id); forge.Parameters.AddWithValue("prompt", a.Prompt.Id);
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => forge.ExecuteNonQueryAsync())).SqlState);
        }
        // Rollback the deliberately failed raw-SQL fixture; no 103-row queue survives this test.
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task ConcurrentForgetFromIndependentPoolsReturnsOneStableReceipt()
    {
        var a = await Fixture(); await using var source = Source(); var queue = new ExecutionQueue(source, new()); var access = Access(queue);
        var job = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input()); var lease = (await queue.Claim())!;
        Assert.True(await queue.Finish(lease, await Process(lease)));
        await using var poolOne = NpgsqlDataSource.Create(postgres.ApplicationConnection);
        await using var poolTwo = NpgsqlDataSource.Create(postgres.ApplicationConnection);
        var first = new AccountExecutionAccess(new AccountAgentMemory(poolOne), queue);
        var second = new AccountExecutionAccess(new AccountAgentMemory(poolTwo), queue);
        var receipts = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(() =>
            (index % 2 == 0 ? first : second).Forget(a.Actor, a.Library, job.Id))));
        Assert.NotNull(receipts[0].ForgottenAt);
        Assert.All(receipts, receipt => Assert.Equal(receipts[0], receipt));
        Assert.Throws<LibraryUnavailableException>(() => access.Result(a.Actor, a.Library, job.Id));
        Assert.False(await queue.Finish(lease, await Process(lease)));
    }
}
