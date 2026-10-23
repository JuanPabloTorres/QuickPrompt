using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Nova.Application;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class SemanticCapabilityWiringTests
{
    [Theory]
    [InlineData(typeof(ISemanticAdviceResponder))]
    [InlineData(typeof(IGroundedSourceSelector))]
    [InlineData(typeof(ISourceReferenceResolver))]
    [InlineData(typeof(ISemanticClauseReviewer))]
    public void ActualFactoryExposesEachExistingCapabilityWithoutConstructingTheFacade(Type capability)
    {
        var services = Services();
        services.AddSingleton<PreparationInference>(_ => throw new InvalidOperationException("Facade forbidden."));
        services.AddSingleton<IPromptGenerator>(_ => throw new InvalidOperationException("Generator forbidden."));
        using var provider = services.BuildServiceProvider();
        if (capability == typeof(ISemanticAdviceResponder)) Assert.IsType<PromptAdviceResponder>(provider.GetRequiredService(capability));
        else Assert.Null(provider.GetService(capability)); // Native planning extensions are retired from production.
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ActualOrchestratorFactoryUsesSeparateAdvicePortWithoutGenerationOrBrowser(bool visible)
    {
        const string request = "Explica cómo usar las instrucciones.";
        using var fixture = new Fixture(); var services = Services(fixture.Path);
        fixture.AddLibrary(services); var spy = new Capabilities(); var plan = AdvicePlan(request);
        services.AddSingleton<ITaskPlanner>(new FixedPlanner(plan));
        services.AddSingleton<IIntentInterpreter>(new MeaningOnly(plan));
        services.AddSingleton<IPromptGenerator>(new ForbiddenGenerator());
        services.AddSingleton<ISemanticAdviceResponder>(spy);
        using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<AgentOrchestrator>();
        var library = provider.GetRequiredService<PromptLibrary>(); Assert.Empty(provider.GetRequiredService<LocalStore>().Templates());
        if (visible) {
            var live = new LiveRuns(new LiveRunStore(provider.GetRequiredService<LocalStore>()), library);
            var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), request); live.Create(input);
            await engine.RunVisible(input, "http://127.0.0.1:5265", live, new ForbiddenUi(), new ForbiddenBrowser(), library);
            Assert.Equal(LiveStatus.COMPLETED, live.Get(input.RunId).Status); Assert.Equal(spy.Answer, live.View(input.RunId).Question);
        } else {
            var result = await engine.Prepare(new(Guid.NewGuid(), request), _ => Task.CompletedTask, (_,_,_) => {}, default);
            Assert.Equal(PreparationState.USER_ACTION, result.State); Assert.Equal(spy.Answer, result.Reason);
        }
        Assert.Equal(request, Assert.Single(spy.AdviceRequests)); Assert.Null(spy.AdviceSource); Assert.Null(spy.AdviceDraft);
        Assert.True(spy.Token.CanBeCanceled); Assert.Empty(provider.GetRequiredService<LocalStore>().Templates());
    }

    [Theory] [InlineData("single")] [InlineData("foreign")] [InlineData("changed-revision")] [InlineData("cancelled")]
    public async Task CompatibilityPlannerPreservesContextualSelectionAndIdentityChecks(string scenario)
    {
        const string request = "Abre el documento sobre avances del grupo.";
        var source = Document(); var recall = new Recall(source); var spy = new Capabilities { Selected = source.Id };
        var plan = SemanticFixture.Plan([SemanticOperation.SEARCH_PROMPT, SemanticOperation.RESOLVE_PROMPT, SemanticOperation.OPEN_PROMPT], TargetReference.NAMED, query: "avances");
        plan = plan with { Goal = TaskGoal.Create(request, SemanticContext.Empty, plan, [new(TaskClauseRole.REFERENCE, request)], [SemanticOperation.SEARCH_PROMPT]) };
        var services = Services(); services.AddSingleton<IPromptRetriever>(recall);
        services.AddSingleton<ISemanticIntentInterpreter>(new MeaningOnly(plan));
        services.AddSingleton<ICandidateSelector>(new ForbiddenSelector()); services.AddSingleton<IGroundedSourceSelector>(spy);
        if (scenario == "foreign") spy.Selected = Guid.NewGuid();
        if (scenario == "changed-revision") spy.BeforeSelection = () => recall.Current = source with { Revision = source.Revision + 1 };
        if (scenario == "cancelled") spy.BeforeSelection = () => throw new OperationCanceledException();
        using var provider = services.BuildServiceProvider(); var planner = CompatibilityPlanner(provider);
        var context = SemanticContext.Empty with { Selected = SemanticEntity.From(source) };
        Task<SemanticResolution> Act() => planner.Resolve(new(plan, context, [], null, null), request, Guid.NewGuid(), "qwen3:8b", null, default);
        if (scenario == "foreign") await Assert.ThrowsAsync<ValidationException>(Act);
        else if (scenario == "changed-revision") await Assert.ThrowsAsync<ConflictException>(Act);
        else if (scenario == "cancelled") await Assert.ThrowsAnyAsync<OperationCanceledException>(Act);
        else Assert.Equal(source.Id, (await Act()).Source?.Id);
        Assert.Equal(request, Assert.Single(spy.SelectionRequests));
        Assert.Equal(source.Id, Assert.Single(spy.Candidates!).Prompt.Id); Assert.True(spy.Token.CanBeCanceled);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ActualWorkflowReachesTheRegisteredSemanticOwnerAndValidatedTransport(bool visible)
    {
        const string request="Explica cómo reutilizar instrucciones.";using var fixture=new Fixture();var services=Services(fixture.Path);
        fixture.AddLibrary(services);var plan=AdvicePlan(request);var transport=new AdviceTransport();
        services.AddSingleton<ITaskPlanner>(new FixedPlanner(plan));services.AddSingleton<IIntentInterpreter>(new MeaningOnly(plan));
        services.AddSingleton<IPromptGenerator>(new ForbiddenGenerator());services.AddSingleton<IValidatedAgentModelTransport>(transport);
        using var provider=services.BuildServiceProvider();var engine=provider.GetRequiredService<AgentOrchestrator>();
        Assert.IsType<PromptAdviceResponder>(provider.GetRequiredService<ISemanticAdviceResponder>());
        if(visible) {
            var library=provider.GetRequiredService<PromptLibrary>();var live=new LiveRuns(new LiveRunStore(provider.GetRequiredService<LocalStore>()),library);
            var input=new LiveStartInput(Guid.NewGuid(),Guid.NewGuid(),request);live.Create(input);
            await engine.RunVisible(input,"http://127.0.0.1:5265",live,new ForbiddenUi(),new ForbiddenBrowser(),library);
            Assert.Equal(LiveStatus.COMPLETED,live.Get(input.RunId).Status);Assert.Equal(AdviceTransport.Answer,live.View(input.RunId).Question);
        } else {
            var result=await engine.Prepare(new(Guid.NewGuid(),request),_=>Task.CompletedTask,(_,_,_)=>{},default);
            Assert.Equal(AdviceTransport.Answer,result.Reason);
        }
        Assert.Equal("ExplainTaskGoal",Assert.Single(transport.Purposes));Assert.Empty(provider.GetRequiredService<LocalStore>().Templates());
        Assert.Contains(request,transport.Packet!.Value.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Fact] public async Task CompatibilityPlannerKeepsReferencePortSeparateFromMeaningInterpreter()
    {
        const string request = "Explica cómo funciona la biblioteca."; var plan = AdvicePlan(request); var meaning = new MeaningOnly(plan); var spy = new Capabilities();
        var services = Services(); services.AddSingleton<ISemanticIntentInterpreter>(meaning);
        services.AddSingleton<IPromptRetriever>(new Recall(Document())); services.AddSingleton<ISourceReferenceResolver>(spy);
        using var provider = services.BuildServiceProvider();
        var result = await CompatibilityPlanner(provider).Interpret(request, Guid.NewGuid(), null, null, "qwen3:8b", null, default);
        Assert.Same(plan, result.Interpretation); Assert.Equal(request, Assert.Single(spy.ReferenceRequests)); Assert.Equal(1, meaning.Calls);
    }

    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public async Task AdviceFailureOrCancellationNeverBecomesTaskSuccess(bool visible,bool cancelled)
    {
        const string request="Explica las funciones disponibles."; using var fixture=new Fixture(); var services=Services(fixture.Path);
        fixture.AddLibrary(services);var plan=AdvicePlan(request);var spy=new Capabilities { Failure=cancelled?new OperationCanceledException():new PreparationToolException("MODEL_UNAVAILABLE","explain_advice","Synthetic unavailable") };
        services.AddSingleton<ITaskPlanner>(new FixedPlanner(plan));services.AddSingleton<IIntentInterpreter>(new MeaningOnly(plan));
        services.AddSingleton<IPromptGenerator>(new ForbiddenGenerator());services.AddSingleton<ISemanticAdviceResponder>(spy);
        using var provider=services.BuildServiceProvider();var engine=provider.GetRequiredService<AgentOrchestrator>();
        if(visible) {
            var live=new LiveRuns(new LiveRunStore(provider.GetRequiredService<LocalStore>()),provider.GetRequiredService<PromptLibrary>());
            var input=new LiveStartInput(Guid.NewGuid(),Guid.NewGuid(),request);live.Create(input);
            await engine.RunVisible(input,"http://127.0.0.1:5265",live,new ForbiddenUi(),new ForbiddenBrowser(),provider.GetRequiredService<PromptLibrary>());
            Assert.Equal(LiveStatus.FAILED,live.Get(input.RunId).Status);Assert.Equal(cancelled?"TIMEOUT":"MODEL_UNAVAILABLE",live.Get(input.RunId).ErrorCode);
        } else {
            Task<PreparedDraft> Act()=>engine.Prepare(new(Guid.NewGuid(),request),_=>Task.CompletedTask,(_,_,_)=>{},default);
            if(cancelled)await Assert.ThrowsAnyAsync<OperationCanceledException>(Act);
            else {var error=await Assert.ThrowsAsync<PreparationToolException>(Act);Assert.Equal("MODEL_UNAVAILABLE",error.Code);}
        }
        Assert.Single(spy.AdviceRequests);Assert.Empty(provider.GetRequiredService<LocalStore>().Templates());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CompatibilityPlannerRejectsInventedContentFromSeparateReviewer(bool invented)
    {
        const string request = "Añade detalle sobre SQLite."; var source = Document(); var spy = new Capabilities { Invented = invented };
        var plan = SemanticFixture.Plan([SemanticOperation.RESOLVE_PROMPT, SemanticOperation.PROPOSE_ADAPTATION], TargetReference.SELECTED, source.Id, outcome: SemanticOutcome.DRAFT);
        plan = plan with { Constraints = plan.Constraints with { TargetTerms = ["SQLite"], LiteralClauses = ["detalle"] } };
        var services = Services(); services.AddSingleton<ISemanticIntentInterpreter>(new MeaningOnly(plan));
        services.AddSingleton<IPromptRetriever>(new Recall(source)); services.AddSingleton<ISemanticClauseReviewer>(spy);
        using var provider = services.BuildServiceProvider(); var planner = CompatibilityPlanner(provider);
        var context = SemanticContext.Empty with { Selected = SemanticEntity.From(source), ExplicitTargetId = source.Id };
        Task<SemanticResolution> Act() => planner.Resolve(new(plan, context, [], null, null), request, Guid.NewGuid(), "qwen3:8b", null, default);
        if (invented) { var error=await Assert.ThrowsAsync<PreparationToolException>(Act); Assert.Equal("INVALID_CONTENT_TARGETS",error.Code); Assert.Empty(spy.ClauseRequests); }
        else { var result=await Act(); Assert.Equal(["SQLite"],result.Interpretation.Constraints.TargetTerms); Assert.Equal(request,Assert.Single(spy.ClauseRequests)); }
        Assert.Equal(request,Assert.Single(spy.TargetRequests)); Assert.Equal(source.Id,spy.ReviewContext!.Selected!.Id);
    }

    private static SemanticInterpretation AdvicePlan(string request)
    {
        var plan=SemanticFixture.Plan([SemanticOperation.EXPLAIN],outcome:SemanticOutcome.ADVICE);
        return plan with { Goal=TaskGoal.Create(request,SemanticContext.Empty,plan,[new(TaskClauseRole.REFERENCE,request)]) };
    }
    private static PromptDocument Document() => PromptLibrary.Document(new Template { Id=Guid.NewGuid(),Revision=3,Title="Reporte semanal",Template="Describe avances del equipo." });

    // These tests preserve the old optional contracts outside production composition.
    private static ITaskPlanner CompatibilityPlanner(IServiceProvider provider) => new NovaTaskPlanner(
        provider.GetRequiredService<ISemanticIntentInterpreter>(), provider.GetRequiredService<IPromptRetriever>(),
        provider.GetRequiredService<AgentSessions>(), provider.GetRequiredService<ICandidateSelector>(),
        provider.GetRequiredService<PreparationOptions>(), referenceResolver: provider.GetService<ISourceReferenceResolver>(),
        clauseReviewer: provider.GetService<ISemanticClauseReviewer>(), groundedSelector: provider.GetService<IGroundedSourceSelector>());

    private sealed class Capabilities : ISemanticAdviceResponder, IGroundedSourceSelector, ISourceReferenceResolver, ISemanticClauseReviewer
    {
        public List<string> AdviceRequests=[],SelectionRequests=[],ReferenceRequests=[],TargetRequests=[],ClauseRequests=[];
        public string Answer="La biblioteca permite guardar y reutilizar instrucciones."; public Guid Selected; public Action? BeforeSelection; public bool Invented; public Exception? Failure;
        public RetrievalCandidate[]? Candidates; public SemanticContext? ReviewContext; public CancellationToken Token;
        public PromptDocument? AdviceSource; public PreparedDraft? AdviceDraft;
        public Task<string> Explain(string request,PromptDocument? source,PreparedDraft? workingDraft,string model,CancellationToken ct)
        { ct.ThrowIfCancellationRequested();AdviceRequests.Add(request);AdviceSource=source;AdviceDraft=workingDraft;Token=ct;if(Failure is not null)throw Failure;return Task.FromResult(Answer); }
        public Task<GroundedSourceChoice> SelectSource(string request,RetrievalCandidate[] candidates,SemanticContext context,string model,CancellationToken ct)
        { ct.ThrowIfCancellationRequested();SelectionRequests.Add(request);Candidates=candidates;Token=ct;BeforeSelection?.Invoke();return Task.FromResult(new GroundedSourceChoice(TargetReference.NAMED,new([Selected],"Selección controlada."))); }
        public Task<SemanticReferenceChoice?> ResolveSourceReference(string request,SemanticContext context,string model,CancellationToken ct)
        { ct.ThrowIfCancellationRequested();ReferenceRequests.Add(request);return Task.FromResult<SemanticReferenceChoice?>(null); }
        public Task<string[]> ReviewTargets(string request,string[] terms,SemanticContext context,string model,CancellationToken ct)
        { ct.ThrowIfCancellationRequested();TargetRequests.Add(request);ReviewContext=context;return Task.FromResult(Invented?new[]{"contenido inventado"}:terms); }
        public Task<string[]> ReviewClauses(string request,string[] clauses,SemanticContext context,string model,CancellationToken ct)
        { ct.ThrowIfCancellationRequested();ClauseRequests.Add(request);return Task.FromResult(clauses); }
    }
    private sealed class MeaningOnly(SemanticInterpretation plan) : ISemanticIntentInterpreter,IIntentInterpreter
    {
        public int Calls;
        public Task<SemanticInterpretation> Interpret(string message,SemanticContext context,string model,CancellationToken ct) { ct.ThrowIfCancellationRequested();Calls++;return Task.FromResult(plan); }
        public Task<Understanding> Understand(string message,string model,CancellationToken ct)=>throw new InvalidOperationException("Legacy interpretation forbidden.");
    }
    private sealed class Recall(PromptDocument source) : IPromptRetriever
    {
        public PromptDocument Current=source;
        public RetrievalCandidate[] Retrieve(string query,Guid? sourceId)=>[new(Current,0,"Sin autoridad por similitud.")];
        public PromptDocument ReadSource(Guid id)=>id==Current.Id?Current:throw new MissingPromptException();
    }
    private sealed class ForbiddenSelector : ICandidateSelector
    { public Task<SemanticCandidateChoice> Select(string message,RetrievalCandidate[] candidates,string model,CancellationToken ct)=>throw new InvalidOperationException("Basic selector is not the contextual capability."); }
    private sealed class ForbiddenGenerator : IPromptGenerator
    { public Task<PromptProposal> Generate(PrepareInput input,Understanding understanding,RetrievalCandidate[] candidates,CancellationToken ct)=>throw new InvalidOperationException("Advice must not generate a draft."); }
    private sealed class AdviceTransport : IValidatedAgentModelTransport
    {
        public const string Answer="Puedes reutilizar una instrucción completando sus campos.";
        public List<string> Purposes=[];public JsonElement? Packet;
        public Task<JsonDocument> Send(object packet,CancellationToken ct,string purpose)
        { ct.ThrowIfCancellationRequested();Purposes.Add(purpose);Packet=JsonSerializer.SerializeToElement(packet);return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message=new {content=JsonSerializer.Serialize(new {answer=Answer})} }))); }
        public bool UsesThinking(string model)=>false;
        public void Observe(int stage,string content,string? error) { }
        public void Reject(string? answer)=>throw new InvalidOperationException("Unexpected rejection of controlled valid reply.");
    }
    private sealed class ForbiddenUi : IUiDecisionModel
    { public Task<UiDecision> Decide(UiDecisionContext context,CancellationToken ct)=>throw new InvalidOperationException("Advice must not request a UI decision."); }
    private sealed class ForbiddenBrowser : IVisibleBrowserFactory
    { public IVisibleBrowser Create(Action<string>? disconnected=null)=>throw new InvalidOperationException("Advice must not open a browser."); }
    private sealed class FixedPlanner(SemanticInterpretation plan) : ITaskPlanner
    {
        public SemanticContext Context(Guid sessionId,Guid? selectedId=null,int? revision=null)=>SemanticContext.Empty;
        public Task<SemanticResolution> Interpret(string message,Guid sessionId,Guid? selectedId,int? revision,string model,RetrievalFilter? filter,CancellationToken ct,LiveCommand? command=null)=>Task.FromResult(new SemanticResolution(plan,SemanticContext.Empty,[],null,null));
        public Task<SemanticResolution> Resolve(SemanticResolution resolution,string message,Guid sessionId,string model,RetrievalFilter? filter,CancellationToken ct)=>Task.FromResult(resolution);
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"nova-wiring-fixture-"+Guid.NewGuid());
        public string Path=>System.IO.Path.Combine(directory,"synthetic.sqlite");
        public void AddLibrary(IServiceCollection services)=>Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules",true)!.GetMethod("AddPromptLibraryModule",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,[services,Path]);
        public void Dispose()
        {
            var target=System.IO.Path.GetFullPath(directory);var temp=System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            Assert.StartsWith(temp,target);Assert.StartsWith("nova-wiring-fixture-",System.IO.Path.GetFileName(target));
            if(Directory.Exists(target))Directory.Delete(target,true);
        }
    }

    private static ServiceCollection Services(string? dataPath = null)
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["QuickPrompt:Ollama"] = "http://127.0.0.1:1"
        }).Build();
        Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!
            .GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [services, config, dataPath ?? Path.Combine(Path.GetTempPath(), "nova-wiring-" + Guid.NewGuid(), "synthetic.sqlite")]);
        return services;
    }
}
