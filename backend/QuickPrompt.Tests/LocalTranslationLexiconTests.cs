using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace QuickPrompt.Nova.Tests;

public sealed class LocalTranslationLexiconTests(ITestOutputHelper output)
{
    [Fact]
    public void AllSensesDuplicateEdgesAndAlternateHeadwordsRemainRecoverable()
    {
        var lexicon = Load(Fixture());
        var evidence = lexicon.Read("es", "en", ["fechar", "FECHAR", "ausente"]);
        Assert.Equal(5, evidence.GetProperty("lookup").GetProperty("fechar").GetArrayLength());
        Assert.Equal(5, evidence.GetProperty("lookup").GetProperty("FECHAR").GetArrayLength());
        var matches = Recover(evidence, "fechar");
        Assert.Equal(new[] { "date", "date", "date alias", "date alias", "slate" }, matches.Select(match => match.Word));
        Assert.All(matches, match => Assert.Equal("fechar", match.Translation));
        Assert.All(matches.Take(4), match => Assert.Equal(new[] { "Add a date.", "Determine age." }, match.Definitions));
        Assert.Equal(new[] { "Schedule an event." }, matches[4].Definitions);
        Assert.Equal(new[] { "ausente" }, evidence.GetProperty("missingTerms").EnumerateArray().Select(term => term.GetString()));
        Assert.Empty(evidence.GetProperty("lookup").GetProperty("ausente").EnumerateArray());
        Assert.Equal(2, evidence.GetProperty("entries").EnumerateObject().Count());
        Assert.Equal(3, evidence.GetProperty("corpus").GetProperty("entryCount").GetInt32());
        Assert.Equal(6, evidence.GetProperty("corpus").GetProperty("edgeCount").GetInt32());
    }

    [Fact]
    public void EvidenceBindsTheWholeInputHashLicenseAndSourceWithoutClaimingOfficialFixtureProvenance()
    {
        var xml = Fixture();
        var evidence = Load(xml).Read("es-BO", "en-US", ["fechar"]);
        var corpus = evidence.GetProperty("corpus");
        Assert.Equal(Hash(xml), corpus.GetProperty("sha256").GetString());
        Assert.Equal("test-edition", corpus.GetProperty("edition").GetString());
        Assert.Contains("CC-BY-SA-3.0", corpus.GetProperty("license").GetString());
        Assert.Equal("https://creativecommons.org/licenses/by-sa/3.0/", corpus.GetProperty("licenseUrl").GetString());
        Assert.Contains("Synthetic source", corpus.GetProperty("attribution").GetString());
        Assert.Null(corpus.GetProperty("sourceUrl").GetString());
        Assert.Equal("https://example.test/corpus", corpus.GetProperty("sourceReferences")[0].GetString());
        Assert.True(corpus.GetProperty("inverseLookup").GetBoolean()); Assert.Equal("en-es", corpus.GetProperty("sourceDirection").GetString());
        Assert.Equal("es", evidence.GetProperty("sourceLanguage").GetString()); Assert.Equal("en", evidence.GetProperty("targetLanguage").GetString());
    }

    [Theory]
    [InlineData("es", "en", true)]
    [InlineData("ES-bo", "EN-us", true)]
    [InlineData("en", "es", false)]
    [InlineData("pt", "en", false)]
    [InlineData("es", "fr", false)]
    public void DirectionNegotiationIsSeparateFromLookup(string source, string target, bool supported)
    {
        Assert.Equal(supported, LocalTranslationLexicon.Default.SupportsDirection(source, target));
        if (!supported) Assert.Throws<ValidationException>(() => LocalTranslationLexicon.Default.Read(source, target, ["fechar"]));
    }

    [Fact]
    public void LookupNormalizesNfcAndCaseButPreservesLiteralCorpusTextAndDoesNotInventInflections()
    {
        var evidence = Load(Fixture()).Read("es", "en", ["revisio\u0301n", "revision", "revisiones"]);
        Assert.Equal("Revisión", Assert.Single(Recover(evidence, "revisio\u0301n")).Translation);
        Assert.Equal(new[] { "revision", "revisiones" }, evidence.GetProperty("missingTerms").EnumerateArray().Select(term => term.GetString()));
    }

    [Fact]
    public void AReturnedJsonValueCannotMutateLaterQueries()
    {
        var lexicon = Load(Fixture());
        var first = lexicon.Read("es", "en", ["fechar"]);
        var second = lexicon.Read("es", "en", ["revisión"]);
        Assert.Equal(5, Recover(first, "fechar").Length);
        Assert.False(second.GetProperty("lookup").TryGetProperty("fechar", out _));
        Assert.Single(Recover(second, "revisión"));
    }

    [Fact]
    public void CorruptionOrAnIncorrectHashRejectsTheCompleteResource()
    {
        var xml = Fixture();
        using var changed = new MemoryStream(Encoding.UTF8.GetBytes(xml.Replace("date alias", "tampered", StringComparison.Ordinal)));
        Assert.Throws<ValidationException>(() => LocalTranslationLexicon.FromTei(changed, Hash(xml)));
        using var valid = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        Assert.Throws<ValidationException>(() => LocalTranslationLexicon.FromTei(valid, "not-a-sha256"));
    }

    [Fact]
    public void CallerOwnsTheInputStreamAfterLoading()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Fixture()));
        var lexicon = LocalTranslationLexicon.FromTei(stream, Hash(Fixture()));
        Assert.True(stream.CanRead); stream.Position = 0; Assert.True(stream.ReadByte() >= 0);
        Assert.Single(Recover(lexicon.Read("es", "en", ["revisión"]), "revisión"));
    }

    [Theory]
    [InlineData("<!DOCTYPE TEI [<!ENTITY injected 'not-source-data'>]>")]
    [InlineData("<!DOCTYPE TEI [<!ENTITY injected SYSTEM 'urn:must-not-be-resolved'>]>")]
    public void EntityReferencesAreRejectedInsteadOfExpandedOrFetched(string doctype)
    {
        var xml = doctype + Fixture().Replace("date alias", "&injected;", StringComparison.Ordinal);
        Assert.Throws<ValidationException>(() => Load(xml));
    }

    [Fact]
    public void ExternalDtdIsIgnoredWithoutFetchingIt()
    {
        var xml = "<!DOCTYPE TEI SYSTEM 'urn:must-not-be-resolved'>" + Fixture();
        Assert.Equal(5, Recover(Load(xml).Read("es", "en", ["fechar"]), "fechar").Length);
    }

    [Fact]
    public void NestedTranslationGroupsCannotBorrowAnUnrelatedSensesDefinition()
    {
        var xml = Fixture().Replace("<sense><def>Add a date.</def></sense>",
            "<sense><cit type=\"trans\" xml:lang=\"es\"><quote>otra operación</quote></cit><def>Unrelated meaning.</def></sense>", StringComparison.Ordinal);
        var failure = Assert.Throws<ValidationException>(() => Load(xml));
        Assert.Contains("anidadas", failure.Message);
    }

    [Theory]
    [InlineData("missing-header")]
    [InlineData("wrong-language")]
    [InlineData("malformed")]
    [InlineData("missing-license")]
    public void InvalidSourceContractsFailClosed(string failure)
    {
        var xml = failure switch {
            "missing-header" => Fixture().Replace("teiHeader", "unknownHeader", StringComparison.Ordinal),
            "wrong-language" => Fixture().Replace("body xml:lang=\"en\"", "body xml:lang=\"pt\"", StringComparison.Ordinal),
            "missing-license" => Fixture().Replace("availability", "unknownAvailability", StringComparison.Ordinal),
            _ => Fixture()[..^8]
        };
        Assert.Throws<ValidationException>(() => Load(xml));
    }

    [Fact]
    public void CancelledLoadsAndQueriesDoNotReturnPartialEvidence()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(Fixture()));
        Assert.ThrowsAny<OperationCanceledException>(() => LocalTranslationLexicon.FromTei(input, Hash(Fixture()), cancelled.Token));
        Assert.Equal(0, input.Position);
        var lexicon = Load(Fixture());
        Assert.ThrowsAny<OperationCanceledException>(() => lexicon.Read("es", "en", ["fechar"], cancelled.Token));
        using var duringLoad = new CancellationTokenSource();
        using var stream = new CancelOnReadStream(duringLoad);
        Assert.ThrowsAny<OperationCanceledException>(() => LocalTranslationLexicon.FromTei(stream, new string('0', 64), duringLoad.Token));
        Assert.Equal(5, Recover(lexicon.Read("es", "en", ["fechar"]), "fechar").Length);
    }

    [Fact]
    public void QueryAndDocumentLimitsRejectOverflowWithoutTruncating()
    {
        var lexicon = Load(Fixture());
        Assert.Throws<ValidationException>(() => lexicon.Read("es", "en", [""]));
        Assert.Throws<ValidationException>(() => lexicon.Read("es", "en", Enumerable.Repeat("x", 16001).ToArray()));
        Assert.Throws<ValidationException>(() => lexicon.Read("es", "en", [new string('x', 16001)]));
        using var oversized = new RepeatingStream(48000001);
        Assert.Throws<ValidationException>(() => LocalTranslationLexicon.FromTei(oversized, new string('0', 64)));
    }

    [Fact]
    public void SourceExtractionCanQueryMoreThan256TermsWithoutTruncatingMissingWords()
    {
        var terms = Enumerable.Range(0, 300).Select(index => "missing" + index).ToArray();
        var context = Load(Fixture()).ReadContext("es", "en", terms);
        Assert.Equal(terms, context.GetProperty("missingTerms").EnumerateArray().Select(term => term.GetString()));
        Assert.Equal(300, context.GetProperty("lookup").EnumerateObject().Count());
    }

    [Fact]
    public void ALongLiteralWordWithinTheSourceBudgetIsReportedMissingWithoutTruncation()
    {
        var literal = new string('z', 8000);
        var context = Load(Fixture()).ReadContext("es", "en", [literal]);
        Assert.Equal(literal, Assert.Single(context.GetProperty("missingTerms").EnumerateArray()).GetString());
        Assert.Empty(context.GetProperty("lookup").GetProperty(literal).EnumerateArray());
    }

    [Fact]
    public void ContextProjectionPreservesEveryMatchedTupleAndDefinitionWithoutUnqueriedSynonyms()
    {
        // A literal synonym shares a sense with a match but is not itself a query.
        var xml = Fixture().Replace("<quote>fechar</quote><quote>fechar</quote>",
            "<quote>fechar</quote><quote>no consultado</quote><quote>fechar</quote>", StringComparison.Ordinal);
        var lexicon = Load(xml); string[] terms = ["fechar", "FECHAR", "revisio\u0301n", "missing"];
        var complete = lexicon.Read("es", "en", terms);
        var context = lexicon.ReadContext("es", "en", terms);
        AssertContextMatchesComplete(complete, context);
        Assert.DoesNotContain("no consultado", context.GetRawText());
        Assert.Contains("no consultado", complete.GetRawText());
        Assert.Equal(new[] { 0, 2, 0, 2, 0 }, context.GetProperty("lookup").GetProperty("fechar").EnumerateArray().Select(address => address[3].GetInt32()));
        Assert.Equal(new[] { "headwords", "partsOfSpeech", "senses" },
            context.GetProperty("layout").GetProperty("entry").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void TheDefaultResourceContainsTheCompletePinnedDictionaryRatherThanTheProbeVocabulary()
    {
        var before = GC.GetTotalMemory(true); var watch = Stopwatch.StartNew();
        var evidence = LocalTranslationLexicon.Default.Read("es", "en",
            ["fechar", "firmar", "sellar", "numerar", "archivar", "aprobar", "finalizar", "continuar", "qp-no-such-lexeme"]);
        watch.Stop(); var after = GC.GetTotalMemory(true);
        var corpus = evidence.GetProperty("corpus");
        Assert.Equal(LocalTranslationLexicon.DefaultSha256, corpus.GetProperty("sha256").GetString());
        Assert.Equal(64258, corpus.GetProperty("entryCount").GetInt32()); Assert.Equal(133073, corpus.GetProperty("edgeCount").GetInt32());
        Assert.Equal(new[] { "qp-no-such-lexeme" }, evidence.GetProperty("missingTerms").EnumerateArray().Select(term => term.GetString()));
        Assert.Equal(new[] { "date", "slate" }, Recover(evidence, "fechar").Select(match => match.Word));
        Assert.Contains("3.0", corpus.GetProperty("license").GetString());
        AssertContextMatchesComplete(evidence, LocalTranslationLexicon.Default.ReadContext("es", "en",
            ["fechar", "firmar", "sellar", "numerar", "archivar", "aprobar", "finalizar", "continuar", "qp-no-such-lexeme"]));
        output.WriteLine($"Default load+lookup: {watch.ElapsedMilliseconds} ms; managed retained delta: {after - before} bytes. Measurements are local, not release performance guarantees.");
    }

    private static LocalTranslationLexicon Load(string xml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return LocalTranslationLexicon.FromTei(stream, Hash(xml));
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static void AssertContextMatchesComplete(JsonElement complete, JsonElement context)
    {
        Assert.Equal(complete.GetProperty("corpus").GetRawText(), context.GetProperty("corpus").GetRawText());
        Assert.Equal(complete.GetProperty("missingTerms").GetRawText(), context.GetProperty("missingTerms").GetRawText());
        foreach (var term in complete.GetProperty("lookup").EnumerateObject()) {
            var compactAddresses = context.GetProperty("lookup").GetProperty(term.Name);
            Assert.Equal(term.Value.GetArrayLength(), compactAddresses.GetArrayLength());
            for (var index = 0; index < term.Value.GetArrayLength(); index++) {
                var original = term.Value[index]; var compact = compactAddresses[index];
                Assert.Equal(original.EnumerateArray().Select(value => value.GetInt32()), compact.EnumerateArray().Take(4).Select(value => value.GetInt32()));
                var entryId = original[0].GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture);
                var senseId = original[1].GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture);
                var entry = complete.GetProperty("entries").GetProperty(entryId);
                var compactEntry = context.GetProperty("entries").GetProperty(entryId);
                Assert.Equal(entry.GetProperty("headwords").GetRawText(), compactEntry[0].GetRawText());
                Assert.Equal(entry.GetProperty("partsOfSpeech").GetRawText(), compactEntry[1].GetRawText());
                var sense = entry.GetProperty("senses").GetProperty(senseId);
                Assert.Equal(sense.GetProperty("definitions").GetRawText(), compactEntry[2].GetProperty(senseId).GetRawText());
                Assert.Equal(sense.GetProperty("translations")[original[3].GetInt32()].GetString(), compact[4].GetString());
            }
        }
    }
    private static (string Word, string Translation, string[] Definitions)[] Recover(JsonElement evidence, string term) =>
        evidence.GetProperty("lookup").GetProperty(term).EnumerateArray().Select(address => {
            var entry = evidence.GetProperty("entries").GetProperty(address[0].GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture));
            var sense = entry.GetProperty("senses").GetProperty(address[1].GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture));
            return (entry.GetProperty("headwords")[address[2].GetInt32()].GetString()!,
                sense.GetProperty("translations")[address[3].GetInt32()].GetString()!,
                sense.GetProperty("definitions").EnumerateArray().Select(value => value.GetString()!).ToArray());
        }).ToArray();

    private static string Fixture() => """
        <TEI xmlns="http://www.tei-c.org/ns/1.0"><teiHeader><fileDesc>
          <editionStmt><edition>test-edition</edition></editionStmt>
          <publicationStmt><availability><p>CC-BY-SA-3.0 <ref target="https://creativecommons.org/licenses/by-sa/3.0/">license</ref></p></availability></publicationStmt>
          <sourceDesc><p>Synthetic source <ref target="https://example.test/corpus">corpus</ref></p></sourceDesc>
        </fileDesc></teiHeader><text><body xml:lang="en">
          <entry><form><orth>date</orth><orth>date alias</orth></form><gramGrp><pos>v</pos></gramGrp>
            <sense><cit type="trans" xml:lang="es"><quote>fechar</quote><quote>fechar</quote></cit>
              <sense><def>Add a date.</def></sense><sense><def>Determine age.</def></sense>
            </sense>
          </entry>
          <entry><form><orth>slate</orth></form><gramGrp><pos>v</pos></gramGrp>
            <sense><cit type="trans" xml:lang="es"><quote>fechar</quote></cit><sense><def>Schedule an event.</def></sense></sense>
          </entry>
          <entry><form><orth>review</orth></form><gramGrp><pos>n</pos></gramGrp>
            <sense><cit type="trans" xml:lang="es"><quote>Revisión</quote></cit><sense><def>A review.</def></sense></sense>
          </entry>
        </body></text></TEI>
        """;

    private sealed class CancelOnReadStream(CancellationTokenSource cancellation) : MemoryStream(Encoding.UTF8.GetBytes(Fixture()))
    {
        public override int Read(Span<byte> buffer) { var count = base.Read(buffer); cancellation.Cancel(); return count; }
    }

    private sealed class RepeatingStream(long remaining) : Stream
    {
        public override int Read(Span<byte> buffer) { var count = (int)Math.Min(buffer.Length, remaining); buffer[..count].Fill(32); remaining -= count; return count; }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
