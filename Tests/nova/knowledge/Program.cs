using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

if (args.Length != 2) throw new ArgumentException("Pass the synthetic dataset and an owned report directory.");
var root = Directory.GetCurrentDirectory(); var output = Path.GetFullPath(args[1]);
if (!output.StartsWith(Path.Combine(root, "docs", "evidence") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Report must stay inside workspace docs/evidence.");
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var bytes = File.ReadAllBytes(args[0]); var dataset = JsonSerializer.Deserialize<Dataset>(bytes, options)!;
if (!dataset.SyntheticOnly) throw new ArgumentException("Only synthetic requests are permitted.");
Directory.CreateDirectory(output);
var cache = Path.Combine(root, ".playwright", "vector-knowledge-20261019", "benchmark.capabilities.sqlite");
using var embeddings = new OllamaEmbeddings("http://127.0.0.1:11434");
var graph = CapabilityGraph.FromReflection(); var identity = await embeddings.Identity(default);
var knowledge = new VectorCapabilityKnowledge(embeddings, new(cache), graph);
var rows = new List<object>(); var metrics = new List<(string Split, double VectorRecall, double LexicalRecall, bool Ready)>();
foreach (var item in dataset.Cases)
{
    var watch = Stopwatch.StartNew(); var context = await knowledge.Retrieve(item.Query, default); var elapsed = watch.ElapsedMilliseconds;
    var retrieved = context.Nodes.Where(node => node.Kind == "OPERATION").Select(node => node.Id["operation:".Length..]).ToArray();
    var terms = PromptSearch.Normalize(item.Query).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
    var lexical = graph.Nodes.Where(node => node.Kind == "OPERATION").Select(node => new {
        node.Id, Score = PromptSearch.Normalize(node.Description).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Count(terms.Contains)
    }).OrderByDescending(node => node.Score).ThenBy(node => node.Id, StringComparer.Ordinal).Take(5).Select(node => node.Id["operation:".Length..]).ToArray();
    double Recall(string[] candidates) => (double)item.Relevant.Count(candidates.Contains) / item.Relevant.Length;
    var vectorRecall = Recall(retrieved); var lexicalRecall = Recall(lexical); var ready = context.Status is "READY" or "CACHE_HIT";
    rows.Add(new { item.Id, item.Split, item.Query, item.Relevant, context.Status, retrieved, lexical, vectorRecallAt5 = vectorRecall, lexicalRecallAt5 = lexicalRecall, elapsedMs = elapsed, graphNodes = context.Nodes.Length, graphEdges = context.Edges.Length });
    metrics.Add((item.Split, vectorRecall, lexicalRecall, ready)); Console.WriteLine($"{item.Id} {context.Status} recall@5={vectorRecall:F2} {elapsed}ms");
}
var groups = metrics.GroupBy(metric => metric.Split).Select(group => new {
    split = group.Key, count = group.Count(), vectorRecallAt5 = group.Average(metric => metric.VectorRecall), lexicalRecallAt5 = group.Average(metric => metric.LexicalRecall),
    allReady = group.All(metric => metric.Ready), accepted = group.All(metric => metric.Ready) && group.Average(metric => metric.VectorRecall) >= dataset.MinimumRecallAt5
}).ToArray();
var report = new { status = groups.All(group => group.accepted) ? "PASS" : "FAIL", scope = dataset.Scope, syntheticOnly = true,
    datasetSha256 = Convert.ToHexString(SHA256.HashData(bytes)), identity, graph.Version, nodeCount = graph.Nodes.Length, edgeCount = graph.Edges.Length,
    dataset.MinimumRecallAt5, splits = groups, results = rows, semanticIntentAccuracy = "NOT_TESTED", authority = "Never inferred from retrieval scores", latencyP95 = "NOT_TESTED" };
File.WriteAllText(Path.Combine(output, "retrieval.json"), JsonSerializer.Serialize(report, options));
Console.WriteLine(JsonSerializer.Serialize(new { report.status, report.splits }, options));
if (report.status != "PASS") Environment.ExitCode = 1;
record QueryCase(string Id, string Split, string Query, string[] Relevant);
record Dataset(bool SyntheticOnly, string Scope, double MinimumRecallAt5, QueryCase[] Cases);
