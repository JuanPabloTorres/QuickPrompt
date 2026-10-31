using System.Text;
using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

/// <summary>Inference data only. Identity, library authority, endpoints and executable paths stay outside the packet.</summary>
public static class LocalProposalProtocol
{
    // Version 2 carries per-stage attempt limits. Strict v1 clients cannot
    // honor that field and must fail at protocol negotiation, before a claim.
    public const string Version = "quickprompt-local-proposal/2";
    public const string Scope = "companion:proposals";
    public const string Profile = "ollama-qwen3-8b-proposals";
    public const string ResourcePath = "/api/companion/proposals";
    public const string Kind = "ModelProposal";
    public const int MaximumBytes = 128000;

    public static void ValidateRequest(JsonElement packet)
        => Check(() => Request(packet));

    private static void Request(JsonElement packet)
    {
        Object(packet, ["model", "stream", "think", "messages", "options", "format", "tools", "decoding"]);
        Size(packet);
        if (Text(packet, "model") != LocalWorkProtocol.Model || packet.GetProperty("stream").ValueKind != JsonValueKind.False)
            throw Invalid();
        if (packet.TryGetProperty("think", out var thinking) && thinking.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Invalid();
        var messages = packet.GetProperty("messages");
        if (messages.ValueKind != JsonValueKind.Array || messages.GetArrayLength() is < 1 or > 16) throw Invalid();
        foreach (var message in messages.EnumerateArray())
        {
            Object(message, ["role", "content"]);
            if (Text(message, "role") is not ("system" or "user" or "assistant" or "tool") || Text(message, "content") is not { Length: > 0 and <= 64000 }) throw Invalid();
        }
        var options = packet.GetProperty("options");
        Object(options, ["num_ctx", "num_predict", "temperature", "top_p", "top_k", "min_p", "seed", "max_requests"]);
        if (options.GetProperty("num_ctx").GetInt32() is < 8192 or > 12288 || options.GetProperty("num_predict").GetInt32() is < 1 or > 4000
            || !options.GetProperty("temperature").TryGetDouble(out var temperature) || temperature is < 0 or > 1.5) throw Invalid();
        foreach (var name in new[] { "top_p", "min_p" })
            if (options.TryGetProperty(name, out var value) && (!value.TryGetDouble(out var number) || number is < 0 or > 1)) throw Invalid();
        if (options.TryGetProperty("top_k", out var topK) && topK.GetInt32() is < 0 or > 100) throw Invalid();
        if (options.TryGetProperty("seed", out var seed)) _ = seed.GetInt32();
        // Trusted model stages can reduce the existing two-attempt budget;
        // inference data cannot increase it or grant any application authority.
        if (options.TryGetProperty("max_requests", out var attempts) &&
            (attempts.ValueKind != JsonValueKind.Number || !attempts.TryGetInt32(out var limit) || limit is < 1 or > 2)) throw Invalid();
        var structured = packet.TryGetProperty("format", out var format) && format.ValueKind != JsonValueKind.Null;
        var tools = packet.TryGetProperty("tools", out var catalog) && catalog.ValueKind != JsonValueKind.Null;
        if (structured && tools) throw Invalid();
        if (structured) Schema(format);
        if (tools)
        {
            if (catalog.ValueKind != JsonValueKind.Array || catalog.GetArrayLength() is < 1 or > 32) throw Invalid();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tool in catalog.EnumerateArray())
            {
                Object(tool, ["type", "function"]);
                if (Text(tool, "type") != "function") throw Invalid();
                var function = tool.GetProperty("function"); Object(function, ["name", "description", "parameters"]);
                if (Text(function, "name") is not { Length: > 0 and <= 100 } name || !names.Add(name)
                    || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '.'))) throw Invalid();
                if (function.TryGetProperty("description", out var description) && description.GetString() is not { Length: <= 8000 }) throw Invalid();
                Schema(function.GetProperty("parameters"));
            }
        }
        if (packet.TryGetProperty("decoding", out var decoding) && (!(structured || tools) || decoding.GetString() is not ("native" or "prompted"))) throw Invalid();
    }

    /// <summary>Checks framing and offered tool identity. Application/domain consumers still validate all proposed meaning and effects.</summary>
    public static void ValidateReply(JsonElement reply, JsonElement request)
        => Check(() => Reply(reply, request));

    private static void Reply(JsonElement reply, JsonElement request)
    {
        Size(reply);
        var maximumCalls = request.GetProperty("options").TryGetProperty("max_requests", out var requestedCalls) ? requestedCalls.GetInt32() : 2;
        if (maximumCalls is < 1 or > 2) throw Invalid();
        Object(reply, ["done", "done_reason", "message", "model", "coreRequestId", "modelCalls", "prompt_eval_count", "eval_count"]);
        if (Text(reply, "model") != LocalWorkProtocol.Model || Text(reply, "done_reason") != "stop"
            || reply.GetProperty("done").ValueKind != JsonValueKind.True || reply.GetProperty("modelCalls").GetInt32() < 1
            || reply.GetProperty("modelCalls").GetInt32() > maximumCalls
            || !Guid.TryParseExact(Text(reply, "coreRequestId"), "N", out _) || reply.GetProperty("prompt_eval_count").GetInt32() is < 0 or > 1000000
            || reply.GetProperty("eval_count").GetInt32() is < 0 or > 1000000) throw Invalid();
        var message = reply.GetProperty("message");
        Object(message, ["content", "tool_calls"]);
        if (request.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array)
        {
            if (message.TryGetProperty("content", out var extraContent) && extraContent.GetString() != "") throw Invalid();
            var calls = message.GetProperty("tool_calls");
            if (calls.ValueKind != JsonValueKind.Array || calls.GetArrayLength() != 1) throw Invalid();
            Object(calls[0], ["function"]);
            var function = calls[0].GetProperty("function");
            Object(function, ["name", "arguments"]);
            if (!tools.EnumerateArray().Any(tool => Text(tool.GetProperty("function"), "name") == Text(function, "name"))
                || function.GetProperty("arguments").ValueKind != JsonValueKind.Object) throw Invalid();
        }
        else
        {
            if (message.TryGetProperty("tool_calls", out _)) throw Invalid();
            var content = Text(message, "content");
            if (content is not { Length: > 0 and <= 64000 }) throw Invalid();
            if (request.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.Object)
            { using var document = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 32 }); }
        }
    }

    private static void Schema(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object) throw Invalid();
        Walk(schema, 0);
        static void Walk(JsonElement value, int depth)
        {
            if (depth > 32) throw Invalid();
            if (value.ValueKind == JsonValueKind.Object)
                foreach (var field in value.EnumerateObject())
                {
                    // No model task may trigger schema retrieval over the network or local files.
                    if (field.Name == "$ref" && (field.Value.ValueKind != JsonValueKind.String || !field.Value.GetString()!.StartsWith("#/", StringComparison.Ordinal))) throw Invalid();
                    Walk(field.Value, depth + 1);
                }
            else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Walk(item, depth + 1);
        }
    }
    private static void Size(JsonElement value) { if (Encoding.UTF8.GetByteCount(value.GetRawText()) > MaximumBytes) throw Invalid(); }
    private static void Object(JsonElement value, string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Any(field => !fields.Contains(field.Name, StringComparer.Ordinal))
            || value.EnumerateObject().Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != value.EnumerateObject().Count()) throw Invalid();
    }
    private static void Check(Action validate)
    { try { validate(); } catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { throw Invalid(); } }
    private static string? Text(JsonElement value, string name) => value.GetProperty(name).GetString();
    private static ValidationException Invalid() => new("Paquete de propuesta local fuera de contrato.");
}

public sealed record LocalProposalLease(Guid Id, Guid Token, JsonElement Packet, int LeaseSeconds, int MaximumSeconds);
public sealed record LocalProposalReceipt(Guid Token, JsonElement? Reply, string? ErrorCode = null);

/// <summary>Fixed operator choices shared by OAuth and the native client. Never selected from task text.</summary>
public static class LocalCompanionProfiles
{
    public static string Scope(string profile) => profile switch { LocalWorkProtocol.Profile => LocalWorkProtocol.Scope, LocalProposalProtocol.Profile => LocalProposalProtocol.Scope, _ => throw new ValidationException("Perfil local desconocido.") };
    public static string ResourcePath(string profile) => profile switch { LocalWorkProtocol.Profile => "/api/companion", LocalProposalProtocol.Profile => LocalProposalProtocol.ResourcePath, _ => throw new ValidationException("Perfil local desconocido.") };
    public static string Protocol(string profile) => profile == LocalProposalProtocol.Profile ? LocalProposalProtocol.Version : profile == LocalWorkProtocol.Profile ? LocalWorkProtocol.Version : throw new ValidationException("Perfil local desconocido.");
    public static bool Known(string? profile) => profile is LocalWorkProtocol.Profile or LocalProposalProtocol.Profile;
}
