using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

var root = Directory.GetCurrentDirectory();
var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
var folder = Path.Combine(root, ".playwright", "retrieval-" + stamp); Directory.CreateDirectory(folder);
var output = Path.Combine(root, "docs", "evidence", "retrieval-" + stamp); Directory.CreateDirectory(output);
var store = new LocalStore(Path.Combine(folder, "synthetic.sqlite"));
var prompts = new NovaPromptStore(store); var library = new PromptLibrary(prompts, prompts);
using var stock = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "apps/web/public/library/biblioteca-practica.es.json")));
var fixtures = stock.RootElement.GetProperty("templates").EnumerateArray().Take(30).Select(p => library.Create(new(p.GetProperty("title").GetString()!, p.GetProperty("template").GetString()!, p.GetProperty("description").GetString()!))).ToArray();
for (var index = fixtures.Length; index < 1000; index++) library.Create(new("Archivo sintético " + index, "Organiza los datos proporcionados sin inventar información.", "Material de QA."));
var retriever = new SqlitePromptRetriever(store); var source = fixtures[0];
var sourceJson = JsonSerializer.Serialize(source);
double Time(Action operation) { var timer = Stopwatch.StartNew(); operation(); return timer.Elapsed.TotalMilliseconds; }
object Statistics(double[] values) { var sorted = values.Order().ToArray(); return new { count = sorted.Length, p50Ms = sorted[(int)Math.Ceiling(sorted.Length * .5) - 1], p95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], throughputPerSecond = 1000 / sorted.Average() }; }
var rankedRead = new List<double>(); var directRead = new List<double>();
// Warm-up is excluded. Alternate the order to avoid systematically favouring one path.
retriever.ReadSource(source.Id); retriever.Retrieve("", source.Id);
for (var index = 0; index < 30; index++) {
    void Before() => rankedRead.Add(Time(() => { var read = retriever.Retrieve("", source.Id).Single(c => c.Prompt.Id == source.Id).Prompt; if (JsonSerializer.Serialize(read) != sourceJson) throw new Exception("Ranked source mismatch"); }));
    void After() => directRead.Add(Time(() => { if (JsonSerializer.Serialize(retriever.ReadSource(source.Id)) != sourceJson) throw new Exception("Direct source mismatch"); }));
    if (index % 2 == 0) { Before(); After(); } else { After(); Before(); }
}
var queries = fixtures.Select(fixture => {
    RetrievalCandidate[] candidates = []; var milliseconds = Time(() => candidates = retriever.Retrieve(fixture.Title, null));
    var index = Array.FindIndex(candidates, c => c.Prompt.Id == fixture.Id);
    if (candidates.Length > 6 || candidates.Any(c => library.Get(c.Prompt.Id).Revision != c.Prompt.Revision)) throw new Exception("Candidate invariant failed");
    return new { queryHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fixture.Title))), milliseconds, found = index >= 0, reciprocalRank = index >= 0 ? 1d / (index + 1) : 0 };
}).ToArray();
using var process = Process.GetCurrentProcess();
var report = new { status = "COMPLETE", syntheticDataOnly = true, corpusSize = 1000, queryType = "30 exact titles from the shipped example library; not a semantic paraphrase benchmark", observationsPerSourceReadPath = 30,
    originalRankedSourceRead = Statistics(rankedRead.ToArray()), directBoundSourceRead = Statistics(directRead.ToArray()), sourceReadBeforeMs = rankedRead, sourceReadAfterMs = directRead,
    unchangedRetrieval = new { recallAt6 = queries.Count(q => q.found) / (double)queries.Length, mrr = queries.Average(q => q.reciprocalRank), latency = Statistics(queries.Select(q => q.milliseconds).ToArray()), queries },
    workingSetBytes = process.WorkingSet64, processCpuMs = process.TotalProcessorTime.TotalMilliseconds };
File.WriteAllText(Path.Combine(output, "measurements.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { output, report.originalRankedSourceRead, report.directBoundSourceRead, report.unchangedRetrieval.recallAt6, report.unchangedRetrieval.mrr }));
