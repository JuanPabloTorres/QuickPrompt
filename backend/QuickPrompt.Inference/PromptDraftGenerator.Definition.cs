using System.Text.Json;
using System.Text.RegularExpressions;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class PromptDraftGenerator
{
    private const string FutureInstructionSystem = """
        Escribe el prompt reutilizable que el usuario entregará a un asistente para que realice su tarea. Redacta las órdenes dirigidas a ese destinatario, sin resolver tú el caso ni rellenar una respuesta de ejemplo.
        fields contiene exclusivamente la tarea y los requisitos futuros que el autor estableció. En INSTRUCTION devuelve esas mismas claves dentro de instruction, con una mejora de redacción para cada valor. Conserva el significado, alcance y condiciones de cada campo; no traslades, amplíes ni suprimas requisitos. Una condición aplicable a un dato concreto sigue aplicándose sólo a ese dato. No añadas métodos, formatos, ejemplos, restricciones ni tareas. Si no hace falta mejorar un campo, conserva su texto.
        task encarga la tarea; step_N es una acción; constraint_N es una condición o prohibición; output_N exige parte del entregable. No crees otras claves ni metas requisitos nuevos dentro de las existentes. La propuesta se mostrará separada: no sustituye ni aprueba automáticamente el contrato establecido.
        inputs contiene los espacios para los datos que se completarán al usar el prompt: incorpóralos en la instrucción. Las referencias Future pertenecen a esa instrucción; las Writing sirven al autor al redactarla. correctionRequirements, cuando existe, reitera los espacios y referencias de contenido que debe cumplir la nueva redacción.
        contentConstraints y approvedPreferences describen cómo editar la redacción (idioma, formato o reducción), no requisitos nuevos para el futuro destinatario. Respétalos sin convertirlos en órdenes dentro del contenido. Conserva todos los campos incluso al acortar su redacción.
        Las decisiones pendientes del autor se aclaran antes de esta etapa. No abras otra ronda de preferencias o datos del caso. Las preguntas que el destinatario futuro deba formular siguen siendo parte de su tarea. literalBodyCandidates sólo ofrece cuerpos cuyo texto coincide exactamente con task establecido por el autor. Para conservar ese cuerpo literal, devuelve LITERAL y su literalReference, con instruction null; la aplicación conserva también sus pasos, restricciones y requisitos de salida. En INSTRUCTION usa question null y literalReference null; en LITERAL usa question null.
        title es un nombre breve de una línea; description describe el uso de la plantilla. Devuelve la definición conforme al esquema.
        """;

    // Discovery retains the established structure. The writer gets one fixed
    // slot per human-authored field; its output cannot create new list items.
    // Slot conservation is structural, never a semantic certificate.
    private record FutureInstruction(string Task, string[] Steps, string[] Constraints, string[] OutputRequirements);
    private record AuthoringDefinition(string Title, string Description, string Outcome,
        Dictionary<string, string>? Instruction, string? Question, int? LiteralReference);

    private static Dictionary<string, string> EstablishedFields(AuthoringBriefContent content)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["task"] = content.Task };
        Add("step", content.Steps); Add("constraint", content.Constraints); Add("output", content.OutputRequirements);
        return fields;

        void Add(string kind, string[] values) {
            for (var index = 0; index < values.Length; index++) fields.Add($"{kind}_{index + 1}", values[index]);
        }
    }

    private static JsonElement DefinitionSchema(int[] literalIds, Dictionary<string, string> establishedFields)
    {
        var instruction = ContractSchema.Object(establishedFields.ToDictionary(item => item.Key,
            _ => (object)new { type = "string", minLength = 1, maxLength = 8000 }), establishedFields.Keys.ToArray());
        var fields = new Dictionary<string, object> {
            ["title"] = new { type = "string", minLength = 1, maxLength = 160, pattern = @"^[^\r\n]+$" },
            ["description"] = ContractSchema.Text(2000),
            ["outcome"] = ContractSchema.Text(20, literalIds.Length == 0
                ? ["INSTRUCTION"] : ["INSTRUCTION", "LITERAL"]),
            ["instruction"] = new { anyOf = new object[] { instruction, new { type = "null" } } },
            ["question"] = new { anyOf = new object[] { new { type = "string", minLength = 1, maxLength = 500 }, new { type = "null" } } },
            ["literalReference"] = literalIds.Length == 0 ? new { type = "null" } : (object)new { anyOf = new object[] {
                new { type = "integer", @enum = literalIds }, new { type = "null" } } }
        };
        string[] required = ["title", "description", "outcome", "instruction", "question", "literalReference"];
        object absent = new { type = "null" };
        var branches = new List<JsonElement> {
            Branch("INSTRUCTION", instruction, absent, absent)
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

    private static AuthoringBriefContent CompileDefinition(AuthoringDefinition definition, AuthoringBriefContent established,
        DraftProposalContract contract)
    {
        if (definition.Outcome == "LITERAL" && definition.Instruction is null && definition.Question is null
            && definition.LiteralReference is { } id && contract.Literals.SingleOrDefault(item => item.Id == id) is { } literal
            && literal.Text == established.Task)
            // Literal provenance is not authority to replace the human task or
            // discard requirements that the established body does not contain.
            return established with { Title = definition.Title, Description = definition.Description, Task = literal.Text };
        if (definition.Outcome != "INSTRUCTION" || definition.Instruction is not { } revisions
            || definition.Question is not null || definition.LiteralReference is not null)
            throw new ValidationException("La redacción debe contener una instrucción futura o una referencia literal, sin preguntas de esta sesión.");
        var expected = EstablishedFields(established);
        if (revisions.Count != expected.Count || expected.Keys.Any(key => !revisions.ContainsKey(key)))
            throw new ValidationException("La revisión debe conservar exactamente los campos establecidos por el usuario.");
        var suggestion = established with { Title = definition.Title, Description = definition.Description,
            Task = RequiredInstruction(revisions["task"]), Steps = Read("step", established.Steps.Length),
            Constraints = Read("constraint", established.Constraints.Length), OutputRequirements = Read("output", established.OutputRequirements.Length) };
        var parts = new List<string> { suggestion.Task };
        // Neutral punctuation preserves the requested language and allows a
        // genuinely short adaptation; no fixed domain solution or prose is added.
        AddInstructions(parts, suggestion.Steps, numbered: true);
        AddInstructions(parts, suggestion.Constraints, numbered: false);
        AddInstructions(parts, suggestion.OutputRequirements, numbered: false);
        var body = string.Join("\n\n", parts);
        if (body.Length > 32000) throw new ValidationException("La instrucción compilada excede el límite completo; no se truncará.");
        return suggestion;

        string[] Read(string kind, int count) => Enumerable.Range(1, count)
            .Select(index => UnmarkedInstruction(revisions[$"{kind}_{index}"])).ToArray();
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
