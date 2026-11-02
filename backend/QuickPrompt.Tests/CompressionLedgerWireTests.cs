using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

/// <summary>Exercises the existing NOVA wire parser plus the compiler. Controlled replies, never semantic-model qualification.</summary>
public sealed class CompressionLedgerWireTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static AuthoringRevisionBasis Basis() => AuthoringEdits.Create(new("Documento", "",
        "Resume <texto>. Resume ese texto.", [], [], [], []));

    private static JsonObject Payload(AuthoringRevisionBasis basis)
    {
        var ledger = new CompressionLedger(basis.BasisHash,
            [new("f0", "summarize", ["texto"], "AFFIRMATIVE", "REQUIRED", new("global", []), [])],
            basis.Fields.SelectMany(field => field.Units).ToDictionary(unit => unit.Id, _ => new[] { "f0" }));
        return (JsonObject)JsonSerializer.SerializeToNode(ledger, Json)!;
    }

    private static CompressionLedger Parse(string json, JsonElement schema)
    {
        using var response = JsonDocument.Parse(JsonSerializer.Serialize(new { message = new { content = json } }));
        var method = typeof(AuthoringCompressionLedger).Assembly.GetType("QuickPrompt.Web.Infrastructure.AgentResponseContent")!
            .GetMethod("ParseContent", BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(typeof(CompressionLedger));
        try { return (CompressionLedger)method.Invoke(null, [response, schema])!; }
        catch (TargetInvocationException failure) when (failure.InnerException is not null) {
            ExceptionDispatchInfo.Capture(failure.InnerException).Throw(); throw;
        }
    }

    [Fact]
    public void ActualNovaParserAcceptsTheClosedWireAndCompilerUsesItsFacts()
    {
        var basis = Basis(); var ledger = Parse(Payload(basis).ToJsonString(), AuthoringCompressionLedger.Schema(basis));
        Assert.Equal("Resume <texto>. ", AuthoringCompressionLedger.Compile(basis, ledger).Task);
    }

    [Theory]
    [InlineData("unit-id-as-fact-id")]
    [InlineData("missing-unit")]
    [InlineData("scope-as-membership")]
    [InlineData("unknown-property")]
    [InlineData("null-dependencies")]
    [InlineData("null-fact")]
    [InlineData("null-units")]
    public void ActualNovaParserRejectsNonContractualWireBeforeTheCompiler(string defect)
    {
        var basis = Basis(); var payload = Payload(basis);
        switch (defect) {
            case "unit-id-as-fact-id": payload["facts"]![0]!["id"] = basis.Fields[0].Units[0].Id; break;
            case "missing-unit": ((JsonObject)payload["units"]!).Remove(basis.Fields[0].Units[0].Id); break;
            case "scope-as-membership": payload["facts"]![0]!["scope"] = new JsonArray("task:1"); break;
            case "unknown-property": payload["approval"] = true; break;
            case "null-dependencies": payload["facts"]![0]!["dependencies"] = null; break;
            case "null-fact": payload["facts"]![0] = null; break;
            default: payload["units"] = null; break;
        }
        Assert.Throws<ValidationException>(() => Parse(payload.ToJsonString(), AuthoringCompressionLedger.Schema(basis)));
        Assert.Equal("Resume <texto>. Resume ese texto.", basis.Content.Task);
    }

    [Theory]
    [InlineData("fact")]
    [InlineData("top")]
    [InlineData("nested-scope")]
    public void DuplicateJsonPropertiesCannotChooseASecondConflictingValue(string level)
    {
        var basis = Basis(); var json = Payload(basis).ToJsonString();
        var duplicate = level switch {
            "fact" => json.Replace("\"polarity\":\"AFFIRMATIVE\"", "\"polarity\":\"NEGATIVE\",\"polarity\":\"AFFIRMATIVE\"", StringComparison.Ordinal),
            "top" => json.Replace("{\"basisHash\":", "{\"basisHash\":\"foreign\",\"basisHash\":", StringComparison.Ordinal),
            _ => json.Replace("\"kind\":\"global\"", "\"kind\":\"conditional\",\"kind\":\"global\"", StringComparison.Ordinal)
        };
        Assert.NotEqual(json, duplicate);
        Assert.Throws<ValidationException>(() => Parse(duplicate, AuthoringCompressionLedger.Schema(basis)));
    }

    [Fact]
    public void TruncatedJsonReturnsTheControlledValidationFailure()
    {
        var basis = Basis(); var incomplete = Payload(basis).ToJsonString()[..^3];
        Assert.Throws<ValidationException>(() => Parse(incomplete, AuthoringCompressionLedger.Schema(basis)));
    }

    [Fact]
    public void CompilerRejectsAStaleBasisEvenWhenThePartialSchemaReaderAcceptsItsString()
    {
        var basis = Basis(); var payload = Payload(basis); payload["basisHash"] = "foreign";
        var parsed = Parse(payload.ToJsonString(), AuthoringCompressionLedger.Schema(basis));
        Assert.Throws<ConflictException>(() => AuthoringCompressionLedger.Compile(basis, parsed));
    }

    [Fact]
    public async Task AmbiguousDiscoveryStopsTheActualGeneratorBeforeWriterAndApproval()
    {
        var input = AuthoringReviewTestFixture.WriterInput("Mejora la claridad sin añadir requisitos.", out var candidates, names: []);
        var provider = new AmbiguousProvider();
        var generator = new PromptDraftGenerator(provider, new AuthoringReviewTestFixture.ForbiddenDependencies());
        await Assert.ThrowsAsync<ValidationException>(() => generator.GenerateAfterSyntheticTask(input,
            input.Interpretation!.Preparation(), candidates, default));
        Assert.Equal(new[] { "DiscoverCurrentAuthorDecision" }, provider.Purposes);
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
        Assert.False(input.Interpretation.Goal.HasHumanContentApproval("contenido"));
    }

    [Fact]
    public async Task CancellationAfterTheReplyWinsBeforeParsingOrStartingTheWriter()
    {
        var input = AuthoringReviewTestFixture.WriterInput("Mejora la claridad sin añadir requisitos.", out var candidates, names: []);
        using var cancelled = new CancellationTokenSource();
        var provider = new AmbiguousProvider { AfterReply = cancelled.Cancel };
        var generator = new PromptDraftGenerator(provider, new AuthoringReviewTestFixture.ForbiddenDependencies());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => generator.GenerateAfterSyntheticTask(input,
            input.Interpretation!.Preparation(), candidates, cancelled.Token));
        Assert.Single(provider.Purposes);
        Assert.Null(input.Interpretation!.Goal!.AuthoringContract);
    }

    private sealed class AmbiguousProvider : IValidatedAgentModelTransport
    {
        internal List<string> Purposes { get; } = [];
        internal Action? AfterReply { get; init; }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? error) { }
        public void Reject(string? answer) { }
        public Task<JsonDocument> Send(object packet, CancellationToken cancellation, string purpose)
        {
            cancellation.ThrowIfCancellationRequested(); Purposes.Add(purpose);
            var result = JsonDocument.Parse(JsonSerializer.Serialize(new { message = new {
                content = "{\"outcome\":\"ASK\",\"outcome\":\"READY\",\"decisionText\":null}" } }));
            AfterReply?.Invoke();
            return Task.FromResult(result);
        }
    }
}
