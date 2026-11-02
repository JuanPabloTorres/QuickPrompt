using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringTranslationCompositionTests
{
    private const string Basis = "selected-preview-hash";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("saltar ⟦0⟧ y saltar ⟦0⟧", 0)]
    [InlineData("Registra el menú “Saltar”. Después saltar ⟦0⟧.", 18)]
    [InlineData("🧭 saltar y saltar.", 3)]
    public void SourceMaskPreservesWholeFieldsAndBindsEachUtf16Occurrence(string source, int firstStart)
    {
        var composition = Create(new() { ["task"] = source }); var slots = Slots(composition);
        Assert.Equal(2, slots.Length); Assert.NotEqual(slots[0].Name, slots[1].Name);
        Assert.Equal(firstStart, slots[0].Value.GetProperty("sourceRange").GetProperty("start").GetInt32());
        var restored = Masked(composition, "task");
        foreach (var slot in slots) {
            var start = slot.Value.GetProperty("sourceRange").GetProperty("start").GetInt32();
            var length = slot.Value.GetProperty("sourceRange").GetProperty("length").GetInt32();
            Assert.Equal(source.Substring(start, length), slot.Value.GetProperty("sourceWord").GetString());
            Assert.Equal("task", slot.Value.GetProperty("fieldId").GetString()); Assert.Equal("r0", slot.Value.GetProperty("resourceId").GetString());
            restored = restored.Replace(slot.Value.GetProperty("token").GetString()!, slot.Value.GetProperty("sourceWord").GetString(), StringComparison.Ordinal);
        }
        Assert.Equal(source, restored); Assert.DoesNotContain("sourceParts", composition.Material.GetRawText());
        Assert.Equal(JsonValueKind.Array, composition.Material.GetProperty("fields").ValueKind);
    }

    [Fact]
    public void AllSensesAndDuplicateEdgesRemainInCandidateCatalogAndCorrelatedSchema()
    {
        var composition = Create(new() { ["task"] = "saltar" }); var slot = Assert.Single(Slots(composition));
        var candidates = slot.Value.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.Equal(new[] { "jump", "skip", "saltar" }, candidates.Select(candidate => candidate.GetProperty("headword").GetString()));
        Assert.Equal(2, candidates[0].GetProperty("sourceEdges").GetArrayLength());
        Assert.Equal(new[] { 0, 1 }, candidates[0].GetProperty("sourceEdges").EnumerateArray().Select(edge => edge[3].GetInt32()));
        var branches = composition.Schema.GetProperty("properties").GetProperty("bindings").GetProperty("properties").GetProperty(slot.Name).GetProperty("anyOf");
        Assert.Equal(3, branches.GetArrayLength());
        for (var index = 0; index < candidates.Length; index++) {
            Assert.Equal(candidates[index].GetProperty("candidateId").GetString(), branches[index].GetProperty("properties").GetProperty("candidateId").GetProperty("const").GetString());
            Assert.Equal(candidates[index].GetProperty("headword").GetString(), branches[index].GetProperty("properties").GetProperty("word").GetProperty("const").GetString());
        }
    }

    [Fact]
    public void WholeFieldRewritesResolveOnlyDeclaredTokensAndPreserveFieldOrder()
    {
        var composition = Create(new() { ["task"] = "saltar ⟦0⟧; seguir ⟦1⟧.", ["constraint_1"] = "Condición." });
        var slots = Slots(composition); var wire = IdentityWire(composition);
        wire.Rewrites["task"] = "First, " + Token(slots[0]) + " ⟦0⟧; then " + Token(slots[1]) + " ⟦1⟧.";
        wire.Rewrites["constraint_1"] = "Condition.";
        wire.Rewrites = wire.Rewrites.Reverse().ToDictionary(field => field.Key, field => field.Value);
        using var response = Response(wire); var compiled = composition.Compile(response);
        Assert.Equal("First, jump ⟦0⟧; then continue ⟦1⟧.", compiled["task"]);
        Assert.Equal(new[] { "task", "constraint_1" }, compiled.Keys);
    }

    [Fact]
    public void AQuotedNameKeepsItsOriginalWhileAnotherOccurrenceChoosesAnEnglishSense()
    {
        var composition = Create(new() { ["task"] = "Registra “Saltar”. Después saltar ⟦0⟧." });
        var slots = Slots(composition); var wire = IdentityWire(composition);
        wire.Bindings[slots[0].Name] = new { candidateId = "KEEP_SOURCE", word = "Saltar", casing = "AS_SOURCE" };
        wire.Bindings[slots[1].Name] = Choice(slots[1].Value, "skip", "UPPERCASE");
        using var response = Response(wire); Assert.Equal("Registra “Saltar”. Después SKIP ⟦0⟧.", composition.Compile(response)["task"]);
    }

    [Theory]
    [InlineData("AS_ENTRY", "jump")]
    [InlineData("CAPITALIZED", "Jump")]
    [InlineData("UPPERCASE", "JUMP")]
    public void CasingIsAppliedFromTheCorpusWordRatherThanTheEcho(string casing, string expected)
    {
        var composition = Create(new() { ["task"] = "saltar" }); var wire = IdentityWire(composition);
        var slot = Assert.Single(Slots(composition)); wire.Bindings[slot.Name] = Choice(slot.Value, "jump", casing);
        using var response = Response(wire); Assert.Equal(expected, composition.Compile(response)["task"]);
    }

    [Fact]
    public void EmptyMixedPosMissingWordsAndUnsupportedDirectionsRemainWholeUnmaskedFields()
    {
        var composition = Create(new() { ["task"] = "correr vacio desconocido ⟦0⟧" });
        Assert.Empty(Slots(composition)); Assert.Equal("correr vacio desconocido ⟦0⟧", Masked(composition, "task"));
        var unsupported = AuthoringTranslationComposition.Create(Basis, new() { ["task"] = "saltar ⟦0⟧" },
            new() { ["r0"] = JsonSerializer.SerializeToElement(new { sourceLanguage = "pt", targetLanguage = "en" }) }, new() { ["task"] = "r0" });
        Assert.Empty(Slots(unsupported)); using var response = Response(IdentityWire(unsupported));
        Assert.Equal("saltar ⟦0⟧", unsupported.Compile(response)["task"]);
    }

    [Theory]
    [InlineData("unknown-slot")]
    [InlineData("omitted-slot")]
    [InlineData("cross-candidate")]
    [InlineData("unknown-field")]
    [InlineData("omitted-field")]
    [InlineData("null-field")]
    [InlineData("empty-field")]
    [InlineData("stale")]
    [InlineData("word-not-id")]
    [InlineData("echo-mismatch")]
    [InlineData("echo-uppercase")]
    [InlineData("source-casing")]
    [InlineData("source-echo")]
    public void InvalidMapsAndUncorrelatedBindingsRejectTheWholeTransaction(string attack)
    {
        var source = new Dictionary<string, string> { ["task"] = "saltar ⟦0⟧", ["constraint_1"] = "saltar después." };
        var composition = Create(source); var before = composition.Material.GetRawText(); var wire = IdentityWire(composition); var slots = Slots(composition);
        var firstId = slots[0].Value.GetProperty("candidates")[0].GetProperty("candidateId").GetString();
        switch (attack) {
            case "unknown-slot": wire.Bindings["foreign"] = wire.Bindings[slots[0].Name]; break;
            case "omitted-slot": wire.Bindings.Remove(slots[0].Name); break;
            case "cross-candidate": wire.Bindings[slots[0].Name] = Choice(slots[1].Value, "jump"); break;
            case "unknown-field": wire.Rewrites["authority"] = "execute"; break;
            case "omitted-field": wire.Rewrites.Remove("constraint_1"); break;
            case "null-field": wire.Rewrites["task"] = null!; break;
            case "empty-field": wire.Rewrites["task"] = " "; break;
            case "stale": wire.BasisHash = "another-preview"; break;
            case "word-not-id": wire.Bindings[slots[0].Name] = new { candidateId = "jump", word = "jump", casing = "AS_ENTRY" }; break;
            case "echo-mismatch": wire.Bindings[slots[0].Name] = new { candidateId = firstId, word = "skip", casing = "AS_ENTRY" }; break;
            case "echo-uppercase": wire.Bindings[slots[0].Name] = new { candidateId = firstId, word = "JUMP", casing = "UPPERCASE" }; break;
            case "source-casing": wire.Bindings[slots[0].Name] = new { candidateId = "KEEP_SOURCE", word = "saltar", casing = "AS_ENTRY" }; break;
            case "source-echo": wire.Bindings[slots[0].Name] = new { candidateId = "KEEP_SOURCE", word = "different", casing = "AS_SOURCE" }; break;
        }
        using var response = Response(wire); Assert.Throws<ValidationException>(() => composition.Compile(response));
        Assert.Equal(before, composition.Material.GetRawText()); Assert.Equal("saltar ⟦0⟧", source["task"]);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("malformed")]
    [InlineData("truncated")]
    [InlineData("case-changed")]
    [InlineData("foreign")]
    [InlineData("order")]
    public void LexicalMarkersMustBeCanonicalOwnedUniqueAndInSourceOrder(string attack)
    {
        var composition = Create(new() { ["task"] = "saltar ⟦0⟧; seguir ⟦1⟧.", ["constraint_1"] = "saltar después." });
        var slots = Slots(composition); var wire = IdentityWire(composition); var first = Token(slots[0]);
        wire.Rewrites["task"] = attack switch {
            "missing" => wire.Rewrites["task"].Replace(first, "jump", StringComparison.Ordinal),
            "duplicate" => wire.Rewrites["task"] + first,
            "unknown" => wire.Rewrites["task"].Replace(first, "⟪lex:s999_0_1⟫", StringComparison.Ordinal),
            "malformed" => wire.Rewrites["task"].Replace(first, "⟪lex:bad⟫", StringComparison.Ordinal),
            "truncated" => wire.Rewrites["task"].Replace(first, first[..^1], StringComparison.Ordinal),
            "case-changed" => wire.Rewrites["task"].Replace(first, first.ToUpperInvariant(), StringComparison.Ordinal),
            "foreign" => wire.Rewrites["task"].Replace(first, Token(slots[2]), StringComparison.Ordinal),
            _ => wire.Rewrites["task"].Replace(first, "temporary", StringComparison.Ordinal).Replace(Token(slots[1]), first, StringComparison.Ordinal).Replace("temporary", Token(slots[1]), StringComparison.Ordinal)
        };
        using var response = Response(wire); Assert.Throws<ValidationException>(() => composition.Compile(response));
    }

    [Theory]
    [InlineData("swap")]
    [InlineData("reuse")]
    [InlineData("missing")]
    [InlineData("move-across-word")]
    public void ExistingInputsCannotSwapReuseDisappearOrMoveAcrossWordMarkers(string attack)
    {
        var composition = Create(new() { ["task"] = "saltar ⟦0⟧; seguir ⟦1⟧." }); var wire = IdentityWire(composition);
        var slots = Slots(composition);
        wire.Rewrites["task"] = attack switch {
            "swap" => Token(slots[0]) + " ⟦1⟧; " + Token(slots[1]) + " ⟦0⟧.",
            "reuse" => wire.Rewrites["task"].Replace("⟦1⟧", "⟦0⟧", StringComparison.Ordinal),
            "missing" => wire.Rewrites["task"].Replace("⟦0⟧", "", StringComparison.Ordinal),
            _ => "⟦0⟧ " + Token(slots[0]) + "; " + Token(slots[1]) + " ⟦1⟧."
        };
        using var response = Response(wire); Assert.Throws<ValidationException>(() => composition.Compile(response));
    }

    [Theory]
    [InlineData("Text ⟦0⟧.", false)]
    [InlineData("Text ⟦0⟧ ⟦0⟧ ⟦0⟧.", false)]
    [InlineData("Text ⟦0⟧ ⟦1⟧.", false)]
    [InlineData("Text ⟦0⟧ ⟦0⟧ ⟦2⟧.", true)]
    public void UnmaskedFieldsRetainExactExistingCountsWithoutBorrowingOtherFields(string text, bool valid)
    {
        var composition = Create(new() { ["task"] = "Texto ⟦0⟧ ⟦0⟧.", ["constraint_1"] = "Condición ⟦1⟧." });
        var wire = IdentityWire(composition); wire.Rewrites["task"] = text;
        using var response = Response(wire);
        if (valid) Assert.Equal(text, composition.Compile(response)["task"]); else Assert.Throws<ValidationException>(() => composition.Compile(response));
    }

    [Fact]
    public void UnmaskedFieldsPermitInputReorderingButNotMalformedInputSyntax()
    {
        var composition = AuthoringTranslationComposition.Create(Basis, new() { ["task"] = "Texto ⟦0⟧ y ⟦1⟧." }, [], []);
        var wire = IdentityWire(composition); wire.Rewrites["task"] = "Text ⟦1⟧ and ⟦0⟧.";
        using (var response = Response(wire)) Assert.Equal("Text ⟦1⟧ and ⟦0⟧.", composition.Compile(response)["task"]);
        foreach (var invalid in new[] { "Text ⟦01⟧ ⟦1⟧.", "Text ⟦word⟧.", "Text <nuevo>." }) {
            wire.Rewrites["task"] = invalid; using var response = Response(wire); Assert.Throws<ValidationException>(() => composition.Compile(response));
        }
    }

    [Fact]
    public void GloballyNewCanonicalInputsRemainSubjectToTheExistingDomainSelection()
    {
        var composition = Create(new() { ["task"] = "saltar ⟦0⟧; seguir ⟦1⟧." }); var wire = IdentityWire(composition);
        wire.Rewrites["task"] += " y ⟦2⟧."; using var response = Response(wire); var encoded = composition.Compile(response)["task"];
        var content = new AuthoringBriefContent("Inputs", "", "saltar <principal>; seguir <anexo>.", [], [], [], []);
        var original = AuthoringEdits.Create(content);
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(original, new(original.BasisHash, AuthoringEdits.Rewrite, null, [new("task", DecodeForDomain(encoded))])));
        var selected = AuthoringEdits.Create(content, ["principal", "anexo", "nuevo"]);
        Assert.Contains("<nuevo>", AuthoringEdits.Apply(selected, new(selected.BasisHash, AuthoringEdits.Rewrite, null, [new("task", DecodeForDomain(encoded))])).Task);
    }

    [Fact]
    public void MarkerPresenceAndOrderDoNotCertifyThePredicatesSemanticRole()
    {
        var composition = Create(new() { ["task"] = "saltar ⟦0⟧." }); var wire = IdentityWire(composition);
        wire.Rewrites["task"] = "A " + Token(Assert.Single(Slots(composition))) + " about ⟦0⟧ is discussed before finalizing everything.";
        using var response = Response(wire);
        Assert.Equal("A jump about ⟦0⟧ is discussed before finalizing everything.", composition.Compile(response)["task"]);
        // This wire is structurally valid and semantically wrong. Independent
        // evaluation, not this compiler, must reject it as a translation.
    }

    [Theory]
    [InlineData("saltar ⟪lex:existing⟫")]
    [InlineData("⟪lex:unterminated")]
    public void SourceNamespaceCollisionsFailBeforeAnyMaskIsPublished(string source) =>
        Assert.Throws<ValidationException>(() => Create(new() { ["task"] = source }));

    [Fact]
    public void UnrelatedMathematicalDelimitersRemainLiteral()
    {
        var composition = Create(new() { ["task"] = "Explica ⟪x⟫; saltar ⟦0⟧." });
        Assert.Contains("⟪x⟫", Masked(composition, "task"));
        using var response = Response(IdentityWire(composition)); Assert.Equal("Explica ⟪x⟫; jump ⟦0⟧.", composition.Compile(response)["task"]);
    }

    [Theory]
    [InlineData("jump ⟦2⟧")]
    [InlineData("jump ⟪lex:s0_0_6⟫")]
    public void LexicalDataCannotIntroduceInputsOrWordMarkers(string headword)
    {
        var node = JsonNode.Parse(Resource().GetRawText())!; node["entries"]!["1"]![0]![0] = headword;
        var composition = AuthoringTranslationComposition.Create(Basis, new() { ["task"] = "saltar ⟦0⟧." },
            new() { ["r0"] = JsonSerializer.SerializeToElement(node) }, new() { ["task"] = "r0" });
        using var response = Response(IdentityWire(composition)); Assert.Throws<ValidationException>(() => composition.Compile(response));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateRootAndBindingPropertiesAreRejected(bool nested)
    {
        var composition = Create(new() { ["task"] = "saltar" }); var raw = JsonSerializer.Serialize(IdentityWire(composition), Web);
        raw = nested ? raw.Replace("\"word\":\"jump\"", "\"word\":\"jump\",\"word\":\"jump\"", StringComparison.Ordinal)
            : raw[..^1] + ",\"basisHash\":\"" + Basis + "\"}";
        using var response = JsonSerializer.SerializeToDocument(new { message = new { content = raw } });
        Assert.Throws<ValidationException>(() => composition.Compile(response));
    }

    [Fact]
    public void CorruptResourceAddressesAndCancellationRemainFailClosed()
    {
        var node = JsonNode.Parse(Resource().GetRawText())!; node["lookup"]!["saltar"]![0]![0] = 999999;
        Assert.Throws<ValidationException>(() => AuthoringTranslationComposition.Create(Basis, new() { ["task"] = "saltar" },
            new() { ["r0"] = JsonSerializer.SerializeToElement(node) }, new() { ["task"] = "r0" }));
        using var stop = new CancellationTokenSource(); stop.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => AuthoringTranslationComposition.Create(Basis, new() { ["task"] = "saltar" }, [], [], stop.Token));
        var composition = Create(new() { ["task"] = "saltar" }); using var response = Response(IdentityWire(composition));
        Assert.ThrowsAny<OperationCanceledException>(() => composition.Compile(response, stop.Token));
    }

    [Fact]
    public void RetiredGapWireIsRejectedAndTheSourceSnapshotCannotBeMutatedByTheCaller()
    {
        var source = new Dictionary<string, string> { ["task"] = "saltar ⟦0⟧." }; var composition = Create(source); source["task"] = "changed";
        using (var valid = Response(IdentityWire(composition))) Assert.Equal("jump ⟦0⟧.", composition.Compile(valid)["task"]);
        using var legacy = JsonSerializer.SerializeToDocument(new { message = new { content = JsonSerializer.Serialize(new { basisHash = Basis, bindings = new { }, gaps = new { g0_0 = "jump" } }) } });
        Assert.Throws<ValidationException>(() => composition.Compile(legacy));
    }

    private static AuthoringTranslationComposition Create(Dictionary<string, string> fields) =>
        AuthoringTranslationComposition.Create(Basis, fields, new() { ["r0"] = Resource() }, fields.Keys.ToDictionary(key => key, _ => "r0"));
    private static JsonProperty[] Slots(AuthoringTranslationComposition composition) => composition.Material.GetProperty("slots").EnumerateObject().ToArray();
    private static string Token(JsonProperty slot) => slot.Value.GetProperty("token").GetString()!;
    private static string Masked(AuthoringTranslationComposition composition, string id) => composition.Material.GetProperty("fields").EnumerateArray().Single(field => field.GetProperty("id").GetString() == id).GetProperty("text").GetString()!;
    private static object Choice(JsonElement slot, string word, string casing = "AS_ENTRY") {
        var candidate = slot.GetProperty("candidates").EnumerateArray().Single(candidate => candidate.GetProperty("headword").GetString() == word && candidate.GetProperty("kind").GetString() == "LEXICAL_EDGE");
        return new { candidateId = candidate.GetProperty("candidateId").GetString(), word, casing };
    }
    private static Wire IdentityWire(AuthoringTranslationComposition composition) => new() {
        BasisHash = Basis, Bindings = Slots(composition).ToDictionary(slot => slot.Name, slot => (object)new {
            candidateId = slot.Value.GetProperty("candidates")[0].GetProperty("candidateId").GetString(),
            word = slot.Value.GetProperty("candidates")[0].GetProperty("headword").GetString(), casing = "AS_ENTRY" }),
        Rewrites = composition.Material.GetProperty("fields").EnumerateArray().ToDictionary(field => field.GetProperty("id").GetString()!, field => field.GetProperty("text").GetString()!)
    };
    private static JsonDocument Response(Wire wire) => JsonSerializer.SerializeToDocument(new { message = new { content = JsonSerializer.Serialize(wire, Web) } });
    private sealed class Wire { public string BasisHash { get; set; } = ""; public Dictionary<string, object> Bindings { get; set; } = []; public Dictionary<string, string> Rewrites { get; set; } = []; }
    private static AuthoringTextSegment[] DecodeForDomain(string text) {
        var result = new List<AuthoringTextSegment>(); var cursor = 0;
        foreach (Match token in Regex.Matches(text, @"⟦([0-9]+)⟧")) {
            if (cursor < token.Index) result.Add(new(text[cursor..token.Index], null));
            result.Add(new(null, int.Parse(token.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))); cursor = token.Index + token.Length;
        }
        if (cursor < text.Length) result.Add(new(text[cursor..], null)); return result.ToArray();
    }
    private static JsonElement Resource()
    {
        const string xml = """
            <TEI xmlns="http://www.tei-c.org/ns/1.0"><teiHeader><fileDesc><editionStmt><edition>synthetic</edition></editionStmt>
            <publicationStmt><availability><p>CC-BY-SA-3.0 <ref target="https://creativecommons.org/licenses/by-sa/3.0/">license</ref></p></availability></publicationStmt>
            <sourceDesc><p>Synthetic source.</p></sourceDesc></fileDesc></teiHeader><text><body xml:lang="en">
            <entry><form><orth>jump</orth></form><gramGrp><pos>v</pos></gramGrp><sense><cit type="trans" xml:lang="es"><quote>saltar</quote><quote>saltar</quote></cit><sense><def>Move off ground.</def></sense></sense></entry>
            <entry><form><orth>skip</orth></form><gramGrp><pos>v</pos></gramGrp><sense><cit type="trans" xml:lang="es"><quote>saltar</quote></cit><sense><def>Leave out.</def></sense></sense></entry>
            <entry><form><orth>continue</orth></form><gramGrp><pos>v</pos></gramGrp><sense><cit type="trans" xml:lang="es"><quote>seguir</quote></cit><sense><def>Carry on.</def></sense></sense></entry>
            <entry><form><orth>run</orth></form><gramGrp><pos>v</pos></gramGrp><sense><cit type="trans" xml:lang="es"><quote>correr</quote></cit></sense></entry>
            <entry><form><orth>course</orth></form><gramGrp><pos>n</pos></gramGrp><sense><cit type="trans" xml:lang="es"><quote>correr</quote></cit></sense></entry>
            <entry><form><orth>unknown</orth></form><gramGrp/><sense><cit type="trans" xml:lang="es"><quote>vacio</quote></cit></sense></entry>
            </body></text></TEI>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return LocalTranslationLexicon.FromTei(stream, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xml))))
            .ReadContext("es", "en", ["saltar", "seguir", "correr", "vacio"]);
    }
}
