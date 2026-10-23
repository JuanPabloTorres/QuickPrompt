using QuickPrompt.Local;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class TemplateSourceBindingTests
{
    [Theory]
    [InlineData("Resume <documento>.", "Evalúa la calidad de la evidencia.", "documento")]
    [InlineData("Reply to <cliente> about <asunto>.", "Write a concise, empathetic response.", "cliente", "asunto")]
    [InlineData("Explique <sujet> pour <public>.", "Présentez une analyse critique accessible.", "sujet", "public")]
    public void CanonicalInputsCompileActualValuesAcrossUnrelatedTaskDomains(string source, string candidate, params string[] names)
    {
        var result = TemplateSourceBinding.Bind(source, candidate);
        Assert.StartsWith(candidate, result.Text); Assert.Equal(names, result.BoundInputs);
        Assert.Equal(names, TemplateCompiler.Names(result.Text));
        var values = names.ToDictionary(name => name, name => "dato real " + name);
        var compiled = TemplateCompiler.Render(result.Text, values);
        foreach (var name in names) Assert.Contains(values[name], compiled);
        Assert.Empty(TemplateCompiler.Names(compiled));
    }
    [Fact] public void ExistingInputsStayInPlaceAndAnInputInARetainedClauseIsNotDuplicated()
    {
        var result = TemplateSourceBinding.Bind("Resume <tema>. No publiques <privado>.", "Analiza <tema> y sus riesgos.");
        Assert.Contains("No publiques <privado>", result.Text); Assert.Empty(result.BoundInputs);
        Assert.Equal(new[] { "tema", "privado" }, TemplateCompiler.Names(result.Text));
        var again = TemplateSourceBinding.Bind("Resume <tema>. No publiques <privado>.", result.Text);
        Assert.Equal(result.Text, again.Text); Assert.Empty(again.RetainedConstraints); Assert.Empty(again.BoundInputs);
    }
    [Theory]
    [InlineData("Resume <tema> con ejemplos.", "Resume con ejemplos.")]
    [InlineData("Resume <tema>. No inventes datos.", "Resume.")]
    [InlineData("Resume <tema>.", "Revisa <otro>.")]
    public void ContractCompositionCannotHideANonEditOrInventAnInput(string source, string candidate)
        => Assert.Throws<ValidationException>(() => TemplateSourceBinding.Bind(source, candidate));
    [Fact] public void CompositionCannotBypassTheFinalLengthLimit()
        => Assert.Throws<ValidationException>(() => TemplateSourceBinding.Bind("Explica <tema>.", new string('x', 31999)));

    [Theory]
    [InlineData("Explica el proceso. No inventes datos.", "No inventes datos.")]
    [InlineData("Explain the process. Keep actual identifiers; Do not invent dates.", "Keep actual identifiers;\nDo not invent dates.")]
    [InlineData("Resume. Preserva <datos>\nNunca publiques información privada.", "Preserva <datos>\nNunca publiques información privada.")]
    public void RestoredConstraintsRetainSourcePunctuationAndAdjacentClauses(string source, string suffix)
    {
        const string candidate = "Evalúa los riesgos del proceso.";
        var result = TemplateSourceBinding.Bind(source, candidate);
        Assert.Equal(candidate + "\n\n" + suffix, result.Text);
        Assert.Empty(TemplateConstraints.Missing(source, result.Text));
        Assert.Equal(result.Text, TemplateSourceBinding.Bind(source, result.Text).Text);
    }
}
