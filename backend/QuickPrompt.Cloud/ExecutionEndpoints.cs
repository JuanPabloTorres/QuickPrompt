using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Cloud;

public static class ExecutionEndpoints
{
    public static void MapAccountExecutions(this WebApplication app)
    {
        var library = app.MapGroup("/api/accounts/v1/libraries/{libraryId:guid}").RequireAuthorization();
        library.MapPost("/prompts/{promptId:guid}/render", (HttpContext context, Guid libraryId, Guid promptId, CompileInput input, AccountExecutionAccess access) =>
            access.Preview(LibraryActor.FromPrincipal(context.User), libraryId, promptId, input)).AddEndpointFilter<AccountCsrf>();
        library.MapPost("/prompts/{promptId:guid}/compile", async (HttpContext context, Guid libraryId, Guid promptId, CompileSubmission input,
            AccountExecutionAccess access, CancellationToken cancellation) =>
        {
            var submitted = await access.Compile(LibraryActor.FromPrincipal(context.User), libraryId, promptId, input, cancellation);
            AccountOperations.SubmissionAccepted(context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("QuickPrompt.Cloud.Operations"), submitted.Kind, submitted.Status);
            return Results.Accepted(value: submitted);
        }).AddEndpointFilter<AccountCsrf>();
        library.MapGet("/executions", (HttpContext context, Guid libraryId, AccountExecutionAccess access) => access.List(LibraryActor.FromPrincipal(context.User), libraryId));
        library.MapGet("/executions/events", AccountExecutionFeed.Open).RequireRateLimiting("credentials");
        library.MapGet("/executions/{id:guid}", (HttpContext context, Guid libraryId, Guid id, AccountExecutionAccess access) => access.Read(LibraryActor.FromPrincipal(context.User), libraryId, id));
        library.MapGet("/executions/{id:guid}/result", (HttpContext context, Guid libraryId, Guid id, AccountExecutionAccess access) => access.Result(LibraryActor.FromPrincipal(context.User), libraryId, id));
        library.MapPost("/executions/{id:guid}/cancel", (HttpContext context, Guid libraryId, Guid id, AccountExecutionAccess access) => access.Cancel(LibraryActor.FromPrincipal(context.User), libraryId, id)).AddEndpointFilter<AccountCsrf>();
        library.MapDelete("/executions/{id:guid}/content", (HttpContext context, Guid libraryId, Guid id, AccountExecutionAccess access) =>
            access.Forget(LibraryActor.FromPrincipal(context.User), libraryId, id)).AddEndpointFilter<AccountCsrf>();
    }
}
