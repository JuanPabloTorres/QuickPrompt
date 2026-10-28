using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

public static partial class RecoveryEvaluation
{
    private static bool LabCheck(string caseId, Guid runId, string check, bool passed, object values,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string symbol = "")
    {
        LabEvent(caseId, runId, "verification", passed ? "PASS" : "FAIL", new { check, values }, file, line, symbol: symbol);
        return passed;
    }
    private static void LabEvent(string caseId, Guid runId, string phase, string status, object values,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, string? kind = null, object? inputs = null,
        [CallerMemberName] string symbol = "")
    {
        if (Environment.GetEnvironmentVariable("NOVA_EVAL_LAB_EVENTS") is not { Length: > 0 } path) return;
        var item = new { schema = "ai-delivery-event/1", id = Guid.NewGuid().ToString("N"),
            observedAt = DateTime.UtcNow.ToString("o"), campaignId = Environment.GetEnvironmentVariable("NOVA_EVAL_LAB_CAMPAIGN") ?? "quickprompt",
            runId, caseId, kind = kind ?? (phase == "input" ? "case_started" : "check_observed"), phase, status,
            owner = "QuickPrompt QA / RecoveryEvaluation", location = new { file, line, symbol, kind = "observed_check" }, inputs, observed = values };
        try { File.AppendAllText(path, JsonSerializer.Serialize(item) + "\n", new UTF8Encoding(false)); }
        catch (IOException) { Console.Error.WriteLine("LAB_OBSERVATION_WRITE_FAILED: diagnosis stream unavailable; task verdict unchanged."); }
        catch (UnauthorizedAccessException) { Console.Error.WriteLine("LAB_OBSERVATION_WRITE_FAILED: diagnosis stream unavailable; task verdict unchanged."); }
    }
    // Synthetic, independently-scoped experiment only. Never registered in the
    // API or used to award a blind semantic qualification / release gate.
    private sealed class LabCoreTransport(AdaptiveCoreOptions options, string labRoot,
        IValidatedAgentModelTransport ordinary) : IValidatedAgentModelTransport
    {
        private Case? scenario;
        public JsonElement? LastGuard { get; private set; }
        public void Configure(Case value) { scenario = value; LastGuard = null; }
        public bool UsesThinking(string model) => ordinary.UsesThinking(model);
        public void Observe(int stage, string content, string? error) => ordinary.Observe(stage, content, error);
        public void Reject(string? answer) => ordinary.Reject(answer);

        public async Task<JsonDocument> Send(object body, CancellationToken ct, string purpose)
        {
            if (purpose != "CoreActionRequest") return await ordinary.Send(body, ct, purpose);
            var current = scenario ?? throw new InvalidOperationException("Configure synthetic QA scope first.");
            if (!File.Exists(Path.Combine(labRoot, "ai_delivery_lab", "core_bridge.py")))
                throw new ArgumentException("Configure the existing AI Delivery Lab explicitly.");
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var packet = QwenThinkingSampling.Apply(JsonSerializer.SerializeToElement(body, json), options.QwenThinkingSamplingEnabled);
            var id = Guid.NewGuid().ToString("N");
            var request = AdaptiveCoreModelTransport.CreateRequest(packet, id);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(current.Message))).ToLowerInvariant();
            var envelope = JsonSerializer.Serialize(new {
                version = "ai-delivery-guard/1", request, requestSha256 = hash, allowQaOracle = true,
                effectsPath = new[] { "commands" }, effectKey = (string?)null,
                scope = new { issuer = "qa_oracle", request_sha256 = hash,
                    // This boundary carries REQUESTED GOALS, not the compiled
                    // dependency closure. Giving a search prerequisite authority
                    // here permits an unsolicited independent search/source goal.
                    authorized_effects = current.Required.Select(ModelName).ToArray(),
                    required_effects = current.Required.Select(ModelName).ToArray(), forbidden_effects = current.Forbidden.Select(ModelName).ToArray() }
            }, json);
            if (Encoding.UTF8.GetByteCount(envelope) > 256000) throw new ArgumentException("Lab envelope exceeds budget.");
            var start = new ProcessStartInfo(options.Python) { UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = Path.GetFullPath(labRoot), RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            foreach (var arg in new[] { "-u", "-B", "-m", "ai_delivery_lab.core_bridge", "--sdk-root", options.SdkRoot, "--endpoint", options.Endpoint })
                start.ArgumentList.Add(arg);
            if (Environment.GetEnvironmentVariable("NOVA_EVAL_LAB_EVENTS") is { Length: > 0 } journal) {
                foreach (var arg in new[] { "--events", journal, "--campaign-id",
                    Environment.GetEnvironmentVariable("NOVA_EVAL_LAB_CAMPAIGN") ?? new DirectoryInfo(labRoot).Parent!.Name,
                    "--case-id", current.Id }) start.ArgumentList.Add(arg);
                if (Environment.GetEnvironmentVariable("NOVA_EVAL_LAB_PUBLIC_INPUTS") == "1") start.ArgumentList.Add("--public-fixture");
            }
            start.Environment["PYDANTIC_AI_NO_BANNER"] = "1";
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(190));
            using var child = Process.Start(start) ?? throw new InvalidOperationException("Cannot start Lab bridge.");
            try {
                var stdout = ReadBounded(child.StandardOutput, deadline.Token);
                var stderr = ReadBounded(child.StandardError, deadline.Token);
                await child.StandardInput.WriteAsync(envelope.AsMemory(), deadline.Token); child.StandardInput.Close();
                await child.WaitForExitAsync(deadline.Token);
                using var reply = JsonDocument.Parse(await stdout); await stderr;
                var root = reply.RootElement;
                if (root.GetProperty("version").GetString() != "ai-delivery-guard/1" || root.GetProperty("requestId").GetString() != id)
                    throw new PreparationToolException("LAB_INVALID_PROTOCOL", purpose, "Lab reply identity mismatch.");
                LastGuard = JsonSerializer.SerializeToElement(new { requestId = id, issuer = "qa_oracle",
                    status = root.GetProperty("status"), reason = root.GetProperty("reason"), events = root.GetProperty("events") });
                if (child.ExitCode != 0 || root.GetProperty("status").GetString() != "SCOPE_CONFORMANT")
                    throw new PreparationToolException("LAB_PROPOSAL_BLOCKED", purpose, "Independent synthetic scope rejected the proposal; no effects executed.");
                return AdaptiveCoreModelTransport.Decode(root.GetProperty("inference"), request, 0);
            } finally {
                if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(CancellationToken.None); }
            }
        }

        private static string ModelName(string operation)
            => UserActions.For(Enum.Parse<SemanticOperation>(operation)).Name;

        private static async Task<string> ReadBounded(StreamReader reader, CancellationToken ct)
        {
            var result = new StringBuilder(); var buffer = new char[4096]; int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), ct)) != 0) {
                if (result.Length + count > 512000) throw new InvalidOperationException("Lab reply exceeds budget.");
                result.Append(buffer, 0, count);
            }
            return result.ToString();
        }
    }
}
