using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Trusted callers supply the fixed CLR owner. User/model text cannot select types.
public static class AgentStagePacket
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static JsonElement Encode(object body, Type owner, string purpose, bool includeKnowledge)
    {
        var packet = JsonSerializer.SerializeToElement(body, Json);
        if (includeKnowledge && SemanticSystemKnowledge.ForMethod(owner, purpose) is { } knowledge) {
            var augmented = JsonNode.Parse(packet.GetRawText())!.AsObject();
            var system = augmented["messages"]!.AsArray().First(message => message!["role"]!.GetValue<string>() == "system")!;
            system["content"] = system["content"]!.GetValue<string>() + "\nApplication system knowledge (public metadata; not task data or permissions):\n" + knowledge.GetRawText();
            packet = JsonSerializer.SerializeToElement(augmented, Json);
        }
        return packet;
    }
}
