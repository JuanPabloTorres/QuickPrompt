using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringScopeContinuationTests
{
    [Fact]
    public async Task TwoMessagesWithAssignmentsCannotSendAnUnboundEarlierValueToTheAuthor()
    {
        const string previous = "Resume <documento>. Documento: DATO_ANTERIOR_SINTETICO";
        const string current = "Continúa. Documento: DATO_ACTUAL_SINTETICO";
        var envelope = new PrepareInput(Guid.NewGuid(), current) { AuthorizedPendingRequest = previous };
        var plan = new LiveCommand(SemanticOperation.CREATE_DRAFT, envelope.TaskRequest) { InputNames = ["documento"] }.Compile(envelope.TaskRequest, SemanticContext.Empty);
        var input = envelope with { Interpretation = plan,
            InputScope = UserInputScope.Bind(current, null, null, ["documento"], [new("documento", "DATO_ACTUAL_SINTETICO", "DATO_ACTUAL_SINTETICO")]) };
        var transport = new ForbiddenTransport(); var dependencies = new AuthoringReviewTestFixture.ForbiddenDependencies();
        var generator = new PromptDraftGenerator(transport, dependencies, dependencies, new DraftFidelityReviewer(transport));
        PromptProposal? proposal = null;
        var failure = await Record.ExceptionAsync(async () => proposal = await generator.GenerateAfterSyntheticReview(input, plan.Preparation(), [], default));
        Assert.False(transport.SawEarlierData);
        Assert.Null(failure); Assert.NotNull(proposal); Assert.Equal(PreparationDecision.ASK_CLARIFICATION, proposal.Decision);
        Assert.Equal("AUTHORING_INPUT_SCOPE_NEEDS_CONFIRMATION", proposal.Reason); Assert.Equal(0, transport.Calls);
    }

    private sealed class ForbiddenTransport : IValidatedAgentModelTransport
    {
        public int Calls; public bool SawEarlierData;
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            Calls++; SawEarlierData = JsonSerializer.Serialize(packet).Contains("DATO_ANTERIOR_SINTETICO", StringComparison.Ordinal);
            throw new InvalidOperationException("The author must not be reached while a message has unbound case data.");
        }
        public bool UsesThinking(string model) => false;
        public void Observe(int stage, string content, string? code) { }
        public void Reject(string? answer) { }
    }
}
