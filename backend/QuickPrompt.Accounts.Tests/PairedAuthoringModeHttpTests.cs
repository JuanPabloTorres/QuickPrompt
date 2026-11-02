using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    /// <summary>Real account/queue HTTP contract with a synthetic reply; never starts Companion or a model.</summary>
    [Fact]
    public async Task CloudAuthoringDiscoveryKeepsTheQualifiedNonThinkingModeOnTheDeviceWire()
    {
        await using var host = await Start();
        using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail));
        var source = await LocalLibrary(owner);
        var grant = await PollingProposalDevice(owner, device);
        var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope();
        using var nova = new AccountNovaInference(scope.ServiceProvider.GetRequiredService<AccountModelProposals>(),
            actor, source.Library, grant.Connection.Id);
        const string request = "Adapta el prompt para una tarea que todavía no he definido. Pregúntame qué tarea debe hacer antes de redactarlo.";
        var document = new PromptDocument(source.Prompt.Id, source.Prompt.Title, source.Prompt.Description, source.Prompt.Template,
            source.Prompt.CategoryId, source.Prompt.IsFavorite, source.Prompt.IsArchived, source.Prompt.Revision,
            DateTime.UtcNow, DateTime.UtcNow, source.Prompt.Uses, null, source.Prompt.VariableNames, new Dictionary<string, string>());
        var plan = new LiveCommand(SemanticOperation.PROPOSE_ADAPTATION, request) { InputNames = [] }
            .Compile(request, SemanticContext.Empty with { Selected = SemanticEntity.From(document) });
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        // Explicit controlled actor establishes a task before this wire-mode probe.
        var generation = nova.Generator.GenerateAfterSyntheticTask(new(Guid.NewGuid(), request, SourcePromptId: document.Id,
            SourceRevision: document.Revision) { Interpretation = plan }, plan.Preparation(), [new(document, 1, "Origen autorizado")], stop.Token);
        try
        {
            var lease = await AwaitProposal(device, generation);
            Assert.Equal("qwen3:8b", lease.Packet.GetProperty("model").GetString());
            Assert.False(lease.Packet.GetProperty("think").GetBoolean());
            Assert.Equal(1, lease.Packet.GetProperty("options").GetProperty("max_requests").GetInt32());
            Assert.Equal(1400, lease.Packet.GetProperty("options").GetProperty("num_predict").GetInt32());
            var reply = JsonSerializer.SerializeToElement(new
            {
                done = true, done_reason = "stop", model = "qwen3:8b",
                message = new { content = JsonSerializer.Serialize(AuthoringSyntheticReview.CurrentAuthorDecisionAsk(request)) },
                coreRequestId = Guid.NewGuid().ToString("N"), modelCalls = 1,
                prompt_eval_count = 30, eval_count = 20
            });
            Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(device, lease, reply)).StatusCode);
            var result = await generation;
            Assert.Equal(PreparationDecision.ASK_CLARIFICATION, result.Decision);
            Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
            Assert.Equal(source.Prompt, await owner.GetFromJsonAsync<AccountPrompt>(
                $"/api/accounts/v1/libraries/{source.Library}/prompts/{source.Prompt.Id}"));
        }
        finally
        {
            stop.Cancel();
            // Drain the in-flight task even when a wire assertion fails; do not leave a background stage.
            await Record.ExceptionAsync(async () => { await generation; });
        }
    }
}
