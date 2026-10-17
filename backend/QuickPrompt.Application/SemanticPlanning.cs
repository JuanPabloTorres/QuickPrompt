using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record SemanticResolution(SemanticInterpretation Interpretation, SemanticContext Context, RetrievalCandidate[] Candidates, PromptDocument? Source, PreparedDraft? ActiveDraft);

// Shared by all natural-language entry points. No write capability; IDs and revisions come from the library.
public sealed class SemanticPlanning(ISemanticIntentInterpreter interpreter, IPromptRetriever retriever, AgentSessions sessions, ICandidateSelector selector, PreparationOptions? options = null)
{
    public SemanticContext Context(Guid sessionId, Guid? selectedId = null, int? revision = null)
    {
        var session = sessions.Get(sessionId);
        var selected = selectedId is { } id ? Read(id, revision) : session?.Source;
        var candidates = (session?.Candidates ?? []).Take(6).ToArray();
        var draft = session?.Draft;
        SemanticEntity? draftSource = null;
        if (draft?.SourceId is { } source) {
            var snapshot = draft.Candidates.SingleOrDefault(c => c.Prompt.Id == source)?.Prompt ?? session?.Candidates?.SingleOrDefault(p => p.Id == source)
                ?? (session?.Source?.Id == source && session.Source.Revision == draft.SourceRevision ? session.Source : null);
            if (snapshot is not null) draftSource = Snapshot(snapshot);
            else {
                try { draftSource = Snapshot(Read(source, draft.SourceRevision)); }
                catch (Exception error) when (error is MissingPromptException or ConflictException or ValidationException) { draftSource = new(source, draft.SourceRevision ?? 1, draft.Title, "", false, true); }
            }
        }
        return new(selected is null ? null : Snapshot(selected), candidates.Select(Snapshot).ToArray(), draftSource, draft?.TemplateText[..Math.Min(2500, draft.TemplateText.Length)],
            session?.LastProposal, session?.PendingAction, session?.Approval ?? "NONE", session?.PendingPlan?.Constraints ?? session?.LastProposal?.Constraints,
            session?.Source?.Id == selected?.Id && session?.Source?.Revision == selected?.Revision ? session?.Values : null, draft?.Values.ToDictionary(v => v.Name, v => v.Value), session?.PendingPlan, selectedId, session?.PendingRequest, draft?.Variables.Select(v => v.Name).ToArray());
    }
    private SemanticEntity Snapshot(PromptDocument prompt) {
        var stale = false;
        try { Read(prompt.Id, prompt.Revision); } catch (Exception error) when (error is MissingPromptException or ConflictException or ValidationException) { stale = true; }
        return SemanticEntity.From(prompt) with { Stale = stale };
    }
    private PromptDocument Read(Guid id, int? revision)
    {
        var prompt = retriever.Retrieve("", id).Single(c => c.Prompt.Id == id).Prompt;
        if (prompt.Revision != revision || prompt.IsArchived) throw new ConflictException("El contexto de origen está obsoleto. Vuelve a seleccionar el prompt.");
        return prompt;
    }
    public async Task<SemanticResolution> Interpret(string message, Guid sessionId, Guid? selectedId, int? revision, string model, RetrievalFilter? filter, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct); limit.CancelAfter(TimeSpan.FromSeconds(options?.StageTimeoutSeconds ?? 90));
        var context = Context(sessionId, selectedId, revision);
        // Selecting a displayed candidate resolves a typed UI question. The original
        // natural task was already interpreted; do not reinterpret its generated question.
        var pending = context.PendingPlan;
        var chosenCandidate = selectedId is { } chosen && context.Candidates.Any(candidate => candidate.Id == chosen && !candidate.Stale);
        var sourceTaskPending = pending?.ValidationNotes?.Any(note => note is "UNRESOLVED_SOURCE_ROLE" or "MISSING_TARGET_REFERENCE" or "UNRESOLVED_SOURCE" or "UNVERIFIED_NAMED_CONTENT_SOURCE") == true
            || pending?.DeferredGoals?.Any(goal => goal is SemanticOperation.SEARCH_PROMPT or SemanticOperation.OPEN_PROMPT or SemanticOperation.PROPOSE_ADAPTATION
                or SemanticOperation.FILL_VARIABLES or SemanticOperation.COPY_PROMPT or SemanticOperation.PREPARE_LOCAL_EXECUTION or SemanticOperation.PREPARE_PROVIDER or SemanticOperation.REQUEST_SAVE_VERSION) == true
            || pending?.TargetReference is not null and not TargetReference.NONE;
        var resumeSelection = chosenCandidate && pending?.DesiredOutcome == SemanticOutcome.CLARIFICATION
            && sourceTaskPending && !string.IsNullOrWhiteSpace(context.PendingRequest)
            && string.Equals(message, context.PendingRequest, StringComparison.Ordinal);
        var plan = resumeSelection ? null : await interpreter.Interpret(message, context, model, limit.Token);
        SemanticEntity? chosenSource = resumeSelection ? context.Selected : null;
        if (!resumeSelection && pending?.DesiredOutcome == SemanticOutcome.CLARIFICATION && sourceTaskPending && !string.IsNullOrWhiteSpace(context.PendingRequest)
            && !plan!.Has(SemanticOperation.CANCEL) && !plan.Has(SemanticOperation.PAUSE) && !plan.Has(SemanticOperation.RESUME)) {
            var candidates = (sessions.Get(sessionId)?.Candidates ?? []).Concat(retriever.Retrieve(message, null, filter).Select(candidate => candidate.Prompt));
            var exact = candidates.Where(candidate => !candidate.IsArchived && string.Equals(message.Trim(), candidate.Title, StringComparison.OrdinalIgnoreCase))
                .GroupBy(candidate => candidate.Id).Select(group => group.First()).ToArray();
            if (exact is { Length: 1 }) chosenSource = SemanticEntity.From(Read(exact[0].Id, exact[0].Revision));
        }
        if (chosenSource is not null && context.PendingRequest is { } literalTask) {
            context = context with { Selected = chosenSource, ExplicitTargetId = chosenSource.Id,
                Candidates = context.Candidates.Where(candidate => candidate.Id != chosenSource.Id).Prepend(chosenSource).Take(6).ToArray(),
                DraftSource = null, ActiveDraft = null, ActiveDraftValues = null, ActiveDraftInputNames = null,
                AuthorizedValues = context.Selected?.Id == chosenSource.Id && context.Selected.Revision == chosenSource.Revision ? context.AuthorizedValues : null };
            // Reinterpret the actual earlier user task with its now-loaded source, not a
            // provisional goal or generated summary from the missing-source question.
            plan = await interpreter.Interpret(literalTask, context with { PendingPlan = null, PendingRequest = null, Constraints = null }, model, limit.Token);
            if (plan.Executes) {
                var goals = plan.Intentions.Where(SemanticPlanCompiler.Goals.Contains).Where(goal => goal != SemanticOperation.EXECUTE_LOCAL).ToArray();
                var constraints = plan.Constraints with { ForbiddenOperations = plan.Constraints.ForbiddenOperations.Append(SemanticOperation.EXECUTE_LOCAL).Distinct().ToArray() };
                plan = SemanticPlanCompiler.Compile(new(goals, plan.TargetReference, plan.TargetDescription, plan.ResolvedTargetId, plan.Query,
                    plan.RequestedTransformation, constraints, SemanticOutcome.READY_LOCAL, plan.MissingInformation, plan.RequiresConfirmation, "", plan.ProviderId,
                    "Origen identificado. La ejecución final requiere una instrucción actual o su control explícito."));
            }
            plan = plan with { ContinuesPendingTask = true, ValidationNotes = [..plan.ValidationNotes ?? [], "PENDING_SOURCE_REINTERPRETED"] };
        }
        SemanticPlanValidator.Validate(plan!, context, message);
        return new(plan!, context, [], null, sessions.Get(sessionId)?.Draft);
    }
    public async Task<SemanticResolution> Resolve(SemanticResolution resolution, string message, Guid sessionId, string model, RetrievalFilter? filter, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct); limit.CancelAfter(TimeSpan.FromSeconds(options?.StageTimeoutSeconds ?? 90)); ct = limit.Token;
        var plan = resolution.Interpretation; var context = resolution.Context;
        var sourceLookupRequested = plan.Has(SemanticOperation.SEARCH_PROMPT) || plan.TargetReference == TargetReference.NAMED;
        PromptDocument? source = null; RetrievalCandidate[] candidates = [];
        var recallAmbiguous = false; var requireSourceSelection = false;
        Guid? target = plan.TargetReference switch {
            TargetReference.SELECTED => context.Selected?.Id,
            TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT => resolution.ActiveDraft is not null ? resolution.ActiveDraft.SourceId : context.DraftSource?.Id ?? sessions.Get(sessionId)?.Source?.Id,
            TargetReference.CANDIDATE => plan.ResolvedTargetId,
            TargetReference.OTHER_CANDIDATE => context.Candidates.Where(c => c.Id != (context.DraftSource?.Id ?? context.Selected?.Id)).Select(c => (Guid?)c.Id).ToArray() is { Length: 1 } others ? others[0] : null,
            _ => null };
        if (context.ExplicitTargetId is { } explicitId) {
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
            candidates = resolution.Candidates.Length > 0 ? resolution.Candidates : retriever.Retrieve(plan.Query, null, filter);
            var ranked = candidates.Where(c => c.Score >= 2).OrderByDescending(c => c.Score).ToArray();
            var literalTitles = candidates.Where(c => message.Contains(c.Prompt.Title, StringComparison.OrdinalIgnoreCase)).ToArray();
            var longest = literalTitles.Length == 0 ? 0 : literalTitles.Max(c => c.Prompt.Title.Length);
            var exactEvidence = literalTitles.Where(c => c.Prompt.Title.Length == longest).ToArray();
            recallAmbiguous = ranked.Length > 1 && ranked[1].Score >= ranked[0].Score * .85 && exactEvidence.Length != 1;
            var choice = await selector.Select(message, candidates, model, ct);
            if (choice.PromptIds.Length > 3 || choice.PromptIds.Distinct().Count() != choice.PromptIds.Length || choice.PromptIds.Any(id => !candidates.Any(c => c.Prompt.Id == id)))
                throw new ValidationException("La selección semántica contiene un ID ajeno a los candidatos reales.");
            candidates = candidates.Select(c => c with { Score = (recallAmbiguous ? ranked.Any(r => r.Prompt.Id == c.Prompt.Id) : choice.PromptIds.Contains(c.Prompt.Id)) ? 3 : 0,
                Reason = recallAmbiguous ? "Varios registros compatibles; hace falta una selección explícita." : choice.Reason }).OrderByDescending(c => c.Score).ToArray();
            target = choice.PromptIds.Length == 1 && !recallAmbiguous ? choice.PromptIds[0] : null;
            if (target is { } identified && exactEvidence.Length == 1 && identified != exactEvidence[0].Prompt.Id) throw new ValidationException("La selección semántica contradice el título identificado por el usuario.");
            // Retrieval similarity proves that a record exists, not that this is the
            // authorized source of a content change. Unbound named proposals require
            // a literal real title or an explicit candidate choice before generation.
            if (target is { } contentSource && (plan.Adapts || plan.Creates)) {
                var title = candidates.Single(candidate => candidate.Prompt.Id == contentSource).Prompt.Title;
                if (!message.Contains(title, StringComparison.OrdinalIgnoreCase)
                    && context.PendingRequest?.Contains(title, StringComparison.OrdinalIgnoreCase) != true) { target = null; requireSourceSelection = true; }
            }
            // A duplicate title cannot be resolved by an unexplained model preference.
            if (target is { } picked && candidates.Count(c => PromptSearch.Normalize(c.Prompt.Title) == PromptSearch.Normalize(candidates.Single(c => c.Prompt.Id == picked).Prompt.Title)) > 1) target = null;
        }
        if (target is { } id)
        {
            var snapshot = plan.TargetReference switch { TargetReference.SELECTED => context.Selected,
                TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT => context.DraftSource,
                _ => context.Candidates.FirstOrDefault(c => c.Id == id) ?? (context.Selected?.Id == id ? context.Selected : context.DraftSource) };
            source = candidates.SingleOrDefault(c => c.Prompt.Id == id)?.Prompt ?? Read(id, snapshot?.Revision);
            if (plan.ResolvedTargetId is { } proposed && proposed != id) throw new ValidationException("La referencia propuesta contradice el destino resuelto.");
            plan = plan with { ResolvedTargetId = id };
            if (!candidates.Any(c => c.Prompt.Id == id)) candidates = [new(source, 10, "Referencia de sesión verificada.")];
        }
        if (source is not null && plan.TargetReference == TargetReference.NAMED && context.ExplicitTargetId is null
            && plan.ValidationNotes?.Contains("SOURCE_CONTEXT_REINTERPRETED") != true) {
            // A named basis was interpreted before its body/slots were available. Resolve
            // content-vs-input roles against that actual revision before permitting effects.
            // This is a system-owned source binding, not an invented ID or user approval.
            var originalQuery = plan.Query;
            var rebound = context with { Selected = SemanticEntity.From(source), ExplicitTargetId = source.Id,
                Candidates = context.Candidates.Where(candidate => candidate.Id != source.Id).Prepend(SemanticEntity.From(source)).Take(6).ToArray(),
                DraftSource = null, ActiveDraft = null, ActiveDraftInputNames = null, ActiveDraftValues = null,
                AuthorizedValues = context.Selected?.Id == source.Id && context.Selected.Revision == source.Revision ? context.AuthorizedValues : null,
                PendingPlan = plan.ContinuesPendingTask ? context.PendingPlan : null, PendingRequest = plan.ContinuesPendingTask ? context.PendingRequest : null };
            var refined = await interpreter.Interpret(message, rebound, model, ct);
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
                    refined.RequiresConfirmation, refined.ExecutionEvidence, refined.ProviderId, refined.Explanation));
                refined = bound with { ContinuesPendingTask = refined.ContinuesPendingTask, ValidationNotes = refined.ValidationNotes };
            }
            plan = refined with { ValidationNotes = [..refined.ValidationNotes ?? [], "SOURCE_CONTEXT_REINTERPRETED"] };
            context = rebound with { ExplicitTargetId = context.ExplicitTargetId };
        }
        var usesWorkingBody = plan.TargetReference is TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT
            && resolution.ActiveDraft is not null && resolution.ActiveDraft.SourceId == source?.Id;
        var actualContext = context with { Selected = source is null ? null : SemanticEntity.From(source), DraftSource = null, PendingPlan = plan,
            ActiveDraft = usesWorkingBody ? resolution.ActiveDraft!.TemplateText[..Math.Min(2500, resolution.ActiveDraft.TemplateText.Length)] : null };
        var literalTask = plan.ContinuesPendingTask && context.PendingRequest is { } earlier && earlier != message ? earlier + "\n" + message : message;
        if ((plan.Adapts || plan.Creates) && plan.Constraints.TargetTerms.Length > 0 && (source is not null || usesWorkingBody)
            && interpreter is ISemanticClauseReviewer targetReviewer) {
            var targets = await targetReviewer.ReviewTargets(literalTask, plan.Constraints.TargetTerms, actualContext, model, ct);
            if (targets.Distinct(StringComparer.Ordinal).Count() != targets.Length
                || targets.Any(term => !plan.Constraints.TargetTerms.Contains(term, StringComparer.Ordinal)))
                throw new PreparationToolException("INVALID_CONTENT_TARGETS", "resolve_prompt", "La revisión no puede inventar identificadores de contenido.");
            plan = plan with { Constraints = plan.Constraints with { TargetTerms = targets } };
        }
        if ((plan.Adapts || plan.Creates) && plan.Constraints.LiteralClauses.Length > 0 && (source is not null || usesWorkingBody)
            && plan.ValidationNotes?.Contains("CONTENT_CLAUSES_REVIEWED_WITH_SOURCE") != true
            && interpreter is ISemanticClauseReviewer reviewer) {
            var clauses = await reviewer.ReviewClauses(literalTask, plan.Constraints.LiteralClauses, actualContext, model, ct);
            if (clauses.Distinct(StringComparer.Ordinal).Count() != clauses.Length
                || clauses.Any(clause => !plan.Constraints.LiteralClauses.Contains(clause, StringComparer.Ordinal)))
                throw new PreparationToolException("INVALID_CONTENT_CLAUSES", "resolve_prompt", "La revisión de contenido no puede inventar cláusulas.");
            plan = plan with { Constraints = plan.Constraints with { LiteralClauses = clauses } };
        }
        var needsSource = plan.Has(SemanticOperation.RESOLVE_PROMPT);
        if (needsSource && source is null && !(plan.TargetReference is TargetReference.ACTIVE_DRAFT or TargetReference.PREVIOUS_RESULT && resolution.ActiveDraft?.SourceId is null && resolution.ActiveDraft is not null))
            plan = Clarify(plan, requireSourceSelection ? "¿Cuál es el prompt de origen? Elige un candidato o escribe su título." : recallAmbiguous || candidates.Count(c => c.Score >= 2) > 1 ? "¿Cuál quieres usar: " + string.Join(", ", candidates.Where(c => c.Score >= 2 || recallAmbiguous).Take(3).Select(c => c.Prompt.Title)) + "?" : "¿A qué prompt te refieres? Selecciónalo o indica su título.", requireSourceSelection ? "UNVERIFIED_NAMED_CONTENT_SOURCE" : "UNRESOLVED_SOURCE");
        var previous = sessions.Get(sessionId);
        var sameSource = source is null || source.Id == previous?.Source?.Id && source.Revision == previous.Source.Revision;
        var selectedPreviousResult = context.ExplicitTargetId is { } chosen && previous?.Candidates?.Any(candidate => candidate.Id == chosen) == true
            || plan.ContinuesPendingTask && source is not null && previous?.Candidates?.Any(candidate => candidate.Id == source.Id) == true;
        var searchContext = sourceLookupRequested && !selectedPreviousResult
            ? candidates.Where(candidate => candidate.Score >= 2).Select(candidate => candidate.Prompt).Take(6).ToArray()
            : previous?.Candidates ?? [];
        if (source is not null) searchContext = searchContext.Select(candidate => candidate.Id == source.Id ? source : candidate).ToArray();
        sessions.Put(new(sessionId, source ?? previous?.Source, sameSource ? previous?.Draft : null, sameSource ? previous?.Values ?? [] : [], DateTime.UtcNow,
            searchContext, previous?.LastProposal, plan.ProposedSteps.FirstOrDefault()?.Operation, "NOT_GRANTED", source is null && previous?.SourceStale == true, plan, previous?.PendingRequest));
        return resolution with { Interpretation = plan, Context = context, Candidates = candidates, Source = source };
    }
    public static SemanticInterpretation Clarify(SemanticInterpretation plan, string question, string reason = "UNRESOLVED_SOURCE") => plan with {
        Intentions = [SemanticOperation.ASK_CLARIFICATION], ProposedSteps = [new(1, SemanticOperation.ASK_CLARIFICATION, [])],
        DesiredOutcome = SemanticOutcome.CLARIFICATION, MissingInformation = [question], ResolvedTargetId = null, ExecutionEvidence = "", ValidationNotes = [..plan.ValidationNotes ?? [], reason],
        DeferredGoals = plan.DeferredGoals ?? plan.Intentions.Where(SemanticPlanCompiler.Goals.Contains).ToArray(), DeferredOutcome = plan.DeferredOutcome ?? plan.DesiredOutcome };
}
