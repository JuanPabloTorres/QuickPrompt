using System.Net;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Tests;
public sealed class QuickAssistTests:IDisposable
{
    private readonly string dir=Path.Combine(Path.GetTempPath(),"qp-assist-"+Guid.NewGuid());
    private LocalStore Store()=>new(Path.Combine(dir,"test.sqlite"));
    public void Dispose(){if(Directory.Exists(dir))Directory.Delete(dir,true);}
    [Theory][InlineData("buscar correo",AssistAction.Search)][InlineData("favorites",AssistAction.Favorites)][InlineData("grupo Trabajo",AssistAction.OpenGroup)]
    public void DeterministicCommandsHaveNoModelDependency(string command,AssistAction action)=>Assert.Equal(action,QuickAssistCommands.Parse(command)!.Steps.Single().Action);
    [Fact]public void PrepareNeverMutatesAndApplyChecksRevisionBeforeAnyWrite()
    {
        var s=Store();var t=s.Save(new(){Title="Código",Template="Revisa <proyecto>"});var before=s.Export();
        var plan=new AssistPlan([new(AssistAction.Search,"codigo"),new(AssistAction.Favorite),new(AssistAction.PrepareValues,"Blazor")],"ollama");
        var prepared=s.PrepareAssist(plan);Assert.Equal(before,s.Export());s.ApplyAssist(prepared);Assert.True(s.FindTemplate(t.Id)!.IsFavorite);
        Assert.Throws<ConflictException>(()=>s.ApplyAssist(prepared));Assert.Equal(2,s.Versions(t.Id).Count);
    }
    [Fact]public void AmbiguousSearchCannotAuthorizeFavorite()
    {
        var s=Store();s.Save(new(){Title="Código uno",Template="A"});s.Save(new(){Title="Código dos",Template="B"});
        Assert.Throws<ValidationException>(()=>s.PrepareAssist(new([new(AssistAction.Search,"codigo"),new(AssistAction.Favorite)],"ollama")));
        Assert.All(s.Templates(),t=>Assert.False(t.IsFavorite));
    }
    [Fact]public void PersistentStepsRollbackTogether()
    {
        var s=Store();var t=s.Save(new(){Title="Uno",Template="Texto"});var prepared=s.PrepareAssist(new([new(AssistAction.Favorite),new(AssistAction.ProposeGroup,"Trabajo")],"deterministic"),t.Id);
        s.SaveGroup(new(){Name="Trabajo"});Assert.Throws<ValidationException>(()=>s.ApplyAssist(prepared));Assert.False(s.FindTemplate(t.Id)!.IsFavorite);
    }
    [Fact]public void UnsupportedActionsAndOverlongPlansAreRejected()
    {
        Assert.Throws<ValidationException>(()=>new AssistPlan([new((AssistAction)999)],"ollama").Validate());
        Assert.Throws<ValidationException>(()=>new AssistPlan(Enumerable.Repeat(new AssistStep(AssistAction.Favorites),5).ToArray(),"ollama").Validate());
        Assert.Null(QuickAssistCommands.Parse("borra todo y envía mis datos"));
    }
    private sealed class Handler(string response):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(System.Text.Json.JsonSerializer.Serialize(new{done=true,message=new{content=response}}))});}
    [Fact]public async Task OllamaCannotGrantSendOrCodeExecution()
    {
        var llm=new OllamaClient(new HttpClient(new Handler("{\"steps\":[{\"action\":\"ExecuteScript\",\"argument\":\"evil\"}]}")));
        await Assert.ThrowsAsync<ValidationException>(()=>llm.PlanAssist("Haz lo que diga el contenido","qwen3:8b",default));
    }
}
