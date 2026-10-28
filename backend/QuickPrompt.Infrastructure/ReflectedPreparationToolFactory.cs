using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Vendor types remain inside Infrastructure. Providers are supplied by trusted composition,
// never discovered from a model-supplied assembly, type or method name.
public sealed class ReflectedPreparationToolFactory : IPreparationToolFactory
{
    public static ReflectedPreparationToolFactory Instance { get; } = new();
    private static readonly JsonSerializerOptions Json = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
            PropertyNameCaseInsensitive = false,
            NumberHandling = JsonNumberHandling.Strict,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            RespectNullableAnnotations = true,
            AllowDuplicateProperties = false
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    public PreparationToolBinding Create(object provider, MethodInfo method, PreparationToolAttribute declaration)
    {
        var argument = method.GetParameters()[0];
        var argumentName = argument.Name!;
        var resultType = method.ReturnType.GenericTypeArguments[0];
        var function = AIFunctionFactory.Create(method, provider, new AIFunctionFactoryOptions {
            Name = declaration.Name, Description = declaration.Description, SerializerOptions = Json,
            MarshalResult = (value, _, _) => ValueTask.FromResult(value)
        });

        object Decode(JsonElement wire)
        {
            // The wire contract is the full DTO schema, not the SDK method-parameter wrapper.
            // Strict typed deserialization is authoritative; the legacy subset schema validator
            // cannot validate SDK schemas containing refs, unions or formats.
            try
            {
                if (wire.ValueKind != JsonValueKind.Object)
                    throw new JsonException("Expected function arguments.");
                return JsonSerializer.Deserialize(wire, argument.ParameterType, Json)
                    ?? throw new JsonException("Null function argument.");
            }
            catch (JsonException)
            {
                throw new ValidationException("Argumentos de herramienta inválidos, incompletos o desconocidos.");
            }
        }

        return new(declaration.Name, declaration.Permission, declaration.Description, argument.ParameterType, resultType,
            AIJsonUtilities.CreateJsonSchema(argument.ParameterType, serializerOptions: Json).Clone(), function.ReturnJsonSchema?.Clone(),
            value => JsonSerializer.SerializeToElement(value, argument.ParameterType, Json),
            Decode,
            async (value, ct) => await function.InvokeAsync(new AIFunctionArguments { [argumentName] = value }, ct)
                ?? throw new ValidationException("La herramienta no devolvió un resultado."));
    }
}
