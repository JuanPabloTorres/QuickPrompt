using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Accounts.Sharing;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class CollectionSharingTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static LibraryActor Actor(Guid id) => LibraryActor.FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "fixture")));

    [Fact]
    public async Task CollectionSnapshotImportsAtomicallyAndRemainsIndependentWhileRevocationDeniesAllFutureAccess()
    {
        await using var scope = postgres.Services.CreateAsyncScope(); var library = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var sharing = scope.ServiceProvider.GetRequiredService<SharingAccess>(); var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var a = Actor(await postgres.CreateUser(true)); var b = Actor(await postgres.CreateUser(true)); var c = Actor(await postgres.CreateUser(true));
        var origin = await library.CreateLibrary(a, "Origen"); var destination = await library.CreateLibrary(b, "Destino"); var sibling = await library.CreateLibrary(b, "Otro destino");
        var one = await library.SavePrompt(a, origin.Id, null, 0, "Uno", "Analiza <tema>.");
        var two = await library.SavePrompt(a, origin.Id, null, 0, "Dos", "Resume <texto> sin inventar.");
        var group = await library.SaveCollection(a, origin.Id, null, new("Colección revisada"));
        await library.AssignCollection(a, origin.Id, one.Id, new(group.Id, 1, true)); await library.AssignCollection(a, origin.Id, two.Id, new(group.Id, 1, true));
        var input = new CollectionPublicationInput(origin.Id, group.Id, 3, new() { [one.Id] = 1, [two.Id] = 1 });
        await Assert.ThrowsAsync<ConflictException>(() => sharing.PublishCollection(a, input with { PromptRevisions = new() { [one.Id] = 1 } }));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.PublishCollection(b, input));
        var published = await sharing.PublishCollection(a, input); Assert.Equal(2, published.Items.Length);
        var json = JsonSerializer.Serialize(published);
        foreach (var secretId in new[] { a.Id, origin.Id, group.Id, one.Id, two.Id }) Assert.DoesNotContain(secretId.ToString(), json);
        await library.SavePrompt(a, origin.Id, one.Id, 1, "Privado posterior", "Dato privado posterior.");
        await Assert.ThrowsAsync<LibraryRevisionException>(() => sharing.PublishCollection(a, input));
        Assert.True(await sharing.Invite(a, published.Id, (await users.FindByIdAsync(b.Id.ToString()))!.Email!));
        var invite = Assert.Single(await sharing.ListInvitations(b)); Assert.True(invite.IsCollection); Assert.False(invite.Available);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.ReadCollection(b, published.Id));
        await sharing.Decide(b, invite.Id, InvitationState.Accepted);
        Assert.Equal(json, JsonSerializer.Serialize(await sharing.ReadCollection(b, published.Id)));
        Assert.Equal(published.Id, Assert.Single(await sharing.ListReceivedCollections(b, "", 0, 10)).Id); Assert.Empty(await sharing.ListReceived(b, "", 0, 10));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.Read(b, published.Id));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.Import(b, published.Id, destination.Id, Guid.NewGuid()));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.ReadCollection(c, published.Id));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.ImportCollection(b, published.Id, origin.Id, Guid.NewGuid()));
        var operation = Guid.NewGuid(); var copy = await sharing.ImportCollection(b, published.Id, destination.Id, operation);
        Assert.Equal(2, copy.Prompts.Length); Assert.Equal(3, copy.Revision);
        Assert.Equal(copy.Id, Assert.Single(await library.ListCollections(b, destination.Id)).Group.Id);
        Assert.Equal(2, (await library.ListPrompts(b, destination.Id, copy.Id, false)).Length);
        Assert.DoesNotContain(copy.Prompts, prompt => prompt.Id == one.Id || prompt.Id == two.Id);
        var first = copy.Prompts[0]; await library.SavePrompt(b, destination.Id, first.Id, 1, "Copia V2", "Sólo en B.");
        var replay = await sharing.ImportCollection(b, published.Id, destination.Id, operation);
        Assert.Equal(JsonSerializer.Serialize(copy), JsonSerializer.Serialize(replay));
        await Assert.ThrowsAsync<ShareConflictException>(() => sharing.ImportCollection(b, published.Id, sibling.Id, operation));
        var solo = await sharing.Publish(a, origin.Id, two.Id, 1);
        await sharing.Invite(a, solo.Id, (await users.FindByIdAsync(b.Id.ToString()))!.Email!);
        await sharing.Decide(b, (await sharing.ListInvitations(b)).Single(item => item.PublicationId == solo.Id).Id, InvitationState.Accepted);
        await Assert.ThrowsAsync<ShareConflictException>(() => sharing.Import(b, solo.Id, destination.Id, operation));
        await library.DeleteCollection(a, origin.Id, group.Id, new(3, true));
        Assert.Equal(json, JsonSerializer.Serialize(await sharing.ReadCollection(b, published.Id)));
        await sharing.Revoke(a, published.Id);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.ReadCollection(b, published.Id));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.ImportCollection(b, published.Id, destination.Id, operation));
        Assert.Empty(await sharing.ListReceivedCollections(b, "", 0, 10));
        Assert.Equal("Sólo en B.", (await library.GetPrompt(b, destination.Id, first.Id)).Template);
        Assert.Equal(2, (await library.ListPrompts(b, destination.Id, copy.Id, false)).Length);
        await using var connection = await postgres.Application.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
        await using (var actor = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,true)", connection))
        { actor.Parameters.AddWithValue("actor", b.Id.ToString()); await actor.ExecuteNonQueryAsync(); }
        await using var hidden = new NpgsqlCommand("SELECT count(*) FROM qp_library.publications WHERE \"Id\"=@id", connection); hidden.Parameters.AddWithValue("id", published.Id);
        Assert.Equal(0L, await hidden.ExecuteScalarAsync()); await transaction.RollbackAsync();
    }

    [Fact]
    public async Task ReceiptStorageFailureRollsBackTheWholeCollectionAndItsPromptVersions()
    {
        await using var scope = postgres.Services.CreateAsyncScope(); var library = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var sharing = scope.ServiceProvider.GetRequiredService<SharingAccess>(); var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var a = Actor(await postgres.CreateUser(true)); var b = Actor(await postgres.CreateUser(true));
        var origin = await library.CreateLibrary(a, "Origen"); var target = await library.CreateLibrary(b, "Destino vacío");
        var source = await library.SavePrompt(a, origin.Id, null, 0, "Original", "Conserva <dato>.");
        var group = await library.SaveCollection(a, origin.Id, null, new("Ideas")); await library.AssignCollection(a, origin.Id, source.Id, new(group.Id, 1, true));
        var publication = await sharing.PublishCollection(a, new(origin.Id, group.Id, 2, new() { [source.Id] = 1 }));
        await sharing.Invite(a, publication.Id, (await users.FindByIdAsync(b.Id.ToString()))!.Email!);
        await sharing.Decide(b, Assert.Single(await sharing.ListInvitations(b)).Id, InvitationState.Accepted);
        var trigger = "qa_collection_import_" + Guid.NewGuid().ToString("N");
        await using var migration = new NpgsqlConnection(postgres.MigrationConnection); await migration.OpenAsync();
        await using (var install = new NpgsqlCommand($"""
            CREATE FUNCTION qp_library.{trigger}() RETURNS trigger LANGUAGE plpgsql AS $fault$
            BEGIN IF NEW."PublicationId"='{publication.Id}'::uuid THEN RAISE EXCEPTION 'synthetic collection import storage fault'; END IF; RETURN NEW; END $fault$;
            CREATE TRIGGER {trigger} BEFORE INSERT ON qp_library.collection_imports FOR EACH ROW EXECUTE FUNCTION qp_library.{trigger}();
            """, migration)) await install.ExecuteNonQueryAsync();
        var operation = Guid.NewGuid();
        try
        {
            await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => sharing.ImportCollection(b, publication.Id, target.Id, operation));
            Assert.Empty(await library.ListPrompts(b, target.Id)); Assert.Empty(await library.ListCollections(b, target.Id));
        }
        finally
        {
            await using var remove = new NpgsqlCommand($"DROP TRIGGER {trigger} ON qp_library.collection_imports; DROP FUNCTION qp_library.{trigger}();", migration); await remove.ExecuteNonQueryAsync();
        }
        var copy = await sharing.ImportCollection(b, publication.Id, target.Id, operation); Assert.Single(copy.Prompts);
        await using var connection = await postgres.Application.OpenConnectionAsync();
        await using var none = new NpgsqlCommand("SELECT count(*) FROM qp_library.collection_imports WHERE \"CollectionId\"=@id", connection); none.Parameters.AddWithValue("id", copy.Id);
        Assert.Equal(0L, await none.ExecuteScalarAsync());
        foreach (var sql in new[] { "UPDATE qp_library.collection_imports SET \"LibraryId\"=gen_random_uuid()", "DELETE FROM qp_library.collection_imports", "UPDATE qp_library.publications SET \"CollectionSnapshot\"='[]'", "UPDATE qp_library.invitations SET \"IsCollection\"=false" })
        { await using var denied = new NpgsqlCommand(sql, connection); Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync())).SqlState); }
    }
}
