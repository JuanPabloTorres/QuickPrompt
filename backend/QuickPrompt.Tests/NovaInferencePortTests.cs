using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class NovaInferencePortTests
{
    [Fact] public async Task ActualCompositionConsumesTheSubstitutedSemanticPortWithoutConstructingAnotherInterpreter()
    {
        var client = new Client(); var semantic = new Semantic(); var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["QuickPrompt:Ollama"] = "http://127.0.0.1:1"
        }).Build();
        var path = Path.Combine(Path.GetTempPath(), "nova-inference-port-" + Guid.NewGuid(), "synthetic.sqlite");
        var module = Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!;
        module.GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [services, configuration, path]);
        services.AddSingleton<ILocalLlmClient>(client);
        services.AddSingleton<ISemanticIntentInterpreter>(semantic);
        services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("Private interpreter construction is forbidden."));
        using var provider = services.BuildServiceProvider();
        var inference = provider.GetRequiredService<INovaInference>();
        using var cancellation = new CancellationTokenSource();
        var result = await inference.Interpret("Encargo íntegro 🛰️", "synthetic:model", cancellation.Token);
        Assert.Equal(NovaIntent.CREATE_DRAFT, result.Intent); Assert.Equal("Consulta del puerto", result.Query); Assert.Equal(3, result.ToolSteps);
        var call = Assert.Single(semantic.Calls);
        Assert.Equal("Encargo íntegro 🛰️", call.Message); Assert.Equal("synthetic:model", call.Model); Assert.Equal(cancellation.Token, call.Token);
        Assert.Null(call.Context.Selected); Assert.Empty(call.Context.Candidates); Assert.Null(call.Context.InputScope);
        Assert.Empty(client.Proposals); Assert.Null(client.Suggestion); Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Fact] public async Task SemanticCancellationAndFailurePropagateWithoutFallingBackToTheModelClient()
    {
        var client = new Client(); var semantic = new Semantic(); var inference = new NovaInference(client, semantic);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inference.Interpret("Cancelada", "synthetic:model", cancelled.Token));
        var error = new ValidationException("Contrato del puerto rechazado."); semantic.Failure = error;
        Assert.Same(error, await Assert.ThrowsAsync<ValidationException>(() => inference.Interpret("Sin fallback", "synthetic:model", default)));
        Assert.Empty(client.Proposals); Assert.Null(client.Suggestion);
    }

    [Fact] public async Task AllProposalOverloadsAndSuggestionsKeepTheirArgumentsAndBorrowedResults()
    {
        var client = new Client(); var semantic = new Semantic(); var inference = new NovaInference(client, semantic);
        var source = new Template { Title = "Origen", Template = "Explica <tema>." }; using var cancellation = new CancellationTokenSource();
        Assert.Same(client.Proposal, await inference.Propose("improve", "Contexto íntegro", source, "synthetic:model", cancellation.Token));
        Assert.Same(client.Proposal, await inference.Propose("improve", "Contexto íntegro", source, "synthetic:model", cancellation.Token, true));
        Assert.Same(client.Proposal, await inference.Propose("improve", "Contexto íntegro", source, "synthetic:model", cancellation.Token, false, "Cambio de contenido"));
        string[] terms = ["Resumen", "Conclusión"];
        Assert.Same(client.Proposal, await inference.Propose("improve", "Contexto íntegro", source, "synthetic:model", cancellation.Token, false, "Cambio de contenido", terms));
        Assert.Equal(4, client.Proposals.Count);
        foreach (var call in client.Proposals) {
            Assert.Equal("improve", call.Action); Assert.Equal("Contexto íntegro", call.Context); Assert.Same(source, call.Source);
            Assert.Equal("synthetic:model", call.Model); Assert.Equal(cancellation.Token, call.Token);
        }
        Assert.Null(client.Proposals[0].AllowRevision); Assert.True(client.Proposals[1].AllowRevision); Assert.True(client.Proposals[1].Shortening);
        Assert.False(client.Proposals[2].Shortening); Assert.Equal("Cambio de contenido", client.Proposals[2].Interpretation!.RequestedContentChange);
        Assert.Same(terms, client.Proposals[3].Interpretation!.TargetTerms);
        Assert.Same(client.Values, await inference.Suggest(source, "Datos del caso", "synthetic:model", cancellation.Token));
        Assert.Same(source, client.Suggestion!.Value.Source); Assert.Equal("Datos del caso", client.Suggestion.Value.Context);
        Assert.Equal(cancellation.Token, client.Suggestion.Value.Token); Assert.Empty(semantic.Calls);
    }

    private sealed class Semantic : ISemanticIntentInterpreter
    {
        public List<(string Message, SemanticContext Context, string Model, CancellationToken Token)> Calls = [];
        public Exception? Failure;
        public Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); if (Failure is not null) throw Failure;
            Calls.Add((message, context, model, ct));
            return Task.FromResult(SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null,
                "Consulta del puerto", "Crea una instrucción reutilizable.", new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT,
                MissingInformation: [], RequiresConfirmation: false, ExecutionEvidence: "", ProviderId: null, Explanation: "Fixture controlado.")));
        }
    }
    private sealed record ProposalCall(string Action, string Context, Template? Source, string Model, CancellationToken Token,
        bool? AllowRevision = null, bool? Shortening = null, TemplateEditInterpretation? Interpretation = null);
    private sealed class Client : ILocalLlmClient
    {
        public readonly AiProposal Proposal = new(Guid.NewGuid(), null, 0, "Resultado del cliente", [], [], [], [], "synthetic:model");
        public readonly Dictionary<string, string> Values = new() { ["tema"] = "Datos literales" };
        public readonly List<ProposalCall> Proposals = [];
        public (Template Source, string Context, string Model, CancellationToken Token)? Suggestion;
        private Task<AiProposal> Capture(ProposalCall call) { Proposals.Add(call); return Task.FromResult(Proposal); }
        public Task<AiProposal> Propose(string a, string c, Template? s, string m, CancellationToken ct) => Capture(new(a, c, s, m, ct));
        public Task<AiProposal> Propose(string a, string c, Template? s, string m, CancellationToken ct, bool r) => Capture(new(a, c, s, m, ct, r));
        public Task<AiProposal> Propose(string a, string c, Template? s, string m, CancellationToken ct, bool r, bool? sh) => Capture(new(a, c, s, m, ct, r, sh));
        public Task<AiProposal> Propose(string a, string c, Template? s, string m, CancellationToken ct, bool r, bool? sh, TemplateEditInterpretation? i) => Capture(new(a, c, s, m, ct, r, sh, i));
        public Task<Dictionary<string, string>> Suggest(Template s, string c, string m, CancellationToken ct) { Suggestion = (s, c, m, ct); return Task.FromResult(Values); }
        public Task<string[]> Available(CancellationToken ct) => throw new InvalidOperationException("No model discovery expected.");
        public Task<AssistPlan> PlanAssist(string command, string model, CancellationToken ct) => throw new InvalidOperationException("No assist planning expected.");
        public Task<(string Text, double ElapsedMs)> Execute(CompiledPrompt prompt, string model, bool confirmed, Func<string, Task>? progress, CancellationToken ct) => throw new InvalidOperationException("No execution expected.");
    }
}
