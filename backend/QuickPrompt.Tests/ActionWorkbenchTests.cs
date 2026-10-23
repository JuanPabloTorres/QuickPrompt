using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ActionWorkbenchTests
{
    [Theory]
    [InlineData("Usa la propuesta anterior y enfócala en obstáculos del equipo.")]
    [InlineData("Hazlo más claro.")]
    [InlineData("Busca un prompt y después ejecútalo.")]
    public async Task MissingActionNeverGuessesAnOperationOrSearches(string message)
    {
        var planner = new NovaTaskPlanner(new ActionChoiceInterpreter(), new ForbiddenSearch(), new AgentSessions(), new ForbiddenSearch());
        var result = await planner.Interpret(message, Guid.NewGuid(), null, null, "qwen3:8b", null, default);
        Assert.Equal(new[] { SemanticOperation.ASK_CLARIFICATION }, result.Interpretation.Intentions);
        Assert.Equal(SemanticOutcome.CLARIFICATION, result.Interpretation.DesiredOutcome);
        Assert.Contains("USER_ACTION_REQUIRED", result.Interpretation.ValidationNotes!);
        Assert.Null(result.Source); Assert.Empty(result.Candidates);
    }

    [Fact] public void TypedSearchKeepsTheOriginalDescriptionAndCannotAcquireWriteEffectsOrTheOpenSource()
    {
        const string query = "La instrucción que resume el avance semanal. Debe permitir revisar riesgos.";
        var command = new LiveCommand(SemanticOperation.SEARCH_PROMPT, query);
        command.Validate(new(Guid.NewGuid(), Guid.NewGuid(), query));
        var incidental = new SemanticEntity(Guid.NewGuid(), 7, "Otro registro", "Texto ajeno.", false);
        var plan = command.Compile(query, SemanticContext.Empty with { Selected = incidental });
        Assert.Equal(query, plan.Query); Assert.Null(plan.ResolvedTargetId); Assert.Equal(TargetReference.NAMED, plan.TargetReference);
        Assert.Equal(new[] { SemanticOperation.SEARCH_PROMPT }, plan.IntentContract!.RequestedEffects);
        Assert.Equal(query, plan.IntentContract.UserActions.UserRequest);
        Assert.True(plan.Goal!.AllowsPlan(plan)); Assert.False(plan.Executes);
        Assert.All(new[] { SemanticOperation.CREATE_DRAFT, SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.EXECUTE_LOCAL,
            SemanticOperation.COPY_TO_CLIPBOARD, SemanticOperation.REQUEST_SAVE_COPY, SemanticOperation.REQUEST_SAVE_VERSION, SemanticOperation.REQUEST_DELETE },
            effect => { Assert.DoesNotContain(effect, plan.Intentions); Assert.Contains(effect, plan.IntentContract.ForbiddenEffects); });
    }

    [Fact] public void ASearchCannotSubmitAnInventedDescriptionOrBindAnIncidentalSource()
    {
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Descripción original");
        Assert.Throws<ValidationException>(() => new LiveCommand(SemanticOperation.SEARCH_PROMPT, "Otra descripción").Validate(input));
        Assert.Throws<ValidationException>(() => new LiveCommand(SemanticOperation.SEARCH_PROMPT, input.Message).Validate(input with { SourceId = Guid.NewGuid(), SourceRevision = 1 }));
    }

    [Fact] public async Task MissingActionHonorsCancellation()
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ActionChoiceInterpreter().Interpret("Petición", SemanticContext.Empty, "qwen3:8b", cancel.Token));
    }

    private sealed class ForbiddenSearch : IPromptRetriever, ICandidateSelector
    {
        public RetrievalCandidate[] Retrieve(string query, Guid? id) => throw new InvalidOperationException("Search requires an explicit action.");
        public Task<SemanticCandidateChoice> Select(string query, RetrievalCandidate[] candidates, string model, CancellationToken ct)
            => throw new InvalidOperationException("No inference without an action.");
    }
}
