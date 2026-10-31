using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class RuntimeSettingsSnapshotTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DeferredStagesKeepRegistrationProfileWhenConfigurationChanges(bool earlyInterpreter)
    {
        var configuration=Configuration();var services=Register(configuration);var transport=new StageTransport();
        services.AddSingleton<IValidatedAgentModelTransport>(transport);
        using var provider=services.BuildServiceProvider();
        var interpreter=earlyInterpreter?provider.GetRequiredService<ISemanticAdviceResponder>():null;
        configuration["QuickPrompt:Preparation:SemanticThinking"]="true";
        configuration["QuickPrompt:Preparation:TaskGoals"]="true";
        configuration["QuickPrompt:Preparation:AuthoringUseContract"]="true";
        interpreter??=provider.GetRequiredService<ISemanticAdviceResponder>();
        await interpreter.Explain("Explica la biblioteca.",null,null,"qwen3:8b",default);
        var input=AuthoringReviewTestFixture.Input("Crea una instrucción para explicar un tema.");
        var generator=provider.GetRequiredService<IPromptGenerator>();
        var proposal=await generator.GenerateAfterSyntheticTask(input,input.Interpretation!.Preparation(),[],default);
        var body=proposal.TemplateText;
        Assert.Equal(3,transport.Packets.Count); // Advice, current-author discovery, then writer.
        Assert.Single(transport.Packets, AuthoringSyntheticReview.IsCurrentAuthorDecision);
        Assert.False(input.Interpretation!.Goal!.HasContentAssessment(body));
        Assert.False(input.Interpretation!.Goal!.HasHumanContentApproval(body));
        Assert.All(transport.Packets,packet=>{
            Assert.False(packet.GetProperty("think").GetBoolean());
            Assert.DoesNotContain("Application system knowledge",packet.GetProperty("messages")[0].GetProperty("content").GetString());
        });
        Assert.False(provider.GetRequiredService<AdaptiveCoreOptions>().Thinking);
        Assert.False(provider.GetRequiredService<IAgentModelTransport>().UsesThinking("qwen3:8b"));
    }

    [Fact] public void SeparateCompositionsCaptureTheirOwnProfilesInsteadOfSharingGlobalOptions()
    {
        var configuration=Configuration();var first=Register(configuration);
        configuration["QuickPrompt:Preparation:SemanticThinking"]="true";
        var second=Register(configuration);
        using var one=first.BuildServiceProvider();using var two=second.BuildServiceProvider();
        Assert.False(one.GetRequiredService<IAgentModelTransport>().UsesThinking("qwen3:8b"));
        Assert.True(two.GetRequiredService<IAgentModelTransport>().UsesThinking("qwen3:8b"));
        Assert.NotSame(one.GetRequiredService<IAgentModelTransport>(),two.GetRequiredService<IAgentModelTransport>());
    }

    [Fact] public async Task EditorTransportAndEmbeddingsUseTheRegisteredEndpointAfterConfigurationMutation()
    {
        await using var original=new Loopback();await using var replacement=new Loopback();
        var configuration=Configuration();configuration["QuickPrompt:Ollama"]=original.Origin;
        var services=Register(configuration);configuration["QuickPrompt:Ollama"]=replacement.Origin;
        using var provider=services.BuildServiceProvider();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        Assert.Equal(["qwen3:8b"],await provider.GetRequiredService<ILocalLlmClient>().Available(timeout.Token));
        Assert.Equal(OllamaEmbeddings.Model,(await provider.GetRequiredService<ILocalEmbeddings>().Identity(timeout.Token)).Model);
        Assert.Equal(original.Origin, provider.GetRequiredService<AdaptiveCoreOptions>().Endpoint);
        var error = await Assert.ThrowsAsync<PreparationToolException>(() => provider.GetRequiredService<IAgentModelTransport>().Send(
            new {model="qwen3:8b",stream=false,think=false,messages=new[]{new {role="user",content="Synthetic transport probe."}}},timeout.Token,"SnapshotProbe"));
        Assert.Equal("CORE_NOT_CONFIGURED", error.Code); // No fallback to the old direct model endpoint.
        Assert.Equal(2,original.Requests);Assert.Equal(0,replacement.Requests);
    }

    private static IConfigurationRoot Configuration()=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["QuickPrompt:Ollama"]="http://127.0.0.1:1",["QuickPrompt:Preparation:SemanticThinking"]="false",
        ["QuickPrompt:Preparation:TaskGoals"]="false",["QuickPrompt:Preparation:AuthoringUseContract"]="false",
        ["QuickPrompt:Preparation:ReuseInference"]="false"
    }).Build();
    private static ServiceCollection Register(IConfiguration configuration)
    {
        var services=new ServiceCollection();Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules",true)!
            .GetMethod("AddNovaAgentModule",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,[services,configuration,Path.Combine(Path.GetTempPath(),"nova-settings-"+Guid.NewGuid(),"synthetic.sqlite")]);
        return services;
    }
    private sealed class StageTransport : IValidatedAgentModelTransport
    {
        public List<JsonElement> Packets=[];
        public Task<JsonDocument> Send(object packet,CancellationToken ct,string purpose)
        {
            ct.ThrowIfCancellationRequested();Packets.Add(JsonSerializer.SerializeToElement(packet));
            var answer = AuthoringSyntheticReview.IsCurrentAuthorDecision(Packets[^1])
                ? JsonSerializer.Serialize(AuthoringSyntheticReview.CurrentAuthorDecisionReady())
                : purpose=="ExplainTaskGoal"?"""{"answer":"Organiza y reutiliza instrucciones."}""":JsonSerializer.Serialize(AuthoringReviewTestFixture.Definition("Explica un tema.","Guía"),AuthoringReviewTestFixture.Json);
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new {message=new {content=answer}})));
        }
        public bool UsesThinking(string model)=>false;
        public void Observe(int stage,string content,string? error) { }
        public void Reject(string? answer)=>throw new InvalidOperationException("Unexpected controlled response rejection.");
    }
    private sealed class Loopback : IAsyncDisposable
    {
        private readonly HttpListener listener=new();private readonly CancellationTokenSource stop=new();private readonly Task loop;private int requests;
        public string Origin {get;}public int Requests=>Volatile.Read(ref requests);
        public Loopback()
        {
            var reservation=new TcpListener(IPAddress.Loopback,0);reservation.Start();var port=((IPEndPoint)reservation.LocalEndpoint).Port;reservation.Stop();
            Origin=$"http://127.0.0.1:{port}";listener.Prefixes.Add(Origin+"/");listener.Start();loop=Serve();
        }
        private async Task Serve()
        {
            try {while(!stop.IsCancellationRequested) {
                var context=await listener.GetContextAsync().WaitAsync(stop.Token);Interlocked.Increment(ref requests);
                object response=context.Request.Url!.AbsolutePath=="/api/tags"?new {models=new[] {new {name="qwen3:8b",digest=new string('a',64)},new {name=OllamaEmbeddings.Model,digest=new string('b',64)}}}:new {done=true,model="qwen3:8b",message=new {content="{}"}};
                context.Response.ContentType="application/json";await JsonSerializer.SerializeAsync(context.Response.OutputStream,response,cancellationToken:stop.Token);context.Response.Close();
            }}catch(OperationCanceledException)when(stop.IsCancellationRequested){}catch(HttpListenerException)when(stop.IsCancellationRequested){}
        }
        public async ValueTask DisposeAsync(){stop.Cancel();listener.Stop();await loop.WaitAsync(TimeSpan.FromSeconds(3));listener.Close();stop.Dispose();}
    }
}
