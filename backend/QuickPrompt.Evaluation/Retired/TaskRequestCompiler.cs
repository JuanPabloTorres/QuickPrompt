using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Nova.Evaluation.Retired;

// Compiler for the rejected decoder's diagnostic fixtures. Production compiles
// the user's typed LiveCommand and never accepts inferred operation evidence.
public static class TaskRequestCompiler
{
    public static SemanticInterpretation Compile(TaskExecutionRequest declaration, string message, SemanticContext context)
    {
        var roots = declaration.Effects.Select(effect => effect.Operation).ToArray();
        if (roots.Length is < 1 or > 8 || roots.Distinct().Count() != roots.Length || roots.Any(root => !SemanticPlanCompiler.Goals.Contains(root)))
            throw new ValidationException("Acciones fuera del contrato de producto.");
        // Session controls address the pending run, never a selected library
        // artifact. Irrelevant model source metadata cannot grant that identity.
        if (roots.All(root => root is SemanticOperation.CANCEL or SemanticOperation.PAUSE or SemanticOperation.RESUME))
            declaration = declaration with { TargetReference = TargetReference.NONE, TargetId = null, Query = "", ContinuesPendingTask = false };
        if (declaration.ContinuesPendingTask && !context.CanContinuePendingTask)
            throw new ValidationException("No hay una tarea pendiente vinculada.");
        // Host-owned default subject, shared with the existing context contract.
        // It supplies identity only for source-dependent tasks without a named
        // lookup. It never turns new creation or ambiguous results into selection.
        var subject = context.CurrentSubject();
        if (declaration.TargetReference == TargetReference.NONE && declaration.TargetId is null
            && string.IsNullOrWhiteSpace(declaration.Query) && subject.Unique
            && !roots.Any(root => root is SemanticOperation.CREATE_DRAFT or SemanticOperation.SEARCH_PROMPT)
            && roots.Any(root => root is SemanticOperation.OPEN_PROMPT or SemanticOperation.PROPOSE_ADAPTATION
                or SemanticOperation.FILL_VARIABLES or SemanticOperation.COPY_PROMPT or SemanticOperation.COPY_TO_CLIPBOARD
                or SemanticOperation.PREPARE_LOCAL_EXECUTION or SemanticOperation.EXECUTE_LOCAL or SemanticOperation.PREPARE_PROVIDER
                or SemanticOperation.REQUEST_SAVE_COPY or SemanticOperation.REQUEST_SAVE_VERSION or SemanticOperation.REQUEST_DELETE))
            declaration = declaration with { TargetReference = subject.Role,
                TargetId = subject.Role == TargetReference.CANDIDATE ? subject.Entity?.Id : null };
        var request = declaration.ContinuesPendingTask ? context.PendingRequest + "\n" + message : message;
        var userActions = declaration.UserActions ?? UserActionRequest.FromOperations(request, roots);
        if (userActions.RequestHash != LiveRuns.Hash(request)) throw new ValidationException("El encargo pertenece a otra petición.");
        // Read/control tasks have no input-writing port. Extra proposed case data
        // cannot be projected into their plan or applied to the form.
        if (!SemanticPlanCompiler.RequiresInputBinding(roots)) declaration = declaration with { Values = new() };
        var names = declaration.TargetReference switch {
            TargetReference.SELECTED => context.Selected?.InputNames ?? [],
            TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT => context.ActiveDraftInputNames ?? context.DraftSource?.InputNames ?? [],
            TargetReference.CANDIDATE or TargetReference.OTHER_CANDIDATE => context.Candidates.SingleOrDefault(candidate=>candidate.Id==declaration.TargetId)?.InputNames ?? [],
            _ => []
        };
        if (names.Length == 0) names = declaration.Values.Keys.ToArray();
        // Model metadata is not supplied case data. Project only names with an
        // actual original assignment; an absent label can never populate a form.
        // Named sources use provisional labels, then BindTarget validates the
        // actual source's names/revision instead of the incidental UI selection.
        var literalAssignments=GroundedVariableResolver.LiteralAssignments(message,names);
        declaration=declaration with {Values=SemanticPlanCompiler.RequiresInputBinding(roots)
            ? literalAssignments.ToDictionary(value=>value.Key,value=>declaration.Values.TryGetValue(value.Key,out var proposed) ? proposed : value.Value)
            : new()};
        var currentValues = declaration.Values.Select(value => new GroundedValue(value.Key, value.Value, value.Value)).ToArray();
        // Reading/control/content-only goals do not own form values. A binding
        // scope would incorrectly require every incidental labelled value and
        // could later pass those values to another consumer on source binding.
        var inputScope = SemanticPlanCompiler.RequiresInputBinding(roots)
            ? UserInputScope.Bind(message, null, null, names, currentValues) : null;
        // Reduce only existing domain dependencies after literal input validation.
        // An ungrounded redundant FILL cannot grant an extra effect; the actual
        // preparation still needs its own directive and real bound case values.
        if (declaration.Values.Count > 0 && !declaration.Forbidden.Any(effect => effect.Operation == SemanticOperation.FILL_VARIABLES)) {
            roots = SemanticPlanCompiler.MinimalGoals(roots, declaration.TargetReference, resolveProvidedInputBindings: true);
            declaration = declaration with { Effects = declaration.Effects.Where(effect => roots.Contains(effect.Operation)).ToArray() };
        }
        var directive = GroundedVariableResolver.InstructionDirective(message, names, inputScope);
        TaskOperationEvidence BindEvidence(TaskOperationEvidence effect) {
            if (SemanticPlanValidator.DirectEvidence(directive, effect.Evidence)) return effect;
            // A passage can include both directive and labelled data. Retain only
            // an unchanged, literal directive fragment inside the cited passage.
            // Data-only, invented and quoted-name citations still cannot authorize.
            if (!string.IsNullOrWhiteSpace(effect.Evidence) && message.Contains(effect.Evidence, StringComparison.Ordinal)) {
                var fragment = directive.Split('\n').Select(text => text.Trim()).Where(text => text.Length > 0
                    && effect.Evidence.Contains(text, StringComparison.Ordinal) && SemanticPlanValidator.DirectEvidence(directive, text))
                    .OrderByDescending(text => text.Length).FirstOrDefault();
                if (fragment is not null) return effect with { Evidence = fragment };
            }
            return effect;
        }
        declaration = declaration with { Effects = declaration.Effects.Select(BindEvidence).ToArray(), Forbidden = declaration.Forbidden.Select(BindEvidence).ToArray() };
        string Evidence(SemanticOperation operation) => declaration.Effects.FirstOrDefault(effect => effect.Operation == operation)?.Evidence ?? "";
        foreach (var effect in declaration.Effects.Concat(declaration.Forbidden)) {
            if (!SemanticPlanCompiler.Goals.Contains(effect.Operation)
                || effect.Operation != SemanticOperation.ASK_CLARIFICATION && !SemanticPlanValidator.DirectEvidence(directive, effect.Evidence)
                    && !(declaration.ContinuesPendingTask && context.PendingPlan!.Has(effect.Operation)
                        && SemanticPlanValidator.DirectEvidence(GroundedVariableResolver.InstructionDirective(context.PendingRequest!, names), effect.Evidence)))
                throw new ValidationException("La procedencia de una acción debe estar fuera de los datos de campos.");
        }
        // The model's search goal already has an exact, validated directive.
        // Reuse that literal query when its optional field was omitted; never
        // guess a title, truncate, infer another goal or change an explicit query.
        if (roots.Contains(SemanticOperation.SEARCH_PROMPT) && string.IsNullOrWhiteSpace(declaration.Query)
            && Evidence(SemanticOperation.SEARCH_PROMPT) is { Length: > 0 and <= 200 } searchDirective)
            declaration = declaration with { Query = searchDirective };
        Guid? id = declaration.TargetReference switch {
            TargetReference.SELECTED => context.Selected?.Id,
            TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT => context.DraftSource?.Id,
            TargetReference.CANDIDATE or TargetReference.OTHER_CANDIDATE => declaration.TargetId,
            _ => null
        };
        // A named lookup is resolved by the repository, never by this unused
        // model metadata. Only candidate selection may supply an identity.
        if (declaration.TargetReference is not (TargetReference.NAMED or TargetReference.CANDIDATE or TargetReference.OTHER_CANDIDATE) && declaration.TargetId is not null && declaration.TargetId != id)
            throw new ValidationException("Core no puede atribuir una identidad de origen por su cuenta.");
        // An offered lookup without identifying data is not a usable source.
        // Reuse the domain compiler's missing-reference suspension instead of
        // searching unboundedly, choosing a nearby record or failing the task.
        if (declaration.TargetReference == TargetReference.NAMED && string.IsNullOrWhiteSpace(declaration.Query))
            declaration=declaration with {TargetReference=TargetReference.NONE,TargetId=null};
        var queryNames = context.CurrentInputNames().Concat(context.Candidates.Concat(context.CatalogCandidates).SelectMany(c => c.InputNames ?? [])).Distinct().ToArray();
        if (declaration.TargetReference == TargetReference.NAMED
            && !GroundedVariableResolver.InstructionDirective(request, queryNames).Contains(declaration.Query, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("La búsqueda debe proceder de la petición original fuera de los datos de campos.");
        var constraints = declaration.Constraints;
        var content = new SemanticConstraintInput(constraints.UserProhibitions, true, constraints.Language, constraints.Format,
            constraints.LiteralClauses, constraints.TargetTerms, constraints.Shortening, constraints.LanguageEvidence, constraints.FormatEvidence);
        content.ValidateEvidence(request, context);
        var denied = declaration.Forbidden.Select(effect => effect.Operation).Concat(new[] {
            SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.CREATE_DRAFT, SemanticOperation.EXECUTE_LOCAL,
            SemanticOperation.COPY_TO_CLIPBOARD, SemanticOperation.REQUEST_DELETE, SemanticOperation.REQUEST_SAVE_COPY,
            SemanticOperation.REQUEST_SAVE_VERSION }.Except(roots)).Distinct().ToArray();
        var outcome = roots.Contains(SemanticOperation.ASK_CLARIFICATION) ? SemanticOutcome.CLARIFICATION
            : roots.Contains(SemanticOperation.CANCEL) ? SemanticOutcome.CANCELLED : roots.Contains(SemanticOperation.PAUSE) ? SemanticOutcome.PAUSED
            : roots.Contains(SemanticOperation.RESUME) ? SemanticOutcome.RESUMED : roots.Contains(SemanticOperation.EXECUTE_LOCAL) ? SemanticOutcome.LOCAL_RESPONSE
            : roots.Contains(SemanticOperation.CREATE_DRAFT) ? SemanticOutcome.NEW_DRAFT : roots.Contains(SemanticOperation.PROPOSE_ADAPTATION) ? SemanticOutcome.DRAFT
            : roots.Contains(SemanticOperation.PREPARE_LOCAL_EXECUTION) ? SemanticOutcome.READY_LOCAL : roots.Contains(SemanticOperation.PREPARE_PROVIDER) ? SemanticOutcome.READY_PROVIDER
            : roots.Contains(SemanticOperation.EXPLAIN) ? SemanticOutcome.ADVICE : roots.Contains(SemanticOperation.REQUEST_DELETE) ? SemanticOutcome.REVIEW : SemanticOutcome.READ;
        var approval = roots.Any(root => root is SemanticOperation.REQUEST_SAVE_COPY or SemanticOperation.REQUEST_SAVE_VERSION or SemanticOperation.REQUEST_DELETE);
        var meaning = new SemanticMeaning(roots, declaration.TargetReference, declaration.Query, id, declaration.Query,
            declaration.ContentEvidence, new(denied, !roots.Contains(SemanticOperation.REQUEST_SAVE_VERSION), content.Language, content.Format,
                content.LiteralClauses, content.TargetTerms, content.Shortening, content.UserProhibitions), outcome,
            roots.Contains(SemanticOperation.ASK_CLARIFICATION) ? [declaration.Question] : [], approval, Evidence(SemanticOperation.EXECUTE_LOCAL), ProviderRegistry.DestinationFor(roots, declaration.ProviderId), declaration.Explanation) {
                ClipboardEvidence = Evidence(SemanticOperation.COPY_TO_CLIPBOARD), DeletionEvidence = Evidence(SemanticOperation.REQUEST_DELETE)
            };
        var intent = TaskIntentContract.Accept(userActions, meaning, declaration.Values.Count > 0);
        var plan = SemanticPlanCompiler.Compile(meaning, resolveProvidedInputBindings: declaration.Values.Count > 0);
        plan = plan with { IntentContract = intent, InputScope = inputScope, ContinuesPendingTask = declaration.ContinuesPendingTask };
        // Missing source suspends execution, not original content authority.
        if (roots.Any(root => root is SemanticOperation.PROPOSE_ADAPTATION or SemanticOperation.CREATE_DRAFT)) {
            if (!SemanticPlanValidator.DirectEvidence(directive, declaration.ContentEvidence)) throw new ValidationException("Falta el encargo literal de contenido.");
            // The generator owns future instruction content. Preserve the complete
            // work order instead of asking a planner to rewrite its requirements.
            plan = plan with { Goal = TaskGoal.FromIntent(request, context, plan, [new(TaskClauseRole.WORK_ORDER, request)], intent) };
        } else plan = plan with { Goal = TaskGoal.FromIntent(request, context, plan, declaration.Effects.Where(effect => effect.Evidence.Length > 0)
            .Select(effect => new TaskClause(TaskClauseRole.WORKFLOW, effect.Evidence)).ToArray(), intent) };
        SemanticPlanValidator.Validate(plan, context, message);
        return plan;
    }
}
