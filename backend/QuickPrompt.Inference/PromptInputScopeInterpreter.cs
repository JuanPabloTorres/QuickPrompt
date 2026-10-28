using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Bounded field-material scope only; source identity and task authority stay outside.
public sealed class PromptInputScopeInterpreter : IInputScopeInterpreter
{
    private readonly IInputValueExtractor extractor;
    private readonly LocalModelPolicy modelPolicy;
    private readonly Action<InferenceObservation>? observe;
    public PromptInputScopeInterpreter(IInputValueExtractor extractor, LocalModelPolicy? modelPolicy = null, Action<InferenceObservation>? observe = null)
    {
        this.extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        this.modelPolicy = modelPolicy ?? LocalModelPolicy.Production;
        this.observe = observe;
    }
    public async Task<UserInputScope?> ResolveInputScope(string request, SemanticContext context, string model, CancellationToken ct)
    {
        modelPolicy.Require(model);
        var source = context.ResolvedSource ?? context.CurrentSubject().Entity;
        var names = context.CurrentInputNames();
        var literals = GroundedVariableResolver.LiteralAssignments(request, names);
        if (literals.Count == 0 || GroundedVariableResolver.AmbiguousLabels(request, names).Length > 0) return null;
        observe?.Invoke(new("SEMANTIC_INPUT_SCOPE", model));
        // Reuse the existing semantic field extractor. A labeled suffix is a
        // candidate, not proof that every later word belongs to its value.
        var input = new PrepareInput(Guid.NewGuid(), request, Model: model);
        for (var attempt = 0; attempt < 2; attempt++) {
            var extracted = await extractor.ExtractInputs(input, source?.Text ?? "", literals.Keys.ToArray(), ct);
            try {
                var scope = UserInputScope.FromExtraction(request, source?.Id, source?.Revision, names, extracted.Values);
                if (!string.IsNullOrWhiteSpace(extracted.Question)) throw new ValidationException(extracted.Question);
                return scope;
            } catch (ValidationException failure) when (attempt == 0) {
                input = input with { ValidationFeedback = System.Text.Json.JsonSerializer.Serialize(new {
                    failure = failure.Message, requirement = "Correct only the field extraction against the unchanged user material and actual field contracts. Return grounded complete values and no question when supplied material maps unambiguously. A field key does not itself demand a proper entity name or optional preference. If actual ambiguity remains, ask its concrete question instead of guessing. Do not reinterpret source, workflow or permissions."
                }) };
            }
        }
        throw new ValidationException("No se pudo delimitar la información de los campos.");
    }
}
