using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

public static class SemanticEvaluation
{
    private record Scenario(string Id, string Split, string Family, string Context, string Message, string[] Required, string[] Outcomes, string[] Forbidden, string? MetamorphicGroup);
    private record Dataset(string Version, bool SyntheticOnly, Scenario[] Scenarios);
    public static async Task Run(string[] args)
    {
        if (args.Length < 3) throw new ArgumentException("dataset, report, split (development|validation|holdout|all)");
        using var modelLease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var runtimeHashes = new[] { typeof(PreparationInference).Assembly, typeof(AdaptiveCoreModelTransport).Assembly, typeof(SemanticPlanCompiler).Assembly, typeof(TemplateCompiler).Assembly }
            .ToDictionary(assembly => Path.GetFileName(assembly.Location), assembly => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant());
        using var modelInfo = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
        using var installedModels = JsonDocument.Parse(await modelInfo.GetStringAsync("http://127.0.0.1:11434/api/tags"));
        var modelDigest = installedModels.RootElement.GetProperty("models").EnumerateArray().Single(model => model.GetProperty("name").GetString() == "qwen3:8b").GetProperty("digest").GetString();
        var bytes = File.ReadAllBytes(args[0]); var dataset = JsonSerializer.Deserialize<Dataset>(bytes, json)!;
        if (!dataset.SyntheticOnly) throw new ArgumentException("Synthetic fixtures required.");
        var thresholdPath = Path.Combine(Path.GetDirectoryName(args[0])!, Path.GetFileNameWithoutExtension(args[0]) + "-thresholds.json");
        using var thresholds = JsonDocument.Parse(File.ReadAllBytes(thresholdPath)); var gate = thresholds.RootElement;
        var scenarios = dataset.Scenarios.Where(s => (args[2] == "all" || s.Split == args[2]) && (args.Length < 4 || args[3].Split(',').Contains(s.Id))).ToArray();
        if (scenarios.Length == 0) throw new ArgumentException("No scenarios matched the requested split/IDs.");
        var directory = Path.Combine(Path.GetTempPath(), "nova-semantic-eval-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        try {
            var store = new LocalStore(Path.Combine(directory, "synthetic.sqlite")); var adapter = new NovaPromptStore(store); var library = new PromptLibrary(adapter, adapter);
            var arch = library.Create(new("Arquitectura", "Evalúa la arquitectura de <codigo>. Identifica límites y dependencias. No inventes datos."));
            var mail = library.Create(new("Correos", "Redacta un correo a <persona> sobre <asunto>. No inventes compromisos."));
            var mail2 = library.Create(new("Correos a clientes", "Redacta una respuesta a <cliente>."));
            bool Flag(string key, bool fallback) => bool.TryParse(Environment.GetEnvironmentVariable(key), out var value) ? value : fallback;
            var profile = new { semanticThinking = Flag("NOVA_EVAL_SEMANTIC_THINKING", true), authoringReview = "unchanged", contextualReferenceReasoning = "unchanged" };
            var observations = new List<InferenceObservation>(); using var inference = new PreparationInference("http://127.0.0.1:11434", observations.Add,
                semanticThinking: profile.semanticThinking);
            var rows = new List<object>(); var samples = new List<(Scenario Case, SemanticInterpretation? Plan, bool Correct, bool Critical, long Ms)>();
            foreach (var scenario in scenarios) {
                var a = SemanticEntity.From(arch); var m = SemanticEntity.From(mail); var m2 = SemanticEntity.From(mail2);
                var context = scenario.Context switch {
                    "selected_arch" => SemanticContext.Empty with { Selected = a }, "selected_mail" => SemanticContext.Empty with { Selected = m },
                    "previous_arch" => SemanticContext.Empty with { Selected = m, DraftSource = a, ActiveDraft = "Evalúa <codigo> y sus límites. No inventes datos.", Candidates = [a], Approval = "NOT_GRANTED" },
                    "two_candidates" => SemanticContext.Empty with { Selected = a, DraftSource = a, Candidates = [a, m] },
                    "ambiguous_search" => SemanticContext.Empty with { Candidates = [m, m2] }, _ => SemanticContext.Empty };
                SemanticInterpretation? plan = null; string? error = null; var timer = Stopwatch.StartNew();
                var attempts = new List<object>(); inference.SemanticDiagnostic = (attempt, content, rejected) => attempts.Add(new { attempt, contentHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))), rejected = rejected is not null });
                try { using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(90)); plan = await inference.Interpret(scenario.Message, context, "qwen3:8b", cancel.Token); }
                catch (Exception exception) { error = exception is PreparationToolException tool ? tool.Code : exception.GetType().Name; }
                var actual = plan?.Intentions.Select(op => op.ToString()).ToArray() ?? [];
                var referenceCorrect = scenario.Context != "previous_arch" || plan is not null && (plan.ResolvedTargetId == arch.Id || plan.ResolvedTargetId is null && plan.TargetReference is TargetReference.PREVIOUS_RESULT or TargetReference.ACTIVE_DRAFT);
                if (scenario.Context == "two_candidates") referenceCorrect = plan is not null && (plan.DesiredOutcome == SemanticOutcome.CLARIFICATION || plan.ResolvedTargetId == mail.Id || plan.TargetReference == TargetReference.OTHER_CANDIDATE);
                var correct = plan is not null && referenceCorrect && scenario.Required.All(actual.Contains) && !scenario.Forbidden.Any(actual.Contains) && scenario.Outcomes.Contains(plan.DesiredOutcome.ToString());
                var critical = plan?.Executes == true && !scenario.Required.Contains("EXECUTE_LOCAL") || plan?.ResolvedTargetId is { } id && !context.Ids.Contains(id)
                    || scenario.Context == "ambiguous_search" && plan?.ResolvedTargetId is not null;
                samples.Add((scenario, plan, correct, critical, timer.ElapsedMilliseconds));
                rows.Add(new { scenario.Id, scenario.Split, scenario.Family, expected = new { scenario.Required, scenario.Forbidden, scenario.Outcomes },
                    interpretation = plan is null ? null : new { plan.Intentions, plan.DesiredOutcome, plan.TargetReference, plan.ResolvedTargetId, plan.ProposedSteps, plan.ProviderId },
                    attempts, referenceCorrect, correct, critical, error, elapsedMs = timer.ElapsedMilliseconds });
                Console.WriteLine($"{scenario.Id} {scenario.Family} {(correct && !critical ? "PASS" : "FAIL")} {timer.ElapsedMilliseconds}ms {error}");
                Report(false);
            }
            Report(true);
            void Report(bool complete) {
                var valid = samples.Count(s => s.Plan is not null); var correct = samples.Count(s => s.Correct); var critical = samples.Count(s => s.Critical);
                var metrics = samples.GroupBy(s => s.Case.Family).Select(g => new { family = g.Key, count = g.Count(), correct = g.Count(s => s.Correct), accuracy = g.Count(s => s.Correct) / (double)g.Count() }).ToArray();
                var times = samples.Select(s => s.Ms).Order().ToArray();
                var groups = samples.Where(s => s.Case.MetamorphicGroup is not null).GroupBy(s => s.Case.MetamorphicGroup).Select(g => new { group = g.Key, equivalent = g.All(s => s.Plan?.Adapts == true && !s.Plan.Executes), count = g.Count() }).ToArray();
                double? equivalence = groups.Length == 0 || groups.Any(g => g.count < 2) ? null : groups.Count(g => g.equivalent) / (double)groups.Length;
                var expectedCount = args[2] == "all" ? dataset.Scenarios.Length : gate.GetProperty(args[2]).GetInt32();
                var pass = samples.Count == expectedCount && correct / (double)samples.Count >= gate.GetProperty("semanticAccuracy").GetDouble()
                    && valid / (double)samples.Count >= gate.GetProperty("validPlanRate").GetDouble() && critical == gate.GetProperty("criticalUnauthorizedEffects").GetInt32()
                    && equivalence >= gate.GetProperty("metamorphicEquivalence").GetDouble();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
                File.WriteAllText(args[1], JsonSerializer.Serialize(new { status = complete ? args.Length >= 4 ? "DIAGNOSTIC_COMPLETE" : pass ? "PASS" : "FAIL" : "RUNNING", evaluationComplete = complete, acceptanceComplete = complete && pass && args.Length < 4, dataset.Version, datasetSha256 = Convert.ToHexString(SHA256.HashData(bytes)), split = args[2], model = "qwen3:8b", modelDigest, runtimeHashes, count = samples.Count,
                    profile, contentLogged = false, semanticAccuracy = samples.Count == 0 ? 0 : correct / (double)samples.Count, validPlanRate = samples.Count == 0 ? 0 : valid / (double)samples.Count, criticalErrors = critical,
                    p50Ms = times.Length == 0 ? 0 : times[(times.Length - 1) / 2], p95Ms = times.Length == 0 ? 0 : times[(int)Math.Ceiling(times.Length * .95) - 1], metrics, metamorphicGroups = groups,
                    thresholds = gate, metamorphicEquivalence = equivalence, metamorphicStatus = equivalence is null ? "NOT_TESTED" : "MEASURED",
                    modelCalls = observations.Count(o => o.Kind == "CHAT_USAGE"), modelStages = observations.Where(o => o.Kind != "CHAT_USAGE").GroupBy(o => o.Kind).ToDictionary(g => g.Key, g => g.Count()),
                    promptTokens = observations.Where(o => o.Kind == "CHAT_USAGE").Sum(o => o.PromptTokens ?? 0), generatedTokens = observations.Where(o => o.Kind == "CHAT_USAGE").Sum(o => o.GeneratedTokens ?? 0),
                    boundedPlanRetries = observations.Count(o => o.Kind == "SEMANTIC_PLAN_RETRY"), rows,
                    limits = "Real local model interpretation/plan contracts on synthetic repository-backed context. No execution or generation quality claim; those require separate headed E2E. This report does not estimate population accuracy." }, json));
                if (complete && (args.Length >= 4 ? samples.Any(s => !s.Correct || s.Critical) : !pass)) Environment.ExitCode = 1;
            }
        } finally { Directory.Delete(directory, true); }
    }
}
