using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using QuickPrompt.History.Models;
using QuickPrompt.History.Repositories;
using QuickPrompt.History.Sync;
using Xunit;

namespace QuickPrompt.Tests.History
{
    public class SyncServiceTests
    {
        [Fact]
        public async Task SyncNowAsync_RespectsLoginAndSyncEnabled()
        {
            var localRepo = new Mock<IExecutionHistoryRepository>();
            var cloudRepo = new Mock<IExecutionHistoryCloudRepository>();
            var sync = new SyncService(localRepo.Object, cloudRepo.Object, () => false, () => true);
            await sync.SyncNowAsync(CancellationToken.None); // No sync, not logged in
            cloudRepo.Verify(x => x.BatchUpsertAsync(It.IsAny<System.Collections.Generic.IEnumerable<ExecutionHistoryEntry>>()), Times.Never);
        }
    }
}
