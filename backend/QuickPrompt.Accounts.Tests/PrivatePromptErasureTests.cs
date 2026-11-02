using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Accounts.Sharing;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class PrivatePromptErasureTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static LibraryActor Actor(Guid id) => LibraryActor.FromPrincipal(new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "synthetic-auth")));

    [Fact]
    public async Task ErasureScrubsAllPrivateVersionsAndCannotReplayCaptureOrUseTheSource()
    {
        var actor = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var own = await access.CreateLibrary(actor, "Erasure owner");
        var capture = new AccountCaptureInput(Guid.NewGuid(), "Synthetic private source", "Explica <tema>.");
        var created = (await access.Capture(actor, own.Id, capture)).Prompt;
        var memory = new AccountAgentMemory(postgres.Application);
        var revised = memory.Library(actor, own.Id).Update(created.Id, new("Synthetic revision", "Describe <tema>.", "Synthetic description", Revision: 1,
            Defaults: new() { ["tema"] = "synthetic-private-default" }));
        var group = await access.SaveCollection(actor, own.Id, null, new("Erasure grouping"));
        await access.AssignCollection(actor, own.Id, revised.Id, new(group.Id, revised.Revision, true));
        var collectionRevision = Assert.Single(await access.ListCollections(actor, own.Id)).Group.Revision;

        await access.DeletePrompt(actor, own.Id, revised.Id, new(revised.Revision, true));
        await access.DeletePrompt(actor, own.Id, revised.Id, new(revised.Revision, true));
        Assert.Empty(await access.ListPrompts(actor, own.Id));
        Assert.Empty(memory.Catalog(actor, own.Id).Search(new()).Items);
        Assert.Throws<MissingPromptException>(() => memory.Catalog(actor, own.Id).Get(revised.Id));
        Assert.Throws<MissingPromptException>(() => memory.Library(actor, own.Id).Update(revised.Id, new("No revive", "Other", Revision: revised.Revision)));
        Assert.Throws<MissingPromptException>(() => memory.Library(actor, own.Id).RestoreVersion(revised.Id, 1, new(revised.Revision, true)));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.ListVersions(actor, own.Id, revised.Id));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.ExportPrompt(actor, own.Id, revised.Id, revised.Revision));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.Capture(actor, own.Id, capture));
        var collection = Assert.Single(await access.ListCollections(actor, own.Id));
        Assert.Equal(0, collection.Templates); Assert.Equal(collectionRevision + 1, collection.Group.Revision);

        await using var connection = new NpgsqlConnection(postgres.MigrationConnection); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetActor(connection, actor.Id);
        await using var tombstone = new NpgsqlCommand("""
            SELECT count(*) FROM qp_library.prompts WHERE "LibraryId"=@library AND "Id"=@prompt AND "DeletedAt" IS NOT NULL
              AND "Title"='' AND "Template"='' AND "Description"='' AND "Defaults"='{}'::jsonb AND "Category"=0
            """, connection);
        tombstone.Parameters.AddWithValue("library", own.Id); tombstone.Parameters.AddWithValue("prompt", revised.Id);
        Assert.Equal(1L, await tombstone.ExecuteScalarAsync());
        await using var versions = new NpgsqlCommand("""
            SELECT count(*) FROM qp_library.prompt_versions WHERE "LibraryId"=@library AND "PromptId"=@prompt
              AND ("Title"<>'' OR "Template"<>'' OR "Description"<>'' OR "Defaults"<>'{}'::jsonb OR "Category"<>0)
            """, connection);
        versions.Parameters.AddWithValue("library", own.Id); versions.Parameters.AddWithValue("prompt", revised.Id);
        Assert.Equal(0L, await versions.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ConfirmationRevisionAndOtherOwnersNeverEraseAnUnreviewedSource()
    {
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var own = await access.CreateLibrary(a, "Review binding");
        var source = await access.SavePrompt(a, own.Id, null, 0, "Untouched", "Exact source.");
        await Assert.ThrowsAsync<ValidationException>(() => access.DeletePrompt(a, own.Id, source.Id, new(1, false)));
        await Assert.ThrowsAsync<LibraryRevisionException>(() => access.DeletePrompt(a, own.Id, source.Id, new(2, true)));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.DeletePrompt(b, own.Id, source.Id, new(1, true)));
        Assert.Equal(source, await access.GetPrompt(a, own.Id, source.Id));
        await using var connection = await postgres.Application.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetActor(connection, b.Id);
        await using var forged = new NpgsqlCommand("SELECT qp_library.erase_private_prompt(@library,@prompt,1)", connection);
        forged.Parameters.AddWithValue("library", own.Id); forged.Parameters.AddWithValue("prompt", source.Id);
        Assert.Equal(0, await forged.ExecuteScalarAsync());
    }

    [Fact]
    public async Task PublishedSnapshotsAndIndependentCopiesRemainWhileDeletedImportReceiptsCannotRevive()
    {
        var a = Actor(await postgres.CreateUser(true)); var b = Actor(await postgres.CreateUser(true));
        await using var scope = postgres.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var sharing = scope.ServiceProvider.GetRequiredService<SharingAccess>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var origin = await access.CreateLibrary(a, "Private origin"); var target = await access.CreateLibrary(b, "Independent copy");
        var source = await access.SavePrompt(a, origin.Id, null, 0, "Published source", "Published <tema>.");
        var snapshot = await sharing.Publish(a, origin.Id, source.Id, 1);
        await sharing.Invite(a, snapshot.Id, (await users.FindByIdAsync(b.Id.ToString()))!.Email!);
        await sharing.Decide(b, Assert.Single(await sharing.ListInvitations(b)).Id, InvitationState.Accepted);
        var operation = Guid.NewGuid(); var copy = await sharing.Import(b, snapshot.Id, target.Id, operation);
        await access.DeletePrompt(a, origin.Id, source.Id, new(1, true));
        Assert.Equal(snapshot, await sharing.Read(b, snapshot.Id));
        Assert.Equal(copy, await access.GetPrompt(b, target.Id, copy.Id));
        Assert.Equal(snapshot.Id, Assert.Single(await sharing.ListPublications(a, origin.Id, source.Id)).Id);
        await access.DeletePrompt(b, target.Id, copy.Id, new(1, true));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.Import(b, snapshot.Id, target.Id, operation));
        Assert.Empty(await access.ListPrompts(b, target.Id));
        Assert.Equal(snapshot, await sharing.Read(b, snapshot.Id));
    }

    [Fact]
    public async Task ARememberedDraftBecomesStaleButCannotOverwriteItsErasedSource()
    {
        var actor = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var own = await access.CreateLibrary(actor, "Separate memory");
        var source = await access.SavePrompt(actor, own.Id, null, 0, "Source", "Explain <tema>.");
        var editor = new AccountEditorMemory(new AccountAgentMemory(postgres.Application));
        var remembered = editor.Save(actor, own.Id, new(Guid.NewGuid(), 0, null, true, source.Id, 1, source.Title, source.Template, new() { ["tema"] = "synthetic" }));
        await access.DeletePrompt(actor, own.Id, source.Id, new(1, true));
        var restored = editor.Read(actor, own.Id, remembered.Id);
        Assert.True(restored.SourceStale); Assert.Equal(remembered.TemplateText, restored.TemplateText);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => access.SavePrompt(actor, own.Id, source.Id, 1, "No revive", restored.TemplateText));
    }

    [Fact]
    public async Task QueuedWorkBlocksErasureAndItsTerminalResultDoesNotPermitNewSourceUse()
    {
        var actor = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var own = await access.CreateLibrary(actor, "Busy library");
        var source = await access.SavePrompt(actor, own.Id, null, 0, "Busy source", "Explain <tema>.");
        await using var executionSource = new ExecutionDataSource(NpgsqlDataSource.Create(postgres.ExecutionConnection));
        var queue = new ExecutionQueue(executionSource, new ExecutionLimits());
        var executions = new AccountExecutionAccess(new AccountAgentMemory(postgres.Application), queue);
        var operation = Guid.NewGuid(); var input = new CompileSubmission(operation, 1, new() { ["tema"] = "synthetic" });
        var job = await executions.Compile(actor, own.Id, source.Id, input);
        await Assert.ThrowsAsync<ConflictException>(() => access.DeletePrompt(actor, own.Id, source.Id, new(1, true)));
        Assert.Equal(source, await access.GetPrompt(actor, own.Id, source.Id));
        executions.Cancel(actor, own.Id, job.Id);
        Assert.Null(await queue.Claim()); // The existing queue acknowledges cancellation; no model/handler runs.
        await access.DeletePrompt(actor, own.Id, source.Id, new(1, true));
        await Assert.ThrowsAsync<MissingPromptException>(() => executions.Compile(actor, own.Id, source.Id, input));
    }

    [Theory]
    [InlineData("UPDATE qp_library.prompt_versions SET \"Template\"='not authorized' WHERE false")]
    [InlineData("DELETE FROM qp_library.captures WHERE false")]
    [InlineData("DELETE FROM qp_library.imports WHERE false")]
    public async Task ErasureNeverGrantsGeneralImmutableRecordMutation(string sql)
    {
        await using var connection = await postgres.Application.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
    }

    [Fact]
    public async Task ADeletedCollectionImportMemberCannotBeRestoredByItsOriginalReceipt()
    {
        var a = Actor(await postgres.CreateUser(true)); var b = Actor(await postgres.CreateUser(true));
        await using var scope = postgres.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>(); var sharing = scope.ServiceProvider.GetRequiredService<SharingAccess>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var origin = await access.CreateLibrary(a, "Collection origin"); var target = await access.CreateLibrary(b, "Collection copy");
        var source = await access.SavePrompt(a, origin.Id, null, 0, "Member", "Collection snapshot.");
        var group = await access.SaveCollection(a, origin.Id, null, new("Published grouping"));
        await access.AssignCollection(a, origin.Id, source.Id, new(group.Id, 1, true));
        var snapshot = await sharing.PublishCollection(a, new(origin.Id, group.Id, 2, new() { [source.Id] = 1 }));
        await sharing.Invite(a, snapshot.Id, (await users.FindByIdAsync(b.Id.ToString()))!.Email!);
        await sharing.Decide(b, Assert.Single(await sharing.ListInvitations(b)).Id, InvitationState.Accepted);
        var operation = Guid.NewGuid(); var imported = await sharing.ImportCollection(b, snapshot.Id, target.Id, operation);
        var copy = Assert.Single(imported.Prompts);
        await access.DeletePrompt(b, target.Id, copy.Id, new(1, true));
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => sharing.ImportCollection(b, snapshot.Id, target.Id, operation));
        Assert.Empty(await access.ListPrompts(b, target.Id));
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(await sharing.ReadCollection(b, snapshot.Id)));
    }

    [Fact]
    public async Task AStorageFailureRollsBackVersionScrubbingMembershipAndTheTombstoneTogether()
    {
        var actor = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>(); var own = await access.CreateLibrary(actor, "Atomic erasure");
        var source = await access.SavePrompt(actor, own.Id, null, 0, "Atomic source", "Untouched content.");
        var group = await access.SaveCollection(actor, own.Id, null, new("Atomic collection"));
        await access.AssignCollection(actor, own.Id, source.Id, new(group.Id, 1, true));
        var before = Assert.Single(await access.ListCollections(actor, own.Id));
        var trigger = "qa_private_erase_" + Guid.NewGuid().ToString("N");
        await using var migration = new NpgsqlConnection(postgres.MigrationConnection); await migration.OpenAsync();
        await using (var install = new NpgsqlCommand($"""
            CREATE FUNCTION qp_library.{trigger}() RETURNS trigger LANGUAGE plpgsql AS $fault$
            BEGIN IF NEW."Id"='{source.Id}'::uuid AND NEW."DeletedAt" IS NOT NULL THEN RAISE EXCEPTION 'synthetic private erasure fault'; END IF; RETURN NEW; END $fault$;
            CREATE TRIGGER {trigger} BEFORE UPDATE ON qp_library.prompts FOR EACH ROW EXECUTE FUNCTION qp_library.{trigger}();
            """, migration)) await install.ExecuteNonQueryAsync();
        try
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => access.DeletePrompt(actor, own.Id, source.Id, new(1, true)));
            Assert.Equal("P0001", failure.SqlState);
            Assert.Equal(source, await access.GetPrompt(actor, own.Id, source.Id));
            Assert.Equal(source.Template, Assert.Single(await access.ListVersions(actor, own.Id, source.Id)).Template);
            var after = Assert.Single(await access.ListCollections(actor, own.Id));
            Assert.Equal(before.Group.Revision, after.Group.Revision); Assert.Equal(1, after.Templates);
        }
        finally
        {
            await using var remove = new NpgsqlCommand($"DROP TRIGGER {trigger} ON qp_library.prompts; DROP FUNCTION qp_library.{trigger}();", migration);
            await remove.ExecuteNonQueryAsync();
        }
        await access.DeletePrompt(actor, own.Id, source.Id, new(1, true));
        Assert.Empty(await access.ListPrompts(actor, own.Id));
    }

    [Fact]
    public async Task DeletedIdentityCannotBeRewrittenOrReceiveANewVersionEvenThroughTheApplicationRole()
    {
        var actor = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var own = await access.CreateLibrary(actor, "Immutable tombstone");
        var source = await access.SavePrompt(actor, own.Id, null, 0, "Source", "Text.");
        await access.DeletePrompt(actor, own.Id, source.Id, new(1, true));
        foreach (var sql in new[] {
            "UPDATE qp_library.prompts SET \"DeletedAt\"=NULL WHERE \"LibraryId\"=@library AND \"Id\"=@prompt",
            "DELETE FROM qp_library.prompts WHERE \"LibraryId\"=@library AND \"Id\"=@prompt" })
        {
            await using var connection = await postgres.Application.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
            await SetActor(connection, actor.Id);
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("library", own.Id); command.Parameters.AddWithValue("prompt", source.Id);
            Assert.Equal(0, await command.ExecuteNonQueryAsync());
        }
        foreach (var sql in new[] {
            "SELECT count(*) FROM qp_library.prompts WHERE \"LibraryId\"=@library AND \"Id\"=@prompt",
            "SELECT count(*) FROM qp_library.prompt_versions WHERE \"LibraryId\"=@library AND \"PromptId\"=@prompt" })
        {
            await using var connection = await postgres.Application.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
            await SetActor(connection, actor.Id);
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("library", own.Id); command.Parameters.AddWithValue("prompt", source.Id);
            Assert.Equal(0L, await command.ExecuteScalarAsync());
        }
        await using (var connection = await postgres.Application.OpenConnectionAsync())
        {
            await using var transaction = await connection.BeginTransactionAsync(); await SetActor(connection, actor.Id);
            await using var command = new NpgsqlCommand("INSERT INTO qp_library.prompt_versions (\"LibraryId\",\"PromptId\",\"Revision\",\"Title\",\"Template\",\"CreatedAt\") VALUES (@library,@prompt,9,'forged','forged',now())", connection);
            command.Parameters.AddWithValue("library", own.Id); command.Parameters.AddWithValue("prompt", source.Id);
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }
        // The restricted definer still sees technical identity for idempotent erasure,
        // but even its role cannot rewrite or remove an existing tombstone.
        foreach (var sql in new[] { "UPDATE qp_library.prompts SET \"DeletedAt\"=NULL WHERE \"Id\"=@prompt", "DELETE FROM qp_library.prompts WHERE \"Id\"=@prompt" })
        {
            await using var connection = new NpgsqlConnection(postgres.MigrationConnection); await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync(); await SetActor(connection, actor.Id);
            await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue("prompt", source.Id);
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }
        await using var worker = new NpgsqlConnection(postgres.ExecutionConnection); await worker.OpenAsync();
        await using var forbidden = new NpgsqlCommand("SELECT qp_library.erase_private_prompt(gen_random_uuid(),gen_random_uuid(),1)", worker);
        Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => forbidden.ExecuteNonQueryAsync())).SqlState);
    }

    private static async Task SetActor(NpgsqlConnection connection, Guid actor)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,true)", connection);
        command.Parameters.AddWithValue("actor", actor.ToString()); await command.ExecuteNonQueryAsync();
    }
}
