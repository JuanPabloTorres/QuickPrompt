using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

// Typed input documents, not a second tool registry or a semantic classifier.
public static class ValidatedAgentJson
{
    private static readonly JsonSerializerOptions Json = Options();
    private static readonly ConcurrentDictionary<Type, JsonElement> Schemas = new();
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
            PropertyNameCaseInsensitive = false, NumberHandling = JsonNumberHandling.Strict,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true,
            AllowDuplicateProperties = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true); return options;
    }
    public static JsonElement Schema<T>() => Schemas.GetOrAdd(typeof(T), type => AIJsonUtilities.CreateJsonSchema(type, serializerOptions: Json).Clone());
    public static T Read<T>(JsonElement value)
    {
        try { return value.Deserialize<T>(Json) ?? throw new JsonException(); }
        catch (JsonException) { throw new ValidationException("Contexto JSON inválido, incompleto o con propiedades desconocidas."); }
    }
    public static string Encode<T>(T value, Action<T>? validate = null)
    {
        JsonElement wire;
        try { wire = JsonSerializer.SerializeToElement(value, Json); }
        catch (JsonException) { throw new ValidationException("Contexto JSON inválido, incompleto o con propiedades desconocidas."); }
        var checkedValue = Read<T>(wire); validate?.Invoke(checkedValue);
        _ = Schema<T>(); // Fail locally if the trusted DTO cannot publish its contract.
        var content = wire.GetRawText();
        if (Encoding.UTF8.GetByteCount(content) > 32000) throw new ValidationException("El contexto JSON excede su presupuesto; no se truncarán los datos.");
        return content;
    }
    public static string SchemaHash<T>() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Schema<T>().GetRawText())));

}
