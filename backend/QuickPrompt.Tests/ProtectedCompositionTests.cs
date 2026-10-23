using QuickPrompt.Local;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class ProtectedCompositionTests
{
    [Theory]
    [InlineData("Redacta un correo. No inventes...")]
    [InlineData("Redacta un correo. No inventes nombres,")]
    [InlineData("Redacta un correo. No inventes nombres ni fechas. No inventes nombres ni fechas.")]
    public void CompositionRejectsDuplicatedOrTruncatedProtectedSuffixes(string core)
        => Assert.Throws<ValidationException>(() => TemplateSourceBinding.ValidateEditableCore(core, ["No inventes nombres ni fechas."]));

    [Fact] public void CompositionKeepsACompleteCoreAndPreservesTheExactSourceConstraint()
    {
        const string core = "Redacta un correo para <persona>.";
        TemplateSourceBinding.ValidateEditableCore(core, ["No inventes nombres ni fechas."]);
        var result = TemplateSourceBinding.Bind("Escribe a <persona>. No inventes nombres ni fechas.", core);
        Assert.Equal(core + "\n\nNo inventes nombres ni fechas.", result.Text);
    }
}
