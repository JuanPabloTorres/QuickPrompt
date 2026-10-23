using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Controlled transport/permission tests, explicitly not real-model comprehension evidence.
public class TaskResultContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateCapabilitiesAreIdempotentAndRepairCannotChangeTheTaskCategory(bool repairSource)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var server = new HttpListener(); server.Prefixes.Add($"http://127.0.0.1:{port}/"); server.Start();
        using var inference = new PreparationInference($"http://127.0.0.1:{port}", taskGoals: true, taskResultContract: true);
        var source = new SemanticEntity(Guid.NewGuid(), 1, "Sintético", "Explica <tema>.", false);
        var pending = inference.Interpret(repairSource ? "Prepara el prompt local." : "Completa todos los campos.", SemanticContext.Empty with { Selected = source }, "qwen3:8b", default);
        for (var attempt = 0; attempt < (repairSource ? 2 : 1); attempt++) {
            var exchange = await server.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
            using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream);
            var properties = packet.RootElement.GetProperty("format").GetProperty("properties");
            Assert.False(properties.TryGetProperty("clarificationQuestion", out _));
            if (attempt == 1) {
                var kinds = properties.GetProperty("requestedResults").GetProperty("items").GetProperty("anyOf").EnumerateArray()
                    .SelectMany(branch => branch.GetProperty("properties").GetProperty("kind").GetProperty("enum").EnumerateArray().Select(value => value.GetString())).ToArray();
                Assert.Contains("LOCAL_READY", kinds); Assert.Contains("CLARIFICATION", kinds);
                Assert.DoesNotContain("NEW_DRAFT", kinds); Assert.DoesNotContain("LOCAL_RESPONSE", kinds); Assert.DoesNotContain("SAVE_COPY_REVIEW", kinds);
            }
            var result = new { kind = repairSource ? "LOCAL_READY" : "FILLED_INPUTS", quote = new { start = 1, end = 4 } };
            var contract = new { source = new { role = repairSource && attempt == 0 ? "NO_SOURCE_NEEDED" : "CURRENT_OPEN_SOURCE" },
                requestedResults = repairSource ? new[] { result } : new[] { result, result }, continuesPendingTask = false, explanation = "Contrato de prueba controlada." };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content = JsonSerializer.Serialize(contract) } });
            exchange.Response.ContentType = "application/json"; await exchange.Response.OutputStream.WriteAsync(bytes); exchange.Response.Close();
        }
        var plan = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { repairSource ? SemanticOperation.PREPARE_LOCAL_EXECUTION : SemanticOperation.FILL_VARIABLES }, plan.Goal!.RequestedEffects);
        Assert.DoesNotContain(SemanticOperation.CREATE_DRAFT, plan.Intentions); Assert.DoesNotContain(SemanticOperation.EXECUTE_LOCAL, plan.Intentions);
    }
    [Fact] public void DependencyReductionCannotHideAnUnrequestedTerminalEffect()
    {
        var requested = new[] { SemanticOperation.SEARCH_PROMPT, SemanticOperation.OPEN_PROMPT, SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.COPY_PROMPT };
        Assert.Equal(new[] { SemanticOperation.PROPOSE_ADAPTATION, SemanticOperation.COPY_PROMPT }, SemanticPlanCompiler.MinimalGoals(requested, TargetReference.NAMED));
        Assert.Equal(new[] { SemanticOperation.CREATE_DRAFT, SemanticOperation.COPY_PROMPT }, SemanticPlanCompiler.MinimalGoals([SemanticOperation.CREATE_DRAFT, SemanticOperation.COPY_PROMPT], TargetReference.NONE));
        Assert.Equal(new[] { SemanticOperation.CREATE_DRAFT, SemanticOperation.REQUEST_SAVE_COPY, SemanticOperation.PREPARE_PROVIDER },
            SemanticPlanCompiler.MinimalGoals([SemanticOperation.CREATE_DRAFT, SemanticOperation.REQUEST_SAVE_COPY, SemanticOperation.PREPARE_PROVIDER], TargetReference.NONE));
    }
    [Theory]
    [InlineData("NEW_DRAFT", false)]
    [InlineData("DERIVED_DRAFT", false)]
    [InlineData("EDITED_DRAFT", false)]
    [InlineData("EDITED_DRAFT", true)]
    [InlineData("MATCHES_ONLY", false)]
    [InlineData("SOURCE_READ", false)]
    [InlineData("PROVIDER_READY", false)]
    public async Task ResultsCompileWithoutImplicitTerminalActions(string kind, bool copyRequested)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var server = new HttpListener(); server.Prefixes.Add($"http://127.0.0.1:{port}/"); server.Start();
        using var inference = new PreparationInference($"http://127.0.0.1:{port}", taskGoals: true, taskResultContract: true);
        var source = new SemanticEntity(Guid.NewGuid(), 1, "Sintético", "Explica <tema>.", false);
        const string request = "Petición sintética de transporte autorizado.";
        var pending = inference.Interpret(request, SemanticContext.Empty with { Selected = source, Candidates = [source] }, "qwen3:8b", CancellationToken.None);
        var exchange = await server.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream);
        var properties = packet.RootElement.GetProperty("format").GetProperty("properties");
        Assert.True(properties.TryGetProperty("requestedResults", out _));
        Assert.False(properties.TryGetProperty("effects", out _)); Assert.False(properties.TryGetProperty("forbidden", out _));
        Assert.False(properties.TryGetProperty("providerId", out _)); Assert.False(properties.TryGetProperty("authoring", out _));
        var results = new List<object>();
        var quote = new { start = 1, end = 5 };
        if (kind is "NEW_DRAFT" or "DERIVED_DRAFT" or "EDITED_DRAFT") results.Add(new { kind, quote,
            edit = new { instruction = "Redactar una explicación útil.", shortenTemplate = false, requestedLanguage = (string?)null, requestedFormat = (string?)null, exactText = Array.Empty<object>(), exactIdentifiers = Array.Empty<object>() } });
        else if (kind == "PROVIDER_READY") results.Add(new { kind, quote, providerId = "claude" });
        else results.Add(new { kind, quote });
        if (copyRequested) results.Add(new { kind = "COPY_READY", quote });
        var contract = new { source = new { role = kind == "NEW_DRAFT" ? "NO_SOURCE_NEEDED" : "CURRENT_OPEN_SOURCE" }, requestedResults = results,
            continuesPendingTask = false, explanation = "Resultado solicitado." };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content = JsonSerializer.Serialize(contract) } });
        exchange.Response.ContentType = "application/json"; await exchange.Response.OutputStream.WriteAsync(bytes); exchange.Response.Close();
        var plan = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(plan.Goal);
        Assert.Equal(copyRequested, plan.Has(SemanticOperation.COPY_PROMPT));
        Assert.Equal(copyRequested, plan.Goal.RequestedEffects.Contains(SemanticOperation.COPY_PROMPT));
        Assert.DoesNotContain(SemanticOperation.EXECUTE_LOCAL, plan.Intentions);
        Assert.DoesNotContain(SemanticOperation.REQUEST_SAVE_COPY, plan.Intentions);
        Assert.Contains(SemanticOperation.EXECUTE_LOCAL, plan.Goal.ForbiddenEffects);
        Assert.Equal(kind == "PROVIDER_READY" ? "claude" : null, plan.ProviderId);
        Assert.True(plan.Goal.AllowsPlan(plan));
        var enlarged = plan with { Intentions = [..plan.Intentions, SemanticOperation.EXECUTE_LOCAL] };
        Assert.False(plan.Goal.AllowsPlan(enlarged)); Assert.False(SemanticPlanValidator.ToolAllowed(enlarged, "ui_execute_local"));
    }

    [Theory]
    [InlineData(SemanticOperation.COPY_PROMPT)]
    [InlineData(SemanticOperation.PREPARE_PROVIDER)]
    [InlineData(SemanticOperation.EXECUTE_LOCAL)]
    [InlineData(SemanticOperation.REQUEST_SAVE_VERSION)]
    public void AddedEffectsCannotPassAStoredGoalEvenIfNotExplicitlyForbidden(SemanticOperation extra)
    {
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", "Crear",
            new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, "Crear"));
        var goal = TaskGoal.Create("Crear", SemanticContext.Empty, plan, [new(TaskClauseRole.CONTENT, "Crear")], [SemanticOperation.CREATE_DRAFT]);
        Assert.Equal(new[] { SemanticOperation.CREATE_DRAFT }, goal.RequestedEffects);
        Assert.Contains(SemanticOperation.COMPILE_PROMPT, goal.RequiredEffects);
        plan = plan with { Goal = goal };
        Assert.False(SemanticPlanValidator.ToolAllowed(plan, "ui_fill_variable"));
        var changed = plan with { Intentions = [..plan.Intentions, extra] };
        Assert.False(goal.AllowsPlan(changed)); Assert.False(SemanticPlanValidator.ToolAllowed(changed, "finish"));
    }
}
