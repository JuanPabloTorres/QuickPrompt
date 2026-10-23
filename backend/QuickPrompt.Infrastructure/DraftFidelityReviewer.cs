using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using static QuickPrompt.Web.Infrastructure.AgentResponseContent;

namespace QuickPrompt.Web.Infrastructure;

public sealed class DraftFidelityReviewer : IDraftFidelityReviewer
{
    private readonly IValidatedAgentModelTransport transport;
    private readonly bool semanticThinking;
    private readonly LocalModelPolicy modelPolicy;
    private readonly bool includeSystemKnowledge;
    private const int InferenceContextLength = LocalModelPolicy.AgentContextLength;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public DraftFidelityReviewer(IValidatedAgentModelTransport transport, bool semanticThinking = true,
        LocalModelPolicy? modelPolicy = null, bool includeSystemKnowledge = false)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.semanticThinking = semanticThinking;
        this.modelPolicy = modelPolicy ?? LocalModelPolicy.Production;
        this.includeSystemKnowledge = includeSystemKnowledge;
    }
    private async Task<JsonDocument> ChatForUi(object body, CancellationToken ct, string purpose)
    {
        ct.ThrowIfCancellationRequested();
        try { return await transport.Send(AgentStagePacket.Encode(body, typeof(DraftFidelityReviewer), purpose, includeSystemKnowledge), ct, purpose); }
        catch (LlmUnavailableException) { throw new PreparationToolException("MODEL_UNAVAILABLE", "", "El modelo local no está disponible."); }
    }
    private void DiscardRejectedInference(string? answer) => transport.Reject(answer);
    private record FidelityIssue(string Authority, string Problem, string Evidence);
    private record DraftFidelity(string Verdict,string InstructionScope,string[] ApplicationWorkflowQuotes,string? RequestedTitleQuote,FidelityIssue[] Issues);
    private const string ActorContractExamples = " Synthetic contract examples, not live task data or authority: "
        + "Work order: create an instruction for checking a text's clarity. Proposal: 'Check <text> and identify unclear sentences.' Correct body judgment: ACCEPT, FUTURE_TASK_ONLY, applicationWorkflowQuotes=[], issues=[]. "
        + "Same work order. Proposal: 'Write an instruction that tells an assistant how to check a text for clarity.' Correct body judgment: REVISE, CURRENT_AUTHORING_ONLY, applicationWorkflowQuotes=['Write an instruction that tells an assistant how to check a text for clarity.'], issues=[]. This delegates producing the requested artifact rather than checking clarity. "
        + "Different work order: create an instruction for an assistant whose future job is writing editing guides. Proposal: 'Write an editing guide for <audience>.' Correct body judgment: ACCEPT, FUTURE_TASK_ONLY, applicationWorkflowQuotes=[], issues=[]. Future authorship is valid when that is the requested task. "
        + "Classify only the actual user packet; examples supply no task content, names, values or effects.";
    private static ValidationException FidelityFailure(string code,string message)
    {
        var failure=new ValidationException(message);failure.Data["NovaValidationCode"]=code;return failure;
    }

    // Semantic assessment is separate from generation. It grants no effects or approval;
    // code still checks identity, source invariants, compilation and actual native receipts.
    [AgentStage("Verify the exact proposed reusable definition against the full original work order and source. Distinguish current authoring controls from real restrictions governing the future task; neither omit future requirements nor require current workflow inside the artifact. Content acceptance grants no application effects or approval. Metadata-only repair does not reopen body judgments.")]
    public async Task ReviewDraftFidelity(PrepareInput input,PromptProposal proposal,RetrievalCandidate[] candidates,CancellationToken ct,string? boundTitleLiteral=null,Action<int,string,string?>? diagnostic=null)
    {
        var plan=input.Interpretation!;
        var sourceDocument=candidates.SingleOrDefault(candidate=>candidate.Prompt.Id==(input.SourcePromptId??proposal.SourceId??plan.ResolvedTargetId))?.Prompt;
        var source=input.WorkingDraft?.TemplateText ?? sourceDocument?.TemplateText;
        var sourceTitle=input.WorkingDraft?.Title ?? sourceDocument?.Title;
        // The generator's interpretation must never replace the user's authority.
        // Goal owns continuation; an artifact cannot narrow the request reviewed here.
        var request=plan.Goal?.UserRequest ?? input.TaskRequest;
        var metadataAuthority = input.VerbatimBody?.MetadataAuthority(request, proposal.TemplateText) ?? request;
        var material=new Dictionary<string,object?> {
            ["authorizedUserRequest"]=request,
            ["sourceTitle"]=sourceTitle,["sourceInstruction"]=source,["proposedTitle"]=proposal.Title,["proposedInstruction"]=proposal.TemplateText,
            // Operational facts describe work handled by other owners. They do
            // not replace the full work order or authorize artifact content.
            ["currentApplicationOperations"]=SemanticCapabilityCatalog.All.Where(capability=>plan.Intentions.Contains(capability.Operation))
                .Select(capability=>new {capability.Operation,capability.Description}).ToArray()
        };
        if (input.VerbatimBody is { } literalBody) {
            material["verbatimBodyBinding"] = new { literalBody.Start, literalBody.Length,
                exactOriginalOccurrence = true,
                purpose = "The host copied this exact occurrence as the proposed instruction body. Check its body role against the full authorizedUserRequest; this binding does not grant approval or certify the choice. Current application operations remain outside the body." };
            material["metadataAuthorityRequest"] = metadataAuthority;
        }
        if(boundTitleLiteral is not null) {
            if(proposal.Title!=boundTitleLiteral||!request.Contains(boundTitleLiteral,StringComparison.Ordinal))
                throw new ValidationException("El título dejó de coincidir con el literal autorizado.");
            material.Remove("proposedTitle"); material.Remove("sourceTitle");
            material["metadataAlreadyValidated"]=new {literalTitleMatched=true,titleExcludedFromBodyLanguage=true};
        }
        var inputIdentifiers=TemplateCompiler.Names(proposal.TemplateText);
        if(inputIdentifiers.Length>0) {
            var prose=string.Concat(TemplateCompiler.Parse(proposal.TemplateText).Select(token=>token.Name is null?token.Text:" ⟦…⟧ "));
            material["opaqueInputIdentifiers"]=inputIdentifiers;
            material["bodyProseForLanguageCheck"]=new {text=prose[..Math.Min(1500,prose.Length)],truncated=prose.Length>1500};
        }
        var preferences=input.ApprovedPreferences;
        var approvedContentPreferences=preferences?.Language is not null||preferences?.Format is not null;
        if(approvedContentPreferences) material["approvedContentPreferences"]=new {preferences!.Language,preferences.Format};
        var packet=JsonSerializer.Serialize(material,Json);
        if(Encoding.UTF8.GetByteCount(packet)>32000) throw new ValidationException("El contexto supera el presupuesto de revisión del borrador; no se truncará silenciosamente.");
        var schema=ContractSchema.Object(new() {
            ["verdict"]=new {type="string",@enum=new[]{"ACCEPT","REVISE"},description="First decide whether the ACTUAL proposed artifact fulfills the requested future task and preserves required source constraints. ACCEPT a correct artifact; no duty to invent criticism. REVISE only for a real supported mismatch. Judge body language from its PROSE, using bodyProseForLanguageCheck when supplied. opaqueInputIdentifiers are protocol field keys: preserve them exactly in any language, never count or translate them as body prose. User-request/title language is also irrelevant to body language."},
            ["instructionScope"]=new {type="string",@enum=new[]{"FUTURE_TASK_ONLY","CURRENT_AUTHORING_ONLY","MIXED_ACTORS"},description="Compare the current artifact author's job with the future reader's requested job. FUTURE_TASK_ONLY instructs the reader to perform the requested future task. CURRENT_AUTHORING_ONLY delegates building this very instruction artifact to its reader instead; MIXED_ACTORS combines both. Do not assume the proposal is already future-task content. A future task explicitly about authoring prompts or requiring approval remains valid; imperative wording alone never determines the actor."},
            ["applicationWorkflowQuotes"]=new {type="array",maxItems=8,items=ContractSchema.Text(500),description="Exact fragments of proposedInstruction that delegate THIS artifact's authoring or current application workflow to its future reader. Judge the actor against the user's requested future task, not individual words. Return [] for a direct instruction carrying out that task, including genuine future prompt authoring or approval. Never quote absent text."},
            ["requestedTitleQuote"]=boundTitleLiteral is not null ? (object)new {type="null",description="The title is already matched EXACTLY to its literal user binding by the domain. It is outside this BODY review, including language checks. Always null; never reinterpret a validated title."}
                : new {anyOf=new object[]{ContractSchema.Text(160),new {type="null"}},description="Exact literal new artifact title explicitly supplied in authorizedUserRequest, excluding its enclosing quotation marks. Never translate or paraphrase it to the requested body language. Null when no new title was specified; a named source to look up or quoted future-task output is not a requested new title."},
            ["issues"]=new {type="array",maxItems=8,description="Only real artifact mismatches, never diagnostic feedback. Each issue cites literal evidence from its stated authority. Omitting current-session workflow is correct.", items=ContractSchema.Object(new() { ["authority"]=ContractSchema.Text(30,["USER_REQUEST","SOURCE_INSTRUCTION"]),["problem"]=ContractSchema.Text(250),["evidence"]=ContractSchema.Text(500) },"authority","problem","evidence")}
        },"verdict","instructionScope","applicationWorkflowQuotes","requestedTitleQuote","issues");
        DraftFidelity review = null!; string rawReview = ""; string repairInstructions = "";
        DraftFidelity? preservedReview = null;
        string[]? repairFields = null;
        var bodyFields = new[] { "verdict", "instructionScope", "applicationWorkflowQuotes", "issues" };
        var allFields = schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
        ValidationException InvalidReview()
        {
            var failure = FidelityFailure("CONTENT_FIDELITY_REVIEW_INVALID", "No se pudo comprobar el borrador con los datos disponibles. El original se conserva.");
            failure.Data["NovaStageRepairExhausted"] = true;
            return failure;
        }
        for (var attempt = 0; attempt < 2; attempt++) {
        var metadataOnly = repairFields is ["requestedTitleQuote"];
        var bodyOnly = repairFields is not null && !repairFields.Contains("requestedTitleQuote");
        var activeSchema = repairFields is null ? schema : ContractSchema.Object(schema.GetProperty("properties").EnumerateObject()
            .Where(property => repairFields.Contains(property.Name)).ToDictionary(property => property.Name, property => (object)property.Value), repairFields);
        // Adaptation keeps source.Title by domain construction. Do not offer it
        // as a candidate NEW name during metadata extraction. Creation from a
        // source still needs its reference context, under the existing contract.
        var metadataPacket = new Dictionary<string, object?> { ["authorizedUserRequest"] = metadataAuthority };
        if (!plan.Adapts) metadataPacket["sourceTitle"] = sourceTitle;
        var activePacket = metadataOnly ? JsonSerializer.Serialize(metadataPacket, Json)
            : bodyOnly ? JsonSerializer.Serialize(material.Where(pair => pair.Key != "proposedTitle").ToDictionary(pair => pair.Key, pair => pair.Value), Json) : packet;
        JsonElement returned;
        rawReview="";
        try {
        using var response=await ChatForUi(new {
            model=input.Model,stream=false,think=!metadataOnly&&semanticThinking&&modelPolicy.Require(input.Model).Thinking,format=activeSchema,
            messages=new[] {
                new {role="system",content=metadataOnly
                    ? "Extract ONLY an exact literal NEW artifact title explicitly supplied in authorizedUserRequest. The request is the sole authority; sourceTitle, task outputs and input identifiers are not new requested names. Return requestedTitleQuote=null when no literal new title was supplied. Do not generate, translate or guess a title, judge/rewrite a body or grant actions. Return only the specified field. Schema: "+activeSchema.GetRawText()
                    : "Revisa únicamente el artefacto reutilizable, sin ejecutar nada. authorizedUserRequest es la petición real. sourceTitle/sourceInstruction son el artefacto original; proposedTitle/proposedInstruction son la propuesta. La propuesta todavía no está calificada. Distingue el trabajo del redactor actual, construir esta instrucción, del trabajo que el usuario quiere encargar a su lector futuro. Comprueba qué haría un lector al seguir el cuerpo: debe realizar la tarea futura pedida, no volver a redactar la instrucción que se te pidió crear. Si el usuario sí encargó al lector futuro redactar prompts o solicitar aprobación, conserva esa tarea legítima. No clasifiques por palabras aisladas ni por usar imperativos. currentApplicationOperations describe las operaciones de esta aplicación ya encargadas a otros propietarios, con descripciones del catálogo real. Son contexto operativo, no autoridad para omitir requisitos de la petición original. No exijas insertar en el cuerpo la búsqueda, revisión, aprobación, guardado o ejecución actuales: los ejecutores se encargan de esos efectos. Una aprobación o acción pedida expresamente como parte de la tarea FUTURA sí debe conservarse. Comprueba el cambio pedido, entradas, cantidades, idioma y restricciones; conserva el resto cuando así se pide. Una tarea futura puede exigir acciones y aprobación: eso es contenido válido. applicationWorkflowQuotes cita SOLO fragmentos del cuerpo propuesto que hablen de ESTA sesión de autoría de QuickPrompt, nunca instrucciones normales de la tarea futura ni texto ausente. El título es metadato: un título original conservado NO es un nuevo nombre literal aportado en la petición; requestedTitleQuote=null si no se aportó un nuevo nombre. Un nombre nuevo literal se conserva sin traducir. Si existe verbatimBodyBinding, comprueba que esa cita sea el cuerpo completo pedido; conservar literalmente ese cuerpo es obligatorio, sin incorporar acciones de la sesión. metadataAuthorityRequest es la única autoridad para requestedTitleQuote cuando se suministra: el tramo BOUND_INSTRUCTION_BODY no es un título. Sin origen no se exigen restricciones de una fuente inexistente. issues contiene SOLO defectos reales del artefacto, con evidencia literal de la petición o del origen; si no hay defectos, issues=[]. Diagnósticos de una revisión anterior no son defectos del artefacto ni requisitos del usuario. Devuelve solo JSON del esquema. "+(approvedContentPreferences?"approvedContentPreferences son preferencias aprobadas subordinadas a la petición. ":"")+ActorContractExamples+"Schema: "+activeSchema.GetRawText()+repairInstructions
                        +(bodyOnly?" Metadata is a separate, already bound declaration and is absent from this repair schema. Repair only the BODY group; do not introduce title judgments.":"")},
                new {role="user",content=activePacket}
            },options=new {temperature=0,num_ctx=InferenceContextLength,num_predict=metadataOnly?600:1800}
        },ct,"ReviewDraftFidelity");
        rawReview=response.RootElement.GetProperty("message").GetProperty("content").GetString()!;
        returned=ParseContent<JsonElement>(response,activeSchema);
        }
        catch(ValidationException) {
            diagnostic?.Invoke(191,JsonSerializer.Serialize(new {invalidResponseContract=true,attempt,
                responseCharacters=rawReview.Length,responseFingerprint=rawReview.Length>0?LiveRuns.Hash(rawReview):null,
                requestedFields=activeSchema.GetProperty("properties").EnumerateObject().Select(property=>property.Name).ToArray()},Json),"CONTENT_FIDELITY_REVIEW_INVALID");
            DiscardRejectedInference(rawReview);
            if(attempt==1) throw InvalidReview();
            repairFields=allFields;preservedReview=null;
            repairInstructions=" REVIEW_CONTRACT_REPAIR: The previous reviewer response violated its response contract. Return only a valid declaration under the unchanged schema for the SAME unchanged artifact. Diagnostic feedback is not a user requirement or an artifact defect.";
            continue;
        }
        if(preservedReview is not null) {
            var combined=JsonSerializer.SerializeToNode(preservedReview,Json)!.AsObject();
            foreach(var property in returned.EnumerateObject()) combined[property.Name]=JsonNode.Parse(property.Value.GetRawText());
            review=Parse<DraftFidelity>(JsonSerializer.SerializeToElement(combined),schema);
            diagnostic?.Invoke(191,JsonSerializer.Serialize(new {repairedFields=repairFields,
                modelResponse=returned,preservedFields=allFields.Except(repairFields!).ToArray()},Json),null);
        } else review=Parse<DraftFidelity>(returned,schema);
        // This is the bound declaration. Stage 191 separately identifies a partial
        // model response and the fields retained from the prior valid group.
        diagnostic?.Invoke(190,JsonSerializer.Serialize(review,Json),null);
        NovaTelemetry.DebugPoint("fidelity.review", review.Verdict != "ACCEPT" || review.ApplicationWorkflowQuotes.Length > 0 || review.Issues.Length > 0, () => new() {
            ["verdict"] = review.Verdict, ["workflowQuoteCount"] = review.ApplicationWorkflowQuotes.Length,
            ["issueCount"] = review.Issues.Length, ["attempt"] = attempt,
            ["proposedBodyHash"] = LiveRuns.Hash(proposal.TemplateText), ["sourceExists"] = source is not null
        });
        var bodyFailures = new List<string>();
        // A literal quotation proves presence, not that it belongs to the current
        // authoring session. Resolve a contradictory body declaration with the
        // existing bounded repair, rather than treating that interpretation as fact.
        var citedWorkflow = review.ApplicationWorkflowQuotes.Any(quote=>!string.IsNullOrWhiteSpace(quote)&&proposal.TemplateText.Contains(quote,StringComparison.Ordinal));
        // A consistent current-authoring defect cannot be erased by metadata repair.
        if(citedWorkflow && review.InstructionScope is "CURRENT_AUTHORING_ONLY" or "MIXED_ACTORS") {
            DiscardRejectedInference(rawReview);
            throw FidelityFailure("CONTENT_FIDELITY_MISMATCH","El cuerpo incluye órdenes de esta edición; conserva únicamente la tarea del futuro asistente.");
        }
        if(review.Verdict=="REVISE" && review.ApplicationWorkflowQuotes.Length==0&&review.Issues.Length==0)
            bodyFailures.Add("REVISE needs an actual grounded artifact issue, not an obligation to invent criticism.");
        if(review.InstructionScope!="FUTURE_TASK_ONLY"&&review.ApplicationWorkflowQuotes.Length==0)
            bodyFailures.Add("instructionScope/applicationWorkflowQuotes contradict each other. A future-task-only body has no current-authoring quotes. Classify the actor first; cite only real current-session contamination when the body mixes actors or only describes this editing session.");
        if(review.InstructionScope=="FUTURE_TASK_ONLY"&&review.ApplicationWorkflowQuotes.Length>0)
            bodyFailures.Add("FUTURE_TASK_ONLY contradicts nonempty applicationWorkflowQuotes. Literal presence alone does not establish current-session authoring. Compare the quoted text with the user's actual future task: retain real current-authoring defects with CURRENT_AUTHORING_ONLY or MIXED_ACTORS and REVISE; otherwise remove the wrongly classified quotes. Do not remove a real defect merely to accept the artifact.");
        if(review.Verdict=="ACCEPT"&&(review.ApplicationWorkflowQuotes.Length>0||review.Issues.Length>0))
            bodyFailures.Add("ACCEPT contradicts declared artifact defects. Return a coherent body judgment against the unchanged user/source authority, not a label-only correction.");
        if(review.Issues.Any(issue=>string.IsNullOrWhiteSpace(issue.Problem)||string.IsNullOrWhiteSpace(issue.Evidence)
            || (issue.Authority=="USER_REQUEST" ? !request.Contains(issue.Evidence,StringComparison.Ordinal)
                : source?.Contains(issue.Evidence,StringComparison.Ordinal)!=true && sourceTitle?.Contains(issue.Evidence,StringComparison.Ordinal)!=true)))
            bodyFailures.Add("issues: cite literal evidence from authorizedUserRequest or the actual source fields. Diagnostic validationFeedback is not user/source authority or an artifact defect.");
        var invalidTitle=review.RequestedTitleQuote is { } titleQuote && (string.IsNullOrWhiteSpace(titleQuote)
            || !request.Contains(titleQuote,StringComparison.Ordinal) || !metadataAuthority.Contains(titleQuote,StringComparison.Ordinal));
        if(review.ApplicationWorkflowQuotes.Any(quote=>string.IsNullOrWhiteSpace(quote)||!proposal.TemplateText.Contains(quote,StringComparison.Ordinal)))
            bodyFailures.Add("applicationWorkflowQuotes: a quote is empty or absent from proposedInstruction; quote only actual body fragments.");
        if(bodyFailures.Count==0&&!invalidTitle) break;
        DiscardRejectedInference(rawReview);
        if(attempt==1) throw InvalidReview();
        preservedReview=review;
        repairFields=(bodyFailures.Count>0?bodyFields:[]).Concat(invalidTitle?["requestedTitleQuote"]:[]).ToArray();
        repairInstructions=" REVIEW_CONTRACT_REPAIR: Repair ONLY the properties in this response schema; the other independently valid group is locked. "+string.Join(" ",bodyFailures)
            +(invalidTitle?" requestedTitleQuote must be an actual literal newly supplied title, never a generated/source title or input identifier.":"")
            +" The artifact and user/source authority are unchanged. Do not report diagnostics as artifact defects.";
        }
        if(review.RequestedTitleQuote is { } requestedTitle && proposal.Title!=requestedTitle) {
            DiscardRejectedInference(rawReview);
            throw FidelityFailure("CONTENT_FIDELITY_MISMATCH","El título debe conservar exactamente el nombre solicitado: "+requestedTitle);
        }
        if(review.Verdict!="ACCEPT"||review.ApplicationWorkflowQuotes.Length>0||review.Issues.Length>0) {
            DiscardRejectedInference(rawReview);
            throw FidelityFailure("CONTENT_FIDELITY_MISMATCH","El borrador no conserva todo el cambio solicitado. "+(review.ApplicationWorkflowQuotes.Length>0?"El cuerpo incluye órdenes dirigidas a la aplicación; debe contener únicamente la tarea del futuro asistente. ":"")+(review.Issues.Length==0?"El original se conserva; puedes ajustar la petición.":string.Join("; ",review.Issues.Select(issue=>issue.Problem))));
        }
        plan.Goal?.RecordContentAssessment(proposal.TemplateText);
    }
}
