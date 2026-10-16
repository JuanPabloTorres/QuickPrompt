using System.Text.Json.Serialization;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NovaIntent { SEARCH, OPEN, CREATE_DRAFT, IMPROVE_DRAFT, FILL_VARIABLES, COMPILE, RUN_LOCAL, SELECT_PROVIDER, HELP, UNKNOWN }
public record NovaPlan(NovaIntent Intent, string Query, int ToolSteps);
public record NovaRequest(string Message, string Model = "qwen3:8b", Guid? PromptId = null, int? Revision = null);
public record NovaAction(int ContractVersion, Guid Id, NovaIntent Intent, string Arguments, Guid[] SourcePromptIds,
    bool RequiresConfirmation, string ProposedEffect, string ValidationStatus, string CorrelationId,
    PromptDocument[] Candidates, string Message);
public record ProposalInput(string Intent, string Context, string Model = "qwen3:8b", Guid? PromptId = null, int? Revision = null);
public record ExecuteInput(Guid PromptId, int Revision, Dictionary<string, string> Values, string Model, bool Confirmed, string ExpectedText);

public interface INovaInference
{
    Task<NovaPlan> Interpret(string message, string model, CancellationToken ct);
    Task<AiProposal> Propose(string intent, string context, Template? source, string model, CancellationToken ct);
    Task<Dictionary<string, string>> Suggest(Template source, string context, string model, CancellationToken ct);
}

// Finite read/propose workflow. This class has no writer, database, shell or browser capability.
public sealed class NovaAssistant(IPromptReader reader, INovaInference inference)
{
    public async Task<NovaAction> Interpret(NovaRequest input, string correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 1000) throw new ValidationException("Escribe una intención de hasta 1000 caracteres.");
        var plan = await inference.Interpret(input.Message, input.Model, ct);
        if (!Enum.IsDefined(plan.Intent) || plan.Query is null || plan.Query.Length > 1000 || plan.ToolSteps is < 0 or > 3)
            throw new ValidationException("Plan rechazado: máximo tres pasos y herramientas conocidas.");
        var candidates = Array.Empty<PromptDocument>();
        if (plan.Intent is NovaIntent.SEARCH or NovaIntent.OPEN)
        {
            if (string.IsNullOrWhiteSpace(plan.Query) || plan.Query.Length > 200) throw new ValidationException("Consulta de búsqueda inválida.");
            candidates = reader.Search(new(Search: plan.Query, Size: 3)).Items.Select(PromptLibrary.Document).ToArray();
        }
        if (plan.Intent is NovaIntent.IMPROVE_DRAFT or NovaIntent.FILL_VARIABLES or NovaIntent.COMPILE or NovaIntent.RUN_LOCAL)
        {
            if (input.PromptId is not { } id) return Action(NovaIntent.UNKNOWN, [], "Selecciona primero un prompt de tu biblioteca.");
            var source = reader.Find(id) ?? throw new MissingPromptException();
            if (source.Revision != input.Revision) throw new ConflictException("El prompt cambió. Recarga antes de continuar.");
            candidates = [PromptLibrary.Document(source)];
        }
        var message = plan.Intent switch
        {
            NovaIntent.SEARCH or NovaIntent.OPEN => candidates.Length == 0 ? "No encontré coincidencias. Puedes probar otras palabras o crear un prompt." : $"Encontré {candidates.Length} prompts en tu biblioteca para «{plan.Query}».",
            NovaIntent.CREATE_DRAFT => "Puedo preparar un borrador. Lo revisarás antes de guardarlo.",
            NovaIntent.IMPROVE_DRAFT => "Puedo proponer una mejora conservando las variables. Revisarás el texto antes de guardarlo.",
            NovaIntent.FILL_VARIABLES => "Puedo sugerir valores. Revisa cada campo antes de utilizarlos.",
            NovaIntent.COMPILE => "Completa las variables para preparar y copiar el resultado.",
            NovaIntent.RUN_LOCAL => "Revisa el prompt y confirma la ejecución local en Usar en…",
            NovaIntent.SELECT_PROVIDER => "Elige un destino en Usar en… Copiar y abrir requiere una acción explícita.",
            NovaIntent.HELP => "Busca un prompt, ábrelo, completa sus variables y cópialo. También puedes crear uno nuevo.",
            _ => "¿Quieres buscar un prompt o preparar un borrador nuevo?"
        };
        return Action(plan.Intent, candidates, message);
        NovaAction Action(NovaIntent intent, PromptDocument[] items, string text) => new(1, Guid.NewGuid(), intent,
            plan.Query, items.Select(x => x.Id).ToArray(), intent is not (NovaIntent.SEARCH or NovaIntent.OPEN or NovaIntent.HELP or NovaIntent.UNKNOWN),
            "Sin cambios guardados. Toda propuesta requiere revisión.", "validated", correlationId, items, text);
    }
    private Template? Source(Guid? id, int? revision)
    {
        if (id is null) return null;
        var source = reader.Find(id.Value) ?? throw new MissingPromptException();
        if (source.Revision != revision || !source.IsActive) throw new ConflictException("El prompt cambió. Prepara una propuesta nueva.");
        return source;
    }
    public Task<AiProposal> Propose(ProposalInput input, CancellationToken ct)
    {
        if (input.Context is null || input.Context.Length > 4000 || input.Intent is not ("create" or "improve")) throw new ValidationException("Solicitud de propuesta inválida.");
        var source = Source(input.PromptId, input.Revision);
        if ((input.Intent == "improve") != (source is not null)) throw new ValidationException("Selecciona el origen correcto para la propuesta.");
        return inference.Propose(input.Intent, input.Context, source, input.Model, ct);
    }
    public Task<Dictionary<string, string>> Suggest(NovaRequest input, CancellationToken ct)
    {
        if (input.Message is null || input.Message.Length > 1000) throw new ValidationException("Contexto inválido.");
        return inference.Suggest(Source(input.PromptId, input.Revision) ?? throw new ValidationException("Selecciona un prompt."), input.Message, input.Model, ct);
    }
}
