using System.Globalization;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// One request-local projection shared by meaning and value binding. No permission,
// persisted state or rewritten user text is accepted from the model.
public sealed class UserInputScope
{
    private sealed record Binding(string Name, string Value, int LabelStart, int ValueStart, int End);
    private readonly string original;
    private readonly Binding[] bindings;
    public Guid? SourceId { get; }
    public int? SourceRevision { get; }
    public string[] Names => bindings.Select(binding => binding.Name).ToArray();
    public UserInputScope BindTarget(Guid id, int revision, string[] names)
    {
        if (SourceId is { } bound && (bound != id || SourceRevision != revision))
            throw new ConflictException("El ámbito de datos pertenece a otro origen o revisión.");
        if (SourceId is not null) { Validate(original, names); return this; }
        return Bind(original, id, revision, names, bindings.Select(binding => new GroundedValue(binding.Name, binding.Value, binding.Value)).ToArray());
    }
    private UserInputScope(string request, Guid? sourceId, int? revision, Binding[] bindings)
        => (original, SourceId, SourceRevision, this.bindings) = (request, sourceId, revision, bindings);

    public static UserInputScope Bind(string request, Guid? sourceId, int? revision, string[] names, GroundedValue[] proposed)
    {
        if ((sourceId is null) != (revision is null)) throw new ValidationException("El origen y su revisión deben estar ligados juntos.");
        var literals = GroundedVariableResolver.LiteralAssignments(request, names);
        if (GroundedVariableResolver.AmbiguousLabels(request, names).Length > 0 || proposed.Length != literals.Count
            || proposed.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != proposed.Length)
            throw new ValidationException("El ámbito requiere una asignación inequívoca por campo declarado.");
        var boundaries = StringInfo.ParseCombiningCharacters(request).Append(request.Length).ToHashSet();
        var ranges = GroundedVariableResolver.InputRanges(request, names);
        var bound = new List<Binding>();
        foreach (var value in proposed) {
            if (!literals.TryGetValue(value.Name, out var literal) || string.IsNullOrWhiteSpace(value.Value)
                || value.Value.Length > 8000 || value.Evidence != value.Value || !literal.StartsWith(value.Value, StringComparison.Ordinal))
                throw new ValidationException("El valor delimitado debe ser un prefijo literal del campo real, sin reformularlo.");
            var matches = ranges.Where(range => request[range.LabelStart..range.ValueStart].Trim().TrimEnd(':').TrimEnd()
                    .Equals(value.Name, StringComparison.OrdinalIgnoreCase))
                .Select(range => (Range: range, Start: request.IndexOf(literal, range.ValueStart, range.End - range.ValueStart, StringComparison.Ordinal)))
                .Where(item => item.Start >= 0).ToArray();
            if (matches.Length != 1) throw new ValidationException("No se pudo ligar el valor a un único rango original.");
            var match = matches[0]; var end = match.Start + value.Value.Length;
            if (!boundaries.Contains(match.Start) || !boundaries.Contains(end)) throw new ValidationException("El ámbito no puede cortar un carácter Unicode.");
            bound.Add(new(value.Name, request[match.Start..end], match.Range.LabelStart, match.Start, end));
        }
        return new(request, sourceId, revision, bound.OrderBy(value => value.LabelStart).ToArray());
    }
    public static UserInputScope FromExtraction(string request, Guid? sourceId, int? revision, string[] names, GroundedValue[] extracted)
    {
        var literals = GroundedVariableResolver.LiteralAssignments(request, names);
        var prefixes = extracted.Select(value => {
            if (!literals.TryGetValue(value.Name, out var literal) || string.IsNullOrWhiteSpace(value.Value)
                || string.IsNullOrWhiteSpace(value.Evidence) || !request.Contains(value.Evidence, StringComparison.Ordinal)
                || !value.Evidence.Contains(value.Value, StringComparison.Ordinal))
                throw new ValidationException("La extracción no cita datos originales del campo.");
            var end = value.Value.Length;
            if (!literal.StartsWith(value.Value, StringComparison.Ordinal)) {
                // Explicit string delimiters are syntax, not natural-language intent.
                // Preserve the complete quoted token instead of stripping its quotes.
                if (literal.Length < 2 || literal[0] is not ('\'' or '"' or '`')
                    || !literal.StartsWith(literal[0] + value.Value + literal[0], StringComparison.Ordinal))
                    throw new ValidationException("La extracción no puede saltar el inicio del dato declarado.");
                end += 2;
            }
            // A semantic citation may omit adjacent punctuation. Bind complete
            // original whitespace units; never promote a sliced token to authority.
            while (end < literal.Length && !char.IsWhiteSpace(literal[end])) end++;
            if (end == literal.TrimEnd().Length) end = literal.Length;
            var prefix = literal[..end]; return new GroundedValue(value.Name, prefix, prefix);
        }).ToArray();
        return Bind(request, sourceId, revision, names, prefixes);
    }
    public Dictionary<string, string> Values(string request, string[] names)
    {
        Validate(request, names); return bindings.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);
    }
    public bool Matches(string request) => request == original;
    public void ValidateTarget(Guid? id, int? revision)
    {
        if (SourceId is { } expected && (id != expected || revision != SourceRevision))
            throw new ConflictException("Los datos delimitados pertenecen a otro origen o revisión.");
    }
    private void Validate(string request, string[] names)
    {
        if (request != original || bindings.Any(value => !names.Contains(value.Name, StringComparer.Ordinal)))
            throw new ValidationException("El ámbito pertenece a otra petición o a otros campos.");
    }
    public string Project(string request, string[] names, bool removeLabels, string[]? maskedNames = null)
    {
        Validate(request, names);
        var text = new System.Text.StringBuilder(); var cursor = 0;
        foreach (var value in bindings.Where(value => maskedNames is null || maskedNames.Contains(value.Name, StringComparer.OrdinalIgnoreCase))) {
            var start = removeLabels ? value.LabelStart : value.ValueStart;
            text.Append(request[cursor..start]);
            text.Append(removeLabels ? "\n" : "[literal input data]");
            cursor = value.End;
        }
        return text.Append(request[cursor..]).ToString();
    }
    /// <summary>Trusted original ranges for authoring projection; values remain owned by input extraction.</summary>
    public AuthoringCaseRange[] AuthoringRanges(string request, string[] names)
    {
        Validate(request, names);
        return bindings.Select(binding => new AuthoringCaseRange(binding.LabelStart, binding.End - binding.LabelStart, binding.Name)).ToArray();
    }
}

public interface IInputScopeInterpreter
{
    Task<UserInputScope?> ResolveInputScope(string request, SemanticContext context, string model, CancellationToken ct);
}
