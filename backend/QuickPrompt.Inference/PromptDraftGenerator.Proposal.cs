using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class PromptDraftGenerator
{
    private record DraftProduction(string Title, string Description, string TemplateText, int? LiteralReference);

    [AgentStage("Define reusable future task instructions, procedure, conditions and deliverable requirements from the user's request, selected source content and input placeholders, or ask a specific clarification when the future task is undefined.")]
    private async Task<PromptProposal> GenerateDraftProposal(PrepareInput input, RetrievalCandidate[] candidates,
        CancellationToken ct, Action<int, string, string?>? diagnostic)
    {
        var plan = input.Interpretation!;
        if (plan.Goal is null) throw new ValidationException("La autoría requiere el encargo completo fijado en su objetivo.");
        var goal = plan.Goal;
        var sourceId = input.SourcePromptId ?? plan.ResolvedTargetId;
        var source = sourceId is { } id ? candidates.SingleOrDefault(candidate => candidate.Prompt.Id == id)?.Prompt
            ?? throw new ValidationException("La fuente autorizada no está disponible.") : null;
        if (input.SourceRevision is { } revision && source?.Revision != revision)
            throw new ValidationException("La revisión de la fuente cambió antes de redactar.");
        var working = input.WorkingDraft;
        var request = goal.UserRequest;
        var fields = goal.InputContract ?? PromptInputContract.FromUserSelection(request,
            working?.TemplateText ?? source?.TemplateText ?? "", plan.Adapts, plan.UserDefinedInputs);
        fields.ValidateBasis(request, working?.TemplateText ?? source?.TemplateText ?? "");
        goal.SetInputContract(fields);
        if (!fields.Enforced)
            return new(PreparationDecision.ASK_CLARIFICATION, null, "", "", "", [], fields.Question, "INPUT_FIELDS_NEED_CONFIRMATION");
        var names = fields.Names.ToArray();
        AuthoringCaseRange[] caseRanges = [];
        if (input.InputScope is { } singleScope && names.Length > 0
            && new[] { input.Message, input.AuthorizedPendingRequest }.Where(text => text is not null).Distinct(StringComparer.Ordinal)
                .Any(text => !singleScope.Matches(text!) && GroundedVariableResolver.LiteralAssignments(text!, names).Count > 0))
            return new(PreparationDecision.ASK_CLARIFICATION, null, "", "", "", [],
                "Hay valores en más de un mensaje y sólo uno tiene un ámbito de datos ligado. Separa los datos del caso de la instrucción reutilizable.", "AUTHORING_INPUT_SCOPE_NEEDS_CONFIRMATION");
        if (input.InputScope is null && names.Length > 0 && new[] { input.Message, input.AuthorizedPendingRequest, request }
            .Where(text => text is not null).Any(text => GroundedVariableResolver.LiteralAssignments(text!, names).Count > 0))
            return new(PreparationDecision.ASK_CLARIFICATION, null, "", "", "", [],
                "La petición incluye valores junto a campos de plantilla. Separa los datos del caso de la instrucción reutilizable para no guardarlos como contenido.", "AUTHORING_INPUT_SCOPE_NEEDS_CONFIRMATION");
        if (input.InputScope is { } dataScope) {
            dataScope.ValidateTarget(source?.Id, source?.Revision);
            var scopeRequest = dataScope.Matches(input.Message) ? input.Message
                : input.AuthorizedPendingRequest is { } earlier && dataScope.Matches(earlier) ? earlier : null;
            var offset = scopeRequest is null ? -1 : request == scopeRequest ? 0 : request.IndexOf(scopeRequest, StringComparison.Ordinal);
            if (scopeRequest is null || offset < 0 || request.IndexOf(scopeRequest, offset + 1, StringComparison.Ordinal) >= 0)
                return new(PreparationDecision.ASK_CLARIFICATION, null, "", "", "", [],
                    "No pude ligar los datos del caso a una única ocurrencia del encargo original. Separa la instrucción reutilizable de sus valores.", "AUTHORING_INPUT_SCOPE_NEEDS_CONFIRMATION");
            var current = dataScope.AuthoringRanges(scopeRequest, names);
            caseRanges = current.Select(range => range with { Start = range.Start + offset }).ToArray();
        }
        var literals = QuotedBodies(request, AuthoringUnits(request)).Select(item => new AuthoringLiteralCandidate(item.Id, item.CharacterStart, item.Text.Length, item.Text)).ToArray();
        var snapshot = source is null && working is null ? null : new DraftSourceSnapshot(source?.Id, source?.Revision,
            working?.TemplateText ?? source!.TemplateText, working?.Title ?? source!.Title,
            working?.Description ?? source!.Description, working?.CategoryId ?? source?.CategoryId ?? 0,
            source is null ? null : LiveRuns.Hash(source.TemplateText), working?.Id,
            working is null ? null : LiveRuns.Hash(working.TemplateText), source?.TemplateText);
        var context = new DraftProposalContract(request, snapshot, fields, caseRanges, literals, input.UserClarifications);
        if (input.ConfirmedBrief is { } confirmed) context.BindConfirmedBrief(confirmed);
        if (goal.AuthoringContract is { } previous && previous.Fingerprint != context.Fingerprint)
            throw new ValidationException("El contexto, origen o campos cambiaron durante la misma tarea.");
        var contract = goal.AuthoringContract ?? context;
        if (contract.ConfirmedBrief is { Review.Stage: AuthoringBriefs.ReviewContent }) goal.BindAuthoringContract(contract);
        if (contract.ConfirmedBrief is null)
            return new(PreparationDecision.ASK_CLARIFICATION, null, "", "", "", [],
                "Establece la tarea y los requisitos antes de que NOVA proponga una redacción. No se llamará al modelo hasta confirmarlos.",
                "AUTHORING_TASK_REQUIRES_ESTABLISHMENT") { Brief = AuthoringBriefs.Establish(contract,
                    goal.RequestedTitle ?? snapshot?.Title ?? "Nueva plantilla", input.AuthoringTaskDraft) };
        DraftProduction production;
        if (contract.ConfirmedBrief is { Review.Stage: AuthoringBriefs.ReviewContent } brief)
            production = new(brief.Content.Title, brief.Content.Description, brief.Render(), null);
        else try {
            // Both stages share the previous authoring deadline and at most two
            // native attempts. Neither retries a semantic omission or authorizes effects.
            using var authoringDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            authoringDeadline.CancelAfter(TimeSpan.FromSeconds(90));
            var authoringToken = authoringDeadline.Token;
            // Freeze both views before inference. Discovery retains provenance;
            // the writer revises only the fields the human established. Neither
            // view is truncated to fit the existing complete-packet budget.
            var material = ContentMaterial(input, contract, HasContentCorrection(input.ValidationFeedback));
            var established = contract.ConfirmedBrief!.Content;
            var discoveryPacket = JsonSerializer.Serialize(material, Json);
            var revisionMaterial = RevisionMaterial(material, established);
            var packet = JsonSerializer.Serialize(revisionMaterial, Json);
            if (Encoding.UTF8.GetByteCount(discoveryPacket) > 26000 || Encoding.UTF8.GetByteCount(packet) > 26000)
                throw new ValidationException("La petición y la fuente no caben completas en el presupuesto; no se truncarán.");
            var question = await DiscoverCurrentAuthorDecision(input, contract, discoveryPacket, authoringToken);
            if (question is not null)
                return new(PreparationDecision.ASK_CLARIFICATION, null, "", "", "", [], question, "FUTURE_TASK_NEEDS_CLARIFICATION");
            var schema = DefinitionSchema(revisionMaterial.LiteralBodyCandidates.Select(item => item.Id).ToArray(), revisionMaterial.Fields);
            using var response = await Chat(new { model = input.Model, stream = false, think = false, decoding = "native", format = schema,
                messages = new[] {
                    new { role = "system", content = FutureInstructionSystem },
                    new { role = "user", content = packet }
                }, options = new { temperature = 0, num_ctx = InferenceContextLength, num_predict = 3500, max_requests = 1 } }, authoringToken, nameof(GenerateDraftProposal), contentOnly: true);
            authoringToken.ThrowIfCancellationRequested();
            var definition = ParseContent<AuthoringDefinition>(response, schema);
            var suggestion = CompileDefinition(definition, established, contract);
            suggestion = suggestion with { Title = goal.RequestedTitle ?? (plan.Adapts ? snapshot?.Title : null) ?? suggestion.Title,
                Description = plan.Adapts ? snapshot?.Description ?? suggestion.Description : suggestion.Description };
            return new(PreparationDecision.ASK_CLARIFICATION, null, "", "", "", [],
                "El contrato establecido se conserva. Compara las mejoras de NOVA y aplica sólo las que quieras antes de confirmar el contenido final.",
                "AUTHORING_BRIEF_REQUIRES_CONFIRMATION") { Brief = AuthoringBriefs.Suggest(contract, suggestion) };
        } catch (LlmUnavailableException failure) { throw InferenceFailures.ToPreparation(failure, nameof(GenerateDraftProposal)); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw InferenceFailures.ToPreparation(new LlmUnavailableException("Authoring deadline exceeded.", "MODEL_TIMEOUT"), nameof(GenerateDraftProposal));
        }
        ct.ThrowIfCancellationRequested();
        var description = production.Description;
        var title = production.Title;
        PromptInput proposed;
        try { proposed = contract.RegisterProposal(new(title, production.TemplateText, description, snapshot?.CategoryId ?? 0), production.LiteralReference, goal.ReferenceCoverage); }
        catch (ValidationException failure) {
            failure.Data["NovaRejectedProposal"] = new { title, templateText = production.TemplateText, description };
            if (!failure.Data.Contains("NovaContractDetails"))
                failure.Data["NovaContractDetails"] = new { contract = "DRAFT_PROPOSAL/1", requiredInputNames = fields.Names };
            throw;
        }
        var body = proposed.TemplateText;
        if (plan.Adapts && (body == snapshot?.TemplateText
            || plan.Constraints.Shortening && snapshot is not null && body.Length >= snapshot.TemplateText.Length))
            return new(PreparationDecision.ASK_CLARIFICATION, null, "", "", "", [],
                "La propuesta no logra el cambio o la reducción solicitada. Concreta qué resultado debe producir la adaptación.", "AUTHORING_DELTA_NEEDS_CONFIRMATION");
        // This is a separate authorized data operation, never a content or permission judge.
        var hasCase = input.InputScope is not null || plan.Has(SemanticOperation.FILL_VARIABLES) || input.Values?.Keys.Any(names.Contains) == true;
        var extraction = names.Length == 0 || !hasCase ? new InputValueExtraction([], "") : await ExtractInputs(input, body, names, ct);
        ct.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(extraction.Question))
            return new(PreparationDecision.ASK_CLARIFICATION, null, "", "", "", [], extraction.Question, "INPUT_VALUES_NEED_CONFIRMATION");
        diagnostic?.Invoke(193, JsonSerializer.Serialize(new { contract = "confirmed-future-task/1", contractHash = contract.Fingerprint,
            bodyHash = LiveRuns.Hash(body), status = "HUMAN_CONTENT_REVIEW_REQUIRED" }, Json), null);
        return new(plan.Adapts && source is not null ? PreparationDecision.ADAPT_EXISTING : PreparationDecision.CREATE_NEW,
            plan.Adapts ? source?.Id : null, proposed.Title, proposed.Description, body, extraction.Values, "",
            "Propuesta de contenido pendiente de revisión humana; la IA no certificó su fidelidad.", TemplateCompiler.Names(body));
    }

    // Raw retry diagnostics are bounded but never supplied to the writer. A
    // correction repeats only current content requirements from trusted owners.
    private static bool HasContentCorrection(string? feedback)
    {
        if (string.IsNullOrWhiteSpace(feedback)) return false;
        JsonElement diagnostic;
        try { using var parsed = JsonDocument.Parse(feedback); diagnostic = parsed.RootElement.Clone(); }
        catch (JsonException) { diagnostic = JsonSerializer.SerializeToElement(new { message = feedback }, Json); }
        var wrapper = new { authority = "NONE", diagnostic };
        if (JsonSerializer.SerializeToUtf8Bytes(wrapper, Json).Length > 8192)
            throw new ValidationException("El diagnóstico de corrección excede 8 KiB; no se enviará incompleto al modelo.");
        return true;
    }
}
