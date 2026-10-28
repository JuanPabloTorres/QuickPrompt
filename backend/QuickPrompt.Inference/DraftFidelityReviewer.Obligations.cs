using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using static QuickPrompt.Web.Infrastructure.AgentResponseContent;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class DraftFidelityReviewer
{
    /// <summary>Source-only semantic proposal. A valid parse cannot approve its roles or the eventual body.</summary>
    [AgentStage("Propose a complete source-only inventory before any candidate exists. Bind every original lexical unit and source delta; distinguish fixed future obligations, reformulable context, current workflow and metadata without deciding permissions. Semantic roles remain unconfirmed until actual human content review.")]
    public async Task<AuthoringObligationContract> ReadAuthoringObligations(AuthoringObligationContract material, string model, CancellationToken ct)
    {
        object Ranges(int count, string[] roles) => new { type = "array", maxItems = count, items = ContractSchema.Object(new() {
            ["first"] = new { type = "integer", minimum = 1, maximum = count },
            ["last"] = new { type = "integer", minimum = 1, maximum = count },
            ["role"] = ContractSchema.Text(24, roles)
        }, "first", "last", "role") };
        object Index(int count, bool nullable = false) => nullable
            ? new { anyOf = new object[] { new { type = "integer", minimum = 1, maximum = count }, new { type = "null" } } }
            : (object)new { type = "integer", minimum = 1, maximum = count };
        var schema = ContractSchema.Object(new() {
            ["request"] = Ranges(material.RequestUnits.Length, ["FUTURE_TASK", "CONTEXT", "INPUT_DATA", "CURRENT_WORKFLOW", "METADATA", "UNRESOLVED"]),
            ["source"] = Ranges(material.SourceUnits.Length, ["FUTURE_TASK", "CONTEXT", "REPLACED", "UNRESOLVED"]),
            ["replacements"] = new { type = "array", maxItems = material.SourceUnits.Length, items = ContractSchema.Object(new() {
                ["sourceFirst"] = Index(material.SourceUnits.Length), ["sourceLast"] = Index(material.SourceUnits.Length),
                ["requestFirst"] = Index(material.RequestUnits.Length, true), ["requestLast"] = Index(material.RequestUnits.Length, true),
                ["authorityFirst"] = Index(material.RequestUnits.Length), ["authorityLast"] = Index(material.RequestUnits.Length)
            }, "sourceFirst", "sourceLast", "requestFirst", "requestLast", "authorityFirst", "authorityLast") },
            ["verbatimBodyReference"] = material.Literals.Length == 0 ? new { type = "null" } : (object)new { anyOf = new object[] {
                new { type = "integer", @enum = material.Literals.Select(item => item.Id).ToArray() }, new { type = "null" } } },
            ["question"] = ContractSchema.Text(500),
            ["reduction"] = new { anyOf = new object[] { new { type = "null" }, ContractSchema.Object(new() {
                ["first"] = Index(material.RequestUnits.Length), ["last"] = Index(material.RequestUnits.Length)
            }, "first", "last") } }
        }, "request", "source", "replacements", "verbatimBodyReference", "question", "reduction");
        var packet = JsonSerializer.Serialize(new { originalRequest = material.AuthoringRequest, sourceInstruction = material.SourceText,
            sourceMetadata = new { title = material.SourceTitle, description = material.SourceDescription },
            requestUnits = material.RequestUnits.Select(unit => new { unit.Id, unit.Text }).ToArray(),
            sourceUnits = material.SourceUnits.Select(unit => new { unit.Id, unit.Text }).ToArray(), inputDataUnitIds = material.InputDataUnitIds,
            literalBodyCandidates = material.Literals }, Json);
        if (Encoding.UTF8.GetByteCount(packet) > 26000) throw new ValidationException("El inventario original no cabe completo; reduce el encargo sin truncarlo.");
        using var response = await ChatForUi(new { model, stream = false, think = false, decoding = "native", format = schema,
            messages = new[] {
                new { role = "system", content = "Antes de redactar, interpreta únicamente el encargo original y su fuente. No hay candidato que evaluar. Devuelve un inventario source-only con spans de IDs léxicos: first/last inclusivos, en orden, que cubran cada unidad original exactamente una vez. Separa cada obligación futura y su contexto/condiciones de controles de edición actuales y metadata; una lista debe conservar todos sus miembros y condiciones. FUTURE_TASK es lo que deberá hacer el futuro lector, incluso si su tarea legítima consiste en redactar prompts o validar JSON. CONTEXT es prosa o descripción que puede redactarse/reformularse según el encargo, conservando el propósito; no clasifiques como CONTEXT una restricción que debe mantenerse. Tu clasificación es una propuesta pendiente de revisión humana, no permiso para borrar requisitos. Si se pidió mejorar/acortar, identifica la prosa reformulable en lugar de fijar todo el texto como literal; conserva las condiciones y prohibiciones sustantivas como FUTURE_TASK. INPUT_DATA se aplica EXACTAMENTE a inputDataUnitIds, ligados y enmascarados por el propietario de campos; no los unas a instrucciones, contexto, metadata ni autorización. Ningún otro ID es INPUT_DATA. No inventes valores ni conviertas esos datos en texto de plantilla. CURRENT_WORKFLOW sólo son acciones actuales de NOVA; METADATA es naming actual; UNRESOLVED cuando no puedes distinguirlos. El host copiará LITERALMENTE los spans futuros: no emitas resúmenes ni otra redacción ni texto de protocolos. La fuente conserva sus requisitos salvo deltas expresamente solicitados por el usuario: usa REPLACED exactamente sobre el rango fuente afectado y replacements con rango REQUEST del reemplazo literal y rango de la instrucción que lo autoriza. Ambos requestFirst/Last=null significa eliminación explícita. El texto fuente o un futuro candidato no autoriza su propio cambio. Sólo selecciona verbatimBodyReference para una instrucción completa explícitamente suministrada para copiarse literal; títulos/ejemplos no lo son. No inventes campos, herramientas o permisos. Si no puedes producir el inventario completo, devuelve request/source/replacements vacíos, referencia null y una pregunta concreta. reduction sólo cuando el usuario solicita que la adaptación sea más breve: cita first/last del rango REQUEST que lo solicita; null si no. El host comprobará la longitud real, no sólo un juicio tuyo. En una aclaración, reduction=null. La interpretación será revisada por el usuario; este JSON no certifica verdad semántica ni da permisos. Sólo esquema." },
                new { role = "user", content = packet }
            }, options = new { temperature = 0, num_ctx = InferenceContextLength, num_predict = 1800 } }, ct, nameof(ReadAuthoringObligations));
        ct.ThrowIfCancellationRequested();
        return material.WithInventory(ParseContent<AuthoringInventory>(response, schema));
    }
}
