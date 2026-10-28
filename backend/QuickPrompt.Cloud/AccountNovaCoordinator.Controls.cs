using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Cloud;

public sealed partial class AccountNovaCoordinator
{
    public void BindSource(LibraryActor actor, Guid libraryId, Guid sessionId, SourceContextInput input)
    {
        using var scope = scopes.CreateScope(); var services = scope.ServiceProvider;
        services.GetRequiredService<QuickPrompt.Accounts.Executions.ExecutionQueue>().Fence(() =>
        {
            var memory = services.GetRequiredService<QuickPrompt.Accounts.Memory.AccountAgentMemory>();
            memory.RequireIdleConversation(actor, libraryId);
            memory.ActiveSessions(actor, libraryId).BindSource(memory.Catalog(actor, libraryId), sessionId, input);
            return Task.FromResult(true);
        }).GetAwaiter().GetResult();
    }

    public UiActionPoll Poll(LibraryActor actor, Guid library, Guid id, string capability, bool visible)
    {
        if (!entries.TryGetValue(new(actor.Id, library, id), out var entry)) return new(View(actor, library, id).Run, null);
        entry = BoundSurface(actor, library, id, capability);
        if (LiveRuns.Terminal(entry.Live.Get(id).Status)) return new(entry.Live.Get(id), null);
        // Completion can win after the read above. A poll then returns terminal metadata,
        // never a conflict that would disguise a successful effect as a failed UI task.
        if (!entry.Store.Heartbeat(id)) return new(entry.Live.Get(id), null);
        return entry.Dispatcher.Poll(id, capability, visible);
    }

    public void Progress(LibraryActor actor, Guid library, Guid id, string capability, UiActionProgress progress) =>
        Surface(actor, library, id, capability).Dispatcher.Progress(id, capability, progress);
    public void Receipt(LibraryActor actor, Guid library, Guid id, string capability, UiActionReceipt receipt)
    {
        var entry = BoundSurface(actor, library, id, capability);
        if (!LiveRuns.Terminal(entry.Live.Get(id).Status)) entry.Store.Heartbeat(id);
        // The dispatcher accepts only the exact previously acknowledged duplicate after
        // termination. An unaccepted or changed receipt still conflicts; no action repeats.
        entry.Dispatcher.Acknowledge(id, capability, receipt);
    }

    public LiveRecord Pause(LibraryActor actor, Guid library, Guid id, string capability)
    { var entry = Surface(actor, library, id, capability); entry.Live.Pause(id); return entry.Live.Get(id); }
    public LiveRecord Resume(LibraryActor actor, Guid library, Guid id, string capability)
    { var entry = Surface(actor, library, id, capability); entry.Live.Resume(id); return entry.Live.Get(id); }
    public LiveRecord Cancel(LibraryActor actor, Guid library, Guid id)
    {
        if (entries.TryGetValue(new(actor.Id, library, id), out var entry))
        {
            try { entry.Live.Cancel(id); }
            finally
            {
                var control = entry.Live.Active(id);
                control.Cancellation.Cancel(); control.Resumed.TrySetResult(); control.ApprovalDecision.TrySetResult(false); control.DeletionDecision.TrySetResult(false);
            }
            return entry.Live.Get(id);
        }
        return ReadStored(actor, library, id, store => { store.Cancel(id); return store.Get(id)!; });
    }
    public LiveView Propose(LibraryActor actor, Guid library, Guid id, string capability, PromptInput draft)
    { var entry = Surface(actor, library, id, capability); entry.Live.RefreshProposal(id, draft); return entry.Live.View(id); }
    public LiveRecord Approve(LibraryActor actor, Guid library, Guid id, string capability, LiveApprovalInput approval)
    { var entry = Surface(actor, library, id, capability); entry.Live.Approve(id, approval); return entry.Live.Get(id); }
    public PromptDocument Commit(LibraryActor actor, Guid library, Guid id, string capability, PromptInput draft) =>
        Surface(actor, library, id, capability).Live.Commit(id, draft);
}
