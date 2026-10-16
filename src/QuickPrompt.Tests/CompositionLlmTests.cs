using System.Net;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Web.Tests;
public sealed class CompositionLlmTests
{
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>send(r);}
    private static CompiledPrompt Prompt(PromptComposition? composition=null)=>new(Guid.NewGuid(),Guid.NewGuid(),1,"test",composition?.ForExternalEditor()??"Pregunta",new Dictionary<string,string>(),DateTime.UtcNow,Composition:composition);
    [Fact]public async Task SelectedInstructionsUseSystemRoleAndCompiledVariablesUseUserRole()
    {
        string sent="";var client=new OllamaClient(new HttpClient(new Handler(async r=>{sent=await r.Content!.ReadAsStringAsync();return new(HttpStatusCode.OK){Content=new StringContent("{\"message\":{\"content\":\"Respuesta\"},\"done\":true}\n")};})));
        var c=new PromptComposition("Explica SQLite.","Español.","Actúa como arquitecto.","Máximo 40 palabras.",Guid.NewGuid(),2,"Arquitectura");await client.Execute(Prompt(c),"qwen3:8b",true,null,default);
        using var json=JsonDocument.Parse(sent);var messages=json.RootElement.GetProperty("messages");Assert.Equal("system",messages[0].GetProperty("role").GetString());Assert.Contains(c.Profile,messages[0].GetProperty("content").GetString());Assert.Contains(c.Adjustments,messages[0].GetProperty("content").GetString());Assert.Equal("user",messages[1].GetProperty("role").GetString());Assert.Equal(c.BaseText,messages[1].GetProperty("content").GetString());Assert.False(json.RootElement.TryGetProperty("tools",out _));
    }
    [Fact]public async Task MismatchedSnapshotAndOversizedContextFailBeforeHttp()
    {
        int calls=0;var client=new OllamaClient(new HttpClient(new Handler(_=>{calls++;throw new InvalidOperationException("Must not dispatch");})));
        var c=new PromptComposition("Pregunta","General","","",null,null,null);await Assert.ThrowsAsync<ValidationException>(()=>client.Execute(Prompt(c) with{Text="otra vista previa"},"qwen3:8b",true,null,default));await Assert.ThrowsAsync<ValidationException>(()=>client.Execute(Prompt() with{Text=new string('x',10000)},"qwen3:8b",true,null,default));Assert.Equal(0,calls);
        Assert.Throws<ValidationException>(()=>(c with{InstructionId=Guid.NewGuid(),InstructionName="Perfil",InstructionRevision=null}).Validate());
    }
    [Theory][InlineData("")][InlineData("   \n\t")]
    public async Task EmptyOrWhitespaceResponseCannotBeSaved(string text)
    {
        var json=JsonSerializer.Serialize(new{message=new{content=text},done=true})+"\n";var client=new OllamaClient(new HttpClient(new Handler(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)}))));await Assert.ThrowsAsync<ValidationException>(()=>client.Execute(Prompt(),"qwen3:8b",true,null,default));
    }
    [Fact]public async Task ModelNotInstalledIsControlledWithoutPartialSuccess()
    {
        var client=new OllamaClient(new HttpClient(new Handler(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound){Content=new StringContent("{\"error\":\"model not found\"}")}))));await Assert.ThrowsAsync<LlmUnavailableException>(()=>client.Execute(Prompt(),"qwen3:8b",true,null,default));
    }
}
