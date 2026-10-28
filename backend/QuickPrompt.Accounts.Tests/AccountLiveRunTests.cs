using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    private AccountLiveRunStore VisibleStore(IServiceProvider services, LibraryActor actor, Guid library, Guid connection, Guid? token = null) =>
        new(new AccountAgentMemory(postgres.Application), services.GetRequiredService<ExecutionQueue>(),
            services.GetRequiredService<IProposalConnectionAuthority>(), actor, library, connection, token ?? Guid.NewGuid());

    private static LiveRecord NewVisibleRecord(Guid? run = null) => new(run ?? Guid.NewGuid(), Guid.NewGuid(), LiveRuns.Hash(Guid.NewGuid()),
        "SAME_TAB", LiveStatus.CREATED, 0, DateTime.UtcNow);

    [Fact]
    public async Task VisibleRunAdmissionIsPrivateAcrossHostsAndDoesNotOccupyTheChildInferenceSlot()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App);
        using var deviceA = Client(host.App); using var deviceB = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var ownA = await LocalLibrary(a); var ownB = await LocalLibrary(b);
        var grantA = await ProposalDevice(a, deviceA); var grantB = await ProposalDevice(b, deviceB);
        var actorA = await ProposalActor(a); var actorB = await ProposalActor(b);
        await using var scope = host.App.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var firstHost = VisibleStore(services, actorA, ownA.Library, grantA.Connection.Id);
        var secondHost = VisibleStore(services, actorA, ownA.Library, grantA.Connection.Id);
        var otherOwner = VisibleStore(services, actorB, ownB.Library, grantB.Connection.Id);
        var record = NewVisibleRecord(); Assert.True(firstHost.TryCreate(record));
        Assert.Throws<ConflictException>(() => secondHost.TryCreate(NewVisibleRecord()));
        Assert.True(otherOwner.TryCreate(NewVisibleRecord())); Assert.Null(otherOwner.Get(record.RunId));
        Assert.Throws<MissingPromptException>(() => otherOwner.Events(record.RunId, 0));
        Assert.Throws<ConflictException>(() => secondHost.Heartbeat(record.RunId));
        var foreign = VisibleStore(services, actorB, ownA.Library, grantB.Connection.Id);
        Assert.Throws<LibraryUnavailableException>(() => foreign.TryCreate(NewVisibleRecord()));
        var proposals = services.GetRequiredService<AccountModelProposals>();
        var child = await proposals.Submit(actorA, ownA.Library, grantA.Connection.Id, Guid.NewGuid(), ProposalPacket());
        using var response = await ClaimProposal(deviceA); response.EnsureSuccessStatusCode();
        var lease = (await response.Content.ReadFromJsonAsync<LocalProposalLease>())!;
        Assert.Equal(child.Id, lease.Id); // OwnerRunning=1 would deadlock if the parent held an inference slot.
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync($"/account/connections/{grantA.Connection.Id}/revoke", new { })).StatusCode);
        Assert.Throws<LibraryUnavailableException>(() => firstHost.Heartbeat(record.RunId));
        firstHost.Cancel(record.RunId); Assert.Equal(LiveStatus.CANCELLED, firstHost.Get(record.RunId)!.Status);
    }

    [Fact]
    public async Task VisibleApprovalAndVersionCommitTogetherAndFailedWritesLeaveApprovalUsable()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var store = VisibleStore(services, actor, source.Library, grant.Connection.Id);
        var library = new AccountAgentMemory(postgres.Application).Library(actor, source.Library);
        var live = new LiveRuns(store, library);
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Revisa esta modificación y pide aprobación.", SourceId: source.Prompt.Id, SourceRevision: 1);
        live.Create(input);
        var material = library.Get(source.Prompt.Id);
        live.ProposeSave(input.RunId, new("create", [], material.Id, material.Revision, material.Title,
            "Explica <tema> con fuentes comprobadas.", [], ["tema"], true, false, null, null, false, false));
        var proposal = live.View(input.RunId).Proposal!;
        live.Approve(input.RunId, new(proposal.Id, proposal.Hash, proposal.Revision, true));
        var approved = live.Get(input.RunId);
        Assert.Throws<InvalidOperationException>(() => store.Commit(approved, proposal, [], target =>
        {
            target.Update(material.Id, proposal.Draft);
            throw new InvalidOperationException("Synthetic failure after version write, before commit receipt.");
        }));
        Assert.Equal(1, library.Get(material.Id).Revision); Assert.Single(library.Versions(material.Id));
        Assert.Equal("APPROVED", store.Get(input.RunId)!.Approval);
        Assert.False(live.Active(input.RunId).ApprovalConsumed);
        var saved = live.Commit(input.RunId, proposal.Draft);
        Assert.Equal(2, saved.Revision); Assert.Equal(proposal.Draft.TemplateText, saved.TemplateText);
        Assert.Equal("CONSUMED", store.Get(input.RunId)!.Approval); Assert.True(store.Get(input.RunId)!.EffectCommitted);
        Assert.Throws<ConflictException>(() => live.Commit(input.RunId, proposal.Draft));
        Assert.Equal(2, library.Versions(material.Id).Length);
        var restarted = VisibleStore(services, actor, source.Library, grant.Connection.Id);
        Assert.True(restarted.Get(input.RunId)!.EffectCommitted);
        Assert.Throws<ConflictException>(() => restarted.Commit(approved, proposal, [], _ => throw new Exception("Must never be reached.")));
    }

    [Fact]
    public async Task CancelledOrRevokedVisibleTurnCannotConsumeAnApprovedProposal()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var store = VisibleStore(services, actor, source.Library, grant.Connection.Id); var record = NewVisibleRecord(); store.TryCreate(record);
        var draft = new PromptInput("Aprobado", "Describe <tema>.");
        var proposal = new LiveApproval(Guid.NewGuid(), LiveRuns.Hash(draft), null, null, draft, "");
        store.Append(record with { Approval = "PENDING", Status = LiveStatus.AWAITING_APPROVAL }, VisibleEvent(record), proposal);
        record = store.Get(record.RunId)!;
        store.Append(record with { Approval = "APPROVED", Status = LiveStatus.TOOL_APPROVED }, VisibleEvent(record), proposal);
        var approved = store.Get(record.RunId)!;
        Assert.Throws<ConflictException>(() => store.Commit(approved, proposal with { Hash = LiveRuns.Hash("changed") }, [], _ => throw new Exception("Must not write.")));
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync($"/account/connections/{grant.Connection.Id}/revoke", new { })).StatusCode);
        Assert.Throws<LibraryUnavailableException>(() => store.Commit(approved, proposal, [], _ => throw new Exception("Must not write.")));
        store.Cancel(record.RunId);
        Assert.Equal(LiveStatus.CANCELLED, store.Get(record.RunId)!.Status); Assert.False(store.Get(record.RunId)!.EffectCommitted);
    }

    [Fact]
    public async Task ExpiredVisibleHostIsInterruptedAndAnotherHostCannotReplayItsApproval()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var store = VisibleStore(services, actor, source.Library, grant.Connection.Id); var record = NewVisibleRecord(); store.TryCreate(record);
        store.Append(record, VisibleEvent(record));
        await using (var db = await postgres.Application.OpenConnectionAsync())
        {
            await using var tx = await db.BeginTransactionAsync();
            await using var expire = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,true); UPDATE qp_operations.agent_runs SET \"LeaseUntil\"=clock_timestamp()-interval '1 second' WHERE \"OwnerId\"=@owner AND \"RunId\"=@run", db, tx);
            expire.Parameters.AddWithValue("actor", actor.Id.ToString()); expire.Parameters.AddWithValue("owner", actor.Id); expire.Parameters.AddWithValue("run", record.RunId);
            await expire.ExecuteNonQueryAsync(); await tx.CommitAsync();
        }
        var restart = VisibleStore(services, actor, source.Library, grant.Connection.Id);
        var recovered = restart.Get(record.RunId)!;
        Assert.Equal(LiveStatus.FAILED, recovered.Status); Assert.Equal("HOST_LEASE_EXPIRED", recovered.ErrorCode);
        Assert.False(recovered.EffectCommitted); Assert.Equal("NONE", recovered.Approval);
        Assert.Equal(new long[] { 1, 2 }, restart.Events(record.RunId, 0).Select(x => x.Sequence));
        Assert.True(restart.TryCreate(NewVisibleRecord()));
        Assert.Throws<ConflictException>(() => store.Append(record, VisibleEvent(record)));
    }

    private static LiveEvent VisibleEvent(LiveRecord record) => new(record.RunId, Guid.NewGuid(), 0, DateTime.UtcNow,
        "Test", LiveActor.SYSTEM_VALIDATION, null, null, record.Status, null, null, "Recibo sintético.");

    [Fact]
    public async Task VisibleRunTablesApplyForcedRlsAndKeepIdentityAndEventsImmutableForTheAppRole()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope();
        var store = VisibleStore(scope.ServiceProvider, actor, source.Library, grant.Connection.Id);
        var record = NewVisibleRecord(); store.TryCreate(record); store.Append(record, VisibleEvent(record));
        await using var db = await postgres.Application.OpenConnectionAsync();
        await using var transaction = await db.BeginTransactionAsync();
        await using (var foreign = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,true)", db, transaction))
        { foreign.Parameters.AddWithValue("actor", Guid.NewGuid().ToString()); await foreign.ExecuteScalarAsync(); }
        foreach (var table in new[] { "agent_runs", "agent_run_events" })
        {
            // Names are a fixed test-owned list. No application filter may hide an RLS failure here.
            await using var rows = new NpgsqlCommand("SELECT count(*) FROM qp_operations." + table + " WHERE \"RunId\"=@run", db, transaction);
            rows.Parameters.AddWithValue("run", record.RunId); Assert.Equal(0L, await rows.ExecuteScalarAsync());
        }
        await using var rights = new NpgsqlCommand("""
            SELECT NOT has_column_privilege(current_user,'qp_operations.agent_runs','OwnerId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_operations.agent_runs','LibraryId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_operations.agent_runs','ConnectionId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_operations.agent_runs','HostToken','UPDATE')
              AND NOT has_table_privilege(current_user,'qp_operations.agent_run_events','UPDATE,DELETE')
              AND NOT has_table_privilege('quickprompt_execution','qp_operations.agent_runs','SELECT,INSERT,UPDATE,DELETE')
              AND (SELECT count(*)=2 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='qp_operations' AND c.relname IN ('agent_runs','agent_run_events') AND c.relrowsecurity AND c.relforcerowsecurity)
            """, db, transaction);
        Assert.Equal(true, await rights.ExecuteScalarAsync());
    }

    [Fact]
    public async Task VisibleEventRetentionErasesOnlyOldTerminalContentOfTheCurrentOwner()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App);
        using var deviceA = Client(host.App); using var deviceB = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var ownA = await LocalLibrary(a); var ownB = await LocalLibrary(b);
        var grantA = await ProposalDevice(a, deviceA); var grantB = await ProposalDevice(b, deviceB);
        var actorA = await ProposalActor(a); var actorB = await ProposalActor(b);
        var oldA = await SeedVisibleEvent(actorA, ownA.Library, grantA.Connection.Id, DateTimeOffset.UtcNow.AddDays(-8));
        var freshA = await SeedVisibleEvent(actorA, ownA.Library, grantA.Connection.Id, DateTimeOffset.UtcNow);
        var oldB = await SeedVisibleEvent(actorB, ownB.Library, grantB.Connection.Id, DateTimeOffset.UtcNow.AddDays(-8));
        await using var scope = host.App.Services.CreateAsyncScope();
        var storeA = VisibleStore(scope.ServiceProvider, actorA, ownA.Library, grantA.Connection.Id);
        Assert.Empty(storeA.Events(oldA, 0)); Assert.Single(storeA.Events(freshA, 0));
        Assert.Equal(LiveStatus.COMPLETED, storeA.Get(oldA)!.Status); // Idempotency metadata survives erasure.
        await using var db = await postgres.Application.OpenConnectionAsync(); await using var transaction = await db.BeginTransactionAsync();
        await using var other = new NpgsqlCommand("SELECT set_config('quickprompt.actor_id',@actor,true); SELECT count(*) FROM qp_operations.agent_run_events WHERE \"RunId\"=@run", db, transaction);
        other.Parameters.AddWithValue("actor", actorB.Id.ToString()); other.Parameters.AddWithValue("run", oldB);
        await using var reader = await other.ExecuteReaderAsync(); await reader.ReadAsync(); await reader.NextResultAsync(); await reader.ReadAsync();
        Assert.Equal(1L, reader.GetInt64(0));
    }

    private async Task<Guid> SeedVisibleEvent(LibraryActor actor, Guid library, Guid connection, DateTimeOffset at)
    {
        var record = NewVisibleRecord() with { Status = LiveStatus.COMPLETED, Sequence = 1, UpdatedAt = at.UtcDateTime };
        var item = VisibleEvent(record) with { Sequence = 1, Timestamp = at.UtcDateTime };
        await using var db = await postgres.Application.OpenConnectionAsync(); await using var transaction = await db.BeginTransactionAsync();
        await using var insert = new NpgsqlCommand("""
            SELECT set_config('quickprompt.actor_id',@actor,true);
            INSERT INTO qp_operations.agent_runs ("OwnerId","RunId","LibraryId","ConnectionId","HostToken","Record","Sequence","Terminal","CreatedAt","LeaseUntil")
              VALUES (@owner,@run,@library,@connection,@host,@record,1,true,@at,@until);
            INSERT INTO qp_operations.agent_run_events ("OwnerId","RunId","Sequence","Payload") VALUES (@owner,@run,1,@event);
            """, db, transaction);
        insert.Parameters.AddWithValue("actor", actor.Id.ToString()); insert.Parameters.AddWithValue("owner", actor.Id);
        insert.Parameters.AddWithValue("run", record.RunId); insert.Parameters.AddWithValue("library", library);
        insert.Parameters.AddWithValue("connection", connection); insert.Parameters.AddWithValue("host", Guid.NewGuid());
        insert.Parameters.AddWithValue("record", NpgsqlTypes.NpgsqlDbType.Jsonb, System.Text.Json.JsonSerializer.Serialize(record));
        insert.Parameters.AddWithValue("event", NpgsqlTypes.NpgsqlDbType.Jsonb, System.Text.Json.JsonSerializer.Serialize(item));
        insert.Parameters.AddWithValue("at", at); insert.Parameters.AddWithValue("until", at.AddSeconds(30));
        await insert.ExecuteNonQueryAsync(); await transaction.CommitAsync(); return record.RunId;
    }

    [Fact]
    public async Task VisibleCommitRejectsAChangedSourceAndRollsBackContentThatDoesNotMatchApproval()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope();
        var store = VisibleStore(scope.ServiceProvider, actor, source.Library, grant.Connection.Id);
        var library = new AccountAgentMemory(postgres.Application).Library(actor, source.Library);
        var record = NewVisibleRecord(); store.TryCreate(record);
        var draft = new PromptInput("Copia aprobada", "Explica <tema>.");
        var proposal = new LiveApproval(Guid.NewGuid(), LiveRuns.Hash(draft), null, null, draft, "") { SourceBasis = new(source.Prompt.Id, 1) };
        store.Append(record with { Approval = "PENDING", Status = LiveStatus.AWAITING_APPROVAL }, VisibleEvent(record), proposal);
        record = store.Get(record.RunId)!;
        store.Append(record with { Approval = "APPROVED", Status = LiveStatus.TOOL_APPROVED }, VisibleEvent(record), proposal);
        var approved = store.Get(record.RunId)!;
        Assert.Throws<ValidationException>(() => store.Commit(approved, proposal, [], target => target.Create(draft with { TemplateText = "Contenido diferente." })));
        Assert.Equal(1, library.Search(new()).Total); Assert.Equal("APPROVED", store.Get(record.RunId)!.Approval);
        library.Update(source.Prompt.Id, new("Origen modificado", "Otro <tema>.", Revision: 1));
        Assert.Throws<LibraryRevisionException>(() => store.Commit(approved, proposal, [], target => target.Create(draft)));
        Assert.Equal(1, library.Search(new()).Total); Assert.False(store.Get(record.RunId)!.EffectCommitted);
    }

    [Fact]
    public async Task VisibleBoundaryObservesCancellationCommittedByAnotherHostWithoutRelyingOnLocalSignals()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner);
        var grant = await ProposalDevice(owner, device); var actor = await ProposalActor(owner);
        await using var scope = host.App.Services.CreateAsyncScope();
        var store = VisibleStore(scope.ServiceProvider, actor, source.Library, grant.Connection.Id);
        var live = new LiveRuns(store, new AccountAgentMemory(postgres.Application).Library(actor, source.Library));
        var input = new LiveStartInput(Guid.NewGuid(), Guid.NewGuid(), "Lee el prompt seleccionado.", SourceId: source.Prompt.Id, SourceRevision: 1);
        live.Create(input);
        VisibleStore(scope.ServiceProvider, actor, source.Library, grant.Connection.Id).Cancel(input.RunId);
        Assert.False(live.Active(input.RunId).Cancellation.IsCancellationRequested);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => live.Boundary(input.RunId, CancellationToken.None));
        Assert.False(live.Get(input.RunId).EffectCommitted);
    }
}
