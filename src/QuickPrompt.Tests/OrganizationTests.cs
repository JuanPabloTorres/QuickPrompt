using System.Text.Json;
using Microsoft.Data.Sqlite;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Web.Tests;

public sealed class OrganizationTests : IDisposable
{
    private readonly string folder=Path.Combine(Path.GetTempPath(),"quickprompt-goal04-"+Guid.NewGuid());
    private string PathFor(string name="main")=>Path.Combine(folder,name+".sqlite");
    private LocalStore Store(string name="main")=>new(PathFor(name));
    private static Template Prompt(string title="Revisión")=>new(){Title=title,Template="Revisa <cambio>. No alteres datos."};
    private static InstructionProfile Profile()=>new(){Name="Arquitectura",Text="Evalúa seguridad y contratos. No inventes hechos."};
    private SqliteConnection Open(string name="main") {SQLitePCL.Batteries_V2.Init();Directory.CreateDirectory(folder);var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=PathFor(name),Pooling=false,ForeignKeys=true}.ToString());db.Open();return db;}
    [Fact] public void MigrationFromV1PreservesPayloadAndCreatesUsableRollbackCopyOnce()
    {
        const string payload="{\"id\":\"ba2b4aa1-65b4-4c6e-876a-10c985eb76d5\",\"title\":\"Original\",\"template\":\"Hola\",\"isFavorite\":true,\"isActive\":true,\"revision\":3,\"createdAt\":\"2024-01-02T03:04:05Z\"}";
        using(var db=Open()){using var c=db.CreateCommand();c.CommandText="CREATE TABLE SchemaVersion(Version INTEGER PRIMARY KEY);INSERT INTO SchemaVersion VALUES(1);CREATE TABLE Documents(Kind TEXT,Id TEXT,Payload TEXT,PRIMARY KEY(Kind,Id));INSERT INTO Documents VALUES('template','ba2b4aa1-65b4-4c6e-876a-10c985eb76d5',$payload)";c.Parameters.AddWithValue("$payload",payload);c.ExecuteNonQuery();}
        var s=Store();Assert.True(s.Templates().Single().IsFavorite);Assert.Equal(3,s.Templates().Single().Revision);
        using(var db=Open()){using var c=db.CreateCommand();c.CommandText="SELECT Payload FROM Documents WHERE Kind='template'";Assert.Equal(payload,c.ExecuteScalar());c.CommandText="SELECT MAX(Version) FROM SchemaVersion";Assert.Equal(2L,c.ExecuteScalar());}
        Store();var backups=Directory.GetFiles(folder,"main.sqlite.before-goal04-*.sqlite");Assert.Single(backups);
        using var rollback=new SqliteConnection($"Data Source={backups[0]};Pooling=False;Mode=ReadOnly");rollback.Open();using var check=rollback.CreateCommand();check.CommandText="SELECT MAX(Version) FROM SchemaVersion";Assert.Equal(1L,check.ExecuteScalar());check.CommandText="SELECT Payload FROM Documents";Assert.Equal(payload,check.ExecuteScalar());
    }
    [Fact] public void MigrationDdlFailureRollsBackNewTablesAndVersion()
    {
        using(var db=Open()){using var c=db.CreateCommand();c.CommandText="CREATE TABLE SchemaVersion(Version INTEGER PRIMARY KEY);INSERT INTO SchemaVersion VALUES(1);CREATE TABLE Documents(Kind TEXT,Id TEXT,Payload TEXT,PRIMARY KEY(Kind,Id));CREATE TABLE GroupMemberships(Broken TEXT);";c.ExecuteNonQuery();}
        Assert.Throws<SqliteException>(()=>Store());using var verify=Open();using var q=verify.CreateCommand();q.CommandText="SELECT MAX(Version) FROM SchemaVersion";Assert.Equal(1L,q.ExecuteScalar());q.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE name='PromptGroups'";Assert.Equal(0L,q.ExecuteScalar());
    }
    [Fact] public void GroupsAreManyToManyUniqueAndDeleteWithoutDeletingPrompts()
    {
        var s=Store();var t=s.Save(Prompt());var a=s.SaveGroup(new(){Name="Desarrollo"});var b=s.SaveGroup(new(){Name="Trabajo"});
        s.AssignGroup(a.Id,"template",[t.Id]);s.AssignGroup(a.Id,"template",[t.Id]);s.AssignGroup(b.Id,"template",[t.Id]);Assert.Equal(2,s.GroupsFor("template",t.Id).Count);Assert.Single(s.Templates());Assert.All(s.Groups(),g=>Assert.Equal(1,g.Templates));
        s.NovaDeleteGroup(a.Id,new(a.Revision,true));Assert.Single(s.GroupsFor("template",t.Id));Assert.Equal(t.Template,s.FindTemplate(t.Id)!.Template);s.AssignGroup(b.Id,"template",[t.Id],false);Assert.Empty(s.GroupsFor("template",t.Id));Assert.Equal(1,s.QueryTemplates(new(Ungrouped:true)).Total);
    }
    [Fact] public void GroupNameUniquenessPinOrderAndOptimisticConcurrencySurviveRestart()
    {
        var s=Store();var a=s.SaveGroup(new(){Name="Investigación",SortOrder=20});var b=s.SaveGroup(new(){Name="Trabajo",SortOrder=10});Assert.Equal(b.Id,s.Groups()[0].Group.Id);a.IsPinned=true;s.SaveGroup(a);Assert.Equal(a.Id,Store().Groups()[0].Group.Id);
        Assert.Throws<ValidationException>(()=>s.SaveGroup(new(){Name="INVESTIGACION"}));var stale=JsonSerializer.Deserialize<PromptGroup>(JsonSerializer.Serialize(a))!;a.Description="nuevo";s.SaveGroup(a);Assert.Throws<ConflictException>(()=>s.SaveGroup(stale));
    }
    [Fact] public void BatchMembershipIsAtomicAndDatabaseForeignKeysRejectOrphans()
    {
        var s=Store();var t=s.Save(Prompt());var g=s.SaveGroup(new(){Name="Trabajo"});Assert.Throws<ValidationException>(()=>s.AssignGroup(g.Id,"template",[t.Id,Guid.NewGuid()]));Assert.Empty(s.GroupsFor("template",t.Id));
        using var db=Open();using var c=db.CreateCommand();c.CommandText="INSERT INTO GroupMemberships VALUES($group,'template',$id)";c.Parameters.AddWithValue("$group",g.Id.ToString());c.Parameters.AddWithValue("$id",Guid.NewGuid().ToString());Assert.Throws<SqliteException>(()=>c.ExecuteNonQuery());
    }
    [Fact] public void InstructionVersionsFavoritesGroupsAndDeletionPreserveHistoricalComposition()
    {
        var s=Store();var t=s.Save(Prompt());var p=s.SaveInstruction(Profile());var g=s.SaveGroup(new(){Name="Arquitectura"});s.AssignGroup(g.Id,"instruction",[p.Id]);p.Text+=" Revisa pruebas.";p.IsFavorite=true;s.SaveInstruction(p);Assert.Equal(2,s.InstructionVersions(p.Id).Count);Assert.Equal(1,s.QueryInstructions(new(Favorites:true,GroupId:g.Id)).Total);
        var h=s.Compile(t.Id,new(){{"cambio","el contrato"}},p.Id);s.DeleteInstruction(p.Id,new(p.Revision,true));Assert.Null(s.FindInstruction(p.Id));Assert.Equal(p.Text,Store().History().Single().Composition!.Profile);Assert.Equal(h.Text,s.History().Single().Text);Assert.Empty(s.InstructionVersions(p.Id));
    }
    [Fact] public void CompilationIsDeterministicAndSnapshotsProfileSettingsAndAdjustments()
    {
        var s=Store();var t=s.Save(Prompt());var p=s.SaveInstruction(Profile());s.SaveSettings(new(GeneralInstructions:"Usa español."));var h=s.Compile(t.Id,new(){{"cambio","x 😀 <literal>"}},p.Id,"Máximo 40 palabras.");
        Assert.Equal("Revisa x 😀 <literal>. No alteres datos.",h.Composition!.BaseText);Assert.Equal(h.Composition.ForExternalEditor(),h.Text);Assert.Contains("ajustes del prompt > perfil seleccionado > instrucciones generales",h.Text);Assert.Equal(p.Revision,h.Composition.InstructionRevision);
        p.Text="Nueva instrucción";s.SaveInstruction(p);s.SaveSettings(new(GeneralInstructions:"Otro idioma"));Assert.Equal(h.Text,Store().History().Single().Text);Assert.Contains("Usa español.",h.Text);Assert.DoesNotContain("Nueva instrucción",h.Text);
    }
    [Fact] public void InvalidCompilationDoesNotCreateHistoryOrUsageAndPlainFlowIsUnchanged()
    {
        var s=Store();var t=s.Save(Prompt());Assert.Throws<ValidationException>(()=>s.Compile(t.Id,new(){{"cambio","x"}},Guid.NewGuid()));Assert.Empty(s.History());Assert.Equal(0,s.FindTemplate(t.Id)!.Uses);
        var h=s.Compile(t.Id,new(){{"cambio","x"}});Assert.Null(h.Composition);Assert.Equal("Revisa x. No alteres datos.",h.Text);
    }
    [Fact] public void SqlPagingAndCombinedFiltersReturnOnlyTheRequestedPageForAThousandTemplates()
    {
        var s=Store();using(var db=Open()){using var tx=db.BeginTransaction();for(int i=0;i<1000;i++){var t=Prompt("Revisión "+i.ToString("D4"));t.IsFavorite=i%2==0;using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT INTO Documents VALUES('template',$id,$payload)";c.Parameters.AddWithValue("$id",t.Id.ToString());c.Parameters.AddWithValue("$payload",JsonSerializer.Serialize(t,LocalStore.Json));c.ExecuteNonQuery();}tx.Commit();}
        var first=s.QueryTemplates(new(Search:"revision",Sort:"title"));var next=s.QueryTemplates(new(Search:"revision",Sort:"title",Page:1));Assert.Equal(1000,first.Total);Assert.Equal(12,first.Items.Count);Assert.Empty(first.Items.Select(t=>t.Id).Intersect(next.Items.Select(t=>t.Id)));Assert.Equal("Revisión 0012",next.Items[0].Title);
        var g=s.SaveGroup(new(){Name="Selección"});s.AssignGroup(g.Id,"template",first.Items.Select(t=>t.Id));Assert.Equal(6,s.QueryTemplates(new(Search:"revision",Favorites:true,GroupId:g.Id)).Total);Assert.Equal(988,s.QueryTemplates(new(Ungrouped:true)).Total);
    }
    [Fact] public void FullV2BackupRestoreAndAdditiveImportPreserveOrganizationAndAreIdempotent()
    {
        var s=Store();var t=s.Save(Prompt());var p=s.SaveInstruction(Profile());var g=s.SaveGroup(new(){Name="Trabajo",IsPinned=true,SortOrder=5});s.AssignGroup(g.Id,"template",[t.Id]);s.AssignGroup(g.Id,"instruction",[p.Id]);s.SaveCombination(new(Guid.NewGuid(),"Revisión segura",t.Id,p.Id,"No modifiques código.",DateTime.UtcNow));s.Compile(t.Id,new(){{"cambio","x"}},p.Id);
        var json=s.Export();Assert.Contains("\"schemaVersion\": 2",json);var target=Store("copy");Assert.Equal(1,target.Import(json));Assert.Equal(0,target.Import(json));Assert.Single(target.Groups());Assert.Single(target.GroupsFor("instruction",p.Id));Assert.Single(target.InstructionVersions(p.Id));Assert.Single(target.Combinations(t.Id));
        s.NovaDeleteGroup(g.Id,new(g.Revision,true));s.DeleteInstruction(p.Id,new(p.Revision,true));s.Import(json,true);Assert.Equal(json,s.Export());Assert.Equal("ok",s.CheckIntegrity());
    }
    [Fact] public void ImportMapsGroupsAndEquivalentEntitiesWithoutDuplicatingOrOverwriting()
    {
        var source=Store();var t=source.Save(Prompt());var p=source.SaveInstruction(Profile());var g=source.SaveGroup(new(){Name="Trabajo"});source.AssignGroup(g.Id,"template",[t.Id]);source.AssignGroup(g.Id,"instruction",[p.Id]);
        var target=Store("target");var keep=target.Save(Prompt());var keepProfile=target.SaveInstruction(Profile());var keepGroup=target.SaveGroup(new(){Name="TRABAJO",Description="Conservar"});Assert.Equal(0,target.Import(source.Export()));Assert.Single(target.Templates());Assert.Single(target.QueryInstructions(new()).Items);Assert.Equal("Conservar",target.Groups().Single().Group.Description);Assert.Equal(keepGroup.Id,target.GroupsFor("template",keep.Id).Single());Assert.Equal(keepGroup.Id,target.GroupsFor("instruction",keepProfile.Id).Single());
    }
    [Fact] public void OrphanMembershipOrCompositionMismatchCannotPartiallyImportOrRestore()
    {
        var s=Store();s.Save(Prompt());var before=s.Export();var archive=JsonSerializer.Deserialize<Archive>(before,LocalStore.Json)!;
        var bad=archive with{Memberships=[new(Guid.NewGuid(),"template",archive.Templates[0].Id)]};Assert.Throws<ValidationException>(()=>s.Import(JsonSerializer.Serialize(bad,LocalStore.Json),true));Assert.Equal(before,s.Export());
        var snapshot=new PromptComposition("Solicitud","Regla","","",null,null,null);var forged=new CompiledPrompt(Guid.NewGuid(),archive.Templates[0].Id,1,"Forged","Different preview",new Dictionary<string,string>(),DateTime.UtcNow,Composition:snapshot);
        Assert.Throws<ValidationException>(()=>s.Import(JsonSerializer.Serialize(archive with{History=[forged]},LocalStore.Json),true));Assert.Equal(before,s.Export());
    }
    [Fact] public void RestoreSqlFailureAfterOrganizationDeleteRollsBackEverything()
    {
        var s=Store();var t=s.Save(Prompt());var p=s.SaveInstruction(Profile());var g=s.SaveGroup(new(){Name="Conservar"});s.AssignGroup(g.Id,"template",[t.Id]);s.AssignGroup(g.Id,"instruction",[p.Id]);var before=s.Export();
        using(var db=Open()){using var c=db.CreateCommand();c.CommandText="CREATE TRIGGER fail_groups BEFORE INSERT ON PromptGroups BEGIN SELECT RAISE(ABORT,'injected restore failure');END;";c.ExecuteNonQuery();}
        Assert.Throws<SqliteException>(()=>s.Import(before,true));Assert.Equal(before,s.Export());Assert.Equal("ok",s.CheckIntegrity());
    }
    [Fact] public void OwnBackupOfHundredsOfTemplatesCanActuallyBeRestoredAboveTheOldFourMbLimit()
    {
        var s=Store();for(int i=0;i<450;i++)s.Save(new(){Title="Escala "+i,Template=new string('a',5000)});
        var backup=s.Export();Assert.True(System.Text.Encoding.UTF8.GetByteCount(backup)>4_000_000);
        var target=Store("large-copy");Assert.Equal(450,target.Import(backup,true));Assert.Equal(backup,target.Export());Assert.Equal("ok",target.CheckIntegrity());
    }
    public void Dispose(){if(Directory.Exists(folder))Directory.Delete(folder,true);}
}
