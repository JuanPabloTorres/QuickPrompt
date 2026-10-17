using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

internal static class AgenticEvaluation
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static async Task Run(string[] args)
    {
        if (args.Length < 3 || args[2] is not ("development" or "validation" or "holdout")) throw new ArgumentException("--agentic dataset report development|validation|holdout");
        var bytes = File.ReadAllBytes(args[0]); var dataset = JsonSerializer.Deserialize<EvalDataset>(bytes, Json)!;
        if (args[2] == "holdout" && args.Length > 3) throw new ArgumentException("Holdout must be evaluated as a complete frozen split.");
        var cases = dataset.Scenarios.Where(c => c.Split == args[2] && (args.Length < 4 || c.Id == args[3])).ToArray();
        var directory = Path.Combine(Path.GetTempPath(), "nova-evaluation-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        var inferenceObservations = new List<InferenceObservation>();
        using var inference = new PreparationInference("http://127.0.0.1:11434", inferenceObservations.Add);
        using var http = new HttpClient { BaseAddress = new("http://127.0.0.1:11434"), Timeout = TimeSpan.FromSeconds(5) };
        var modelInfo = await http.GetStringAsync("api/tags"); var ollamaVersion = await http.GetStringAsync("api/version");
        var samples = new List<ResourceSample>(); using var sampling = new CancellationTokenSource();
        var sampler = Task.Run(async () => { while (!sampling.IsCancellationRequested) { try { using var ps = JsonDocument.Parse(await http.GetStringAsync("api/ps", sampling.Token));
            var models = ps.RootElement.GetProperty("models").EnumerateArray().ToArray(); long Ram() { long ram = 0; foreach (var p in Process.GetProcessesByName("ollama")) { using (p) ram += p.WorkingSet64; } return ram; }
            lock (samples) samples.Add(new(DateTime.UtcNow, Ram(), models.Sum(m => m.TryGetProperty("size_vram", out var v) ? v.GetInt64() : 0)));
        } catch (Exception e) when (e is HttpRequestException or OperationCanceledException or InvalidOperationException) { } try { await Task.Delay(1000, sampling.Token); } catch (OperationCanceledException) { } } });
        var results = new List<EvalResult>(); var retrievalBenchmarks = new List<RetrievalBenchmark>();
        void Report(bool complete)
        {
            var completed = results.Count; var latencies = results.Select(r => r.Latency).Order().ToArray();
            var critical = results.All(r => r.UnauthorizedMutations == 0 && r.AcceptedHallucinatedIds == 0 && r.DataPreserved);
            var retrieval = results.Where(r => r.RetrievalRank is not null).ToArray();
            ResourceSample[] resourceCopy; lock (samples) resourceCopy = samples.ToArray();
            var report = new { status = !complete ? "RUNNING" : critical ? "MEASURED" : "CRITICAL_FAIL", split = args[2], dataset.Version, datasetSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                contractVersion = AgentOrchestrator.ContractVersion, model = "qwen3:8b", modelInfo = JsonSerializer.Deserialize<object>(modelInfo), ollamaVersion = JsonSerializer.Deserialize<object>(ollamaVersion),
                binaries = new { applicationSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(AgentOrchestrator).Assembly.Location))),
                    infrastructureSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(PreparationInference).Assembly.Location))) },
                parameters = new { version = "nova-ollama-parameters-v2", temperature = 0, intentContext = 8192, generationContext = 12288, intentMaxTokens = 500, generationMaxTokens = 2500,
                    sourceEditor = new { temperature = .2, num_ctx = 8192, num_predict = 1800, contract = "existing-local-editor-v1" },
                    semanticSelection = new { temperature = 0, num_ctx = 8192, num_predict = 500, maxCandidates = 6, maxSelected = 3 }, maxTools = 5, maxRevision = 1 },
                tokenUsage = new { promptTokens = inferenceObservations.Sum(o => o.PromptTokens ?? 0), generatedTokens = inferenceObservations.Sum(o => o.GeneratedTokens ?? 0), measuredCalls = inferenceObservations.Count(o => o.PromptTokens is not null), limitation = "Intent and schema-generation calls only; the existing source-editor/execution interfaces do not expose token counts." },
                hardware = new { os = Environment.OSVersion.ToString(), logicalProcessors = Environment.ProcessorCount },
                completed, total = cases.Length, metrics = new {
                    intentAccuracy = completed == 0 ? 0 : results.Count(r => r.IntentCorrect) / (double)completed,
                    toolCallAccuracy = completed == 0 ? 0 : results.Count(r => r.ToolsCorrect) / (double)completed,
                    schemaValidity = completed == 0 ? 0 : results.Count(r => r.SchemaValid) / (double)completed,
                    taskCompletionRate = completed == 0 ? 0 : results.Count(r => r.OutcomeCorrect) / (double)completed,
                    recallAt3 = retrieval.Length == 0 ? (double?)null : retrieval.Count(r => r.RetrievalRank is >= 1 and <= 3) / (double)retrieval.Length,
                    mrr = retrieval.Length == 0 ? (double?)null : retrieval.Average(r => r.RetrievalRank is > 0 ? 1d / r.RetrievalRank.Value : 0),
                    variableResolutionAccuracy = results.Where(r => r.VariableCorrect is not null).Select(r => r.VariableCorrect!.Value ? 1d : 0).DefaultIfEmpty().Average(),
                    unauthorizedMutationCount = results.Sum(r => r.UnauthorizedMutations), unauthorizedExternalSendCount = 0,
                    acceptedHallucinatedPromptIdCount = results.Sum(r => r.AcceptedHallucinatedIds), dataPreservation = critical,
                    p50Ms = Percentile(latencies, .5), p95Ms = Percentile(latencies, .95),
                    timePerTool = results.SelectMany(r => r.Tools).GroupBy(t => t.Tool).Select(g => new { tool = g.Key, count = g.Count(), meanMs = g.Average(t => t.DurationMs), maxMs = g.Max(t => t.DurationMs) }) },
                resourceSamples = resourceCopy.Length, sampledPeakOllamaRamBytes = resourceCopy.Select(s => s.Ram).DefaultIfEmpty().Max(), sampledPeakModelVramBytes = resourceCopy.Select(s => s.Vram).DefaultIfEmpty().Max(),
                retrievalBenchmarks, results,
                limits = "Synthetic local tasks only. MEASURED is not acceptance: inspect intent, tools, task assertions and critical thresholds. Same Qwen is not an independent semantic judge. No external sending tool exists. Resource peaks are sampled at 1s; API size_vram is model allocation, not total GPU utilization. Controlled faults are separate from real inference. Holdout is never used for tuning." };
            File.WriteAllText(args[1], JsonSerializer.Serialize(report, Json));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!); Report(false);
        try
        {
            foreach (var scenario in cases)
            {
                var store = new LocalStore(Path.Combine(directory, scenario.Id + ".sqlite")); var adapter = new NovaPromptStore(store); var library = new PromptLibrary(adapter, adapter);
                var fixtures = new Dictionary<string, PromptDocument> {
                    ["arch"] = library.Create(new("Arquitectura", "Revisa la arquitectura de <proyecto>. Evalúa límites, dependencias y pruebas. No inventes requisitos.", "Diseño de sistemas y APIs.", Defaults: new() { ["proyecto"] = "API de libros en .NET 8 con SQLite y pruebas de integración" })),
                    ["summary"] = library.Create(new("Resumen", "Resume <texto> en tres frases. No inventes hechos.")),
                    ["letters"] = library.Create(new("Cartas profesionales", "Escribe una carta para <puesto>. No inventes experiencia.")) };
                if (scenario.Family == "ambiguity") fixtures["letters2"] = library.Create(new("Cartas de presentación", "Escribe una carta para <puesto>."));
                if (scenario.Family == "duplicate") { fixtures["duplicate"] = library.Create(new("Resumen duplicado", "Resume <texto>.")); fixtures["duplicate2"] = library.Create(new("Resumen duplicado", "Resume <texto>.")); }
                var retriever = new SqlitePromptRetriever(store); var drafts = new PreparedDraftStore(store); var runs = new AgentRunStore(store); var sessions = new AgentSessions();
                var sessionId = Guid.NewGuid(); var selected = scenario.Source is { } source && fixtures.TryGetValue(source, out var s) ? s : null;
                if (scenario.ExplicitSource && selected is not null) sessions.Put(new(sessionId, selected, null, scenario.SessionValues ?? [], DateTime.UtcNow));
                var faults = new ControlledFault(); IIntentInterpreter interpreter = scenario.Mode == "controlled-fault" ? faults : inference;
                IPromptGenerator generator = scenario.Mode == "controlled-fault" ? faults : inference;
                using var executionClient = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan };
                var executor = new LocalPreparedExecutor(new OllamaClient(executionClient));
                var diagnostics = new PreparationDiagnostics();
                var engine = new AgentOrchestrator(interpreter, retriever, generator, new GroundedVariableResolver(), new DeterministicPromptEvaluator(), drafts, new(), runs, sessions, executor, diagnostics: diagnostics,
                    candidateSelector: scenario.Mode == "controlled-fault" ? null : inference);
                string Snapshot() => JsonSerializer.Serialize(new { templates = store.Templates(), versions = store.Templates().SelectMany(t => store.Versions(t.Id)).ToArray(), history = store.History(), executions = store.Executions() }, Json);
                var before = Snapshot(); var timer = Stopwatch.StartNew(); PreparedDraft? draft = null; string? error = null;
                var input = new PrepareInput(Guid.NewGuid(), scenario.Message, SessionId: sessionId);
                try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(185)); draft = await engine.Prepare(input, _ => Task.CompletedTask, (_, _, _) => { }, timeout.Token); }
                catch (Exception e) { error = e is PreparationToolException tool ? tool.Code : e.GetType().Name + ": " + e.Message; }
                if (diagnostics.Get(input.OperationId) is { } diagnostic)
                {
                    var diagnosticDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, Path.GetFileNameWithoutExtension(args[1]) + "-synthetic-drafts");
                    Directory.CreateDirectory(diagnosticDirectory); File.WriteAllText(Path.Combine(diagnosticDirectory, scenario.Id + ".json"), JsonSerializer.Serialize(diagnostic, Json));
                }
                var run = runs.Get(input.OperationId)!; var state = draft?.State.ToString() ?? "FAILED";
                var expected = Enum.Parse<PreparationIntent>(scenario.ExpectedIntent); var actualIntent = run.UserIntent;
                var ids = fixtures.Values.Select(f => f.Id).ToHashSet(); var acceptedUnknown = draft?.SourceId is { } accepted && !ids.Contains(accepted) ? 1 : 0;
                var preserved = before == Snapshot(); var variableCorrect = scenario.Family is "known" or "multiturn" ? draft?.CompiledText is { } compiled && !compiled.Contains("<texto>") && draft.Values.Any(v => v.Name == "texto") : scenario.Family == "missing" ? draft?.MissingVariables.Contains("texto") == true : (bool?)null;
                var tools = run.Tools; var expectedTools = scenario.ExpectedTools;
                var actualTools = tools.Select(t => t.Tool).ToArray();
                var toolsCorrect = (scenario.ExpectedToolSequences ?? [expectedTools]).Any(sequence => actualTools.SequenceEqual(sequence));
                if (scenario.ExpectedOutcome == "FAILED") toolsCorrect &= tools.LastOrDefault()?.Outcome == "FAILED";
                var sourceCorrect = selected is null || scenario.Family is "ambiguity" or "duplicate" || draft?.SourceId == selected.Id;
                var outcome = state == scenario.ExpectedOutcome && sourceCorrect && variableCorrect != false && preserved && acceptedUnknown == 0;
                if (scenario.Family == "execute" && string.IsNullOrWhiteSpace(draft?.LocalResponse)) outcome = false;
                if (scenario.Family == "adapt" && draft?.TemplateText == selected?.TemplateText) outcome = false;
                int? rank = selected is null || scenario.ExplicitSource ? null : Array.IndexOf(run.RetrievedPromptIds, selected.Id) + 1;
                results.Add(new(scenario.Id, scenario.Split, scenario.Family, scenario.Mode, scenario.ExpectedIntent, actualIntent.ToString(), actualIntent == AgentRouting.Intent(expected),
                    expectedTools, actualTools, toolsCorrect, scenario.ExpectedPromptIds, run.RetrievedPromptIds,
                    scenario.ExpectedOutcome, state, outcome, draft is not null || scenario.Mode == "controlled-fault", variableCorrect, preserved, preserved ? 0 : 1, acceptedUnknown, rank, timer.ElapsedMilliseconds, tools, error, scenario.ExpectedToolSequences));
                if (selected is not null && !scenario.ExplicitSource) retrievalBenchmarks.Add(Benchmark(store, retriever, scenario.Message, selected.Id, scenario.Id));
                Console.WriteLine($"{scenario.Id} {scenario.Split} {(outcome ? "TASK_OK" : "TASK_MISS")} intent={actualIntent} state={state} {timer.ElapsedMilliseconds}ms"); Report(false);
            }
        }
        finally { sampling.Cancel(); await sampler; Report(true); }
    }
    private static long Percentile(long[] values, double p) => values.Length == 0 ? 0 : values[(int)Math.Ceiling(values.Length * p) - 1];
    private static RetrievalBenchmark Benchmark(LocalStore store, IPromptRetriever retriever, string query, Guid target, string id)
    {
        var timer = Stopwatch.StartNew(); var lexical = retriever.Retrieve(query[..Math.Min(200, query.Length)], null); var lexicalMs = timer.Elapsed.TotalMilliseconds;
        using var db = new SqliteConnection("Data Source=:memory:"); db.Open(); using var cmd = db.CreateCommand();
        try
        {
            cmd.CommandText = "CREATE VIRTUAL TABLE corpus USING fts5(id UNINDEXED,title,body,tokenize='unicode61 remove_diacritics 2')"; cmd.ExecuteNonQuery();
            foreach (var prompt in store.Templates()) { cmd.CommandText = "INSERT INTO corpus VALUES($id,$title,$body)"; cmd.Parameters.Clear(); cmd.Parameters.AddWithValue("$id", prompt.Id.ToString()); cmd.Parameters.AddWithValue("$title", prompt.Title); cmd.Parameters.AddWithValue("$body", prompt.Description + " " + prompt.Template); cmd.ExecuteNonQuery(); }
            var terms = System.Text.RegularExpressions.Regex.Matches(query, @"[\p{L}\p{N}]+").Select(m => "\"" + m.Value + "\"").Take(30).ToArray();
            cmd.CommandText = "SELECT id FROM corpus WHERE corpus MATCH $query ORDER BY bm25(corpus,0,4,2) LIMIT 6"; cmd.Parameters.Clear(); cmd.Parameters.AddWithValue("$query", string.Join(" OR ", terms));
            timer.Restart(); using var reader = cmd.ExecuteReader(); var found = new List<Guid>(); while (reader.Read()) found.Add(Guid.Parse(reader.GetString(0)));
            return new(id, Array.FindIndex(lexical, c => c.Prompt.Id == target) + 1, found.IndexOf(target) + 1, lexicalMs, timer.Elapsed.TotalMilliseconds, true);
        }
        catch (SqliteException) { return new(id, Array.FindIndex(lexical, c => c.Prompt.Id == target) + 1, 0, lexicalMs, 0, false); }
    }
    private sealed class ControlledFault : IIntentInterpreter, IPromptGenerator
    {
        public Task<Understanding> Understand(string message, string model, CancellationToken ct) => Task.FromResult(new Understanding(PreparationIntent.CREATE_PROMPT, "", ""));
        public Task<PromptProposal> Generate(PrepareInput input, Understanding understanding, RetrievalCandidate[] candidates, CancellationToken ct) => throw new PreparationToolException("CONTROLLED_FAULT", "propose_new_prompt", "Synthetic fault; no real inference.");
    }
    private record EvalDataset(string Version, EvalCase[] Scenarios);
    private record EvalCase(string Id, string Split, string Family, string Message, string ExpectedIntent, string? Source, string[] ExpectedTools, string[] ExpectedPromptIds, string ExpectedOutcome, bool ExplicitSource, Dictionary<string, string>? SessionValues, string Mode, string[][]? ExpectedToolSequences = null);
    private record ResourceSample(DateTime At, long Ram, long Vram);
    private record RetrievalBenchmark(string Id, int LexicalRank, int Fts5Rank, double LexicalMs, double Fts5Ms, bool Available);
    private record EvalResult(string Id, string Split, string Family, string Mode, string ExpectedIntent, string ActualIntent, bool IntentCorrect,
        string[] ExpectedTools, string[] ActualTools, bool ToolsCorrect, string[] ExpectedPromptIds, Guid[] ActualPromptIds,
        string ExpectedOutcome, string ActualOutcome, bool OutcomeCorrect, bool SchemaValid, bool? VariableCorrect, bool DataPreserved,
        int UnauthorizedMutations, int AcceptedHallucinatedIds, int? RetrievalRank, long Latency, AgentToolResult[] Tools, string? Error, string[][]? ExpectedToolSequences = null);
}
