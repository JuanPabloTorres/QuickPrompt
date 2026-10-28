using System.Reflection;
using System.Text.Json;

namespace QuickPrompt.Nova.Application;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum AgentRepresentation { UserWorkOrder, LiteralAuthoringDirective, ReusableInstruction, CaseValues, RenderedInstruction }

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class AgentRepresentationAttribute(AgentRepresentation role, string description) : Attribute
{
    public AgentRepresentation Role { get; } = role;
    public string Description { get; } = description;
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class AgentStageAttribute(string responsibility) : Attribute
{
    public string Responsibility { get; } = responsibility;
}

public sealed record AgentRepresentationDescriptor(AgentRepresentation Role, string Member, string Description);

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class UiCapabilityAttribute(string name, string label, string description) : Attribute
{
    public string Name { get; } = name;
    public string Label { get; } = label;
    public string Description { get; } = description;
    public string Permission { get; set; } = "READ_OR_PREPARE";
    public bool RequiresTemplateCopyChoice { get; set; }
}

[AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class AgentOperationAttribute(string description, params string[] toolNames) : Attribute
{
    public string Description { get; } = description;
    public string[] ToolNames { get; } = toolNames;
    public string? ModelName { get; set; }
    public AgentRepresentation[] Writes { get; set; } = [];
    public AgentRepresentation[] Requires { get; set; } = [];
}

public sealed record UiCapabilityDescriptor(UiToolContract Contract, string Label)
{
    [System.Text.Json.Serialization.JsonIgnore] public bool RequiresTemplateCopyChoice { get; init; }
}
public sealed record SemanticCapabilityDescriptor(SemanticOperation Operation, string Description, IReadOnlyList<string> ToolNames)
{
    public string ModelName { get; init; } = Operation.ToString();
    public IReadOnlyList<AgentRepresentation> Writes { get; init; } = [];
    public IReadOnlyList<AgentRepresentation> Requires { get; init; } = [];
}

public static class CapabilityDiscovery
{
    // Metadata only: do not read getters, inspect runtime objects or scan assemblies.
    public static AgentRepresentationDescriptor[] Representations(params Type[] declarations)
    {
        var descriptions = declarations.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(property => property.IsDefined(typeof(AgentRepresentationAttribute), false))
            .Select(property => {
                var attribute = property.GetCustomAttribute<AgentRepresentationAttribute>()!;
                if (!Enum.IsDefined(attribute.Role) || string.IsNullOrWhiteSpace(attribute.Description)
                    || property.GetMethod is not { IsPublic: true, IsStatic: false } || property.GetIndexParameters().Length != 0)
                    throw new InvalidOperationException("Invalid agent representation: " + property.Name);
                return new AgentRepresentationDescriptor(attribute.Role, property.DeclaringType!.Name + "." + property.Name, attribute.Description);
            }).OrderBy(item => item.Role).ToArray();
        if (descriptions.Select(item => item.Role).Distinct().Count() != descriptions.Length)
            throw new InvalidOperationException("Duplicate agent representation role.");
        return descriptions;
    }

    // The composition root supplies a trusted type. Never scan assemblies or accept
    // a model-provided type/member name. Invalid declarations fail initialization.
    public static UiCapabilityDescriptor[] Ui(Type declarations)
    {
        var definitions = declarations.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => property.IsDefined(typeof(UiCapabilityAttribute), false))
            .Select(property =>
            {
                var attribute = property.GetCustomAttribute<UiCapabilityAttribute>()!;
                if (property.PropertyType != typeof(JsonElement) || property.GetMethod is not { IsPublic: true, IsStatic: true }
                    || property.GetIndexParameters().Length != 0 || string.IsNullOrWhiteSpace(attribute.Name)
                    || string.IsNullOrWhiteSpace(attribute.Label) || string.IsNullOrWhiteSpace(attribute.Description)
                    || string.IsNullOrWhiteSpace(attribute.Permission))
                    throw new InvalidOperationException($"Invalid UI capability declaration: {property.Name}.");
                var schema = property.GetMethod.CreateDelegate<Func<JsonElement>>()();
                if (schema.ValueKind != JsonValueKind.Object || schema.GetProperty("type").GetString() != "object")
                    throw new InvalidOperationException($"Invalid argument schema: {attribute.Name}.");
                return new UiCapabilityDescriptor(new(attribute.Name, attribute.Description, schema.Clone(), attribute.Permission), attribute.Label) {
                    RequiresTemplateCopyChoice = attribute.RequiresTemplateCopyChoice };
            }).ToArray();
        if (definitions.Select(definition => definition.Contract.Name).Distinct(StringComparer.Ordinal).Count() != definitions.Length)
            throw new InvalidOperationException("Duplicate UI capability name.");
        return definitions;
    }
}

public static class SemanticCapabilityCatalog
{
    public static IReadOnlyList<SemanticCapabilityDescriptor> All { get; } = Discover();

    private static IReadOnlyList<SemanticCapabilityDescriptor> Discover()
    {
        var definitions = typeof(SemanticOperation).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field =>
            {
                var attribute = field.GetCustomAttribute<AgentOperationAttribute>()
                    ?? throw new InvalidOperationException($"Missing agent operation declaration: {field.Name}.");
                if (string.IsNullOrWhiteSpace(attribute.Description) || attribute.ModelName is not null && string.IsNullOrWhiteSpace(attribute.ModelName)
                    || attribute.ToolNames.Any(string.IsNullOrWhiteSpace)
                    || attribute.ToolNames.Distinct(StringComparer.Ordinal).Count() != attribute.ToolNames.Length
                    || attribute.Writes.Concat(attribute.Requires).Any(value => !Enum.IsDefined(value)) || attribute.Writes.Distinct().Count() != attribute.Writes.Length
                    || attribute.Requires.Distinct().Count() != attribute.Requires.Length)
                    throw new InvalidOperationException($"Invalid agent operation declaration: {field.Name}.");
                return new SemanticCapabilityDescriptor((SemanticOperation)field.GetValue(null)!, attribute.Description, Array.AsReadOnly(attribute.ToolNames)) {
                    ModelName = attribute.ModelName ?? field.Name,
                    Writes = Array.AsReadOnly(attribute.Writes), Requires = Array.AsReadOnly(attribute.Requires) };
            }).ToArray();
        if (definitions.Select(item => item.ModelName).Distinct(StringComparer.Ordinal).Count() != definitions.Length)
            throw new InvalidOperationException("Duplicate model operation name.");
        return Array.AsReadOnly(definitions);
    }
}
