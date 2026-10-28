using Microsoft.JSInterop;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Web.Tests;
public class WebPromptTransferTests
{
    private static PromptTransferRequest Request(){var now=DateTimeOffset.UtcNow;return new(1,Guid.NewGuid(),ProviderId.ChatGptWeb,"Texto sintético autorizado",TransferAction.PrepareAndSend,now,now.AddMinutes(1),true);}
    [Fact] public async Task InvalidConsentNeverReachesTheBrowserTransport()
    {
        var runtime=new BrowserRuntime();await using var service=new WebPromptTransfer(runtime);
        await Assert.ThrowsAsync<ValidationException>(()=>service.Transfer(Request() with{ExplicitUserAction=false}).AsTask());
        Assert.Empty(runtime.Imports);Assert.Empty(runtime.Module.Calls);
    }
    [Fact] public async Task LosingTransportConfirmationCannotProveThatCancellationPreventedAClick()
    {
        var runtime=new BrowserRuntime();await using var service=new WebPromptTransfer(runtime);using var cancellation=new CancellationTokenSource();
        runtime.Module.OnTransfer=cancellation.Cancel;var result=await service.Transfer(Request(),cancellation.Token);
        Assert.Equal(TransferState.NeedsManualPaste,result.State);Assert.Equal("cancel-outcome-unknown",result.Code);
        Assert.Equal(["transfer","cancel"],runtime.Module.Calls);
    }
    private sealed class BrowserRuntime : IJSRuntime
    {
        public readonly List<string> Imports=[];public readonly CancelledBrowserCall Module=new();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>InvokeAsync<TValue>(identifier,default,args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken token,object?[]? args){Imports.Add(identifier);return ValueTask.FromResult((TValue)(object)Module);}
    }
    private sealed class CancelledBrowserCall : IJSObjectReference
    {
        public readonly List<string> Calls=[];
        public Action? OnTransfer;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>InvokeAsync<TValue>(identifier,default,args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken token,object?[]? args){Calls.Add(identifier);if(identifier=="transfer"){OnTransfer?.Invoke();throw new OperationCanceledException(token);}return ValueTask.FromResult(default(TValue)!);}
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
