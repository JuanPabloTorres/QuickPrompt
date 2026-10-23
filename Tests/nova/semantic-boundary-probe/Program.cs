// Synthetic diagnostic consumer of FROZEN binaries. Never calls a provider or
// executes a domain/UI effect; no domain rules are reimplemented here.
using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
if (args.Length != 2 || File.Exists(args[1])) throw new ArgumentException("Input and a NEW output path required.");
using var input = JsonDocument.Parse(File.ReadAllText(args[0]));
var root = input.RootElement;
if (!root.GetProperty("syntheticOnly").GetBoolean() || root.GetProperty("context").GetString() != "selected_report")
    throw new ArgumentException("Only the declared synthetic selected-source fixture is supported.");
var fixture = root.GetProperty("source");
var text = fixture.GetProperty("templateText").GetString()!;
var context = SemanticContext.Empty with { Selected = new SemanticEntity(
    fixture.GetProperty("id").GetGuid(), fixture.GetProperty("revision").GetInt32(),
    fixture.GetProperty("title").GetString()!, text, false, InputNames: TemplateCompiler.Names(text)) };
var message = root.GetProperty("message").GetString()!;
var model = root.GetProperty("model").GetString()!;
var rows = new List<object>();
JsonElement? captured = null;
foreach (var item in root.GetProperty("proposals").EnumerateArray()) {
    var transport = new ReplayTransport(item.GetProperty("output"));
    var interpreter = new AdaptiveCoreInterpreter(new("", "", "", "http://127.0.0.1:11434"), transport);
    object? plan = null; string? error = null;
    try {
        var result = await interpreter.Interpret(message, context, model, CancellationToken.None);
        plan = new { intentions = result.Intentions.Select(x => x.ToString()),
            requestedEffects = result.Goal?.RequestedEffects.Select(x => x.ToString()),
            providerId = result.ProviderId, outcome = result.DesiredOutcome.ToString() };
    }
    catch (Exception failure) { error = failure.GetType().Name + ": " + failure.Message; }
    captured ??= transport.Packet;
    if (captured.Value.GetRawText() != transport.Packet.GetRawText()) throw new InvalidOperationException("Replay packet changed.");
    rows.Add(new { id = item.GetProperty("id").GetString(), output = item.GetProperty("output"), acceptedByHost = error is null, plan, error });
}
if (captured is null) throw new ArgumentException("At least one recorded proposal required.");
var protocol = AdaptiveCoreModelTransport.CreateRequest(captured.Value, new string('0', 32));
var hashes = new[] { typeof(AdaptiveCoreInterpreter).Assembly, typeof(SemanticContext).Assembly, typeof(TemplateCompiler).Assembly }
    .ToDictionary(a => Path.GetFileName(a.Location), a => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))));
File.WriteAllText(args[1], JsonSerializer.Serialize(new {
    schema = "quickprompt-semantic-replay/1", scope = "OFFLINE_FROZEN_HOST_REPLAY_NO_EFFECTS",
    runtimeHashes = hashes, packet = captured, protocol, rows,
    operations = SemanticCapabilityCatalog.All.Select(x => new { operation = x.Operation.ToString(), modelName = x.ModelName })
}, json));

sealed class ReplayTransport(JsonElement output) : IValidatedAgentModelTransport {
    public JsonElement Packet { get; private set; }
    public bool UsesThinking(string model) => true;
    public void Observe(int stage, string content, string? error) { }
    public void Reject(string? answer) { }
    public Task<JsonDocument> Send(object packet, CancellationToken ct, string purpose) {
        ct.ThrowIfCancellationRequested();
        if (purpose != "CoreWorkOrder") throw new InvalidOperationException("Unexpected inference boundary.");
        Packet = JsonSerializer.SerializeToElement(packet);
        return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(new {
            message = new { content = output.GetRawText() }, modelCalls = 1, coreRequestId = new string('0', 32)
        })));
    }
}
