using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class ExecutionQueueTests
{
    [Theory, Trait("Category", "QueueAdmissionLifecycle")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledOrExpiredQueuedWorkReleasesAdmissionWithoutAWorkerClaimAndStillConsumesItsDailyAdmission(bool expire)
    {
        var owner = await Fixture();
        await using var source = Source();
        var limits = new ExecutionLimits(OwnerQueued: 1, GlobalQueued: 64, DailyAdmissions: 2,
            QueueLifetimeSeconds: expire ? 1 : 900);
        var access = Access(new ExecutionQueue(source, limits));
        var ownJobs = new List<ExecutionView>();
        var originalRequest = Input();
        try
        {
            var original = await access.Compile(owner.Actor, owner.Library, owner.Prompt.Id, originalRequest);
            ownJobs.Add(original);
            Assert.Equal(original.Id, (await access.Compile(owner.Actor, owner.Library, owner.Prompt.Id, originalRequest)).Id);
            if (expire)
            {
                // Positive real wait, checked against PostgreSQL's clock. No timestamp
                // mutation, fake clock or dispatcher claim creates this precondition.
                await Task.Delay(TimeSpan.FromMilliseconds(1200));
                await using var connection = await source.Data.OpenConnectionAsync();
                await using var elapsed = new NpgsqlCommand("SELECT clock_timestamp() >= @created + make_interval(secs => @seconds)", connection);
                elapsed.Parameters.AddWithValue("created", original.CreatedAt);
                elapsed.Parameters.AddWithValue("seconds", limits.QueueLifetimeSeconds);
                Assert.Equal(true, await elapsed.ExecuteScalarAsync());
            }
            else Assert.True(access.Cancel(owner.Actor, owner.Library, original.Id).CancelRequested);

            // No Claim runs between cancellation/expiry and this independent operation.
            var nextRequest = Input();
            var next = await access.Compile(owner.Actor, owner.Library, owner.Prompt.Id, nextRequest);
            ownJobs.Add(next);
            Assert.NotEqual(original.Id, next.Id);
            var originalReplay = await access.Compile(owner.Actor, owner.Library, owner.Prompt.Id, originalRequest);
            Assert.Equal(original.Id, originalReplay.Id);
            Assert.Null(originalReplay.StartedAt);
            Assert.NotEqual(ExecutionStatus.Running, originalReplay.Status);
            Assert.NotEqual(ExecutionStatus.Completed, originalReplay.Status);
            if (!expire) Assert.True(originalReplay.CancelRequested);
            Assert.Equal(next.Id, (await access.Compile(owner.Actor, owner.Library, owner.Prompt.Id, nextRequest)).Id);

            access.Cancel(owner.Actor, owner.Library, next.Id);
            // Releasing queue capacity never refunds daily admissions or lets replay
            // create another execution identity. Both original rows remain receipts.
            var quota = await Assert.ThrowsAsync<ExecutionQuotaException>(() =>
                access.Compile(owner.Actor, owner.Library, owner.Prompt.Id, Input()));
            Assert.Equal("DAILY_LIMIT", quota.Code);
            Assert.Equal(2, access.List(owner.Actor, owner.Library).Length);
            foreach (var job in ownJobs)
            {
                Assert.Null(access.Read(owner.Actor, owner.Library, job.Id).StartedAt);
                Assert.Throws<LibraryUnavailableException>(() => access.Result(owner.Actor, owner.Library, job.Id));
            }
        }
        finally
        {
            // Do not claim, delete or terminalize another test's queued work.
            foreach (var job in ownJobs) access.Cancel(owner.Actor, owner.Library, job.Id);
        }
    }

    [Fact, Trait("Category", "QueueAdmissionLifecycle")]
    public async Task AStillEligibleQueuedWorkKeepsTheOwnerLimitWhileItsReplayKeepsTheSameReceipt()
    {
        var owner = await Fixture();
        await using var source = Source();
        var access = Access(new ExecutionQueue(source, new(OwnerQueued: 1, GlobalQueued: 64, DailyAdmissions: 2)));
        var request = Input();
        var job = await access.Compile(owner.Actor, owner.Library, owner.Prompt.Id, request);
        try
        {
            Assert.Equal(job.Id, (await access.Compile(owner.Actor, owner.Library, owner.Prompt.Id, request)).Id);
            var quota = await Assert.ThrowsAsync<ExecutionQuotaException>(() =>
                access.Compile(owner.Actor, owner.Library, owner.Prompt.Id, Input()));
            Assert.Equal("OWNER_QUEUE_FULL", quota.Code);
            var retained = Assert.Single(access.List(owner.Actor, owner.Library));
            Assert.Equal(job.Id, retained.Id);
            Assert.False(retained.CancelRequested);
            Assert.Null(retained.StartedAt);
        }
        finally { access.Cancel(owner.Actor, owner.Library, job.Id); }
    }
}
