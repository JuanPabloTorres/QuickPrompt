using System.Diagnostics;
using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class DevelopmentLabObserverTests
{
    private static string NewDirectory()=>Path.Combine(Path.GetTempPath(),"nova-lab-"+Guid.NewGuid());
    [Fact]
    public void ProductionRejectsBeforeCreatingAnyFiles()
    {
        var directory=NewDirectory();
        Assert.False(DevelopmentLabObserver.Allowed(false,true));
        Assert.False(DevelopmentLabObserver.Allowed(true,false));
        Assert.Throws<InvalidOperationException>(()=>new DevelopmentLabObserver(false,directory,Directory.GetCurrentDirectory()));
        Assert.False(Directory.Exists(directory));
    }
    [Fact]
    public void DefaultCaptureExcludesPrivateRequestAndWorkerPayload()
    {
        using var observer=new DevelopmentLabObserver(true,NewDirectory(),Directory.GetCurrentDirectory());
        var id=Guid.NewGuid();
        observer.Request(new(id,Guid.NewGuid(),"PRIVATE_REQUEST_SENTINEL",Values:new(){["secret"]="PRIVATE_VALUE_SENTINEL"}));
        var start=new ProcessStartInfo();observer.ConfigureWorker(start,id,"request");
        Assert.Contains(id.ToString(),start.ArgumentList);Assert.DoesNotContain("--public-structured-output",start.ArgumentList);
        observer.Dispose();
        var rows=File.ReadAllText(observer.JournalPath);
        Assert.DoesNotContain("PRIVATE_REQUEST_SENTINEL",rows);Assert.DoesNotContain("PRIVATE_VALUE_SENTINEL",rows);
        Assert.Contains(id.ToString(),rows);
    }
    [Fact]
    public void SyntheticFixtureCaptureIsExplicitAndCompletionNotAcceptance()
    {
        using var observer=new DevelopmentLabObserver(true,NewDirectory(),Directory.GetCurrentDirectory(),true);
        var id=Guid.NewGuid();observer.Request(new(id,Guid.NewGuid(),"PUBLIC_FIXTURE"));
        observer.Live(new(id,Guid.NewGuid(),"fingerprint","SAME_TAB",LiveStatus.COMPLETED,4,DateTime.UtcNow));
        var start=new ProcessStartInfo();observer.ConfigureWorker(start,id,"request");
        Assert.Contains("--public-structured-output",start.ArgumentList);
        observer.Dispose();
        var rows=File.ReadLines(observer.JournalPath).Select(line=>JsonDocument.Parse(line).RootElement.Clone()).ToArray();
        var terminal=Assert.Single(rows,row=>row.GetProperty("kind").GetString()=="run_finished");
        Assert.Equal("OBSERVED",terminal.GetProperty("status").GetString());
        Assert.Contains("PUBLIC_FIXTURE",File.ReadAllText(observer.JournalPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactModelInputsAreCapturedOnlyForExplicitSyntheticFixtures(bool capture)
    {
        using var observer=new DevelopmentLabObserver(true,NewDirectory(),Directory.GetCurrentDirectory(),capture);
        var id=Guid.NewGuid();
        var request=JsonSerializer.SerializeToElement(new {requestId="call-7",model="qwen3:8b",limits=new {requests=2},
            messages=new[]{new{role="user",content="MODEL_INPUT_SENTINEL"}},outputSchema=new{type="object"}});
        observer.Inference(id,"CoreWorkOrder",request);
        observer.Dispose();
        var row=File.ReadLines(observer.JournalPath).Select(line=>JsonDocument.Parse(line).RootElement.Clone())
            .Single(item=>item.GetProperty("kind").GetString()=="model_request");
        Assert.Equal(id.ToString(),row.GetProperty("runId").GetString());
        Assert.Equal("call-7",row.GetProperty("observed").GetProperty("requestId").GetString());
        if(capture)Assert.Contains("MODEL_INPUT_SENTINEL",row.GetProperty("inputs").GetRawText());
        else Assert.DoesNotContain("MODEL_INPUT_SENTINEL",row.GetRawText());
    }

    [Fact]
    public async Task ClosureDrainsTheCurrentWriterAndRejectsLateEventsWithoutChangingTheJournal()
    {
        using var observer=new DevelopmentLabObserver(true,NewDirectory(),Directory.GetCurrentDirectory());
        var id=Guid.NewGuid();
        observer.Request(new(id,Guid.NewGuid(),"PRIVATE_REQUEST_SENTINEL"));
        var producer=Task.Run(()=> {
            for (var i=0;i<200;i++) observer.Emit(id,"concurrent_event","inspection","OBSERVED",new {sequence=i});
        });
        observer.Dispose();
        var closed=File.ReadAllBytes(observer.JournalPath);
        await producer;
        observer.Emit(id,"late_event","inspection","OBSERVED",new {late=true});
        observer.Dispose();
        Assert.Equal(closed,File.ReadAllBytes(observer.JournalPath));
        var lines=File.ReadAllLines(observer.JournalPath);
        Assert.All(lines,line=>Assert.Equal("ai-delivery-event/1",JsonDocument.Parse(line).RootElement.GetProperty("schema").GetString()));
        Assert.DoesNotContain("PRIVATE_REQUEST_SENTINEL",string.Join('\n',lines));
        Assert.Equal(0,observer.Errors);
    }
}

