using QuickPrompt.Accounts.Executions;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class ExecutionQueueTests
{
    [Fact]
    public async Task LiveListKeepsOldActiveWorkWhenNewerReceiptsReachThePageBound()
    {
        var a = await Fixture();
        await using var source = Source();
        // Exercise another supported module configuration without raising the host's fixed quota.
        var queue = new ExecutionQueue(source, new(OwnerRunning: 2, DailyAdmissions: 200, MaximumSeconds: 600));
        var access = Access(queue);
        var old = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input(value: "older-active"));
        var held = (await queue.Claim())!; Assert.Equal(old.Id, held.Id);
        try
        {
            for (var index = 0; index < 100; index++)
            {
                var next = await access.Compile(a.Actor, a.Library, a.Prompt.Id, Input(value: "newer-receipt"));
                var lease = (await queue.Claim())!; Assert.Equal(next.Id, lease.Id);
                Assert.True(await queue.Finish(lease, await Process(lease)));
                if (index % 10 == 0) Assert.True((await queue.Heartbeat(held)).Owned);
            }
            var snapshot = access.List(a.Actor, a.Library);
            Assert.Equal(100, snapshot.Length);
            Assert.Contains(snapshot, item => item.Id == old.Id);
            Assert.Equal(old.Id, snapshot[0].Id);
            Assert.Equal(ExecutionStatus.Running, snapshot[0].Status);
            Assert.Equal(99, snapshot.Count(item => item.Status == ExecutionStatus.Completed));
        }
        finally { await queue.Finish(held, null, interrupted: true); }
    }
}
