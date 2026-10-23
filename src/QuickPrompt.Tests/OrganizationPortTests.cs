using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Web.Tests;

public sealed class OrganizationPortTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "organization-ports-" + Guid.NewGuid());
    private LocalPromptLibrary Module() => new(Path.Combine(directory, "synthetic.sqlite"));
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [Fact]
    public void ObsoleteUnguardedDeletionCannotBeDiscoveredOnTheConcreteOwner()
    {
        Assert.Null(typeof(LocalStore).GetMethod("DeleteGroup", [typeof(Guid)]));
        Assert.Null(typeof(LocalStore).GetMethod("DeleteInstruction", [typeof(Guid)]));
    }

    [Fact]
    public void CatalogsShareOwnersAndMembershipIdentityIncludesKindWithoutDuplicatingEntities()
    {
        var module = Module(); Assert.Same(module.Store, module.GroupAuthoring); Assert.Same(module.Store, module.Memberships); Assert.Same(module.Store, module.Instructions); Assert.Same(module.Store, module.InstructionWriter); Assert.Same(module.Store, module.Combinations);
        var template = module.Authoring.Save(new() { Title = "Fuente", Template = "Hola <nombre>" });
        var profile = module.InstructionWriter.SaveInstruction(new() { Id = template.Id, Name = "Perfil", Text = "Sé breve." });
        var group = module.GroupAuthoring.SaveGroup(new() { Name = "Trabajo", Description = "Conservada", Icon = "braces", Color = "teal", IsPinned = true });
        module.Memberships.AssignGroup(group.Id, "template", [template.Id]);
        Assert.Equal([group.Id], module.Memberships.GroupsFor("template", template.Id)); Assert.Empty(module.Memberships.GroupsFor("instruction", profile.Id));
        module.Memberships.AssignGroup(group.Id, "instruction", [profile.Id]); module.Memberships.AssignGroup(group.Id, "instruction", [profile.Id]);
        var summary = Assert.Single(module.CollectionReader.Groups()); Assert.Equal(1, summary.Templates); Assert.Equal(1, summary.Instructions); Assert.Equal(group.Description, summary.Group.Description);
        Assert.Single(module.Templates.Search(new()).Items); Assert.Single(module.Instructions.QueryInstructions(new()).Items);
        module.Memberships.AssignGroup(group.Id, "template", [template.Id], false); Assert.Empty(module.Memberships.GroupsFor("template", template.Id)); Assert.Equal([group.Id], module.Memberships.GroupsFor("instruction", profile.Id));
    }

    [Fact]
    public void CreateAndAssignRollsBackWholeGroupAndMembershipBatchOnInvalidReferences()
    {
        var module = Module(); var source = module.Authoring.Save(new() { Title = "Fuente", Template = "Texto." }); var before = module.Archives.Export();
        Assert.Throws<ValidationException>(() => module.Memberships.CreateGroupAndAssign(new() { Name = "Sin origen" }, "template", Guid.NewGuid()));
        Assert.Throws<ValidationException>(() => module.Memberships.CreateGroupAndAssign(new() { Name = "Tipo inválido" }, "unsupported", source.Id));
        Assert.Equal(before, module.Archives.Export()); Assert.Empty(module.CollectionReader.Groups());
        var group = module.Memberships.CreateGroupAndAssign(new() { Name = "Válido" }, "template", source.Id); var created = module.Archives.Export();
        Assert.Throws<ValidationException>(() => module.Memberships.AssignGroup(group.Id, "template", [source.Id, Guid.NewGuid()]));
        Assert.Equal(created, module.Archives.Export()); Assert.Equal([group.Id], module.Memberships.GroupsFor("template", source.Id));
    }

    [Fact]
    public void ProtectedGroupDeletePreservesProfilesTemplatesAndCompiledHistory()
    {
        var module = Module(); var source = module.Authoring.Save(new() { Title = "Fuente", Template = "Texto." }); var profile = module.InstructionWriter.SaveInstruction(new() { Name = "Perfil", Text = "Breve." });
        var compiled = module.Compilation.Compile(source.Id, [], profile.Id, expectedRevision: source.Revision);
        var group = module.Memberships.CreateGroupAndAssign(new() { Name = "Trabajo" }, "template", source.Id); module.Memberships.AssignGroup(group.Id, "instruction", [profile.Id]); var oldRevision = group.Revision;
        group.Name = "Renombrado"; module.GroupAuthoring.SaveGroup(group); var before = module.Archives.Export();
        Assert.Throws<ConflictException>(() => module.CollectionWriter.Delete(group.Id, new(oldRevision, true)));
        Assert.Throws<ValidationException>(() => module.CollectionWriter.Delete(group.Id, new(group.Revision, false))); Assert.Equal(before, module.Archives.Export());
        module.CollectionWriter.Delete(group.Id, new(group.Revision, true)); Assert.Empty(module.CollectionReader.Groups()); Assert.Empty(module.Memberships.GroupsFor("template", source.Id)); Assert.Empty(module.Memberships.GroupsFor("instruction", profile.Id));
        Assert.NotNull(module.Templates.Find(source.Id)); Assert.NotNull(module.Instructions.FindInstruction(profile.Id)); Assert.Equal(compiled.Text, Assert.Single(module.Compiled.History()).Text);
    }

    [Fact]
    public void ProfileDeleteChecksRevisionAndConfirmationAndPreservesCompiledSnapshot()
    {
        var module = Module(); var source = module.Authoring.Save(new() { Title = "Fuente", Template = "Texto." }); var profile = module.InstructionWriter.SaveInstruction(new() { Name = "Perfil", Text = "Versión uno." });
        var compiled = module.Compilation.Compile(source.Id, [], profile.Id, expectedRevision: source.Revision);
        var combination = module.Combinations.SaveCombination(new(Guid.NewGuid(), "Reutilizable", source.Id, profile.Id, "Ajustes.", DateTime.UtcNow));
        var group = module.Memberships.CreateGroupAndAssign(new() { Name = "Perfiles" }, "instruction", profile.Id); var oldRevision = profile.Revision;
        profile.Text = "Versión dos."; module.InstructionWriter.SaveInstruction(profile); Assert.Equal(2, module.Instructions.InstructionVersions(profile.Id).Count);
        var before = module.Archives.Export();
        Assert.Throws<ConflictException>(() => module.InstructionWriter.DeleteInstruction(profile.Id, new(oldRevision, true)));
        Assert.Throws<ValidationException>(() => module.InstructionWriter.DeleteInstruction(profile.Id, new(profile.Revision, false))); Assert.Equal(before, module.Archives.Export());
        module.InstructionWriter.DeleteInstruction(profile.Id, new(profile.Revision, true));
        Assert.Null(module.Instructions.FindInstruction(profile.Id)); Assert.Empty(module.Instructions.InstructionVersions(profile.Id)); Assert.Empty(module.Combinations.Combinations(source.Id)); Assert.Empty(module.Memberships.GroupsFor("instruction", profile.Id));
        Assert.Equal(compiled.Text, Assert.Single(module.Compiled.History()).Text); Assert.Equal("Versión uno.", Assert.Single(module.Compiled.History()).Composition!.Profile); Assert.NotNull(module.Templates.Find(source.Id)); Assert.Single(module.CollectionReader.Groups());
    }
}
