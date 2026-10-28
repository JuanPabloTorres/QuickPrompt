using QuickPrompt.Local;

// Namespace retained for source and binary contract compatibility. Owned by Application.
namespace QuickPrompt.Web.Infrastructure;

public interface IWebPromptTransfer
{
    event Action<PromptTransferResult>? Progress;
    ValueTask<PromptTransferResult> Detect(CancellationToken cancellationToken = default);
    ValueTask<PromptTransferResult> Transfer(PromptTransferRequest request, CancellationToken cancellationToken = default);
    ValueTask Cancel(Guid requestId);
}

