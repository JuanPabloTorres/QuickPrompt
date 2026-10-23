using LocalModelPolicy = QuickPrompt.Web.Infrastructure.LocalModelPolicy;
using QuickPrompt.Local;
namespace QuickPrompt.Nova.Application;
public record LocalCommandInput(string Message, Guid? SessionId = null, Guid? SourcePromptId = null, int? SourceRevision = null);
public record LocalCommand(string Kind, PromptDocument[] Candidates, string Instruction, string Message, SemanticInterpretation? Semantic = null);
// Compatibility endpoint only. Natural language is interpreted by the shared Qwen planner.
public sealed class LocalCommands(ITaskPlanner planning)
{
    public async Task<LocalCommand> Resolve(LocalCommandInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 16000) throw new ValidationException("Escribe una instrucción de hasta 16000 caracteres.");
        var session = input.SessionId ?? Guid.NewGuid();
        var resolution = await planning.Interpret(input.Message, session, input.SourcePromptId, input.SourceRevision, LocalModelPolicy.Production.DefaultModel, null, ct);
        var plan = resolution.Interpretation;
        // Combined operations are executed by /prepare, never swallowed by a navigation shortcut.
        if (plan.Intentions.Any(operation => operation is not (SemanticOperation.SEARCH_PROMPT or SemanticOperation.RESOLVE_PROMPT or SemanticOperation.OPEN_PROMPT))
            || plan.DesiredOutcome != SemanticOutcome.READ) return new("NONE", [], "", plan.Explanation, plan);
        resolution = await planning.Resolve(resolution, input.Message, session, LocalModelPolicy.Production.DefaultModel, null, ct);
        return new("OPEN", resolution.Candidates.Where(c => c.Score >= 2).Select(c => c.Prompt).Take(6).ToArray(), "", string.Join(" ", resolution.Interpretation.MissingInformation), resolution.Interpretation);
    }
}
