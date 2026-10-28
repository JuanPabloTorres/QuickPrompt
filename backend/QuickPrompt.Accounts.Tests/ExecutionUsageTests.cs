using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class ExecutionQueueTests
{
    [Fact]
    public async Task CompileAndResultReadsDoNotCountButExposeOnlyTheVerifiedPrivatePreparation()
    {
        var a = await Fixture(); var b = await Fixture(); await using var source = Source(); var queue = new ExecutionQueue(source, new()); var access = Access(queue);
        var request = Input(value: "ñ <literal>\n二"); var job = await access.Compile(a.Actor, a.Library, a.Prompt.Id, request);
        var lease = (await queue.Claim())!; Assert.True(await queue.Finish(lease, await Process(lease)));
        await using var scope = postgres.Services.CreateAsyncScope(); var libraries = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var result = access.Result(a.Actor, a.Library, job.Id);
        Assert.Equal(job.Id, result.Usage!.OperationId); Assert.Equal(request.Values["tema"], result.Usage.Values["tema"]);
        Assert.Equal(result.Text, access.Result(a.Actor, a.Library, job.Id).Text);
        Assert.Equal(0, (await libraries.GetPrompt(a.Actor, a.Library, a.Prompt.Id)).Uses);
        Assert.Throws<LibraryUnavailableException>(() => access.Result(b.Actor, a.Library, job.Id));
        await libraries.RecordUse(a.Actor, a.Library, a.Prompt.Id, new(result.PromptRevision, result.Usage.Values, result.Usage.OperationId, result.Text));
        await libraries.RecordUse(a.Actor, a.Library, a.Prompt.Id, new(result.PromptRevision, result.Usage.Values, result.Usage.OperationId, result.Text));
        Assert.Equal(1, (await libraries.GetPrompt(a.Actor, a.Library, a.Prompt.Id)).Uses);
        access.Forget(a.Actor, a.Library, job.Id);
        Assert.Throws<LibraryUnavailableException>(() => access.Result(a.Actor, a.Library, job.Id));
    }

}
