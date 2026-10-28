using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class ExecutionWorkerTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Theory]
    [InlineData("cancel", ExecutionStatus.Cancelled)]
    [InlineData("stop", ExecutionStatus.Interrupted)]
    [InlineData("timeout", ExecutionStatus.Failed)]
    public async Task DispatcherBoundsWaitingAndRejectsLateCompletion(string action, ExecutionStatus expected)
    {
        var owner = await postgres.CreateUser();
        var actor = LibraryActor.FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.ToString())], "fixture")));
        await using var scope = postgres.Services.CreateAsyncScope(); var libraries = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var library = await libraries.CreateLibrary(actor, "QA worker");
        var prompt = await libraries.SavePrompt(actor, library.Id, null, 0, "Worker", "Explica <tema>.");
        await using var source = new ExecutionDataSource(NpgsqlDataSource.Create(postgres.ExecutionConnection));
        var queue = new ExecutionQueue(source, new(MaximumSeconds: action == "timeout" ? 1 : 30));
        var access = new AccountExecutionAccess(new AccountAgentMemory(postgres.Application), queue);
        var job = await access.Compile(actor, library.Id, prompt.Id, new(Guid.NewGuid(), 1, new() { ["tema"] = "cancelación" }));
        var handler = new HeldHandler();
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddLogging(); services.AddSingleton(queue); services.AddSingleton<IAccountExecutionHandler>(handler);
            services.AddHostedService<ExecutionWorker>();
        }).Build();
        await host.StartAsync();
        try
        {
            await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (action == "cancel") access.Cancel(actor, library.Id, job.Id);
            if (action == "stop") await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (access.Read(actor, library.Id, job.Id).Status is ExecutionStatus.Queued or ExecutionStatus.Running && timer.Elapsed < TimeSpan.FromSeconds(6))
                await Task.Delay(50);
            Assert.Equal(expected, access.Read(actor, library.Id, job.Id).Status);
            Assert.Equal(1, handler.Calls);
            // This deliberately non-cooperative asynchronous handler completes late. Its value cannot resurrect the run.
            handler.Release.TrySetResult(new(prompt.Id, 1, prompt.Title, "Explica cancelación."));
            await Task.Delay(50);
            Assert.Equal(expected, access.Read(actor, library.Id, job.Id).Status);
            Assert.Throws<LibraryUnavailableException>(() => access.Result(actor, library.Id, job.Id));
        }
        finally { handler.Release.TrySetCanceled(); await host.StopAsync(); }
    }

    private sealed class HeldHandler : IAccountExecutionHandler
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ExecutionOutput> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public Task<ExecutionOutput> Execute(ExecutionLease lease, CancellationToken cancellation)
        { Interlocked.Increment(ref Calls); Entered.TrySetResult(); return Release.Task; }
    }
}
