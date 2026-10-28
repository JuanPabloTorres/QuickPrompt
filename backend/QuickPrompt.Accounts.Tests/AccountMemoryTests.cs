using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class AccountMemoryTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static LibraryActor Actor(Guid id) => LibraryActor.FromPrincipal(new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test-authenticated")));

    [Fact]
    public async Task SameSessionIdentityRemainsPrivateAcrossUsersAndLibrariesAndSurvivesNewAdapter()
    {
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var libraries = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var first = await libraries.CreateLibrary(a, "Contexto A");
        var second = await libraries.CreateLibrary(a, "Otro contexto A");
        var third = await libraries.CreateLibrary(b, "Contexto B");
        var prompt = await libraries.SavePrompt(a, first.Id, null, 0, "Origen", "Explica <tema>.");
        var sessionId = Guid.NewGuid();
        var memory = new AccountAgentMemory(postgres.Application);
        var editor = new AccountEditorMemory(memory);
        var savedA = editor.Save(a, first.Id, new(sessionId, 0, null, true, prompt.Id, 1, "Borrador A", "Explica <tema> con claridad.", new() { ["tema"] = "algebra" }));
        var savedB = editor.Save(b, third.Id, new(sessionId, 0, null, true, null, null, "Borrador B", "Resume <tema>.", new() { ["tema"] = "biologia" }));
        var savedOther = editor.Save(a, second.Id, new(sessionId, 0, null, true, null, null, "Otro A", "Otro <tema>.", new() { ["tema"] = "geometria" }));
        var restarted = new AccountEditorMemory(new AccountAgentMemory(postgres.Application));
        Assert.Equal("Explica algebra con claridad.", restarted.Read(a, first.Id, sessionId).CompiledText);
        Assert.Equal(savedB.DraftId, restarted.Read(b, third.Id, sessionId).DraftId);
        Assert.Equal(savedOther.DraftId, restarted.Read(a, second.Id, sessionId).DraftId);
        Assert.NotEqual(savedA.DraftId, savedB.DraftId);
        await Assert.ThrowsAsync<LibraryUnavailableException>(() => Task.Run(() => restarted.Read(b, first.Id, sessionId)));
        var sessions = memory.RememberedSessions(a, first.Id);
        var original = sessions.Get(sessionId)!;
        sessions.Put(original with { Approval = "GRANTED", PendingAction = SemanticOperation.EXECUTE_LOCAL, PendingRequest = "No repetir esta orden" });
        var restored = sessions.Get(sessionId)!;
        Assert.Equal("NOT_GRANTED", restored.Approval);
        Assert.Null(restored.PendingAction); Assert.Null(restored.PendingRequest); Assert.Null(restored.PendingCommand);
        Assert.Equal(savedA.TemplateText, restored.Draft!.TemplateText);
        var foreign = memory.Catalog(a, first.Id).Get(prompt.Id);
        Assert.Throws<LibraryUnavailableException>(() => memory.RememberedSessions(b, third.Id).Put(new(Guid.NewGuid(), foreign, null, [], DateTime.UtcNow)));
    }

    [Fact]
    public async Task SavedEditorUsesCoreCompilationChecksSourceAndProtectsArtifactIdentityAcrossRevisionReset()
    {
        var actor = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var libraries = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var library = await libraries.CreateLibrary(actor, "Revisiones");
        var source = await libraries.SavePrompt(actor, library.Id, null, 0, "Origen", "Explica <tema>.");
        var memory = new AccountAgentMemory(postgres.Application); var editor = new AccountEditorMemory(memory);
        var id = Guid.NewGuid();
        var initial = new RememberEditorContext(id, 0, null, true, source.Id, 1, "Mi borrador", "Explica <tema> en español.", new() { ["tema"] = "RLS" });
        var saved = editor.Save(actor, library.Id, initial);
        Assert.Equal(new[] { "tema" }, saved.VariableNames);
        Assert.Equal("Explica RLS en español.", saved.CompiledText);
        Assert.Throws<ConflictException>(() => editor.Save(actor, library.Id, initial));
        await libraries.SavePrompt(actor, library.Id, source.Id, 1, "Origen", "Versión nueva <tema>.");
        Assert.True(editor.Read(actor, library.Id, id).SourceStale);
        Assert.Throws<ConflictException>(() => editor.Save(actor, library.Id, initial with { ExpectedDraftRevision = saved.DraftRevision, ExpectedDraftId = saved.DraftId }));
        var changedSource = editor.Save(actor, library.Id, initial with { Revision = 2, ExpectedDraftRevision = saved.DraftRevision, ExpectedDraftId = saved.DraftId, ExpectedMemoryRevision = saved.MemoryRevision, TemplateText = "Nueva revisión <tema>." });
        Assert.Equal(1, changedSource.DraftRevision);
        Assert.NotEqual(saved.DraftId, changedSource.DraftId);
        Assert.Throws<ConflictException>(() => editor.Save(actor, library.Id, initial with { Revision = 2, ExpectedDraftRevision = 1, ExpectedDraftId = saved.DraftId, ExpectedMemoryRevision = changedSource.MemoryRevision }));
        Assert.Throws<ValidationException>(() => editor.Save(actor, library.Id, initial with { Confirmed = false }));
        Assert.Throws<ValidationException>(() => editor.Save(actor, library.Id, initial with { Values = new() { ["no_existe"] = "dato" } }));
        memory.RememberedSessions(actor, library.Id).Forget(id);
        Assert.Throws<MissingPromptException>(() => new AccountEditorMemory(new AccountAgentMemory(postgres.Application)).Read(actor, library.Id, id));
        Assert.Empty(memory.List(actor, library.Id));
        Assert.Single(await libraries.ListPrompts(actor, library.Id));
    }

    [Fact]
    public async Task PreferencesAreConfirmedValidatedAndPrivateAcrossNewServiceInstances()
    {
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        var memory = new AccountAgentMemory(postgres.Application);
        var prefsA = memory.Preferences(a); var prefsB = memory.Preferences(b);
        var approved = new PreparationPreferences("es", "Markdown", "ollama");
        Assert.Throws<ValidationException>(() => prefsA.Confirm(new(approved, false)));
        Assert.Throws<ValidationException>(() => prefsA.Save(new("inventado", "Markdown")));
        Assert.Equal(approved, prefsA.Confirm(new(approved, true)));
        Assert.Equal(new PreparationPreferences(), prefsB.Get());
        prefsB.Confirm(new(new("en", "JSON"), true));
        Assert.Equal(approved, new AccountAgentMemory(postgres.Application).Preferences(a).Get());
        prefsA.Forget();
        Assert.Equal(new PreparationPreferences(), prefsA.Get());
        Assert.Equal("en", prefsB.Get().Language);
    }

    [Fact]
    public async Task OwnerRetentionLimitAndExpirationDoNotEvictAnotherUsersSession()
    {
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var libraries = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var libraryA = await libraries.CreateLibrary(a, "Límite A"); var libraryB = await libraries.CreateLibrary(b, "Límite B");
        var memory = new AccountAgentMemory(postgres.Application);
        var sessionsA = memory.RememberedSessions(a, libraryA.Id); var sessionsB = memory.RememberedSessions(b, libraryB.Id);
        var idB = Guid.NewGuid(); sessionsB.Put(new(idB, null, null, new() { ["dato"] = "B conservado" }, DateTime.UtcNow));
        for (var index = 0; index < 101; index++) sessionsA.Put(new(Guid.NewGuid(), null, null, [], DateTime.UtcNow));
        Assert.Equal(100, memory.List(a, libraryA.Id).Length);
        Assert.Equal("B conservado", sessionsB.Get(idB)!.Values["dato"]);
        await using (var connection = await postgres.Application.OpenConnectionAsync())
        {
            await using var transaction = await connection.BeginTransactionAsync();
            await SetActor(connection, a.Id);
            await using var expire = new NpgsqlCommand("UPDATE qp_library.agent_contexts SET \"UpdatedAt\"=now()-interval '8 days', \"ExpiresAt\"=now()-interval '1 day' WHERE \"LibraryId\"=@library", connection);
            expire.Parameters.AddWithValue("library", libraryA.Id);
            Assert.Equal(100, await expire.ExecuteNonQueryAsync());
            await transaction.CommitAsync();
        }
        Assert.Empty(memory.List(a, libraryA.Id));
        Assert.NotNull(sessionsB.Get(idB));
    }

    [Fact]
    public async Task ValueOnlyEditsCannotReuseAStaleRememberedRevision()
    {
        var actor = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var library = await scope.ServiceProvider.GetRequiredService<LibraryAccess>().CreateLibrary(actor, "Valores concurrentes");
        var editor = new AccountEditorMemory(new AccountAgentMemory(postgres.Application));
        var input = new RememberEditorContext(Guid.NewGuid(), 0, null, true, null, null, "Valores", "Explica <tema>.", new() { ["tema"] = "inicial" });
        var saved = editor.Save(actor, library.Id, input);
        var edit = input with { ExpectedDraftRevision = saved.DraftRevision, ExpectedDraftId = saved.DraftId, ExpectedMemoryRevision = saved.MemoryRevision, Values = new() { ["tema"] = "segunda versión" } };
        var changed = editor.Save(actor, library.Id, edit);
        Assert.Equal(saved.DraftRevision, changed.DraftRevision);
        Assert.Equal(saved.MemoryRevision + 1, changed.MemoryRevision);
        Assert.Throws<ConflictException>(() => editor.Save(actor, library.Id, edit with { Values = new() { ["tema"] = "valor obsoleto" } }));
    }

    [Fact]
    public async Task ConcurrentEditorsCannotOverwriteTheSameRememberedRevision()
    {
        var actor = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var library = await scope.ServiceProvider.GetRequiredService<LibraryAccess>().CreateLibrary(actor, "Concurrencia");
        await using var concurrent = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(postgres.ApplicationConnection) { MaxPoolSize = 4 }.ConnectionString);
        var editor = new AccountEditorMemory(new AccountAgentMemory(concurrent));
        var input = new RememberEditorContext(Guid.NewGuid(), 0, null, true, null, null, "Inicial", "Explica <tema>.", new() { ["tema"] = "datos" });
        var saved = editor.Save(actor, library.Id, input);
        var results = await Task.WhenAll(new[] { "Primero <tema>.", "Segundo <tema>." }.Select(text => Task.Run(() =>
        {
            try { editor.Save(actor, library.Id, input with { ExpectedDraftRevision = saved.DraftRevision, ExpectedDraftId = saved.DraftId, ExpectedMemoryRevision = saved.MemoryRevision, TemplateText = text }); return true; }
            catch (ConflictException) { return false; }
        })));
        Assert.Single(results, result => result);
        Assert.Single(results, result => !result);
        Assert.Equal(2, editor.Read(actor, library.Id, input.SessionId).DraftRevision);
    }

    [Fact]
    public async Task LimitedSqlCannotReadOrRewriteAnotherAccountsMemoryOrRetargetItsOwner()
    {
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var library = await scope.ServiceProvider.GetRequiredService<LibraryAccess>().CreateLibrary(a, "SQL privado");
        var memory = new AccountAgentMemory(postgres.Application);
        var id = Guid.NewGuid(); memory.RememberedSessions(a, library.Id).Put(new(id, null, null, [], DateTime.UtcNow));
        memory.Preferences(a).Save(new("es", "Markdown"));
        await using var connection = await postgres.Application.OpenConnectionAsync();
        await using (var noActor = new NpgsqlCommand("SELECT (SELECT count(*) FROM qp_library.agent_contexts) + (SELECT count(*) FROM qp_library.agent_preferences)", connection))
            Assert.Equal(0L, await noActor.ExecuteScalarAsync());
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetActor(connection, b.Id);
            await using var read = new NpgsqlCommand("SELECT count(*) FROM qp_library.agent_contexts WHERE \"SessionId\"=@id", connection);
            read.Parameters.AddWithValue("id", id); Assert.Equal(0L, await read.ExecuteScalarAsync());
            await using var update = new NpgsqlCommand("UPDATE qp_library.agent_preferences SET \"Language\"='en' WHERE \"OwnerId\"=@owner", connection);
            update.Parameters.AddWithValue("owner", a.Id); Assert.Equal(0, await update.ExecuteNonQueryAsync());
            await transaction.RollbackAsync();
        }
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetActor(connection, a.Id);
            await using var retarget = new NpgsqlCommand("UPDATE qp_library.agent_contexts SET \"OwnerId\"=@other WHERE \"SessionId\"=@id", connection);
            retarget.Parameters.AddWithValue("other", b.Id); retarget.Parameters.AddWithValue("id", id);
            var error = await Assert.ThrowsAsync<PostgresException>(() => retarget.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
            await transaction.RollbackAsync();
        }
    }

    private static async Task SetActor(NpgsqlConnection connection, Guid actor)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id', @actor, true)", connection);
        command.Parameters.AddWithValue("actor", actor.ToString()); await command.ExecuteNonQueryAsync();
    }
}
