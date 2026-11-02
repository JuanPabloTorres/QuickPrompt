using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Accounts.Sharing;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class AccountPromptPortTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task PortableExportPreservesPublicDefinitionButCannotExportDefaultsOrPrivateIdentity()
    {
        var actor = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var own = await access.CreateLibrary(actor, "Exportación privada");
        var memory = new AccountAgentMemory(postgres.Application);
        var prompt = memory.Library(actor, own.Id).Create(new("Plantilla", "Explica <tema>.", "Descripción exportable", (int)PromptCategory.Education,
            Defaults: new() { ["tema"] = "PRIVATE_DEFAULT_NOT_EXPORTABLE" }));
        var exported = await access.ExportPrompt(actor, own.Id, prompt.Id, prompt.Revision);
        Assert.Equal(prompt.Title, exported.Prompt.Title); Assert.Equal(prompt.TemplateText, exported.Prompt.Template);
        Assert.Equal(prompt.Description, exported.Prompt.Description); Assert.Equal(PromptCategory.Education, exported.Prompt.Category);
        Assert.DoesNotContain("PRIVATE_DEFAULT_NOT_EXPORTABLE", JsonSerializer.Serialize(exported));
        Assert.Empty(exported.ToTemplate().Variables);
        Assert.Single((await access.ListVersions(actor, own.Id, prompt.Id)));
    }

    private static LibraryActor Actor(Guid id) => LibraryActor.FromPrincipal(new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "fixture")));

    [Fact]
    public async Task PrivateCatalogRecallFindsOlderAccentedDefinitionsWithoutFallingBackToTheRecentPage()
    {
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope(); var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var own = await access.CreateLibrary(a, "Recall A"); var other = await access.CreateLibrary(b, "Recall B");
        var memory = new AccountAgentMemory(postgres.Application); var library = memory.Library(a, own.Id);
        var target = library.Create(new("Diseño de software", "Explica <tema>.", "Propósito y evaluación."));
        var foreign = memory.Library(b, other.Id).Create(new("Diseno de software privado B", "Contenido ajeno."));
        for (var index = 0; index < 20; index++) library.Create(new("Nota reciente " + index, "Contenido diferente."));
        var retrieved = memory.Retriever(a, own.Id).Retrieve("diseño", null);
        Assert.Contains(retrieved, candidate => candidate.Prompt.Id == target.Id && candidate.Score >= 4);
        Assert.DoesNotContain(retrieved, candidate => candidate.Prompt.Id == foreign.Id); Assert.True(retrieved.Length <= 6);
        Assert.Contains(memory.Catalog(a, own.Id).Search(new(Search: "proposito")).Items, prompt => prompt.Id == target.Id);
    }

    [Fact]
    public async Task ApplicationLibraryPreservesDefinitionAndVersionsAcrossAdaptersAndRejectsForeignAndStaleWrites()
    {
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope(); var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var own = await access.CreateLibrary(a, "Puerto A"); var other = await access.CreateLibrary(a, "Otro A"); var foreign = await access.CreateLibrary(b, "Puerto B");
        var memory = new AccountAgentMemory(postgres.Application); var library = memory.Library(a, own.Id);
        var source = library.Create(new("Definición completa", "Explica <tema>.", "Descripción privada", (int)PromptCategory.Education,
            Defaults: new() { ["tema"] = "Dato privado sintético" }));
        var restarted = memory.Library(a, own.Id).Get(source.Id);
        Assert.Equal(source.Description, restarted.Description); Assert.Equal(source.CategoryId, restarted.CategoryId);
        Assert.Equal(source.Defaults, restarted.Defaults); Assert.Equal(new[] { "tema" }, restarted.VariableNames);
        Assert.Equal("Explica Dato privado sintético.", library.Compile(source.Id, new(1, new())).Text);
        Assert.Throws<ValidationException>(() => library.Compile(source.Id, new(1, new() { ["tema"] = "" })));
        Assert.Null(library.Render(source.Id, new(1, new())).Text); // Explicit rendering never consumes defaults silently.
        Assert.Throws<LibraryUnavailableException>(() => memory.Library(b, own.Id).Get(source.Id));
        Assert.Throws<MissingPromptException>(() => memory.Library(b, foreign.Id).Get(source.Id));
        Assert.Throws<MissingPromptException>(() => memory.Library(a, other.Id).Update(source.Id, new("Intrusión", "Otro", Revision: 1)));
        var revised = library.Update(source.Id, new("Revisión", "Describe <tema> en detalle.", "Descripción revisada", (int)PromptCategory.Technology,
            Revision: 1, Defaults: new() { ["tema"] = "Default revisado" }));
        Assert.Equal(2, revised.Revision);
        Assert.Throws<LibraryRevisionException>(() => library.Update(source.Id, new("Stale", "Texto", Revision: 1)));
        var versions = library.Versions(source.Id); Assert.Equal(new[] { 2, 1 }, versions.Select(x => x.Revision));
        Assert.Equal("Dato privado sintético", versions[1].Snapshot.Variables["tema"]);
        Assert.Equal("Descripción privada", versions[1].Snapshot.Description);
        var restored = library.RestoreVersion(source.Id, 1, new(2, true));
        Assert.Equal(3, restored.Revision); Assert.Equal(source.TemplateText, restored.TemplateText);
        Assert.Equal(source.Defaults, restored.Defaults); Assert.Equal(source.Description, restored.Description);
        var manual = await access.SavePrompt(a, own.Id, source.Id, 3, "Editor manual", "Explica <otro>.");
        var afterManual = library.Get(source.Id);
        Assert.Equal(4, manual.Revision); Assert.Equal(source.Description, afterManual.Description); Assert.Equal(source.CategoryId, afterManual.CategoryId);
        Assert.Empty(afterManual.Defaults); Assert.Equal(4, library.Versions(source.Id).Length);
    }

    [Fact]
    public async Task PrivateFavoritesArchiveSearchAndUseReceiptsKeepIdentityRevisionAndExplicitValues()
    {
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope(); var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var own = await access.CreateLibrary(a, "Uso A"); var foreign = await access.CreateLibrary(b, "Uso B");
        var memory = new AccountAgentMemory(postgres.Application); var library = memory.Library(a, own.Id);
        var source = library.Create(new("SQL 100%_literal", "Explica <tema>.", "Descripción", (int)PromptCategory.Programming, Defaults: new() { ["tema"] = "default privado" }));
        library.Create(new("SQL otro", "Otro texto."));
        var favorite = library.Favorite(source.Id, 1);
        Assert.True(favorite.IsFavorite); Assert.Equal(2, favorite.Revision);
        Assert.Equal(source.Id, Assert.Single(library.Search(new(Search: "100%_LITERAL")).Items).Id);
        Assert.Equal(source.Id, Assert.Single(memory.Catalog(a, own.Id).Search(new(Favorites: true, Category: ((int)PromptCategory.Programming).ToString())).Items).Id);
        var request = new UseInput(2, new() { ["tema"] = "valor explícito" }, Guid.NewGuid(), "Explica valor explícito.");
        library.RecordUse(source.Id, request); library.RecordUse(source.Id, request);
        var used = library.Get(source.Id); Assert.Equal(1, used.Uses); Assert.Equal(2, used.Revision); Assert.NotNull(used.LastUsedAt);
        Assert.Equal(source.Id, Assert.Single(library.Search(new(Recent: true, Sort: "recent")).Items).Id);
        Assert.Throws<ConflictException>(() => library.RecordUse(source.Id, request with { ExpectedText = "Otro" }));
        Assert.Throws<LibraryUnavailableException>(() => memory.Library(b, own.Id).RecordUse(source.Id, request));
        Assert.Throws<LibraryUnavailableException>(() => memory.Library(b, foreign.Id).RecordUse(source.Id, request));
        var archived = library.Archive(source.Id, 2, true); Assert.True(archived.IsArchived);
        Assert.DoesNotContain(library.Search(new()).Items, x => x.Id == source.Id);
        Assert.Equal(source.Id, Assert.Single(library.Search(new(Active: false)).Items).Id);
        Assert.DoesNotContain(await access.ListPrompts(a, own.Id), x => x.Id == source.Id);
        library.RecordUse(source.Id, request); Assert.Equal(1, library.Get(source.Id).Uses); // Receipt, not another effect.
        var active = library.Archive(source.Id, 3, false); Assert.False(active.IsArchived); Assert.Equal(4, active.Revision);
        Assert.Equal(1, active.Uses); Assert.Equal("default privado", active.Defaults["tema"]);
        await using var db = await postgres.Application.OpenConnectionAsync(); await using var tx = await db.BeginTransactionAsync();
        await using (var identity = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,true)", db, tx))
        { identity.Parameters.AddWithValue("actor", b.Id.ToString()); await identity.ExecuteScalarAsync(); }
        await using var receipts = new NpgsqlCommand("SELECT count(*) FROM qp_library.prompt_uses", db, tx);
        Assert.Equal(0L, await receipts.ExecuteScalarAsync());
    }

    [Fact]
    public async Task PublicationsAndImportsDoNotCopyPrivateDefaultsOrSourceMetadata()
    {
        var a = Actor(await postgres.CreateUser(confirmed: true)); var b = Actor(await postgres.CreateUser(confirmed: true));
        await using var scope = postgres.Services.CreateAsyncScope(); var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var sharing = scope.ServiceProvider.GetRequiredService<SharingAccess>(); var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var own = await access.CreateLibrary(a, "Origen"); var target = await access.CreateLibrary(b, "Destino");
        var memory = new AccountAgentMemory(postgres.Application);
        const string privateValue = "DEFAULT_PRIVADO_QA_NO_PUBLICAR";
        var source = memory.Library(a, own.Id).Create(new("Instrucción", "Analiza <tema>.", "Descripción privada", (int)PromptCategory.BusinessAndStartups,
            Defaults: new() { ["tema"] = privateValue }));
        var published = await sharing.Publish(a, own.Id, source.Id, source.Revision);
        Assert.DoesNotContain(privateValue, JsonSerializer.Serialize(published));
        Assert.DoesNotContain(source.Description, JsonSerializer.Serialize(published));
        await sharing.Invite(a, published.Id, (await users.FindByIdAsync(b.Id.ToString()))!.Email!);
        var invitation = Assert.Single(await sharing.ListInvitations(b)); await sharing.Decide(b, invitation.Id, InvitationState.Accepted);
        var copied = await sharing.Import(b, published.Id, target.Id, Guid.NewGuid());
        var imported = memory.Library(b, target.Id).Get(copied.Id);
        Assert.Equal(source.TemplateText, imported.TemplateText); Assert.Empty(imported.Defaults);
        Assert.Equal("", imported.Description); Assert.Equal(0, imported.CategoryId);
        Assert.Equal(privateValue, memory.Library(a, own.Id).Get(source.Id).Defaults["tema"]);
    }
}
