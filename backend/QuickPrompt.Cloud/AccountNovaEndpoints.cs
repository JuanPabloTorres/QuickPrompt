using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Cloud;

public static class AccountNovaEndpoints
{
    public static void MapAccountNova(this WebApplication app)
    {
        var api = app.MapGroup("/api/accounts/v1/libraries/{libraryId:guid}/nova").RequireAuthorization();
        api.MapPost("/sessions/{id:guid}/context", (HttpContext context, Guid libraryId, Guid id, SourceContextInput input, AccountNovaCoordinator nova) =>
        { nova.BindSource(Actor(context), libraryId, id, input); return Results.NoContent(); }).AddEndpointFilter<AccountCsrf>();
        api.MapPost("/live", (HttpContext context, Guid libraryId, AccountLiveStart input, AccountNovaCoordinator nova) =>
            nova.Start(Actor(context), libraryId, input, Surface(context))).AddEndpointFilter<AccountCsrf>();
        api.MapGet("/live/{id:guid}", (HttpContext context, Guid libraryId, Guid id, AccountNovaCoordinator nova) => nova.View(Actor(context), libraryId, id));
        api.MapGet("/live/{id:guid}/activity", (HttpContext context, Guid libraryId, Guid id, long after, AccountNovaCoordinator nova) => nova.Events(Actor(context), libraryId, id, after));
        api.MapGet("/live/{id:guid}/events", AccountNovaFeed.Open).RequireRateLimiting("credentials");
        api.MapGet("/live/{id:guid}/actions", (HttpContext context, Guid libraryId, Guid id, bool visible, AccountNovaCoordinator nova) => nova.Poll(Actor(context), libraryId, id, Surface(context), visible));
        api.MapPost("/live/{id:guid}/actions/progress", (HttpContext context, Guid libraryId, Guid id, UiActionProgress input, AccountNovaCoordinator nova) =>
        { nova.Progress(Actor(context), libraryId, id, Surface(context), input); return Results.NoContent(); }).AddEndpointFilter<AccountCsrf>();
        api.MapPost("/live/{id:guid}/actions/receipt", (HttpContext context, Guid libraryId, Guid id, UiActionReceipt input, AccountNovaCoordinator nova) =>
        { nova.Receipt(Actor(context), libraryId, id, Surface(context), input); return Results.NoContent(); }).AddEndpointFilter<AccountCsrf>();
        api.MapPost("/live/{id:guid}/pause", (HttpContext context, Guid libraryId, Guid id, AccountNovaCoordinator nova) => nova.Pause(Actor(context), libraryId, id, Surface(context))).AddEndpointFilter<AccountCsrf>();
        api.MapPost("/live/{id:guid}/resume", (HttpContext context, Guid libraryId, Guid id, AccountNovaCoordinator nova) => nova.Resume(Actor(context), libraryId, id, Surface(context))).AddEndpointFilter<AccountCsrf>();
        api.MapPost("/live/{id:guid}/cancel", (HttpContext context, Guid libraryId, Guid id, AccountNovaCoordinator nova) => nova.Cancel(Actor(context), libraryId, id)).AddEndpointFilter<AccountCsrf>();
        api.MapPost("/live/{id:guid}/proposal", (HttpContext context, Guid libraryId, Guid id, PromptInput input, AccountNovaCoordinator nova) => nova.Propose(Actor(context), libraryId, id, Surface(context), input)).AddEndpointFilter<AccountCsrf>();
        api.MapPost("/live/{id:guid}/approve", (HttpContext context, Guid libraryId, Guid id, LiveApprovalInput input, AccountNovaCoordinator nova) => nova.Approve(Actor(context), libraryId, id, Surface(context), input)).AddEndpointFilter<AccountCsrf>();
        api.MapPost("/live/{id:guid}/commit", (HttpContext context, Guid libraryId, Guid id, PromptInput input, AccountNovaCoordinator nova) => nova.Commit(Actor(context), libraryId, id, Surface(context), input)).AddEndpointFilter<AccountCsrf>();
    }

    private static LibraryActor Actor(HttpContext context) => LibraryActor.FromPrincipal(context.User);
    private static string Surface(HttpContext context) => context.Request.Headers["X-Nova-Surface"].ToString();
}
