using System.Collections.Immutable;
using System.Text.RegularExpressions;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum AuthoringReferenceRole { FUTURE_TASK, CURRENT_SESSION, BOTH }
public sealed record AuthoringReferenceUse(int Id, AuthoringReferenceRole Role, string Evidence);
public sealed record AuthoringReferenceInterpretation(AuthoringReferenceUse[] Uses, string Question);

/// <summary>
/// Literal HTTP(S) reference coverage for one scoped authoring directive. Does not
/// interpret intent, fetch resources, insert content, or authorize session actions.
/// Callers remove case data using the existing input-scope owner before capture.
/// </summary>
public sealed class AuthoringReferenceCoverage
{
    private static readonly Regex Links = new("https?://[^\\s<>\"'`“”‘’«»]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private readonly string directive;
    [System.Text.Json.Serialization.JsonIgnore] public string Directive => directive;
    public ImmutableArray<string> References { get; }
    public ImmutableArray<AuthoringReferenceUse> Uses { get; private init; } = [];
    public bool Resolved => Uses.Length == References.Length;
    public string Fingerprint => LiveRuns.Hash(new { directive, References, Uses });
    public bool SameDirective(AuthoringReferenceCoverage other) => directive == other.directive;

    public AuthoringReferenceCoverage(string scopedDirective)
    {
        if (scopedDirective.Length > 32000) throw new ValidationException("El encargo de referencias supera el límite de autoría.");
        directive = scopedDirective;
        References = Read(directive).Distinct(StringComparer.Ordinal).ToImmutableArray();
        if (References.Length > 16 || References.Sum(value => value.Length) > 8000)
            throw new ValidationException("Reduce las referencias del encargo; no se truncarán silenciosamente.");
    }

    /// <summary>Bind complete source-grounded roles before drafting; never infer them from the proposed body.</summary>
    public AuthoringReferenceCoverage WithScopes(AuthoringReferenceInterpretation proposal)
    {
        if (!string.IsNullOrWhiteSpace(proposal.Question) || proposal.Uses is null || proposal.Uses.Length != References.Length
            || proposal.Uses.Select(use => use.Id).Distinct().Count() != References.Length
            || proposal.Uses.Any(use => use.Id < 1 || use.Id > References.Length || !Enum.IsDefined(use.Role)
                || string.IsNullOrWhiteSpace(use.Evidence) || !directive.Contains(use.Evidence, StringComparison.Ordinal)
                || !(use.Evidence == References[use.Id - 1]
                    || Read(use.Evidence).Contains(References[use.Id - 1], StringComparer.Ordinal))
                || use.Role is AuthoringReferenceRole.CURRENT_SESSION or AuthoringReferenceRole.BOTH
                    && !Links.Replace(use.Evidence, "").Any(char.IsLetterOrDigit)))
            throw Failure("AUTHORING_REFERENCE_SCOPE_INVALID", "Cada referencia necesita un papel y una cita contextual del encargo original, sin añadir ni omitir IDs.");
        return new(directive) { Uses = proposal.Uses.OrderBy(use => use.Id).ToImmutableArray() };
    }

    private static IEnumerable<string> Read(string text)
    {
        foreach (Match match in Links.Matches(text)) {
            var value = DelimitedToken(text, match) ?? Token(match.Value);
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") yield return value;
        }
    }

    // Explicit literal delimiters determine a URI boundary. Punctuation inside
    // them is part of the resource, including a final colon, period or parenthesis.
    // Unquoted prose retains the existing punctuation policy below.
    private static string? DelimitedToken(string text, Match match)
    {
        var start = match.Index; var end = start + match.Length;
        if (start >= 2 && text[start - 2] == ']' && text[start - 1] == '(') {
            var depth = 0;
            for (var index = 0; index < match.Value.Length; index++) {
                if (match.Value[index] == '(') depth++;
                else if (match.Value[index] == ')') {
                    if (depth == 0) return match.Value[..index];
                    depth--;
                }
            }
            return match.Value;
        }
        if (start == 0 || end == text.Length) return null;
        var closing = text[start - 1] switch {
            '<' => '>', '`' => '`', '"' => '"', '\'' => '\'',
            '“' => '”', '‘' => '’', '«' => '»', _ => '\0'
        };
        return closing != '\0' && text[end] == closing ? match.Value : null;
    }

    // Strip ordinary prose/Markdown delimiters, preserving balanced parentheses
    // inside paths and literal query/fragment characters. Never normalize the URI.
    private static string Token(string value)
    {
        while (value.Length > 0)
        {
            var end = value[^1];
            var opening = end switch { ')' => '(', ']' => '[', '}' => '{', _ => '\0' };
            if (end is '.' or ',' or ';' or ':' || opening != '\0' && value.Count(c => c == end) > value.Count(c => c == opening))
                value = value[..^1];
            else break;
        }
        return value;
    }

    /// <summary>The body invariant also applies when recording a semantic assessment after generation.</summary>
    public void ValidateBody(string body)
    {
        if (!Resolved) throw Failure("AUTHORING_REFERENCE_SCOPE_MISSING", "Falta interpretar el papel de las referencias.");
        var found = Read(body).ToHashSet(StringComparer.Ordinal);
        var mismatches = References.Select((uri, index) => new {
            id = index + 1, uri, role = Uses[index].Role.ToString(),
            expectedInBody = Uses[index].Role != AuthoringReferenceRole.CURRENT_SESSION,
            observedInBody = found.Contains(uri)
        }).Where(reference => reference.expectedInBody != reference.observedInBody).ToArray();
        if (mismatches.Length > 0) {
            var failure = Failure("AUTHORING_REFERENCE_BODY_MISMATCH", "El cuerpo no respeta las referencias y papeles fijados desde el encargo original.");
            failure.Data["NovaContractDetails"] = new { contract = "AUTHORING_REFERENCES/1", mismatches };
            throw failure;
        }
    }

    private static ValidationException Failure(string code, string message)
    {
        var failure = new ValidationException(message); failure.Data["NovaValidationCode"] = code; return failure;
    }
}
