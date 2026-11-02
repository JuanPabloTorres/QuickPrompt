using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class LocalTranslationLexicon
{
    private const int MaximumTeiBytes = 48000000;
    private static readonly XNamespace Tei = "http://www.tei-c.org/ns/1.0";

    private static Corpus Parse(Stream source, string expectedHash, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellation.ThrowIfCancellationRequested();
        if (expectedHash is null || expectedHash.Length != 64 || expectedHash.Any(character => !Uri.IsHexDigit(character)))
            throw new ValidationException("La huella del recurso léxico no es SHA256.");
        try {
            using var bytes = ReadBounded(source, cancellation);
            var hash = Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length)))).ToLowerInvariant();
            if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("La huella del recurso léxico no coincide; no se usará evidencia sin verificar.");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null,
                MaxCharactersInDocument = MaximumTeiBytes, CloseInput = false };
            using var reader = XmlReader.Create(bytes, settings);
            var entries = new List<Entry>();
            var index = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
            XElement? header = null;
            var englishBody = false;
            var edgeCount = 0;
            while (reader.Read()) {
                cancellation.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI != Tei.NamespaceName) continue;
                if (reader.LocalName == "teiHeader") header = ReadElement(reader);
                else if (reader.LocalName == "body") englishBody = reader.GetAttribute("lang", XNamespace.Xml.NamespaceName) == "en";
                else if (reader.LocalName == "entry") {
                    if (!englishBody || header is null) throw new ValidationException("La dirección del corpus léxico no es inglés→español.");
                    var entry = ReadEntry(ReadElement(reader), cancellation);
                    entries.Add(entry);
                    foreach (var (senseId, sense) in entry.Senses)
                        for (var headwordIndex = 0; headwordIndex < entry.Headwords.Length; headwordIndex++)
                            for (var translationIndex = 0; translationIndex < sense.Translations.Length; translationIndex++) {
                                cancellation.ThrowIfCancellationRequested();
                                var key = Normalize(sense.Translations[translationIndex]);
                                if (!index.TryGetValue(key, out var edges)) index.Add(key, edges = []);
                                edges.Add(new(entries.Count, senseId, headwordIndex, translationIndex)); edgeCount++;
                            }
                }
            }
            if (header is null || entries.Count == 0 || edgeCount == 0)
                throw new ValidationException("El corpus léxico completo no contiene entradas utilizables.");
            var edition = Text(header.Descendants(Tei + "edition").FirstOrDefault());
            var availability = header.Descendants(Tei + "availability").SingleOrDefault();
            var license = Text(availability);
            var licenseUrl = availability?.Descendants(Tei + "ref").FirstOrDefault()?.Attribute("target")?.Value ?? "";
            var origin = string.Join(" ", header.Descendants(Tei + "sourceDesc").Elements(Tei + "p").Select(Text));
            var attribution = string.Join(" ", new[] { Text(header.Descendants(Tei + "title").FirstOrDefault()),
                string.Join(", ", header.Descendants(Tei + "titleStmt").Descendants(Tei + "name").Select(Text)), origin }.Where(value => value.Length > 0));
            var sourceReferences = header.Descendants(Tei + "sourceDesc").Descendants(Tei + "ref")
                .Select(reference => reference.Attribute("target")?.Value).OfType<string>().ToArray();
            if (string.IsNullOrWhiteSpace(edition) || string.IsNullOrWhiteSpace(license) || string.IsNullOrWhiteSpace(licenseUrl)
                || string.IsNullOrWhiteSpace(origin))
                throw new ValidationException("El corpus léxico no declara edición, licencia y procedencia completas.");
            cancellation.ThrowIfCancellationRequested();
            return new(edition, hash, license, licenseUrl, attribution, sourceReferences, entries.ToArray(), index, edgeCount);
        } catch (Exception failure) when (failure is XmlException or InvalidDataException or IOException or InvalidOperationException) {
            throw new ValidationException("El recurso léxico local está dañado o contiene XML no admitido.");
        }
    }

    private static MemoryStream ReadBounded(Stream source, CancellationToken cancellation)
    {
        var result = new MemoryStream();
        try {
            var buffer = new byte[65536];
            int read;
            while ((read = source.Read(buffer)) > 0) {
                cancellation.ThrowIfCancellationRequested();
                if (result.Length + read > MaximumTeiBytes)
                    throw new ValidationException("El corpus léxico excede el límite completo; no se truncará.");
                result.Write(buffer, 0, read);
            }
            cancellation.ThrowIfCancellationRequested();
            result.Position = 0;
            return result;
        } catch { result.Dispose(); throw; }
    }

    private static XElement ReadElement(XmlReader reader)
    {
        using var subtree = reader.ReadSubtree();
        return XElement.Load(subtree, LoadOptions.None);
    }

    private static Entry ReadEntry(XElement element, CancellationToken cancellation)
    {
        var headwords = element.Elements(Tei + "form").Elements(Tei + "orth").Select(Text).ToArray();
        var partsOfSpeech = element.Elements(Tei + "gramGrp").Elements(Tei + "pos").Select(Text).ToArray();
        var senses = new Dictionary<int, Sense>();
        var senseId = 0;
        foreach (var sense in element.Descendants(Tei + "sense")) {
            cancellation.ThrowIfCancellationRequested(); senseId++;
            // Child definition senses describe the parent's translation group.
            // Direct citations avoid assigning another sense's translations to this group.
            var translations = sense.Elements(Tei + "cit").Where(citation => citation.Attribute("type")?.Value == "trans"
                && citation.Attribute(XNamespace.Xml + "lang")?.Value == "es").Elements(Tei + "quote").Select(Text).ToArray();
            if (translations.Length == 0) continue;
            if (sense.Descendants(Tei + "sense").Any(child => child.Elements(Tei + "cit").Any(citation => citation.Attribute("type")?.Value == "trans")))
                throw new ValidationException("El corpus contiene traducciones anidadas cuya asociación de acepciones no está admitida.");
            senses.Add(senseId, new(sense.Descendants(Tei + "def").Select(Text).ToArray(), translations));
        }
        if (headwords.Any(string.IsNullOrWhiteSpace) || senses.Values.Any(sense => sense.Translations.Any(string.IsNullOrWhiteSpace)))
            throw new ValidationException("Una entrada del corpus léxico contiene texto vacío.");
        return new(headwords, partsOfSpeech, senses);
    }

    private static string Text(XElement? element) => element?.Value.Trim() ?? "";
}
