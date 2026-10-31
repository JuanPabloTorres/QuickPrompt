using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    private static string ModelMaterial(LocalProposalLease lease) => string.Join("\n", lease.Packet.GetProperty("messages").EnumerateArray().Select(message => message.GetProperty("content").GetString()));
    private static JsonElement ContentReply(object content) => ModelReply(JsonSerializer.Serialize(content));
    private static JsonElement ModelReply(string content) => JsonSerializer.SerializeToElement(new
    {
        done = true,
        done_reason = "stop",
        model = "qwen3:8b",
        message = new { content },
        coreRequestId = Guid.NewGuid().ToString("N"),
        modelCalls = 1,
        prompt_eval_count = 30,
        eval_count = 20
    });

    [Fact]
    public async Task CanonicalNovaCapabilitiesUseSeparatePrivateCompanionsAndNeverConstructServerHttp()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App);
        using var deviceA = Client(host.App); using var deviceB = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var ownA = await LocalLibrary(a); var ownB = await LocalLibrary(b);
        var grantA = await PollingProposalDevice(a, deviceA); var grantB = await PollingProposalDevice(b, deviceB);
        var actorA = await ProposalActor(a); var actorB = await ProposalActor(b);
        await using var scope = host.App.Services.CreateAsyncScope(); var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        using var novaA = new AccountNovaInference(proposals, actorA, ownA.Library, grantA.Connection.Id);
        using var novaB = new AccountNovaInference(proposals, actorB, ownB.Library, grantB.Connection.Id);
        Assert.IsType<PromptDraftGenerator>(novaA.Generator);
        Assert.Null(typeof(AccountNovaInference).GetProperty("Fidelity"));
        Assert.IsType<SemanticCandidateSelector>(novaA.Candidates); Assert.IsType<UiDecisionModel>(novaA.UiDecision);
        await Assert.ThrowsAsync<LlmUnavailableException>(() => novaA.Editor.Available(default));
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var extractedA = novaA.InputValues.ExtractInputs(new(Guid.NewGuid(), "tema: álgebra privada A"), "Explica <tema>.", ["tema"], limit.Token);
        var extractedB = novaB.InputValues.ExtractInputs(new(Guid.NewGuid(), "tema: historia privada B"), "Explica <tema>.", ["tema"], limit.Token);
        var leaseA = await AwaitProposal(deviceA, extractedA); var leaseB = await AwaitProposal(deviceB, extractedB);
        Assert.Contains("álgebra privada A", ModelMaterial(leaseA)); Assert.DoesNotContain("historia privada B", ModelMaterial(leaseA));
        Assert.Contains("historia privada B", ModelMaterial(leaseB)); Assert.DoesNotContain("álgebra privada A", ModelMaterial(leaseB));
        var responseA = ContentReply(new { values = new[] { new { name = "tema", value = "álgebra privada A", evidence = "tema: álgebra privada A" } }, question = "" });
        var responseB = ContentReply(new { values = new[] { new { name = "tema", value = "historia privada B", evidence = "tema: historia privada B" } }, question = "" });
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(deviceB, leaseB, responseB)).StatusCode);
        Assert.Equal("historia privada B", Assert.Single((await extractedB).Values).Value); Assert.False(extractedA.IsCompleted);
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(deviceA, leaseA, responseA)).StatusCode);
        Assert.Equal("álgebra privada A", Assert.Single((await extractedA).Values).Value);
        var execution = novaA.Editor.Execute(new(Guid.NewGuid(), ownA.Prompt.Id, ownA.Prompt.Revision, ownA.Prompt.Title,
            "Explica álgebra privada A.", new Dictionary<string, string>(), DateTime.UtcNow), "qwen3:8b", true, null, limit.Token);
        var executionLease = await AwaitProposal(deviceA, execution);
        Assert.False(executionLease.Packet.GetProperty("stream").GetBoolean());
        Assert.Contains("Explica álgebra privada A.", ModelMaterial(executionLease));
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(deviceA, executionLease, ModelReply("Respuesta privada de A"))).StatusCode);
        Assert.Equal("Respuesta privada de A", (await execution).Text);
        using var wrong = new AccountNovaInference(proposals, actorA, ownA.Library, grantB.Connection.Id);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => wrong.InputValues.ExtractInputs(new(Guid.NewGuid(), "tema: no permitido"), "Explica <tema>.", ["tema"], limit.Token));
        Assert.Equal(ownA.Prompt, await a.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{ownA.Library}/prompts/{ownA.Prompt.Id}"));
        Assert.Equal(ownB.Prompt, await b.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{ownB.Library}/prompts/{ownB.Prompt.Id}"));
    }
}
