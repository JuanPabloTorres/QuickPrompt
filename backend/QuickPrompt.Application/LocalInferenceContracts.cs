using QuickPrompt.Local;

// Namespace retained for source and binary contract compatibility. Owned by Application.
namespace QuickPrompt.Web.Infrastructure;

public record TemplateEditInterpretation(string RequestedContentChange, string? ValidationFeedback = null, string? ApprovedPreferencesJson = null, string[]? TargetTerms = null)
{
    [System.Text.Json.Serialization.JsonIgnore] public string[]? ApplicationEffects {get;init;}
}
public interface ILocalLlmClient
{
    Task<string[]> Available(CancellationToken ct);
    Task<AiProposal> Propose(string action, string context, Template? template, string model, CancellationToken ct);
    Task<AiProposal> Propose(string action, string context, Template? template, string model, CancellationToken ct, bool allowRevision) => Propose(action, context, template, model, ct);
    Task<AiProposal> Propose(string action, string context, Template? template, string model, CancellationToken ct, bool allowRevision, bool? requestedShortening) => Propose(action, context, template, model, ct, allowRevision);
    Task<AiProposal> Propose(string action, string context, Template? template, string model, CancellationToken ct, bool allowRevision, bool? requestedShortening, TemplateEditInterpretation? interpretation) => Propose(action, context, template, model, ct, allowRevision, requestedShortening);
    Task<Dictionary<string,string>> Suggest(Template template, string context, string model, CancellationToken ct);
    Task<AssistPlan> PlanAssist(string command,string model,CancellationToken ct);
    Task<(string Text,double ElapsedMs)> Execute(CompiledPrompt prompt, string model, bool confirmed, Func<string,Task>? progress, CancellationToken ct);
}
public sealed class LlmUnavailableException(string message) : Exception(message);
