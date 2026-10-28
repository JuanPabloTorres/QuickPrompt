using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class CollectionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static LibraryActor Actor(Guid id) => LibraryActor.FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "fixture")));

    [Fact]
    public async Task AssignmentWaitsForTheSourceEditorAndRejectsItsNowStaleRevision()
    {
        await using var scope = postgres.Services.CreateAsyncScope(); var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var a = Actor(await postgres.CreateUser()); var library = await access.CreateLibrary(a, "Revisión en carrera");
        var prompt = await access.SavePrompt(a, library.Id, null, 0, "V1", "Analiza <tema>.");
        var group = await access.SaveCollection(a, library.Id, null, new("Ideas"));
        var settings = new NpgsqlConnectionStringBuilder(postgres.ApplicationConnection) { MaxPoolSize = 4, ApplicationName = "qa-collection-revision-" + Guid.NewGuid().ToString("N") };
        await using var pool = NpgsqlDataSource.Create(settings.ConnectionString);
        await using var writer = await postgres.Application.OpenConnectionAsync(); await using var transaction = await writer.BeginTransactionAsync();
        await using (var actor = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,true)", writer))
        { actor.Parameters.AddWithValue("actor", a.Id.ToString()); await actor.ExecuteNonQueryAsync(); }
        await using (var edit = new NpgsqlCommand("""
            UPDATE qp_library.prompts SET "Title"='V2',"Revision"=2,"UpdatedAt"=clock_timestamp() WHERE "Id"=@id;
            INSERT INTO qp_library.prompt_versions ("LibraryId","PromptId","Revision","Title","Template","CreatedAt")
              SELECT "LibraryId","Id","Revision","Title","Template","UpdatedAt" FROM qp_library.prompts WHERE "Id"=@id;
            """, writer)) { edit.Parameters.AddWithValue("id", prompt.Id); await edit.ExecuteNonQueryAsync(); }
        await using var database = new AccountDatabase(new DbContextOptionsBuilder<AccountDatabase>().UseNpgsql(pool).Options);
        var pending = new LibraryAccess(database).AssignCollection(a, library.Id, prompt.Id, new(group.Id, 1, true));
        await using (var probe = await pool.OpenConnectionAsync())
        {
            await using var wait = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE application_name=@name AND wait_event_type='Lock' AND query LIKE '%FOR SHARE%')", probe);
            wait.Parameters.AddWithValue("name", settings.ApplicationName!); var observed = false;
            for (var attempt = 0; attempt < 100 && !observed; attempt++) { observed = await wait.ExecuteScalarAsync() is true; if (!observed) await Task.Delay(25); }
            Assert.True(observed, "Assignment must really wait for the protected source, not merely for test timing.");
        }
        await transaction.CommitAsync(); await writer.CloseAsync();
        await Assert.ThrowsAsync<LibraryRevisionException>(() => pending);
        Assert.Empty(await access.Memberships(a, library.Id, prompt.Id)); Assert.Equal(1, Assert.Single(await access.ListCollections(a, library.Id)).Group.Revision);
        Assert.Equal(2, (await access.GetPrompt(a, library.Id, prompt.Id)).Revision);
    }

    [Fact]
    public async Task CollectionsAreLibraryScopedAndBothCatalogConsumersRejectForeignFilters()
    {
        await using var scope = postgres.Services.CreateAsyncScope(); var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        var library = await access.CreateLibrary(a, "Mismo nombre"); var sibling = await access.CreateLibrary(a, "Otra propia"); var foreign = await access.CreateLibrary(b, "Mismo nombre");
        var prompt = await access.SavePrompt(a, library.Id, null, 0, "Privado A", "Analiza <tema>.");
        var unrelated = await access.SavePrompt(a, library.Id, null, 0, "Sin colección", "Otro <tema>.");
        var otherPrompt = await access.SavePrompt(b, foreign.Id, null, 0, "Privado B", "No revelar.");
        var group = await access.SaveCollection(a, library.Id, null, new("Ideas")); var other = await access.SaveCollection(b, foreign.Id, null, new("Ideas"));
        var siblingGroup = await access.SaveCollection(a, sibling.Id, null, new("Ideas"));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.AssignCollection(a, library.Id, prompt.Id, new(other.Id, 1, true)));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.AssignCollection(a, library.Id, prompt.Id, new(siblingGroup.Id, 1, true)));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.AssignCollection(a, library.Id, otherPrompt.Id, new(group.Id, 1, true)));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.ListCollections(b, library.Id));
        Assert.True(await access.AssignCollection(a, library.Id, prompt.Id, new(group.Id, 1, true)));
        Assert.False(await access.AssignCollection(a, library.Id, prompt.Id, new(group.Id, 1, true)));
        Assert.Equal(group.Id, Assert.Single(await access.Memberships(a, library.Id, prompt.Id)));
        var listed = Assert.Single(await access.ListCollections(a, library.Id)); Assert.Equal(1, listed.Templates); Assert.Equal(2, listed.Group.Revision);
        var memory = new AccountAgentMemory(postgres.Application);
        Assert.Equal(group.Id, Assert.Single(memory.Collections(a, library.Id).Groups()).Group.Id);
        Assert.Equal(group.Id, Assert.Single(memory.Collections(a, library.Id).Memberships(prompt.Id)));
        Assert.Equal(prompt.Id, Assert.Single((await access.ListPrompts(a, library.Id, group.Id, false))).Id);
        Assert.Equal(unrelated.Id, Assert.Single((await access.ListPrompts(a, library.Id, null, true))).Id);
        Assert.Equal(prompt.Id, Assert.Single(memory.Catalog(a, library.Id).Search(new(GroupId: group.Id)).Items).Id);
        Assert.Equal(unrelated.Id, Assert.Single(memory.Catalog(a, library.Id).Search(new(Ungrouped: true)).Items).Id);
        Assert.Throws<LibraryUnavailableException>(() => memory.Catalog(a, library.Id).Search(new(GroupId: other.Id)));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.ListPrompts(a, library.Id, siblingGroup.Id, false));
        Assert.Throws<ValidationException>(() => memory.Catalog(a, library.Id).Search(new(GroupId: group.Id, Ungrouped: true)));
        Assert.Single(await access.ListCollections(b, foreign.Id)); Assert.Empty(await access.Memberships(b, foreign.Id, otherPrompt.Id));

        await Assert.ThrowsAsync<ConflictException>(() => access.SaveCollection(a, library.Id, group.Id, new("Obsoleta", 1)));
        var renamed = await access.SaveCollection(a, library.Id, group.Id, new("Planificación", 2)); Assert.Equal(3, renamed.Revision);
        await Assert.ThrowsAsync<ValidationException>(() => access.DeleteCollection(a, library.Id, group.Id, new(3, false)));
        await Assert.ThrowsAsync<ConflictException>(() => access.DeleteCollection(a, library.Id, group.Id, new(2, true)));
        await access.DeleteCollection(a, library.Id, group.Id, new(3, true));
        Assert.Empty(await access.Memberships(a, library.Id, prompt.Id)); Assert.Empty(await access.ListCollections(a, library.Id));
        Assert.Equal(prompt, await access.GetPrompt(a, library.Id, prompt.Id)); Assert.Single(await access.ListVersions(a, library.Id, prompt.Id));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.ListPrompts(a, library.Id, group.Id, false));
    }

    [Fact]
    public async Task DirectSqlCannotReadOtherOwnersRetargetCollectionsOrMixResourcesAcrossLibraries()
    {
        await using var scope = postgres.Services.CreateAsyncScope(); var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        var library = await access.CreateLibrary(a, "A"); var sibling = await access.CreateLibrary(a, "A2"); var foreign = await access.CreateLibrary(b, "B");
        var group = await access.SaveCollection(a, library.Id, null, new("Privada"));
        var prompt = await access.SavePrompt(a, library.Id, null, 0, "Propio", "Texto privado.");
        var other = await access.SavePrompt(a, sibling.Id, null, 0, "Otro", "No mezclar.");
        await access.AssignCollection(a, library.Id, prompt.Id, new(group.Id, 1, true));
        await using var connection = await postgres.Application.OpenConnectionAsync();
        await using var read = new NpgsqlCommand("SELECT (SELECT count(*) FROM qp_library.collections WHERE \"Id\"=@group)+(SELECT count(*) FROM qp_library.collection_members WHERE \"CollectionId\"=@group)", connection);
        read.Parameters.AddWithValue("group", group.Id); Assert.Equal(0L, await read.ExecuteScalarAsync());
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using var actor = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,true)", connection); actor.Parameters.AddWithValue("actor", b.Id.ToString()); await actor.ExecuteNonQueryAsync();
            Assert.Equal(0L, await read.ExecuteScalarAsync());
            await using var remove = new NpgsqlCommand("DELETE FROM qp_library.collections WHERE \"Id\"=@id", connection); remove.Parameters.AddWithValue("id", group.Id);
            Assert.Equal(0, await remove.ExecuteNonQueryAsync()); await transaction.RollbackAsync();
        }
        foreach (var sql in new[] { "UPDATE qp_library.collections SET \"OwnerId\"=gen_random_uuid()", "UPDATE qp_library.collections SET \"LibraryId\"=gen_random_uuid()", "UPDATE qp_library.collection_members SET \"PromptId\"=gen_random_uuid()" })
        { await using var denied = new NpgsqlCommand(sql, connection); Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync())).SqlState); }
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using var actor = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,true)", connection); actor.Parameters.AddWithValue("actor", a.Id.ToString()); await actor.ExecuteNonQueryAsync();
            await using var mix = new NpgsqlCommand("INSERT INTO qp_library.collection_members (\"LibraryId\",\"CollectionId\",\"OwnerId\",\"PromptId\") VALUES (@library,@group,@actor,@prompt)", connection);
            mix.Parameters.AddWithValue("library", library.Id); mix.Parameters.AddWithValue("group", group.Id); mix.Parameters.AddWithValue("actor", a.Id); mix.Parameters.AddWithValue("prompt", other.Id);
            Assert.Equal("23503", (await Assert.ThrowsAsync<PostgresException>(() => mix.ExecuteNonQueryAsync())).SqlState); await transaction.RollbackAsync();
        }
    }
}
