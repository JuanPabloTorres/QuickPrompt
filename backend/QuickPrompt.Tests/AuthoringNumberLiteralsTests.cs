using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

/// <summary>Lexical source-compression guards; these checks do not certify quantities' semantic roles.</summary>
public sealed class AuthoringNumberLiteralsTests
{
    [Theory]
    [InlineData("Entrega 5 resultados.", "5")]
    [InlineData("Usa -2 y +3.", "-2|+3")]
    [InlineData("Conserva 1.5, 1,5 y 20,000.", "1.5|1,5|20,000")]
    [InlineData("Entrega cinco resultados y cero supuestos.", "cinco|cero")]
    [InlineData("Return FIVE results and twenty-two questions.", "five|twenty|two")]
    [InlineData("Conserva dieciséis y veintidós elementos.", "dieciséis|veintidós")]
    public void RecognizedDigitNotationsAndDeclaredCardinalsStayLiteral(string text, string expected) =>
        Assert.Equal(expected.Split('|'), AuthoringNumberLiterals.Read(text));

    [Fact]
    public void ConservationUsesASingleSetWithoutInterpretingCardinalsAsNormalizedValues()
    {
        Assert.True(AuthoringNumberLiterals.Conserved("Entrega cinco resultados. La tarea entrega cinco resultados.", "Entrega cinco resultados."));
        Assert.True(AuthoringNumberLiterals.Conserved("Return 5 results. Return 5 results.", "Return 5 results."));
        Assert.False(AuthoringNumberLiterals.Conserved("Entrega cinco resultados.", "Return five results."));
        Assert.False(AuthoringNumberLiterals.Conserved("Usa 1.50.", "Usa 1.5."));
        Assert.False(AuthoringNumberLiterals.Conserved("Usa -2.", "Usa 2."));
    }

    [Fact]
    public void ArticlesAndVariableNamesAreNotQuantityFacts()
    {
        Assert.Empty(AuthoringNumberLiterals.Read("Una tarea para un asistente con <cinco_2>, <paso 2026>, <one> y <3>."));
        Assert.Equal(new[] { "cinco", "2" }, AuthoringNumberLiterals.Read("Con <cinco_2> entrega cinco resultados y 2 dudas."));
        // Ambiguous lexemes in the declared repertoire remain conservative tokens.
        Assert.Equal(new[] { "one", "once" }, AuthoringNumberLiterals.Read("One may review this once."));
    }

    [Fact]
    public void EqualNumericTokensDoNotCertifyWhichObligationOwnsTheQuantity()
    {
        Assert.True(AuthoringNumberLiterals.Conserved("Entrega cinco opciones. Haz cinco preguntas.", "Entrega cinco opciones."));
        // This intentionally demonstrates the guard's limit: the independent
        // semantic evaluator must still reject losing the questioning task.
    }

    [Theory]
    [InlineData("cinco")]
    [InlineData("five")]
    [InlineData("5")]
    public void CompressionCannotLoseAQuantityEvenWhenAnotherFieldContainsTheSameToken(string quantity)
    {
        var source = Content("Evalúa <datos>. Entrega " + quantity + " resultados.",
            ["Entrega " + quantity + " comprobaciones adicionales."]);
        var basis = AuthoringEdits.Create(source);
        Assert.Equal(2, basis.Fields.Single(field => field.Id == "task").Units.Length);
        var proposal = new AuthoringRevisionProposal(basis.BasisHash, AuthoringEdits.Compress,
            [new("task:2", ["task:1"])], null);
        var error = Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, proposal));
        Assert.Contains("numéricos", error.Message);
        Assert.Equal(LiveRuns.Hash(source), LiveRuns.Hash(basis.Content));
    }

    [Fact]
    public void DuplicateQuantityRestatementCanBeRemovedWithoutChangingItsRecognizedSet()
    {
        var source = Content("Entrega cinco resultados de <datos>. Esta tarea entrega cinco resultados.");
        var basis = AuthoringEdits.Create(source);
        var result = AuthoringEdits.Apply(basis, new(basis.BasisHash, AuthoringEdits.Compress,
            [new("task:2", ["task:1"])], null));
        Assert.Equal("Entrega cinco resultados de <datos>. ", result.Task);
        Assert.Equal(new[] { "cinco" }, AuthoringNumberLiterals.Read(result.Task));
    }

    [Fact]
    public void NumericRejectionIsAtomicAndRewritingCanProposeChangingFiveToThree()
    {
        var source = Content("Evalúa <datos>. Entrega cinco resultados.") with { Steps = ["Describe los hechos. Describe los hechos."] };
        var basis = AuthoringEdits.Create(source);
        Assert.Throws<ValidationException>(() => AuthoringEdits.Apply(basis, new(basis.BasisHash, AuthoringEdits.Compress,
            [new("step_1:2", ["step_1:1"]), new("task:2", ["task:1"])], null)));
        Assert.Equal(source.Steps, basis.Content.Steps); Assert.Equal(source.Task, basis.Content.Task);
        var changed = AuthoringEdits.Apply(basis, new(basis.BasisHash, AuthoringEdits.Rewrite, null,
            [new("task", [new("Evalúa ", null), new(null, 0), new(" y entrega tres resultados.", null)])]));
        Assert.Equal("Evalúa <datos> y entrega tres resultados.", changed.Task);
        Assert.Equal(source.Steps, changed.Steps); Assert.Equal(source.References, changed.References);
        // A proposed quantity change is not an approval or a semantic assessment.
    }

    private static AuthoringBriefContent Content(string task, string[]? constraints = null) =>
        new("Cantidad", "Metadatos conservados", task, [], constraints ?? [], [], []);
}
