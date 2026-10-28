using QuickPrompt.Local;
using Xunit;

namespace QuickPrompt.Tests;
public class TemplateEditorTests
{
    [Fact] public void SelectionRoundtripAndHistoryPreserveOriginalMultilineText()
    {
        const string text="Explica programación\npara 李 & amigos.";
        var e=new TemplateEditor(text,new Dictionary<string,string>());
        e.Create(8,text.Length-1,"tema");Assert.Equal("Explica <tema>.",e.Text);
        e.Remove(8,14);Assert.Equal(text,e.Text);
        e.Undo();Assert.Equal("Explica <tema>.",e.Text);
        e.Undo();Assert.Equal(text,e.Text);e.Redo();Assert.Equal("Explica <tema>.",e.Text);
        e.Input("Otro texto",e.Defaults);Assert.False(e.CanRedo);
    }
    [Fact] public void RenameUpdatesRepeatedTokensAndDefaultsButNotEscapedLiterals()
    {
        var e=new TemplateEditor(@"<nombre> y <nombre> y \<nombre>",new Dictionary<string,string>{{"nombre","Ana"}});
        e.Rename(1,7,"persona");Assert.Equal(@"<persona> y <persona> y \<nombre>",e.Text);Assert.Equal("Ana",e.Defaults["persona"]);
        Assert.Equal("Ana y Ana y <nombre>",TemplateCompiler.Render(e.Text,new Dictionary<string,string>(),e.Defaults));
        e.Undo();Assert.Equal("Ana",e.Defaults["nombre"]);Assert.False(e.Defaults.ContainsKey("persona"));
        e.Remove(0,8);Assert.Equal("nombre y <nombre> y \\<nombre>",e.Text);Assert.True(e.Defaults.ContainsKey("nombre"));
    }
    [Theory] [InlineData("x><x")] [InlineData(" nombre ")] [InlineData("a\nb")] [InlineData("")]
    public void InvalidNamesCannotInjectMarkers(string name){var e=new TemplateEditor("Hola",new Dictionary<string,string>());Assert.Throws<ValidationException>(()=>e.Create(0,4,name));Assert.Equal("Hola",e.Text);}
    [Fact] public void OverlapCollisionAndIncompleteEscapesAreRejectedAtomically()
    {
        var e=new TemplateEditor(@"<a> <b> \<literal>",new Dictionary<string,string>());
        Assert.Throws<ValidationException>(()=>e.Rename(0,3,"b"));
        Assert.Throws<ValidationException>(()=>e.Create(1,5,"c"));
        Assert.Throws<ValidationException>(()=>e.Create(9,17,"c"));Assert.False(e.CanUndo);
    }
    [Fact] public void ManualInvalidIntermediateInputCanBeUndone()
    {
        var e=new TemplateEditor("Hola",new Dictionary<string,string>());e.Input("Hola <",e.Defaults);
        Assert.Throws<ValidationException>(()=>e.Create(0,4,"saludo"));e.Undo();Assert.Equal("Hola",e.Text);
    }
    [Fact] public void OriginalSurvivesRenameAndUndoVariable()
    {
        var e=new TemplateEditor("Explica C# & .NET",new Dictionary<string,string>());e.Create(8,17,"tecnologia");
        e.Rename(8,20,"tema");e.Remove(8,14);Assert.Equal("Explica C# & .NET",e.Text);
    }
}
