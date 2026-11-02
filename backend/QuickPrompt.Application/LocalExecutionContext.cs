using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

/// <summary>
/// An exact template/value snapshot for work protocol v2. The hash binds content,
/// never identity, authorization, a lease or permission to persist an effect.
/// </summary>
public sealed record LocalExecutionContext(string Protocol, string Template, Dictionary<string, string> Values, string BasisHash)
{
    public const string Version = "quickprompt-execution-context/1";

    /// <summary>Copies only inputs used by the selected template; extra submission data stays on the server.</summary>
    public static LocalExecutionContext Create(string template, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(template) || values is null || values.Count > 60) throw Invalid();
        var names = TemplateCompiler.Names(template);
        var bound = names.ToDictionary(name => name, name => values.GetValueOrDefault(name) ?? "", StringComparer.Ordinal);
        _ = TemplateCompiler.Render(template, bound);
        return new(Version, template, bound, Hash(template, bound));
    }

    /// <summary>Returns an isolated copy after checking source, variables and exact compilation against its lease.</summary>
    public LocalExecutionContext Validate(string instruction)
    {
        if (Protocol != Version || Template is null || Values is null || BasisHash is null) throw Invalid();
        var copy = Create(Template, Values);
        if (Values.Count != copy.Values.Count || BasisHash != copy.BasisHash
            || TemplateCompiler.Render(copy.Template, copy.Values) != instruction) throw Invalid();
        return copy;
    }

    private static string Hash(string template, Dictionary<string, string> values) => LiveRuns.Hash(new {
        protocol = Version, template, values = values.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray()
    });
    private static ValidationException Invalid() => new("El contexto de ejecución no corresponde a su instrucción y valores originales.");
}
