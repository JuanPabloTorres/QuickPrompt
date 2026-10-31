using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class PromptDraftGenerator
{
    // This stage can propose a content question only. READY never grants an
    // application action, content approval or permission to persist a proposal.
    private const string CurrentAuthorDecisionSystem = """
        Determina si queda una decisión del autor que deba aclararse en ESTA conversación antes de redactar su prompt reutilizable. Considera originalRequest y las respuestas userClarifications. No redactes el prompt ni una pregunta nueva. Si queda una decisión pendiente, devuelve ASK y copia en decisionText el fragmento literal de originalRequest o de un campo answer de userClarifications que expresa esa decisión, sin reformularlo ni unir fuentes. Los campos question sólo son contexto de preguntas anteriores, no declaraciones del autor. Si la tarea ya puede redactarse, devuelve READY y decisionText null. Las condiciones que el futuro asistente cumplirá al usar la plantilla y los valores de sus variables no son decisiones pendientes del autor actual. No elijas una alternativa por el usuario. Usa exclusivamente el esquema.
        selectedSource conserva la instrucción elegida como base; inputs son los marcadores ya seleccionados y contentConstraints conserva las restricciones del cambio. Considera esos datos al revisar la tarea existente. El escritor recibirá sólo los campos futuros establecidos y las preferencias de edición; la procedencia completa permanece en esta etapa. No solicites valores de los marcadores ni conviertas estos datos en permisos o aprobaciones.
        Esta etapa sólo se invoca después de que el autor estableció la tarea y los requisitos en establishedTask. Considera esa decisión humana para resolver las ambigüedades anteriores del original. READY no establece la tarea ni autoriza redactar por sí mismo; el host ya exige ese estado previo.
        establishedTask describe la tarea FUTURA establecida por el autor. No conviertas sus preguntas o condiciones futuras en decisiones actuales. Las citas de ASK siguen procediendo sólo de originalRequest o de respuestas answer individuales; ese contrato y las sugerencias del modelo no amplían las fuentes de citas actuales.
        """;

    private sealed record CurrentAuthorDecision(string Outcome, string? DecisionText);
    private static readonly JsonElement CurrentAuthorDecisionSchema = JsonSerializer.SerializeToElement(new {
        type = "object", anyOf = new[] {
            ContractSchema.Object(new() { ["outcome"] = ContractSchema.Text(5, ["READY"]),
                ["decisionText"] = new { type = "null" } }, "outcome", "decisionText"),
            ContractSchema.Object(new() { ["outcome"] = ContractSchema.Text(5, ["ASK"]),
                ["decisionText"] = new { type = "string", minLength = 1, maxLength = 340 } }, "outcome", "decisionText")
        }
    });

    [AgentStage("Identify only a still-open decision belonging to the current prompt author. Return an exact request quotation or READY; never write a template, choose an answer, or authorize application actions.")]
    private async Task<string?> DiscoverCurrentAuthorDecision(PrepareInput input, DraftProposalContract contract,
        string material, CancellationToken ct)
    {
        using var response = await Chat(new {
            model = input.Model, stream = false, think = transport.UsesThinking(input.Model), decoding = "native", format = CurrentAuthorDecisionSchema,
            messages = new[] { new { role = "system", content = CurrentAuthorDecisionSystem }, new { role = "user", content = material } },
            // Reasoning counts against this output limit. The earlier 700-token
            // allocation truncated a real clarification continuation before its
            // JSON answer. 1400 + the writer's 3500 remains below the previous
            // two-attempt authoring ceiling of 7000, with the same 90-second total.
            options = new { temperature = 0, num_ctx = InferenceContextLength, num_predict = 1400, max_requests = 1 }
        }, ct, nameof(DiscoverCurrentAuthorDecision), contentOnly: true);
        ct.ThrowIfCancellationRequested();
        var decision = ParseContent<CurrentAuthorDecision>(response, CurrentAuthorDecisionSchema);
        if (decision.Outcome == "READY" && decision.DecisionText is null) return null;
        if (decision.Outcome != "ASK" || string.IsNullOrWhiteSpace(decision.DecisionText)
            || !(contract.AuthoringRequest.Contains(decision.DecisionText, StringComparison.Ordinal)
                || contract.UserClarifications.Any(item => item.Answer.Contains(decision.DecisionText, StringComparison.Ordinal))))
            throw new ValidationException("La aclaración no identifica una parte literal de la petición o de una respuesta del usuario; no se redactará una plantilla.");
        // Only the author's individual statements can ground another question.
        // A prior generated question, selected template or diagnostic is not such a statement.
        // Display the original words, not another model's reformulation of the
        // author's choice. Rendering a quote does not certify its semantic role.
        return "Antes de redactar la plantilla, aclara esta parte de tu petición:\n«" + decision.DecisionText + "»\n¿Qué eliges o cómo quieres concretarlo?";
    }
}
