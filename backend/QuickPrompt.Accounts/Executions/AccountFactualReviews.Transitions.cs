using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Executions;

public sealed partial class AccountFactualReviews
{
    public Task<FactualReviewView> Confirm(LibraryActor actor, Guid library, Guid id, ConfirmFactualReview input, CancellationToken cancellation)
    {
        if (!input.Confirmed) throw new ValidationException("Confirma explícitamente los hechos y sus roles. Esto todavía no guarda el documento.");
        FactualTransform.ValidateTitle(input.Title);
        FactualTransform.ValidateStructure(input.Facts);
        return Change(actor, library, id, (_, row, _) => {
            if (!Pending(row)) return;
            var encoded = JsonSerializer.Serialize(input.Facts, Json);
            if (row.Status == "COMPOSED" && row.Version == input.Version + 1 && Facts(row.ConfirmedFacts).SequenceEqual(input.Facts) && row.Title == input.Title) return;
            Version(row, input.Version);
            if (row.Status is not ("REVIEW" or "COMPOSED")) throw new ConflictException("Espera la propuesta antes de confirmar los hechos.");
            var preview = FactualTransform.Compose(input.Title, input.Facts, Source(row));
            if (preview.Length > 64000) throw new ValidationException("El documento compuesto supera su límite.");
            row.ConfirmedFacts = encoded; row.Edited = !Facts(row.ProposalFacts).SequenceEqual(input.Facts); row.Title = input.Title;
            row.Preview = preview; row.Status = "COMPOSED"; row.Version++;
        }, cancellation);
    }

    /// <summary>Final approval inserts a document and changes review state in the SAME actor transaction. Replays return the same document.</summary>
    public async Task<FactualDocumentView> Approve(LibraryActor actor, Guid library, Guid id, FactualDecision input, CancellationToken cancellation)
    {
        if (!input.Confirmed) throw new ValidationException("Aprueba por separado el documento final que se guardará.");
        var review = await Change(actor, library, id, (db, row, now) => {
            if (row.Status == "APPROVED") return;
            if (!Pending(row)) return;
            Version(row, input.Version);
            if (row.Status != "COMPOSED" || row.Preview is null) throw new ConflictException("Confirma primero los hechos y revisa el documento compuesto.");
            var document = new FactualDocument { Id = Guid.NewGuid(), OwnerId = actor.Id, LibraryId = library, ReviewId = id,
                PromptId = row.PromptId, PromptRevision = row.PromptRevision, Title = row.Title, Text = row.Preview,
                Facts = row.ConfirmedFacts, ProposalFacts = row.ProposalFacts, Edited = row.Edited, Model = LocalWorkProtocol.Model, CreatedAt = now,
                SourceFields = JsonSerializer.Serialize(Source(row).SourceFields, Json) };
            db.FactualDocuments.Add(document);
            db.SaveChanges(); // Insert guard observes COMPOSED. The outer actor transaction still owns both writes.
            row.DocumentId = document.Id;
            // The immutable document owns the approved facts/provenance. A second copy of source values is unnecessary.
            Clear(row, "APPROVED");
        }, cancellation);
        if (review.Status != "APPROVED" || review.DocumentId is null) throw new ConflictException("La revisión perdió vigencia; no se guardó un documento.");
        return Document(actor, library, review.DocumentId.Value);
    }

    public async Task<FactualReviewView> Cancel(LibraryActor actor, Guid library, Guid id, FactualDecision input, CancellationToken cancellation)
    {
        Guid? child = null;
        var review = await Change(actor, library, id, (_, row, _) => {
            if (!Pending(row)) return;
            Version(row, input.Version); child = row.ChildExecutionId; Clear(row, "CANCELLED");
        }, cancellation);
        if (child is { } job) new AccountExecutionAccess(memory, queue).Cancel(actor, library, job);
        return review;
    }

    public FactualDocumentPage Documents(LibraryActor actor, Guid library, int page = 0, int limit = 20)
    {
        if (page is < 0 or > 10000 || limit is < 1 or > 50) throw new ValidationException("Página o límite de documentos inválido.");
        return memory.InScope(actor, library, db => {
            var rows = db.FactualDocuments.AsNoTracking().Where(x => x.OwnerId == actor.Id && x.LibraryId == library);
            return new FactualDocumentPage(rows.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(page * limit).Take(limit)
                .Select(x => new FactualDocumentSummary(x.Id, x.LibraryId, x.ReviewId, x.PromptId, x.PromptRevision, x.Title, x.Edited, x.CreatedAt)).ToArray(), rows.Count(), page, limit);
        });
    }

    public FactualDocumentView Document(LibraryActor actor, Guid library, Guid id) => memory.InScope(actor, library, db =>
        DocumentView(db.FactualDocuments.AsNoTracking().SingleOrDefault(x => x.Id == id && x.OwnerId == actor.Id && x.LibraryId == library) ?? throw new LibraryUnavailableException()));

    /// <summary>Erases this approved artifact and its review evidence, never the source prompt or another execution.</summary>
    public async Task ForgetDocument(LibraryActor actor, Guid library, Guid id, bool confirmed, CancellationToken cancellation)
    {
        if (!confirmed) throw new ValidationException("Confirma el borrado definitivo de este documento.");
        await queue.Fence(() => {
            memory.InScope(actor, library, db => {
                var row = db.FactualDocuments.SingleOrDefault(x => x.Id == id && x.OwnerId == actor.Id && x.LibraryId == library);
                if (row is null) return true;
                var review = db.FactualReviews.Single(x => x.Id == row.ReviewId && x.OwnerId == actor.Id && x.LibraryId == library);
                // This narrowly scoped function does not acquire another queue fence/connection.
                // Child forgetting and artifact deletion commit together under the existing outer fence.
                var erased = db.Database.SqlQuery<int>($"SELECT qp_operations.forget_factual_child({library},{review.Id}) AS \"Value\"").Single();
                if (erased == 3) throw new ConflictException("La propuesta aún no terminó; no se borró el documento.");
                Clear(review, "CANCELLED", "DOCUMENT_DELETED"); review.DocumentId = null; review.Title = "";
                db.FactualDocuments.Remove(row); db.SaveChanges(); return true;
            }); return Task.FromResult(true);
        }, cancellation);
    }

    private static FactualDocumentView DocumentView(FactualDocument row) => new(row.Id, row.LibraryId, row.ReviewId, row.PromptId,
        row.PromptRevision, row.Title, row.Text, Facts(row.Facts), Facts(row.ProposalFacts), row.Edited, row.Model, row.CreatedAt,
        JsonSerializer.Deserialize<Dictionary<string, string>>(row.SourceFields, Json)!);
}
