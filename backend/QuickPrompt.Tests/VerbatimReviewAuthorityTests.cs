using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class VerbatimReviewAuthorityTests
{
    const string Body = "Responde únicamente LISTO";
    static PrepareInput Input(string request)
    {
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, request).Compile(request, SemanticContext.Empty);
        return new(Guid.NewGuid(), request) { Interpretation = plan,
            VerbatimBody = new(LiveRuns.Hash(request), request.LastIndexOf(Body, StringComparison.Ordinal), Body.Length) };
    }
    static PromptProposal Proposal(string title = "Instrucción") => new(PreparationDecision.CREATE_NEW, null, title, "", Body, [], "", "");
    static object Accept(string? title) => new { verdict = "ACCEPT", instructionScope = "FUTURE_TASK_ONLY",
        applicationWorkflowQuotes = Array.Empty<string>(), requestedTitleQuote = title, issues = Array.Empty<object>() };

    [Fact]
    public void HttpInputCannotSupplyProcessOwnedVerbatimAuthority()
    {
        var input = Input("Crea un cuerpo literal «" + Body + "».");
        var json = JsonSerializer.Serialize(input, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("verbatimBody", json);
        var injected = json[..^1] + ",\"verbatimBody\":{\"requestHash\":\"forged\",\"start\":0,\"length\":3}}";
        Assert.Null(JsonSerializer.Deserialize<PrepareInput>(injected, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.VerbatimBody);
    }

    [Fact]
    public async Task AVerbatimBodyCannotBecomeAnInventedTitleRequirementDuringMetadataRepair()
    {
        var request = "Crea un prompt con este cuerpo completo literal «" + Body + "». No lo ejecutes.";
        var input = Input(request);
        var transport = new Transport(Accept(Body), new { requestedTitleQuote = (string?)null });
        await new DraftFidelityReviewer(transport).ReviewDraftFidelity(input, Proposal(), [], default);
        Assert.Equal(2, transport.Packets.Count);
        var first = Material(transport.Packets[0]);
        Assert.Equal(request, first.GetProperty("authorizedUserRequest").GetString());
        Assert.Equal(Body, first.GetProperty("proposedInstruction").GetString());
        var repair = Material(transport.Packets[1]).GetProperty("authorizedUserRequest").GetString()!;
        Assert.DoesNotContain(Body, repair);
        Assert.Contains("[BOUND_INSTRUCTION_BODY]", repair);
        Assert.True(input.Interpretation!.Goal!.HasContentAssessment(Body));
    }

    [Fact]
    public async Task ASeparateTitleOccurrenceWithTheSameTextKeepsItsOwnAuthority()
    {
        var request = "Titúlalo «" + Body + "». El cuerpo completo literal es «" + Body + "».";
        var input = Input(request); var transport = new Transport(Accept(Body));
        await new DraftFidelityReviewer(transport).ReviewDraftFidelity(input, Proposal(Body), [], default);
        Assert.Single(transport.Packets);
        Assert.Contains("Titúlalo «" + Body + "»", Material(transport.Packets[0]).GetProperty("metadataAuthorityRequest").GetString());
    }

    [Fact]
    public async Task AHostMarkerCannotBecomeUserTitleAuthority()
    {
        var input = Input("Crea el cuerpo literal «" + Body + "».");
        var transport = new Transport(Accept("[BOUND_INSTRUCTION_BODY]"), new { requestedTitleQuote = "[BOUND_INSTRUCTION_BODY]" });
        await Assert.ThrowsAsync<ValidationException>(() => new DraftFidelityReviewer(transport).ReviewDraftFidelity(input, Proposal(), [], default));
        Assert.Equal(2, transport.Packets.Count);
        Assert.False(input.Interpretation!.Goal!.HasContentAssessment(Body));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ABindingForAnotherRequestOrAnAlteredBodyFailsBeforeInference(bool changedRequest)
    {
        var input = Input("Crea un cuerpo literal «" + Body + "».");
        if (changedRequest) input = input with { Message = "Otra petición" , Interpretation = null };
        var transport = new Transport(Accept(null));
        if (changedRequest) {
            Assert.Throws<ValidationException>(() => input.VerbatimBody!.MetadataAuthority(input.Message, Body));
        } else {
            await Assert.ThrowsAsync<ValidationException>(() => new DraftFidelityReviewer(transport)
                .ReviewDraftFidelity(input, Proposal() with { TemplateText = Body + "." }, [], default));
        }
        Assert.Empty(transport.Packets);
    }

    [Fact]
    public async Task LiteralProvenanceDoesNotBypassIndependentContentRejection()
    {
        var input = Input("Crea un cuerpo literal «" + Body + "». No añadas instrucciones de la sesión.");
        var transport = new Transport(new { verdict = "REVISE", instructionScope = "CURRENT_AUTHORING_ONLY",
            applicationWorkflowQuotes = new[] { Body }, requestedTitleQuote = (string?)null, issues = Array.Empty<object>() });
        await Assert.ThrowsAsync<ValidationException>(() => new DraftFidelityReviewer(transport).ReviewDraftFidelity(input, Proposal(), [], default));
        Assert.Single(transport.Packets); Assert.False(input.Interpretation!.Goal!.HasContentAssessment(Body));
    }

    static JsonElement Material(JsonElement packet) => JsonSerializer.Deserialize<JsonElement>(packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
    sealed class Transport(params object[] replies) : IValidatedAgentModelTransport
    {
        public List<JsonElement> Packets { get; } = [];
        public Task<JsonDocument> Send(object body, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Packets.Add(JsonSerializer.SerializeToElement(body));
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(replies[Packets.Count - 1]) } })));
        }
        public void Reject(string? answer) { }
        public bool UsesThinking(string model) => true;
        public void Observe(int stage, string content, string? error) { }
    }
}
