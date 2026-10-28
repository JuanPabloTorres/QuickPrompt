using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class DraftFidelityReviewer
{
    /// <summary>Source-only interpretation; no draft or proposed scope assignments are supplied to this stage.</summary>
    public async Task<AuthoringReferenceInterpretation> ReadReferenceScopes(AuthoringReferenceCoverage references, string model, CancellationToken ct)
    {
        if (references.References.Length == 0) return new([], "");
        var schema = ContractSchema.Object(new() {
            ["uses"] = new { type = "array", minItems = 0, maxItems = references.References.Length,
                items = ContractSchema.Object(new() {
                    ["id"] = new { type = "integer", @enum = Enumerable.Range(1, references.References.Length).ToArray() },
                    ["role"] = ContractSchema.Text(32, Enum.GetNames<AuthoringReferenceRole>()), ["evidence"] = ContractSchema.Text(8000)
                }, "id", "role", "evidence") },
            ["question"] = ContractSchema.Text(500)
        }, "uses", "question");
        using var response = await ChatForUi(new {
            model, stream = false, think = false, format = schema,
            messages = new[] {
                new { role = "system", content = "Classify each supplied reference from the original authoring directive only. FUTURE_TASK means the future instruction reader needs this exact resource, including learning/research/reference links. CURRENT_SESSION means it belongs only to the present artifact's review, naming, saving or other current application workflow. BOTH requires both explicitly requested roles. Never infer a workflow action from a URI alone. Evidence must be an exact contextual source quote containing the complete URI, not an invented explanation or the URI alone for a session control. When question is empty, cover every ID exactly once. If clarification is essential, return uses=[] and the question. Case values have already been masked. Do not draft, shorten URIs, choose application operations, fetch resources or grant permissions. If the source leaves the role genuinely ambiguous, ask one concrete question; do not decide from a future draft. Return only the schema JSON." },
                new { role = "user", content = JsonSerializer.Serialize(new { originalDirective = references.Directive,
                    references = references.References.Select((uri, index) => new { id = index + 1, uri }) }, Json) }
            }, options = new { temperature = 0, num_ctx = InferenceContextLength, num_predict = 1400 }
        }, ct, nameof(ReadReferenceScopes));
        var result = AgentResponseContent.ParseContent<AuthoringReferenceInterpretation>(response, schema);
        if (string.IsNullOrWhiteSpace(result.Question)) _ = references.WithScopes(result);
        return result;
    }
}
