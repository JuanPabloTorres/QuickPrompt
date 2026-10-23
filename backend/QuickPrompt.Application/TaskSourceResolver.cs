using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

internal sealed record ReferencePreparation(SemanticContext Context, RetrievalCandidate[] Candidates, PromptDocument? Source, SemanticInterpretation? Question = null);

// Owns source reads, identity/revision checks and source-dependent refinement.
internal sealed class TaskSourceResolver(ISemanticIntentInterpreter interpreter, IPromptRetriever retriever, AgentSessions sessions, ICandidateSelector selector, PreparationOptions? options, bool directSourceRead, IInputScopeInterpreter? scopeInterpreter = null,
    ISourceReferenceResolver? referenceResolver = null, ISemanticClauseReviewer? clauseReviewer = null, IGroundedSourceSelector? groundedSelector = null)
{
    private readonly IInputScopeInterpreter? inputScopes = scopeInterpreter ?? interpreter as IInputScopeInterpreter;
    private readonly ISourceReferenceResolver? sourceReferences = referenceResolver ?? interpreter as ISourceReferenceResolver;
    private readonly ISemanticClauseReviewer? contentReview = clauseReviewer ?? interpreter as ISemanticClauseReviewer;
    private readonly IGroundedSourceSelector? contextualSelection = groundedSelector ?? selector as IGroundedSourceSelector;
    public async Task<ReferencePreparation?> PrepareReference(string request, SemanticContext context, string model, RetrievalFilter? filter, CancellationToken ct)
    {
        if (sourceReferences is null) return null;
        var reference = context.ExplicitTargetId is { } selected
            ? new SemanticReferenceChoice(TargetReference.SELECTED, selected, context.Selected?.Title ?? "", false, "")
            : await sourceReferences.ResolveSourceReference(request, context, model, ct);
        if (reference is null) return null;
        ct.ThrowIfCancellationRequested();
        var seed = SemanticPlanCompiler.Compile(new([SemanticOperation.OPEN_PROMPT], reference.TargetReference, reference.Query,
            reference.ResolvedTargetId, reference.Query, "", new([], true, null, null, [], [], false), SemanticOutcome.READ, [], false, "", null, "Resolución de origen; no autoriza efectos."));
        if (reference.NeedsClarification)
            return new(context, [], null, TaskClarification.Apply(seed, reference.Clarification, "UNRESOLVED_SOURCE_ROLE"));
        if (reference.TargetReference != TargetReference.NAMED)
            return new(context with { ResolvedReference = ResolvedTaskReference.Bind(request, reference, context) }, [], null);

        SourceMatch match;
        if (reference.ProposedCatalogSource is { } proposedSource) {
            var offered = context.CatalogCandidates.SingleOrDefault(candidate => candidate.Id == proposedSource.Id
                && candidate.Revision == proposedSource.Revision && !candidate.Stale)
                ?? throw new ValidationException("La propuesta no pertenece al catálogo ofrecido para esta petición.");
            var current = Read(offered.Id, offered.Revision);
            var recalled = retriever.Retrieve(current.Title, null, filter);
            var sameTitle = recalled.Where(candidate => PromptSearch.Normalize(candidate.Prompt.Title) == PromptSearch.Normalize(current.Title)).ToArray();
            if (sameTitle.Length > 1) match = new(null, sameTitle, TargetReference.NAMED);
            else {
                if (sameTitle.Length != 1 || sameTitle[0].Prompt.Id != current.Id)
                    throw new ConflictException("El origen ya no pertenece al alcance vigente de la petición.");
                match = new(current.Id, [new(current, 3, "Identidad propuesta del catálogo y leída en su revisión vigente.")], TargetReference.NAMED);
            }
        } else match = await LookupSource(seed, request, context, [], model, filter, ct);
        if (match.Target is not { } id) {
            var question = match.Candidates.Length == 0
                ? $"No encontré una coincidencia para «{reference.Query}» entre los prompts disponibles. ¿Quieres revisar el nombre o describirlo de otra forma?"
                : "¿Cuál quieres usar: " + string.Join(", ", match.Candidates.Select(candidate => candidate.Prompt.Title)) + "?";
            return new(context, match.Candidates, null, TaskClarification.Apply(seed, question, match.Candidates.Length == 0 ? "NO_MATCHING_SOURCE" : "UNRESOLVED_SOURCE"));
        }
        var retrieved = match.Candidates.Single(candidate => candidate.Prompt.Id == id).Prompt;
        var source = Read(id, retrieved.Revision);
        if (reference.ResolvedTargetId is { } proposed && proposed != id) throw new ValidationException("La referencia contradice el origen vigente.");
        var entity = SemanticEntity.From(source);
        var loaded = context with { ResolvedSource = entity,
            Candidates = context.Candidates.Where(candidate => candidate.Id != id).Prepend(entity).Take(6).ToArray() };
        loaded = loaded with { ResolvedReference = ResolvedTaskReference.Bind(request, reference with { ResolvedTargetId = id, ProposedCatalogSource = null }, loaded) };
        return new(loaded, match.Candidates, source);
    }
    public SemanticEntity Snapshot(PromptDocument prompt) {
        var stale = false;
        try { Read(prompt.Id, prompt.Revision); } catch (Exception error) when (error is MissingPromptException or ConflictException or ValidationException) { stale = true; }
        return SemanticEntity.From(prompt) with { Stale = stale };
    }
    public PromptDocument Read(Guid id, int? revision)
    {
        var prompt = directSourceRead ? retriever.ReadSource(id) : retriever.Retrieve("", id).Single(candidate => candidate.Prompt.Id == id).Prompt;
        if (prompt.Revision != revision || prompt.IsArchived) throw new ConflictException("El contexto de origen está obsoleto. Vuelve a seleccionar el prompt.");
        return prompt;
    }
    public async Task<SemanticResolution> Resolve(SemanticResolution resolution, string message, Guid sessionId, string model, RetrievalFilter? filter, CancellationToken ct)
    {
        using var measurement = NovaTelemetry.Measure("planning.resolve");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct); limit.CancelAfter(TimeSpan.FromSeconds(options?.StageTimeoutSeconds ?? 90)); ct = limit.Token;
        var plan = resolution.Interpretation; var context = resolution.Context;
        // A suspended goal owns no executable effects. Its reference and deferred
        // effects are retained for the next user turn, not a fresh selection now.
        if (plan.DesiredOutcome == SemanticOutcome.CLARIFICATION && plan.DeferredGoals is { Length: > 0 }
            && plan.Intentions is [SemanticOperation.ASK_CLARIFICATION]) {
            ct.ThrowIfCancellationRequested();
            return resolution;
        }
        var sourceLookupRequested = plan.Has(SemanticOperation.SEARCH_PROMPT) || plan.TargetReference == TargetReference.NAMED;
        PromptDocument? source = null; RetrievalCandidate[] candidates = [];
        var recallAmbiguous = false; var requireSourceSelection = false; var sourceSelectionCompleted = false;
        Guid? target = plan.TargetReference switch {
            TargetReference.SELECTED => context.Selected?.Id,
            TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT => resolution.ActiveDraft is not null ? resolution.ActiveDraft.SourceId : context.DraftSource?.Id ?? sessions.Get(sessionId)?.Source?.Id,
            TargetReference.CANDIDATE => plan.ResolvedTargetId,
            TargetReference.OTHER_CANDIDATE => context.Candidates.Where(c => c.Id != (context.DraftSource?.Id ?? context.Selected?.Id)).Select(c => (Guid?)c.Id).ToArray() is { Length: 1 } others ? others[0] : null,
            _ => null };
        if (context.ResolvedReference is { } binding && context.ResolvedSource is { } loaded) {
            var reference = binding.Validate(message, context);
            if (reference.ResolvedTargetId != loaded.Id || plan.ResolvedTargetId != loaded.Id)
                throw new ValidationException("El plan cambió el origen ligado a esta petición.");
            source = Read(loaded.Id, loaded.Revision); target = loaded.Id;
            candidates = [new(source, 3, "Origen leído antes de interpretar la tarea.")];
        }
        if (plan.Goal is not null && plan.ResolvedTargetId is { } groundedTarget && context.Ids.Contains(groundedTarget)) target = groundedTarget;
        if (plan.Goal is not null && target is not null && (plan.TargetReference is TargetReference.CANDIDATE or TargetReference.NAMED)
            && !(plan.ContinuesPendingTask && context.ExplicitTargetId == target)) {
            // This ID is already bound to an offered context entity. Recall scores
            // cannot overturn that binding. Actual duplicate literal names still
            // require a choice, and a contradictory title remains invalid.
            var recalled = retriever.Retrieve(message, null, filter);
            var titles = recalled.Where(c => message.Contains(c.Prompt.Title, StringComparison.OrdinalIgnoreCase)).ToArray();
            var longest = titles.Length == 0 ? 0 : titles.Max(c => c.Prompt.Title.Length);
            var exact = titles.Where(c => c.Prompt.Title.Length == longest).ToArray();
            if (exact.Length > 1) {
                plan = TaskClarification.Apply(plan, "Hay varios orígenes compatibles. ¿Cuál quieres utilizar?", "AMBIGUOUS_SOURCE"); target = null; candidates = exact;
            }
            if (exact.Length == 1 && exact[0].Prompt.Id != target) throw new ValidationException("La referencia contradice el título identificado por el usuario.");
        }
        if (context.ExplicitTargetId is { } explicitId && (plan.Goal is null || plan.TargetReference == TargetReference.SELECTED)) {
            source = Read(explicitId, context.Selected?.Revision); target = explicitId;
            candidates = [new(source, 10, "Selección explícita de la interfaz verificada contra el registro vigente.")];
        }
        // A contextual binding already identifies the authorized source. A search
        // goal must not replace it with a retrieval candidate or require its title
        // again. Source-free working drafts likewise remain session artifacts.
        else if (target is null
            && !(plan.TargetReference is TargetReference.ACTIVE_DRAFT or TargetReference.PREVIOUS_RESULT && resolution.ActiveDraft is not null)
            && (plan.TargetReference == TargetReference.NAMED || plan.Has(SemanticOperation.SEARCH_PROMPT)))
        {
            var match = await LookupSource(plan,message,context,resolution.Candidates,model,filter,ct);
            candidates=match.Candidates; target=match.Target; recallAmbiguous=candidates.Length>1; sourceSelectionCompleted=true;
            plan=plan with { TargetReference=match.Reference };
            // Retrieval similarity proves that a record exists, not that this is the
            // authorized source of a content change. Unbound named proposals require
            // a literal real title or an explicit candidate choice before generation.
            if (plan.Goal is null && target is { } contentSource && (plan.Adapts || plan.Creates)) {
                var title = candidates.Single(candidate => candidate.Prompt.Id == contentSource).Prompt.Title;
                if (!message.Contains(title, StringComparison.OrdinalIgnoreCase)
                    && context.PendingRequest?.Contains(title, StringComparison.OrdinalIgnoreCase) != true) { target = null; requireSourceSelection = true; }
            }
        }
        if (target is { } id)
        {
            var snapshot = plan.TargetReference switch { TargetReference.SELECTED => context.Selected,
                TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT => context.DraftSource,
                _ => context.Candidates.FirstOrDefault(c => c.Id == id) ?? (context.Selected?.Id == id ? context.Selected : context.DraftSource) };
            var retrieved = candidates.SingleOrDefault(c => c.Prompt.Id == id)?.Prompt;
            source = Read(id, retrieved?.Revision ?? snapshot?.Revision);
            if (plan.ResolvedTargetId is { } proposed && proposed != id) throw new ValidationException("La referencia propuesta contradice el destino resuelto.");
            plan = plan with { ResolvedTargetId = id, Goal = plan.Goal?.Bind(source) };
            if (plan.InputScope is { } scope) plan = plan with { InputScope = scope.BindTarget(source.Id, source.Revision, source.VariableNames) };
            if (plan.Goal is not null && plan.Executes && (plan.InputScope is { } actualScope
                ? !SemanticPlanValidator.DirectEvidence(GroundedVariableResolver.InstructionDirective(message, source.VariableNames, actualScope), plan.ExecutionEvidence)
                : GroundedVariableResolver.InputRanges(message, source.VariableNames)
                    .Any(range => message[range.LabelStart..range.End].Contains(plan.ExecutionEvidence, StringComparison.Ordinal))))
                throw new PreparationToolException("INPUT_DATA_IS_NOT_EXECUTION_AUTHORITY", "resolve_prompt", "Los datos de un campo no autorizan ejecutar su contenido.");
            if (!candidates.Any(c => c.Prompt.Id == id)) candidates = [new(source, 10, "Referencia de sesión verificada.")];
        }
        if (context.ResolvedSource is null && plan.Goal is null && source is not null && (plan.ValidationNotes?.Contains("CONTENT_CONSTRAINTS_REQUIRE_SOURCE") == true
            || plan.TargetReference == TargetReference.NAMED && context.ExplicitTargetId is null)
            && plan.ValidationNotes?.Contains("SOURCE_CONTEXT_REINTERPRETED") != true) {
            // A named basis was interpreted before its body/slots were available. Resolve
            // content-vs-input roles against that actual revision before permitting effects.
            // This is a system-owned source binding, not an invented ID or user approval.
            var originalQuery = plan.Query;
            var rebound = context with { Selected = SemanticEntity.From(source), ExplicitTargetId = source.Id,
                Candidates = context.Candidates.Where(candidate => candidate.Id != source.Id).Prepend(SemanticEntity.From(source)).Take(6).ToArray(),
                DraftSource = null, ActiveDraft = null, ActiveDraftInputNames = null, ActiveDraftValues = null,
                AuthorizedValues = context.Selected?.Id == source.Id && context.Selected.Revision == source.Revision ? context.AuthorizedValues : null,
                PendingPlan = plan.ContinuesPendingTask ? context.PendingPlan : null, PendingRequest = plan.ContinuesPendingTask ? context.PendingRequest : null,
                RefinementWorkflowGoals = plan.WorkflowGoals, RefinementAuthoring = plan.Authoring };
            if (inputScopes is { } boundScope)
                rebound = rebound with { InputScope = await boundScope.ResolveInputScope(message, rebound, model, ct) };
            var refined = await interpreter.Interpret(message, rebound, model, ct);
            if (rebound.InputScope is not null) refined = refined with { InputScope = rebound.InputScope };
            SemanticPlanValidator.Validate(refined, rebound with { ExplicitTargetId = context.ExplicitTargetId }, message);
            source = Read(source.Id, source.Revision); // Long inference must not reuse a changed revision.
            var terminal = refined.Has(SemanticOperation.ASK_CLARIFICATION) || refined.Has(SemanticOperation.CANCEL)
                || refined.Has(SemanticOperation.PAUSE) || refined.Has(SemanticOperation.RESUME) || refined.Has(SemanticOperation.EXPLAIN);
            if (!terminal) {
                if (refined.ResolvedTargetId is { } different && different != source.Id || refined.TargetReference == TargetReference.NONE) throw new ValidationException("La revisión contextual intentó cambiar el origen resuelto.");
                var goals = refined.Intentions.Where(SemanticPlanCompiler.Goals.Contains).ToArray();
                if (sourceLookupRequested && !goals.Contains(SemanticOperation.SEARCH_PROMPT)) goals = [SemanticOperation.SEARCH_PROMPT, ..goals];
                var bound = SemanticPlanCompiler.Compile(new(goals, TargetReference.SELECTED, source.Title, source.Id, originalQuery,
                    refined.RequestedTransformation, refined.Constraints, refined.DesiredOutcome, refined.MissingInformation,
                    refined.RequiresConfirmation, refined.ExecutionEvidence, refined.ProviderId, refined.Explanation) { DeletionEvidence = refined.DeletionEvidence, ClipboardEvidence = refined.ClipboardEvidence });
                refined = bound with { ContinuesPendingTask = refined.ContinuesPendingTask, ValidationNotes = refined.ValidationNotes, WorkflowGoals = refined.WorkflowGoals,
                    InputScope = refined.InputScope, Authoring = refined.Authoring };
            }
            plan = refined with { ValidationNotes = [..refined.ValidationNotes ?? [], "SOURCE_CONTEXT_REINTERPRETED"] };
            context = rebound with { ExplicitTargetId = context.ExplicitTargetId, RefinementWorkflowGoals = null, RefinementAuthoring = null };
        }
        if (source is not null && plan.ValidationNotes?.Contains("CONTENT_CONSTRAINTS_REQUIRE_SOURCE") == true)
            throw new PreparationToolException("SOURCE_CONTENT_CONTEXT_REQUIRED", "resolve_prompt", "Las restricciones de contenido siguen pendientes de revisar con el origen vigente. No se aplicó ningún cambio.");
        var usesWorkingBody = plan.TargetReference is TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT
            && resolution.ActiveDraft is not null && resolution.ActiveDraft.SourceId == source?.Id;
        var actualContext = context with { Selected = source is null ? null : SemanticEntity.From(source), DraftSource = null, PendingPlan = plan,
            ActiveDraft = usesWorkingBody ? resolution.ActiveDraft!.TemplateText[..Math.Min(2500, resolution.ActiveDraft.TemplateText.Length)] : null };
        var literalTask = plan.ContinuesPendingTask && context.PendingRequest is { } earlier && earlier != message ? earlier + "\n" + message : message;
        // Goal-mode terms already cite only authorized CONTENT, disjoint from source
        // references and workflow. Reclassifying them after a read can erase a user
        // requirement (the legacy reviewer accepts only technical identifiers).
        if (plan.Goal is null && (plan.Adapts || plan.Creates) && plan.Constraints.TargetTerms.Length > 0 && (source is not null || usesWorkingBody)
            && contentReview is { } targetReviewer) {
            var targets = await targetReviewer.ReviewTargets(literalTask, plan.Constraints.TargetTerms, actualContext, model, ct);
            if (targets.Distinct(StringComparer.Ordinal).Count() != targets.Length
                || targets.Any(term => !plan.Constraints.TargetTerms.Contains(term, StringComparer.Ordinal)))
                throw new PreparationToolException("INVALID_CONTENT_TARGETS", "resolve_prompt", "La revisión no puede inventar identificadores de contenido.");
            var boundConstraints = plan.Constraints with { TargetTerms = targets };
            plan = plan with { Constraints = boundConstraints, Goal = plan.Goal is { } goal ? goal with { Constraints = boundConstraints } : null };
        }
        if (plan.Goal is null && (plan.Adapts || plan.Creates) && plan.Constraints.LiteralClauses.Length > 0 && (source is not null || usesWorkingBody)
            && plan.ValidationNotes?.Contains("CONTENT_CLAUSES_REVIEWED_WITH_SOURCE") != true
            && contentReview is { } reviewer) {
            var clauses = await reviewer.ReviewClauses(literalTask, plan.Constraints.LiteralClauses, actualContext, model, ct);
            if (clauses.Distinct(StringComparer.Ordinal).Count() != clauses.Length
                || clauses.Any(clause => !plan.Constraints.LiteralClauses.Contains(clause, StringComparer.Ordinal)))
                throw new PreparationToolException("INVALID_CONTENT_CLAUSES", "resolve_prompt", "La revisión de contenido no puede inventar cláusulas.");
            plan = plan with { Constraints = plan.Constraints with { LiteralClauses = clauses } };
        }
        var needsSource = plan.Has(SemanticOperation.RESOLVE_PROMPT);
        var noMatchingSource = sourceSelectionCompleted && candidates.Length == 0 && !string.IsNullOrWhiteSpace(plan.Query);
        if (needsSource && source is null && !(plan.TargetReference is TargetReference.ACTIVE_DRAFT or TargetReference.PREVIOUS_RESULT && resolution.ActiveDraft?.SourceId is null && resolution.ActiveDraft is not null))
            plan = TaskClarification.Apply(plan, noMatchingSource ? $"No encontré una coincidencia para «{plan.Query}» entre los prompts disponibles. ¿Quieres revisar el nombre o describirlo de otra forma?"
                : requireSourceSelection ? "¿Cuál es el prompt de origen? Elige un candidato o escribe su título." : recallAmbiguous || candidates.Count(c => c.Score >= 2) > 1 ? "¿Cuál quieres usar: " + string.Join(", ", candidates.Where(c => c.Score >= 2 || recallAmbiguous).Select(c => c.Prompt.Title)) + "?" : "¿A qué prompt te refieres? Selecciónalo o indica su título.", noMatchingSource ? "NO_MATCHING_SOURCE" : requireSourceSelection ? "UNVERIFIED_NAMED_CONTENT_SOURCE" : "UNRESOLVED_SOURCE");
        return resolution with { Interpretation = plan, Context = context, Candidates = candidates, Source = source };
    }
    private sealed record SourceMatch(Guid? Target, RetrievalCandidate[] Candidates, TargetReference Reference);
    private async Task<SourceMatch> LookupSource(SemanticInterpretation plan, string message, SemanticContext context, RetrievalCandidate[] providedCandidates, string model, RetrievalFilter? filter, CancellationToken ct)
    {
        Guid? target = null; var recallAmbiguous = false;
            var candidates = providedCandidates.Length > 0 ? providedCandidates : retriever.Retrieve(plan.Query, null, filter);
            if (plan.Goal is not null && contextualSelection is not null) {
                foreach (var contextual in new[] { context.Selected, context.DraftSource }.Where(entity => entity is { Stale: false }))
                    if (!candidates.Any(candidate => candidate.Prompt.Id == contextual!.Id)) candidates = [..candidates, new(Read(contextual!.Id, contextual.Revision), 0, "Origen real de la conversación.")];
            }
            // Query already describes the identified source, independently of the
            // requested case/output. Use the same scope for retrieval, matching
            // and literal-title checks; keep the original task for refinement.
            var sourceDescription = plan.Query;
            if (string.IsNullOrWhiteSpace(sourceDescription)) throw new ValidationException("La selección requiere una descripción del origen, no una tarea o un candidato por cercanía.");
            var literalTitles = candidates.Where(c => sourceDescription.Contains(c.Prompt.Title, StringComparison.OrdinalIgnoreCase)).ToArray();
            var longest = literalTitles.Length == 0 ? 0 : literalTitles.Max(c => c.Prompt.Title.Length);
            var exactEvidence = literalTitles.Where(c => c.Prompt.Title.Length == longest).ToArray();
            // A named semantic lookup may be a paraphrase or another language.
            // Literal recall scores cannot establish or refute semantic relevance.
            var inputNames = candidates.SelectMany(candidate => candidate.Prompt.VariableNames)
                .Concat(context.ActiveDraftInputNames ?? context.Selected?.InputNames ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var lookup = new SourceLookupContext(sourceDescription,
                GroundedVariableResolver.InstructionDirective(message, inputNames),
                context.PendingRequest is { } pending ? GroundedVariableResolver.InstructionDirective(pending, inputNames) : null);
            var selection = plan.Goal is not null && contextualSelection is { } grounded
                ? await grounded.SelectSource(message, candidates, context, model, ct)
                : new GroundedSourceChoice(TargetReference.NAMED, await selector.Select(lookup, candidates, model, ct));
            var choice = selection.Choice;
            if (selection.Reference is TargetReference.SELECTED or TargetReference.ACTIVE_DRAFT) {
                plan = plan with { TargetReference = selection.Reference };
            }
            if (choice.PromptIds.Distinct().Count() != choice.PromptIds.Length || choice.PromptIds.Any(id => !candidates.Any(c => c.Prompt.Id == id)))
                throw new ValidationException("La selección semántica contiene un ID ajeno a los candidatos reales.");
            var selectedIds = choice.PromptIds.ToHashSet();
            if (choice.PromptIds is [var selectedReferenceId]) {
                if (exactEvidence.Length > 0 && !exactEvidence.Any(candidate => candidate.Prompt.Id == selectedReferenceId))
                    throw new ValidationException("La selección semántica contradice el título identificado por el usuario.");
                // Identical real titles remain ambiguous even when a model picks
                // one ID. Never restore unrelated rows merely because scores tie.
                var title = PromptSearch.Normalize(candidates.Single(candidate => candidate.Prompt.Id == selectedReferenceId).Prompt.Title);
                selectedIds.UnionWith(candidates.Where(candidate => PromptSearch.Normalize(candidate.Prompt.Title) == title).Select(candidate => candidate.Prompt.Id));
            }
            recallAmbiguous = selectedIds.Count > 1;
            candidates = candidates.Where(candidate => selectedIds.Contains(candidate.Prompt.Id))
                .Select(candidate => candidate with { Score = 3, Reason = recallAmbiguous ? "Varios registros compatibles; hace falta una selección explícita." : choice.Reason }).ToArray();
            target = selectedIds.Count == 1 ? selectedIds.Single() : null;
        return new(target,candidates,plan.TargetReference);
    }
}
