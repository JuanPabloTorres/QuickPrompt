using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;

// Test the existing shared editor's creation capability without a second prompt
// implementation. It never opens a database, browser, or application tool.
internal static class SharedAuthoringEvaluation
{
    private sealed record Scenario(string Id, string Message);
    private sealed record Dataset(bool SyntheticOnly, Scenario[] Cases);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task Run(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Use --shared-authoring owned-synthetic-fixture.json new-report.json");
        var root = Path.GetFullPath(Environment.CurrentDirectory);
        var fixturePath = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]);
        if (!fixturePath.StartsWith(Path.Combine(root, ".playwright") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !output.StartsWith(Path.Combine(root, "docs", "evidence") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || File.Exists(output)) throw new ArgumentException("Use owned synthetic inputs and preserve previous evidence.");
        var bytes = await File.ReadAllBytesAsync(fixturePath);
        var dataset = JsonSerializer.Deserialize<Dataset>(bytes, Json) ?? throw new ArgumentException("Invalid dataset");
        if (!dataset.SyntheticOnly || dataset.Cases.Length == 0 || dataset.Cases.Select(item => item.Id).Distinct().Count() != dataset.Cases.Length
            || dataset.Cases.Any(item => string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Message) || item.Message.Length > 16000))
            throw new ArgumentException("Explicit synthetic, uniquely identified bounded requests required.");
        await using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var gateway = new HttpClient { BaseAddress = new("http://127.0.0.1:11434"), Timeout = TimeSpan.FromSeconds(5) };
        var modelIdentity = JsonSerializer.Deserialize<JsonElement>(await gateway.GetStringAsync("api/tags"));
        var model = modelIdentity.GetProperty("models").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "qwen3:8b");
        var rows = new List<object>(); var failed = false;
        async Task Save(string state) => await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new {
            state, scope = "EXISTING_SHARED_EDITOR_CREATE_COMPONENT_NOT_AGENT_UI", syntheticOnly = true,
            acceptanceComplete = false, fixture = Path.GetRelativePath(root, fixturePath), fixtureSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            model = model.Clone(), contextLength = 12288, timeoutSeconds = 90, allowRevision = false,
            assemblySha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(OllamaClient).Assembly.Location))), rows
        }, Json));
        await Save("RUNNING");
        foreach (var item in dataset.Cases) {
            using var observer = new Trace();
            using var http = new HttpClient(observer) { Timeout = Timeout.InfiniteTimeSpan };
            var editor = new OllamaClient(http, "http://127.0.0.1:11434", TimeSpan.FromSeconds(90), 12288);
            var timer = Stopwatch.StartNew();
            try {
                var proposal = await editor.Propose("create", item.Message, null, "qwen3:8b", CancellationToken.None, allowRevision: false);
                rows.Add(new { item.Id, item.Message, status = "VALID_ARTIFACT_REQUIRES_EXTERNAL_REVIEW", elapsedMs = timer.ElapsedMilliseconds,
                    proposal, calls = observer.Calls });
                Console.WriteLine(JsonSerializer.Serialize(new { item.Id, status = "VALID_ARTIFACT_REQUIRES_EXTERNAL_REVIEW", proposal.Text, elapsedMs = timer.ElapsedMilliseconds }));
            } catch (Exception failure) when (failure is ValidationException or LlmUnavailableException or OperationCanceledException
                or HttpRequestException or JsonException or IOException) {
                failed = true;
                rows.Add(new { item.Id, item.Message, status = "FAILED", error = failure.Message, elapsedMs = timer.ElapsedMilliseconds, calls = observer.Calls });
                Console.WriteLine(JsonSerializer.Serialize(new { item.Id, status = "FAILED", error = failure.Message }));
            }
            await Save("RUNNING");
        }
        await Save(failed ? "FAILED_COMPONENT" : "EXECUTED_REQUIRES_EXTERNAL_REVIEW");
        // Valid transport is not semantic acceptance.
        Environment.ExitCode = failed ? 2 : 3;
    }

    private sealed class Trace : DelegatingHandler
    {
        public List<object> Calls { get; } = [];
        public Trace() : base(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var input = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(ct));
            var response = await base.SendAsync(request, ct);
            var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(ct));
            Calls.Add(new { request = input, responseStatus = (int)response.StatusCode,
                content = body.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content) ? content.GetString() : null,
                doneReason = body.TryGetProperty("done_reason", out var reason) ? reason.GetString() : null });
            // The model's private reasoning is never retained.
            return response;
        }
    }
}
