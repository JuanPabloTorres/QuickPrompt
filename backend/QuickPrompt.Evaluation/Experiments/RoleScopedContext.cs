using System.ComponentModel;
using System.Reflection;

namespace QuickPrompt.Web.Infrastructure;

// Unqualified semantic experiment, outside the production project graph.
// Trusted composition selects a type; user data cannot select or override a role.
public static class RoleScopedContext
{
    public static AgentModelContext Describe<T>(T value)
    {
        var guide = typeof(T).GetCustomAttribute<DescriptionAttribute>(inherit: false)?.Description;
        if (string.IsNullOrWhiteSpace(guide) || guide.Length > 8000)
            throw new InvalidOperationException("The model context requires a bounded role description on its actual type.");
        return new(guide, ValidatedAgentJson.Encode(value), ValidatedAgentJson.SchemaHash<T>());
    }
}

public sealed record AgentModelContext(string SystemGuide, string DocumentJson, string SchemaHash);
