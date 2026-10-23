using System;
using System.Threading.Tasks;
using QuickPrompt.History.Models;
using QuickPrompt.History.Repositories;
using Xunit;

namespace QuickPrompt.Tests.History
{
    public class SqliteExecutionHistoryRepositoryTests
    {
        [Fact]
        public async Task CanAddAndGetEntry()
        {
            var repo = new SqliteExecutionHistoryRepository(":memory:");
            var entry = new ExecutionHistoryEntry
            {
                Id = Guid.NewGuid(),
                EngineId = "ChatGPT",
                PromptCompiled = "test",
                ExecutedAt = DateTime.UtcNow,
                Status = "Success",
                UsedFallback = false,
                DeviceId = "dev1",
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            await repo.AddAsync(entry);
            var fetched = await repo.GetByIdAsync(entry.Id);
            Assert.NotNull(fetched);
            Assert.Equal(entry.EngineId, fetched.EngineId);
        }
    }
}
