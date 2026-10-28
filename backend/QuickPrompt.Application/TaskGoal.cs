using System.Text.Json.Serialization;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record GoalTarget(Guid Id, int Revision);
public record WorkingArtifactTarget(Guid ArtifactId, string BodyHash, int CharacterCount, int Revision = 1);
public record TaskScope(SemanticOperation[] AllowedEffects, SemanticOperation[] ForbiddenEffects, SemanticOperation[] ApprovalRequirements)
{
    public bool Allows(SemanticOperation[] effects) => AllowedEffects.ToHashSet().SetEquals(effects) && !effects.Intersect(ForbiddenEffects).Any();
}
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompletionCondition { SOURCE_VISIBLE, DRAFT_CHANGED, NEW_DRAFT_VISIBLE, CONTENT_VALID, COMPILED_VISIBLE, CLIPBOARD_WRITTEN, VALUES_APPLIED, DESTINATION_READY, LOCAL_RESPONSE, APPROVED_WRITE, EXPLANATION_READY, SEARCH_RESULTS_VISIBLE, APPROVED_DELETION, TEMPLATE_VISIBLE }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskClauseRole { CONTENT, WORKFLOW, INPUT_DATA, REFERENCE, WORK_ORDER }
public record TaskClause(TaskClauseRole Role, string Quote)
{
    // Authoring evidence proves authority; this separate interpreted change is
    // generation data and cannot grant an application operation or approval.
    [JsonIgnore] public string? ContentChange { get; init; }
}

// Process/session-owned authority. Never persisted, sent to the editor, or logged as a request packet.
public record TaskGoal(Guid GoalId,
    [property: AgentRepresentation(AgentRepresentation.UserWorkOrder, "The full original user work order bound to the current goal, including any authorized continuation. It may describe both current NOVA actions and a task to put inside a future instruction. Neither a generator-selected subset nor an interpreted summary replaces this authority. Current approval/deferral controls must not silently become restrictions on the future reader.")] string UserRequest, string SessionContextReference, GoalTarget[] TargetReferences,
    SemanticOutcome RequestedOutcome, SemanticOperation[] RequiredEffects, SemanticOperation[] ForbiddenEffects,
    SemanticConstraints Constraints, string[] RequiredInformation, CompletionCondition[] CompletionConditions,
    SemanticOperation[] ApprovalRequirements, TaskClause[] Clauses)
{
    // Requested roots are distinct from compiler dependencies (read/validate/compile).
    // Owned by the interpreter/compiler boundary, never accepted from HTTP input.
    [JsonIgnore] public SemanticOperation[] RequestedEffects { get; init; } = RequiredEffects;
    [JsonIgnore] public CopyContent CopyContent { get; init; } = CopyContent.RENDERED;
    [JsonIgnore] public WorkingArtifactTarget? WorkingTarget { get; init; }
    [JsonIgnore] public InstructionAuthoring? AuthoringDecision { get; init; }
    [JsonIgnore] public TaskIntentContract? IntentContract { get; init; }
    [JsonIgnore] public CompletionCondition[] EffectiveCompletionConditions => IntentContract?.RequiredOutcomes.ToArray() ?? CompletionConditions;
    [JsonIgnore] public TaskScope Scope => IntentContract is { } intent
        ? new(intent.AllowedEffects.ToArray(), intent.ForbiddenEffects.ToArray(), intent.ApprovalRequirements.ToArray())
        : new(RequiredEffects, ForbiddenEffects, ApprovalRequirements);
    public bool AllowsPlan(SemanticInterpretation plan) => CopyContent == plan.CopyContent && (IntentContract?.Allows(plan) ?? Scope.Allows(plan.Intentions));
    private readonly object assessmentLock=new();
    private readonly Queue<string> assessedBodies=new();
    private string? humanApprovedBody;
    private string? humanApprovedDraft;
    private string? authoringFingerprint;
    [JsonIgnore] public DraftProposalContract? AuthoringContract { get; private set; }
    [JsonIgnore] public PromptInputContract? InputContract { get; private set; }
    [JsonIgnore] public AuthoringReferenceCoverage? ReferenceCoverage { get; private set; }
    [JsonIgnore] public bool TitleInterpreted { get; private set; }
    [JsonIgnore] public string? RequestedTitle { get; private set; }
    public void BindRequestedTitle(string request,string? title)
    {
        if(title is not null&&(string.IsNullOrWhiteSpace(title)||title.Length>160||title.Contains('\n')||!request.Contains(title,StringComparison.Ordinal)))
            throw new ValidationException("El título no es una cita literal del encargo.");
        lock(assessmentLock) {
            if(TitleInterpreted&&RequestedTitle!=title) throw new ValidationException("El título solicitado no puede reinterpretarse durante el reintento.");
            RequestedTitle=title;TitleInterpreted=true;
        }
    }
    // Trusted content preparation binds this once; it cannot rewrite an accepted declaration on retry.
    public void SetInputContract(PromptInputContract contract)
    {
        lock (assessmentLock) {
            if (InputContract is not null && InputContract != contract)
                throw new ValidationException("No se puede reinterpretar el contrato de campos durante la misma tarea.");
            InputContract = contract;
        }
    }
    /// <summary>Fix source-grounded reference roles for this task; revisions may not replace that expectation.</summary>
    public void BindReferenceCoverage(AuthoringReferenceCoverage coverage)
    {
        if (!coverage.Resolved) throw new ValidationException("Falta resolver el contrato de referencias.");
        lock (assessmentLock)
        {
            if (ReferenceCoverage is not null && ReferenceCoverage.Fingerprint != coverage.Fingerprint)
                throw new ValidationException("El papel de las referencias no puede reinterpretarse durante la misma tarea.");
            ReferenceCoverage = coverage;
        }
    }
    /// <summary>Bind application-owned context before drafting; this does not approve the proposed meaning.</summary>
    public void BindAuthoringContract(DraftProposalContract contract)
    {
        if (contract.OriginalRequest != UserRequest
            || (contract.SourceId is null) != (contract.SourceRevision is null)
            || contract.SourceId is { } sourceId && !TargetReferences.Contains(new(sourceId, contract.SourceRevision!.Value))
            || contract.SourceSnapshot?.PromptId is { } savedId
                && !TargetReferences.Contains(new(savedId, contract.SourceSnapshot.Revision!.Value))
            || contract.SourceId is null && TargetReferences.Length > 0 && WorkingTarget is null
            || contract.SourceId is null && WorkingTarget is { } working && LiveRuns.Hash(contract.SourceText ?? "") != working.BodyHash)
            throw new ValidationException("La propuesta de autoría no pertenece al encargo y origen vigentes.");
        lock (assessmentLock)
        {
            if (authoringFingerprint is not null && authoringFingerprint != contract.Fingerprint)
                throw new ValidationException("El contexto de autoría no puede cambiar durante la misma tarea.");
            AuthoringContract = contract;
            authoringFingerprint = contract.Fingerprint;
        }
    }
    /// <summary>Checks the exact proposed body receipt; semantic acceptance still requires explicit content review.</summary>
    public bool HasAuthoringStructure(string body) => HasCurrentAuthoringContract && AuthoringContract!.MatchesProposal(body);
    [JsonIgnore] internal bool HasCurrentAuthoringContract => AuthoringContract is { } contract
        && authoringFingerprint == contract.Fingerprint;
    // Separate provenance: this is a confirmed proposal, not a model assessment.
    internal void RecordHumanContentApproval(PromptInput draft)
    {
        lock (assessmentLock)
        {
            humanApprovedBody = AssessmentHash(draft.TemplateText);
            humanApprovedDraft = ContentApprovalFingerprint(draft);
        }
    }
    public bool HasHumanContentApproval(string body) { lock (assessmentLock) return humanApprovedBody == AssessmentHash(body); }
    public bool HasHumanContentApproval(PromptInput draft) { lock (assessmentLock) return humanApprovedDraft == ContentApprovalFingerprint(draft); }
    internal string ContentApprovalFingerprint(PromptInput draft) => LiveRuns.Hash(new { authority = AssessmentHash(draft.TemplateText), draft });
    // An assessment cannot grant effects or declare completion. The exact body is
    // bound to this process-owned goal; native structure/receipts remain independent.
    private string AssessmentHash(string body)=>LiveRuns.Hash(new {GoalId,UserRequest,TargetReferences,WorkingTarget,Constraints,AuthoringDecision,Clauses,InputContract,ReferenceCoverage=ReferenceCoverage?.Fingerprint,AuthoringContract=AuthoringContract?.Fingerprint,TitleInterpreted,RequestedTitle,body});
    public void RecordContentAssessment(string body)
    {
        if (AuthoringContract is not null)
            throw new ValidationException("El contrato verifica procedencia y estructura; el modelo no certifica el contenido de esta tarea.");
        ReferenceCoverage?.ValidateBody(body);
        var hash=AssessmentHash(body);
        lock(assessmentLock) {
            if(assessedBodies.Contains(hash))return;
            while(assessedBodies.Count>=4)assessedBodies.Dequeue();
            assessedBodies.Enqueue(hash);
        }
    }
    public bool HasContentAssessment(string body)
    {
        if (AuthoringContract is not null) return false;
        var hash=AssessmentHash(body);lock(assessmentLock)return assessedBodies.Contains(hash);
    }
    [JsonIgnore] public string ContentRequest {
        get {
            // A workflow planner may authorize authoring without interpreting its
            // future content. In that mode the content owner receives the full
            // literal work order, never a planner-generated brief.
            if (Clauses.Any(clause => clause.Role == TaskClauseRole.WORK_ORDER)) return UserRequest;
            var quotes = Clauses.Where(c => c.Role == TaskClauseRole.CONTENT).Select(c => c.Quote).Distinct(StringComparer.Ordinal).ToArray();
            return string.Join("\n", quotes.Where(quote => !quotes.Any(other => other.Length > quote.Length && other.Contains(quote, StringComparison.Ordinal))));
        }
    }
    public static TaskGoal Create(string request, SemanticContext context, SemanticInterpretation plan, TaskClause[] clauses, SemanticOperation[]? requestedEffects = null)
    {
        // A question suspends execution, not the user's requested effects. Keep the actual
        // pending goal so a clarification can never be counted as task completion.
        var suspended=plan.DeferredGoals is {Length:>0};
        if (plan.DeferredGoals is { Length: > 0 } deferred) plan = plan with { Intentions = deferred, DesiredOutcome = plan.DeferredOutcome ?? plan.DesiredOutcome };
        if (clauses.Length > 20 || clauses.Any(c => !Enum.IsDefined(c.Role) || string.IsNullOrWhiteSpace(c.Quote) || !request.Contains(c.Quote, StringComparison.Ordinal)))
            throw new PreparationToolException("UNGROUNDED_TASK_CLAUSE", "semantic_goal", "Las cláusulas deben citar la petición autorizada.");
        if (clauses.Any(clause => clause.Role == TaskClauseRole.WORK_ORDER && (clause.Quote != request || clause.ContentChange is not null))
            || clauses.Any(clause => clause.Role == TaskClauseRole.WORK_ORDER) && clauses.Any(clause => clause.Role == TaskClauseRole.CONTENT))
            throw new PreparationToolException("INVALID_WORK_ORDER", "semantic_goal", "El encargo literal debe conservar la petición completa sin mezclarla con requisitos interpretados.");
        foreach (var content in clauses.Where(c => c.Role == TaskClauseRole.CONTENT))
            foreach (var other in clauses.Where(c => c.Role != TaskClauseRole.CONTENT))
                if (!suspended && content.ContentChange is null && (content.Quote.Contains(other.Quote, StringComparison.Ordinal) || other.Quote.Contains(content.Quote, StringComparison.Ordinal)))
                    throw new PreparationToolException("CONFLICTING_CLAUSE_ROLES", "semantic_goal", "El contenido y el workflow requieren ámbitos distintos.");
        var changes=clauses.Where(clause=>clause.Role==TaskClauseRole.CONTENT && clause.ContentChange is not null).Select(clause=>clause.ContentChange!).Distinct().ToArray();
        if(changes.Any(change=>string.IsNullOrWhiteSpace(change)||change.Length>1000)
            || changes.Length>0 && plan.RequestedTransformation!=string.Join("\n",changes))
            throw new PreparationToolException("INCONSISTENT_AUTHORING_DATA","semantic_goal","La instrucción de contenido debe coincidir con el contrato interpretado, sin conceder operaciones.");
        if ((plan.Adapts || plan.Creates) && !clauses.Any(c => c.Role is TaskClauseRole.CONTENT or TaskClauseRole.WORK_ORDER))
            throw new PreparationToolException("MISSING_CONTENT_AUTHORITY", "semantic_goal", "Falta el cambio de contenido solicitado.");
        // Only scoped content may become literal output constraints. Workflow prohibitions remain authority,
        // but are not required substrings in the user's reusable instruction.
        var contentRequest = string.Join("\n", clauses.Where(c => c.Role == TaskClauseRole.CONTENT).Select(c => c.Quote));
        if ((plan.Adapts || plan.Creates) && plan.Constraints.TargetTerms.Any(term => !contentRequest.Contains(term, StringComparison.OrdinalIgnoreCase)
            && context.Constraints?.TargetTerms?.Contains(term, StringComparer.OrdinalIgnoreCase) != true))
            throw new PreparationToolException("WORKFLOW_TARGET_AS_CONTENT", "semantic_goal", "Los objetivos del contenido deben proceder de sus cláusulas autorizadas.");
        if (plan.Constraints.LiteralClauses.Any(c => !contentRequest.Contains(c, StringComparison.Ordinal)
            && context.Constraints?.LiteralClauses.Contains(c, StringComparer.Ordinal) != true))
            throw new PreparationToolException("WORKFLOW_AS_CONTENT", "semantic_goal", "Una restricción del workflow no pertenece al prompt redactado.");
        var conditions = new List<CompletionCondition>();
        if ((plan.Has(SemanticOperation.RESOLVE_PROMPT) || plan.Adapts || plan.Has(SemanticOperation.SEARCH_PROMPT) || plan.Has(SemanticOperation.OPEN_PROMPT))
            && !plan.Deletes && (plan.DesiredOutcome != SemanticOutcome.ADVICE || plan.Has(SemanticOperation.OPEN_PROMPT)))
            conditions.Add(plan.Has(SemanticOperation.SEARCH_PROMPT) && plan.Constraints.ForbiddenOperations.Contains(SemanticOperation.OPEN_PROMPT) ? CompletionCondition.SEARCH_RESULTS_VISIBLE : CompletionCondition.SOURCE_VISIBLE);
        if (plan.Has(SemanticOperation.EXPLAIN)) conditions.Add(CompletionCondition.EXPLANATION_READY);
        if (plan.Adapts) conditions.Add(CompletionCondition.DRAFT_CHANGED);
        if (plan.Creates) conditions.Add(CompletionCondition.NEW_DRAFT_VISIBLE);
        if (plan.Adapts || plan.Creates) conditions.Add(CompletionCondition.CONTENT_VALID);
        if (plan.RequiresCompiledOutput)
            conditions.Add(CompletionCondition.COMPILED_VISIBLE);
        if (plan.RequiresTemplatePreview) conditions.Add(CompletionCondition.TEMPLATE_VISIBLE);
        if (plan.Has(SemanticOperation.FILL_VARIABLES) && (requestedEffects is null || requestedEffects.Contains(SemanticOperation.FILL_VARIABLES))) conditions.Add(CompletionCondition.VALUES_APPLIED);
        if (plan.Has(SemanticOperation.PREPARE_PROVIDER) || requestedEffects?.Contains(SemanticOperation.PREPARE_LOCAL_EXECUTION) == true) conditions.Add(CompletionCondition.DESTINATION_READY);
        if (plan.Has(SemanticOperation.COPY_TO_CLIPBOARD)) conditions.Add(CompletionCondition.CLIPBOARD_WRITTEN);
        if (plan.Executes) conditions.Add(CompletionCondition.LOCAL_RESPONSE);
        var approvals = plan.Intentions.Where(op => op is SemanticOperation.REQUEST_SAVE_VERSION or SemanticOperation.REQUEST_SAVE_COPY or SemanticOperation.REQUEST_DELETE).ToArray();
        if (approvals.Any(op => op != SemanticOperation.REQUEST_DELETE)) conditions.Add(CompletionCondition.APPROVED_WRITE);
        if (plan.Deletes) conditions.Add(CompletionCondition.APPROVED_DELETION);
        var identities = context.Ids.Select(id => context.Candidates.FirstOrDefault(c => c.Id == id) ?? (context.Selected?.Id == id ? context.Selected : context.DraftSource)!)
            .Where(entity => entity.Id == plan.ResolvedTargetId).Select(entity => new GoalTarget(entity.Id, entity.Revision)).ToArray();
        var roots = requestedEffects ?? plan.Intentions.Where(SemanticPlanCompiler.Goals.Contains).ToArray();
        if (roots.Any(operation => !SemanticPlanCompiler.Goals.Contains(operation) || !plan.Has(operation)))
            throw new PreparationToolException("INVALID_REQUESTED_EFFECTS", "semantic_goal", "Los resultados solicitados deben pertenecer al plan compilado.");
        var denied = Enum.GetValues<SemanticOperation>().Except(plan.Intentions).Concat(plan.Constraints.ForbiddenOperations).Distinct().ToArray();
        return new(Guid.NewGuid(), request, LiveRuns.Hash(new { context.ExplicitTargetId, selected = context.Selected?.Id, selectedRevision = context.Selected?.Revision,
            draft = context.DraftSource?.Id, draftRevision = context.DraftSource?.Revision, candidates = context.Candidates.Select(c => new { c.Id, c.Revision }), context.Approval }),
            identities, plan.DesiredOutcome, plan.Intentions, denied, plan.Constraints, plan.MissingInformation, conditions.ToArray(), approvals, clauses) { RequestedEffects = roots, AuthoringDecision = plan.Authoring, CopyContent = plan.CopyContent };
    }

    public static TaskGoal FromIntent(string request, SemanticContext context, SemanticInterpretation plan,
        TaskClause[] clauses, TaskIntentContract intent)
    {
        if (intent.RequestHash != LiveRuns.Hash(request) || !intent.Allows(plan))
            throw new PreparationToolException("INTENT_PLAN_DIVERGED", "semantic_goal", "El plan no satisface el encargo fijado antes de compilar.");
        // Existing clause/content validation stays with its single domain owner.
        var goal = Create(request, context, plan, clauses, intent.RequestedEffects.ToArray());
        return goal with { IntentContract = intent, RequestedOutcome = intent.Outcome,
            RequiredEffects = intent.AllowedEffects.ToArray(), ForbiddenEffects = intent.ForbiddenEffects.ToArray(),
            CompletionConditions = intent.RequiredOutcomes.ToArray(), ApprovalRequirements = intent.ApprovalRequirements.ToArray(),
            RequestedEffects = intent.RequestedEffects.ToArray() };
    }

    // Bind the new authoring protocol after source refinement, before execution.
    // The declaration fixes text effects; constraints/values/workflow keep their owners.
    public static TaskGoal FromAuthoring(string request, SemanticContext context, SemanticInterpretation plan)
    {
        var authoring = plan.Authoring ?? throw new ValidationException("Falta la declaración de autoría.");
        if (authoring.ContinuesPendingTask && context.PendingPlan is null)
            throw new ValidationException("Falta la tarea pendiente vinculada al encargo.");
        authoring.ValidateFor(plan, request, context.PendingRequest);
        var goalRequest = authoring.ContinuesPendingTask ? context.PendingRequest + "\n" + request : request;
        var clauses = new List<TaskClause>();
        if (authoring.Kind != InstructionAuthorship.TEMPLATE_UNCHANGED) {
            clauses.Add(new(TaskClauseRole.CONTENT, authoring.Evidence) { ContentChange = authoring.ContentInstruction });
            foreach (var literal in plan.Constraints.LiteralClauses.Where(goalRequest.Contains))
                if (!clauses.Any(clause => clause.Quote.Contains(literal, StringComparison.Ordinal))) clauses.Add(new(TaskClauseRole.CONTENT, literal));
            foreach (var term in plan.Constraints.TargetTerms) {
                var index = goalRequest.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && !clauses.Any(clause => clause.Quote.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    clauses.Add(new(TaskClauseRole.CONTENT, goalRequest.Substring(index, term.Length)));
            }
        }
        return Create(goalRequest, context, plan, clauses.ToArray(),
            SemanticPlanCompiler.MinimalGoals((plan.DeferredGoals ?? plan.Intentions).Where(SemanticPlanCompiler.Goals.Contains).ToArray(), plan.TargetReference));
    }
    public TaskGoal Bind(PromptDocument source) => this with { TargetReferences = [new(source.Id, source.Revision)] };
    public TaskGoal BindWorking(PreparedDraft draft, int revision = 1) => this with {
        WorkingTarget = new(draft.Id, LiveRuns.Hash(draft.TemplateText), draft.TemplateText.Length, revision)
    };
}

public record GoalCompletion(bool Complete, string[] Missing);
public static class GoalCompletionValidator
{
    public static bool ClipboardMatches(UiVisibleState state, CopyContent copyContent = CopyContent.RENDERED) => (copyContent == CopyContent.TEMPLATE || state.CompiledReady) && state.Clipboard is { OperationId: var operation } receipt
        && operation != Guid.Empty && receipt.PromptId == state.PromptId && receipt.Revision == state.Revision
        && receipt.CopyContent == copyContent && (copyContent == CopyContent.TEMPLATE ? state.TemplateText : state.CompiledText) is { Length: > 0 } text && receipt.Text == text;
    // Completion is code-owned. A body-bound semantic assessment is only one
    // prerequisite; native observations, domain validation and receipts must also pass.
    public static GoalCompletion Validate(TaskGoal goal, SemanticInterpretation plan, UiVisibleState state,
        PromptDocument? original, PromptDocument? current, IReadOnlyCollection<string> executed,
        IReadOnlyDictionary<string, string> groundedValues, bool contentValid, bool effectCommitted, bool includePersistence = false, UiClipboardReceipt? completedClipboard = null,
        bool allowPendingContentReview = false)
    {
        var missing = new List<string>();
        if (goal.RequiredEffects.Contains(SemanticOperation.ASK_CLARIFICATION)) missing.Add("AWAITING_INFORMATION");
        if (!goal.AllowsPlan(plan)) missing.Add("GOAL_PLAN_DIVERGED");
        if (plan.IntentContract is { } intent && !ReferenceEquals(intent, goal.IntentContract)) missing.Add("INTENT_CONTRACT_REPLACED");
        if (executed.Any(tool => UiToolCatalog.IsRegistered(tool) && !SemanticPlanValidator.ToolAllowed(plan, tool)))
            missing.Add("UNREQUESTED_UI_EFFECT");
        if (goal.IntentContract?.RequestedEffects.Any(effect => effect is SemanticOperation.CANCEL or SemanticOperation.PAUSE or SemanticOperation.RESUME) == true)
            missing.Add("SESSION_CONTROL_RECEIPT_REQUIRED");
        if (goal.AuthoringDecision != plan.Authoring) missing.Add("AUTHORING_DECISION_CHANGED");
        if (state.Error is not null) missing.Add("UI_ERROR");
        if (goal.InputContract is { } inputs && !inputs.Matches(state.TemplateText ?? "")) missing.Add("REQUESTED_INPUTS_MISSING_OR_EXTRA");
        if (goal.TargetReferences is [var target] && (original?.Id != target.Id || original.Revision != target.Revision
            || !effectCommitted && (current?.Id != target.Id || current.Revision != target.Revision || current.TemplateText != original.TemplateText))) missing.Add("SOURCE_REVISION_CHANGED");
        if (effectCommitted && goal.ApprovalRequirements.Length == 0) missing.Add("UNREQUESTED_PERSISTENCE");
        // Do not let a replacement plan (or a copied goal with fewer conditions)
        // erase the outcomes fixed before planning.
        foreach (var condition in goal.EffectiveCompletionConditions) {
            var reached = condition switch {
                CompletionCondition.SOURCE_VISIBLE => goal.TargetReferences is [var source] && state.PromptId == source.Id && (state.Revision == source.Revision || effectCommitted && state.Revision == source.Revision + 1)
                    || goal.TargetReferences is [var basis] && original?.Id == basis.Id && original.Revision == basis.Revision && plan.Creates && executed.Contains("ui_prepare_creation")
                    || effectCommitted && goal.ApprovalRequirements.Contains(SemanticOperation.REQUEST_SAVE_COPY) && state.PromptId is not null && state.PromptId != original?.Id
                    || goal.TargetReferences.Length == 0 && state.Mode == "create" && state.TemplateText is { Length: > 0 },
                CompletionCondition.DRAFT_CHANGED => (state.DraftChanged || effectCommitted) && executed.Contains("ui_prepare_update") && state.TemplateText is { Length: > 0 }
                    && (goal.WorkingTarget is { } working ? LiveRuns.Hash(state.TemplateText) != working.BodyHash : state.TemplateText != original?.TemplateText)
                    && (!goal.Constraints.Shortening || state.TemplateText.Length < (goal.WorkingTarget?.CharacterCount ?? original?.TemplateText.Length)),
                CompletionCondition.NEW_DRAFT_VISIBLE => (state.Mode == "create" && state.PromptId is null && state.DraftChanged || effectCommitted && state.PromptId is not null) && state.Title is { Length: > 0 }
                    && state.TemplateText is { Length: > 0 } && executed.Contains("ui_prepare_creation"),
                CompletionCondition.CONTENT_VALID => contentValid && state.TemplateText is { } assessed
                    && (goal.HasHumanContentApproval(assessed) || goal.AuthoringContract is null && goal.HasContentAssessment(assessed)
                        || allowPendingContentReview && goal.HasAuthoringStructure(assessed)),
                CompletionCondition.TEMPLATE_VISIBLE => state.TemplateText is { Length: > 0 } && state.TemplatePreviewText == state.TemplateText && executed.Contains("ui_show_template"),
                CompletionCondition.CLIPBOARD_WRITTEN => executed.Contains(plan.RequiresTemplatePreview ? "ui_copy_template" : "ui_copy_compiled_prompt") && (ClipboardMatches(state, plan.CopyContent)
                    // A verified save changes visible identity/revision. The prior copied
                    // artifact remains evidence only for its exact unchanged compiled text.
                    || effectCommitted && goal.ApprovalRequirements.Length > 0 && completedClipboard is { OperationId: var copyId }
                        && copyId != Guid.Empty && state.CompiledReady && completedClipboard.Text == state.CompiledText),
                CompletionCondition.COMPILED_VISIBLE => state.CompiledReady && state.CompiledText is { Length: > 0 },
                CompletionCondition.VALUES_APPLIED => groundedValues.Count > 0 && groundedValues.All(v => state.VariableNames.Contains(v.Key)
                    && !string.IsNullOrWhiteSpace(v.Value) && state.Values.GetValueOrDefault(v.Key) == v.Value),
                CompletionCondition.DESTINATION_READY => state.ProviderOpen && (plan.ProviderId is null || state.ProviderId == plan.ProviderId),
                CompletionCondition.LOCAL_RESPONSE => state.ResponseComplete && state.Response is not null && executed.Contains("ui_execute_local"),
                CompletionCondition.APPROVED_WRITE => !includePersistence || effectCommitted,
                CompletionCondition.APPROVED_DELETION => effectCommitted && current is null && original is not null
                    && executed.Contains("ui_verify_deletion") && state.DeletionPromptId is null && state.PromptId != original.Id
                    && !state.Candidates.Any(candidate => candidate.Id == original.Id)
                    && (!plan.Has(SemanticOperation.OPEN_PROMPT) || executed.Contains("ui_open_prompt")),
                CompletionCondition.EXPLANATION_READY => executed.Contains("explain_advice"),
                CompletionCondition.SEARCH_RESULTS_VISIBLE => state.Mode == "discover" && goal.TargetReferences is [var found]
                    && state.Candidates.Any(candidate => candidate.Id == found.Id && candidate.Revision == found.Revision),
                _ => false
            };
            if (!reached) missing.Add(condition.ToString());
        }
        return new(missing.Count == 0, missing.ToArray());
    }
    public static GoalCompletion ValidateAdvice(TaskGoal goal, SemanticInterpretation plan, string answer, PromptDocument? source, PromptDocument? current)
    {
        var missing = new List<string>();
        if ((goal.IntentContract?.Outcome ?? goal.RequestedOutcome) != SemanticOutcome.ADVICE || !goal.EffectiveCompletionConditions.Contains(CompletionCondition.EXPLANATION_READY)) missing.Add("NOT_AN_ADVICE_GOAL");
        if (string.IsNullOrWhiteSpace(answer)) missing.Add("EXPLANATION_READY");
        if (goal.EffectiveCompletionConditions.Any(condition => condition != CompletionCondition.EXPLANATION_READY)) missing.Add("UI_EFFECTS_PENDING");
        if (!goal.AllowsPlan(plan)) missing.Add("GOAL_PLAN_DIVERGED");
        if (plan.IntentContract is { } intent && !ReferenceEquals(intent, goal.IntentContract)) missing.Add("INTENT_CONTRACT_REPLACED");
        if (goal.AuthoringDecision != plan.Authoring) missing.Add("AUTHORING_DECISION_CHANGED");
        if (goal.TargetReferences is [var target] && (source?.Id != target.Id || source.Revision != target.Revision || current?.Revision != target.Revision || current.TemplateText != source.TemplateText)) missing.Add("SOURCE_REVISION_CHANGED");
        if (plan.Has(SemanticOperation.RESOLVE_PROMPT) && source is null && goal.TargetReferences.Length > 0) missing.Add("SOURCE_NOT_RESOLVED");
        return new(missing.Count == 0, missing.ToArray());
    }
}

// Used by planning to decide whether pre-recall can feed a single coherent semantic decision.
public interface ITaskGoalInterpreter { bool TaskGoalsEnabled { get; } }
public interface ISemanticAdviceResponder
{
    Task<string> Explain(string request, PromptDocument? source, PreparedDraft? workingDraft, string model, CancellationToken ct);
}
