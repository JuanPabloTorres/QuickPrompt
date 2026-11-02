using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Controlled proposals and executor receipts prove ordering/authority only.
// No model, browser, PostgreSQL or actual human acceptance is involved.
public sealed class ContentReviewExecutionBoundaryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-content-execution-" + Guid.NewGuid());
    private readonly LocalStore store;
    private readonly PromptLibrary library;

    public ContentReviewExecutionBoundaryTests()
    {
        store = new(Path.Combine(directory, "synthetic.sqlite"));
        var adapter = new NovaPromptStore(store);
        library = new(adapter, adapter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnAuthoredBodyCannotExecuteBeforeItsExactContentReview(bool adapts)
    {
        const string request = "Prepara una instrucción clara. Ejecuta ahora.";
        var source = adapts ? library.Create(new("Fuente", "Explica el tema. No inventes datos. Consulta https://example.test/manual.")) : null;
        var plan = Plan(request, source, adapts ? SemanticOperation.PROPOSE_ADAPTATION : SemanticOperation.CREATE_DRAFT);
        var generator = new ProposalGenerator(source);
        var executor = new ExecutorSpy();
        var tools = new List<string>();
        var engine = Engine(plan, source, generator, executor);

        var result = await engine.Prepare(new(Guid.NewGuid(), request, SourcePromptId: source?.Id, SourceRevision: source?.Revision),
            _ => Task.CompletedTask, (tool, _, _) => tools.Add(tool), default);

        Assert.Equal(PreparationState.USER_ACTION, result.State);
        Assert.Equal(1, generator.Calls);
        Assert.Equal(0, executor.Calls);
        Assert.DoesNotContain("execute_local_llm", tools);
        Assert.Null(result.LocalResponse);
        Assert.Contains("Revisa", result.Reason);
        Assert.True(plan.Goal!.HasAuthoringStructure(result.TemplateText));
        Assert.False(plan.Goal.HasHumanContentApproval(result.TemplateText));
        Assert.Empty(plan.Goal.ApprovalRequirements);
        if (source is not null)
        {
            Assert.Contains(plan.Goal.AuthoringContract!.GetContentReview(result.TemplateText).LiteralChanges.Entries,
                change => change.Kind == "MISSING_SOURCE_CLAUSE");
            Assert.Contains(plan.Goal.AuthoringContract.GetContentReview(result.TemplateText).LiteralChanges.Entries,
                change => change.Kind == "REMOVED_SOURCE_URI");
            Assert.Equal(source.TemplateText, library.Get(source.Id).TemplateText);
        }
    }

    [Fact]
    public async Task ExistingInstructionExecutionWithoutAnAuthoringContractStillUsesItsAuthorizedExecutor()
    {
        const string request = "Ejecuta ahora.";
        var source = library.Create(new("Fuente", "Explica el tema sin inventar datos."));
        var plan = Plan(request, source, SemanticOperation.OPEN_PROMPT);
        var generator = new ProposalGenerator(source);
        var executor = new ExecutorSpy();
        var engine = Engine(plan, source, generator, executor);

        var result = await engine.Prepare(new(Guid.NewGuid(), request, SourcePromptId: source.Id, SourceRevision: source.Revision),
            _ => Task.CompletedTask, (_, _, _) => { }, default);

        Assert.Equal(PreparationState.READY, result.State);
        Assert.Equal(0, generator.Calls);
        Assert.Equal(1, executor.Calls);
        Assert.Equal("CONTROLLED_EXECUTOR_RESULT", result.LocalResponse);
        Assert.Null(plan.Goal!.AuthoringContract);
        Assert.Equal(source.TemplateText, library.Get(source.Id).TemplateText);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task VisibleCopyWaitsForContentAndSavingStillNeedsItsOwnDecision(bool save, bool copy)
    {
        var request = "Prepara una instrucción clara." + (copy ? " Copia ahora." : "") + (save ? " Prepara el guardado como copia." : "");
        var operations = new List<SemanticOperation> { SemanticOperation.CREATE_DRAFT };
        if (copy) operations.Add(SemanticOperation.COPY_TO_CLIPBOARD);
        if (save) operations.Add(SemanticOperation.REQUEST_SAVE_COPY);
        var plan = SemanticPlanCompiler.Compile(new(operations.ToArray(), TargetReference.NONE, "Nueva tarea", null, "",
            "Preparar una instrucción clara.", new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT,
            [], save, "", null, "Resultados explícitos controlados.") { ClipboardEvidence = copy ? "Copia ahora." : null });
        plan = plan with { Goal = TaskGoal.Create(request, SemanticContext.Empty, plan, [new(TaskClauseRole.WORK_ORDER, request)]) };
        var live = new LiveRuns(new LiveRunStore(store), library);
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), request, "SAME_TAB");
        live.Create(input);
        var generator = new ProposalGenerator(null);
        var engine = new AgentOrchestrator(generator, new SqlitePromptRetriever(store), generator, new GroundedVariableResolver(),
            new DeterministicPromptEvaluator(), new PreparedDraftStore(store), new(),
            taskPlanner: new Planner(plan, null), stopAfterVerifiedResult: true, executeGroundedPlan: true);
        var browser = new ReviewBrowser(live, library, input.RunId);
        var pending = engine.RunVisible(input, "http://127.0.0.1:5226", live, new ReadyDecision(), browser, library);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            var initial = await WaitForReview(copy || !save ? LiveApprovalPurpose.CONTENT_ONLY : LiveApprovalPurpose.SAVE);
            Assert.Empty(store.Templates());
            Assert.DoesNotContain("ui_copy_compiled_prompt", browser.Tools);
            Approve(initial);
            if (save && copy)
            {
                var saveProposal = await WaitForReview(LiveApprovalPurpose.SAVE);
                Assert.Contains("ui_copy_compiled_prompt", browser.Tools);
                Assert.Empty(store.Templates());
                Assert.False(live.Active(input.RunId).ApprovalDecision.Task.IsCompleted);
                Approve(saveProposal);
            }
            await pending.WaitAsync(timeout.Token);
            Assert.True(live.Get(input.RunId).Status == LiveStatus.COMPLETED,
                string.Join(" | ", live.Events(input.RunId, 0).Select(item => item.ErrorCode + ":" + item.SafeDisplayMessage)));
            Assert.Equal(save, live.Get(input.RunId).EffectCommitted);
            Assert.Equal(save ? 1 : 0, store.Templates().Count);
            Assert.Equal(copy ? 1 : 0, browser.Tools.Count(tool => tool == "ui_copy_compiled_prompt"));
            Assert.Equal(save ? 1 : 0, browser.Tools.Count(tool => tool == "commit_approved"));
        }
        finally
        {
            if (!pending.IsCompleted) live.Cancel(input.RunId);
            await pending;
        }

        async Task<LiveApproval> WaitForReview(LiveApprovalPurpose purpose)
        {
            while (!pending.IsCompleted)
            {
                var view = live.View(input.RunId);
                if (view.Run.Status == LiveStatus.AWAITING_APPROVAL && view.Run.Approval == "PENDING" && view.Proposal?.Purpose == purpose)
                    return view.Proposal;
                await Task.Delay(10, timeout.Token);
            }
            throw new InvalidOperationException("Expected review was not reached: " + string.Join(" | ",
                live.Events(input.RunId, 0).Select(item => item.ErrorCode + ":" + item.SafeDisplayMessage)));
        }
        void Approve(LiveApproval proposal) => live.Approve(input.RunId, new(proposal.Id, proposal.Hash, proposal.Revision, true)
        {
            ContentReviewed = true, ContentReviewHash = proposal.ContentReview!.ContractHash,
            ApprovedLiteralChangeIds = proposal.ContentReview.LiteralChanges.Entries.Select(change => change.Id).ToArray()
        });
    }

    private static SemanticInterpretation Plan(string request, PromptDocument? source, SemanticOperation content)
    {
        var context = source is null ? SemanticContext.Empty : SemanticContext.Empty with { Selected = SemanticEntity.From(source) };
        var plan = SemanticPlanCompiler.Compile(new([content, SemanticOperation.EXECUTE_LOCAL],
            source is null ? TargetReference.NONE : TargetReference.SELECTED, source?.Title ?? "Nueva tarea", source?.Id, "",
            content == SemanticOperation.OPEN_PROMPT ? "" : "Preparar una instrucción clara.",
            new([], true, null, null, [], [], false), SemanticOutcome.LOCAL_RESPONSE, [], false,
            "Ejecuta ahora", "ollama", "Ejecución explícita controlada."));
        return plan with { Goal = TaskGoal.Create(request, context, plan,
            content == SemanticOperation.OPEN_PROMPT ? [new(TaskClauseRole.WORKFLOW, request)] : [new(TaskClauseRole.WORK_ORDER, request)]) };
    }

    private AgentOrchestrator Engine(SemanticInterpretation plan, PromptDocument? source, ProposalGenerator generator, ExecutorSpy executor)
        => new(generator, new SqlitePromptRetriever(store), generator, new GroundedVariableResolver(), new DeterministicPromptEvaluator(),
            new PreparedDraftStore(store), new(), executor: executor, taskPlanner: new Planner(plan, source));

    private sealed class Planner(SemanticInterpretation plan, PromptDocument? source) : ITaskPlanner
    {
        public SemanticContext Context(Guid sessionId, Guid? selectedId = null, int? revision = null) =>
            source is null ? SemanticContext.Empty : SemanticContext.Empty with { Selected = SemanticEntity.From(source) };
        public Task<SemanticResolution> Interpret(string message, Guid sessionId, Guid? selectedId, int? revision,
            string model, RetrievalFilter? filter, CancellationToken ct, LiveCommand? command = null) =>
            Task.FromResult(new SemanticResolution(plan, Context(sessionId), source is null ? [] : [new(source, 10, "Origen real")], source, null));
        public Task<SemanticResolution> Resolve(SemanticResolution resolution, string message, Guid sessionId, string model,
            RetrievalFilter? filter, CancellationToken ct) => Task.FromResult(resolution);
    }

    private sealed class ProposalGenerator(PromptDocument? source) : IIntentInterpreter, IPromptGenerator
    {
        public int Calls;
        public Task<Understanding> Understand(string message, string model, CancellationToken ct) => throw new InvalidOperationException("Planning is separately controlled.");
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls++;
            var goal = input.Interpretation!.Goal!;
            var fields = goal.InputContract ?? PromptInputContract.FromUserSelection(goal.UserRequest, source?.TemplateText ?? "", source is not null, []);
            goal.SetInputContract(fields);
            var snapshot = source is null ? null : new DraftSourceSnapshot(source.Id, source.Revision, source.TemplateText,
                source.Title, source.Description, source.CategoryId, LiveRuns.Hash(source.TemplateText));
            var contract = new DraftProposalContract(goal.UserRequest, snapshot, fields);
            goal.BindAuthoringContract(contract);
            var proposed = contract.RegisterProposal(new(source?.Title ?? "Propuesta", "Explica el tema con claridad.", source?.Description ?? ""));
            return Task.FromResult(new PromptProposal(source is null ? PreparationDecision.CREATE_NEW : PreparationDecision.ADAPT_EXISTING,
                source?.Id, proposed.Title, proposed.Description, proposed.TemplateText, [], "", "Propuesta controlada, no aprobada.", []));
        }
    }

    private sealed class ExecutorSpy : ILocalPreparedExecutor
    {
        public int Calls;
        public Task<string> Execute(PreparedDraft draft, string model, Func<string, Task>? progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls++;
            return Task.FromResult("CONTROLLED_EXECUTOR_RESULT");
        }
    }

    private sealed class ReadyDecision : IUiDecisionModel
    {
        public Task<UiDecision> Decide(UiDecisionContext context, CancellationToken ct)
        {
            var ready = new[] { "ui_open_create_prompt", "ui_prepare_creation", "ui_show_compiled_prompt", "ui_copy_compiled_prompt", "finish" }
                .FirstOrDefault(name => context.Tools.Any(tool => tool.Name == name))
                ?? throw new InvalidOperationException("No expected test action is available.");
            return Task.FromResult(new UiDecision(ready, JsonSerializer.SerializeToElement(new { })));
        }
    }

    private sealed class ReviewBrowser(LiveRuns live, PromptLibrary library, Guid runId) : IVisibleBrowser, IVisibleBrowserFactory
    {
        private UiVisibleState state = new("discover", [], null, null, null, null, [], [], false, false, null, null, false, false);
        public List<string> Tools { get; } = [];
        public IVisibleBrowser Create(Action<string>? disconnected = null) => this;
        public Task<UiVisibleState> Start(Guid id, string origin, bool allowed, CancellationToken ct, string editRequest = "") => Task.FromResult(state);
        public Task<UiVisibleState> Execute(string tool, JsonElement arguments, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Tools.Add(tool);
            if (tool == "ui_open_create_prompt") state = state with { Mode = "create", Title = "", TemplateText = "" };
            if (tool == "ui_prepare_creation")
            {
                var body = arguments.GetProperty("templateText").GetString()!;
                state = state with { Title = arguments.GetProperty("title").GetString(), TemplateText = body, DraftChanged = true,
                    VariableNames = TemplateCompiler.Names(body), CompiledReady = true, CompiledText = body };
            }
            if (tool == "ui_copy_compiled_prompt") state = state with { Clipboard = new(Guid.NewGuid(), state.PromptId, state.Revision, state.CompiledText!) };
            if (tool == "commit_approved")
            {
                var saved = live.Commit(runId, live.View(runId).Proposal!.Draft);
                Assert.Equal(saved.TemplateText, library.Get(saved.Id).TemplateText);
                state = state with { Mode = "focus", PromptId = saved.Id, Revision = saved.Revision, Title = saved.Title,
                    TemplateText = saved.TemplateText, CompiledText = saved.TemplateText, DraftChanged = false };
            }
            return Task.FromResult(state);
        }
        public Task Focus(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        var full = Path.GetFullPath(directory);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("nova-content-execution-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected fixture path.");
        Directory.Delete(full, true);
    }
}
