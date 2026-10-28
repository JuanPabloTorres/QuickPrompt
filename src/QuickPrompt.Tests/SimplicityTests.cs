using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Web.Tests;
public sealed class SimplicityTests
{
    private static LocalStore Store()=>new(Path.Combine(Path.GetTempPath(),"qp05-"+Guid.NewGuid(),"test.sqlite"));
    [Fact] public void PreviewMatchesCompiledCompositionWithoutAnyPersistedChanges()
    {
        var s=Store();var t=s.Save(new(){Title="Mensaje",Template="Hola <nombre>. <nombre>.",Variables=new(){{"nombre","María"}}});
        var profile=s.SaveInstruction(new(){Name="Breve",Text="Sé breve."});s.SaveSettings(s.Settings() with{GeneralInstructions="En español."});
        var before=s.Export();var preview=s.Preview(t.Id,new(),profile.Id,"Dos frases.");
        Assert.Equal(before,s.Export());Assert.Equal(0,s.FindTemplate(t.Id)!.Uses);Assert.Empty(s.History());
        var compiled=s.Compile(t.Id,new(),profile.Id,"Dos frases.");Assert.Equal(preview.Text,compiled.Text);Assert.Equal(preview.Composition,compiled.Composition);Assert.Equal(1,s.FindTemplate(t.Id)!.Uses);
    }
    [Fact] public void MissingPreviewDoesNotCreateHistoryOrModifySource()
    {
        var s=Store();var t=s.Save(new(){Title="Carta",Template="Hola <nombre>."});var before=s.Export();
        Assert.Throws<ValidationException>(()=>s.Preview(t.Id,new()));Assert.Equal(before,s.Export());
    }
    [Fact] public void RecentCatalogExcludesUnusedAndRetainsPagingAndFilters()
    {
        var s=Store();var a=s.Save(new(){Title="Usado",Template="Texto"});s.Save(new(){Title="No usado",Template="Otro"});s.Compile(a.Id,new());
        var page=s.QueryTemplates(new(Recent:true,Sort:"recent",Size:1));Assert.Equal(1,page.Total);Assert.Equal(a.Id,Assert.Single(page.Items).Id);
        Assert.Empty(s.QueryTemplates(new(Recent:true,Page:1,Size:1)).Items);Assert.Equal(0,s.QueryTemplates(new(Recent:true,Search:"No usado")).Total);
    }
    [Fact] public void ContextualGroupCreationAndAssignmentAreAtomic()
    {
        var s=Store();var t=s.Save(new(){Title="Lectura",Template="Lee un capítulo."});var before=s.Export();
        Assert.Throws<ValidationException>(()=>s.CreateGroupAndAssign(new(){Name="Estudios"},"template",Guid.NewGuid()));Assert.Equal(before,s.Export());
        var g=s.CreateGroupAndAssign(new(){Name="Estudios"},"template",t.Id);Assert.Equal(g.Id,Assert.Single(s.GroupsFor("template",t.Id)));s.NovaDeleteGroup(g.Id,new(g.Revision,true));Assert.Equal(t.Template,s.FindTemplate(t.Id)!.Template);
    }
    [Fact] public void AcceptedEditorImprovementStagesOnlyAndCannotBeReplayed()
    {
        var s=Store();var source=s.Save(new(){Title="Carta",Template="Hola <persona>."});var original=source.Template;
        var p=new AiProposal(Guid.NewGuid(),source.Id,source.Revision,"Saluda cordialmente a <persona>.",[],[],[],["persona"],"qwen3:8b");s.SaveProposal(p);
        var candidate=s.StageDraftImprovement(p.Id,source);
        Assert.Equal(original,source.Template);Assert.Equal(p.Text,candidate.Template);Assert.Equal(original,s.FindTemplate(source.Id)!.Template);Assert.Single(s.Versions(source.Id));
        var staged=s.Export();Assert.Throws<ConflictException>(()=>s.StageDraftImprovement(p.Id,source));Assert.Equal(staged,s.Export());
        s.Save(candidate);Assert.Equal(p.Text,s.FindTemplate(source.Id)!.Template);Assert.Equal(2,s.Versions(source.Id).Count);
    }
    [Fact] public void InvalidEditorImprovementDoesNotMutateDraftOrStore()
    {
        var s=Store();var draft=new Template{Title="Borrador",Template="Hola <persona>."};
        var p=new AiProposal(Guid.NewGuid(),draft.Id,draft.Revision,"Hola <otra>.",[],[],[],[],"qwen3:8b");s.SaveProposal(p);var before=s.Export();
        Assert.Throws<ConflictException>(()=>s.StageDraftImprovement(p.Id,draft));Assert.Equal(before,s.Export());Assert.Equal("Hola <persona>.",draft.Template);Assert.Empty(s.Templates());Assert.Empty(s.Versions(draft.Id));
        var valid=p with{Id=Guid.NewGuid(),Text="Saluda a <persona>."};s.SaveProposal(valid);var staged=s.StageDraftImprovement(valid.Id,draft);Assert.Equal(valid.Text,staged.Template);Assert.Empty(s.Templates());Assert.Empty(s.Versions(draft.Id));
    }
}
