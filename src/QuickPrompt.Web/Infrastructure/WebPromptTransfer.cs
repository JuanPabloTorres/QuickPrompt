using Microsoft.JSInterop;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

public interface IWebPromptTransfer
{
    event Action<PromptTransferResult>? Progress;
    ValueTask<PromptTransferResult> Detect(CancellationToken cancellationToken = default);
    ValueTask<PromptTransferResult> Transfer(PromptTransferRequest request, CancellationToken cancellationToken = default);
    ValueTask Cancel(Guid requestId);
}

// Browser-only transport. The ASP.NET host never contacts provider websites.
public sealed class WebPromptTransfer(IJSRuntime js) : IWebPromptTransfer, IAsyncDisposable
{
    private IJSObjectReference? module;
    private DotNetObjectReference<WebPromptTransfer>? reference;
    public event Action<PromptTransferResult>? Progress;
    [JSInvokable] public void ReportProgress(PromptTransferResult result)=>Progress?.Invoke(result);
    private async ValueTask<IJSObjectReference> Module()=>module ??= await js.InvokeAsync<IJSObjectReference>("import","./provider-transfer.js");
    public async ValueTask<PromptTransferResult> Detect(CancellationToken cancellationToken = default)
        => await (await Module()).InvokeAsync<PromptTransferResult>("detect",cancellationToken);
    public async ValueTask<PromptTransferResult> Transfer(PromptTransferRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate(DateTimeOffset.UtcNow);
        reference ??= DotNetObjectReference.Create(this);
        try{return await (await Module()).InvokeAsync<PromptTransferResult>("transfer",cancellationToken,request,reference);}
        catch(OperationCanceledException){await Cancel(request.RequestId);return new(request.RequestId,TransferState.NeedsManualPaste,"cancel-outcome-unknown");}
    }
    public async ValueTask Cancel(Guid requestId){if(module!=null)try{await module.InvokeVoidAsync("cancel",requestId);}catch(JSDisconnectedException){}}
    public async ValueTask DisposeAsync(){reference?.Dispose();if(module!=null)try{await module.DisposeAsync();}catch(JSDisconnectedException){}}
}
