using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

/// <summary>Deterministic source/graph compiler checks with supplied facts; not Qwen extraction acceptance.</summary>
public sealed class AuthoringCompressionLedgerTests
{
    private static AuthoringBriefContent Content(string task = "Resume <texto>. Resume ese texto. Resume ese texto.") =>
        new("Documento", "Metadatos intactos", task, [], [], [], []);

    private static CompressionFact Fact(string id, string predicate = "summarize", string[]? arguments = null,
        string polarity = "AFFIRMATIVE", string modality = "REQUIRED", string scope = "global", string[]? dependencies = null) =>
        new(id, predicate, arguments ?? ["texto"], polarity, modality, new(scope, []), dependencies ?? []);

    private static CompressionLedger Repeated(AuthoringRevisionBasis basis) => new(basis.BasisHash,
        basis.Fields.SelectMany(field => field.Units).Select((_, i) => Fact("f" + i)).ToArray(),
        basis.Fields.SelectMany(field => field.Units).Select((unit, i) => (unit.Id, Fact: "f" + i))
            .ToDictionary(item => item.Id, item => new[] { item.Fact }));

    [Fact]
    public void RemovesEveryCoveredLaterUnitWithoutAWinningPreviewChoice()
    {
        var source = Content(); var basis = AuthoringEdits.Create(source);
        Assert.Equal(3, basis.Fields[0].Units.Length);
        var result = AuthoringCompressionLedger.Compile(basis, Repeated(basis));
        Assert.Equal(basis.Fields[0].Units[0].Text, result.Task);
        Assert.Equal(source.Title, result.Title); Assert.Equal(source.Description, result.Description);
        Assert.Equal("Resume <texto>. Resume ese texto. Resume ese texto.", source.Task);
    }

    [Fact]
    public void CompactsAllFieldsButNeverUsesAnotherFieldAsItsWitness()
    {
        var source = Content() with { Constraints = ["No inventes datos. Esta regla prohíbe inventar datos. La regla consiste en no inventar datos."] };
        var basis = AuthoringEdits.Create(source);
        var result = AuthoringCompressionLedger.Compile(basis, Repeated(basis));
        Assert.Equal(basis.Fields[0].Units[0].Text, result.Task);
        Assert.Equal(new[] { basis.Fields[1].Units[0].Text }, result.Constraints);
    }

    [Fact]
    public void KeepsAContinuationThatAddsAnObligation()
    {
        var source = Content("Pregunta por la fecha antes de fechar <resumen>. Continúa con los otros temas de <resumen>.");
        var basis = AuthoringEdits.Create(source); var ledger = Repeated(basis);
        ledger.Facts[1] = Fact("f1", "continue", ["other topics"], scope: "outside-date-dependency");
        Assert.Equal(source.Task, AuthoringCompressionLedger.Compile(basis, ledger).Task);
    }

    [Theory]
    [InlineData("predicate")]
    [InlineData("argument")]
    [InlineData("argument-case")]
    [InlineData("polarity")]
    [InlineData("modality")]
    [InlineData("scope")]
    [InlineData("scope-argument")]
    public void AChangedMeaningBearingComponentCannotBecomeADuplicate(string difference)
    {
        var source = Content("Resume <texto>. Describe ese texto."); var basis = AuthoringEdits.Create(source);
        var ledger = Repeated(basis); var fact = ledger.Facts[1];
        ledger.Facts[1] = difference switch {
            "predicate" => fact with { Predicate = "describe" },
            "argument" => fact with { Arguments = ["otro texto"] },
            "argument-case" => fact with { Arguments = ["Texto"] },
            "polarity" => fact with { Polarity = "NEGATIVE" },
            "modality" => fact with { Modality = "PERMITTED" },
            "scope" => fact with { Scope = new("before-recommendation", []) },
            _ => fact with { Scope = new("global", ["only-date"]) }
        };
        Assert.Equal(source.Task, AuthoringCompressionLedger.Compile(basis, ledger).Task);
    }

    [Fact]
    public void EquivalentDependencyGraphsMergeDespiteDifferentFactIdsAndOrder()
    {
        var basis = AuthoringEdits.Create(Content("Resume <texto>. Resume ese texto."));
        var units = basis.Fields[0].Units;
        var ledger = new CompressionLedger(basis.BasisHash,
            [Fact("f0", "ask", ["date"]), Fact("f1", "ASK", ["date"]),
             Fact("f2", dependencies: ["f0"]), Fact("f3", " SUMMARIZE ", dependencies: ["f1"])],
            new() { [units[0].Id] = ["f2"], [units[1].Id] = ["f3"] });
        Assert.Equal(units[0].Text, AuthoringCompressionLedger.Compile(basis, ledger).Task);
    }

    [Fact]
    public void ANewDependencyIsNotAnEquivalentObligation()
    {
        var source = Content("Resume <texto>. Resume ese texto."); var basis = AuthoringEdits.Create(source);
        var ledger = Repeated(basis);
        ledger = ledger with { Facts = [ledger.Facts[0], ledger.Facts[1] with { Dependencies = ["f2"] }, Fact("f2", "ask", ["date"])] };
        Assert.Equal(source.Task, AuthoringCompressionLedger.Compile(basis, ledger).Task);
    }

    [Theory]
    [InlineData("missing-unit")]
    [InlineData("unknown-unit")]
    [InlineData("empty-coverage")]
    [InlineData("unknown-fact")]
    [InlineData("duplicate-fact-id")]
    [InlineData("unreachable-fact")]
    [InlineData("unknown-dependency")]
    [InlineData("self-cycle")]
    [InlineData("dependency-cycle")]
    [InlineData("invalid-polarity")]
    public void RejectsInvalidLedgersAtomically(string failure)
    {
        var source = Content(); var basis = AuthoringEdits.Create(source); var ledger = Repeated(basis);
        var first = basis.Fields[0].Units[0].Id;
        switch (failure) {
            case "missing-unit": ledger.Units.Remove(first); break;
            case "unknown-unit": ledger.Units.Add("foreign:1", ["f0"]); break;
            case "empty-coverage": ledger.Units[first] = []; break;
            case "unknown-fact": ledger.Units[first] = ["f99"]; break;
            case "duplicate-fact-id": ledger.Facts[1] = ledger.Facts[0]; break;
            case "unreachable-fact": ledger = ledger with { Facts = [.. ledger.Facts, Fact("f99")] }; break;
            case "unknown-dependency": ledger.Facts[0] = ledger.Facts[0] with { Dependencies = ["f99"] }; break;
            case "self-cycle": ledger.Facts[0] = ledger.Facts[0] with { Dependencies = ["f0"] }; break;
            case "dependency-cycle":
                ledger.Facts[0] = ledger.Facts[0] with { Dependencies = ["f1"] };
                ledger.Facts[1] = ledger.Facts[1] with { Dependencies = ["f0"] }; break;
            default: ledger.Facts[0] = ledger.Facts[0] with { Polarity = "IGNORE" }; break;
        }
        Assert.Throws<ValidationException>(() => AuthoringCompressionLedger.Compile(basis, ledger));
        Assert.Equal("Resume <texto>. Resume ese texto. Resume ese texto.", source.Task);
    }

    [Fact]
    public void ExistingQuantityGuardRejectsAnInaccurateLedgerInsteadOfRemovingUniqueNumbers()
    {
        var basis = AuthoringEdits.Create(Content("Resume <texto>. Entrega 7 secciones."));
        Assert.Throws<ValidationException>(() => AuthoringCompressionLedger.Compile(basis, Repeated(basis)));
    }

    [Fact]
    public void AVariableInAnotherFieldDoesNotPermitLosingItHere()
    {
        var basis = AuthoringEdits.Create(Content("Resume el texto. Revisa <texto>.") with { Steps = ["Lee <texto>."] });
        Assert.Throws<ValidationException>(() => AuthoringCompressionLedger.Compile(basis, Repeated(basis)));
    }

    [Fact]
    public void StaleBaseAndCancellationNeverProduceAProposal()
    {
        var basis = AuthoringEdits.Create(Content()); var ledger = Repeated(basis);
        Assert.Throws<ConflictException>(() => AuthoringCompressionLedger.Compile(basis, ledger with { BasisHash = "stale" }));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => AuthoringCompressionLedger.Compile(basis, ledger, cancelled.Token));
    }

    [Fact]
    public void MaterialGivesHostOwnedUnitsWithoutModelOwnedOffsetsAndSchemaRequiresCompleteCoverage()
    {
        var basis = AuthoringEdits.Create(Content());
        var material = JsonSerializer.SerializeToElement(AuthoringCompressionLedger.Material(basis), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(basis.BasisHash, material.GetProperty("basisHash").GetString());
        var unit = material.GetProperty("fields")[0].GetProperty("units")[0];
        Assert.Equal(basis.Fields[0].Units[0].Text, unit.GetProperty("text").GetString());
        Assert.False(unit.TryGetProperty("start", out _));
        var schema = AuthoringCompressionLedger.Schema(basis);
        Assert.Equal(3, schema.GetProperty("properties").GetProperty("units").GetProperty("required").GetArrayLength());
    }

    [Fact]
    public void OversizedMaterialAndLedgerAreRejectedWithoutTruncation()
    {
        var large = AuthoringEdits.Create(Content(new string('x', 7000)) with {
            Constraints = [new string('y', 7000), new string('z', 7000), new string('w', 7000)] });
        Assert.Throws<ValidationException>(() => AuthoringCompressionLedger.Material(large));
        var basis = AuthoringEdits.Create(Content()); var ledger = Repeated(basis);
        ledger.Facts[0] = ledger.Facts[0] with { Arguments = [new string('x', 15000), new string('y', 15000)] };
        Assert.Throws<ValidationException>(() => AuthoringCompressionLedger.Compile(basis, ledger));
    }

    [Fact]
    public void PreparedMaterialDoesNotFollowLaterArrayMutationsInTheCallersBasis()
    {
        var basis = AuthoringEdits.Create(Content());
        var material = AuthoringCompressionLedger.Material(basis);
        basis.InputNames[0] = "foreign";
        basis.Fields[0].Units[0] = basis.Fields[0].Units[0] with { Text = "Texto cambiado" };
        var captured = JsonSerializer.SerializeToElement(material, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("texto", captured.GetProperty("inputs")[0].GetString());
        Assert.Equal("Resume <texto>. ", captured.GetProperty("fields")[0].GetProperty("units")[0].GetProperty("text").GetString());
    }

    [Fact]
    public void NativeSchemaClosesEveryFactReferenceWithTheSameFiniteNamespace()
    {
        var basis = AuthoringEdits.Create(Content()); var schema = AuthoringCompressionLedger.Schema(basis, 4);
        var properties = schema.GetProperty("properties");
        var fact = properties.GetProperty("facts").GetProperty("items").GetProperty("properties");
        var expected = new[] { "f0", "f1", "f2", "f3" };
        Assert.Equal(expected, fact.GetProperty("id").GetProperty("enum").EnumerateArray().Select(id => id.GetString()));
        Assert.Equal(expected, fact.GetProperty("dependencies").GetProperty("items").GetProperty("enum").EnumerateArray().Select(id => id.GetString()));
        foreach (var unit in properties.GetProperty("units").GetProperty("properties").EnumerateObject())
            Assert.Equal(expected, unit.Value.GetProperty("items").GetProperty("enum").EnumerateArray().Select(id => id.GetString()));
        Assert.Equal(4, properties.GetProperty("facts").GetProperty("maxItems").GetInt32());
    }

    [Theory]
    [InlineData("task:1")]
    [InlineData("f128")]
    [InlineData("f00")]
    public void HostAlsoRejectsIdsOutsideTheNativeFactNamespace(string id)
    {
        var basis = AuthoringEdits.Create(Content()); var ledger = Repeated(basis);
        ledger.Units[basis.Fields[0].Units[0].Id] = [id];
        ledger.Facts[0] = ledger.Facts[0] with { Id = id };
        Assert.Throws<ValidationException>(() => AuthoringCompressionLedger.Compile(basis, ledger));
    }
}
