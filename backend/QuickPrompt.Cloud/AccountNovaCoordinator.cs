using System.Collections.Concurrent;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Cloud;

public sealed record AccountLiveStart(Guid ConnectionId, LiveStartInput Request);

/// <summary>Host lifetime/composition for the existing visible engine. No model process or actor-global state.</summary>
public sealed partial class AccountNovaCoordinator(IServiceScopeFactory scopes, AccountOrigin origin, ILogger<AccountNovaCoordinator> logger) : IHostedService, IAsyncDisposable
{
    private readonly record struct Key(Guid Owner, Guid Library, Guid Run);
    private readonly ConcurrentDictionary<Key, Entry> entries = new();
    private readonly object admissions = new();
    private readonly Guid hostToken = Guid.NewGuid();
    private bool stopping;
    private Task? shutdown;

    private sealed class Entry(IServiceScope scope, Guid runId, LibraryActor actor, Guid library, Guid connection,
        AccountLiveRunStore store, LiveRuns live, SameTabUiDispatcher dispatcher, AccountNovaInference inference, AgentOrchestrator engine)
    {
        public readonly IServiceScope Scope = scope;
        public readonly Guid RunId = runId;
        public readonly LibraryActor Actor = actor;
        public readonly Guid Library = library;
        public readonly Guid Connection = connection;
        public readonly AccountLiveRunStore Store = store;
        public readonly LiveRuns Live = live;
        public readonly SameTabUiDispatcher Dispatcher = dispatcher;
        public readonly AccountNovaInference Inference = inference;
        public readonly AgentOrchestrator Engine = engine;
        public Task Work = Task.CompletedTask;
        public DateTime? FinishedAt;
    }

    public LiveRecord Start(LibraryActor actor, Guid libraryId, AccountLiveStart request, string surface)
    {
        var input = request.Request;
        if (request.ConnectionId == Guid.Empty || input is null || input.Command is null || input.Mode != "SAME_TAB" || surface.Length is < 32 or > 128)
            throw new ValidationException("Elige una acción, biblioteca, equipo vinculado y pestaña de NOVA.");
        lock (admissions)
        {
            if (stopping) throw new ConflictException("El servicio se está deteniendo. No se inició una ejecución.");
            Trim();
            var key = new Key(actor.Id, libraryId, input.RunId);
            if (entries.TryGetValue(key, out var existing))
            {
                existing.Dispatcher.RequireSurface(input.RunId, surface);
                if (existing.Connection != request.ConnectionId) throw new ConflictException("La ejecución pertenece a otra conexión.");
                return existing.Live.Create(input); // Original fingerprint, no second launch.
            }
            if (entries.Count >= 1024 || entries.Values.Count(entry => entry.FinishedAt is null) >= 64)
                throw new ExecutionQuotaException("AGENT_HOST_FULL");
            var scope = scopes.CreateScope();
            AccountNovaInference? inference = null;
            try
            {
                var services = scope.ServiceProvider;
                var memory = services.GetRequiredService<AccountAgentMemory>();
                var store = Store(services, actor, libraryId, request.ConnectionId);
                if (store.Get(input.RunId) is not null)
                {
                    var original = AccountLiveRunStore.Reopen(memory, services.GetRequiredService<ExecutionQueue>(), services.GetRequiredService<IProposalConnectionAuthority>(),
                        actor, libraryId, input.RunId, hostToken);
                    if (original.ConnectionId != request.ConnectionId) throw new ConflictException("La ejecución pertenece a otra conexión.");
                    // Restarts/another host preserve metadata but never reconstruct controls or re-run a task.
                    var replay = new LiveRuns(store, memory.Library(actor, libraryId)).Create(input);
                    scope.Dispose(); return replay;
                }
                var library = memory.Library(actor, libraryId);
                if (input.SourceId is { } sourceId)
                {
                    var source = library.Get(sourceId);
                    if (source.IsArchived || source.Revision != input.SourceRevision) throw new ConflictException("El origen cambió. Selecciona su versión actual.");
                }
                var sessions = memory.ActiveSessions(actor, libraryId);
                var retriever = memory.Retriever(actor, libraryId);
                var interpreter = new ActionChoiceInterpreter();
                inference = new(services.GetRequiredService<AccountModelProposals>(), actor, libraryId, request.ConnectionId);
                var planner = new NovaTaskPlanner(interpreter, retriever, sessions, inference.Candidates, scopeInterpreter: inference.InputScope);
                var engine = new AgentOrchestrator(interpreter, retriever, inference.Generator, new GroundedVariableResolver(),
                    new DeterministicPromptEvaluator(), null, new(), sessions: sessions, preferences: memory.Preferences(actor),
                    candidateSelector: inference.Candidates, taskPlanner: planner, stopAfterVerifiedResult: true, executeGroundedPlan: true,
                    inputExtractor: inference.InputValues, adviceResponder: inference.Advice);
                var live = new LiveRuns(store, library);
                var dispatcher = new SameTabUiDispatcher(live);
                var run = live.Create(input); dispatcher.Bind(input.RunId, surface);
                var entry = new Entry(scope, input.RunId, actor, libraryId, request.ConnectionId, store, live, dispatcher, inference, engine);
                if (!entries.TryAdd(key, entry)) throw new ConflictException("La ejecución ya está registrada.");
                if (live.TryLaunch(input.RunId)) entry.Work = Task.Run(() => Run(entry, input, library));
                return run;
            }
            catch { inference?.Dispose(); scope.Dispose(); throw; }
        }
    }

    private AccountLiveRunStore Store(IServiceProvider services, LibraryActor actor, Guid library, Guid connection) =>
        new(services.GetRequiredService<AccountAgentMemory>(), services.GetRequiredService<ExecutionQueue>(),
            services.GetRequiredService<IProposalConnectionAuthority>(), actor, library, connection, hostToken);

    private T ReadStored<T>(LibraryActor actor, Guid library, Guid id, Func<AccountLiveRunStore, T> read)
    {
        using var scope = scopes.CreateScope(); var services = scope.ServiceProvider;
        return read(AccountLiveRunStore.Reopen(services.GetRequiredService<AccountAgentMemory>(), services.GetRequiredService<ExecutionQueue>(),
            services.GetRequiredService<IProposalConnectionAuthority>(), actor, library, id, hostToken));
    }

    public LiveView View(LibraryActor actor, Guid library, Guid id)
    {
        if (entries.TryGetValue(new(actor.Id, library, id), out var entry)) return entry.Live.View(id);
        return ReadStored(actor, library, id, store => new LiveRuns(store, scopeLibrary(actor, library)).View(id));

        PromptLibrary scopeLibrary(LibraryActor owner, Guid selected)
        { using var scope = scopes.CreateScope(); return scope.ServiceProvider.GetRequiredService<AccountAgentMemory>().Library(owner, selected); }
    }

    public LiveEvent[] Events(LibraryActor actor, Guid library, Guid id, long after) =>
        entries.TryGetValue(new(actor.Id, library, id), out var entry) ? entry.Live.Events(id, after) : ReadStored(actor, library, id, store => store.Events(id, after));

    public LiveRecord Record(LibraryActor actor, Guid library, Guid id) => entries.TryGetValue(new(actor.Id, library, id), out var entry)
        ? entry.Live.Get(id) : ReadStored(actor, library, id, store => store.Get(id) ?? throw new MissingPromptException());

    private Entry BoundSurface(LibraryActor actor, Guid library, Guid id, string capability)
    {
        if (!entries.TryGetValue(new(actor.Id, library, id), out var entry))
        { ReadStored(actor, library, id, store => store.Get(id)); throw new ConflictException("Esta ejecución ya no tiene controles activos en este proceso."); }
        entry.Live.Get(id); entry.Dispatcher.RequireSurface(id, capability);
        return entry;
    }

    private Entry Surface(LibraryActor actor, Guid library, Guid id, string capability)
    {
        var entry = BoundSurface(actor, library, id, capability);
        if (!entry.Store.Heartbeat(id)) throw new ConflictException("La ejecución ya terminó.");
        return entry;
    }

    public Task StartAsync(CancellationToken cancellation) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellation)
    {
        lock (admissions) { stopping = true; return shutdown ??= StopInside(cancellation); }
    }

    public ValueTask DisposeAsync() => new(StopAsync(CancellationToken.None));

    private async Task StopInside(CancellationToken cancellation)
    {
        var active = entries.Values.ToArray();
        foreach (var entry in active) Stop(entry, "HOST_STOPPED");
        try { await Task.WhenAll(active.Select(entry => entry.Work)).WaitAsync(TimeSpan.FromSeconds(10), cancellation); }
        catch (Exception error) when (error is OperationCanceledException or TimeoutException) { }
        foreach (var entry in active) await Dispose(entry);
        entries.Clear();
    }

    private void Trim()
    {
        foreach (var pair in entries.Where(pair => pair.Value.FinishedAt < DateTime.UtcNow.AddHours(-2)).ToArray())
            if (entries.TryRemove(pair.Key, out var removed)) Dispose(removed).AsTask().GetAwaiter().GetResult();
    }

    private static async ValueTask Dispose(Entry entry)
    { await entry.Dispatcher.DisposeAsync(); entry.Inference.Dispose(); entry.Scope.Dispose(); }

    private async Task Run(Entry entry, LiveStartInput input, PromptLibrary library)
    {
        using var finished = new CancellationTokenSource();
        var heartbeat = Pulse(entry, input.RunId, finished.Token);
        try { await entry.Engine.RunVisible(input, origin.Value.GetLeftPart(UriPartial.Authority), entry.Live, entry.Inference.UiDecision, entry.Dispatcher.Factory(), library); }
        catch (Exception error)
        {
            logger.LogWarning("Visible account task stopped unexpectedly: {ExceptionType}", error.GetType().Name);
            Stop(entry, "VISIBLE_HOST_FAILED");
        }
        finally
        {
            finished.Cancel();
            try { await heartbeat; } catch (OperationCanceledException) { }
            entry.FinishedAt = DateTime.UtcNow;
        }
    }

    private async Task Pulse(Entry entry, Guid id, CancellationToken cancellation)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellation);
                if (LiveRuns.Terminal(entry.Live.Get(id).Status)) return;
                if (!entry.Store.Heartbeat(id)) { Stop(entry, "HOST_LEASE_EXPIRED"); return; }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        { logger.LogWarning("Visible account heartbeat stopped: {ExceptionType}", error.GetType().Name); Stop(entry, "AGENT_ACCESS_LOST"); }
    }

    private static void Stop(Entry entry, string reason)
    {
        var control = entry.Live.Active(entry.RunId);
        try
        {
            var id = control.Input.RunId;
            if (!LiveRuns.Terminal(entry.Live.Get(id).Status)) entry.Live.Emit(id, LiveStatus.FAILED, LiveActor.SYSTEM_VALIDATION,
                "NOVA perdió su turno o autorización. No se repetirán acciones.", error: reason);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        { /* Persistence remains authoritative; a lost lease is recovered on the next authorized read. */ }
        finally { control.Cancellation.Cancel(); control.Resumed.TrySetResult(); control.ApprovalDecision.TrySetResult(false); control.DeletionDecision.TrySetResult(false); }
    }
}
