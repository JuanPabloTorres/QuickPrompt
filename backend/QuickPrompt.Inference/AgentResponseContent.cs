using System.Text.Json;
using System.Text.Json.Serialization;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Validates legacy-protocol model content only; no task or authorization decisions.
internal static class AgentResponseContent
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static T ParseContent<T>(JsonDocument response, JsonElement schema)
    {
        try { using var content = JsonDocument.Parse(response.RootElement.GetProperty("message").GetProperty("content").GetString()!); return Parse<T>(content.RootElement, schema); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentNullException) { throw new ValidationException("Propuesta JSON inválida. No se aplicó ninguna acción."); }
    }
    public static T Parse<T>(JsonElement content, JsonElement schema)
    {
        ContractSchema.Validate(content, schema);
        try { return content.Deserialize<T>(Json) ?? throw new ValidationException("Contrato vacío."); }
        catch (JsonException) { throw new ValidationException("Contrato incompatible con NOVA."); }
    }
}
