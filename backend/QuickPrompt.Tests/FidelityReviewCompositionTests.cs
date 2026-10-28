using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using static QuickPrompt.Nova.Tests.AuthoringReviewTestFixture;

namespace QuickPrompt.Nova.Tests;

// Controlled protocol/factory tests. Parsing an inventory is not semantic or human acceptance.
public sealed class FidelityReviewCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASourceTitleCannotBecomeRequestedMetadataOrCauseAnotherBodyJudgment(bool secondReplyWouldRepeatInvalidTitle)
    {
        const string sourceTitle = "QA · Elección de copia";
        const string request = "Centra la agenda en decisiones. Conserva el marcador <puntos> y la prohibición de inventar responsables.";
        var transport = new Transport((_, _, count) => new { requestedTitle = count == 1 || secondReplyWouldRepeatInvalidTitle ? sourceTitle : null });
        var reviewer = new DraftFidelityReviewer(transport);
        await Assert.ThrowsAsync<ValidationException>(() => reviewer.ReadRequestedTitle(request, ["puntos"], "qwen3:8b", default));
        var packet = Assert.Single(transport.Calls).Packet;
        var data = Material(packet);
        Assert.Equal(request, data.GetProperty("userRequest").GetString());
        Assert.False(data.TryGetProperty("sourceTitle", out _)); Assert.False(data.TryGetProperty("proposedInstruction", out _));
        Assert.Equal(new[] { "requestedTitle" }, packet.GetProperty("format").GetProperty("properties").EnumerateObject().Select(property => property.Name));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualFactoryBorrowsTransportForThePriorInventoryWithoutConstructingInterpreterOrEditor(bool knowledge)
    {
        var services = new ServiceCollection(); var transport = new Transport();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["QuickPrompt:Ollama"] = "http://127.0.0.1:1", ["QuickPrompt:Preparation:AuthoringUseContract"] = knowledge.ToString()
        }).Build();
        var path = Path.Combine(Path.GetTempPath(), "nova-fidelity-composition-" + Guid.NewGuid(), "synthetic.sqlite");
        var module = Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!;
        module.GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [services, configuration, path]);
        services.AddSingleton<IValidatedAgentModelTransport>(transport);
        services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("Interpreter construction forbidden."));
        services.AddSingleton<ILocalLlmClient>(_ => throw new InvalidOperationException("Editing client construction forbidden."));
        using var provider = services.BuildServiceProvider();
        var generator=Assert.IsType<PromptDraftGenerator>(provider.GetRequiredService<IPromptGenerator>());
        var input=Input("Explica un tema con claridad.");using var cancellation=new CancellationTokenSource();
        var diagnostics=new List<int>();
        var proposal=await generator.GenerateAfterSyntheticReview(input,input.Interpretation!.Preparation(),[],cancellation.Token,(stage,_,_)=>diagnostics.Add(stage));
        var call=Assert.Single(transport.Calls);
        Assert.Equal("GenerateDraftProposal",call.Purpose);Assert.Equal(cancellation.Token,call.Token);
        Assert.Equal(input.Model,call.Packet.GetProperty("model").GetString());Assert.False(call.Packet.GetProperty("think").GetBoolean());
        Assert.Equal(LocalModelPolicy.AgentContextLength,call.Packet.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.Equal(3500,call.Packet.GetProperty("options").GetProperty("num_predict").GetInt32());
        var system=call.Packet.GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.DoesNotContain("Application system knowledge",system,StringComparison.Ordinal);
        Assert.Equal(input.TaskRequest,Material(call.Packet).GetProperty("originalRequest").GetString());
        Assert.Equal(new[]{193},diagnostics);Assert.Empty(transport.Rejections);
        Assert.False(input.Interpretation.Goal!.HasContentAssessment(proposal.TemplateText));
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval(proposal.TemplateText));
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(PromptDraftGenerator)));
    }

    [Fact]
    public async Task CancellationAndUnavailabilityCannotProduceAResolvedInventoryOrAnotherAttempt()
    {
        var transport=new Transport();var forbidden=new ForbiddenDependencies();var generator=new PromptDraftGenerator(transport,forbidden,forbidden,forbidden);
        var input=Input("Explica un tema.");using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>generator.GenerateAfterSyntheticReview(input,input.Interpretation!.Preparation(),[],cancellation.Token));
        Assert.Empty(transport.Calls);transport.Failure=new LlmUnavailableException("Synthetic unavailable");
        var error=await Assert.ThrowsAsync<PreparationToolException>(()=>generator.GenerateAfterSyntheticReview(input,input.Interpretation!.Preparation(),[],default));
        Assert.Equal("MODEL_UNAVAILABLE",error.Code);Assert.Single(transport.Calls);Assert.Empty(transport.Rejections);
        Assert.False(input.Interpretation!.Goal!.HasAuthoringStructure("Explica un tema."));
    }

    [Fact]
    public async Task ExistingPrivateBridgeUsesBorrowedReviewerAndPerCallDiagnosticsWithoutDisposingIt()
    {
        var reviewer = new BorrowedReviewer(); var input = Input("Explica un tema."); var proposal = Proposal("Explica un tema."); var diagnostics = new List<int>();
        using var cancellation = new CancellationTokenSource();
        using (var inference = new PreparationInference("http://127.0.0.1:1", fidelityReviewer: reviewer)) {
            inference.SemanticDiagnostic = (stage, _, _) => diagnostics.Add(stage);
            var method = typeof(PreparationInference).GetMethod("ReviewDraftFidelity", BindingFlags.NonPublic | BindingFlags.Instance)!;
            await (Task)method.Invoke(inference, [input, proposal, Array.Empty<RetrievalCandidate>(), cancellation.Token, null])!;
        }
        Assert.Equal(1, reviewer.Calls); Assert.Same(input, reviewer.Input); Assert.Same(proposal, reviewer.Proposal);
        Assert.Equal(cancellation.Token, reviewer.Token); Assert.Equal(new[] { 190 }, diagnostics); Assert.False(reviewer.Disposed);
    }

    private sealed class BorrowedReviewer : IDraftFidelityReviewer, IDisposable
    {
        internal int Calls; internal bool Disposed; internal PrepareInput? Input; internal PromptProposal? Proposal; internal CancellationToken Token;
        public Task ReviewDraftFidelity(PrepareInput input, PromptProposal proposal, RetrievalCandidate[] candidates, CancellationToken ct,
            string? boundTitleLiteral = null, Action<int, string, string?>? diagnostic = null)
        { ct.ThrowIfCancellationRequested(); Assert.False(Disposed); Calls++; Input = input; Proposal = proposal; Token = ct; diagnostic?.Invoke(190, "Controlled bridge, no content assessment.", null); return Task.CompletedTask; }
        public void Dispose() => Disposed = true;
    }
}
