using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ProhibitionScopeTransportTests
{
    [Theory]
    [InlineData("No inventes información.", "No guardes el borrador.")]
    [InlineData("Never cite nonexistent sources.", "Do not archive the record.")]
    public async Task RecognizedProhibitionsReachTheExistingScopeOwnerBeforeFinalRequirements(string future, string workflow)
    {
        var request = "Create an instruction to review <material>. " + future + " " + workflow;
        var transport = new Transport(future, workflow);
        var interpreter = new SemanticIntentInterpreter(transport, new SemanticCandidateSelector(transport));
        var result = await interpreter.ExtractContent(request, SemanticContext.Empty, "qwen3:8b", default);
        Assert.Equal(new[] { future, workflow }, result.UserProhibitions);
        Assert.Equal(new[] { future }, result.LiteralClauses);
        Assert.Equal(2, transport.Calls);
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", "",
            new([], true, null, null, result.LiteralClauses, [], false, result.UserProhibitions), SemanticOutcome.NEW_DRAFT, [], false, "", null, ""));
        Assert.Equal(new[] { future }, plan.Requirements.Constraints);
        Assert.False(plan.Executes); Assert.False(plan.RequestsApprovalFor(SemanticOperation.REQUEST_SAVE_COPY));
    }

    private sealed class Transport(string future, string workflow) : IValidatedAgentModelTransport
    {
        public int Calls { get; private set; }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            ct.ThrowIfCancellationRequested(); Calls++;
            var wire = JsonSerializer.SerializeToElement(packet);
            object reply;
            if (Calls == 1) {
                Assert.Equal(ValidatedAgentJson.Schema<SemanticConstraintInput>().GetProperty("properties").GetProperty("literalClauses").GetProperty("description").GetString(),
                    wire.GetProperty("format").GetProperty("properties").GetProperty("literalClauses").GetProperty("description").GetString());
                reply = new SemanticConstraintInput([future, workflow], true, null, null, [], [], false, "", "");
            }
            else {
                using var user = JsonDocument.Parse(wire.GetProperty("messages")[1].GetProperty("content").GetString()!);
                Assert.Equal(new[] { future, workflow }, user.RootElement.GetProperty("candidateClauses").EnumerateArray().Select(clause => clause.GetProperty("text").GetString()));
                reply = new { clauses = new[] { new { index = 0, scope = "FUTURE_TASK_INVARIANT" }, new { index = 1, scope = "EDITOR" } } };
            }
            return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(reply, new JsonSerializerOptions(JsonSerializerDefaults.Web)) } })));
        }
        public bool UsesThinking(string model) => true;
        public void Observe(int stage, string content, string? error) { }
        public void Reject(string? answer) { }
    }
}
