using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

/// <summary>Proposes a bounded edit over a confirmed snapshot; never confirms content or effects.</summary>
public sealed partial class AuthoringRevisionEditor
{
    private readonly IValidatedAgentModelTransport transport;
    private readonly IAuthoringTranslationSourceViews? sourceViews;
    /// <summary>The optional legacy corpus parameter is accepted for constructor compatibility; translation no longer substitutes dictionary senses for source text.</summary>
    public AuthoringRevisionEditor(IValidatedAgentModelTransport transport, LocalTranslationLexicon? lexicalSource = null, IAuthoringTranslationSourceViews? sourceViews = null)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _ = lexicalSource;
        this.sourceViews = sourceViews;
    }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    public async Task<AuthoringBriefContent> Propose(string request, AuthoringClarification[] clarifications,
        AuthoringBriefContent established, string model, CancellationToken cancellation,
        PreparationPreferences? preferences = null, bool? shortening = null)
    {
        var review = await Review(request, clarifications, established, model, cancellation, preferences,
            new([], false, null, null, [], [], shortening == true));
        return review.Suggested ?? throw new ValidationException(review.Question!);
    }

    private static string Token(int id) => "⟦" + id + "⟧";

    private static string Encode(AuthoringTextSegment[] segments) => string.Concat(segments.Select(segment =>
        segment.InputId is { } id ? Token(id) : Literal(segment.Text!)));

    private static string Literal(string text) => text.IndexOfAny(['⟦', '⟧']) < 0 ? text
        : throw new ValidationException("El texto contiene delimitadores reservados de edición; usa otra representación antes de editar.");

    private static string EncodeText(string text, string[] names)
    {
        var encoded = new StringBuilder();
        var cursor = 0;
        foreach (var span in TemplateEditor.Spans(text)) {
            encoded.Append(Literal(text[cursor..span.Start]));
            encoded.Append(Token(Array.IndexOf(names, span.Name)));
            cursor = span.Start + span.Length;
        }
        return encoded.Append(Literal(text[cursor..])).ToString();
    }

    private static AuthoringTextSegment[] Decode(string encoded, int inputCount)
    {
        var segments = new List<AuthoringTextSegment>();
        var cursor = 0;
        while (cursor < encoded.Length) {
            var opening = encoded.IndexOf('⟦', cursor);
            var end = opening < 0 ? encoded.Length : opening;
            if (end > cursor) segments.Add(new(Literal(encoded[cursor..end]), null));
            if (opening < 0) break;
            var closing = encoded.IndexOf('⟧', opening + 1);
            if (closing < 0 || !int.TryParse(encoded.AsSpan(opening + 1, closing - opening - 1), out var id)
                || id < 0 || id >= inputCount || encoded[opening..(closing + 1)] != Token(id))
                throw new ValidationException("La reescritura contiene una variable codificada desconocida.");
            segments.Add(new(null, id));
            cursor = closing + 1;
        }
        return segments.ToArray();
    }
}
