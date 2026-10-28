using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Tests;
public sealed class SharingTests : IDisposable
{
    private readonly string dir=Path.Combine(Path.GetTempPath(),"qp-sharing-"+Guid.NewGuid());
    private LocalStore NewStore()=>new(Path.Combine(dir,Guid.NewGuid()+".sqlite"));
    public void Dispose(){if(Directory.Exists(dir))Directory.Delete(dir,true);}
    [Fact] public void SharingAnExpectedRevisionCannotExportAnUnreviewedNewerSnapshot()
    {
        var store = NewStore();
        var template = store.Save(new() { Title = "Revisada", Template = "Hola <persona>.", Variables = new() { ["persona"] = "PRIVATE_DEFAULT" } });
        var revision = template.Revision;
        Assert.Equal(store.ExportTemplate(template.Id), store.ExportTemplate(template.Id, revision));
        template.Template = "Texto posterior para <persona>, sin revisar."; store.Save(template);
        var before = store.Export();
        Assert.Throws<ConflictException>(() => store.ExportTemplate(template.Id, revision));
        Assert.Equal(before, store.Export());
        var current = store.ExportTemplate(template.Id, template.Revision);
        using var packet = JsonDocument.Parse(current);
        Assert.Equal(template.Template, packet.RootElement.GetProperty("prompt").GetProperty("template").GetString());
        Assert.DoesNotContain("PRIVATE_DEFAULT", current);
    }
    [Fact] public void PortableProjectionOmitsDefaultsPrivateHistorySettingsAndIdentity()
    {
        var store=NewStore();var t=store.Save(new(){Title="Correo",Template="Hola <nombre>",Variables=new(){{"nombre","PRIVATE_DEFAULT"}}});
        store.SaveSettings(new(RememberValues:true,GeneralInstructions:"PRIVATE_INSTRUCTIONS"));store.Compile(t.Id,new(){{"nombre","PRIVATE_VALUE"}});
        var json=store.ExportTemplate(t.Id);
        foreach(var secret in new[]{"PRIVATE_DEFAULT","PRIVATE_INSTRUCTIONS","PRIVATE_VALUE",t.Id.ToString(),"history","variables"})Assert.DoesNotContain(secret,json);
        var target=NewStore();var preview=target.PreviewImport(json);Assert.Equal(1,preview.NewTemplates);Assert.Empty(target.Templates());
        Assert.Equal(1,target.Import(json));Assert.Equal(t.Template,Assert.Single(target.Templates()).Template);Assert.Empty(Assert.Single(target.Templates()).Variables);
        Assert.Throws<ValidationException>(()=>target.PreviewImport(json,true));
        Assert.Equal(1,target.PreviewImport(json).Duplicates);Assert.Equal(0,target.Import(json));
    }
    [Fact] public void PreviewValidatesAllRecordsAndReportsConflictingIdentityWithoutWriting()
    {
        var store=NewStore();var t=store.Save(new(){Title="Original",Template="Texto"});var before=store.Export();
        var archive=JsonSerializer.Deserialize<Archive>(before,LocalStore.Json)!;archive.Templates[0].Template="Diferente";
        var json=JsonSerializer.Serialize(archive,LocalStore.Json);var preview=store.PreviewImport(json);
        Assert.Equal(1,preview.Conflicts);Assert.Equal(before,store.Export());Assert.Equal(0,store.Import(json));Assert.Equal("Texto",store.FindTemplate(t.Id)!.Template);
        Assert.Throws<ValidationException>(()=>store.PreviewImport("{\"Title\":\"A\",\"Template\":\"<sin cerrar\"}"));Assert.Equal(before,store.Export());
    }
    [Fact] public void CompiledValuesAreACopiedReadOnlySnapshotAndRoundtripJson()
    {
        var values=new Dictionary<string,string>{{"tema","Uno"}};var compiled=new CompiledPrompt(Guid.NewGuid(),Guid.NewGuid(),1,"Test","Uno",values,DateTime.UtcNow);
        values["tema"]="Dos";Assert.Equal("Uno",compiled.Values["tema"]);
        Assert.Throws<NotSupportedException>(()=>((IDictionary<string,string>)compiled.Values)["tema"]="Tres");
        var copy=JsonSerializer.Deserialize<CompiledPrompt>(JsonSerializer.Serialize(compiled,LocalStore.Json),LocalStore.Json)!;
        Assert.Equal("Uno",copy.Values["tema"]);Assert.Equal(compiled.Text,copy.Text);
    }
    [Fact] public void NullHistoryValuesFailControlledValidationBeforeWriting()
    {
        var store=NewStore();var t=store.Save(new(){Title="Uno",Template="Texto"});store.Compile(t.Id,new());
        var json=store.Export().Replace("\"values\": {}","\"values\": null");var before=store.Export();
        Assert.Throws<ValidationException>(()=>store.PreviewImport(json));Assert.Equal(before,store.Export());
    }
}
