using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using QuickPrompt.Domain.Enums;
using Xunit;

namespace QuickPrompt.Tests;
public sealed class PanelSessionTests:IDisposable
{
    private readonly string dir=Path.Combine(Path.GetTempPath(),"qp-panel-"+Guid.NewGuid());
    public void Dispose(){if(Directory.Exists(dir))Directory.Delete(dir,true);}
    [Fact] public void PanelUsesAuthoritativeCompilationAndRejectsStaleRevisionWithoutUsage()
    {
        var store=new LocalStore(Path.Combine(dir,"test.sqlite"));var t=store.Save(new(){Title="Código",Template="Explica <tema>",Variables=new(){{"tema","PRIVATE_DEFAULT"}},Category=PromptCategory.Programming});store.SaveSettings(new(GeneralInstructions:"En español."));
        var panel=new PanelSession(new PromptPanel(new NovaPromptStore(store),store));var item=Assert.Single(panel.Library("",false,0).Items);Assert.Equal(new[]{"tema"},item.Variables);
        var compiled=panel.Compile(t.Id,t.Revision,new(){{"tema","SQLite"}});Assert.Equal(store.History().Single().Text,compiled.Text);Assert.Contains("SQLite",compiled.Text);
        var updated=store.FindTemplate(t.Id)!;store.Save(updated);Assert.Throws<ConflictException>(()=>panel.Compile(t.Id,item.Revision,new(){{"tema","Blazor"}}));Assert.Single(store.History());
        Assert.Throws<ValidationException>(()=>panel.Compile(t.Id,updated.Revision,new(){{"extra","x"}}));Assert.Single(store.History());
    }
    [Fact] public void SearchIncludesCategoryGroupAndRecallWordsAcrossFields()
    {
        var store=new LocalStore(Path.Combine(dir,"test.sqlite"));var t=store.Save(new(){Title="Auditoría",Template="Revisa código",Category=PromptCategory.Programming});var group=store.SaveGroup(new(){Name="Trabajo Blazor"});store.AssignGroup(group.Id,"template",[t.Id]);
        Assert.Single(store.QueryTemplates(new(Search:"blazor programming auditoria")).Items);
        Assert.Empty(store.QueryTemplates(new(Search:"blazor education")).Items);
    }
}
