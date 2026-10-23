using LocalModelPolicy = QuickPrompt.Web.Infrastructure.LocalModelPolicy;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public sealed partial class AgentOrchestrator(IIntentInterpreter interpreter, IPromptRetriever retriever, IPromptGenerator generator,
    IVariableResolver resolver, IPromptEvaluator evaluator, IPreparedDraftStore drafts, PreparationOptions options,
    IAgentRunStore? runs = null, AgentSessions? sessions = null, ILocalPreparedExecutor? executor = null, IPreparationReadTools? readTools = null,
    PreparationDiagnostics? diagnostics = null, IPreparationPreferences? preferences = null, ICandidateSelector? candidateSelector = null,
    ITaskPlanner? taskPlanner = null, bool stopAfterVerifiedResult = false, bool executeGroundedPlan = false, IInputValueExtractor? inputExtractor = null,
    ISemanticAdviceResponder? adviceResponder = null)
{
    public const string ContractVersion = "nova-semantic-1.0";
    private readonly IInputValueExtractor? fieldExtractor = inputExtractor ?? generator as IInputValueExtractor;
    private readonly ISemanticAdviceResponder? manualAdvice = adviceResponder ?? generator as ISemanticAdviceResponder;
    private readonly ITaskPlanner planning = taskPlanner ?? new NovaTaskPlanner(
        interpreter as ISemanticIntentInterpreter ?? new StructuredIntentCompatibility(interpreter), retriever, sessions ?? new(),
        candidateSelector ?? interpreter as ICandidateSelector ?? new MissingCandidateSelector());
    public Task<PreparedDraft> Prepare(PrepareInput input, Func<PreparationState, Task> progress, Action<string, string, double> observe,
        CancellationToken ct, Func<string, Task>? responseProgress = null, Func<string, CancellationToken, Task>? beforeTool = null, Action<string>? modelConsulted = null)
        => PrepareSemantic(input, progress, observe, ct, responseProgress, beforeTool, modelConsulted);
    public static void Validate(PrepareInput input)
    {
        if (input.OperationId == Guid.Empty || input.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 16000 || !LocalModelPolicy.Production.Allows(input.Model)
            || input.Values?.Count > 60 || input.Values?.Any(x => x.Key.Length > 80 || x.Value is null || x.Value.Length > 8000) == true
            || (input.SourcePromptId is null) != (input.SourceRevision is null) || input.SourceRevision is < 1 || input.Filter?.CategoryId is < 0)
            throw new ValidationException("Solicitud de preparación inválida.");
    }
    private record SearchArgs(string Query, Guid? SourceId);
    private record EmptyArgs;
}
