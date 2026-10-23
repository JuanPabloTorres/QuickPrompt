using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

// Controlled transport evidence for code boundaries, never Qwen accuracy evidence.
public class Phase5BoundaryTests
{
    private static HttpListener Listen()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start(); return listener;
    }
    private static async Task Reply(HttpListenerContext exchange, object value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { done = true, message = new { content = JsonSerializer.Serialize(value) } });
        exchange.Response.ContentType = "application/json";
        await exchange.Response.OutputStream.WriteAsync(bytes); exchange.Response.Close();
    }

    [Fact]
    public async Task ReviewAndCompilerAssessTheSameExplicitlyDeclaredBody()
    {
        using var listener = Listen();
        using var inference = new PreparationInference(listener.Prefixes.Single(), taskGoals: true, taskResultContract: true);
        const string message = "Crea una instrucción para resumir un documento.";
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", message,
            new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, "Crear"));
        var goal = TaskGoal.Create(message, SemanticContext.Empty, plan, [new(TaskClauseRole.CONTENT, message)]);
        var input = new PrepareInput(Guid.NewGuid(), message, SessionId: Guid.NewGuid()) { Interpretation = plan with { Goal = goal } };
        var understanding = input.Interpretation.Preparation();
        var pending = inference.Generate(input, understanding, [], default);
        var generation = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
        string generationPacket;using (var request = new StreamReader(generation.Request.InputStream)) generationPacket=await request.ReadToEndAsync();
        using var generationJson=JsonDocument.Parse(generationPacket);
        using var generationData=JsonDocument.Parse(generationJson.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        var lastUnit=generationData.RootElement.GetProperty("requestUnits").GetArrayLength()-1;
        await Reply(generation, new { decision = "CREATE_NEW", sourceId = (Guid?)null, title = "Resumen documental", description = "",
            authoringEvidence=new {bodyRanges=new[]{new {start=0,end=lastUnit}},outsideRanges=Array.Empty<object>(),titleRange=(object?)null},
            templateText = "Resume <documento> sin inventar datos.", requiredInputs = new[] { "documento" }, values = Array.Empty<object>(), clarification = "", reason = "Instrucción solicitada." });
        var review = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
        string body; using (var request = new StreamReader(review.Request.InputStream)) body = await request.ReadToEndAsync();
        using var packet = JsonDocument.Parse(body);
        using var material = JsonDocument.Parse(packet.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        var reviewed = material.RootElement.GetProperty("proposedInstruction").GetString()!;
        Assert.Contains("<documento>", reviewed);
        await Reply(review, new { verdict="ACCEPT",instructionScope="FUTURE_TASK_ONLY",applicationWorkflowQuotes = Array.Empty<string>(), requestedTitleQuote=(string?)null, issues = Array.Empty<object>() });
        var proposal = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        var pipeline = new PromptPreparationPipeline(inference, new GroundedVariableResolver(), new DeterministicPromptEvaluator());
        var prepared = pipeline.PromptCompilation(input, understanding, proposal, []);
        Assert.Equal(reviewed, prepared.TemplateText);
        Assert.Equal(proposal, DeterministicPromptEvaluator.Canonicalize(proposal));
        Assert.True(goal.HasContentAssessment(prepared.TemplateText));
        Assert.False(goal.HasContentAssessment(prepared.TemplateText + "\nCambio posterior."));
        Assert.False((goal with { Constraints = goal.Constraints with { LiteralClauses = ["Restricción posterior."] } })
            .HasContentAssessment(prepared.TemplateText));
        var sourceBound = goal with { TargetReferences = [new(Guid.NewGuid(), 7)] };
        sourceBound.RecordContentAssessment(prepared.TemplateText);
        Assert.False((sourceBound with { TargetReferences = [sourceBound.TargetReferences[0] with { Revision = 8 }] })
            .HasContentAssessment(prepared.TemplateText));
        var working = goal.BindWorking(prepared, 1); working.RecordContentAssessment(prepared.TemplateText);
        Assert.False(working.BindWorking(prepared, 2).HasContentAssessment(prepared.TemplateText));
        Assert.False(working.BindWorking(prepared with { Id = Guid.NewGuid() }, 1).HasContentAssessment(prepared.TemplateText));
    }

    [Theory]
    [InlineData("SOURCE_READ", false)]
    [InlineData("SAVE_VERSION_REVIEW", false)]
    [InlineData("SOURCE_READ", true)]
    [InlineData("SAVE_VERSION_REVIEW", true)]
    public async Task ScopeRepairCannotDropAnOriginalResultAndExecuteAReducedGoal(string removed, bool libraryReference)
    {
        using var listener = Listen();
        using var inference = new PreparationInference(listener.Prefixes.Single(), taskGoals: true, taskResultContract: true);
        const string message = "Actualiza la instrucción y solicita revisar una versión.";
        var source = new SemanticEntity(Guid.NewGuid(), 7, "Fuente", "Resume <documento>.", false);
        var context = libraryReference ? SemanticContext.Empty with { Candidates = [source] } : SemanticContext.Empty with { Selected = source };
        var pending = inference.Interpret(message, context, "qwen3:8b", default);
        var range = new { start = 1, end = message.Split(' ').Length };
        foreach (var attempt in new[] { 0, 1 })
        {
            var exchange = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
            using (var request = new StreamReader(exchange.Request.InputStream)) await request.ReadToEndAsync();
            var results = new List<object>();
            if (attempt == 0 || removed != "SOURCE_READ") results.Add(new { kind = "SOURCE_READ", quote = range });
            results.Add(new { kind = "DERIVED_DRAFT", quote = range, edit = new { instruction = "Actualiza la instrucción", shortenTemplate = false,
                requestedLanguage = (string?)null, requestedFormat = (string?)null, exactText = Array.Empty<object>(), exactIdentifiers = Array.Empty<object>() } });
            if (attempt == 0 || removed != "SAVE_VERSION_REVIEW") results.Add(new { kind = "SAVE_VERSION_REVIEW", quote = range });
            object reference = libraryReference ? new { role = "KNOWN_LIBRARY_SOURCE", id = source.Id } : new { role = "CURRENT_OPEN_SOURCE" };
            await Reply(exchange, new { source = reference, requestedResults = results, continuesPendingTask = false, explanation = "Revisar el contenido y la versión." });
        }
        var plan = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(SemanticOutcome.CLARIFICATION, plan.DesiredOutcome);
        Assert.Contains("REPAIR_SCOPE_CHANGED", plan.ValidationNotes!);
        Assert.Equal(new[] { SemanticOperation.ASK_CLARIFICATION }, plan.Intentions);
        Assert.NotNull(plan.Goal);
        Assert.Contains(SemanticOperation.REQUEST_SAVE_VERSION, plan.Goal.RequiredEffects);
        Assert.Contains(SemanticOperation.REQUEST_SAVE_VERSION, plan.DeferredGoals!);
        Assert.Equal(plan.DeferredOutcome, plan.Goal.RequestedOutcome);
        Assert.Equal(source.Id, plan.Goal.TargetReferences.Single().Id);
        Assert.Equal(7, plan.Goal.TargetReferences.Single().Revision);
        Assert.False(SemanticPlanValidator.ToolAllowed(plan, "ui_prepare_creation"));
        var planning = new NovaTaskPlanner(inference, new NoWorkRetriever(), new AgentSessions(), inference);
        var resolution = new SemanticResolution(plan, context, [], null, null);
        Assert.Same(resolution, await planning.Resolve(resolution, message, Guid.NewGuid(), "qwen3:8b", null, default));
    }

    private sealed class NoWorkRetriever : IPromptRetriever
    {
        public RetrievalCandidate[] Retrieve(string query, Guid? sourceId) => throw new InvalidOperationException("A suspended goal cannot retrieve sources.");
        public PromptDocument ReadSource(Guid id) => throw new InvalidOperationException("A suspended goal cannot read a source.");
    }
}
