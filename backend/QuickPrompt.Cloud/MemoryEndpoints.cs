using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Cloud;

public static class MemoryEndpoints
{
    public static void MapAccountMemory(this WebApplication app)
    {
        var preferences = app.MapGroup("/account/preferences").RequireAuthorization();
        preferences.MapGet("/", (HttpContext context, AccountAgentMemory memory) => memory.Preferences(LibraryActor.FromPrincipal(context.User)).Get());
        preferences.MapPost("/", (HttpContext context, PreferenceApproval input, AccountAgentMemory memory) =>
            memory.Preferences(LibraryActor.FromPrincipal(context.User)).Confirm(input)).AddEndpointFilter<AccountCsrf>();
        preferences.MapDelete("/", (HttpContext context, AccountAgentMemory memory) =>
        { memory.Preferences(LibraryActor.FromPrincipal(context.User)).Forget(); return Results.NoContent(); }).AddEndpointFilter<AccountCsrf>();

        var contexts = app.MapGroup("/api/accounts/v1/libraries/{libraryId:guid}/memory").RequireAuthorization();
        contexts.MapGet("/sessions", (HttpContext context, Guid libraryId, AccountAgentMemory memory) =>
            memory.List(LibraryActor.FromPrincipal(context.User), libraryId));
        contexts.MapGet("/sessions/{id:guid}", (HttpContext context, Guid libraryId, Guid id, AccountEditorMemory memory) =>
            memory.Read(LibraryActor.FromPrincipal(context.User), libraryId, id));
        contexts.MapPost("/editor", (HttpContext context, Guid libraryId, RememberEditorContext input, AccountEditorMemory memory) =>
            memory.Save(LibraryActor.FromPrincipal(context.User), libraryId, input)).AddEndpointFilter<AccountCsrf>();
        contexts.MapPost("/editor/inspect", (HttpContext context, Guid libraryId, InspectAccountEditor input, AccountEditorMemory memory) =>
            memory.Inspect(LibraryActor.FromPrincipal(context.User), libraryId, input)).AddEndpointFilter<AccountCsrf>();
        contexts.MapDelete("/sessions/{id:guid}", (HttpContext context, Guid libraryId, Guid id, AccountAgentMemory memory) =>
        { memory.RememberedSessions(LibraryActor.FromPrincipal(context.User), libraryId).Forget(id); return Results.NoContent(); }).AddEndpointFilter<AccountCsrf>();
    }
}
