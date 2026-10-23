using System.Reflection;
using System.Text.Json;

namespace QuickPrompt.Nova.Application;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class PreparationToolAttribute(string name, ToolPermission permission, string description) : Attribute
{
    public string Name { get; } = name;
    public ToolPermission Permission { get; } = permission;
    public string Description { get; } = description;
}

// Infrastructure supplies schema generation/marshalling; the application owns execution policy.
public interface IPreparationToolFactory
{
    PreparationToolBinding Create(object provider, MethodInfo method, PreparationToolAttribute declaration);
}

public sealed record PreparationToolBinding(string Name, ToolPermission Permission, string Description,
    Type ArgumentsType, Type ResultType, JsonElement ArgumentsSchema, JsonElement? ResultSchema,
    Func<object, JsonElement> EncodeArguments, Func<JsonElement, object> DecodeArguments,
    Func<object, CancellationToken, Task<object>> Invoke);

internal static class PreparationToolDiscovery
{
    public static PreparationToolBinding[] Bind(object provider, IPreparationToolFactory factory)
        => provider.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => method.IsDefined(typeof(PreparationToolAttribute), false))
            .Select(method => Bind(provider, method, factory)).ToArray();

    private static PreparationToolBinding Bind(object provider, MethodInfo method, IPreparationToolFactory factory)
    {
        var declaration = method.GetCustomAttribute<PreparationToolAttribute>()!;
        var parameters = method.GetParameters();
        if (!method.IsPublic || method.IsStatic || method.ContainsGenericParameters || parameters.Length != 2
            || parameters[0].ParameterType.IsByRef || parameters[1].ParameterType != typeof(CancellationToken)
            || !method.ReturnType.IsGenericType || method.ReturnType.GetGenericTypeDefinition() != typeof(Task<>)
            || !Enum.IsDefined(declaration.Permission) || string.IsNullOrWhiteSpace(declaration.Name) || string.IsNullOrWhiteSpace(declaration.Description))
            throw new InvalidOperationException($"Invalid preparation tool declaration: {method.Name}.");
        return factory.Create(provider, method, declaration);
    }
}
