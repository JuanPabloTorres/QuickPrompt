using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public sealed class AgentOrchestrator(IIntentInterpreter interpreter, IPromptRetriever retriever, IPromptGenerator generator,
    IVariableResolver resolver, IPromptEvaluator evaluator, IPreparedDraftStore drafts, PreparationOptions options)
{
    public async Task<PreparedDraft> Prepare(PrepareInput input, Func<PreparationState, Task> progress, Action<string, string, double> observe, CancellationToken ct)
    {
        Validate(input);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input with { Values = input.Values?.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary() }))));
        if (drafts.Fingerprint(input.OperationId) is { } previous)
        {
            if (previous != fingerprint) throw new ConflictException("Identificador de preparación ya utilizado para otra solicitud.");
            var existing = drafts.Get(input.OperationId) ?? throw new ConflictException("Este borrador ya no está disponible.");
            await progress(existing.State); return existing;
        }
        using var total = CancellationTokenSource.CreateLinkedTokenSource(ct); total.CancelAfter(TimeSpan.FromSeconds(options.TotalTimeoutSeconds));
        var token = total.Token;
        var tools = new PreparationTools(options.MaxSteps, options.StageTimeoutSeconds, observe);
        var messageSchema = ContractSchema.Object(new() { ["message"] = ContractSchema.Text(16000), ["model"] = ContractSchema.Text(60) }, "message", "model");
        var querySchema = ContractSchema.Object(new() { ["query"] = ContractSchema.Text(200), ["sourceId"] = new { anyOf = new object[] { new { type = "string", maxLength = 36 }, new { type = "null" } } } }, "query", "sourceId");
        tools.Register<InterpretArgs, Understanding>("interpret_intent", ToolPermission.Prepare, messageSchema, (args, cancel) => interpreter.Understand(args.Message, args.Model, cancel));
        tools.Register<SearchArgs, RetrievalCandidate[]>("search_prompts", ToolPermission.Read, querySchema, (args, _) => Task.FromResult(retriever.Retrieve(args.Query, args.SourceId)));
        var empty = ContractSchema.Object(new());
        Understanding understanding = null!; RetrievalCandidate[] candidates = []; PromptProposal proposal = null!; VariableValue[] values = [];
        tools.Register<EmptyArgs, PromptProposal>("propose_prompt", ToolPermission.Prepare, empty, (_, cancel) => generator.Generate(input, understanding, candidates, cancel));
        tools.Register<EmptyArgs, VariableValue[]>("resolve_variables", ToolPermission.Prepare, empty, (_, _) => Task.FromResult(resolver.Resolve(input, proposal, candidates.SingleOrDefault(c => c.Prompt.Id == proposal.SourceId)?.Prompt)));
        tools.Register<EmptyArgs, PreparedDraft>("validate_prompt", ToolPermission.Prepare, empty, (_, _) => Task.FromResult(evaluator.Evaluate(input, understanding, proposal, candidates, values)));
        tools.Register<EmptyArgs, PreparedDraft>("compile_prompt", ToolPermission.Prepare, empty, (_, _) => Task.FromResult(evaluator.Evaluate(input, understanding, proposal, candidates, values)));
        await progress(PreparationState.RECEIVED);
        try
        {
            await progress(PreparationState.UNDERSTANDING);
            understanding = await tools.Call<InterpretArgs, Understanding>("interpret_intent", new(input.Message, input.Model), ToolPermission.Prepare, token);
            if (!Enum.IsDefined(understanding.Intent) || understanding.Query is null || understanding.Query.Length > 200 || understanding.Clarification is null || understanding.Clarification.Length > 500)
                throw new ValidationException("Interpretación inválida.");
            await progress(PreparationState.SEARCHING);
            candidates = await tools.Call<SearchArgs, RetrievalCandidate[]>("search_prompts", new(understanding.Query, input.SourcePromptId), ToolPermission.Read, token);
            if (input.SourcePromptId is { } sourceId && candidates.SingleOrDefault(c => c.Prompt.Id == sourceId)?.Prompt.Revision != input.SourceRevision)
                throw new ConflictException("El origen cambió. Revisa el prompt antes de adaptarlo.");
            await progress(PreparationState.SELECTING_OR_GENERATING);
            var needsSource = understanding.Intent is PreparationIntent.ADAPT_PROMPT or PreparationIntent.IMPROVE_PROMPT or PreparationIntent.COMPLETE_PROMPT
                or PreparationIntent.SAVE_PROMPT or PreparationIntent.USE_PROMPT or PreparationIntent.RUN_LOCAL;
            if (needsSource && input.SourcePromptId is null && !candidates.Any(c => c.Score >= 2))
                proposal = new(PreparationDecision.ASK_CLARIFICATION, null, "", "", "", [], "¿Qué prompt quieres preparar? Indica su título o la tarea concreta.",
                    "La solicitud requiere un origen identificable; los resultados recientes no sustituyen el contexto que falta.");
            else if (understanding.Intent is PreparationIntent.UNKNOWN or PreparationIntent.ORGANIZE_PROMPT)
                proposal = new(PreparationDecision.ASK_CLARIFICATION, null, "", "", "", [], string.IsNullOrWhiteSpace(understanding.Clarification) ? "¿Qué instrucción necesitas preparar?" : understanding.Clarification,
                    "Esta preparación no concede permisos para cambiar la biblioteca ni ejecutar acciones externas.");
            else proposal = await tools.Call<EmptyArgs, PromptProposal>("propose_prompt", new(), ToolPermission.Prepare, token);
            // Reject unknown IDs and invalid structure before variable resolution, then bind reuse to the real snapshot.
            var validated = evaluator.Evaluate(input, understanding, proposal, candidates, []);
            proposal = proposal with { TemplateText = validated.TemplateText, RequiredInputs = validated.Variables.Select(v => v.Name).ToArray() };
            if (input.SourcePromptId is { } explicitSource && proposal.Decision != PreparationDecision.ASK_CLARIFICATION && proposal.SourceId != explicitSource)
                throw new ValidationException("La propuesta no respeta el origen seleccionado.");
            if (proposal.Decision == PreparationDecision.REUSE_EXISTING)
            {
                var source = candidates.Single(c => c.Prompt.Id == proposal.SourceId).Prompt;
                proposal = proposal with { Title = source.Title, Description = source.Description, TemplateText = source.TemplateText };
            }
            await progress(PreparationState.FILLING);
            values = await tools.Call<EmptyArgs, VariableValue[]>("resolve_variables", new(), ToolPermission.Prepare, token);
            await progress(PreparationState.VALIDATING);
            var draft = await tools.Call<EmptyArgs, PreparedDraft>("validate_prompt", new(), ToolPermission.Prepare, token);
            if (draft.State == PreparationState.READY)
                draft = await tools.Call<EmptyArgs, PreparedDraft>("compile_prompt", new(), ToolPermission.Prepare, token);
            token.ThrowIfCancellationRequested();
            drafts.Put(draft, fingerprint); // Recovery persists content only when the user opts in.
            await progress(draft.State); return draft;
        }
        catch (OperationCanceledException) { await progress(ct.IsCancellationRequested ? PreparationState.CANCELLED : PreparationState.FAILED); throw; }
    }
    public static void Validate(PrepareInput input)
    {
        if (input.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 16000 || input.Model is not ("qwen3:8b" or "qwen2.5:7b-instruct")
            || input.Values?.Count > 60 || input.Values?.Any(x => x.Key.Length > 80 || x.Value is null || x.Value.Length > 8000) == true
            || (input.SourcePromptId is null) != (input.SourceRevision is null)) throw new ValidationException("Solicitud de preparación inválida.");
    }
    private record InterpretArgs(string Message, string Model);
    private record SearchArgs(string Query, Guid? SourceId);
    private record EmptyArgs;
}
