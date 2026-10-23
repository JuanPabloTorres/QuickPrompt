using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Controlled composition and effect boundaries, not local-model interpretation accuracy.
public sealed class DraftGenerationCompositionTests
{
    [Fact]
    public async Task ExplicitUiCreationUsesCanonicalAuthoringAndPreservesLiteralBodyBeforeIndependentReview()
    {
        const string body = "Extrae las tareas de <notas>. No inventes datos.";
        var request = "Crea una instrucción con cuerpo completo literal «" + body + "».";
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request).Compile(request, SemanticContext.Empty);
        var reviewer = new Reviewer(); var extractor = new Extractor();
        var generator = new PromptDraftGenerator(new VerbatimTransport(), new Editor(), extractor, reviewer);
        var input = new PrepareInput(Guid.NewGuid(), request) { Interpretation = plan };
        var proposal = await generator.Generate(input, plan.Preparation(), [], default);
        Assert.Equal("Tarea", proposal.Title); Assert.Equal(body, proposal.TemplateText);
        Assert.NotNull(reviewer.Input!.VerbatimBody); Assert.Same(proposal, reviewer.Proposal);
        Assert.Equal(1, reviewer.Calls); Assert.Equal(0, extractor.Calls);
        Assert.Contains(SemanticOperation.REQUEST_SAVE_COPY, plan.Goal!.ApprovalRequirements);
        Assert.False(plan.Executes);
    }

    [Theory]
    [InlineData("Responde únicamente LISTO")]
    [InlineData("Primera línea.\nSegunda línea 🪴")]
    [InlineData("Explica <tema> sin inventar datos.")]
    public async Task ExplicitVerbatimBodyUsesOriginalCharactersBeforeReview(string literal)
    {
        var request = "Crea una instrucción con el cuerpo literal «" + literal + "». Déjala como borrador.";
        var transport = new VerbatimTransport(); var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(transport, new Editor(), new Extractor(), reviewer);
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", request,
            new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, "Crear borrador"));
        plan = plan with { Goal = TaskGoal.Create(request, SemanticContext.Empty, plan, [new(TaskClauseRole.WORK_ORDER, request)]) };
        var proposal = await generator.Generate(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default);
        Assert.Equal(literal, proposal.TemplateText);
        Assert.Equal(TemplateCompiler.Names(literal), proposal.RequiredInputs);
        Assert.Equal(literal, reviewer.Proposal!.TemplateText);
        Assert.NotNull(reviewer.Input!.VerbatimBody);
        Assert.DoesNotContain(literal, reviewer.Input.VerbatimBody.MetadataAuthority(request, literal));
        Assert.Equal(1, reviewer.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VerbatimBindingCannotCopyUnobservedQuotesOrForceOrdinaryGeneration(bool outsideRange)
    {
        const string request = "Crea un prompt sobre «un bosque». Déjalo en borrador.";
        var transport = new VerbatimTransport { OutsideRange = outsideRange, BindLiteral = outsideRange };
        var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(transport, new Editor(), new Extractor(), reviewer);
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", request,
            new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, "Crear"));
        plan = plan with { Goal = TaskGoal.Create(request, SemanticContext.Empty, plan, [new(TaskClauseRole.WORK_ORDER, request)]) };
        Task<PromptProposal> Act() => generator.Generate(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default);
        if (outsideRange) { await Assert.ThrowsAsync<ValidationException>(Act); Assert.Equal(0, reviewer.Calls); }
        else Assert.Equal("Texto generado.", (await Act()).TemplateText);
    }

    [Fact]
    public async Task MultilineTitleIsRejectedBeforeReviewOrUiWhileBodyMayBeMultiline()
    {
        const string request = "Crea un prompt con cuerpo literal «Línea uno.\nLínea dos 🪴».";
        var transport = new VerbatimTransport { Title = "Título\nInválido" }; var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(transport, new Editor(), new Extractor(), reviewer);
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", request,
            new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, "Crear"));
        plan = plan with { Goal = TaskGoal.Create(request, SemanticContext.Empty, plan, [new(TaskClauseRole.WORK_ORDER, request)]) };
        await Assert.ThrowsAsync<ValidationException>(() => generator.Generate(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default));
        Assert.Equal(0, reviewer.Calls);
    }

    private sealed class VerbatimTransport : IValidatedAgentModelTransport
    {
        public bool BindLiteral = true; public bool OutsideRange; public string Title = "Tarea";
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested();
            using var data = JsonDocument.Parse(JsonSerializer.SerializeToElement(packet).GetProperty("messages")[1].GetProperty("content").GetString()!);
            Assert.True(data.RootElement.TryGetProperty("originalAuthoringRequest",out _));
            Assert.False(data.RootElement.TryGetProperty("requestUnits",out _));
            var reply=new {title=Title,description="",templateText="Texto generado.",verbatimBodyReference=BindLiteral?(int?)(OutsideRange?99:0):null};
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new {message=new {content=JsonSerializer.Serialize(reply)}})));
        }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? error) { }
        public void Reject(string? answer) { }
    }

    [Fact]
    public async Task CurrentProfileCreationHasOneContentAuthorAndNoSelfAssignedScopeOrIdentity()
    {
        var transport = new Transport(); var extractor = new Extractor(); var reviewer = new Reviewer();
        var initial = Input(); var input = initial with { Interpretation = initial.Interpretation! with { Authoring = null } };
        var generator = new PromptDraftGenerator(transport, new Editor(), extractor, reviewer);
        var proposal = await generator.Generate(input, input.Interpretation!.Preparation(), [], default);
        var packet = Assert.Single(transport.Calls).Packet;
        Assert.Equal(new[] { "title", "description", "templateText" }, packet.GetProperty("format").GetProperty("properties").EnumerateObject().Select(property => property.Name));
        using var data = JsonDocument.Parse(packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(input.TaskRequest, data.RootElement.GetProperty("originalAuthoringRequest").GetString());
        Assert.False(data.RootElement.TryGetProperty("requestUnits", out _));
        Assert.Equal(PreparationDecision.CREATE_NEW, proposal.Decision); Assert.Null(proposal.SourceId);
        Assert.Equal(new[] { "tema" }, proposal.RequiredInputs); Assert.Equal(1, extractor.Calls); Assert.Equal(1, reviewer.Calls);
    }
    private static PrepareInput Input(string? message = null)
    {
        message ??= "Crea una instrucción para explicar <tema>. Tema: compostaje.";
        var plan = SemanticFixture.Plan([SemanticOperation.CREATE_DRAFT, SemanticOperation.VALIDATE_DRAFT],
            TargetReference.NONE, outcome: SemanticOutcome.NEW_DRAFT) with {
                Authoring = new(InstructionAuthorship.CREATE_TEMPLATE, message, message, LiveRuns.Hash(message))
            };
        return new(Guid.NewGuid(), message) { Interpretation = plan };
    }

    [Fact] public async Task ReusableDefinitionWithoutCaseDataReachesReviewWithUnfilledFutureInputs()
    {
        var transport = new Transport(); var extractor = new Extractor(); var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(transport, new Editor(), extractor, reviewer);
        var input = Input("Crea una instrucción reutilizable para explicar un tema sin inventar datos.");
        var proposal = await generator.Generate(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal("Explica <tema>.", proposal.TemplateText);
        Assert.Equal(new[] { "tema" }, proposal.RequiredInputs); Assert.Empty(proposal.Values);
        Assert.Equal(0, extractor.Calls); Assert.Equal(1, reviewer.Calls);
        Assert.Same(proposal, reviewer.Proposal);
    }

    [Fact] public async Task ExplicitFillContractRetainsExtractionForUnlabelledMaterial()
    {
        var transport = new Transport(); var extractor = new Extractor(); var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(transport, new Editor(), extractor, reviewer);
        var input = Input("Crea una instrucción reutilizable y usa compostaje como tema.");
        input = input with { Interpretation = input.Interpretation! with {
            Intentions = [..input.Interpretation!.Intentions, SemanticOperation.FILL_VARIABLES] } };
        var proposal = await generator.Generate(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal("compostaje", Assert.Single(proposal.Values).Value);
        Assert.Equal(1, extractor.Calls); Assert.Equal(1, reviewer.Calls);
    }

    [Fact] public async Task ExplicitUiValuesStillUseTheExtractionBoundaryWithoutAnInferredFill()
    {
        var extractor = new Extractor(); var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(new Transport(), new Editor(), extractor, reviewer);
        var input = Input("Crea una instrucción reutilizable para explicar un tema.") with {
            Values = new() { ["tema"] = "compostaje" } };
        await generator.Generate(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(1, extractor.Calls); Assert.Equal(1, reviewer.Calls);
    }

    [Theory] [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task ActualFactoryGeneratesThroughIndependentPortsWithInterpreterForbidden(bool knowledge, bool thinking)
    {
        var services = new ServiceCollection(); var transport = new Transport(); var editor = new Editor();
        var extractor = new Extractor(); var reviewer = new Reviewer();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["QuickPrompt:Ollama"] = "http://127.0.0.1:1",
            ["QuickPrompt:Preparation:AuthoringUseContract"] = knowledge.ToString(),
            ["QuickPrompt:Preparation:SemanticThinking"] = thinking.ToString()
        }).Build();
        var path = Path.Combine(Path.GetTempPath(), "nova-generation-composition-" + Guid.NewGuid(), "synthetic.sqlite");
        Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!
            .GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [services, configuration, path]);
        services.AddSingleton<IValidatedAgentModelTransport>(transport);
        services.AddSingleton<ILocalLlmClient>(editor); services.AddSingleton<IInputValueExtractor>(extractor);
        services.AddSingleton<IDraftFidelityReviewer>(reviewer);
        services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("Interpreter construction forbidden."));
        using (var provider = services.BuildServiceProvider()) {
            var generator = Assert.IsType<PromptDraftGenerator>(provider.GetRequiredService<IPromptGenerator>());
            var input = Input(); using var cancellation = new CancellationTokenSource(); var diagnostics = new List<int>();
            var proposal = await ((IPromptGenerator)generator).Generate(input, input.Interpretation!.Preparation(), [], cancellation.Token,
                (stage, _, _) => diagnostics.Add(stage));
            Assert.Equal("Explica <tema>.", proposal.TemplateText); Assert.Equal("Explicación", proposal.Title);
            Assert.Equal(new[] { "tema" }, proposal.RequiredInputs); Assert.Equal("compostaje", Assert.Single(proposal.Values).Value);
            Assert.Null(proposal.SourceId); Assert.Equal(PreparationDecision.CREATE_NEW, proposal.Decision);
            Assert.Equal(new[] { 189, 190 }, diagnostics); Assert.Equal(1, extractor.Calls); Assert.Equal(1, reviewer.Calls);
            Assert.Same(input, extractor.Input); Assert.Same(input, reviewer.Input); Assert.Same(proposal, reviewer.Proposal);
            Assert.Equal(cancellation.Token, reviewer.Token); Assert.Equal(0, editor.Calls);
            var call = Assert.Single(transport.Calls); Assert.Equal("GenerateAuthoredDefinition", call.Purpose);
            Assert.Equal(cancellation.Token, call.Token); Assert.Equal(thinking, call.Packet.GetProperty("think").GetBoolean());
            Assert.Equal(LocalModelPolicy.AgentContextLength, call.Packet.GetProperty("options").GetProperty("num_ctx").GetInt32());
            var system = call.Packet.GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.Equal(knowledge, system.Contains("PromptDraftGenerator.GenerateAuthoredDefinition", StringComparison.Ordinal));
            using var data = JsonDocument.Parse(call.Packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
            Assert.Equal(input.Message, data.RootElement.GetProperty("originalAuthoringRequest").GetString());
            Assert.Equal(0, data.RootElement.GetProperty("applicationEffects").GetArrayLength());
            Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
            Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(PromptDraftGenerator)));
        }
        Assert.False(editor.Disposed); Assert.False(reviewer.Disposed); Assert.False(extractor.Disposed); Assert.False(transport.Disposed);
    }

    [Theory] [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public async Task AdaptationUsesBoundIdentityAndRevisionAndRejectsAnotherIdentityBeforeReview(bool wrongIdentity, bool wrongRevision)
    {
        var source = new Template { Id = Guid.NewGuid(), Revision = 7, Title = "Guía", Template = "Explica <tema>." };
        var document = PromptLibrary.Document(source); const string request = "Adapta la explicación para principiantes.";
        var plan = SemanticFixture.Plan([SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.VALIDATE_DRAFT],
            TargetReference.SELECTED, source.Id, outcome: SemanticOutcome.DRAFT) with { RequestedTransformation = request };
        var input = new PrepareInput(Guid.NewGuid(), request, SourcePromptId: source.Id, SourceRevision: source.Revision) { Interpretation = plan };
        var editor = new Editor { WrongIdentity = wrongIdentity, WrongRevision = wrongRevision }; var reviewer = new Reviewer();
        var transport = new Transport(); var extractor = new Extractor();
        var generator = new PromptDraftGenerator(transport, editor, extractor, reviewer);
        var pending = generator.Generate(input, plan.Preparation(), [new(document, 1, "Fuente controlada")], default);
        if (wrongIdentity || wrongRevision) { await Assert.ThrowsAsync<ValidationException>(() => pending); Assert.Equal(0, reviewer.Calls); }
        else {
            var proposal = await pending; Assert.Equal(source.Id, proposal.SourceId); Assert.Equal(source.Title, proposal.Title);
            Assert.Equal("Explica <tema> con ejemplos.", proposal.TemplateText); Assert.Equal(1, reviewer.Calls);
        }
        Assert.Equal(1, editor.Calls); Assert.Equal(request, editor.Request); Assert.Equal(source.Id, editor.Source!.Id);
        Assert.Equal(source.Revision, editor.Source.Revision); Assert.Equal(0, extractor.Calls); Assert.Empty(transport.Calls);
        Assert.Equal("Explica <tema>.", source.Template); Assert.Equal(7, source.Revision);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CancellationOrModelFailureCannotReachExtractionOrReview(bool unavailable)
    {
        var transport = new Transport { Unavailable = unavailable }; var extractor = new Extractor(); var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(transport, new Editor(), extractor, reviewer);
        using var cancellation = new CancellationTokenSource(); if (!unavailable) cancellation.Cancel();
        var input = Input(); var pending = generator.Generate(input, input.Interpretation!.Preparation(), [], cancellation.Token);
        if (unavailable) await Assert.ThrowsAsync<LlmUnavailableException>(() => pending);
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(unavailable ? 1 : 0, transport.Calls.Count); Assert.Equal(0, extractor.Calls); Assert.Equal(0, reviewer.Calls);
    }

    [Fact] public async Task ReviewRejectionRetainsTheActualRejectedArtifactAndNeverRetriesGeneration()
    {
        var transport = new Transport(); var reviewer = new Reviewer { Reject = true }; var input = Input();
        var generator = new PromptDraftGenerator(transport, new Editor(), new Extractor(), reviewer);
        var error = await Assert.ThrowsAsync<ValidationException>(() => generator.Generate(input, input.Interpretation!.Preparation(), [], default));
        var rejected = JsonSerializer.SerializeToElement(error.Data["NovaRejectedProposal"]);
        Assert.Equal("Explica <tema>.", rejected.GetProperty("TemplateText").GetString());
        Assert.Single(transport.Calls); Assert.Equal(1, reviewer.Calls);
    }

    [Fact] public async Task LegacyPortImplementationRemainsSubstitutableThroughTheCompatibilityBridge()
    {
        var legacy = new LegacyGenerator(); var input = Input(); var transport = new Transport();
        using (var inference = new PreparationInference("http://127.0.0.1:1", modelTransport: transport, promptGenerator: legacy)) {
            inference.SemanticDiagnostic = (_, _, _) => throw new InvalidOperationException("Legacy provider supplies no diagnostic capability.");
            Assert.Same(legacy.Proposal, await inference.Generate(input, input.Interpretation!.Preparation(), [], default));
        }
        Assert.Equal(1, legacy.Calls); Assert.False(legacy.Disposed); Assert.Empty(transport.Calls); Assert.False(transport.Disposed);
    }

    [Fact] public async Task ConcurrentRequestsKeepTheirDiagnosticSubscribersAndReviewInputsSeparate()
    {
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var count = 0;
        var reviewer = new Reviewer { Wait = async () => { if (Interlocked.Increment(ref count) == 2) arrived.SetResult(); await arrived.Task; } };
        var generator = new PromptDraftGenerator(new Transport(), new Editor(), new Extractor(), reviewer);
        var first = Input("Crea una explicación. Dato A."); var second = Input("Crea otra explicación. Dato B.");
        var firstSeen = new List<string>(); var secondSeen = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(generator.Generate(first, first.Interpretation!.Preparation(), [], timeout.Token,
                (stage, content, _) => { if (stage == 190) firstSeen.Add(content); }),
            generator.Generate(second, second.Interpretation!.Preparation(), [], timeout.Token,
                (stage, content, _) => { if (stage == 190) secondSeen.Add(content); })).WaitAsync(timeout.Token);
        Assert.Equal(new[] { first.Message }, firstSeen); Assert.Equal(new[] { second.Message }, secondSeen);
    }

    private sealed class LegacyGenerator : IPromptGenerator, IDisposable
    {
        public readonly PromptProposal Proposal = new(PreparationDecision.CREATE_NEW, null, "Prueba", "", "Explica el asunto.", [], "", "");
        public int Calls; public bool Disposed;
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct)
        { Calls++; return Task.FromResult(Proposal); }
        public void Dispose() => Disposed = true;
    }
    private sealed class Extractor : IInputValueExtractor, IDisposable
    {
        public int Calls; public PrepareInput? Input; public bool Disposed;
        public Task<InputValueExtraction> ExtractInputs(PrepareInput input, string template, string[] names, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Calls++; Input = input; Assert.Equal(new[] { "tema" }, names); return Task.FromResult(new InputValueExtraction([new("tema", "compostaje", "compostaje")], "")); }
        public void Dispose() => Disposed = true;
    }
    private sealed class Reviewer : IDraftFidelityReviewer, IDisposable
    {
        public int Calls; public bool Disposed; public bool Reject; public Func<Task>? Wait;
        public PrepareInput? Input; public PromptProposal? Proposal; public CancellationToken Token;
        public async Task ReviewDraftFidelity(PrepareInput input, PromptProposal proposal, RetrievalCandidate[] candidates, CancellationToken ct,
            string? boundTitleLiteral = null, Action<int, string, string?>? diagnostic = null)
        {
            ct.ThrowIfCancellationRequested(); Calls++; Input = input; Proposal = proposal; Token = ct;
            if (Wait is not null) await Wait();
            diagnostic?.Invoke(190, input.Message, null);
            if (Reject) throw new ValidationException("Synthetic fidelity rejection");
        }
        public void Dispose() => Disposed = true;
    }
    private sealed class Transport : IValidatedAgentModelTransport, IDisposable
    {
        public List<(JsonElement Packet, string Purpose, CancellationToken Token)> Calls = [];
        public bool Unavailable; public bool Disposed;
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add((JsonSerializer.SerializeToElement(packet), purpose, ct));
            if (Unavailable) throw new LlmUnavailableException("Synthetic unavailable");
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new {
                content = """{"title":"Explicación","description":"","templateText":"Explica <tema>."}""" } })));
        }
        public bool UsesThinking(string model) => throw new InvalidOperationException("Profile is owned by generator.");
        public void Observe(int stage, string content, string? error) => throw new InvalidOperationException("Diagnostics are per-call.");
        public void Reject(string? answer) => throw new InvalidOperationException("No repair in composition fixtures.");
        public void Dispose() => Disposed = true;
    }
    private sealed class Editor : ILocalLlmClient, IDisposable
    {
        public int Calls; public bool WrongIdentity; public bool WrongRevision; public bool Disposed; public string? Request; public Template? Source;
        public Task<AiProposal> Propose(string action, string context, Template? template, string model, CancellationToken ct,
            bool allowRevision, bool? requestedShortening, TemplateEditInterpretation? interpretation)
        {
            ct.ThrowIfCancellationRequested(); Calls++; Request = context; Source = template;
            Assert.False(allowRevision); Assert.Equal("improve", action);
            return Task.FromResult(new AiProposal(Guid.NewGuid(), WrongIdentity ? Guid.NewGuid() : template!.Id,
                WrongRevision ? template!.Revision + 1 : template!.Revision, "Explica <tema> con ejemplos.", [], [], [], [], model));
        }
        public Task<AiProposal> Propose(string action, string context, Template? template, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<string[]> Available(CancellationToken ct) => throw new NotSupportedException();
        public Task<(string Text, double ElapsedMs)> Execute(CompiledPrompt prompt, string model, bool confirmed, Func<string, Task>? progress, CancellationToken ct) => throw new NotSupportedException();
        public Task<AssistPlan> PlanAssist(string command, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<Dictionary<string, string>> Suggest(Template template, string context, string model, CancellationToken ct) => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
    }
}
