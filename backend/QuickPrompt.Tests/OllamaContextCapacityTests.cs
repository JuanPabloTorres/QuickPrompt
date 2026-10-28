using System.Net;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class OllamaContextCapacityTests
{
    [Theory]
    [InlineData(8192, 2000, false)]
    [InlineData(12288, 2000, true)]
    [InlineData(12288, 6000, false)]
    public async Task CorrectionUsesConfiguredCapacityWithoutTruncatingAuthority(int capacity,int feedbackLength,bool fits)
    {
        var feedback=new string('é',feedbackLength);
        const string previous="Borrador rechazado completo.";
        var correction=JsonSerializer.Serialize(new {issues=new[]{new {code="CONTENT_FIDELITY_MISMATCH",message=feedback}},rejectedCandidate=previous},
            new JsonSerializerOptions {Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
        var source=new Template {Title="Guía",Template="Explica <tema> con el material proporcionado."};
        var calls=0;
        using var http=new HttpClient(new Handler(async request=> {
            calls++;
            using var packet=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var root=packet.RootElement;
            Assert.Equal(capacity,root.GetProperty("options").GetProperty("num_ctx").GetInt32());
            Assert.True(root.GetProperty("think").GetBoolean());
            var messages=root.GetProperty("messages");
            var bytes=messages.EnumerateArray().Sum(message=>Encoding.UTF8.GetByteCount(message.GetProperty("content").GetString()!));
            var reserved=root.GetProperty("options").GetProperty("num_predict").GetInt32()+256;
            Assert.InRange(bytes+reserved,8193,capacity);
            using var data=JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
            Assert.Equal(source.Template,data.RootElement.GetProperty("source").GetString());
            Assert.Equal(previous,data.RootElement.GetProperty("previousDraft").GetString());
            Assert.Equal(feedback,data.RootElement.GetProperty("validationFeedback").GetProperty("issues")[0].GetProperty("message").GetString());
            return new(HttpStatusCode.OK) {Content=new StringContent(JsonSerializer.Serialize(new {done=true,message=new {content=JsonSerializer.Serialize(new {
                text="Presenta <tema> con claridad para principiantes.",reasons=new[]{"Claridad"},changes=new[]{"Audiencia"},warnings=Array.Empty<string>(),variablesPreserved=new[]{"tema"}
            })}}))};
        }));
        var client=new OllamaClient(http,contextLength:capacity);
        var pending=client.Propose("improve","Aclara la explicación para principiantes.",source,"qwen3:8b",default,false,false,
            new TemplateEditInterpretation("Explicación clara para principiantes.",correction));
        if(fits) {
            var result=await pending;
            Assert.Equal("Presenta <tema> con claridad para principiantes.",result.Text);
            Assert.Equal(1,calls);
        } else {
            var failure=await Assert.ThrowsAsync<ValidationException>(()=>pending);
            Assert.Contains("presupuesto seguro de contexto",failure.Message);
            Assert.Equal(0,calls);
        }
    }
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> respond):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>respond(request);
    }
}
