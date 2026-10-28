using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Nova.Evaluation;
using QuickPrompt.Web.Infrastructure;
if (args.FirstOrDefault() == "--module-host") { ModuleHostEvaluation.Run(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--instruction-contracts") {
    if (args.Length != 2) throw new ArgumentException("Pass a new owned docs/nova/contracts directory.");
    var target = Path.GetFullPath(args[1]);
    var allowed = Path.GetFullPath(Path.Combine("docs", "nova", "contracts")) + Path.DirectorySeparatorChar;
    if (!target.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || Directory.Exists(target))
        throw new ArgumentException("Use a new versioned contracts directory; existing artifacts are preserved.");
    Directory.CreateDirectory(target);
    foreach (var contract in PreparationInference.InstructionScopeSchemas())
        File.WriteAllText(Path.Combine(target, contract.Key + ".schema.json"), JsonSerializer.Serialize(contract.Value, new JsonSerializerOptions { WriteIndented = true }));
    File.WriteAllText(Path.Combine(target, "capabilities.json"), JsonSerializer.Serialize(SemanticCapabilityCatalog.All, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    Console.WriteLine("Exported actual C# instruction-scope contracts and reflected capabilities."); return;
}
if (args.FirstOrDefault() == "--reflected-content") { await ReflectedContentEvaluation.Run(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--reflected-preparation") { await ReflectedContentEvaluation.RunPreparation(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--reflected-recovery") { await RecoveryEvaluation.RunReflected(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--reflected-tools") { await ReflectedToolsEvaluation.Run(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--authoring-wire") { await AuthoringWireEvaluation.Run(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--shared-authoring") { await SharedAuthoringEvaluation.Run(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--authoring-review") { await AuthoringReviewEvaluation.Run(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--recovery") { await RecoveryEvaluation.Run(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--generation-diagnostic") { await GenerationDiagnostic.Run(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--planning-diagnostic") { await GenerationDiagnostic.Plan(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--causal-gold") { await RecoveryEvaluation.RunGold(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--fidelity") { await FidelityEvaluation.Run(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--semantic") { await SemanticEvaluation.Run(args.Skip(1).ToArray()); return; }

if (args.FirstOrDefault() == "--agentic") { await AgenticEvaluation.Run(args.Skip(1).ToArray()); return; }
if (args.Length != 2) throw new ArgumentException("Pass a synthetic dataset path and report path.");
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var bytes = File.ReadAllBytes(args[0]); var dataset = JsonSerializer.Deserialize<Dataset>(bytes, json)!;
var observations = new List<InferenceObservation>();
using var inference = new PreparationInference("http://127.0.0.1:11434", observations.Add);
using var client = new HttpClient { BaseAddress = new("http://127.0.0.1:11434"), Timeout = TimeSpan.FromSeconds(5) };
var resourcesBefore = await client.GetStringAsync("api/ps");
var results = new List<object>(); var samples = new List<(string Split, bool Correct, bool Valid, long Ms)>();
void Report(bool complete)
{
    var metrics = samples.GroupBy(s => s.Split).Select(g => new { split = g.Key, count = g.Count(), intentAccuracy = g.Count(s => s.Correct) / (double)g.Count(), jsonValid = g.Count(s => s.Valid) / (double)g.Count() }).ToArray();
    var times = samples.Select(s => s.Ms).Order().ToArray();
    var passed = samples.Count == dataset.Scenarios.Length && samples.Count(s => s.Correct) / (double)samples.Count >= dataset.Thresholds.IntentAccuracy && samples.Count(s => s.Valid) / (double)samples.Count >= dataset.Thresholds.JsonValid;
    File.WriteAllText(args[1], JsonSerializer.Serialize(new { status = complete ? passed ? "PASS" : "FAIL" : "RUNNING", datasetSha256 = Convert.ToHexString(SHA256.HashData(bytes)), dataset.Thresholds,
        model = "qwen3:8b", metrics, count = samples.Count, p50Ms = times.Length == 0 ? 0 : times[(times.Length - 1) / 2], p95Ms = times.Length == 0 ? 0 : times[(int)Math.Ceiling(times.Length * .95) - 1],
        toolCalls = observations.Count(o => o.Kind == "TOOL_VALID"), fallbackCalls = observations.Count(o => o.Kind == "FALLBACK"), rejectedTools = observations.Count(o => o.Kind == "TOOL_REJECTED"),
        resourcesBefore = JsonSerializer.Deserialize<object>(resourcesBefore), peakRamVram = "NOT_TESTED: snapshots are not a sampled peak", results,
        limits = "This dataset measures the production intent interpreter and contract/tool proposal, not generation quality, Recall@K or full E2E success. Holdout was not used to tune code or prompts." }, json));
}
foreach (var scenario in dataset.Scenarios)
{
    var timer = Stopwatch.StartNew(); string? actual = null, error = null;
    try { using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(90)); actual = (await inference.Understand(scenario.Message, "qwen3:8b", cancel.Token)).Intent.ToString(); }
    catch (Exception exception) { error = exception.GetType().Name; }
    var correct = actual == scenario.ExpectedIntent; samples.Add((scenario.Split, correct, error is null, timer.ElapsedMilliseconds));
    results.Add(new { scenario.Id, scenario.Split, scenario.ExpectedIntent, actual, correct, jsonValid = error is null, error, elapsedMs = timer.ElapsedMilliseconds });
    Console.WriteLine($"{scenario.Id} {scenario.Split} {(correct ? "PASS" : "FAIL")} {timer.ElapsedMilliseconds}ms"); Report(false);
}
Report(true);
record Scenario(string Id, string Split, string Message, string ExpectedIntent);
record Thresholds(double IntentAccuracy, double JsonValid);
record Dataset(Thresholds Thresholds, Scenario[] Scenarios);
