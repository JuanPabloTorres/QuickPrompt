using QuickPrompt.Local;
namespace QuickPrompt.Nova.Application;

public record SavedResult(Guid Id, Guid TemplateId, int Revision, string Title, string Text, DateTime CreatedAt, LlmExecution[] Executions);
public record ResultHistoryPage(int Page, int PageSize, int Total, int ResponseCount, int TransferCount, string ExpectedSnapshot, SavedResult[] Items);
public record HistoryClearInput(bool Confirmed, string ExpectedSnapshot);

public interface IResultHistory
{
    ResultHistoryPage ResultHistory(int page);
    void ClearResultHistory(HistoryClearInput input);
}
