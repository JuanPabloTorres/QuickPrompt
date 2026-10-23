using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class EditorRenameTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenamesAllRealOccurrencesMovesDefaultsAndReturnsTheActualSelectedSpan(bool caret)
    {
        const string text = "<名>\n<名> y \\<名> y <otro>.";
        var defaults = new Dictionary<string, string> { ["名"] = "α\nβ 李", ["otro"] = "fin", ["removed"] = "stale" };
        var start = text.IndexOf("<名>", 1, StringComparison.Ordinal);
        var result = EditorTools.Rename(new(text, start + (caret ? 1 : 0), start + (caret ? 1 : 3), "persona", defaults));
        Assert.Equal("<persona>\n<persona> y \\<名> y <otro>.", result.TemplateText);
        Assert.Equal("<persona>", result.TemplateText[result.Start..result.End]);
        Assert.Equal(result.TemplateText.IndexOf("<persona>", 1, StringComparison.Ordinal), result.Start);
        Assert.Equal(new[] { "persona", "otro" }, result.VariableNames);
        Assert.Equal("α\nβ 李", result.Defaults["persona"]); Assert.Equal("fin", result.Defaults["otro"]);
        Assert.False(result.Defaults.ContainsKey("名")); Assert.False(result.Defaults.ContainsKey("removed"));
        Assert.Equal("α\nβ 李\nα\nβ 李 y <名> y fin.", TemplateCompiler.Render(result.TemplateText, new Dictionary<string, string>(), result.Defaults));
        Assert.Equal(3, defaults.Count); Assert.True(defaults.ContainsKey("名")); Assert.False(defaults.ContainsKey("persona"));
    }

    [Theory]
    [InlineData(0, 3, "b")]
    [InlineData(0, 3, "x><x")]
    [InlineData(0, 3, "")]
    [InlineData(0, 7, "nuevo")]
    [InlineData(99, 99, "nuevo")]
    [InlineData(-1, 1, "nuevo")]
    [InlineData(4, 3, "nuevo")]
    public void RejectsInvalidTransformsAtomically(int start, int end, string name)
    {
        var defaults = new Dictionary<string, string> { ["a"] = "valor" };
        Assert.Throws<ValidationException>(() => EditorTools.Rename(new("<a> <b>", start, end, name, defaults)));
        Assert.Equal("valor", Assert.Single(defaults).Value);
    }

    [Fact]
    public void DoesNotRenameEscapedLiteralsOrAcceptInvalidDefaultValues()
    {
        Assert.Throws<ValidationException>(() => EditorTools.Rename(new(@"\<a>", 2, 3, "b")));
        Assert.Throws<ValidationException>(() => EditorTools.Rename(new("<a>", 0, 3, "b", new() { ["a"] = new('x', 8001) })));
        Assert.Throws<ValidationException>(() => EditorTools.Rename(new(new('x', 32001), 0, 1, "b")));
    }
}
