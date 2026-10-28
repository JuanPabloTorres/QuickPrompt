using System.Text.Json;
using System.Text.RegularExpressions;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class PromptDraftGenerator
{
    private const string FutureInstructionSystem = """
        Escribe el prompt reutilizable que el usuario entregará a un asistente para que realice su tarea. Redacta las órdenes dirigidas a ese destinatario, sin resolver tú el caso ni rellenar una respuesta de ejemplo.
        El encargo completo está en originalRequest; selectedSource es la instrucción elegida para editar o usar como base. Conserva el propósito y desarrolla las acciones, análisis y condiciones pertinentes del encargo, incluidos los condicionales. No conviertas la instrucción en un índice con huecos o una solución prefabricada.
        userClarifications contiene respuestas del usuario a preguntas previas sobre ese encargo. Usa esas respuestas para precisar el contenido; las preguntas son contexto, no órdenes para el futuro destinatario. Conserva el encargo original y no transfieras nombres del contrato, preguntas de la aplicación ni controles de guardado a la instrucción futura.
        En INSTRUCTION, task encarga la tarea; steps describe qué hacer; constraints contiene sólo las condiciones al realizar esa tarea; outputRequirements exige qué desarrollar y entregar. Puedes usar task compacto y listas vacías al reducir una instrucción. Respeta idioma y formato solicitados.
        inputs contiene los espacios para los datos que se completarán al usar el prompt: incorpóralos en la instrucción. Las referencias Future pertenecen a esa instrucción; las Writing sirven al autor al redactarla. correctionRequirements, cuando existe, reitera los espacios y referencias de contenido que debe cumplir la nueva redacción.
        Si no puede determinarse la tarea, devuelve CLARIFICATION con una pregunta concreta e instruction null. No preguntes porque aún falten los valores de inputs. Si se solicita copiar literalmente un cuerpo completo disponible en literalBodyCandidates, devuelve LITERAL y su literalReference, con instruction null. En INSTRUCTION usa question null y literalReference null; en CLARIFICATION usa literalReference null; en LITERAL usa question null.
        title es un nombre breve de una línea; description describe el uso de la plantilla. Devuelve la definición conforme al esquema.
        """;

    // These are future instructions, not an answer, a source inventory or a
    // semantic certificate. The host owns their composition and branch rules.
    private record FutureInstruction(string Task, string[] Steps, string[] Constraints, string[] OutputRequirements);
    private record AuthoringDefinition(string Title, string Description, string Outcome,
        FutureInstruction? Instruction, string? Question, int? LiteralReference);

    private static JsonElement DefinitionSchema(int[] literalIds)
    {
        var instruction = ContractSchema.Object(new() {
            ["task"] = new { type = "string", minLength = 1, maxLength = 8000,
                description = "An executable instruction addressed to the future assistant: what it must accomplish using the supplied input placeholders. Not an answer, example, heading, artifact summary or instruction to write another prompt." },
            ["steps"] = InstructionList("Actions the future assistant must perform to accomplish the task. Develop the actual requested method; do not fill in a case, prescribe an unrequested solution or list empty response sections."),
            ["constraints"] = InstructionList("Conditions and prohibitions the future assistant must respect while doing the task. Preserve applicable source constraints. Include only conditions applying when performing the task."),
            ["outputRequirements"] = InstructionList("What the future assistant must include and explain in its deliverable. Phrase as requirements to fulfill, not a prefilled answer, sample folder tree or bracketed filler.")
        }, "task", "steps", "constraints", "outputRequirements");
        var fields = new Dictionary<string, object> {
            ["title"] = new { type = "string", minLength = 1, maxLength = 160, pattern = @"^[^\r\n]+$" },
            ["description"] = ContractSchema.Text(2000),
            ["outcome"] = ContractSchema.Text(20, literalIds.Length == 0
                ? ["INSTRUCTION", "CLARIFICATION"] : ["INSTRUCTION", "CLARIFICATION", "LITERAL"]),
            ["instruction"] = new { anyOf = new object[] { instruction, new { type = "null" } } },
            ["question"] = new { anyOf = new object[] { new { type = "string", minLength = 1, maxLength = 500 }, new { type = "null" } } },
            ["literalReference"] = literalIds.Length == 0 ? new { type = "null" } : (object)new { anyOf = new object[] {
                new { type = "integer", @enum = literalIds }, new { type = "null" } } }
        };
        string[] required = ["title", "description", "outcome", "instruction", "question", "literalReference"];
        object absent = new { type = "null" };
        var branches = new List<JsonElement> {
            Branch("INSTRUCTION", instruction, absent, absent),
            Branch("CLARIFICATION", absent, new { type = "string", minLength = 1, maxLength = 500 }, absent)
        };
        if (literalIds.Length > 0)
            branches.Add(Branch("LITERAL", absent, absent, new { type = "integer", @enum = literalIds }));
        // The provider and Core must reject mixed outcomes, not merely the host
        // after generation. Each alternative is complete: the small validator
        // evaluates anyOf before siblings. Native grammar converters do not
        // support properties alongside anyOf, so all constraints live in branches.
        return JsonSerializer.SerializeToElement(new { type = "object", anyOf = branches });

        JsonElement Branch(string outcome, object body, object question, object literal) =>
            ContractSchema.Object(new Dictionary<string, object>(fields) {
                ["outcome"] = ContractSchema.Text(20, [outcome]),
                ["instruction"] = body, ["question"] = question, ["literalReference"] = literal
            }, required);
    }

    private static object InstructionList(string description) => new { type = "array", maxItems = 40, description,
        items = new { type = "string", minLength = 1, maxLength = 8000 } };

    private static DraftProduction CompileDefinition(AuthoringDefinition definition, int[] literalIds)
    {
        if (definition.Outcome == "LITERAL" && definition.Instruction is null && definition.Question is null
            && definition.LiteralReference is { } id && literalIds.Contains(id))
            return new(definition.Title, definition.Description, "", id);
        if (definition.Outcome != "INSTRUCTION" || definition.Instruction is not { } instruction
            || definition.Question is not null || definition.LiteralReference is not null)
            throw new ValidationException("La definición debe contener una instrucción futura, una aclaración o una referencia literal exclusivas.");
        var parts = new List<string> { RequiredInstruction(instruction.Task) };
        // Neutral punctuation preserves the requested language and allows a
        // genuinely short adaptation; no fixed domain solution or prose is added.
        AddInstructions(parts, instruction.Steps, numbered: true);
        AddInstructions(parts, instruction.Constraints, numbered: false);
        AddInstructions(parts, instruction.OutputRequirements, numbered: false);
        var body = string.Join("\n\n", parts);
        if (body.Length > 32000) throw new ValidationException("La instrucción compilada excede el límite completo; no se truncará.");
        return new(definition.Title, definition.Description, body, null);
    }

    private static void AddInstructions(List<string> parts, string[] instructions, bool numbered)
    {
        if (instructions.Length == 0) return;
        parts.Add(string.Join("\n", instructions.Select((text, index) =>
            (numbered ? $"{index + 1}. " : "- ") + UnmarkedInstruction(text))));
    }

    // A model may include Markdown markers despite returning array items. This
    // formatting step leaves content, decimal numbers and the literal branch intact.
    private static string UnmarkedInstruction(string text) => RequiredInstruction(Regex.Replace(RequiredInstruction(text),
        @"^[ \t]*(?:[0-9]+[.)]|[-+*])[ \t]+", "", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));

    private static string RequiredInstruction(string text) => !string.IsNullOrWhiteSpace(text) ? text
        : throw new ValidationException("Una instrucción futura no puede estar vacía.");
}
