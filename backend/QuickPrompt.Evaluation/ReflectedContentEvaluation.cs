using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Nova.Evaluation;
using QuickPrompt.Web.Infrastructure;

internal static class ReflectedContentEvaluation
{
    private record Case(string Id, string Message);
    private record Dataset(bool SyntheticOnly, Case[] Cases);
    private sealed class RecordingTransport(IAgentModelTransport inner) : IAgentModelTransport
    {
        public Action<string, JsonElement>? Request { get; set; }
        public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose)
        {
            // Available only in this synthetic evaluator. Keep the actual input
            // and schema, never the model's private thinking response.
            var safe = JsonSerializer.SerializeToNode(packet)!;
            foreach (var message in safe["messages"]!.AsArray()) {
                message!.AsObject().Remove("thinking");
                message.AsObject().Remove("reasoning_content");
            }
            Request?.Invoke(purpose, JsonSerializer.SerializeToElement(safe));
            return inner.Send(packet, ct, purpose);
        }
        public bool UsesThinking(string model) => inner.UsesThinking(model);
        public void Observe(int stage, string content, string? error) => inner.Observe(stage, content, error);
    }
    public static Task Run(string[] args) => RunCore(args, false);
    public static Task RunPreparation(string[] args) => RunCore(args, true);
    private static async Task RunCore(string[] args, bool preparationPath)
    {
        if (args.Length != 2) throw new ArgumentException("Pass synthetic development cases and a new evidence path.");
        var root = Environment.CurrentDirectory; var output = Path.GetFullPath(args[1]);
        if (!output.StartsWith(Path.Combine(root, "docs", "evidence") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || File.Exists(output))
            throw new ArgumentException("Preserve previous evidence; use a new owned report.");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var bytes = await File.ReadAllBytesAsync(args[0]); var data = JsonSerializer.Deserialize<Dataset>(bytes, json)!;
        if (!data.SyntheticOnly || data.Cases.Length is < 1 or > 20 || data.Cases.Any(item => string.IsNullOrWhiteSpace(item.Message) || item.Message.Length > 16000))
            throw new ArgumentException("Bounded synthetic cases required.");
        await using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
        var model = await EvaluationLocalModel.Load(http, "http://127.0.0.1:11434");
        var directory = Path.Combine(Path.GetTempPath(), "nova-reflected-content-" + Guid.NewGuid());
        var store = new LocalStore(Path.Combine(directory, "synthetic.sqlite")); var retriever = new SqlitePromptRetriever(store);
        if (store.QueryTemplates(new()).Total != 0) throw new InvalidOperationException("The owned fixture must start empty.");
        using var transport = new OllamaAgentModelTransport("http://127.0.0.1:11434", modelPolicy: model.Policy);
        var recording = new RecordingTransport(transport);
        var sessions = new AgentSessions();
        var core = new ReflectedSemanticAgent(recording, retriever, new()); var planning = new NovaTaskPlanner(core, retriever, sessions, core);
        var pipeline = new PromptPreparationPipeline(core, new GroundedVariableResolver(), new DeterministicPromptEvaluator());
        var orchestrator = new AgentOrchestrator(core, retriever, core, new GroundedVariableResolver(), new DeterministicPromptEvaluator(),
            new PreparedDraftStore(store), new(), sessions: sessions, candidateSelector: core, taskPlanner: planning);
        var rows = new List<object>(); var failed = false;
        async Task Report(bool complete) => await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new {
            status = complete ? "EXECUTED_REQUIRES_EXTERNAL_REVIEW" : "RUNNING",
            scope = preparationPath ? "ACTUAL_AGENT_PREPARE_ORCHESTRATOR_WITH_EXISTING_SINGLE_CORRECTION_NOT_UI" : "REFLECTED_APPLICATION_INTERPRETATION_GENERATION_AND_COMPILATION_NOT_UI",
            syntheticOnly = true, acceptanceComplete = false, model.Name, model.Digest, datasetSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            ownerAssemblySha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(ReflectedSemanticAgent).Assembly.Location))), rows }, json));
        try
        {
            foreach (var item in data.Cases)
            {
                var stages = new List<object>(); var timer = Stopwatch.StartNew();
                recording.Request = (phase, packet) => stages.Add(new { stage = 900, contract = new { phase, packet }, error = (string?)null });
                transport.SemanticDiagnostic = (stage, content, error) => stages.Add(new { stage, contract = JsonSerializer.Deserialize<JsonElement>(content), error });
                PromptProposal? proposal = null; PreparedDraft? draft = null; SemanticInterpretation? plan = null; string? error = null;
                var session = Guid.NewGuid(); using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                try
                {
                    if (preparationPath) {
                        draft = await orchestrator.Prepare(new(Guid.NewGuid(), item.Message, model.Name, SessionId: session),
                            _ => Task.CompletedTask, (_, _, _) => { }, timeout.Token);
                        plan = draft.Semantic;
                        if (plan?.Creates != true || plan.Executes || plan.Adapts || string.IsNullOrWhiteSpace(draft.TemplateText))
                            throw new ValidationException("La creación no produjo el artefacto pedido mediante el flujo real.");
                    } else {
                    var resolution = await planning.Interpret(item.Message, session, null, null, model.Name, null, timeout.Token);
                    resolution = await planning.Resolve(resolution, item.Message, session, model.Name, null, timeout.Token); plan = resolution.Interpretation;
                    if (!plan.Creates || plan.Executes || plan.Adapts) throw new ValidationException("La petición de creación no produjo el resultado autorizado esperado.");
                    var input = new PrepareInput(Guid.NewGuid(), item.Message, model.Name) { Interpretation = plan };
                    proposal = await core.Generate(input, plan.Preparation(), [], timeout.Token);
                    draft = pipeline.PromptCompilation(input, plan.Preparation(), proposal, []);
                    PromptPreparationPipeline.FinalPreparation(draft, plan.Requirements, plan.Goal);
                    }
                    if (plan!.Intentions.Except(new[] { SemanticOperation.CREATE_DRAFT, SemanticOperation.VALIDATE_DRAFT, SemanticOperation.COMPILE_PROMPT }).Any())
                        throw new ValidationException("La creación añadió efectos no solicitados en este corpus.");
                    if (store.QueryTemplates(new()).Total != 0) throw new InvalidOperationException("Generation must not persist a library record.");
                }
                catch (Exception failure) { error = failure.GetType().Name + ": " + failure.Message; failed = true; }
                rows.Add(new { item.Id, item.Message, status = error is null ? "GENERATED_REQUIRES_EXTERNAL_REVIEW" : "FAIL", elapsedMs = timer.ElapsedMilliseconds, plan, proposal, draft, stages, error });
                Console.WriteLine(JsonSerializer.Serialize(new { item.Id, elapsedMs = timer.ElapsedMilliseconds, error, body = draft?.TemplateText ?? proposal?.TemplateText }));
                await Report(false);
            }
            await Report(true);
        }
        finally { Directory.Delete(directory, true); }
        if (failed) Environment.ExitCode = 2;
    }
}
