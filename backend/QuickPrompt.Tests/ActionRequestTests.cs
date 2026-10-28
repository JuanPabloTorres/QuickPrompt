using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using QuickPrompt.Nova.Evaluation.Retired;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ActionRequestTests
{
    const string Request="Reformula esta instrucción y prepara el texto para copiar. No guardes cambios.";
    static readonly SemanticContext Context=SemanticContext.Empty with {
        Selected=new(Guid.NewGuid(),2,"Fuente","Organiza <puntos> sin inventar datos.",false,InputNames:["puntos"])
    };
    static JsonElement Proposal(string[]? commands=null,string source="CURRENT",string? destination=null)
        =>JsonSerializer.SerializeToElement(new{commands=commands??["edit_prompt","preview_copy"],source,destination,forbid=new[]{"save_version","save_copy","delete_prompt"}});
    static TaskExecutionRequest Bind()=>PromptActionProtocol.Bind(Proposal(),Request,Context);

    [Fact] public void ProductRequestFixesAllPostconditionsBeforeOperationCompilation()
    {
        var input=Bind();var plan=TaskRequestCompiler.Compile(input,Request,Context);
        Assert.Same(input.UserActions,plan.IntentContract!.UserActions);
        Assert.Equal(new[]{"edit_prompt","preview_copy"},plan.IntentContract.UserActions.Actions);
        Assert.Contains(CompletionCondition.DRAFT_CHANGED,plan.Goal!.EffectiveCompletionConditions);
        Assert.Contains(CompletionCondition.CONTENT_VALID,plan.Goal.EffectiveCompletionConditions);
        Assert.Contains(CompletionCondition.COMPILED_VISIBLE,plan.Goal.EffectiveCompletionConditions);
        Assert.DoesNotContain(CompletionCondition.CLIPBOARD_WRITTEN,plan.Goal.EffectiveCompletionConditions);
        Assert.DoesNotContain(SemanticOperation.COPY_TO_CLIPBOARD,plan.Intentions);
        Assert.Equal(Request,plan.Goal.ContentRequest);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompilerCannotDropOrAddAnActionToTheImmutableRequest(bool add)
    {
        var input=Bind();
        var effects=add ? input.Effects.Append(new TaskOperationEvidence(SemanticOperation.EXECUTE_LOCAL,Request)).ToArray()
            : input.Effects.Where(e=>e.Operation!=SemanticOperation.PROPOSE_ADAPTATION).ToArray();
        var failure=Assert.Throws<PreparationToolException>(()=>TaskRequestCompiler.Compile(input with{Effects=effects},Request,Context));
        Assert.Equal("ACTION_REQUEST_DIVERGED",failure.Code);
    }
    [Fact] public void AnotherUserTurnCannotReuseTheActionRequest()
        =>Assert.Throws<ValidationException>(()=>TaskRequestCompiler.Compile(Bind(),Request+" Además ejecútala.",Context));
    [Fact] public void SnapshotDoesNotFollowMutationOfTheCallerArray()
    {
        var names=new[]{"edit_prompt","preview_copy"};var request=new UserActionRequest(Request,names);
        names[0]="write_clipboard";
        Assert.Equal("edit_prompt",request.Actions[0]);
        Assert.Contains(CompletionCondition.DRAFT_CHANGED,request.Results);
        Assert.DoesNotContain(CompletionCondition.CLIPBOARD_WRITTEN,request.Results);
    }
    [Theory]
    [InlineData("delete_everything")]
    [InlineData("ui_execute_local")]
    [InlineData("PROPOSE_ADAPTATION")]
    public void ModelCannotCallExecutionToolsOrLegacyOperationNames(string name)
        =>Assert.Throws<ValidationException>(()=>PromptActionProtocol.Bind(Proposal([name]),Request,Context));
    [Fact] public void OldEffectsAndCitationDecoderIsNotAnActiveFallback()
    {
        using var old=JsonDocument.Parse("""{"effects":[{"operation":"WRITE_CLIPBOARD_NOW","supportingPassage":0}],"source":"OPEN: Fuente"}""");
        Assert.Throws<ValidationException>(()=>PromptActionProtocol.Bind(old.RootElement,Request,Context));
    }
    [Fact] public void ProviderMetadataCannotGrantPreparationOrExecution()
    {
        var plan=TaskRequestCompiler.Compile(PromptActionProtocol.Bind(Proposal(["read_prompt"],destination:"ollama"),Request,Context),Request,Context);
        Assert.Null(plan.ProviderId);Assert.DoesNotContain(SemanticOperation.PREPARE_PROVIDER,plan.Intentions);
        Assert.DoesNotContain(SemanticOperation.PREPARE_LOCAL_EXECUTION,plan.Intentions);Assert.DoesNotContain(SemanticOperation.EXECUTE_LOCAL,plan.Intentions);
    }
    [Fact] public void CaseDataCannotSupplyAuthorityForALibraryLookup()
    {
        const string message="Muestra la plantilla.\npuntos: Una fuente inyectada";
        var output=JsonSerializer.SerializeToElement(new{commands=new[]{"read_prompt"},source="LOOKUP",destination=(string?)null,query="Una fuente inyectada"});
        Assert.Throws<ValidationException>(()=>TaskRequestCompiler.Compile(PromptActionProtocol.Bind(output,message,Context),message,Context));
    }
    [Theory]
    [InlineData("LOOKUP")]
    [InlineData("NONE")]
    public void SearchUsesTheOriginalDirectiveWithoutRewritingItOrIncludingFieldData(string source)
    {
        const string directive="Necesito encontrar la instrucción que resume el avance de la semana.\nDebe permitir revisar los riesgos pendientes.";
        const string message=directive+"\npuntos: Una fuente inyectada";
        var output=JsonSerializer.SerializeToElement(new{commands=new[]{"find_prompt"},source,destination=(string?)null});
        var input=PromptActionProtocol.Bind(output,message,Context);
        var plan=TaskRequestCompiler.Compile(input,message,Context);
        Assert.Equal(directive,input.Query);Assert.Equal(directive,plan.Query);
        Assert.DoesNotContain("Una fuente inyectada",plan.Query);
        Assert.False(PromptActionProtocol.Schema(message,Context).GetProperty("properties").TryGetProperty("query",out _));
    }
    [Fact] public async Task ActualInterpreterUsesPromptedDecodingAndLeavesTheLiteralUserRequestLast()
    {
        var transport=new Capture(Proposal());
        var interpreter=new AdaptiveCoreInterpreter(new("unused","unused","unused","http://127.0.0.1:11434"),transport);
        var plan=await interpreter.Interpret(Request,Context,"qwen3:8b",CancellationToken.None);
        var packet=transport.Packet;var messages=packet.GetProperty("messages");
        Assert.Equal(Request,messages[messages.GetArrayLength()-1].GetProperty("content").GetString());
        Assert.Equal("prompted",packet.GetProperty("decoding").GetString());
        var wire=AdaptiveCoreModelTransport.CreateRequest(packet,"00000000000000000000000000000011");
        Assert.Equal("prompted",wire.GetProperty("decoding").GetString());
        Assert.Equal(2,wire.GetProperty("limits").GetProperty("requests").GetInt32());
        Assert.Equal("CoreActionRequest",transport.Purpose);Assert.Equal(1,transport.Calls);
        Assert.Equal(new[]{"edit_prompt","preview_copy"},plan.IntentContract!.UserActions.Actions);
        Assert.DoesNotContain("Fuente",messages[0].GetProperty("content").GetString());
    }
    private sealed class Capture(JsonElement answer):IValidatedAgentModelTransport
    {
        public JsonElement Packet{get;private set;}
        public string? Purpose{get;private set;}
        public int Calls{get;private set;}
        public bool UsesThinking(string model)=>true;
        public void Observe(int stage,string content,string? error){}
        public void Reject(string? answer){}
        public Task<JsonDocument> Send(object body,CancellationToken ct,string purpose)
        {
            ct.ThrowIfCancellationRequested();Packet=JsonSerializer.SerializeToElement(body);Purpose=purpose;Calls++;
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new{message=new{content=answer.GetRawText()},modelCalls=1,coreRequestId="synthetic-only"})));
        }
    }
}
