using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Accounts.Executions;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Corrupt historical material cannot be written by the execution role. Exercise the bounded decoder directly.</summary>
public sealed class ExecutionUsageContractTests
{
    private static (AccountExecution Job, AccountExecutionResult Result) Material()
    {
        var id = Guid.NewGuid(); var owner = Guid.NewGuid(); var library = Guid.NewGuid(); var prompt = Guid.NewGuid();
        var work = new CompileWork(prompt, 1, "Synthetic", "Explain <tema>.", new() { ["tema"] = "ñ <literal>\n二" });
        return (new() { Id = id, OwnerId = owner, LibraryId = library, Status = ExecutionStatus.Completed,
            Payload = JsonSerializer.Serialize(work, CompileExecutionHandler.Json) },
            new() { ExecutionId = id, OwnerId = owner, LibraryId = library, PromptId = prompt, PromptRevision = 1, Title = "Synthetic", Text = "Explain ñ <literal>\n二." });
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"notWork\":true}")]
    [InlineData("null")]
    [InlineData("\"invalid work object\"")]
    [InlineData("invalid json")]
    public void LegacyOrMalformedStoredWorkDoesNotInventAUsageReceipt(string payload)
    {
        var (job, result) = Material(); job.Payload = payload;
        Assert.Null(ExecutionUsage.Verify(job, result));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("revision")]
    [InlineData("template")]
    [InlineData("values")]
    public void AStoredWorkThatDoesNotReproduceItsResultCannotAuthorizeUse(string changed)
    {
        var (job, result) = Material(); var node = JsonNode.Parse(job.Payload)!;
        if (changed == "revision") node[changed] = 2;
        else if (changed == "values") node[changed]!["tema"] = "Other";
        else node[changed] = "Other";
        job.Payload = node.ToJsonString(); Assert.Null(ExecutionUsage.Verify(job, result));
    }

    [Fact]
    public void SuccessfulDecoderReturnsACopyAndRejectsForeignOrForgottenResults()
    {
        var (job, result) = Material(); var usage = ExecutionUsage.Verify(job, result)!;
        Assert.Equal(job.Id, usage.OperationId); Assert.Equal("ñ <literal>\n二", usage.Values["tema"]);
        usage.Values["tema"] = "mutated"; Assert.Equal("ñ <literal>\n二", ExecutionUsage.Verify(job, result)!.Values["tema"]);
        result.OwnerId = Guid.NewGuid(); Assert.Null(ExecutionUsage.Verify(job, result));
        result.OwnerId = job.OwnerId; job.ForgottenAt = DateTimeOffset.UtcNow; Assert.Null(ExecutionUsage.Verify(job, result));
    }
}
