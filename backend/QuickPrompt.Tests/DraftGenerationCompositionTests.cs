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
    [Theory]
    [InlineData("CONTENT_SCOPE_NEEDS_CONFIRMATION", true)]
    [InlineData("CONTENT_COVERAGE_NEEDS_CONFIRMATION", true)]
    [InlineData("CONTENT_FIDELITY_REVIEW_INVALID", false)]
    [InlineData("CONTENT_FIDELITY_MISMATCH", false)]
    public async Task RetiredReviewerVerdictsCannotControlOrAcceptTheProductionProposal(string code, bool clarify)
    {
        const string request = "Crea una instrucción para organizar archivos. Déjala como borrador.";
        const string question = "¿Qué tarea debe realizar el futuro asistente?";
        var failure = new ValidationException(question);
        failure.Data["NovaValidationCode"] = code;
        var reviewer = new Reviewer { Failure = failure };
        var transport = new VerbatimTransport();
        var generator = new PromptDraftGenerator(transport, new Editor(), new Extractor(), reviewer);
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request) { InputNames = [] }.Compile(request, SemanticContext.Empty);
        var input = new PrepareInput(Guid.NewGuid(), request) { Interpretation = plan };
        var pending = generator.GenerateAfterSyntheticReview(input, plan.Preparation(), [], default);
        var proposal=await pending;
        Assert.Equal("Texto generado. No inventes datos.",proposal.TemplateText);
        Assert.Equal(0,reviewer.Calls);Assert.Equal(1,transport.Calls);
        Assert.False(plan.Goal!.HasContentAssessment(proposal.TemplateText));Assert.False(plan.Goal.HasHumanContentApproval(proposal.TemplateText));
        Assert.Equal("REQUIRES_HUMAN_CONTENT_REVIEW",plan.Goal.AuthoringContract!.GetContentReview(proposal.TemplateText).Status);
        Assert.Equal(question,failure.Message);Assert.Equal(code,failure.Data["NovaValidationCode"]);
        Assert.Equal(clarify,code is "CONTENT_SCOPE_NEEDS_CONFIRMATION" or "CONTENT_COVERAGE_NEEDS_CONFIRMATION");
    }

    [Fact]
    public async Task ExplicitUiCreationUsesCanonicalAuthoringAndPreservesLiteralBodyBeforeIndependentReview()
    {
        const string body = "Extrae las tareas de <notas>. No inventes datos.";
        var request = "Crea una instrucción con cuerpo completo literal «" + body + "».";
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request){InputNames=["notas"]}.Compile(request, SemanticContext.Empty);
        var reviewer = new Reviewer(); var extractor = new Extractor();
        var generator = new PromptDraftGenerator(new VerbatimTransport(), new Editor(), extractor, reviewer);
        var input = new PrepareInput(Guid.NewGuid(), request) { Interpretation = plan };
        var proposal = await generator.GenerateAfterSyntheticReview(input, plan.Preparation(), [], default);
        Assert.Equal("Tarea", proposal.Title); Assert.Equal(body, proposal.TemplateText);
        Assert.NotEmpty(plan.Goal!.AuthoringContract!.Literals);Assert.Null(reviewer.Proposal);
        Assert.Equal(0, reviewer.Calls); Assert.Equal(0, extractor.Calls);
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
        plan.Goal!.SetInputContract(PromptInputContract.FromUserSelection(request,"",false,TemplateCompiler.Names(request)));
        var proposal = await generator.GenerateAfterSyntheticReview(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default);
        Assert.Equal(literal, proposal.TemplateText);
        Assert.Equal(TemplateCompiler.Names(literal), proposal.RequiredInputs);
        Assert.Null(reviewer.Proposal);
        Assert.NotEmpty(plan.Goal!.AuthoringContract!.Literals);
        Assert.Contains(literal,plan.Goal.AuthoringContract.OriginalRequest);
        Assert.Equal(0, reviewer.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VerbatimBindingCannotCopyUnobservedQuotesOrForceOrdinaryGeneration(bool outsideRange)
    {
        const string request = "Crea un prompt sobre «un bosque». Déjalo en borrador.";
        var transport = new VerbatimTransport { OutsideLiteralRange=outsideRange, SelectLiteral=false };
        var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(transport, new Editor(), new Extractor(), reviewer);
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", request,
            new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, "Crear"));
        plan = plan with { Goal = TaskGoal.Create(request, SemanticContext.Empty, plan, [new(TaskClauseRole.WORK_ORDER, request)]) };
        plan.Goal!.SetInputContract(PromptInputContract.FromUserSelection(request,"",false,TemplateCompiler.Names(request)));
        Task<PromptProposal> Act() => generator.GenerateAfterSyntheticReview(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default);
        if (outsideRange) { await Assert.ThrowsAsync<ValidationException>(Act); Assert.Equal(0, reviewer.Calls); }
        else Assert.Equal("Texto generado. No inventes datos.", (await Act()).TemplateText);
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
        plan.Goal!.SetInputContract(PromptInputContract.FromUserSelection(request,"",false,TemplateCompiler.Names(request)));
        await Assert.ThrowsAsync<ValidationException>(() => generator.GenerateAfterSyntheticReview(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default));
        Assert.Equal(0, reviewer.Calls);
    }

    [Fact]
    public async Task SessionControlsNeverCompileIntoFutureBodyButRemainVisibleToIndependentReview()
    {
        const string request = "Crea un prompt para organizar archivos. Revisa conmigo antes de guardarlo.";
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request){InputNames=[]}.Compile(request, SemanticContext.Empty);
        var transport = new VerbatimTransport(); var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(transport, new Editor(), new Extractor(), reviewer);
        var proposal = await generator.GenerateAfterSyntheticReview(new(Guid.NewGuid(), request) { Interpretation = plan }, plan.Preparation(), [], default);
        Assert.DoesNotContain("Revisa conmigo antes de guardarlo.", proposal.TemplateText);
        Assert.Contains("No inventes datos.", proposal.TemplateText);
        Assert.Equal(request, transport.AuthorRequest);
        Assert.Null(reviewer.Input);
        Assert.Equal(0, reviewer.Calls);
    }
    private sealed class VerbatimTransport : IValidatedAgentModelTransport
    {
        public string Title="Tarea";public string? AuthorRequest;public int Calls;public bool OutsideLiteralRange;public bool SelectLiteral=true;
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            using var data = JsonDocument.Parse(JsonSerializer.SerializeToElement(packet).GetProperty("messages")[1].GetProperty("content").GetString()!);
            Assert.True(data.RootElement.TryGetProperty("originalRequest",out _));
            AuthorRequest = data.RootElement.GetProperty("originalRequest").GetString();
            Assert.False(data.RootElement.TryGetProperty("requestUnits",out _));
            var fields = AuthoringReviewTestFixture.SchemaFields(JsonSerializer.SerializeToElement(packet).GetProperty("format"));
            Assert.False(fields.TryGetProperty("taskInstruction", out _));
            Assert.False(fields.TryGetProperty("requirements", out _));
            int? reference=null;
            if(OutsideLiteralRange)reference=99;
            else if(SelectLiteral&&data.RootElement.GetProperty("literalBodyCandidates").GetArrayLength()>0)
                reference=data.RootElement.GetProperty("literalBodyCandidates")[0].GetProperty("id").GetInt32();
            object reply=AuthoringReviewTestFixture.Definition("Texto generado. No inventes datos.",Title,literalReference:reference);
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new {message=new {content=JsonSerializer.Serialize(reply,AuthoringReviewTestFixture.Json)}})));
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
        var proposal = await generator.GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        var packet = Assert.Single(transport.Calls).Packet;
        Assert.Equal(new[] { "title", "description", "outcome", "instruction", "question", "literalReference" }, AuthoringReviewTestFixture.SchemaFields(packet.GetProperty("format")).EnumerateObject().Select(property => property.Name));
        using var data = JsonDocument.Parse(packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(input.TaskRequest, data.RootElement.GetProperty("originalRequest").GetString());
        Assert.False(data.RootElement.TryGetProperty("requestUnits", out _));
        Assert.Equal(PreparationDecision.CREATE_NEW, proposal.Decision); Assert.Null(proposal.SourceId);
        Assert.Equal(new[] { "tema" }, proposal.RequiredInputs); Assert.Equal(1, extractor.Calls); Assert.Equal(0, reviewer.Calls);
    }
    private static PrepareInput Input(string? message = null)
    {
        message ??= "Crea una instrucción para explicar <tema>.";
        var plan = SemanticFixture.Plan([SemanticOperation.CREATE_DRAFT, SemanticOperation.VALIDATE_DRAFT],
            TargetReference.NONE, outcome: SemanticOutcome.NEW_DRAFT) with {
                Authoring = new(InstructionAuthorship.CREATE_TEMPLATE, message, message, LiveRuns.Hash(message))
            };
        plan=plan with {Goal=TaskGoal.Create(message,SemanticContext.Empty,plan,[new(TaskClauseRole.WORK_ORDER,message)])};
        plan.Goal!.SetInputContract(PromptInputContract.FromUserSelection(message,"",false,["tema"]));
        return new(Guid.NewGuid(),message){Interpretation=plan,Values=new(){["tema"]="compostaje"}};
    }

    [Fact] public async Task ReusableDefinitionWithoutCaseDataReachesReviewWithUnfilledFutureInputs()
    {
        var transport = new Transport(); var extractor = new Extractor(); var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(transport, new Editor(), extractor, reviewer);
        var input=Input("Crea una instrucción reutilizable para explicar un tema sin inventar datos.") with {Values=null};
        var proposal = await generator.GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal("Explica <tema>.", proposal.TemplateText);
        Assert.Equal(new[] { "tema" }, proposal.RequiredInputs); Assert.Empty(proposal.Values);
        Assert.Equal(0, extractor.Calls); Assert.Equal(0, reviewer.Calls);
        Assert.Null(reviewer.Proposal);
    }

    [Fact] public async Task ExplicitFillContractRetainsExtractionForUnlabelledMaterial()
    {
        var transport = new Transport(); var extractor = new Extractor(); var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(transport, new Editor(), extractor, reviewer);
        var input=Input("Crea una instrucción reutilizable y usa compostaje como tema.") with {Values=null};
        input = input with { Interpretation = input.Interpretation! with {
            Intentions = [..input.Interpretation!.Intentions, SemanticOperation.FILL_VARIABLES] } };
        var proposal = await generator.GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal("compostaje", Assert.Single(proposal.Values).Value);
        Assert.Equal(1, extractor.Calls); Assert.Equal(0, reviewer.Calls);
    }

    [Fact] public async Task ExplicitUiValuesStillUseTheExtractionBoundaryWithoutAnInferredFill()
    {
        var extractor = new Extractor(); var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(new Transport(), new Editor(), extractor, reviewer);
        var input = Input("Crea una instrucción reutilizable para explicar un tema.") with {
            Values = new() { ["tema"] = "compostaje" } };
        await generator.GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], default);
        Assert.Equal(1, extractor.Calls); Assert.Equal(0, reviewer.Calls);
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
            var proposal = await generator.GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], cancellation.Token,
                (stage, _, _) => diagnostics.Add(stage));
            Assert.Equal("Explica <tema>.", proposal.TemplateText); Assert.Equal("Explicación", proposal.Title);
            Assert.Equal(new[] { "tema" }, proposal.RequiredInputs); Assert.Equal("compostaje", Assert.Single(proposal.Values).Value);
            Assert.Null(proposal.SourceId); Assert.Equal(PreparationDecision.CREATE_NEW, proposal.Decision);
            Assert.Equal(new[] { 193 }, diagnostics); Assert.Equal(1, extractor.Calls); Assert.Equal(0, reviewer.Calls);
            Assert.Equal(input.OperationId,extractor.Input!.OperationId); Assert.Equal(input.TaskRequest,extractor.Input.TaskRequest);
            Assert.NotNull(extractor.Input.ConfirmedBrief); Assert.Null(reviewer.Input);Assert.Null(reviewer.Proposal);
            Assert.Equal(default,reviewer.Token); Assert.Equal(0, editor.Calls);
            var call = Assert.Single(transport.Calls); Assert.Equal("GenerateDraftProposal", call.Purpose);
            Assert.Equal(cancellation.Token, call.Token); Assert.False(call.Packet.GetProperty("think").GetBoolean());
            Assert.Equal(thinking,provider.GetRequiredService<IAgentModelTransport>().UsesThinking(input.Model));
            Assert.Equal(LocalModelPolicy.AgentContextLength, call.Packet.GetProperty("options").GetProperty("num_ctx").GetInt32());
            var system = call.Packet.GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.DoesNotContain("PromptDraftGenerator.GenerateDraftProposal", system, StringComparison.Ordinal);
            using var data = JsonDocument.Parse(call.Packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
            Assert.Equal(input.Message, data.RootElement.GetProperty("originalRequest").GetString());
            Assert.False(data.RootElement.TryGetProperty("applicationEffects",out _));
            Assert.False(call.Packet.TryGetProperty("tools",out _));
            Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
            Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(PromptDraftGenerator)));
        }
        Assert.False(editor.Disposed); Assert.False(reviewer.Disposed); Assert.False(extractor.Disposed); Assert.False(transport.Disposed);
    }

    [Theory] [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)]
    public async Task AdaptationUsesBoundIdentityAndRevisionAndRejectsAnotherIdentityBeforeReview(bool wrongIdentity,bool wrongRevision)
    {
        var source=new Template {Id=Guid.NewGuid(),Revision=7,Title="Guía",Template="Explica <tema>."};
        var document=PromptLibrary.Document(source);const string request="Adapta la explicación para principiantes.";
        var context=SemanticContext.Empty with {Selected=SemanticEntity.From(document)};
        var plan=new LiveCommand(SemanticOperation.PROPOSE_ADAPTATION,request){InputNames=[]}.Compile(request,context);
        var input=new PrepareInput(Guid.NewGuid(),request,SourcePromptId:wrongIdentity?Guid.NewGuid():source.Id,SourceRevision:wrongRevision?8:source.Revision){Interpretation=plan};
        var editor=new Editor();var reviewer=new Reviewer();var transport=new Transport {Body="Explica <tema> con ejemplos."};var extractor=new Extractor();
        var generator=new PromptDraftGenerator(transport,editor,extractor,reviewer);
        Task<PromptProposal> Act()=>generator.GenerateAfterSyntheticReview(input,plan.Preparation(),[new(document,1,"Fuente controlada")],default);
        if(wrongIdentity||wrongRevision) {
            await Assert.ThrowsAsync<ValidationException>(Act);Assert.Equal(0,reviewer.Calls);Assert.Empty(transport.Calls);
        } else {
            var proposal=await Act();Assert.Equal(source.Id,proposal.SourceId);Assert.Equal(source.Title,proposal.Title);
            Assert.Equal("Explica <tema> con ejemplos.",proposal.TemplateText);Assert.Equal(0,reviewer.Calls);Assert.Single(transport.Calls);
            Assert.Equal(source.Id,plan.Goal!.AuthoringContract!.SourceId);Assert.Equal(source.Revision,plan.Goal.AuthoringContract.SourceRevision);
            Assert.False(plan.Goal.HasContentAssessment(proposal.TemplateText));Assert.False(plan.Goal.HasHumanContentApproval(proposal.TemplateText));
        }
        Assert.Equal(0,editor.Calls);Assert.Equal(0,extractor.Calls);Assert.Equal("Explica <tema>.",source.Template);Assert.Equal(7,source.Revision);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CancellationOrModelFailureCannotReachExtractionOrReview(bool unavailable)
    {
        var transport = new Transport { Unavailable = unavailable }; var extractor = new Extractor(); var reviewer = new Reviewer();
        var generator = new PromptDraftGenerator(transport, new Editor(), extractor, reviewer);
        using var cancellation = new CancellationTokenSource(); if (!unavailable) cancellation.Cancel();
        var input = Input(); var pending = generator.GenerateAfterSyntheticReview(input, input.Interpretation!.Preparation(), [], cancellation.Token);
        if(unavailable){var failure=await Assert.ThrowsAsync<PreparationToolException>(()=>pending);Assert.Equal("MODEL_UNAVAILABLE",failure.Code);}
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(unavailable ? 1 : 0, transport.Calls.Count); Assert.Equal(0, extractor.Calls); Assert.Equal(0, reviewer.Calls);
    }

    [Fact] public async Task FieldRejectionRetainsTheActualRejectedArtifactAndNeverRetriesGeneration()
    {
        var transport=new Transport{Body="Explica <intruso>."};var reviewer=new Reviewer{Reject=true};var input=Input();
        var generator=new PromptDraftGenerator(transport,new Editor(),new Extractor(),reviewer);
        var suggestion=await generator.Generate(input,input.Interpretation!.Preparation(),[],default);
        Assert.Equal("Explica <intruso>.",suggestion.Brief!.Proposed.Task); Assert.Empty(suggestion.TemplateText);
        Assert.Throws<ValidationException>(()=>AuthoringBriefs.Confirm(suggestion.Brief,
            new(suggestion.Brief.BasisHash,suggestion.Brief.Proposed,true)));
        Assert.Single(transport.Calls);Assert.Equal(0,reviewer.Calls); Assert.Null(input.Interpretation.Goal!.AuthoringContract);
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
        var reviewer=new Reviewer();var generator=new PromptDraftGenerator(new Transport(),new Editor(),new Extractor(),reviewer);
        var first=Input("Crea una explicación. Dato A.");var second=Input("Crea otra explicación. Dato B.");
        var firstSeen=new List<string>();var secondSeen=new List<string>();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(generator.GenerateAfterSyntheticReview(first,first.Interpretation!.Preparation(),[],timeout.Token,(stage,content,_)=>{if(stage==193)firstSeen.Add(content);}),
            generator.GenerateAfterSyntheticReview(second,second.Interpretation!.Preparation(),[],timeout.Token,(stage,content,_)=>{if(stage==193)secondSeen.Add(content);}));
        using var one=JsonDocument.Parse(Assert.Single(firstSeen));using var two=JsonDocument.Parse(Assert.Single(secondSeen));
        Assert.Equal(first.Interpretation!.Goal!.AuthoringContract!.Fingerprint,one.RootElement.GetProperty("contractHash").GetString());
        Assert.Equal(second.Interpretation!.Goal!.AuthoringContract!.Fingerprint,two.RootElement.GetProperty("contractHash").GetString());
        Assert.NotEqual(first.Interpretation.Goal.AuthoringContract.Fingerprint,second.Interpretation.Goal.AuthoringContract.Fingerprint);Assert.Equal(0,reviewer.Calls);
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
        public int Calls; public bool Disposed; public bool Reject; public ValidationException? Failure;
        public PrepareInput? Input; public PromptProposal? Proposal; public CancellationToken Token;
        public async Task ReviewDraftFidelity(PrepareInput input, PromptProposal proposal, RetrievalCandidate[] candidates, CancellationToken ct,
            string? boundTitleLiteral = null, Action<int, string, string?>? diagnostic = null)
        {
            ct.ThrowIfCancellationRequested(); Calls++; Input = input; Proposal = proposal; Token = ct;
            await Task.CompletedTask;
            diagnostic?.Invoke(190, input.Message, null);
            if (Failure is not null) throw Failure;
            if (Reject) throw new ValidationException("Synthetic fidelity rejection");
        }
        public void Dispose() => Disposed = true;
    }
    private sealed class Transport : IValidatedAgentModelTransport, IDisposable
    {
        public List<(JsonElement Packet, string Purpose, CancellationToken Token)> Calls = [];
        public bool Unavailable; public bool Disposed; public string Body="Explica <tema>.";
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add((JsonSerializer.SerializeToElement(packet), purpose, ct));
            if (Unavailable) throw new LlmUnavailableException("Synthetic unavailable");
            var material=AuthoringReviewTestFixture.Material(Calls[^1].Packet);
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new {
                content=JsonSerializer.Serialize(AuthoringReviewTestFixture.Definition(Body,"Explicación"),AuthoringReviewTestFixture.Json)} })));

        }
        public bool UsesThinking(string model) => throw new InvalidOperationException("Profile is owned by generator.");
        public void Observe(int stage, string content, string? error) => throw new InvalidOperationException("Diagnostics are per-call.");
        public void Reject(string? answer) => throw new InvalidOperationException("No repair in composition fixtures.");
        public void Dispose() => Disposed = true;
    }
    private sealed class Editor : ILocalLlmClient, IDisposable
    {
        public int Calls; public bool Disposed; public string? Request; public Template? Source;
        public Task<AiProposal> Propose(string action, string context, Template? template, string model, CancellationToken ct,
            bool allowRevision, bool? requestedShortening, TemplateEditInterpretation? interpretation)
        {
            ct.ThrowIfCancellationRequested(); Calls++; Request = context; Source = template;
            Assert.False(allowRevision); Assert.Equal("improve", action);
            return Task.FromResult(new AiProposal(Guid.NewGuid(), template!.Id,
                template.Revision, "Explica <tema> con ejemplos.", [], [], [], [], model));
        }
        public Task<AiProposal> Propose(string action, string context, Template? template, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<string[]> Available(CancellationToken ct) => throw new NotSupportedException();
        public Task<(string Text, double ElapsedMs)> Execute(CompiledPrompt prompt, string model, bool confirmed, Func<string, Task>? progress, CancellationToken ct) => throw new NotSupportedException();
        public Task<AssistPlan> PlanAssist(string command, string model, CancellationToken ct) => throw new NotSupportedException();
        public Task<Dictionary<string, string>> Suggest(Template template, string context, string model, CancellationToken ct) => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
    }
}
