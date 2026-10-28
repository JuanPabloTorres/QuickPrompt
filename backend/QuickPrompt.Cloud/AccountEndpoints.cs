using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Cloud.Authentication;
using QuickPrompt.Cloud.Delegation;
using QuickPrompt.Accounts.Executions;

namespace QuickPrompt.Cloud;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var account = app.MapGroup("/account");
        account.MapGet("/csrf", (HttpContext context, IAntiforgery csrf) => Results.Ok(new { token = csrf.GetAndStoreTokens(context).RequestToken }));
        account.MapGet("/options", (IAccountMail mail, GoogleAvailability google, DelegationAvailability delegation) => Results.Ok(new
        {
            email = mail.Available,
            google = google.Enabled,
            delegation = delegation.Enabled,
            mailDelivery = mail is DevelopmentAccountMail ? "development-pickup" : mail.Available ? "configured" : "unconfigured"
        }));
        account.MapGoogleAccounts();
        account.MapGet("/me", (HttpContext context) => Results.Ok(new { id = LibraryActor.FromPrincipal(context.User).Id, email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value })).RequireAuthorization();
        account.MapPost("/register", (Registration input, AccountCredentials credentials, CancellationToken cancellation) => credentials.Register(input, cancellation)).AddEndpointFilter<AccountCsrf>().RequireRateLimiting("credentials");
        account.MapPost("/login", (Login input, AccountCredentials credentials) => credentials.Login(input)).AddEndpointFilter<AccountCsrf>().RequireRateLimiting("credentials");
        account.MapPost("/confirm", (TokenConfirmation input, AccountCredentials credentials) => credentials.Confirm(input)).AddEndpointFilter<AccountCsrf>().RequireRateLimiting("credentials");
        account.MapPost("/resend", (RecoveryRequest input, AccountCredentials credentials, CancellationToken cancellation) => credentials.Resend(input, cancellation)).AddEndpointFilter<AccountCsrf>().RequireRateLimiting("credentials");
        account.MapPost("/recover", (RecoveryRequest input, AccountCredentials credentials, CancellationToken cancellation) => credentials.Recover(input, cancellation)).AddEndpointFilter<AccountCsrf>().RequireRateLimiting("credentials");
        account.MapPost("/reset", (PasswordRecovery input, AccountCredentials credentials) => credentials.Reset(input)).AddEndpointFilter<AccountCsrf>().RequireRateLimiting("credentials");
        account.MapPost("/logout", async (SignInManager<AccountUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization().AddEndpointFilter<AccountCsrf>();
        account.MapPost("/logout-all", async (HttpContext context, UserManager<AccountUser> users, SignInManager<AccountUser> signIn, ExecutionQueue queue) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null) return Results.Unauthorized();
            if (!await queue.InvalidateLocalSession(user.Id, async () => (await users.UpdateSecurityStampAsync(user)).Succeeded, context.RequestAborted)) return Results.StatusCode(503);
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization().AddEndpointFilter<AccountCsrf>();
    }

    public static void MapAccountLibraries(this WebApplication app)
    {
        var libraries = app.MapGroup("/api/accounts/v1/libraries").RequireAuthorization();
        libraries.MapGet("/", (HttpContext context, LibraryAccess library, CancellationToken cancellation) =>
            library.ListLibraries(LibraryActor.FromPrincipal(context.User), cancellation));
        libraries.MapPost("/", (HttpContext context, LibraryName input, LibraryAccess library, CancellationToken cancellation) =>
            library.CreateLibrary(LibraryActor.FromPrincipal(context.User), input.Name, cancellation)).AddEndpointFilter<AccountCsrf>();
        libraries.MapGet("/{libraryId:guid}/prompts", (HttpContext context, Guid libraryId, Guid? collectionId, bool? ungrouped, LibraryAccess library, CancellationToken cancellation) =>
            library.ListPrompts(LibraryActor.FromPrincipal(context.User), libraryId, collectionId, ungrouped ?? false, cancellation));
        libraries.MapGet("/{libraryId:guid}/prompts/{promptId:guid}", (HttpContext context, Guid libraryId, Guid promptId, LibraryAccess library, CancellationToken cancellation) =>
            library.GetPrompt(LibraryActor.FromPrincipal(context.User), libraryId, promptId, cancellation));
        libraries.MapGet("/{libraryId:guid}/prompts/{promptId:guid}/versions", (HttpContext context, Guid libraryId, Guid promptId, LibraryAccess library, CancellationToken cancellation) =>
            library.ListVersions(LibraryActor.FromPrincipal(context.User), libraryId, promptId, cancellation));
        libraries.MapGet("/{libraryId:guid}/prompts/{promptId:guid}/share", (HttpContext context, Guid libraryId, Guid promptId, int revision, LibraryAccess library, CancellationToken cancellation) =>
            library.ExportPrompt(LibraryActor.FromPrincipal(context.User), libraryId, promptId, revision, cancellation));
        libraries.MapPost("/{libraryId:guid}/prompts", (HttpContext context, Guid libraryId, PromptContent input, LibraryAccess library, CancellationToken cancellation) =>
            library.SavePrompt(LibraryActor.FromPrincipal(context.User), libraryId, null, input.Revision, input.Title, input.Template, cancellation)).AddEndpointFilter<AccountCsrf>();
        libraries.MapPut("/{libraryId:guid}/prompts/{promptId:guid}", (HttpContext context, Guid libraryId, Guid promptId, PromptContent input, LibraryAccess library, CancellationToken cancellation) =>
            library.SavePrompt(LibraryActor.FromPrincipal(context.User), libraryId, promptId, input.Revision, input.Title, input.Template, cancellation)).AddEndpointFilter<AccountCsrf>();
    }
}

public sealed record LibraryName(string Name);
public sealed record PromptContent(string Title, string Template, int Revision);
