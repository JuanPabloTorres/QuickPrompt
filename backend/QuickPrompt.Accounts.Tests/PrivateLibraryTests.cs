using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuickPrompt.Accounts.Persistence;
using Npgsql;
using QuickPrompt.Cloud;
using QuickPrompt.Accounts.Libraries;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class PrivateLibraryTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static LibraryActor Actor(Guid id) => LibraryActor.FromPrincipal(new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test-authenticated")));

    [Fact]
    public async Task OwnersKeepSameNamedLibrariesPrivateAndCannotSelectAnotherResource()
    {
        var a = Actor(await postgres.CreateUser());
        var b = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var library = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var libraryA = await library.CreateLibrary(a, "Conceptos");
        var libraryB = await library.CreateLibrary(b, "Conceptos");
        var promptA = await library.SavePrompt(a, libraryA.Id, null, 0, "Conceptualizar", "Explica <tema> sin inventar datos.");
        Assert.Equal(libraryA.Id, Assert.Single(await library.ListLibraries(a)).Id);
        Assert.Equal(libraryB.Id, Assert.Single(await library.ListLibraries(b)).Id);
        Assert.Empty(await library.ListPrompts(b, libraryB.Id));
        Assert.Equal(promptA, Assert.Single(await library.ListPrompts(a, libraryA.Id)));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => library.ListPrompts(b, libraryA.Id));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => library.SavePrompt(b, libraryA.Id, promptA.Id, 1, "Ataque", "Otro contenido"));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => library.SavePrompt(b, libraryB.Id, promptA.Id, 1, "Ataque", "Otro contenido"));
        var changed = await library.SavePrompt(a, libraryA.Id, promptA.Id, 1, "Conceptualizar", "Explica <tema> en tres puntos.");
        Assert.Equal(2, changed.Revision);
        await Assert.ThrowsAsync<LibraryRevisionException>(() => library.SavePrompt(a, libraryA.Id, promptA.Id, 1, "Viejo", "No aplicar"));
        Assert.Equal(changed, Assert.Single(await library.ListPrompts(a, libraryA.Id)));
        var versions = await library.ListVersions(a, libraryA.Id, promptA.Id);
        Assert.Equal(new[] { 2, 1 }, versions.Select(x => x.Revision));
        Assert.Equal(promptA.Template, versions[1].Template);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => library.ListVersions(b, libraryA.Id, promptA.Id));
    }

    [Fact]
    public async Task LimitedRoleCannotBypassPoliciesEvenWhenApplicationOwnerFilterIsOmitted()
    {
        var a = Actor(await postgres.CreateUser());
        var b = Actor(await postgres.CreateUser());
        Guid libraryId;
        await using (var scope = postgres.Services.CreateAsyncScope())
            libraryId = (await scope.ServiceProvider.GetRequiredService<LibraryAccess>().CreateLibrary(a, "RLS privado")).Id;

        await using var connection = await postgres.Application.OpenConnectionAsync();
        await using var role = new NpgsqlCommand("SELECT rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole FROM pg_roles WHERE rolname = current_user", connection);
        Assert.False((bool)(await role.ExecuteScalarAsync())!);
        await using var transaction = await connection.BeginTransactionAsync();
        await SetActor(connection, b.Id);
        await using var read = new NpgsqlCommand("SELECT count(*) FROM qp_library.libraries WHERE \"Id\" = @id", connection);
        read.Parameters.AddWithValue("id", libraryId);
        Assert.Equal(0L, await read.ExecuteScalarAsync());
        await using var history = new NpgsqlCommand("SELECT count(*) FROM qp_library.prompt_versions WHERE \"LibraryId\"=@id", connection);
        history.Parameters.AddWithValue("id", libraryId);
        Assert.Equal(0L, await history.ExecuteScalarAsync());
        await using var update = new NpgsqlCommand("UPDATE qp_library.libraries SET \"Name\"='not authorized' WHERE \"Id\"=@id", connection);
        update.Parameters.AddWithValue("id", libraryId);
        Assert.Equal(0, await update.ExecuteNonQueryAsync());
        await using var insert = new NpgsqlCommand("INSERT INTO qp_library.prompts (\"Id\",\"LibraryId\",\"Title\",\"Template\",\"Revision\",\"CreatedAt\",\"UpdatedAt\") VALUES (@id,@library,'not authorized','text',1,now(),now())", connection);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("library", libraryId);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task MissingContextAndPooledConnectionAfterCommitOrRollbackExposeNoRows()
    {
        var a = Actor(await postgres.CreateUser());
        await using (var scope = postgres.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LibraryAccess>().CreateLibrary(a, "Pool");
        foreach (var commit in new[] { true, false })
        {
            int backend;
            await using (var connection = await postgres.Application.OpenConnectionAsync())
            {
                backend = connection.ProcessID;
                await using var transaction = await connection.BeginTransactionAsync();
                await SetActor(connection, a.Id);
                await using var count = new NpgsqlCommand("SELECT count(*) FROM qp_library.libraries", connection);
                Assert.Equal(1L, await count.ExecuteScalarAsync());
                if (commit) await transaction.CommitAsync(); else await transaction.RollbackAsync();
            }
            await using var reused = await postgres.Application.OpenConnectionAsync();
            Assert.Equal(backend, reused.ProcessID);
            await using var noActor = new NpgsqlCommand("SELECT count(*) FROM qp_library.libraries", reused);
            Assert.Equal(0L, await noActor.ExecuteScalarAsync());
        }
    }

    [Fact]
    public void UnauthenticatedOrMalformedPrincipalsNeverBecomeActors()
    {
        Assert.Throws<UnauthorizedAccessException>(() => LibraryActor.FromPrincipal(new ClaimsPrincipal()));
        Assert.Throws<UnauthorizedAccessException>(() => Actor(Guid.Empty));
        Assert.Throws<UnauthorizedAccessException>(() => LibraryActor.FromPrincipal(new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "another actor")], "test"))));
    }

    [Fact]
    public async Task AccountHostRefusesTheMigrationOwnerAsItsApplicationCredential()
    {
        await using var privileged = NpgsqlDataSource.Create(postgres.MigrationConnection);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AccountRoleGuard(privileged).StartAsync(CancellationToken.None));
        await new AccountRoleGuard(postgres.Application).StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task VersionRlsRejectsAnotherLibraryBeforeTheCompositeForeignKeyEvenForTheSameOwner()
    {
        var actor = Actor(await postgres.CreateUser());
        Guid otherLibrary;
        AccountPrompt prompt;
        await using (var scope = postgres.Services.CreateAsyncScope())
        {
            var library = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
            var first = await library.CreateLibrary(actor, "Primera");
            otherLibrary = (await library.CreateLibrary(actor, "Segunda")).Id;
            prompt = await library.SavePrompt(actor, first.Id, null, 0, "Versión", "Explica <tema>.");
        }
        await using var connection = await postgres.Application.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetActor(connection, actor.Id);
        await using var wrongParent = new NpgsqlCommand("INSERT INTO qp_library.prompt_versions (\"LibraryId\",\"PromptId\",\"Revision\",\"Title\",\"Template\",\"CreatedAt\") VALUES (@library,@prompt,1,'Incorrecto','Texto',now())", connection);
        wrongParent.Parameters.AddWithValue("library", otherLibrary);
        wrongParent.Parameters.AddWithValue("prompt", prompt.Id);
        var rejected = await Assert.ThrowsAsync<PostgresException>(() => wrongParent.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, rejected.SqlState);
        await transaction.RollbackAsync();
        // The restrictive parent policy now rejects first; the composite FK remains
        // a separate database invariant instead of being replaced by that policy.
        await using var foreignKey = new NpgsqlCommand("""
            SELECT pg_get_constraintdef(oid) FROM pg_constraint
            WHERE conrelid='qp_library.prompt_versions'::regclass AND conname='FK_prompt_versions_prompts_LibraryId_PromptId'
            """, connection);
        Assert.Equal("FOREIGN KEY (\"LibraryId\", \"PromptId\") REFERENCES qp_library.prompts(\"LibraryId\", \"Id\") ON DELETE RESTRICT", await foreignKey.ExecuteScalarAsync());
    }

    [Fact]
    public async Task DestructiveRollbackIsRejectedAndLeavesPrivateHistoryUsable()
    {
        var actor = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var library = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var owned = await library.CreateLibrary(actor, "Rollback protegido");
        var prompt = await library.SavePrompt(actor, owned.Id, null, 0, "Conservar", "Explica <tema>.");
        await using var migration = new AccountDatabase(new DbContextOptionsBuilder<AccountDatabase>()
            .UseNpgsql(postgres.MigrationConnection, options => options.MigrationsHistoryTable("history", "qp_migrations")).Options);
        await Assert.ThrowsAsync<NotSupportedException>(() => migration.GetService<IMigrator>()
            .MigrateAsync("20261026050826_InitialAccounts"));
        Assert.Equal(prompt.Template, Assert.Single(await library.ListVersions(actor, owned.Id, prompt.Id)).Template);
        Assert.Equal(prompt, Assert.Single(await library.ListPrompts(actor, owned.Id)));
    }

    private static async Task SetActor(NpgsqlConnection connection, Guid actor)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id', @actor, true)", connection);
        command.Parameters.AddWithValue("actor", actor.ToString());
        await command.ExecuteNonQueryAsync();
    }
}
