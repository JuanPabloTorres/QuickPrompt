using System.Text.Json;
using Microsoft.Data.Sqlite;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;
namespace QuickPrompt.Web.Tests;
public sealed class StabilizationTests : IDisposable
{
    private readonly string folder=Path.Combine(Path.GetTempPath(),"quickprompt-goal02-"+Guid.NewGuid());
    private LocalStore Store()=>new(Path.Combine(folder,"normal.sqlite"));
    [Fact] public void EverydaySearchFindsAccentedTitlesAndContentWithoutChangingStoredText()
    {
        var t=new Template{Title="Revisión .NET",Description="Aplicación local",Template="Conserva el diseño y <restricciones>."};
        Assert.True(PromptSearch.Matches(t,"REVISION"));Assert.True(PromptSearch.Matches(t,"aplicacion"));Assert.True(PromptSearch.Matches(t,"diseno"));Assert.True(PromptSearch.Matches(t,""));Assert.False(PromptSearch.Matches(t,"otra cosa"));Assert.Equal("Revisión .NET",t.Title);
    }
    [Fact] public void FutureSchemaIsRejectedWithoutWritingIntoIt()
    {
        Directory.CreateDirectory(folder);SQLitePCL.Batteries_V2.Init();var path=Path.Combine(folder,"normal.sqlite");
        using(var db=new SqliteConnection($"Data Source={path};Pooling=False")){db.Open();using var c=db.CreateCommand();c.CommandText="CREATE TABLE SchemaVersion(Version INTEGER PRIMARY KEY);INSERT INTO SchemaVersion VALUES($future);CREATE TABLE FutureData(Value TEXT);INSERT INTO FutureData VALUES('keep');";c.Parameters.AddWithValue("$future",LocalStore.CurrentSchemaVersion+1);c.ExecuteNonQuery();}
        var before=File.ReadAllBytes(path);Assert.Throws<ValidationException>(()=>Store());Assert.Equal(before,File.ReadAllBytes(path));
    }
    [Fact] public void RestoreRejectsLegacyTemplateInsteadOfReplacingTheWholeLibrary()
    {
        var s=Store();s.Save(new(){Title="Conservar",Template="Original"});var before=s.Export();
        Assert.Throws<ValidationException>(()=>s.Import("{\"Title\":\"Objeto legado\",\"Template\":\"Contenido\"}",restore:true));Assert.Equal(before,s.Export());
    }
    [Fact] public void RestoreRollsBackWhenActualSqliteWriteFailsAfterDelete()
    {
        var s=Store();s.Save(new(){Title="Conservar",Template="Original"});var before=s.Export();var archive=JsonSerializer.Deserialize<Archive>(before,LocalStore.Json)!;archive.Templates[0].Title="blocked";
        using(var db=new SqliteConnection($"Data Source={Path.Combine(folder,"normal.sqlite")};Pooling=False")){db.Open();using var c=db.CreateCommand();c.CommandText="CREATE TRIGGER fail_restore BEFORE INSERT ON Documents WHEN json_extract(NEW.Payload,'$.title')='blocked' BEGIN SELECT RAISE(ABORT,'injected test failure');END;";c.ExecuteNonQuery();}
        Assert.Throws<SqliteException>(()=>s.Import(JsonSerializer.Serialize(archive,LocalStore.Json),restore:true));Assert.Equal(before,s.Export());Assert.Equal("ok",s.CheckIntegrity());
    }
    public void Dispose(){var target=Path.GetFullPath(folder);if(!target.StartsWith(Path.Combine(Path.GetTempPath(),"quickprompt-goal02-"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unsafe cleanup path");if(Directory.Exists(target))Directory.Delete(target,true);}
}
