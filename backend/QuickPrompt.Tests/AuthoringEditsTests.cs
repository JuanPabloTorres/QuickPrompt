using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringEditsTests
{
    [Fact]
    public void AHeldHumanSelectionMayAddASlotMissingFromTheEstablishedBody()
    {
        var basis = AuthoringEdits.Create(Content(), ["inventario", "registro", "audiencia"]);
        var revised = AuthoringEdits.Apply(basis, Rewrite(basis, new AuthoringFieldRewrite("task",
            [new("Compara ", null), new(null, 0), new(" con ", null), new(null, 1), new(" para ", null), new(null, 2), new(".", null)])));
        Assert.Equal("Compara <inventario> con <registro> para <audiencia>.", revised.Task);
        Assert.Equal(Content().Constraints, revised.Constraints); Assert.Equal(Content().References, revised.References);
    }

    [Fact]
    public void UnknownSlotsAndASelectionThatExcludesAnExistingSlotFailClosed()
    {
        Assert.Throws<ValidationException>(() => AuthoringEdits.Create(Content(), ["inventario", "audiencia"]));
        var basis = AuthoringEdits.Create(Content(), ["inventario", "registro", "audiencia"]);
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Rewrite(basis,
            new AuthoringFieldRewrite("task", [new(null, 0), new(null, 1), new(null, 3)]))));
    }

    [Fact]
    public void AddingAnAllowedSlotCannotCompensateForLosingAnExistingSlot()
    {
        var basis = AuthoringEdits.Create(Content(), ["inventario", "registro", "audiencia"]);
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Rewrite(basis,
            new AuthoringFieldRewrite("task", [new("Compara ", null), new(null, 0), new(" para ", null), new(null, 2), new(".", null)]))));
    }

    [Fact]
    public void TheHumanSelectionIsCopiedAndBoundToTheRevisionHash()
    {
        string[] selected = ["inventario", "registro", "audiencia"];
        var basis = AuthoringEdits.Create(Content(), selected);
        var proposal = new AuthoringRevisionProposal(basis.BasisHash, AuthoringEdits.Keep, null, null);
        selected[2] = "cambio_externo";
        Assert.Equal("audiencia", basis.InputNames[2]);
        Assert.NotEqual(AuthoringEdits.Create(Content()).BasisHash, basis.BasisHash);
        basis.InputNames[2] = "cambio_interno";
        Assert.Throws<ConflictException>(() => AuthoringEdits.Apply(basis, proposal));
    }

    private static AuthoringBriefContent Content() => new("Plantilla", "Descripción conservada",
        "Compara <inventario> con <registro>. Esta tarea consiste en comparar esas mismas entradas.",
        ["Describe coincidencias. Describe coincidencias."],
        ["Si falta una unidad, pregunta antes de comparar esas cantidades; continúa con los datos comparables.", "No asignes cero a un dato ausente."],
        ["Entrega únicamente las diferencias y los datos pendientes."],
        [new("https://example.invalid/guide", AuthoringReferenceRole.FUTURE_TASK), new("https://example.invalid/writing", AuthoringReferenceRole.CURRENT_SESSION)]);

    private static AuthoringRevisionProposal Compress(AuthoringRevisionBasis basis, params AuthoringUnitRemoval[] removals) =>
        new(basis.BasisHash, AuthoringEdits.Compress, removals, null);
    private static AuthoringRevisionProposal Rewrite(AuthoringRevisionBasis basis, params AuthoringFieldRewrite[] changes) =>
        new(basis.BasisHash, AuthoringEdits.Rewrite, null, changes);

    [Fact]
    public void KeepPreservesASeparateSnapshotWithoutInventingAChangeOrAnApproval()
    {
        var content = Content(); var basis = AuthoringEdits.Create(content);
        var unchanged = AuthoringEdits.Apply(basis, new(basis.BasisHash, AuthoringEdits.Keep, null, null));
        Assert.Equal(LiveRuns.Hash(content), LiveRuns.Hash(unchanged));
        Assert.NotSame(basis.Content, unchanged); Assert.NotSame(basis.Content.Constraints, unchanged.Constraints);
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, new(basis.BasisHash, AuthoringEdits.Keep, [], null)));
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Compress(basis)));
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Rewrite(basis)));
    }

    [Fact]
    public void CompressionUsesExactNonOverlappingSlicesAndPreservesEveryOtherFieldAndRole()
    {
        var content = Content(); var basis = AuthoringEdits.Create(content);
        foreach (var field in basis.Fields)
        {
            Assert.Equal(0, field.Units[0].Start);
            for (var index = 1; index < field.Units.Length; index++)
                Assert.Equal(field.Units[index - 1].Start + field.Units[index - 1].Length, field.Units[index].Start);
        }
        var task = basis.Fields.Single(field => field.Id == "task");
        Assert.Equal(content.Task, string.Concat(task.Units.Select(unit => unit.Text)));
        Assert.Equal(new[] { "inventario", "registro" }, basis.InputNames);
        var revised = AuthoringEdits.Apply(basis, Compress(basis, new AuthoringUnitRemoval("task:2", ["task:1"]), new AuthoringUnitRemoval("step_1:2", ["step_1:1"])));
        Assert.Equal(task.Units[0].Text, revised.Task);
        Assert.Equal("Describe coincidencias. ", revised.Steps[0]);
        Assert.Equal(content.Title, revised.Title); Assert.Equal(content.Description, revised.Description);
        Assert.Equal(content.Constraints, revised.Constraints); Assert.Equal(content.OutputRequirements, revised.OutputRequirements);
        Assert.Equal(content.References, revised.References);
        Assert.Equal(content.Task, basis.Content.Task); Assert.Equal(content.Steps[0], basis.Content.Steps[0]);
    }

    [Theory]
    [InlineData("task:90", "task:1")]
    [InlineData("task:2", "output_1:1")]
    [InlineData("task:2", "task:2")]
    [InlineData("task:2", "task:90")]
    public void CompressionRejectsUnknownCrossFieldAndRemovedWitnesses(string remove, string witness)
    {
        var basis = AuthoringEdits.Create(Content());
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Compress(basis, new AuthoringUnitRemoval(remove, [witness]))));
    }

    [Fact]
    public void CompressionRejectsDuplicatesMissingWitnessesAndDeletionOfEveryUnit()
    {
        var basis = AuthoringEdits.Create(Content());
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Compress(basis, new AuthoringUnitRemoval("task:2", ["task:1"]), new AuthoringUnitRemoval("task:2", ["task:1"]))));
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Compress(basis, new AuthoringUnitRemoval("task:2", []))));
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Compress(basis, new AuthoringUnitRemoval("task:2", ["task:1", "task:1"]))));
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Compress(basis,
            new AuthoringUnitRemoval("task:1", ["task:2"]), new AuthoringUnitRemoval("task:2", ["task:1"]))));
    }

    [Fact]
    public void CompressionCannotLoseAVariableEvenWhenAWitnessExists()
    {
        var basis = AuthoringEdits.Create(Content());
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Compress(basis, new AuthoringUnitRemoval("task:1", ["task:2"]))));
    }

    [Theory]
    [InlineData("Lee https://example.invalid/guide. Resume el texto.")]
    [InlineData("Usa `a.b`. Resume el texto.")]
    [InlineData("Consulta al Dr. Pérez. Resume el texto.")]
    [InlineData("Consulta al Prof. Pérez. Resume el texto.")]
    [InlineData("Usa 3.14 como valor. Resume el texto.")]
    [InlineData("Conserva «Sólo esta cita. No otra». Resume el texto.")]
    [InlineData("Lee datos... Resume el texto.")]
    public void AmbiguousPunctuationStaysOneIndivisibleUnit(string task)
    {
        var basis = AuthoringEdits.Create(Content() with { Task = task });
        var unit = Assert.Single(basis.Fields.Single(field => field.Id == "task").Units);
        Assert.Equal(task, unit.Text); Assert.Equal(0, unit.Start); Assert.Equal(task.Length, unit.Length);
    }

    [Fact]
    public void SegmentationNeverCutsInsideALegacyMarkerAndKeepsUnicodeSlicesExact()
    {
        var content = Content() with { Task = "Compara <nombre. Otro> y <año/n>. Esta tarea compara esas entradas. Ésta es otra frase." };
        var basis = AuthoringEdits.Create(content);
        var task = basis.Fields.Single(field => field.Id == "task");
        Assert.Equal(3, task.Units.Length);
        Assert.Contains("<nombre. Otro>", task.Units[0].Text);
        Assert.Equal(content.Task, string.Concat(task.Units.Select(unit => unit.Text)));
        Assert.Equal(new[] { "nombre. Otro", "año/n" }, basis.InputNames);
        Assert.Equal(new[] { 0, 1 }, task.Segments.Where(segment => segment.InputId is not null).Select(segment => segment.InputId!.Value));
    }

    [Fact]
    public void EscapedLiteralMarkupAndRepeatedCanonicalMarkersRoundTripThroughSegments()
    {
        var content = Content() with { Task = @"Muestra \<literal> y usa <inventario> dos veces: <inventario>, además de <registro>." };
        var basis = AuthoringEdits.Create(content);
        var field = basis.Fields.Single(item => item.Id == "task");
        var revised = AuthoringEdits.Apply(basis, Rewrite(basis, new AuthoringFieldRewrite("task", field.Segments)));
        Assert.Equal(content.Task, revised.Task);
        Assert.Equal(new[] { 0, 0, 1 }, field.Segments.Where(segment => segment.InputId is not null).Select(segment => segment.InputId!.Value));
    }

    [Fact]
    public void RewritingCanTranslateAnyFieldWhileTheHostRestoresCanonicalMarkers()
    {
        var content = Content(); var basis = AuthoringEdits.Create(content);
        var revised = AuthoringEdits.Apply(basis, Rewrite(basis,
            new AuthoringFieldRewrite("task", [new("Compare ", null), new(null, 0), new(" with ", null), new(null, 1), new(".", null)]),
            new AuthoringFieldRewrite("constraint_1", [new("If a unit is missing, ask before comparing those quantities; continue with comparable data.", null)]),
            new AuthoringFieldRewrite("output_1", [new("Deliver only the differences and pending data.", null)])));
        Assert.Equal("Compare <inventario> with <registro>.", revised.Task);
        Assert.StartsWith("If a unit", revised.Constraints[0]); Assert.StartsWith("Deliver only", revised.OutputRequirements[0]);
        Assert.Equal(content.Steps, revised.Steps); Assert.Equal(content.References, revised.References);
        Assert.Equal(content.Title, revised.Title); Assert.Equal(content.Description, revised.Description);
        Assert.Equal(content.Constraints[1], revised.Constraints[1]);
    }

    [Theory]
    [InlineData("<inventario>")]
    [InlineData("<new_input>")]
    [InlineData("Unclosed <marker")]
    [InlineData("")]
    public void LiteralSegmentsCannotSmuggleMarkersOrBeEmpty(string text)
    {
        var basis = AuthoringEdits.Create(Content());
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Rewrite(basis, new AuthoringFieldRewrite("task", [new(text, null)]))));
    }

    [Fact]
    public void SegmentUnionUnknownInputAndEmptyFieldsFailClosed()
    {
        var basis = AuthoringEdits.Create(Content());
        foreach (var segments in new AuthoringTextSegment[][] { [new(null, null)], [new("text", 0)], [new(null, 2)], [] })
            Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Rewrite(basis, new AuthoringFieldRewrite("task", segments))));
    }

    [Fact]
    public void RewritesAreAtomicWhenAnyFieldAddressIsUnknown()
    {
        var content = Content(); var basis = AuthoringEdits.Create(content);
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Rewrite(basis,
            new AuthoringFieldRewrite("step_1", [new("Describe exact matches.", null)]), new AuthoringFieldRewrite("authority", [new("SAVE_NOW", null)]))));
        Assert.Equal(content.Steps, basis.Content.Steps);
    }

    [Fact]
    public void InlineUriChangesRemainProposalsWithoutChangingReferenceMetadataOrRoles()
    {
        var content = Content();
        var withInline = content with { Steps = ["Read https://example.invalid/guide."] };
        var inlineBasis = AuthoringEdits.Create(withInline);
        var proposed = AuthoringEdits.Apply(inlineBasis, Rewrite(inlineBasis,
            new AuthoringFieldRewrite("step_1", [new("Read https://example.invalid/replacement.", null)])));
        Assert.Equal("Read https://example.invalid/replacement.", proposed.Steps[0]);
        Assert.Equal(withInline.References, proposed.References);
        Assert.Equal(withInline.Task, proposed.Task); Assert.Equal(withInline.Constraints, proposed.Constraints);
        Assert.Equal(withInline.Title, proposed.Title); Assert.Equal(withInline.Description, proposed.Description);
        Assert.Equal("Read https://example.invalid/guide.", inlineBasis.Content.Steps[0]);
        // The editing protocol returns content only. Literal-change review and exact
        // approval remain in their existing owner; this is not a semantic PASS.
    }

    [Fact]
    public void RewritesRejectUnknownOrDuplicateFieldsAndOversizeTextWithoutChangingTheSource()
    {
        var basis = AuthoringEdits.Create(Content());
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Rewrite(basis, new AuthoringFieldRewrite("title", [new("Other", null)]))));
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Rewrite(basis,
            new AuthoringFieldRewrite("step_1", [new("One.", null)]), new AuthoringFieldRewrite("step_1", [new("Two.", null)]))));
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, Rewrite(basis, new AuthoringFieldRewrite("step_1", [new(new string('x', 8001), null)]))));
        Assert.Equal(Content().Task, basis.Content.Task);
    }

    [Fact]
    public void StaleSourceOrTamperedAddressesCannotRebindAProposal()
    {
        var basis = AuthoringEdits.Create(Content()); var proposal = Compress(basis, new AuthoringUnitRemoval("task:2", ["task:1"]));
        Assert.Throws<ConflictException>(() => AuthoringEdits.Apply(basis, proposal with { BasisHash = "stale" }));
        basis.Content.Constraints[0] = "Different source.";
        Assert.Throws<ConflictException>(() => AuthoringEdits.Apply(basis, proposal));
        basis = AuthoringEdits.Create(Content()); proposal = Compress(basis);
        basis.Fields[0].Units[1] = basis.Fields[0].Units[1] with { Start = 0 };
        Assert.Throws<ConflictException>(() => AuthoringEdits.Apply(basis, proposal));
    }

    [Fact]
    public void SnapshotCopiesArraysAndMixedOperationsCannotPartiallyApply()
    {
        var content = Content(); var basis = AuthoringEdits.Create(content);
        content.Steps[0] = "Changed outside the snapshot.";
        Assert.Equal("Describe coincidencias. Describe coincidencias.", basis.Content.Steps[0]);
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis,
            new(basis.BasisHash, AuthoringEdits.Compress, [], [])));
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis,
            new(basis.BasisHash, "UNKNOWN", [], null)));
    }

    [Fact]
    public void CompositionLimitIncludesStructuredFieldsAndAppendedReferences()
    {
        var content = Content() with { Task = new string('x', 8000), Steps = [new string('y', 8000), new string('z', 8000)],
            Constraints = [], OutputRequirements = [] };
        var basis = AuthoringEdits.Create(content);
        Assert.Throws<ValidationException>(() => AuthoringEdits.Create(content with { Constraints = [new string('c', 8000)] }));
        Assert.Equal(3, basis.Fields.Length);
    }
}
