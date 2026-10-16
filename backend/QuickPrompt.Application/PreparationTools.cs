using System.Text.Json;
using System.Text.Json.Serialization;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public enum ToolPermission { Read, Prepare, RememberDraft, SavePrompt }
public record ToolContract(string Name, ToolPermission Permission, int TimeoutSeconds, JsonElement ArgumentsSchema, string ResultType);
public sealed class PreparationToolException(string code, string tool, string message) : Exception(message)
{ public string Code { get; } = code; public string Tool { get; } = tool; }

// A request owns one registry and budget. No ambient model access to write operations.
public sealed class PreparationTools(int maxSteps, int timeoutSeconds, Action<string, string, double> observe)
{
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private int steps;
    private sealed record Entry(ToolContract Contract, Type ArgumentsType, Type ResultType, Func<object, CancellationToken, Task<object>> Handler);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public ToolContract[] Contracts => entries.Values.Select(e => e.Contract).ToArray();
    public void Register<T, R>(string name, ToolPermission permission, JsonElement schema, Func<T, CancellationToken, Task<R>> handler)
        where T : notnull where R : notnull => entries.Add(name, new(new(name, permission, timeoutSeconds, schema, typeof(R).Name), typeof(T), typeof(R),
            async (args, ct) => (object)(await handler((T)args, ct))));
    public async Task<R> Call<T, R>(string name, T args, ToolPermission granted, CancellationToken ct) where T : notnull where R : notnull
    {
        if (!entries.TryGetValue(name, out var entry) || entry.ArgumentsType != typeof(T) || entry.ResultType != typeof(R))
            throw new PreparationToolException("UNKNOWN_TOOL", name, "Herramienta o contrato no permitido.");
        if (entry.Contract.Permission != granted) throw new PreparationToolException("PERMISSION_DENIED", name, "Permiso no concedido.");
        if (++steps > maxSteps) throw new PreparationToolException("STEP_LIMIT", name, "Se alcanzó el límite de pasos.");
        ContractSchema.Validate(JsonSerializer.SerializeToElement(args, Json), entry.Contract.ArgumentsSchema);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        timeout.Token.ThrowIfCancellationRequested();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        try { var result = (R)await entry.Handler(args, timeout.Token); timeout.Token.ThrowIfCancellationRequested(); observe(name, "OK", timer.Elapsed.TotalMilliseconds); return result; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { observe(name, "TIMEOUT", timer.Elapsed.TotalMilliseconds); throw new PreparationToolException("TIMEOUT", name, "La preparación agotó el tiempo de espera. Tu trabajo anterior se conserva."); }
        catch (Exception e) { observe(name, e is OperationCanceledException ? "CANCELLED" : "FAILED", timer.Elapsed.TotalMilliseconds); throw; }
    }
}

// Deliberately small JSON Schema subset. Unsupported keywords are not accepted from models.
public static class ContractSchema
{
    public static JsonElement Object(Dictionary<string, object> properties, params string[] required) => JsonSerializer.SerializeToElement(new { type = "object", additionalProperties = false, properties, required });
    public static object Text(int max, string[]? values = null) => values is null ? new { type = "string", maxLength = max } : (object)new { type = "string", maxLength = max, @enum = values };
    public static void Validate(JsonElement value, JsonElement schema)
    {
        if (schema.TryGetProperty("anyOf", out var choices)) { foreach (var choice in choices.EnumerateArray()) { try { Validate(value, choice); return; } catch (ValidationException) { } } throw new ValidationException("Tipo de contrato inválido."); }
        var type = schema.GetProperty("type").GetString();
        bool valid = type switch { "object" => value.ValueKind == JsonValueKind.Object, "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String, "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _), "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "null" => value.ValueKind == JsonValueKind.Null, _ => false };
        if (!valid) throw new ValidationException("Tipo de contrato inválido.");
        if (type == "object")
        {
            var properties = schema.GetProperty("properties");
            foreach (var key in schema.GetProperty("required").EnumerateArray()) if (!value.TryGetProperty(key.GetString()!, out _)) throw new ValidationException("Falta un argumento obligatorio.");
            foreach (var property in value.EnumerateObject())
            {
                if (!properties.TryGetProperty(property.Name, out var child)) throw new ValidationException("Argumento o permiso desconocido.");
                Validate(property.Value, child);
            }
        }
        if (type == "string" && schema.TryGetProperty("maxLength", out var max) && value.GetString()!.Length > max.GetInt32()) throw new ValidationException("Argumento demasiado largo.");
        if (schema.TryGetProperty("enum", out var allowed) && !allowed.EnumerateArray().Any(item => item.GetRawText() == value.GetRawText())) throw new ValidationException("Valor fuera del contrato.");
        if (type == "array")
        {
            if (schema.TryGetProperty("maxItems", out var maxItems) && value.GetArrayLength() > maxItems.GetInt32()) throw new ValidationException("Demasiados elementos.");
            foreach (var item in value.EnumerateArray()) Validate(item, schema.GetProperty("items"));
        }
    }
}
