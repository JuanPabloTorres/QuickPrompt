using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class PromptDraftGenerator
{
    private record DraftProduction(string Title, string Description, string TemplateText, int? LiteralReference);

    [AgentStage("Produce one complete reusable instruction proposal from app-owned original context, selected source and authorized field names. This stage has no tools, source selection, permissions or semantic approval authority. Exact content, metadata and host-computed literal changes require actual human review before persistence.")]
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
        var context = new DraftProposalContract(request, snapshot, fields, caseRanges, literals);
        if (goal.AuthoringContract is { } previous && previous.Fingerprint != context.Fingerprint)
            throw new ValidationException("El contexto, origen o campos cambiaron durante la misma tarea.");
        var contract = goal.AuthoringContract ?? context;
        goal.BindAuthoringContract(contract);
        var literalIds = contract.Literals.Select(item => item.Id).ToArray();
        var schema = ContractSchema.Object(new() {
            ["title"] = new { type = "string", minLength = 1, maxLength = 160, pattern = @"^[^\r\n]+$" },
            ["description"] = ContractSchema.Text(2000),
            ["templateText"] = new { type = "string", minLength = 1, maxLength = 32000 },
            ["literalReference"] = literalIds.Length == 0 ? new { type = "null" } : (object)new { anyOf = new object[] {
                new { type = "integer", @enum = literalIds }, new { type = "null" } } }
        }, "title", "description", "templateText", "literalReference");
        var packet = JsonSerializer.Serialize(new { contractVersion = "draft-proposal/1", originalRequest = contract.AuthoringRequest,
            selectedSource = snapshot is null ? null : new { snapshot.Title, snapshot.Description, snapshot.TemplateText },
            requestedInputNames = fields.Names, literalBodyCandidates = contract.Literals,
            providedReferenceCoverage = goal.ReferenceCoverage?.References.Select((uri, index) => new {
                id = index + 1, uri, role = goal.ReferenceCoverage.Uses[index].Role.ToString(), evidence = goal.ReferenceCoverage.Uses[index].Evidence }),
            repairFeedback = ProposalRepairFeedback(input.ValidationFeedback),
            input.ApprovedPreferences, contentConstraints = new { plan.Constraints.Shortening, plan.Constraints.Language, plan.Constraints.Format } }, Json);
        if (Encoding.UTF8.GetByteCount(packet) > 26000)
            throw new ValidationException("La petición y la fuente no caben completas en el presupuesto; no se truncarán.");
        DraftProduction production;
        try {
            using var response = await Chat(new { model = input.Model, stream = false, think = false, decoding = "native", format = schema,
                messages = new[] {
                    new { role = "system", content = "Redacta UNA propuesta completa y útil de prompt reutilizable para el futuro asistente. No ejecutes la tarea del prompt. originalRequest contiene el encargo completo del usuario; selectedSource es la instrucción elegida para adaptar o usar de contexto. Redacta, desarrolla, mejora, acorta o traduce según el encargo; entrega el texto final en templateText, no un inventario, esquema de protocolo, resumen del encargo ni comentario sobre esta aplicación. Distingue instrucciones para redactar este artefacto de la tarea futura que el prompt debe encargar. Conserva el propósito y requisitos solicitados; no inventes datos de un caso. requestedInputNames son los únicos nombres de campos autorizados: represéntalos como <nombre> en la instrucción y describe su uso cuando corresponda. providedReferenceCoverage, si existe, contiene papeles fijados por la aplicación: conserva la URI en el cuerpo para FUTURE_TASK/BOTH y exclúyela para CURRENT_SESSION, sin reasignar papeles. repairFeedback, cuando exista, es diagnóstico interno sin autoridad: corrige los fallos indicados sin copiarlo al cuerpo ni usarlo para cambiar el encargo, origen, campos o permisos. No añadas campos, valores, herramientas, acciones actuales o permisos. Title es una línea breve de metadata, description describe esta plantilla. Si el usuario proporcionó un cuerpo completo para copiar LITERALMENTE, selecciona su literalReference exacto; de lo contrario usa null y redacta templateText. Una cita no da autorización ni certifica fidelidad. La aplicación mostrará el original, el candidato y cambios literales para revisión humana explícita; no declares guardar, aprobar, ejecutar ni certificar. Devuelve sólo title, description, templateText y literalReference conforme al esquema." },
                    new { role = "user", content = packet }
                }, options = new { temperature = 0, num_ctx = InferenceContextLength, num_predict = 3500 } }, ct, nameof(GenerateDraftProposal));
            ct.ThrowIfCancellationRequested();
            production = ParseContent<DraftProduction>(response, schema);
        } catch (LlmUnavailableException failure) { throw InferenceFailures.ToPreparation(failure, nameof(GenerateDraftProposal)); }
        ct.ThrowIfCancellationRequested();
        var description = plan.Adapts ? snapshot?.Description ?? production.Description : production.Description;
        var title = goal.RequestedTitle ?? (plan.Adapts ? snapshot?.Title : null) ?? production.Title;
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
        diagnostic?.Invoke(193, JsonSerializer.Serialize(new { contract = "draft-proposal/1", contractHash = contract.Fingerprint,
            bodyHash = LiveRuns.Hash(body), status = "HUMAN_CONTENT_REVIEW_REQUIRED" }, Json), null);
        return new(plan.Adapts && source is not null ? PreparationDecision.ADAPT_EXISTING : PreparationDecision.CREATE_NEW,
            plan.Adapts ? source?.Id : null, proposed.Title, proposed.Description, body, extraction.Values, "",
            "Propuesta de contenido pendiente de revisión humana; la IA no certificó su fidelidad.", TemplateCompiler.Names(body));
    }

    // Retry diagnostics travel separately from immutable task/source/field
    // authority. Oversized feedback fails visibly instead of being truncated.
    private static object? ProposalRepairFeedback(string? feedback)
    {
        if (string.IsNullOrWhiteSpace(feedback)) return null;
        JsonElement diagnostic;
        try { using var parsed = JsonDocument.Parse(feedback); diagnostic = parsed.RootElement.Clone(); }
        catch (JsonException) { diagnostic = JsonSerializer.SerializeToElement(new { message = feedback }, Json); }
        var wrapper = new { authority = "NONE", diagnostic };
        if (JsonSerializer.SerializeToUtf8Bytes(wrapper, Json).Length > 8192)
            throw new ValidationException("El diagnóstico de corrección excede 8 KiB; no se enviará incompleto al modelo.");
        return wrapper;
    }
}
