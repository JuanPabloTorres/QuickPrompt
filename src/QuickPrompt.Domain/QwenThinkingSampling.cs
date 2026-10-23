using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuickPrompt.Local;

// Explicit, request-local experiment. Never changes model/server defaults or disables reasoning.
public static class QwenThinkingSampling
{
    public static JsonElement Apply(JsonElement packet, bool enabled)
    {
        if (!enabled || !packet.TryGetProperty("model", out var model) || model.GetString() != "qwen3:8b"
            || !packet.TryGetProperty("think", out var think) || think.ValueKind != JsonValueKind.True) return packet;
        var data = JsonNode.Parse(packet.GetRawText())!.AsObject();
        if (data["options"] is null) data["options"] = new JsonObject();
        var options = data["options"]!.AsObject();
        options["temperature"] = .6; options["top_p"] = .95; options["top_k"] = 20; options["min_p"] = 0; options["seed"] = 42017;
        return JsonSerializer.SerializeToElement(data);
    }
}
