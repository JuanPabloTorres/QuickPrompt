using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringUriBoundaryTests
{
    [Theory]
    [InlineData("`https://example.test/path:`", "https://example.test/path:")]
    [InlineData("<https://example.test/path.>", "https://example.test/path.")]
    [InlineData("\"https://example.test/path)\"", "https://example.test/path)")]
    [InlineData("«https://example.test/path:»", "https://example.test/path:")]
    [InlineData("[manual](https://example.test/path:).", "https://example.test/path:")]
    [InlineData("[manual](https://example.test/path_(one)).", "https://example.test/path_(one)")]
    public void LiteralBoundariesPreserveResourcePunctuationAndRejectItsRemoval(string literal, string uri)
    {
        var coverage = new AuthoringReferenceCoverage("Consulta " + literal);
        Assert.Equal(uri, Assert.Single(coverage.References));
        coverage = coverage.WithScopes(new([new(1, AuthoringReferenceRole.FUTURE_TASK, uri)], ""));
        coverage.ValidateBody("Consulta " + literal);
        Assert.Throws<ValidationException>(() => coverage.ValidateBody("Consulta `" + uri[..^1] + "`"));
    }

    [Fact]
    public void OrdinarySentencePunctuationAndForeignUriSuffixesRemainDistinct()
    {
        var coverage = new AuthoringReferenceCoverage("Consulta (https://example.test/ref). Luego continúa.");
        Assert.Equal("https://example.test/ref", Assert.Single(coverage.References));
        coverage = coverage.WithScopes(new([new(1, AuthoringReferenceRole.FUTURE_TASK, "https://example.test/ref")], ""));
        coverage.ValidateBody("Consulta https://example.test/ref.");
        Assert.Throws<ValidationException>(() => coverage.ValidateBody("Consulta https://example.test/ref-other."));
    }

    [Theory]
    [InlineData("`https://example.test/ref`")]
    [InlineData("<https://example.test/ref>")]
    [InlineData("\"https://example.test/ref\"")]
    [InlineData("«https://example.test/ref»")]
    public void LiteralUriDelimitersDoNotBecomeCurrentSessionAuthority(string literal)
    {
        var coverage = new AuthoringReferenceCoverage("Crea una instrucción usando " + literal);
        foreach (var role in new[] { AuthoringReferenceRole.CURRENT_SESSION, AuthoringReferenceRole.BOTH })
            Assert.Throws<ValidationException>(() => coverage.WithScopes(new([new(1, role, literal)], "")));
    }
}
