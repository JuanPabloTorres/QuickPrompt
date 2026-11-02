using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

/// <summary>
/// Read-only evidence from a complete, pinned EN→ES dictionary, inverted for
/// ES→EN lookup. Matches are lexical evidence, never semantic approval.
/// </summary>
public sealed partial class LocalTranslationLexicon
{
    public const string DefaultSha256 = "85c6055f18be00f8b2edfccd76c0f050e8aa4575d54aaa61fe597e54fcef950d";
    private const string ResourceName = "QuickPrompt.Translation.eng-spa.2025.11.23.tei.gz";
    private const string DefaultSourceUrl = "https://download.freedict.org/dictionaries/eng-spa/2025.11.23/freedict-eng-spa-2025.11.23.src.tar.xz";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly object loadGate = new();
    private readonly Func<CancellationToken, Corpus> load;
    private Corpus? corpus;

    private LocalTranslationLexicon(Func<CancellationToken, Corpus> load) => this.load = load;
    private LocalTranslationLexicon(Corpus corpus) { this.corpus = corpus; load = _ => corpus; }

    /// <summary>The embedded resource is not opened until the first supported Read.</summary>
    public static LocalTranslationLexicon Default { get; } = new(cancellation => {
        using var compressed = typeof(LocalTranslationLexicon).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new ValidationException("El recurso léxico local no está disponible.");
        using var tei = new GZipStream(compressed, CompressionMode.Decompress);
        return Parse(tei, DefaultSha256, cancellation);
    });

    /// <summary>Reports capability without opening the dictionary. Regional tags use their base language.</summary>
    public bool SupportsDirection(string sourceLanguage, string targetLanguage) =>
        BaseLanguage(sourceLanguage) == "es" && BaseLanguage(targetLanguage) == "en";

    /// <summary>
    /// Loads uncompressed TEI only after checking its complete SHA256. The caller
    /// owns the stream. Invalid, oversized or cancelled input never yields a lexicon.
    /// </summary>
    public static LocalTranslationLexicon FromTei(Stream tei, string expectedHash, CancellationToken cancellation = default) =>
        new(Parse(tei, expectedHash, cancellation));

    /// <summary>
    /// Returns every matching edge, with original headwords, translations and
    /// senses. Each lookup address is [entryId,senseId,headwordIndex,translationIndex].
    /// Only NFC/case normalization is used; no stemming, sense ranking or new words.
    /// </summary>
    public JsonElement Read(string sourceLanguage, string targetLanguage, string[] exactTerms, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!SupportsDirection(sourceLanguage, targetLanguage))
            throw new ValidationException("El recurso léxico local sólo ofrece evidencia español→inglés.");
        if (exactTerms is null || exactTerms.Length > 16000 || exactTerms.Any(term => string.IsNullOrWhiteSpace(term) || term.Length > 16000)
            || exactTerms.Sum(term => term.Length) > 16000)
            throw new ValidationException("La consulta léxica excede sus límites o contiene un término vacío.");
        var source = GetCorpus(cancellation);
        var entries = new Dictionary<string, EvidenceEntry>(StringComparer.Ordinal);
        var lookup = new Dictionary<string, int[][]>(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var term in exactTerms.Distinct(StringComparer.Ordinal)) {
            cancellation.ThrowIfCancellationRequested();
            if (!source.Index.TryGetValue(Normalize(term), out var edges)) {
                lookup.Add(term, []); missing.Add(term); continue;
            }
            lookup.Add(term, edges.Select(edge => new[] { edge.EntryId, edge.SenseId, edge.HeadwordIndex, edge.TranslationIndex }).ToArray());
            foreach (var edge in edges) {
                cancellation.ThrowIfCancellationRequested();
                var entry = source.Entries[edge.EntryId - 1];
                var id = edge.EntryId.ToString(CultureInfo.InvariantCulture);
                if (!entries.TryGetValue(id, out var selected)) {
                    selected = new(entry.Headwords, entry.PartsOfSpeech, new(StringComparer.Ordinal));
                    entries.Add(id, selected);
                }
                selected.Senses.TryAdd(edge.SenseId.ToString(CultureInfo.InvariantCulture), entry.Senses[edge.SenseId]);
            }
        }
        cancellation.ThrowIfCancellationRequested();
        var evidence = JsonSerializer.SerializeToElement(new {
            sourceLanguage = BaseLanguage(sourceLanguage), targetLanguage = BaseLanguage(targetLanguage),
            corpus = new { source.Edition, source.Sha256, source.License, source.LicenseUrl, source.Attribution,
                sourceUrl = source.Sha256 == DefaultSha256 ? DefaultSourceUrl : null, source.SourceReferences,
                sourceDirection = "en-es", inverseLookup = true,
                entryCount = source.Entries.Length, edgeCount = source.EdgeCount },
            entries, lookup, missingTerms = missing
        }, Json);
        cancellation.ThrowIfCancellationRequested();
        return evidence;
    }

    /// <summary>
    /// Projects Read's complete matched edges for model context. Entry tuples are
    /// [headwords,partsOfSpeech,senses]; each sense contains all its definitions.
    /// Lookup tuples append the original translation text to its original address.
    /// Unmatched Spanish synonyms are omitted; matching senses/edges are never ranked.
    /// </summary>
    public JsonElement ReadContext(string sourceLanguage, string targetLanguage, string[] exactTerms, CancellationToken cancellation = default)
    {
        var complete = Read(sourceLanguage, targetLanguage, exactTerms, cancellation);
        var entries = new Dictionary<string, object[]>(StringComparer.Ordinal);
        foreach (var entry in complete.GetProperty("entries").EnumerateObject()) {
            cancellation.ThrowIfCancellationRequested();
            entries.Add(entry.Name, [entry.Value.GetProperty("headwords"), entry.Value.GetProperty("partsOfSpeech"),
                entry.Value.GetProperty("senses").EnumerateObject().ToDictionary(sense => sense.Name,
                    sense => sense.Value.GetProperty("definitions"), StringComparer.Ordinal)]);
        }
        var lookup = new Dictionary<string, object[][]>(StringComparer.Ordinal);
        foreach (var term in complete.GetProperty("lookup").EnumerateObject()) {
            var matches = new List<object[]>();
            foreach (var address in term.Value.EnumerateArray()) {
                cancellation.ThrowIfCancellationRequested();
                var entryId = address[0].GetInt32(); var senseId = address[1].GetInt32();
                var headwordIndex = address[2].GetInt32(); var translationIndex = address[3].GetInt32();
                var originalSense = complete.GetProperty("entries").GetProperty(entryId.ToString(CultureInfo.InvariantCulture))
                    .GetProperty("senses").GetProperty(senseId.ToString(CultureInfo.InvariantCulture));
                matches.Add([entryId, senseId, headwordIndex, translationIndex,
                    originalSense.GetProperty("translations")[translationIndex].GetString()!]);
            }
            lookup.Add(term.Name, matches.ToArray());
        }
        cancellation.ThrowIfCancellationRequested();
        var context = JsonSerializer.SerializeToElement(new {
            sourceLanguage = complete.GetProperty("sourceLanguage"), targetLanguage = complete.GetProperty("targetLanguage"),
            corpus = complete.GetProperty("corpus"),
            layout = new {
                entry = new[] { "headwords", "partsOfSpeech", "senses" }, sense = "complete definition array",
                lookup = new[] { "entryId", "senseId", "headwordIndex", "sourceTranslationIndex", "sourceTranslationText" }
            },
            entries, lookup, missingTerms = complete.GetProperty("missingTerms")
        }, Json);
        cancellation.ThrowIfCancellationRequested();
        return context;
    }

    private Corpus GetCorpus(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        lock (loadGate) {
            cancellation.ThrowIfCancellationRequested();
            // Only successful loads are cached. A cancelled first query cannot poison later queries.
            return corpus ??= load(cancellation);
        }
    }

    private static string BaseLanguage(string? language) => (language ?? "").Trim().Split('-', 2)[0].ToLowerInvariant();
    private static string Normalize(string term) => term.Normalize(NormalizationForm.FormC).ToLowerInvariant();
    private sealed record Edge(int EntryId, int SenseId, int HeadwordIndex, int TranslationIndex);
    private sealed record Sense(string[] Definitions, string[] Translations);
    private sealed record Entry(string[] Headwords, string[] PartsOfSpeech, Dictionary<int, Sense> Senses);
    private sealed record EvidenceEntry(string[] Headwords, string[] PartsOfSpeech, Dictionary<string, Sense> Senses);
    private sealed record Corpus(string Edition, string Sha256, string License, string LicenseUrl, string Attribution, string[] SourceReferences,
        Entry[] Entries, Dictionary<string, List<Edge>> Index, int EdgeCount);
}
