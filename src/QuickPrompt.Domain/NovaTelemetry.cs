using LocalModelPolicy = QuickPrompt.Web.Infrastructure.LocalModelPolicy;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace QuickPrompt.Local;

public record NovaTiming(string Operation, double StartedMs, double DurationMs, string Outcome,
    string? Model = null, bool? Thinking = null, int? ContextTokens = null, int? OutputBudget = null,
    double? LoadMs = null, double? PromptEvaluationMs = null, double? GenerationMs = null,
    int? PromptTokens = null, int? OutputTokens = null, int? CachedTokens = null, double? Temperature = null, string? KeepAlive = null);
public record NovaPerformance(Guid RunId, double ElapsedMs, int OperationCount, int LlmCalls, int ToolCalls,
    double? FirstVisibleActionMs, double? FirstModelTokenMs, string FirstTokenMeasurement,
    long WorkingSetBytes, double ProcessCpuMs, NovaTiming[] Operations, bool Truncated, double? ResultReadyMs);
public record NovaDebugPoint(Guid? RunId, string Boundary, bool Rejected, Dictionary<string, object?> Values);

// Bounded, process-local, content-free diagnostics. No logger or external exporter.
// Run IDs belong on traces, never on metric labels.
public static class NovaTelemetry
{
    public static readonly ActivitySource Activities = new("QuickPrompt.Nova", "1.0");
    private static readonly Meter Meter = new("QuickPrompt.Nova", "1.0");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("nova.operation.duration", "ms");
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("nova.operations");
    private static readonly AsyncLocal<State?> Current = new();
    public static Guid? RunId => Current.Value?.Id;
    // DEBUG-only call sites; no listener, serialization, files or public UI data by default.
    // A function breakpoint here exposes facts and the original caller's locals.
    [Conditional("DEBUG")]
    public static void DebugPoint(string boundary, bool rejected, Func<ActivityTagsCollection> facts,
        [CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (!Activities.HasListeners() && !Debugger.IsAttached) return;
        var values = facts();
        values["nova.run_id"] = RunId; values["debug.rejected"] = rejected;
        values["code.member"] = member; values["code.file"] = Path.GetFileName(file); values["code.line"] = line;
        if (rejected && Debugger.IsAttached && Environment.GetEnvironmentVariable("NOVA_DEBUG_BREAK_ON_FAILURE") == "1") Debugger.Break();
        using var point = Activities.StartActivity("debug." + boundary);
        if (point is not null) foreach (var item in values) point.SetTag(item.Key, item.Value);
    }
    // Read the source before registering: a first-use listener must not compare
    // against Activities while its ActivitySource constructor is still running.
    public static DebugCapture CaptureDebugPoints(int limit = 256, Action<NovaDebugPoint>? observe = null) => new(Activities, limit, observe);
    public sealed class DebugCapture : IDisposable
    {
        private readonly ConcurrentQueue<NovaDebugPoint> points = new();
        private readonly ActivityListener listener;
        private int count, observerErrors;
        public int Dropped => Math.Max(0, Volatile.Read(ref count) - Limit);
        public int ObserverErrors => Volatile.Read(ref observerErrors);
        public int Limit { get; }
        public NovaDebugPoint[] Snapshot() => points.ToArray();
        internal DebugCapture(ActivitySource source, int limit, Action<NovaDebugPoint>? observe)
        {
            if (limit is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(limit));
            Limit = limit;
            listener = new() {
                ShouldListenTo = candidate => candidate == source,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => {
                    if (!activity.OperationName.StartsWith("debug.", StringComparison.Ordinal)) return;
                    if (Interlocked.Increment(ref count) > Limit) return;
                    var values = activity.TagObjects.ToDictionary(item => item.Key, item => item.Value);
                    var point = new NovaDebugPoint(values.GetValueOrDefault("nova.run_id") is Guid id ? id : null,
                        activity.OperationName[6..], values.GetValueOrDefault("debug.rejected") is true, values);
                    points.Enqueue(point);
                    // An optional development observer must never change domain execution.
                    try { observe?.Invoke(point); } catch (Exception) { Interlocked.Increment(ref observerErrors); }
                }
            };
            ActivitySource.AddActivityListener(listener);
        }
        public void Dispose() => listener.Dispose();
    }
    private static readonly ConcurrentDictionary<Guid, State> Runs = new();
    internal sealed class State(Guid id)
    {
        public readonly Guid Id = id;
        public readonly Stopwatch Timer = Stopwatch.StartNew();
        public readonly List<NovaTiming> Items = [];
        public int Count, LlmCalls, ToolCalls;
        public double? FirstVisible, FirstToken;
        public bool Truncated;
        public double? Ended;
        public double? Ready;
    }
    public static IDisposable Begin(Guid runId)
    {
        var previous = Current.Value;
        var state = new State(runId);
        Runs[runId] = state;
        foreach (var old in Runs.Values.Where(s => s.Ended is not null).OrderByDescending(s => s.Timer.Elapsed).Take(Math.Max(0, Runs.Count - 64))) Runs.TryRemove(old.Id, out _);
        Current.Value = state;
        var activity = Activities.StartActivity("nova.run"); activity?.SetTag("nova.run_id", runId);
        return new Disposal(() => { lock (state.Items) state.Ended = state.Timer.Elapsed.TotalMilliseconds; activity?.Dispose(); Current.Value = previous; });
    }
    public static IDisposable Measure(string operation)
    {
        var state = Current.Value; var start = state?.Timer.Elapsed.TotalMilliseconds ?? 0;
        var timer = Stopwatch.StartNew(); var activity = Activities.StartActivity(operation);
        return new Disposal(() => { Record(state, new(operation, start, timer.Elapsed.TotalMilliseconds, "OBSERVED")); activity?.Dispose(); });
    }
    public static IDisposable Attach(Guid runId)
    {
        var previous = Current.Value;
        if (Runs.TryGetValue(runId, out var state)) Current.Value = state;
        return new Disposal(() => Current.Value = previous);
    }
    public static void VisibleAction(Guid id)
    {
        if (Runs.TryGetValue(id, out var state)) lock (state.Items) state.FirstVisible ??= state.Timer.Elapsed.TotalMilliseconds;
    }
    public static void ModelToken() { if (Current.Value is { } state) lock (state.Items) state.FirstToken ??= state.Timer.Elapsed.TotalMilliseconds; }
    public static void ResultReady(Guid id) { if (Runs.TryGetValue(id, out var state)) lock (state.Items) state.Ready ??= state.Timer.Elapsed.TotalMilliseconds; }
    public static void EventDelivered(Guid id, double milliseconds)
    {
        if (Runs.TryGetValue(id, out var state)) Record(state, new("events.delivery", state.Timer.Elapsed.TotalMilliseconds - milliseconds, milliseconds, "OBSERVED"));
    }
    public static LlmMeasurement Llm(JsonElement packet, [CallerMemberName] string purpose = "Chat") => new(Current.Value, packet, purpose);
    public sealed class LlmMeasurement : IDisposable
    {
        private readonly State? state; private readonly Stopwatch timer = Stopwatch.StartNew();
        private readonly double start; private readonly string purpose, model;
        private readonly bool? thinking; private readonly int? contextTokens, outputBudget; private readonly double? temperature; private readonly string? keepAlive;
        private JsonElement? usage; private string outcome = "ERROR_OR_CANCELLED"; private bool requested;
        private static int? Integer(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
        internal LlmMeasurement(State? state, JsonElement packet, string purpose)
        {
            this.state = state; this.purpose = purpose; start = state?.Timer.Elapsed.TotalMilliseconds ?? 0;
            var options = packet.TryGetProperty("options", out var configured) ? configured : default;
            model = packet.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String && LocalModelPolicy.Production.Allows(m.GetString()!) ? m.GetString()! : "other";
            thinking = packet.TryGetProperty("think", out var think) && think.ValueKind is JsonValueKind.True or JsonValueKind.False ? think.GetBoolean() : null;
            contextTokens = Integer(options, "num_ctx"); outputBudget = Integer(options, "num_predict");
            temperature = options.ValueKind == JsonValueKind.Object && options.TryGetProperty("temperature", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetDouble(out var value) ? value : null;
            keepAlive = packet.TryGetProperty("keep_alive", out var keep) && keep.ValueKind == JsonValueKind.String && keep.GetString() is "5m" or "10m" or "0" ? keep.GetString() : null;
        }
        public void Complete(JsonElement response)
        {
            // Retain numeric usage only, even for the duration of this measurement.
            var fields = new Dictionary<string, JsonElement>();
            foreach (var name in new[] { "prompt_eval_count", "eval_count", "cached_tokens", "load_duration", "prompt_eval_duration", "eval_duration" })
                if (response.ValueKind == JsonValueKind.Object && response.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number) fields[name] = value.Clone();
            usage = JsonSerializer.SerializeToElement(fields); outcome = "RESPONSE"; requested = true;
        }
        public void Requested() { requested = true; }
        public void CacheHit() { outcome = "CACHE_HIT"; }
        public void Dispose()
        {
            if (!requested && outcome != "CACHE_HIT") outcome = "NOT_REQUESTED";
            double? Milliseconds(string name) => usage is { } data && data.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) ? number / 1e6 : null;
            Record(state, new("llm." + purpose, start, timer.Elapsed.TotalMilliseconds, outcome, model, thinking,
                contextTokens, outputBudget, Milliseconds("load_duration"), Milliseconds("prompt_eval_duration"), Milliseconds("eval_duration"),
                usage is { } u ? Integer(u, "prompt_eval_count") : null, usage is { } v ? Integer(v, "eval_count") : null, usage is { } w ? Integer(w, "cached_tokens") : null, temperature, keepAlive));
        }
    }
    private static void Record(State? state, NovaTiming item)
    {
        Duration.Record(item.DurationMs, new KeyValuePair<string, object?>("operation", item.Operation));
        Operations.Add(1, new KeyValuePair<string, object?>("operation", item.Operation));
        if (state is null) return;
        lock (state.Items) {
            state.Count++; if (item.Operation.StartsWith("llm.") && item.Outcome is not ("CACHE_HIT" or "NOT_REQUESTED")) state.LlmCalls++;
            if (item.Operation.StartsWith("ui.")) state.ToolCalls++;
            if (state.Items.Count < 256) state.Items.Add(item); else state.Truncated = true;
        }
    }
    public static NovaPerformance? Snapshot(Guid id)
    {
        if (!Runs.TryGetValue(id, out var state)) return null;
        using var process = Process.GetCurrentProcess();
        lock (state.Items) return new(id, state.Ended ?? state.Timer.Elapsed.TotalMilliseconds, state.Count, state.LlmCalls, state.ToolCalls,
            state.FirstVisible, state.FirstToken, state.FirstToken is null ? "Not observed; structured calls are non-streaming" : "First nonempty streamed content",
            process.WorkingSet64, process.TotalProcessorTime.TotalMilliseconds, state.Items.ToArray(), state.Truncated, state.Ready);
    }
    private sealed class Disposal(Action action) : IDisposable { private Action? dispose = action; public void Dispose() => Interlocked.Exchange(ref dispose, null)?.Invoke(); }
}
