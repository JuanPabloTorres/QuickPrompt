using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public interface IQuickAssistWorkflow
{
    PreparedAssistPlan PrepareAssist(AssistPlan plan, Guid? selectedId = null);
    void ApplyAssist(PreparedAssistPlan prepared);
}
public interface ITransferActivityReader
{
    List<WebTransferActivity> Transfers();
}
public interface ITransferActivityWriter
{
    void RecordTransfer(WebTransferActivity activity);
}
public interface ILibraryDiagnostics
{
    string CheckIntegrity();
}
