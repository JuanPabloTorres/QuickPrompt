using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class UserInputScopeTests
{
    [Theory]
    [InlineData("un catálogo con búsqueda. Copia.", "un catálogo con búsqueda", "un catálogo con búsqueda.", "un catálogo con búsqueda.")]
    [InlineData("\"lee y luego guarda\". Copia.", "lee y luego guarda", "\"lee y luego guarda\"", "\"lee y luego guarda\".")]
    [InlineData("árboles 🪴. Copia.", "árboles 🪴", "árboles 🪴.", "árboles 🪴.")]
    [InlineData("línea uno\nlínea dos", "línea uno\nlínea dos", "línea uno\nlínea dos", "línea uno\nlínea dos")]
    public void ExtractedCitationsBindCompleteOriginalUnits(string tail, string value, string evidence, string expected)
    {
        var request = "Completa. dato: " + tail;
        var scope = UserInputScope.FromExtraction(request, null, null, ["dato"], [new("dato", value, evidence)]);
        Assert.Equal(expected, scope.Values(request, ["dato"])["dato"]);
        if (tail.EndsWith("Copia.")) Assert.Contains("Copia.", scope.Project(request, ["dato"], true));
    }
    [Fact]
    public void ExtractionCannotSkipDataOrBorrowEvidenceAndSourceRequiresItsRevision()
    {
        Assert.Throws<ValidationException>(() => UserInputScope.FromExtraction("dato: primero segundo. Copia.", null, null, ["dato"], [new("dato", "segundo", "primero segundo.")]));
        Assert.Throws<ValidationException>(() => UserInputScope.FromExtraction("dato: original", null, null, ["dato"], [new("dato", "original", "inventado")]));
        Assert.Throws<ValidationException>(() => UserInputScope.Bind("dato: x", Guid.NewGuid(), null, ["dato"], [new("dato", "x", "x")]));
        Assert.Throws<ValidationException>(() => UserInputScope.Bind("dato: x", null, 1, ["dato"], [new("dato", "x", "x")]));
    }
    [Theory]
    [InlineData("proyecto", "un catálogo de productos con búsqueda.")]
    [InlineData("material", "una ficha con ñ y 中文.")]
    [InlineData("dato", "un proyecto 🪴 de jardín.")]
    public void ScopeSeparatesDataFromWorkflowWithoutChangingTheLiteralOrLegacyGrammar(string name, string value)
    {
        var request = "Completa la ficha con " + name + ": " + value + " Déjala lista para copiar.";
        var scope = UserInputScope.Bind(request, Guid.NewGuid(), 3, [name], [new(name, value, value)]);
        Assert.Equal(value, GroundedVariableResolver.LiteralAssignments(request, [name], scope)[name]);
        Assert.Equal(value + " Déjala lista para copiar.", GroundedVariableResolver.LiteralAssignments(request, [name])[name]);
        var directive = GroundedVariableResolver.InstructionDirective(request, [name], scope);
        Assert.DoesNotContain(value, directive); Assert.Contains("Déjala lista para copiar.", directive);
        Assert.Contains('\n', directive);
        Assert.All(directive.Split('\n').Select(text => text.Trim()).Where(text => text.Length > 0), text => Assert.Contains(text, request));
        Assert.Contains(name + ": [literal input data] Déjala lista para copiar.", GroundedVariableResolver.DirectiveOutsideInputs(request, [name], scope));
        var proposal = new PromptProposal(PreparationDecision.REUSE_EXISTING, null, "Ficha", "", "Revisa <" + name + ">.", [], "", "");
        var resolved = new GroundedVariableResolver().Resolve(new PrepareInput(Guid.NewGuid(), request) { InputScope = scope }, proposal, null);
        Assert.Equal(value, Assert.Single(resolved).Value); Assert.Equal("REQUEST_SCOPE", resolved[0].ValueSource);
    }
    [Fact]
    public void MultipleFieldsKeepTheirOwnRangesEvenWithEqualValuesAndSeparators()
    {
        const string request = "Completa. tema: jardines; publico: jardines. Después copia.";
        var scope = UserInputScope.Bind(request, null, null, ["tema", "publico"], [new("tema", "jardines", "jardines"), new("publico", "jardines.", "jardines.")]);
        var values = scope.Values(request, ["tema", "publico"]);
        Assert.Equal("jardines", values["tema"]); Assert.Equal("jardines.", values["publico"]);
        Assert.Contains("Después copia.", scope.Project(request, ["tema", "publico"], true));
    }
    [Theory]
    [InlineData("", "")]
    [InlineData("café", "caf")]
    [InlineData("otro", "otro")]
    [InlineData("Completa.", "Completa.")]
    public void AModelCannotRewriteOrBorrowASubstringOutsideTheCandidatePrefix(string value, string evidence)
        => Assert.Throws<ValidationException>(() => UserInputScope.Bind("Completa. dato: un café. Copia.", null, null, ["dato"], [new("dato", value, evidence)]));
    [Fact]
    public void ScopeRejectsDuplicateUnknownMissingAndRepeatedNames()
    {
        Assert.Throws<ValidationException>(() => UserInputScope.Bind("dato: x", null, null, ["dato"], []));
        Assert.Throws<ValidationException>(() => UserInputScope.Bind("dato: x", null, null, ["dato"], [new("otro", "x", "x")]));
        Assert.Throws<ValidationException>(() => UserInputScope.Bind("dato: x", null, null, ["dato"], [new("dato", "x", "x"), new("dato", "x", "x")]));
        Assert.Throws<ValidationException>(() => UserInputScope.Bind("dato: x; dato: y", null, null, ["dato"], []));
    }
    [Fact]
    public void ScopeCannotCrossRequestsFormsSourcesOrRevisions()
    {
        var source = Guid.NewGuid(); var scope = UserInputScope.Bind("dato: x", source, 2, ["dato"], [new("dato", "x", "x")]);
        Assert.Throws<ValidationException>(() => scope.Values("dato: y", ["dato"]));
        Assert.Throws<ValidationException>(() => scope.Values("dato: x", ["otro"]));
        Assert.Throws<ConflictException>(() => scope.ValidateTarget(Guid.NewGuid(), 2));
        Assert.Throws<ConflictException>(() => scope.ValidateTarget(source, 3)); scope.ValidateTarget(source, 2);
        var values = scope.Values("dato: x", ["dato"]); values["dato"] = "modified";
        Assert.Equal("x", scope.Values("dato: x", ["dato"])["dato"]);
    }
    [Theory]
    [InlineData("a\u0301", "a")]
    [InlineData("🪴", "\uD83E")]
    public void ScopeCannotCutAUnicodeGrapheme(string literal, string cut)
        => Assert.Throws<ValidationException>(() => UserInputScope.Bind("dato: " + literal, null, null, ["dato"], [new("dato", cut, cut)]));
    [Fact]
    public void StructuredValuesStillHavePriorityAndPendingAuthorityIsBoundToItsOriginalRequest()
    {
        var scope = UserInputScope.Bind("dato: original. Copia.", null, null, ["dato"], [new("dato", "original.", "original.")]);
        var proposal = new PromptProposal(PreparationDecision.REUSE_EXISTING, null, "Ficha", "", "Revisa <dato>.", [], "", "");
        var resolver = new GroundedVariableResolver();
        var explicitInput = new PrepareInput(Guid.NewGuid(), "dato: original. Copia.", Values: new() { ["dato"] = "valor explícito" }) { InputScope = scope };
        Assert.Equal("valor explícito", Assert.Single(resolver.Resolve(explicitInput, proposal, null)).Value);
        var continuation = new PrepareInput(Guid.NewGuid(), "el mismo") { AuthorizedPendingRequest = "dato: original. Copia.", InputScope = scope };
        Assert.Equal("original.", Assert.Single(resolver.Resolve(continuation, proposal, null)).Value);
    }
}
