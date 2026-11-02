using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class AccountActiveSessionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static LibraryActor Actor(Guid id) => LibraryActor.FromPrincipal(new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "fixture")));

    [Fact]
    public async Task PendingConversationSurvivesRestartWithoutApprovalAndIsIndependentFromRememberedContext()
    {
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var first = await access.CreateLibrary(a, "Activa A"); var second = await access.CreateLibrary(a, "Otra A");
        var foreign = await access.CreateLibrary(b, "Activa B");
        var memory = new AccountAgentMemory(postgres.Application); var id = Guid.NewGuid();
        var session = new AgentSession(id, null, null, new() { ["tema"] = "A" }, DateTime.UtcNow,
            PendingAction: SemanticOperation.CREATE_DRAFT, Approval: "GRANTED", PendingRequest: "Aclaración de A")
        {
            PendingCommand = new(SemanticOperation.CREATE_DRAFT, "Crear con variables")
        };
        memory.ActiveSessions(a, first.Id).Put(session);
        memory.RememberedSessions(a, first.Id).Put(session);
        memory.ActiveSessions(a, second.Id).Put(session with { PendingRequest = "Otra biblioteca" });
        memory.ActiveSessions(b, foreign.Id).Put(session with { PendingRequest = "Aclaración de B" });
        var restored = new AccountAgentMemory(postgres.Application).ActiveSessions(a, first.Id).Get(id)!;
        Assert.Equal("NOT_GRANTED", restored.Approval); Assert.Equal(1, restored.StorageRevision);
        Assert.Equal(session.PendingAction, restored.PendingAction); Assert.Equal(session.PendingCommand, restored.PendingCommand);
        Assert.Equal("Aclaración de A", restored.PendingRequest);
        Assert.Equal("Otra biblioteca", memory.ActiveSessions(a, second.Id).Get(id)!.PendingRequest);
        Assert.Equal("Aclaración de B", memory.ActiveSessions(b, foreign.Id).Get(id)!.PendingRequest);
        Assert.Throws<LibraryUnavailableException>(() => memory.ActiveSessions(b, first.Id).Get(id));
        var remembered = memory.RememberedSessions(a, first.Id).Get(id)!;
        Assert.Equal("NOT_GRANTED", remembered.Approval); Assert.Null(remembered.PendingAction);
        Assert.Null(remembered.PendingCommand); Assert.Null(remembered.PendingRequest);
        memory.ActiveSessions(a, first.Id).Forget(id);
        Assert.Null(memory.ActiveSessions(a, first.Id).Get(id)); Assert.NotNull(memory.RememberedSessions(a, first.Id).Get(id));
        memory.ActiveSessions(a, first.Id).Put(session); memory.RememberedSessions(a, first.Id).Forget(id);
        Assert.NotNull(memory.ActiveSessions(a, first.Id).Get(id)); Assert.NotNull(memory.ActiveSessions(b, foreign.Id).Get(id));
    }

    [Fact]
    public async Task SourceAndDraftSessionReferencesCannotCrossLibraryOrAccount()
    {
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope(); var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var own = await access.CreateLibrary(a, "Origen"); var other = await access.CreateLibrary(a, "Otra");
        var foreign = await access.CreateLibrary(b, "Ajena"); var memory = new AccountAgentMemory(postgres.Application);
        var prompt = memory.Library(a, own.Id).Create(new("Origen", "Explica <tema>."));
        var source = memory.Catalog(a, own.Id).Get(prompt.Id); var id = Guid.NewGuid();
        var session = new AgentSession(id, source, null, [], DateTime.UtcNow);
        memory.ActiveSessions(a, own.Id).Put(session);
        Assert.Throws<LibraryUnavailableException>(() => memory.ActiveSessions(a, other.Id).Put(session));
        Assert.Throws<LibraryUnavailableException>(() => memory.ActiveSessions(b, foreign.Id).Put(session));
        var draft = EditorDraftPreparation.Compile(new(null, null, "Borrador", "Texto", [], Guid.NewGuid()), memory.Catalog(a, own.Id), new DeterministicPromptEvaluator());
        Assert.Throws<ValidationException>(() => memory.ActiveSessions(a, own.Id).Put(session with { Draft = draft }));
        Assert.Throws<ValidationException>(() => memory.ActiveSessions(a, own.Id).Put(session with { Id = Guid.Empty }));
        Assert.Null(memory.ActiveSessions(a, other.Id).Get(id));
    }

    [Fact]
    public async Task ConcurrentDraftObservationsThroughSeparateAdaptersPreserveEveryRevision()
    {
        var actor = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var library = await scope.ServiceProvider.GetRequiredService<LibraryAccess>().CreateLibrary(actor, "Transiciones");
        await using var pool = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(postgres.ApplicationConnection) { MaxPoolSize = 4 }.ConnectionString);
        var memory = new AccountAgentMemory(pool); var id = Guid.NewGuid();
        PreparedDraft Draft(string body) => EditorDraftPreparation.Compile(new(null, null, "Borrador", body, [], id),
            memory.Catalog(actor, library.Id), new DeterministicPromptEvaluator());
        memory.ActiveSessions(actor, library.Id).ObserveDraft(new(id, null, Draft("Inicial"), [], DateTime.UtcNow));
        var observations = Enumerable.Range(1, 20).Select(index => new AgentSession(id, null, Draft($"Contenido {index}"), [], DateTime.UtcNow)).ToArray();
        await Task.WhenAll(observations.Select(item => Task.Run(() => new AccountAgentMemory(pool).ActiveSessions(actor, library.Id).ObserveDraft(item))));
        var current = new AccountAgentMemory(postgres.Application).ActiveSessions(actor, library.Id).Get(id)!;
        Assert.Equal(21, current.DraftRevision); Assert.Equal(21, current.StorageRevision);
        Assert.Contains(current.Draft!.TemplateText, observations.Select(x => x.Draft!.TemplateText));
        Assert.Equal("NOT_GRANTED", current.Approval);
    }

    [Fact]
    public async Task ActiveExpiryAndOwnerBudgetDoNotEvictOtherOwnersOrRememberedSessions()
    {
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope(); var access = scope.ServiceProvider.GetRequiredService<LibraryAccess>();
        var own = await access.CreateLibrary(a, "Límite activo"); var foreign = await access.CreateLibrary(b, "B conservado");
        var memory = new AccountAgentMemory(postgres.Application); var remembered = Guid.NewGuid(); var idB = Guid.NewGuid(); var oldest = Guid.NewGuid();
        memory.RememberedSessions(a, own.Id).Put(new(remembered, null, null, [], DateTime.UtcNow));
        memory.ActiveSessions(b, foreign.Id).Put(new(idB, null, null, [], DateTime.UtcNow));
        memory.ActiveSessions(a, own.Id).Put(new(oldest, null, null, [], DateTime.UtcNow));
        for (var index = 0; index < 100; index++) memory.ActiveSessions(a, own.Id).Put(new(Guid.NewGuid(), null, null, [], DateTime.UtcNow));
        Assert.Null(memory.ActiveSessions(a, own.Id).Get(oldest)); Assert.NotNull(memory.ActiveSessions(b, foreign.Id).Get(idB));
        await using (var connection = await postgres.Application.OpenConnectionAsync())
        {
            await using var transaction = await connection.BeginTransactionAsync(); await SetActor(connection, a.Id);
            await using var count = new NpgsqlCommand("SELECT count(*) FROM qp_library.agent_sessions", connection);
            Assert.Equal(100L, await count.ExecuteScalarAsync());
            await using var expire = new NpgsqlCommand("UPDATE qp_library.agent_sessions SET \"UpdatedAt\"=now()-interval '3 hours', \"ExpiresAt\"=now()-interval '1 hour'", connection);
            Assert.Equal(100, await expire.ExecuteNonQueryAsync()); await transaction.CommitAsync();
        }
        Assert.Null(memory.ActiveSessions(a, own.Id).Get(oldest));
        await using (var connection = await postgres.Application.OpenConnectionAsync())
        {
            await using var transaction = await connection.BeginTransactionAsync(); await SetActor(connection, a.Id);
            await using var count = new NpgsqlCommand("SELECT count(*) FROM qp_library.agent_sessions", connection);
            Assert.Equal(0L, await count.ExecuteScalarAsync());
        }
        Assert.NotNull(memory.ActiveSessions(b, foreign.Id).Get(idB)); Assert.NotNull(memory.RememberedSessions(a, own.Id).Get(remembered));
    }

    [Fact]
    public async Task LimitedSqlCannotReadForeignSessionsRetargetIdentityOrStoreApproval()
    {
        var a = Actor(await postgres.CreateUser()); var b = Actor(await postgres.CreateUser());
        await using var scope = postgres.Services.CreateAsyncScope();
        var library = await scope.ServiceProvider.GetRequiredService<LibraryAccess>().CreateLibrary(a, "SQL activa");
        var id = Guid.NewGuid(); new AccountAgentMemory(postgres.Application).ActiveSessions(a, library.Id).Put(new(id, null, null, [], DateTime.UtcNow));
        await using var connection = await postgres.Application.OpenConnectionAsync();
        await using (var noActor = new NpgsqlCommand("SELECT count(*) FROM qp_library.agent_sessions", connection)) Assert.Equal(0L, await noActor.ExecuteScalarAsync());
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetActor(connection, b.Id);
            await using var read = new NpgsqlCommand("SELECT count(*) FROM qp_library.agent_sessions", connection);
            Assert.Equal(0L, await read.ExecuteScalarAsync()); await transaction.RollbackAsync();
        }
        foreach (var column in new[] { "OwnerId", "LibraryId", "SessionId" })
        {
            await using var transaction = await connection.BeginTransactionAsync(); await SetActor(connection, a.Id);
            // Identifiers come only from the fixed list above; data remains parameterized.
            await using var retarget = new NpgsqlCommand($"UPDATE qp_library.agent_sessions SET \"{column}\"=@target", connection);
            retarget.Parameters.AddWithValue("target", Guid.NewGuid());
            var error = await Assert.ThrowsAsync<PostgresException>(() => retarget.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState); await transaction.RollbackAsync();
        }
        foreach (var payload in new[] { "{\"approval\":\"GRANTED\"}", "{\"approval\":null}", "{\"id\":null}", "{\"values\":null}" })
        {
            await using var transaction = await connection.BeginTransactionAsync(); await SetActor(connection, a.Id);
            await using var grant = new NpgsqlCommand("UPDATE qp_library.agent_sessions SET \"Payload\"=\"Payload\" || @payload::jsonb WHERE \"SessionId\"=@id", connection);
            grant.Parameters.AddWithValue("payload", payload); grant.Parameters.AddWithValue("id", id);
            var error = await Assert.ThrowsAsync<PostgresException>(() => grant.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState); await transaction.RollbackAsync();
        }
    }

    private static async Task SetActor(NpgsqlConnection connection, Guid actor)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,true)", connection);
        command.Parameters.AddWithValue("actor", actor.ToString()); await command.ExecuteNonQueryAsync();
    }
}
