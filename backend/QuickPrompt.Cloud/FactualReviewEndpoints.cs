using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;

namespace QuickPrompt.Cloud;

public static class FactualReviewEndpoints
{
    public static void MapFactualReviews(this WebApplication app)
    {
        var api = app.MapGroup("/api/accounts/v1/libraries/{libraryId:guid}").RequireAuthorization().RequireRateLimiting("credentials");
        api.MapPost("/factual-reviews", (HttpContext context, Guid libraryId, StartFactualReview input, AccountFactualReviews reviews, CancellationToken ct) =>
            reviews.Start(LibraryActor.FromPrincipal(context.User), libraryId, input, ct)).AddEndpointFilter<AccountCsrf>();
        api.MapGet("/factual-reviews/{id:guid}", (HttpContext context, Guid libraryId, Guid id, AccountFactualReviews reviews, CancellationToken ct) =>
            reviews.Read(LibraryActor.FromPrincipal(context.User), libraryId, id, ct));
        api.MapPost("/factual-reviews/{id:guid}/confirm", (HttpContext context, Guid libraryId, Guid id, ConfirmFactualReview input, AccountFactualReviews reviews, CancellationToken ct) =>
            reviews.Confirm(LibraryActor.FromPrincipal(context.User), libraryId, id, input, ct)).AddEndpointFilter<AccountCsrf>();
        api.MapPost("/factual-reviews/{id:guid}/approve", (HttpContext context, Guid libraryId, Guid id, FactualDecision input, AccountFactualReviews reviews, CancellationToken ct) =>
            reviews.Approve(LibraryActor.FromPrincipal(context.User), libraryId, id, input, ct)).AddEndpointFilter<AccountCsrf>();
        api.MapPost("/factual-reviews/{id:guid}/cancel", (HttpContext context, Guid libraryId, Guid id, FactualDecision input, AccountFactualReviews reviews, CancellationToken ct) =>
            reviews.Cancel(LibraryActor.FromPrincipal(context.User), libraryId, id, input, ct)).AddEndpointFilter<AccountCsrf>();
        api.MapGet("/factual-documents", (HttpContext context, Guid libraryId, int? page, int? limit, AccountFactualReviews reviews) =>
            reviews.Documents(LibraryActor.FromPrincipal(context.User), libraryId, page ?? 0, limit ?? 20));
        api.MapGet("/factual-documents/{id:guid}", (HttpContext context, Guid libraryId, Guid id, AccountFactualReviews reviews) =>
            reviews.Document(LibraryActor.FromPrincipal(context.User), libraryId, id));
        api.MapDelete("/factual-documents/{id:guid}", async (HttpContext context, Guid libraryId, Guid id, [Microsoft.AspNetCore.Mvc.FromBody] ForgetFactualDocument input, AccountFactualReviews reviews, CancellationToken ct) => {
            await reviews.ForgetDocument(LibraryActor.FromPrincipal(context.User), libraryId, id, input.Confirmed, ct); return Results.NoContent();
        }).AddEndpointFilter<AccountCsrf>();
    }
}
