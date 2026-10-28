using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record SemanticResolution(SemanticInterpretation Interpretation, SemanticContext Context, RetrievalCandidate[] Candidates, PromptDocument? Source, PreparedDraft? ActiveDraft);

public interface ITaskPlanner
{
    SemanticContext Context(Guid sessionId, Guid? selectedId = null, int? revision = null);
    Task<SemanticResolution> Interpret(string message, Guid sessionId, Guid? selectedId, int? revision, string model, RetrievalFilter? filter, CancellationToken ct, LiveCommand? command = null);
    Task<SemanticResolution> Resolve(SemanticResolution resolution, string message, Guid sessionId, string model, RetrievalFilter? filter, CancellationToken ct);
}

// Shared application coordinator. Capability owners retain state, source and domain invariants.
public sealed class NovaTaskPlanner : ITaskPlanner
{
    private readonly ISemanticIntentInterpreter interpreter; private readonly IPromptRetriever retriever;
    private readonly AgentSessions sessions; private readonly PreparationOptions? options;
    private readonly TaskSourceResolver sources; private readonly TaskConversationContext conversation;
    private readonly IInputScopeInterpreter? inputScopes;
    public NovaTaskPlanner(ISemanticIntentInterpreter interpreter, IPromptRetriever retriever, AgentSessions sessions, ICandidateSelector selector, PreparationOptions? options = null, bool directSourceRead = true, IInputScopeInterpreter? scopeInterpreter = null,
        ISourceReferenceResolver? referenceResolver = null, ISemanticClauseReviewer? clauseReviewer = null, IGroundedSourceSelector? groundedSelector = null)
    {
        this.interpreter=interpreter; this.retriever=retriever; this.sessions=sessions; this.options=options;
        inputScopes=scopeInterpreter ?? interpreter as IInputScopeInterpreter;
        sources=new(interpreter,retriever,sessions,selector,options,directSourceRead,inputScopes,referenceResolver,clauseReviewer,groundedSelector); conversation=new(sessions,sources);
    }
    public SemanticContext Context(Guid sessionId, Guid? selectedId = null, int? revision = null) => conversation.Restore(sessionId,selectedId,revision);
    public async Task<SemanticResolution> Interpret(string message, Guid sessionId, Guid? selectedId, int? revision, string model, RetrievalFilter? filter, CancellationToken ct, LiveCommand? command = null)
    {
        using var measurement = NovaTelemetry.Measure("planning.interpret");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct); limit.CancelAfter(TimeSpan.FromSeconds(options?.StageTimeoutSeconds ?? 90));
        var context = conversation.Restore(sessionId, selectedId, revision) with { RetrievalScope = filter };
        if (command is not null) {
            var explicitPlan = command.Compile(message, context);
            SemanticPlanValidator.Validate(explicitPlan, context, message);
            return new(explicitPlan, context, [], null, null);
        }
        // A data-only reply completes the existing field task. Preserve its requested
        // effects, destination and constraints rather than inferring a new task.
        // Authoring and writes require their own checkpoint/approval, never this shortcut.
        var pendingSession = sessions.Get(sessionId);
        if (context.PendingPlan is { } pendingPlan && pendingPlan.Has(SemanticOperation.FILL_VARIABLES)
            && !pendingPlan.Adapts && !pendingPlan.Creates
            && !pendingPlan.Has(SemanticOperation.REQUEST_SAVE_VERSION) && !pendingPlan.Has(SemanticOperation.REQUEST_SAVE_COPY)
            && !string.IsNullOrWhiteSpace(context.PendingRequest)
            && context.Selected is { InputNames: { } pendingNames } pendingSource && pendingSource.Id == pendingPlan.ResolvedTargetId
            && GroundedVariableResolver.LiteralAssignments(message, pendingNames).Count > 0
            && string.IsNullOrWhiteSpace(GroundedVariableResolver.InstructionDirective(message, pendingNames))) {
            if (pendingSource.Stale || pendingSession?.Source?.Id != pendingSource.Id || pendingSession.SourceRevision != pendingSource.Revision)
                throw new ConflictException("El prompt cambió mientras esperaba los datos. Revisa su versión actual y vuelve a solicitar la acción.");
            var resumed = pendingPlan with { ContinuesPendingTask = true, InputScope = null,
                Authoring = pendingPlan.Authoring is { Kind: InstructionAuthorship.TEMPLATE_UNCHANGED } authoring
                    ? authoring with { RequestHash = LiveRuns.Hash(message), PendingRequestHash = LiveRuns.Hash(context.PendingRequest) } : pendingPlan.Authoring };
            SemanticPlanValidator.Validate(resumed, context, context.PendingRequest + "\n" + message);
            return new(resumed, context, [], null, pendingSession.Draft);
        }
        if (interpreter is ActionChoiceInterpreter) {
            var choice = await interpreter.Interpret(message, context, model, limit.Token);
            return new(choice, context, [], null, null);
        }
        if (context.ExplicitTargetId is null) {
            // Resolve identities against the actual catalog, excluding field data.
            var directive = GroundedVariableResolver.InstructionDirective(message, context.ActiveDraftInputNames ?? context.Selected?.InputNames ?? []);
            context = context with { CatalogCandidates = retriever.Retrieve(directive, null, filter).Where(candidate => candidate.Score >= 2).Take(6).Select(candidate => sources.Snapshot(candidate.Prompt)).ToArray() };
        }
        // Selecting a displayed candidate resolves a typed UI question. The original
        // natural task was already interpreted; do not reinterpret its generated question.
        var pending = context.PendingPlan;
        var chosenCandidate = selectedId is { } chosen && context.Candidates.Any(candidate => candidate.Id == chosen && !candidate.Stale);
        var sourceTaskPending = pending?.ValidationNotes?.Any(note => note is "UNRESOLVED_SOURCE_ROLE" or "MISSING_TARGET_REFERENCE" or "UNRESOLVED_SOURCE" or "UNVERIFIED_NAMED_CONTENT_SOURCE") == true
            || pending?.DeferredGoals?.Any(goal => goal is SemanticOperation.SEARCH_PROMPT or SemanticOperation.OPEN_PROMPT or SemanticOperation.PROPOSE_ADAPTATION
                or SemanticOperation.FILL_VARIABLES or SemanticOperation.COPY_PROMPT or SemanticOperation.COPY_TO_CLIPBOARD or SemanticOperation.PREPARE_LOCAL_EXECUTION or SemanticOperation.PREPARE_PROVIDER or SemanticOperation.REQUEST_SAVE_VERSION or SemanticOperation.REQUEST_DELETE) == true
            || pending?.TargetReference is not null and not TargetReference.NONE;
        var resumeSelection = chosenCandidate && pending?.DesiredOutcome == SemanticOutcome.CLARIFICATION
            && sourceTaskPending && !string.IsNullOrWhiteSpace(context.PendingRequest)
            && string.Equals(message, context.PendingRequest, StringComparison.Ordinal);
        ReferencePreparation? prepared = null;
        if (!resumeSelection) {
            prepared = await sources.PrepareReference(message, context, model, filter, limit.Token);
            if (prepared is not null) {
                context = prepared.Context;
                if (prepared.Question is { } question) {
                    SemanticPlanValidator.Validate(question, context, message);
                    var suspended = new SemanticResolution(question, context, prepared.Candidates, null, sessions.Get(sessionId)?.Draft);
                    conversation.Checkpoint(sessionId, suspended, question.TargetReference == TargetReference.NAMED, message);
                    return suspended;
                }
            }
        }
        if (!resumeSelection) context = await ScopeInputs(message, context, model, limit.Token);
        var plan = resumeSelection ? null : await interpreter.Interpret(message, context, model, limit.Token);
        SemanticEntity? chosenSource = resumeSelection ? context.Selected : null;
        if (!resumeSelection && pending?.DesiredOutcome == SemanticOutcome.CLARIFICATION && sourceTaskPending && !string.IsNullOrWhiteSpace(context.PendingRequest)
            && !plan!.Has(SemanticOperation.CANCEL) && !plan.Has(SemanticOperation.PAUSE) && !plan.Has(SemanticOperation.RESUME)) {
            var candidates = (sessions.Get(sessionId)?.Candidates ?? []).Concat(retriever.Retrieve(message, null, filter).Select(candidate => candidate.Prompt));
            var exact = candidates.Where(candidate => !candidate.IsArchived && string.Equals(message.Trim(), candidate.Title, StringComparison.OrdinalIgnoreCase))
                .GroupBy(candidate => candidate.Id).Select(group => group.First()).ToArray();
            if (exact is { Length: 1 }) chosenSource = SemanticEntity.From(sources.Read(exact[0].Id, exact[0].Revision));
        }
        if (chosenSource is not null && context.PendingRequest is { } literalTask) {
            context = context with { Selected = chosenSource, ExplicitTargetId = chosenSource.Id,
                Candidates = context.Candidates.Where(candidate => candidate.Id != chosenSource.Id).Prepend(chosenSource).Take(6).ToArray(),
                DraftSource = null, ActiveDraft = null, ActiveDraftValues = null, ActiveDraftInputNames = null, ResolvedReference = null, ResolvedSource = null,
                AuthorizedValues = context.Selected?.Id == chosenSource.Id && context.Selected.Revision == chosenSource.Revision ? context.AuthorizedValues : null };
            // A typed source choice resolves identity, not the user's already
            // accepted outcomes. Reuse that immutable contract when it belongs to
            // this exact task; legacy/unbound interpretations still need refinement.
            if (resumeSelection && CanBindAcceptedSource(pending!, literalTask, chosenSource))
                plan = BindAcceptedSource(pending!, sources.Read(chosenSource.Id, chosenSource.Revision));
            else {
                context = await ScopeInputs(literalTask, context, model, limit.Token);
                plan = await interpreter.Interpret(literalTask, context with { PendingPlan = null, PendingRequest = null, Constraints = null }, model, limit.Token);
                plan = plan with { ValidationNotes = [..plan.ValidationNotes ?? [], "PENDING_SOURCE_REINTERPRETED"] };
            }
            if (plan.Executes) {
                var goal = plan.Goal;
                var goals = plan.Intentions.Where(SemanticPlanCompiler.Goals.Contains).Where(goal => goal != SemanticOperation.EXECUTE_LOCAL).ToArray();
                var constraints = plan.Constraints with { ForbiddenOperations = plan.Constraints.ForbiddenOperations.Append(SemanticOperation.EXECUTE_LOCAL).Distinct().ToArray() };
                plan = SemanticPlanCompiler.Compile(new(goals, plan.TargetReference, plan.TargetDescription, plan.ResolvedTargetId, plan.Query,
                    plan.RequestedTransformation, constraints, SemanticOutcome.READY_LOCAL, plan.MissingInformation, plan.RequiresConfirmation, "", plan.ProviderId,
                    "Origen identificado. La ejecución final requiere una instrucción actual o su control explícito."));
                if (goal is not null) plan = plan with { Goal = TaskGoal.Create(goal.UserRequest, context, plan, goal.Clauses) };
            }
            plan = plan with { ContinuesPendingTask = true };
        }
        if (plan!.ObservedSources.Length > 0)
            context = context with { Candidates = plan.ObservedSources.Concat(context.Candidates).DistinctBy(source => source.Id).Take(12).ToArray() };
        if (context.InputScope is not null) plan = plan! with { InputScope = context.InputScope };
        SemanticPlanValidator.Validate(plan!, context, message);
        return new(plan!, context, prepared?.Candidates ?? [], prepared?.Source, sessions.Get(sessionId)?.Draft);
    }
    private static bool CanBindAcceptedSource(SemanticInterpretation pending, string request, SemanticEntity source)
        => pending.IntentContract is { Reference: not TargetReference.NONE } intent
            && pending.Goal is { WorkingTarget: null } goal && ReferenceEquals(goal.IntentContract, intent)
            && goal.UserRequest == request && intent.RequestHash == LiveRuns.Hash(request) && intent.Allows(pending)
            && (intent.TargetId is null || intent.TargetId == source.Id)
            && goal.TargetReferences.All(target => target.Id == source.Id && target.Revision == source.Revision)
            // A source choice cannot renew an earlier execution authorization.
            && !intent.RequestedEffects.Contains(SemanticOperation.EXECUTE_LOCAL);

    private static SemanticInterpretation BindAcceptedSource(SemanticInterpretation pending, PromptDocument source)
    {
        var intent = pending.IntentContract!;
        var meaning = new SemanticMeaning(intent.RequestedEffects.ToArray(), intent.Reference, source.Title, source.Id,
            pending.Query, pending.RequestedTransformation, pending.Constraints, intent.Outcome, [], pending.RequiresConfirmation,
            "", intent.ProviderId, pending.Explanation) {
                CopyContent = intent.CopyContent, DeletionEvidence = pending.DeletionEvidence, ClipboardEvidence = pending.ClipboardEvidence
            };
        var plan = SemanticPlanCompiler.Compile(meaning, intent.AllowedEffects.Contains(SemanticOperation.FILL_VARIABLES));
        plan = plan with { IntentContract = intent, Goal = pending.Goal!.Bind(source), InputScope = pending.InputScope,
            Authoring = pending.Authoring, WorkflowGoals = pending.WorkflowGoals,
            ValidationNotes = [..pending.ValidationNotes ?? [], "PENDING_SOURCE_BOUND_WITHOUT_INFERENCE"] };
        if (!intent.Allows(plan)) throw new ValidationException("La selección del origen alteró el contrato de acciones pendiente.");
        return plan;
    }
    private async Task<SemanticContext> ScopeInputs(string request, SemanticContext context, string model, CancellationToken ct)
    {
        if (inputScopes is not { } scopeInterpreter) return context;
        return context with { InputScope = await scopeInterpreter.ResolveInputScope(request, context, model, ct) };
    }
    public async Task<SemanticResolution> Resolve(SemanticResolution resolution, string message, Guid sessionId, string model, RetrievalFilter? filter, CancellationToken ct)
    {
        SemanticPlanValidator.ValidateIntent(resolution.Interpretation);
        if (resolution.Interpretation.DesiredOutcome == SemanticOutcome.CLARIFICATION && resolution.Interpretation.DeferredGoals is { Length: > 0 }
            && resolution.Interpretation.Intentions is [SemanticOperation.ASK_CLARIFICATION]) return resolution;
        var lookup=resolution.Interpretation.Has(SemanticOperation.SEARCH_PROMPT)||resolution.Interpretation.TargetReference==TargetReference.NAMED;
        var resolved=await sources.Resolve(resolution,message,sessionId,model,filter,ct);
        if (resolved.Interpretation.Authoring is not null) {
            var goal = TaskGoal.FromAuthoring(message, resolved.Context, resolved.Interpretation);
            if (resolved.Source is { } source) goal = goal.Bind(source);
            resolved = resolved with { Interpretation = resolved.Interpretation with { Goal = goal } };
        }
        SemanticPlanValidator.ValidateIntent(resolved.Interpretation);
        conversation.Checkpoint(sessionId,resolved,lookup);
        return resolved;
    }
}
