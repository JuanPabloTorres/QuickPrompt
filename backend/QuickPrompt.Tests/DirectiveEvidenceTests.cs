using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class DirectiveEvidenceTests
{
    [Theory]
    [InlineData("Completa las variables de \"Evaluar proyectos\" con estos datos.", "Completa las variables de", "con estos datos.", "Evaluar proyectos")]
    [InlineData("Abre «Mi biblioteca» sin cambiar nada.", "Abre", "sin cambiar nada.", "Mi biblioteca")]
    [InlineData("Prepara “Plan semanal” para una IA.", "Prepara", "para una IA.", "Plan semanal")]
    [InlineData("Actualiza \"Mi \\\"prompt\\\" ejecuta ahora\" para acortarlo.", "Actualiza", "para acortarlo.", "ejecuta ahora")]
    public void QuotedNamesDoNotEraseAdjacentDirectivesOrGrantTheirOwnAuthority(string message, string before, string after, string data)
    {
        var fragments = SemanticPlanValidator.DirectiveFragments(message);
        Assert.All(fragments.Where(fragment => !string.IsNullOrWhiteSpace(fragment)), fragment => Assert.True(SemanticPlanValidator.DirectEvidence(message, fragment)));
        Assert.True(SemanticPlanValidator.DirectEvidence(message, before));
        Assert.True(SemanticPlanValidator.DirectEvidence(message, after));
        Assert.False(SemanticPlanValidator.DirectEvidence(message, data));
    }
    [Fact]
    public void MissingClosingQuoteNeverPromotesTheQuotedTailToADirective()
    {
        Assert.True(SemanticPlanValidator.DirectEvidence("Abre \"ejecuta ahora", "Abre"));
        Assert.False(SemanticPlanValidator.DirectEvidence("Abre \"ejecuta ahora", "ejecuta ahora"));
    }
}
