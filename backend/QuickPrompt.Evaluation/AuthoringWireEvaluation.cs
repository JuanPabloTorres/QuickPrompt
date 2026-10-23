using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

// QA adapter comparison. Native tool declarations are inspected, never dispatched.
internal static class AuthoringWireEvaluation
{
    private sealed record Case(string Id, string Message);
    private sealed record Dataset(bool SyntheticOnly, Case[] Cases);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static async Task Run(string[] args)
    {
        if (args.Length != 2 && !(args.Length == 3 && args[2] is "structured" or "native-tools"))
            throw new ArgumentException("Pass owned synthetic fixture, new evidence output and optional structured|native-tools profile.");
        var native = args.Length == 2 || args[2] == "native-tools";
        var root = Path.GetFullPath(Environment.CurrentDirectory);
        var fixture = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]);
        if (!fixture.StartsWith(Path.Combine(root, ".playwright") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !output.StartsWith(Path.Combine(root, "docs", "evidence") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || File.Exists(output)) throw new ArgumentException("Preserve existing evidence and use owned paths.");
        var bytes = await File.ReadAllBytesAsync(fixture);
        var dataset = JsonSerializer.Deserialize<Dataset>(bytes, Json) ?? throw new ArgumentException("Invalid fixture");
        if (!dataset.SyntheticOnly || dataset.Cases.Length == 0 || dataset.Cases.Select(x => x.Id).Distinct().Count() != dataset.Cases.Length
            || dataset.Cases.Any(x => string.IsNullOrWhiteSpace(x.Message) || x.Message.Length > 16000)) throw new ArgumentException("Bounded synthetic cases required.");
        await using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false }) {
            BaseAddress = new("http://127.0.0.1:11434"), Timeout = Timeout.InfiniteTimeSpan };
        using var identityTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var tags = await client.GetFromJsonAsync<JsonElement>("api/tags", identityTimeout.Token);
        var model = tags.GetProperty("models").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "qwen3:8b").Clone();
        var rows = new List<object>(); var failed = false;
        async Task Save(string state) => await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new {
            state, scope = "CONTENT_OWNER_WIRE_COMPONENT_NOT_COMPLETE_GENERATOR_REVIEW_OR_UI", profile = native ? "native-tools" : "structured-owner", syntheticOnly = true, acceptanceComplete = false,
            fixtureSha256 = Convert.ToHexString(SHA256.HashData(bytes)), model,
            ownerAssemblySha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(PromptDraftGenerator).Assembly.Location))),
            evaluatorAssemblySha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(AuthoringWireEvaluation).Assembly.Location))), rows
        }, Json));
        await Save("RUNNING");
        foreach (var item in dataset.Cases) {
            var original = await Capture(item.Message);
            var wire = JsonNode.Parse(original.GetRawText())!.AsObject();
            var schema = original.GetProperty("format");
            const string function = "propose_reusable_instruction";
            if (native) {
            wire.Remove("format");
            wire["tools"] = JsonSerializer.SerializeToNode(new[] { new { type = "function", function = new {
                name = function, description = "Deliver the requested reusable instruction artifact as a content proposal. This function does not save, execute or approve anything.", parameters = schema } } });
            var system = wire["messages"]![0]!["content"]!.GetValue<string>();
            wire["messages"]![0]!["content"] = system + " Transport: deliver your three content fields by calling propose_reusable_instruction exactly once. Do not return a separate JSON message or call other functions.";
            }
            var timer = Stopwatch.StartNew();
            try {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                using var response = await client.PostAsJsonAsync("api/chat", wire, timeout.Token);
                response.EnsureSuccessStatusCode();
                var raw = await response.Content.ReadFromJsonAsync<JsonElement>(timeout.Token);
                if (!raw.GetProperty("done").GetBoolean() || raw.GetProperty("done_reason").GetString() == "length") throw new ValidationException("Incomplete native declaration");
                JsonElement artifact;
                if (native) {
                    var calls = raw.GetProperty("message").GetProperty("tool_calls");
                    if (calls.GetArrayLength() != 1 || calls[0].GetProperty("function").GetProperty("name").GetString() != function)
                        throw new ValidationException("Exactly one known content function required");
                    artifact = calls[0].GetProperty("function").GetProperty("arguments");
                } else artifact = JsonSerializer.Deserialize<JsonElement>(raw.GetProperty("message").GetProperty("content").GetString()!);
                ContractSchema.Validate(artifact, schema);
                rows.Add(new { item.Id, item.Message, status = "VALID_DECLARATION_REQUIRES_EXTERNAL_REVIEW", elapsedMs = timer.ElapsedMilliseconds,
                    originalRequest = original, submittedRequest = wire, artifact = artifact.Clone(),
                    promptTokens = raw.TryGetProperty("prompt_eval_count", out var p) ? p.GetInt32() : (int?)null,
                    generatedTokens = raw.TryGetProperty("eval_count", out var e) ? e.GetInt32() : (int?)null });
                Console.WriteLine(JsonSerializer.Serialize(new { item.Id, elapsedMs = timer.ElapsedMilliseconds, artifact }));
            } catch (Exception failure) when (failure is ValidationException or HttpRequestException or JsonException or KeyNotFoundException or OperationCanceledException) {
                failed = true; rows.Add(new { item.Id, item.Message, status = "INVALID_DECLARATION", elapsedMs = timer.ElapsedMilliseconds, error = failure.Message, originalRequest = original, nativeRequest = wire });
                Console.WriteLine(JsonSerializer.Serialize(new { item.Id, error = failure.Message }));
            }
            await Save("RUNNING");
        }
        await Save(failed ? "FAILED_TRANSPORT_COMPONENT" : "EXECUTED_REQUIRES_EXTERNAL_REVIEW");
        Environment.ExitCode = failed ? 2 : 3;
    }

    private static async Task<JsonElement> Capture(string request)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var endpoint = $"http://127.0.0.1:{port}";
        using var transport = new OllamaAgentModelTransport(endpoint);
        using var editorHttp = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });
        var generator = new PromptDraftGenerator(transport, new OllamaClient(editorHttp, endpoint),
            new PromptInputValueExtractor(transport), new DraftFidelityReviewer(transport), includeSystemKnowledge: true);
        var plan = SemanticPlanCompiler.Compile(new([SemanticOperation.CREATE_DRAFT], TargetReference.NONE, "", null, "", request,
            new([], true, null, null, [], [], false), SemanticOutcome.NEW_DRAFT, [], false, "", null, "Synthetic wire capture")) with {
                Authoring = new(InstructionAuthorship.CREATE_TEMPLATE, request, request, LiveRuns.Hash(request)) };
        plan = plan with { Goal = TaskGoal.FromAuthoring(request, SemanticContext.Empty, plan) };
        var input = new PrepareInput(Guid.NewGuid(), request) { Interpretation = plan };
        var owner = typeof(PromptDraftGenerator).GetMethod("GenerateAuthoredDefinition", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pending = (Task)owner.Invoke(generator, [input, Array.Empty<RetrievalCandidate>(), timeout.Token])!;
        var exchange = await listener.GetContextAsync().WaitAsync(timeout.Token);
        using var packet = await JsonDocument.ParseAsync(exchange.Request.InputStream, cancellationToken: timeout.Token);
        var result = packet.RootElement.Clone();
        var response = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { message = new {
            content = JsonSerializer.Serialize(new { title = "Captura controlada", description = "Sin valor semántico", templateText = "Texto temporal sin entradas." }) }, done = true, done_reason = "stop" }));
        exchange.Response.ContentType = "application/json"; exchange.Response.ContentLength64 = response.Length;
        await exchange.Response.OutputStream.WriteAsync(response, timeout.Token); exchange.Response.Close();
        await pending.WaitAsync(timeout.Token);
        return result;
    }
}
