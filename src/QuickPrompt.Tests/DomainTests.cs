using QuickPrompt.Domain.Entities;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Local;
using Xunit;
namespace QuickPrompt.Web.Tests;
public class DomainTests
{
    [Theory]
    [InlineData("<tema> y <tema>","tema")]
    [InlineData("<niño/1>","niño/1")]
    [InlineData("<目标>","目标")]
    [InlineData("<nombre completo>","nombre completo")]
    [InlineData("<café>","café")]
    public void UniqueLegacyNames(string text,string expected)=>Assert.Equal([expected],TemplateCompiler.Names(text));
    [Fact] public void ValuesAreNeverRetokenized()=>Assert.Equal("<b> & $1\nα final",TemplateCompiler.Render("<a> <b>",new Dictionary<string,string>{{"a","<b> & $1\nα"},{"b","final"}}));
    [Theory][InlineData("<>")][InlineData("< a>")][InlineData("<a\nb>")][InlineData("<a<b>")][InlineData("<unclosed")]
    public void RejectsMalformedNames(string input)=>Assert.Throws<ValidationException>(()=>TemplateCompiler.Parse(input));
    [Fact] public void EmptyParserButEmptyTemplateRejected(){Assert.Empty(TemplateCompiler.Parse(""));Assert.Throws<ValidationException>(()=>new Template{Title="x"}.Validate());}
    [Fact] public void LiteralEscapes()=>Assert.Equal("<tag> \\ text",TemplateCompiler.Render(@"\<tag\> \\ text",new Dictionary<string,string>()));
    [Fact] public void RequiredVariableCannotBeOmitted()=>Assert.Throws<ValidationException>(()=>TemplateCompiler.Render("<x>",new Dictionary<string,string>()));
    [Fact] public void DefaultUsedButExplicitWins(){Assert.Equal("base",TemplateCompiler.Render("<x>",new Dictionary<string,string>(),new Dictionary<string,string>{{"x","base"}}));Assert.Equal("new",TemplateCompiler.Render("<x>",new Dictionary<string,string>{{"x","new"}},new Dictionary<string,string>{{"x","base"}}));}
    [Fact] public void ReusesLegacyDomainRules(){var p=PromptTemplate.Create("Title","Template","",new());Assert.Equal("N/A",p.Description);p.ToggleFavorite();Assert.True(p.IsFavorite);p.MarkAsDeleted();Assert.False(p.IsActive);Assert.NotNull(p.DeletedAt);}
    [Fact] public void LimitsFieldAndInvalidCategory(){Assert.Throws<ValidationException>(()=>new Template{Title=new string('x',161),Template="t"}.Validate());Assert.Throws<ValidationException>(()=>new Template{Title="x",Template="t",Category=(PromptCategory)999}.Validate());Assert.Throws<ValidationException>(()=>TemplateCompiler.Parse(new string('a',32001)));}
    [Fact] public void AnalysisSeparatesActualMissingFieldsAndHeuristicSignals(){var a=PromptAnalyzer.Analyze(new(){Title="test",Template="Explica <tema>. Usa una lista de máximo 80 palabras."},new Dictionary<string,string>());Assert.Equal(["tema"],a.MissingValues);Assert.True(a.HasObjectiveSignal);Assert.True(a.HasConstraintSignal);Assert.True(a.HasFormatSignal);var b=PromptAnalyzer.Analyze(new(){Template="<x>",Variables=new(){{"x","default"}}},new Dictionary<string,string>());Assert.Empty(b.MissingValues);Assert.False(b.HasObjectiveSignal);}
}
