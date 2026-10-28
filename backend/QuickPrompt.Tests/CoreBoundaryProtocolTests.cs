using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using QuickPrompt.Nova.Evaluation.Retired;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class CoreBoundaryProtocolTests
{
    [Fact] public void DestinationIdentityMustBeConsideredAndUsesTheDomainProviderCatalog()
    {
        var schema=PromptActionProtocol.Schema("Prepara el destino.",SemanticContext.Empty);
        Assert.Contains("destination",schema.GetProperty("required").EnumerateArray().Select(v=>v.GetString()));
        using var packet=JsonDocument.Parse(AdaptiveCoreInterpreter.CreatePacket("Prepara el destino.",SemanticContext.Empty));
        var providers=packet.RootElement.GetProperty("providers").EnumerateArray().ToArray();
        Assert.Equal(ProviderRegistry.Providers.Select(p=>p.ProviderId),providers.Select(p=>p.GetProperty("providerId").GetString()));
        Assert.Equal(ProviderRegistry.Providers.Select(p=>p.DisplayName),providers.Select(p=>p.GetProperty("displayName").GetString()));
    }

    [Theory]
    [InlineData("preview_copy",SemanticOperation.COPY_PROMPT)]
    [InlineData("write_clipboard",SemanticOperation.COPY_TO_CLIPBOARD)]
    public void ModelCopyOutcomesBindToDistinctExistingOperations(string name,SemanticOperation expected)
    {
        const string request="Solicito este resultado de la aplicación.";
        var source=new SemanticEntity(Guid.NewGuid(),1,"Fuente","Texto fijo.",false,InputNames:[]);
        var context=SemanticContext.Empty with {Selected=source};
        var output=JsonSerializer.SerializeToElement(new {commands=new[]{name},source="CURRENT",destination=(string?)null});
        var declaration=PromptActionProtocol.Bind(output,request,context);
        Assert.Equal(expected,Assert.Single(declaration.Effects).Operation);
        var commands=PromptActionProtocol.Schema(request,context).GetProperty("properties").GetProperty("commands").GetProperty("items");
        Assert.Contains(name,commands.GetProperty("enum").EnumerateArray().Select(v=>v.GetString()));
        Assert.Equal(name,Assert.Single(declaration.UserActions!.Actions));
    }

    [Fact]
    public void ExplicitSamplingSeedSurvivesTheCoreProtocolBoundary()
    {
        var packet=Element("""{"model":"qwen3:8b","think":true,"messages":[{"role":"user","content":"Synthetic"}],"options":{"temperature":0.6,"top_p":0.95,"top_k":20,"min_p":0,"seed":42017}}""");
        var limits=AdaptiveCoreModelTransport.CreateRequest(packet,Id).GetProperty("limits");
        Assert.Equal(42017,limits.GetProperty("seed").GetInt32());
        Assert.Equal(.6,limits.GetProperty("temperature").GetDouble());
        Assert.Equal(20,limits.GetProperty("topK").GetInt32());
        var ordinary=AdaptiveCoreModelTransport.CreateRequest(Element("""{"model":"qwen3:8b","messages":[{"role":"user","content":"Synthetic"}]}"""),Id);
        Assert.False(ordinary.GetProperty("limits").TryGetProperty("seed",out _));
    }

    [Theory]
    [InlineData(false, null, false)]
    [InlineData(false, "Tarea anterior", false)]
    [InlineData(true, null, false)]
    [InlineData(true, " ", false)]
    [InlineData(true, "Prepara la copia.", true)]
    public void ContinuationSchemaUsesVerifiedPendingState(bool hasPlan, string? request, bool canContinue)
    {
        const string original = "Prepara la copia.";
        var source = new SemanticEntity(Guid.NewGuid(), 1, "Synthetic", "Synthetic instruction.", false, InputNames: []);
        var context = SemanticContext.Empty with { Selected = source };
        var declaration = new TaskExecutionRequest([new(SemanticOperation.COPY_PROMPT, original)], [], TargetReference.SELECTED,
            null, "", [], "", new(null, null, "", "", [], [], [], false), null, false, "", "");
        var pending = TaskRequestCompiler.Compile(declaration, original, context);
        context = context with { PendingPlan = hasPlan ? pending : null, PendingRequest = request };
        var schema = PromptActionProtocol.Schema("Continúa.", context);
        var offered = schema.GetProperty("properties").GetProperty("continuesPendingTask").GetProperty("enum")
            .EnumerateArray().Select(value => value.GetBoolean()).ToArray();
        Assert.Equal(canContinue ? [false, true] : new[] { false }, offered);
    }

    [Fact] public void PublishedProviderReferencesComeFromTheDomainRegistry()
    {
        var schema = PromptActionProtocol.Schema("Prepara el destino.", SemanticContext.Empty);
        var ids = schema.GetProperty("properties").GetProperty("destination").GetProperty("anyOf")[0]
            .GetProperty("enum").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Equal(ProviderRegistry.Providers.Select(provider => provider.ProviderId).ToArray(), ids);
    }

    [Fact] public void TheNewSchemaAcceptsProductRequestsWithoutModelChosenCitationsOrExecutionSteps()
    {
        var schema = PromptActionProtocol.Schema("Prepara la copia.", SemanticContext.Empty);
        Assert.False(schema.GetProperty("properties").TryGetProperty("effects",out _));
        Assert.DoesNotContain("supportingPassage",schema.GetRawText());
        Assert.Equal(UserActions.All.Select(a=>a.Name),schema.GetProperty("properties").GetProperty("commands").GetProperty("items").GetProperty("enum").EnumerateArray().Select(v=>v.GetString()));
    }

    private static JsonElement Element(string json) => JsonSerializer.Deserialize<JsonElement>(json);
    private static readonly JsonElement Packet = Element("""{"model":"qwen3:8b","think":false,"format":{"type":"object"},"messages":[{"role":"user","content":"Synthetic"}]}""");
    private const string Id = "00000000000000000000000000000001";

    [Fact] public void WireProtocolHasAnIdentityAndNoHostDatabaseOrDomainOperations()
    {
        var request = AdaptiveCoreModelTransport.CreateRequest(Packet, Id);
        Assert.Equal("adaptive-inference/1", request.GetProperty("version").GetString());
        Assert.Equal(Id, request.GetProperty("requestId").GetString());
        Assert.False(request.TryGetProperty("state", out _));
        Assert.False(request.TryGetProperty("sourceChoices", out _));
        Assert.Equal(2, request.GetProperty("limits").GetProperty("requests").GetInt32());
    }

    [Theory]
    [InlineData("old", Id, "proposed", 1)]
    [InlineData("adaptive-inference/1", "00000000000000000000000000000002", "proposed", 1)]
    [InlineData("adaptive-inference/1", Id, "completed", 1)]
    [InlineData("adaptive-inference/1", Id, "proposed", 3)]
    public void WrongVersionIdentityStatusOrBudgetNeverBecomeSuccess(string version, string id, string status, int calls)
    {
        var reply = JsonSerializer.SerializeToElement(new { version, requestId = id, status, output = new { title = "Synthetic" }, usage = new { requests = calls, inputTokens = 1, outputTokens = 1 }, error = (string?)null });
        Assert.Throws<PreparationToolException>(() => AdaptiveCoreModelTransport.Decode(reply, AdaptiveCoreModelTransport.CreateRequest(Packet, Id), 0));
    }

    [Fact] public void AProposalPreservesTheActualRepairCountWithoutClaimingBusinessCompletion()
    {
        var reply = Element("""{"version":"adaptive-inference/1","requestId":"00000000000000000000000000000001","status":"proposed","output":{"title":"Synthetic"},"usage":{"requests":2,"inputTokens":13,"outputTokens":7},"error":null}""");
        using var decoded = AdaptiveCoreModelTransport.Decode(reply, AdaptiveCoreModelTransport.CreateRequest(Packet, Id), 0);
        Assert.Equal(2, decoded.RootElement.GetProperty("modelCalls").GetInt32());
        Assert.False(decoded.RootElement.TryGetProperty("taskCompleted", out _));
        Assert.Equal(7, decoded.RootElement.GetProperty("eval_count").GetInt32());
    }

    [Fact] public void TheHostOwnsLiteralEvidenceBindingsAndEveryIndependentOutcome()
    {
        const string message = "Crea una instrucción y prepara su copia.\ntema: energía 🪴";
        var output = Element("""{"commands":["new_prompt","preview_copy"],"source":"NONE","destination":null}""");
        var context=SemanticContext.Empty with {Selected=new(Guid.NewGuid(),1,"Synthetic","Explica <tema>.",false,InputNames:["tema"])};
        var declaration = PromptActionProtocol.Bind(output, message, context);
        Assert.Equal(2, declaration.Effects.Length);
        Assert.Equal("energía 🪴", declaration.Values["tema"]);
        Assert.All(declaration.Effects, effect => Assert.Equal("Crea una instrucción y prepara su copia.", effect.Evidence));
        Assert.Null(declaration.TargetId);
        Assert.Equal(TargetReference.NONE, declaration.TargetReference);
    }

    [Theory]
    [InlineData("{\"effects\":[{\"operation\":\"OPEN_PROMPT\",\"supportingPassage\":999}],\"source\":\"NO_EXISTING_SOURCE\"}")]
    [InlineData("{\"effects\":[{\"operation\":\"OPEN_PROMPT\",\"supportingPassage\":0}],\"source\":\"invented\"}")]
    public void HostRejectsInventedSourceAndEvidence(string output)
        => Assert.Throws<ValidationException>(() => PromptActionProtocol.Bind(Element(output), "Abre la instrucción.", SemanticContext.Empty));

    [Fact] public void ToolSelectionCarriesDescriptionsAndOnlyTheOfferedArgumentSchemas()
    {
        var packet = Element("""{"model":"qwen3:8b","messages":[{"role":"user","content":"Synthetic"}],"tools":[{"function":{"name":"read_note","description":"Reads an existing note; never writes it.","parameters":{"type":"object","properties":{},"required":[],"additionalProperties":false}}}]}""");
        var request = AdaptiveCoreModelTransport.CreateRequest(packet, Id);
        Assert.Equal("tools", request.GetProperty("mode").GetString());
        Assert.Equal("object", request.GetProperty("outputSchema").GetProperty("type").GetString());
        var offered = request.GetProperty("outputSchema").GetProperty("anyOf")[0];
        Assert.Contains("never writes", offered.GetProperty("description").GetString());
        Assert.Equal("read_note", offered.GetProperty("properties").GetProperty("toolName").GetProperty("enum")[0].GetString());
    }
}
