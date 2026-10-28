using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class CollectionPortTests
{
    [Fact]
    public async Task ReflectedReadsWorkWithSubstitutedReadOnlyPortsWithoutAStoreOrWriter()
    {
        var catalog = new ReadOnlyCatalog();
        var registry = new PreparationTools(4, 3, (_, _, _) => { });
        new PreparationReadTools(catalog, catalog).Register(registry);
        Assert.False(catalog is ICollectionWriter);
        Assert.False(catalog is IPromptWriter);
        var first = await registry.Call<PreparationReadTools.PromptIdArgs, PromptDocument>("get_prompt", new(catalog.Source.Id), ToolPermission.Read, default);
        Assert.Equal(1, first.Revision);
        catalog.Source = catalog.Source with { Revision = 2, TemplateText = "Compara <tema>." };
        var next = await registry.Call<PreparationReadTools.PromptIdArgs, PromptDocument>("get_prompt", new(catalog.Source.Id), ToolPermission.Read, default);
        Assert.Equal(2, next.Revision);
        var groups = await registry.Call<PreparationReadTools.EmptyArgs, GroupSummary[]>("list_groups", new(), ToolPermission.Read, default);
        Assert.Equal("Sólo lectura", Assert.Single(groups).Group.Name);
        Assert.All(registry.Contracts, contract => Assert.Equal(ToolPermission.Read, contract.Permission));
    }

    [Fact]
    public void CollectionPortsPreserveTransactionsRevisionsConfirmationAndPromptIdentity()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nova-collection-ports-" + Guid.NewGuid());
        try {
            var store = new LocalStore(Path.Combine(directory, "synthetic.sqlite"));
            var adapter = new NovaPromptStore(store); var library = new PromptLibrary(adapter, adapter);
            var ports = new NovaCollectionStore(store, library);
            ICollectionReader read = ports; ICollectionWriter write = ports;
            var source = library.Create(new("Origen", "Explica <tema>."));
            var group = write.Create(new("Inicial"));
            write.Assign(source.Id, new(group.Id, source.Revision, true));
            Assert.Equal(group.Id, Assert.Single(read.Memberships(source.Id)));
            Assert.Equal(1, Assert.Single(read.Groups()).Templates);
            var renamed = write.Rename(group.Id, new("Renombrada", group.Revision));
            var unchanged = store.NovaSnapshot();
            Assert.Throws<ConflictException>(() => write.Rename(group.Id, new("Obsoleta", group.Revision)));
            Assert.Throws<ValidationException>(() => write.Delete(group.Id, new(renamed.Revision, false)));
            Assert.Throws<ConflictException>(() => write.Delete(group.Id, new(group.Revision, true)));
            Assert.Equal(unchanged, store.NovaSnapshot());
            var updated = library.Update(source.Id, new(source.Title, "Compara <tema>.", Revision: source.Revision));
            Assert.Throws<ConflictException>(() => write.Assign(source.Id, new(group.Id, source.Revision, false)));
            Assert.Equal(group.Id, Assert.Single(read.Memberships(source.Id)));
            write.Delete(group.Id, new(renamed.Revision, true));
            Assert.Empty(read.Groups()); Assert.Empty(read.Memberships(source.Id));
            Assert.Equal(updated.Revision, library.Get(source.Id).Revision);
            Assert.Equal(updated.TemplateText, library.Get(source.Id).TemplateText);
            Assert.Throws<MissingPromptException>(() => read.Memberships(Guid.NewGuid()));
            Assert.Throws<MissingPromptException>(() => ((IPromptCatalog)library).Versions(Guid.NewGuid()));
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private class ReadOnlyCatalog : IPromptCatalog, ICollectionReader
    {
        public PromptDocument Source = new(Guid.NewGuid(), "Fuente", "", "Explica <tema>.", 0, false, false, 1, DateTime.UtcNow, DateTime.UtcNow, 0, null, ["tema"], new Dictionary<string, string>());
        public PromptDocument Get(Guid id) => id == Source.Id ? Source : throw new MissingPromptException();
        public CatalogPage<PromptDocument> Search(CatalogQuery query) => new([Source], 1);
        public TemplateVersion[] Versions(Guid id) => [];
        public GroupSummary[] Groups() => [new(new PromptGroup { Name = "Sólo lectura" }, 0, 0)];
        public Guid[] Memberships(Guid promptId) => [];
    }
}
