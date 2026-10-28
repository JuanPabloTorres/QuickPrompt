using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// Contracts for the existing server-rendered workflows. Compilation explicitly
// persists history here; it must not be substituted for React's read-only preview.
public interface ITemplateAuthoring
{
    Template Save(Template value, string reason = "edit", Guid? proposalId = null);
}
public interface ICompilationReader
{
    CompiledPrompt Preview(Guid id, Dictionary<string, string> values, Guid? instructionId = null, string adjustments = "");
    List<CompiledPrompt> History();
    List<LlmExecution> Executions();
}
public interface ICompilationWriter
{
    CompiledPrompt Compile(Guid id, Dictionary<string, string> values, Guid? instructionId = null, string adjustments = "", int? expectedRevision = null);
    void FavoriteFinal(CompiledPrompt prompt);
    void SaveExecution(LlmExecution execution);
    void ClearHistory();
}
public interface ILocalSettings
{
    Preferences Settings();
    void SaveSettings(Preferences preferences);
}
public interface IProposalLifecycle
{
    void SaveProposal(AiProposal proposal);
    void DraftProposal(AiProposal proposal);
    Template StageDraftImprovement(Guid proposalId, Template source);
    void Reject(AiProposal proposal);
}
