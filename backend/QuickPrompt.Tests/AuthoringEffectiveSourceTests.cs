using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringEffectiveSourceTests
{
    [Theory]
    [InlineData("No uses librerías externas.", "Permite librerías aprobadas.")]
    [InlineData("Permite librerías externas.", "No uses librerías externas.")]
    [InlineData("Prepara tres preguntas.", "Prepara cinco preguntas.")]
    public void ASourceBoundDeltaReplacesTheOldConstraintBasisRatherThanReinsertingIt(string oldClause, string newClause)
    {
        var (contract, inputs) = Replaced(oldClause, newClause);
        var body = contract.Compose(inputs);
        Assert.Contains(newClause, body); Assert.DoesNotContain(oldClause, body);
        Assert.Contains("No inventes datos.", body); Assert.Equal(new[] { "tema" }, TemplateCompiler.Names(body));
        Assert.DoesNotContain(oldClause, contract.EffectiveSource);
        Assert.Equal(body, inputs.Bind(contract.EffectiveSource, body));
        Assert.False(contract.IsComposed(body + " Cambio fuera del compositor."));
    }

    [Fact]
    public void ExplicitRemovalDoesNotReintroduceTheRemovedProhibitionAndStillPreservesOtherConstraints()
    {
        const string old = "No uses librerías externas.";
        var (contract, inputs) = Replaced(old, null);
        var body = contract.Compose(inputs);
        Assert.DoesNotContain(old, body); Assert.Contains("No inventes datos.", body);
        Assert.Equal(new[] { "tema" }, TemplateCompiler.Names(body));
    }

    [Fact]
    public void CallingSourceProseContextDoesNotAuthorizeDroppingDeterministicallyProtectedProhibitions()
    {
        const string source = "Explica <tema>. No inventes datos.";
        const string request = "Reformula la explicación.";
        var contract = new AuthoringObligationContract(request, source).WithInventory(new(
            [new(1, 3, "CURRENT_WORKFLOW")], [new(1, 5, "CONTEXT")], [], null, ""));
        var body = contract.Compose(PromptInputContract.FromUserSelection(request, source, true, []), [new(2, "Resume <tema>.")]);
        Assert.Contains("No inventes datos.", body); Assert.Equal(source, contract.EffectiveSource);
    }

    [Fact]
    public void SourceUriInContextCannotDisappearMerelyBecauseTheModelRewritesThatProse()
    {
        const string source = "Consulta https://example.test/original para resolver <tema>.";
        const string request = "Reformula la explicación.";
        var contract = new AuthoringObligationContract(request, source);
        contract.WithInventory(new([new(1, 3, "CURRENT_WORKFLOW")], [new(1, contract.SourceUnits.Length, "CONTEXT")], [], null, ""));
        var failure = Assert.Throws<ValidationException>(() => contract.Compose(PromptInputContract.FromUserSelection(request, source, true, []),
            [new(2, "Explica <tema> sin citar recursos.")]));
        Assert.Equal("AUTHORING_REFERENCE_BODY_MISMATCH", failure.Data["NovaValidationCode"]);
        Assert.False(contract.IsComposed("Explica <tema> sin citar recursos."));
    }

    [Fact]
    public void AnExplicitSourceBoundUriReplacementUsesTheNewResourceAndKeepsOtherGuards()
    {
        const string old = "Consulta https://example.test/original para resolver <tema>.";
        const string replacement = "Consulta https://example.test/nueva para resolver <tema>.";
        var (contract, inputs) = Replaced(old, replacement);
        var body = contract.Compose(inputs);
        Assert.DoesNotContain("https://example.test/original", body); Assert.Contains("https://example.test/nueva", body);
        Assert.Contains("No inventes datos.", body); Assert.Equal(new[] { "tema" }, TemplateCompiler.Names(body));
    }

    private static (AuthoringObligationContract Contract, PromptInputContract Inputs) Replaced(string oldClause, string? newClause)
    {
        var source = oldClause + " Explica <tema>. No inventes datos.";
        var request = (newClause is null ? "Elimina la primera regla." : newClause + " Sustituye la primera regla por esta.") + " Conserva el resto.";
        var contract = new AuthoringObligationContract(request, source);
        var oldCount = new AuthoringObligationContract(oldClause).RequestUnits.Length;
        var replacementCount = newClause is null ? 0 : new AuthoringObligationContract(newClause).RequestUnits.Length;
        contract.WithInventory(new([new(1, contract.RequestUnits.Length, "CURRENT_WORKFLOW")],
            [new(1, oldCount, "REPLACED"), new(oldCount + 1, contract.SourceUnits.Length, "FUTURE_TASK")],
            [new(1, oldCount, newClause is null ? null : 1, newClause is null ? null : replacementCount, 1, contract.RequestUnits.Length)], null, ""));
        return (contract, PromptInputContract.FromUserSelection(request, source, true, []));
    }
}
