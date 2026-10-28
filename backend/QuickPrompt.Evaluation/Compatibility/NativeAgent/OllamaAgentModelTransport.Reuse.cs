using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class OllamaAgentModelTransport
{
    private sealed record Memo(string Digest, string Response, DateTime Expires, string? AnswerFingerprint);
    private readonly Dictionary<string, Memo> memo = new(StringComparer.Ordinal);
    private readonly object memoGate = new();
    private readonly bool reuseInference;
    private int memoBytes;
    private const int MaxMemoEntries = 64, MaxMemoBytes = 4 * 1024 * 1024;

    // Only pure inference responses are reused. UI effects, approval, receipts,
    // source reads and all domain validators still run for every new operation.
    private async Task<(string Key, string Digest)?> ReuseIdentity(JsonElement packet, CancellationToken ct)
    {
        if (!reuseInference || !packet.TryGetProperty("model", out var model) || model.GetString() is not { } name) return null;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromMilliseconds(500));
        try {
            using var tags = await http.GetAsync("api/tags", limit.Token);
            if (!tags.IsSuccessStatusCode) return null;
            using var data = JsonDocument.Parse(await tags.Content.ReadAsStringAsync(limit.Token));
            if (!data.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array) return null;
            foreach (var item in models.EnumerateArray()) {
                if (item.TryGetProperty("name", out var installed) && installed.GetString() == name
                    && item.TryGetProperty("digest", out var digest) && digest.GetString() is { Length: > 0 } version) {
                    return (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(packet.GetRawText()))), version);
                }
            }
        } catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException or OperationCanceledException) {
            ct.ThrowIfCancellationRequested(); // Cache availability never masks actual cancellation.
        }
        return null; // Unknown model identity: perform normal inference, never use stale data.
    }
    private JsonDocument? Recall((string Key, string Digest) identity)
    {
        lock (memoGate) {
            PruneMemo();
            if (!memo.TryGetValue(identity.Key, out var prior) || prior.Digest != identity.Digest) return null;
            observe?.Invoke(new("INFERENCE_CACHE_HIT"));
            return JsonDocument.Parse(prior.Response);
        }
    }
    private void Remember((string Key, string Digest)? identity, JsonElement packet, JsonDocument response)
    {
        if (identity is not { } key || !ReusableContract(packet, response.RootElement)) return;
        // Keep only the visible structured answer, never the model's private thinking.
        var copy = System.Text.Json.Nodes.JsonNode.Parse(response.RootElement.GetRawText())!;
        if (copy["message"] is System.Text.Json.Nodes.JsonObject message) message.Remove("thinking");
        var text = copy.ToJsonString();
        if (Encoding.UTF8.GetByteCount(text) > 256000) return;
        lock (memoGate) {
            PruneMemo();
            if (memo.Remove(key.Key, out var old)) memoBytes -= Encoding.UTF8.GetByteCount(old.Response);
            var bytes = Encoding.UTF8.GetByteCount(text);
            while (memo.Count >= MaxMemoEntries || memoBytes + bytes > MaxMemoBytes) {
                var first = memo.First(); memo.Remove(first.Key); memoBytes -= Encoding.UTF8.GetByteCount(first.Value.Response);
            }
            var content = response.RootElement.GetProperty("message");
            var fingerprint = content.TryGetProperty("content", out var answer) && answer.ValueKind == JsonValueKind.String
                ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(answer.GetString()!))) : null;
            memo[key.Key] = new(key.Digest, text, DateTime.UtcNow.AddMinutes(5), fingerprint); memoBytes += bytes;
        }
    }
    public void Reject(string? answer)
    {
        if (!reuseInference || string.IsNullOrEmpty(answer)) return;
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(answer)));
        lock (memoGate) {
            // Eviction is conservative: an identical visible answer may have been
            // retained under more than one packet. A miss is safer than replaying
            // a contract that failed semantic validation. No reasoning is retained.
            foreach (var key in memo.Where(entry => entry.Value.AnswerFingerprint == fingerprint).Select(entry => entry.Key).ToArray()) {
                memoBytes -= Encoding.UTF8.GetByteCount(memo[key].Response); memo.Remove(key);
            }
        }
    }
    private void PruneMemo()
    {
        foreach (var entry in memo.Where(entry => entry.Value.Expires <= DateTime.UtcNow).ToArray()) {
            memo.Remove(entry.Key); memoBytes -= Encoding.UTF8.GetByteCount(entry.Value.Response);
        }
    }
    private static bool ReusableContract(JsonElement packet, JsonElement response)
    {
        try {
            var message = response.GetProperty("message");
            if (packet.TryGetProperty("format", out var schema) && schema.ValueKind == JsonValueKind.Object) {
                using var answer = JsonDocument.Parse(message.GetProperty("content").GetString()!);
                ContractSchema.Validate(answer.RootElement, schema); return true;
            }
            if (packet.TryGetProperty("tools", out var tools) && message.TryGetProperty("tool_calls", out var calls) && calls.GetArrayLength() == 1) {
                var call = calls[0].GetProperty("function");
                foreach (var tool in tools.EnumerateArray()) {
                    var contract = tool.GetProperty("function");
                    if (contract.GetProperty("name").GetString() != call.GetProperty("name").GetString()) continue;
                    ContractSchema.Validate(call.GetProperty("arguments"), contract.GetProperty("parameters")); return true;
                }
            }
        } catch (Exception error) when (error is JsonException or ValidationException or KeyNotFoundException or InvalidOperationException or ArgumentNullException) { }
        return false; // Never retain malformed, batched or unavailable selections.
    }
}
