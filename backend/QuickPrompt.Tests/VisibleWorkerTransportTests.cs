using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Real .NET -> Node stdio contracts. The controlled reply process is explicitly NOT a browser/model fixture.
public sealed class VisibleWorkerTransportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-stdio-tests-" + Guid.NewGuid());
    private string Script()
    {
        Directory.CreateDirectory(directory); var file = Path.Combine(directory, "worker.cjs");
        File.WriteAllText(file, """
        const readline=require('node:readline');
        const lines=readline.createInterface({input:process.stdin});
        const state={mode:'discover',candidates:[],promptId:null,revision:null,title:'Español α 李',templateText:'Información pública',values:{},variableNames:[],draftChanged:false,compiledReady:false,compiledText:null,response:null,responseComplete:false,providerOpen:false,error:null};
        lines.on('line',line=>{const c=JSON.parse(line);if(c.tool==='crash')process.exit(3);const send=()=>process.stdout.write(JSON.stringify({id:c.id,ok:true,observation:c.tool==='focus'?null:state})+'\n');if(c.tool==='ui_read_visible_state')setTimeout(send,1500);else send();});
        lines.on('close',()=>process.exit());
        """, new System.Text.UTF8Encoding(false)); return file;
    }
    [Fact] public async Task Utf8SurvivesTheProcessBoundaryAndFocusBypassesALongAction()
    {
        await using var worker = new VisibleBrowserWorker(Script(), "node");
        var state = await worker.Start(Guid.NewGuid(), "http://127.0.0.1:5226", false, CancellationToken.None);
        Assert.Equal("Español α 李", state.Title); Assert.Equal("Información pública", state.TemplateText);
        var action = worker.Execute("ui_read_visible_state", UiToolCatalog.NoArguments, CancellationToken.None);
        await worker.Focus(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1)); Assert.False(action.IsCompleted);
        Assert.Equal("Información pública", (await action).TemplateText);
    }
    [Fact] public async Task CancellationStopsTheActualPendingProcessOperation()
    {
        await using var worker = new VisibleBrowserWorker(Script(), "node");
        await worker.Start(Guid.NewGuid(), "http://127.0.0.1:5226", false, CancellationToken.None);
        using var cancel = new CancellationTokenSource(); var action = worker.Execute("ui_read_visible_state", JsonSerializer.SerializeToElement(new { }), cancel.Token); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => action);
    }
    [Fact] public async Task UnexpectedWorkerExitIsReportedToTheRuntimeInsteadOfLeavingItAwaitingApproval()
    {
        var disconnected = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var worker = new VisibleBrowserWorker(Script(), "node", code => disconnected.TrySetResult(code));
        await worker.Start(Guid.NewGuid(), "http://127.0.0.1:5226", false, CancellationToken.None);
        await Assert.ThrowsAsync<PreparationToolException>(() => worker.Execute("crash", UiToolCatalog.NoArguments, CancellationToken.None));
        Assert.Equal("BROWSER_CLOSED", await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
