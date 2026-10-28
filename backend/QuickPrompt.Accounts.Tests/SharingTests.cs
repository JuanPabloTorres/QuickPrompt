using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Accounts.Sharing;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class SharingTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ReceivedCatalogueIsEmptyWithoutAcceptedInvitations()
    {
        var actor = Actor(await postgres.CreateUser(confirmed: true));
        await using var scope = postgres.Services.CreateAsyncScope();
        var sharing = scope.ServiceProvider.GetRequiredService<SharingAccess>();
        try { Assert.Empty(await sharing.ListReceived(actor, "", 0, 10)); }
        catch (InvalidOperationException error)
        {
            Assert.Fail("Received catalogue failed: type=InvalidOperationException; translation=" + error.Message.Contains("could not be translated", StringComparison.Ordinal));
        }
    }
    private static LibraryActor Actor(Guid id) => LibraryActor.FromPrincipal(new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test-authenticated")));

    [Fact]
    public async Task AcceptedSnapshotIsFrozenAndImportsArePrivateIdempotentAndRevocable()
    {
        var a = Actor(await postgres.CreateUser(confirmed: true));
        var b = Actor(await postgres.CreateUser(confirmed: true));
        var c = Actor(await postgres.CreateUser(confirmed: true));
        await using var scope = postgres.Services.CreateAsyncScope();
        var library = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var sharing = scope.ServiceProvider.GetRequiredService<SharingAccess>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var origin = await library.CreateLibrary(a, "Origen privado");
        var destination = await library.CreateLibrary(b, "Mi copia");
        var source = await library.SavePrompt(a, origin.Id, null, 0, "Compartido", "Explica <tema> con claridad.");
        await Assert.ThrowsAsync<LibraryRevisionException>(() => sharing.Publish(a, origin.Id, source.Id, 0));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.Publish(b, origin.Id, source.Id, 1));
        var publication = await sharing.Publish(a, origin.Id, source.Id, source.Revision);
        await library.SavePrompt(a, origin.Id, source.Id, 1, "Privado posterior", "Contenido privado posterior.");
        Assert.True(await sharing.Invite(a, publication.Id, (await users.FindByIdAsync(b.Id.ToString()))!.Email!));
        var invitation = Assert.Single(await sharing.ListInvitations(b));
        Assert.Null(invitation.RecipientEmail);
        Assert.False(invitation.Available);
        Assert.Equal((await users.FindByIdAsync(b.Id.ToString()))!.Email, Assert.Single(await sharing.ListInvitations(a)).RecipientEmail);
        Assert.Equal(publication.Id, Assert.Single(await sharing.ListPublications(a, origin.Id, source.Id)).Id);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.ListPublications(b, origin.Id, source.Id));
        Assert.Empty(await sharing.ListInvitations(c));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.Read(b, publication.Id));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.Decide(c, invitation.Id, InvitationState.Accepted));
        Assert.True(await sharing.Decide(b, invitation.Id, InvitationState.Accepted));
        Assert.True(Assert.Single(await sharing.ListInvitations(b)).Available);
        Assert.Equal(source.Template, (await sharing.Read(b, publication.Id)).Template);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => library.ListVersions(b, origin.Id, source.Id));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.Import(b, publication.Id, origin.Id, Guid.NewGuid()));
        var operation = Guid.NewGuid();
        var copy = await sharing.Import(b, publication.Id, destination.Id, operation);
        Assert.Equal(copy, await sharing.Import(b, publication.Id, destination.Id, operation));
        Assert.Equal(source.Template, copy.Template);
        Assert.NotEqual(source.Id, copy.Id);
        var editedCopy = await library.SavePrompt(b, destination.Id, copy.Id, 1, "Mi edición", "Editado por B.");
        Assert.Equal(copy, await sharing.Import(b, publication.Id, destination.Id, operation));
        Assert.Equal(editedCopy, Assert.Single(await library.ListPrompts(b, destination.Id)));
        Assert.Equal("Contenido privado posterior.", Assert.Single(await library.ListPrompts(a, origin.Id)).Template);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.Revoke(b, publication.Id));
        Assert.True(await sharing.Revoke(a, publication.Id));
        Assert.False(await sharing.Revoke(a, publication.Id));
        Assert.False(Assert.Single(await sharing.ListInvitations(b)).Available);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.Read(b, publication.Id));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.Import(b, publication.Id, destination.Id, operation));
        Assert.Equal(editedCopy, Assert.Single(await library.ListPrompts(b, destination.Id)));
    }

    [Fact]
    public async Task GrantRevocationAndDeclineAreTerminalAndUnknownRecipientsDoNotGetAccess()
    {
        var a = Actor(await postgres.CreateUser(confirmed: true));
        var b = Actor(await postgres.CreateUser(confirmed: true));
        var unconfirmed = await postgres.CreateUser();
        await using var scope = postgres.Services.CreateAsyncScope();
        var library = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var sharing = scope.ServiceProvider.GetRequiredService<SharingAccess>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var owned = await library.CreateLibrary(a, "Invitaciones");
        var source = await library.SavePrompt(a, owned.Id, null, 0, "Título", "Texto.");
        var first = await sharing.Publish(a, owned.Id, source.Id, 1);
        var second = await sharing.Publish(a, owned.Id, source.Id, 1);
        Assert.False(await sharing.Invite(a, first.Id, "unknown@example.invalid"));
        Assert.False(await sharing.Invite(a, first.Id, (await users.FindByIdAsync(unconfirmed.ToString()))!.Email!));
        var emailB = (await users.FindByIdAsync(b.Id.ToString()))!.Email!;
        await sharing.Invite(a, first.Id, emailB);
        await sharing.Invite(a, first.Id, emailB);
        var invitation = Assert.Single(await sharing.ListInvitations(b));
        await sharing.Decide(b, invitation.Id, InvitationState.Declined);
        await Assert.ThrowsAsync<ShareConflictException>(() => sharing.Decide(b, invitation.Id, InvitationState.Accepted));
        await sharing.Invite(a, first.Id, emailB);
        var replacement = (await sharing.ListInvitations(b)).Single(x => x.State == InvitationState.Pending);
        await sharing.Decide(b, replacement.Id, InvitationState.Accepted);
        var destination = await library.CreateLibrary(b, "Destino");
        var operation = Guid.NewGuid();
        await sharing.Import(b, first.Id, destination.Id, operation);
        await sharing.Invite(a, second.Id, emailB);
        await sharing.Decide(b, (await sharing.ListInvitations(b)).Single(x => x.PublicationId == second.Id).Id, InvitationState.Accepted);
        await Assert.ThrowsAsync<ShareConflictException>(() => sharing.Import(b, second.Id, destination.Id, operation));
        await sharing.Decide(a, replacement.Id, InvitationState.Revoked);
        await Assert.ThrowsAsync<ShareConflictException>(() => sharing.Decide(b, replacement.Id, InvitationState.Accepted));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.Read(b, first.Id));
        // A publication revoked before acceptance cannot be revived by accepting its old invite.
        await sharing.Invite(a, first.Id, emailB);
        var pending = (await sharing.ListInvitations(b)).Single(x => x.PublicationId == first.Id && x.State == InvitationState.Pending);
        await sharing.Revoke(a, first.Id);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.Decide(b, pending.Id, InvitationState.Accepted));
        Assert.Equal(InvitationState.Pending, (await sharing.ListInvitations(b)).Single(x => x.Id == pending.Id).State);
    }

    [Fact]
    public async Task DirectSqlCannotReadPendingContentRewritePublicationsOrRetargetInvitations()
    {
        var a = Actor(await postgres.CreateUser(confirmed: true));
        var b = Actor(await postgres.CreateUser(confirmed: true));
        Guid publicationId, invitationId;
        await using (var scope = postgres.Services.CreateAsyncScope())
        {
            var library = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
            var sharing = scope.ServiceProvider.GetRequiredService<SharingAccess>();
            var owned = await library.CreateLibrary(a, "SQL");
            var source = await library.SavePrompt(a, owned.Id, null, 0, "SQL", "Contenido protegido.");
            publicationId = (await sharing.Publish(a, owned.Id, source.Id, 1)).Id;
            var user = await scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>().FindByIdAsync(b.Id.ToString());
            await sharing.Invite(a, publicationId, user!.Email!);
            invitationId = Assert.Single(await sharing.ListInvitations(b)).Id;
        }
        await using var connection = await postgres.Application.OpenConnectionAsync();
        await using (var noActor = new NpgsqlCommand("SELECT (SELECT count(*) FROM qp_library.publications) + (SELECT count(*) FROM qp_library.invitations) + (SELECT count(*) FROM qp_library.imports)", connection))
            Assert.Equal(0L, await noActor.ExecuteScalarAsync());
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetActor(connection, b.Id);
            await using var read = new NpgsqlCommand("SELECT count(*) FROM qp_library.publications WHERE \"Id\"=@id", connection);
            read.Parameters.AddWithValue("id", publicationId);
            Assert.Equal(0L, await read.ExecuteScalarAsync());
            await transaction.RollbackAsync();
        }
        foreach (var sql in new[]
        {
            "UPDATE qp_library.publications SET \"Template\"='rewrite' WHERE \"Id\"=@publication",
            "DELETE FROM qp_library.publications WHERE \"Id\"=@publication",
            "UPDATE qp_library.invitations SET \"RecipientId\"=@actor WHERE \"Id\"=@invitation"
        })
        {
            await using var transaction = await connection.BeginTransactionAsync();
            await SetActor(connection, a.Id);
            await using var mutation = new NpgsqlCommand(sql, connection);
            mutation.Parameters.AddWithValue("publication", publicationId);
            mutation.Parameters.AddWithValue("invitation", invitationId);
            mutation.Parameters.AddWithValue("actor", a.Id);
            var error = await Assert.ThrowsAsync<PostgresException>(() => mutation.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
            await transaction.RollbackAsync();
        }
    }

    [Fact]
    public async Task ImportWaitingBehindRevocationRechecksPermissionAndCreatesNothing()
    {
        var a = Actor(await postgres.CreateUser(confirmed: true));
        var b = Actor(await postgres.CreateUser(confirmed: true));
        Guid publicationId, destination;
        await using (var seed = postgres.Services.CreateAsyncScope())
        {
            var library = seed.ServiceProvider.GetRequiredService<LibraryAccess>();
            var sharing = seed.ServiceProvider.GetRequiredService<SharingAccess>();
            var owned = await library.CreateLibrary(a, "Carrera");
            destination = (await library.CreateLibrary(b, "Destino")).Id;
            var prompt = await library.SavePrompt(a, owned.Id, null, 0, "Versión", "Contenido.");
            publicationId = (await sharing.Publish(a, owned.Id, prompt.Id, 1)).Id;
            var email = (await seed.ServiceProvider.GetRequiredService<UserManager<AccountUser>>().FindByIdAsync(b.Id.ToString()))!.Email!;
            await sharing.Invite(a, publicationId, email);
            await sharing.Decide(b, Assert.Single(await sharing.ListInvitations(b)).Id, InvitationState.Accepted);
        }
        // Separate connection lets the normal single-connection test pool wait on the real PG lock.
        var raw = new NpgsqlConnectionStringBuilder(postgres.ApplicationConnection) { Pooling = false };
        await using var revoke = new NpgsqlConnection(raw.ConnectionString);
        await revoke.OpenAsync();
        await using var transaction = await revoke.BeginTransactionAsync();
        await SetActor(revoke, a.Id);
        await using (var lockAndRevoke = new NpgsqlCommand("SELECT pg_advisory_xact_lock(5277, hashtext(@key)); UPDATE qp_library.publications SET \"RevokedAt\"=now() WHERE \"Id\"=@id", revoke))
        {
            lockAndRevoke.Parameters.AddWithValue("key", publicationId.ToString());
            lockAndRevoke.Parameters.AddWithValue("id", publicationId);
            await lockAndRevoke.ExecuteNonQueryAsync();
        }
        await using var scope = postgres.Services.CreateAsyncScope();
        var import = scope.ServiceProvider.GetRequiredService<SharingAccess>().Import(b, publicationId, destination, Guid.NewGuid());
        var waiting = false;
        for (var attempt = 0; attempt < 80 && !waiting; attempt++)
        {
            await using var locks = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype='advisory' AND classid=5277 AND NOT granted)", revoke);
            waiting = (bool)(await locks.ExecuteScalarAsync())!;
            if (!waiting) await Task.Delay(25);
        }
        Assert.True(waiting, "Must observe the competing import blocked on the publication lock, not infer a race from timing.");
        await transaction.CommitAsync();
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => import);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<LibraryAccess>().ListPrompts(b, destination));
    }

    private static async Task SetActor(NpgsqlConnection connection, Guid actor)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id', @actor, true)", connection);
        command.Parameters.AddWithValue("actor", actor.ToString());
        await command.ExecuteNonQueryAsync();
    }
}
