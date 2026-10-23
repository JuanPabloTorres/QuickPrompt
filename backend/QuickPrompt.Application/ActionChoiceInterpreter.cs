using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// Compatibility boundary for clients without a typed action. No model call,
// guessed operation, source lookup or side effect is permitted here.
public sealed class ActionChoiceInterpreter : ISemanticIntentInterpreter, IIntentInterpreter
{
    public Task<SemanticInterpretation> Interpret(string message, SemanticContext context, string model, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var plan = SemanticPlanCompiler.Compile(new SemanticMeaning([SemanticOperation.ASK_CLARIFICATION],
            TargetReference.NONE, "", null, "", "", new([], true, null, null, [], [], false),
            SemanticOutcome.CLARIFICATION, ["Elige una acción de NOVA y después escribe sus detalles."], false, "", null,
            "La acción debe ser elegida por el usuario."));
        return Task.FromResult(plan with { ValidationNotes = ["USER_ACTION_REQUIRED"] });
    }

    public async Task<Understanding> Understand(string message, string model, CancellationToken ct)
        => (await Interpret(message, SemanticContext.Empty, model, ct)).Preparation();
}
