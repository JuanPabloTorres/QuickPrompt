using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Cloud;

/// <summary>
/// Per-conversation model capabilities bound to an authenticated account, library and approved
/// companion. Owns only its editor wrapper; the model port and domain effects remain external.
/// No HTTP endpoint, local model process or user-selected host is constructed here.
/// </summary>
public sealed class AccountNovaInference : IDisposable
{
    private readonly OllamaClient editor;
    public ILocalLlmClient Editor => editor;
    public IInputValueExtractor InputValues { get; }
    public IInputScopeInterpreter InputScope { get; }
    public IPromptGenerator Generator { get; }
    public ICandidateSelector Candidates { get; }
    public IUiDecisionModel UiDecision { get; }
    public ISemanticAdviceResponder Advice { get; }
    public ISemanticContentInterpreter Content { get; }

    public AccountNovaInference(AccountModelProposals proposals, LibraryActor actor, Guid libraryId, Guid connectionId)
    {
        if (libraryId == Guid.Empty || connectionId == Guid.Empty) throw new ValidationException("Biblioteca y conexión requeridas.");
        var transport = proposals.ForConnection(actor, libraryId, connectionId);
        editor = OllamaClient.ForTransport(transport);
        InputValues = new PromptInputValueExtractor(transport);
        InputScope = new PromptInputScopeInterpreter(InputValues);
        Generator = new PromptDraftGenerator(transport, InputValues);
        Candidates = new SemanticCandidateSelector(transport);
        UiDecision = new UiDecisionModel(transport);
        Advice = new PromptAdviceResponder(transport);
        Content = new PromptContentInterpreter(transport);
    }

    public void Dispose() => editor.Dispose();
}
