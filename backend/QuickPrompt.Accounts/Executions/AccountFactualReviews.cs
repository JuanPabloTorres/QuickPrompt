using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Executions;

/// <summary>Shared PostgreSQL review authority. No inference or approval state is kept in a host dictionary.</summary>
public sealed partial class AccountFactualReviews(AccountAgentMemory memory, ExecutionQueue queue, AccountModelProposals proposals,
    IProposalConnectionAuthority connections, IFactualProposalCodec codec)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static bool Pending(FactualReview row) => row.Status is "ADMITTED" or "PROPOSING" or "REVIEW" or "COMPOSED";
    private static DateTimeOffset Clock(AccountDatabase db) => db.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").Single();
    private static FactualSource Source(FactualReview row) => JsonSerializer.Deserialize<FactualSource>(row.Source, Json) ?? throw new ConflictException("La fuente de esta revisión ya no está disponible.");
    private static FactualStatement[] Facts(string text) => JsonSerializer.Deserialize<FactualStatement[]>(text, Json)!;
    private static FactualReviewView View(FactualReview row) => new(row.Id, row.LibraryId, row.PromptId, row.PromptRevision, row.ConnectionId,
        row.Version, row.Status, row.Title, row.Source == "null" ? [] : Source(row).SourceFields, Facts(row.ProposalFacts), Facts(row.ConfirmedFacts),
        row.Preview, row.DocumentId, row.Edited, row.ExpiresAt, row.ErrorCode);
    private static void Clear(FactualReview row, string status, string? error = null)
    {
        row.Status = status; row.ErrorCode = error; row.Source = "null"; row.ProposalFacts = "[]"; row.ConfirmedFacts = "[]";
        row.Preview = null; row.Version++;
        row.Title = "";
    }
    private static void Version(FactualReview row, long expected)
    { if (row.Version != expected) throw new ConflictException("La revisión cambió. Consulta su estado antes de decidir."); }
    private static LibraryPrompt? Current(AccountDatabase db, Guid library, Guid prompt) => db.Prompts
        .FromSqlInterpolated($"SELECT * FROM qp_library.prompts WHERE \"LibraryId\"={library} AND \"Id\"={prompt} FOR SHARE")
        .IgnoreQueryFilters().AsNoTracking().SingleOrDefault();

    public async Task<FactualReviewView> Start(LibraryActor actor, Guid library, StartFactualReview input, CancellationToken cancellation)
    {
        Validate(input); var fingerprint = Hash(JsonSerializer.Serialize(input with { Values = input.Values.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary() }, Json));
        var row = await queue.Fence(async () => {
            if (!await connections.ProposalOwned(actor, input.ConnectionId, cancellation)) throw new LibraryUnavailableException();
            return memory.InScope(actor, library, db => {
                var now = Clock(db);
                var previous = db.FactualReviews.SingleOrDefault(x => x.OwnerId == actor.Id && x.OperationId == input.OperationId);
                if (previous is not null)
                {
                    if (previous.LibraryId != library || previous.Fingerprint != fingerprint) throw new ConflictException("La operación pertenece a otro encargo factual.");
                    return previous;
                }
                CompanionAvailability.Require(db, actor.Id, input.ConnectionId, now);
                foreach (var old in db.FactualReviews.Where(x => x.OwnerId == actor.Id && x.ExpiresAt <= now && x.Status != "APPROVED").ToArray())
                    if (Pending(old)) Clear(old, "EXPIRED");
                if (db.FactualReviews.Any(x => x.OwnerId == actor.Id && x.ExpiresAt > now && new[] { "ADMITTED", "PROPOSING", "REVIEW", "COMPOSED" }.Contains(x.Status)))
                    throw new ConflictException("Termina o cancela tu revisión factual activa.");
                var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
                if (db.FactualReviews.Count(x => x.OwnerId == actor.Id && x.CreatedAt >= day) >= queue.Limits.DailyAdmissions)
                    throw new ExecutionQuotaException("DAILY_FACTUAL_LIMIT");
                var prompt = Current(db, library, input.PromptId) ?? throw new MissingPromptException();
                if (prompt.DeletedAt is not null || prompt.IsArchived || prompt.Revision != input.Revision) throw new LibraryRevisionException();
                var names = TemplateCompiler.Names(prompt.Template);
                if (input.SourceFields.Any(name => !names.Contains(name, StringComparer.Ordinal))) throw new ValidationException("La fuente debe usar variables reales de esta plantilla.");
                var compiled = EditorTools.Compile(prompt.Template, input.Values);
                if (compiled.Text is null) throw new ValidationException("Completa las variables de la fuente.");
                var source = new FactualSource(prompt.Id, prompt.Revision, prompt.Template, new(input.Values), input.SourceFields.ToDictionary(name => name, name => input.Values[name]), compiled.Text);
                _ = codec.Packet(source); // Validate complete byte/schema budget before admission, never truncate.
                var encodedSource = JsonSerializer.Serialize(source, Json);
                if (Encoding.UTF8.GetByteCount(encodedSource) > 128000) throw new ValidationException("La instantánea de fuente factual supera su límite; no se truncará.");
                var created = new FactualReview { Id = Guid.NewGuid(), OwnerId = actor.Id, LibraryId = library, PromptId = prompt.Id,
                    PromptRevision = prompt.Revision, ConnectionId = input.ConnectionId, OperationId = input.OperationId, ChildOperationId = Guid.NewGuid(),
                    Source = encodedSource, Title = input.Title, Fingerprint = fingerprint, CreatedAt = now, ExpiresAt = now.AddMinutes(15) };
                db.FactualReviews.Add(created); db.SaveChanges(); return created;
            });
        }, cancellation);
        var currentView = await Change(actor, library, row.Id, (_, _, _) => { }, cancellation);
        if (currentView.Status != "ADMITTED") return await Read(actor, library, row.Id, cancellation);
        try
        {
            var source = Source(row);
            var child = await proposals.Submit(actor, library, row.ConnectionId, row.ChildOperationId, codec.Packet(source), cancellation,
                (_, token) => { token.ThrowIfCancellationRequested();
                    memory.InScope(actor, library, db => {
                        var review = db.FactualReviews.AsNoTracking().Single(x => x.Id == row.Id && x.OwnerId == actor.Id);
                        if (review.Status != "ADMITTED" || review.ExpiresAt <= Clock(db)) throw new ConflictException("La admisión factual perdió vigencia.");
                        var current = Current(db, library, row.PromptId);
                        if (current is null || current.DeletedAt is not null || current.IsArchived || current.Revision != row.PromptRevision || current.Template != source.Template) throw new LibraryRevisionException();
                        return true;
                    }); });
            var attached = await Change(actor, library, row.Id, (_, active, _) => {
                active.ChildExecutionId ??= child.Id;
                if (active.Status == "ADMITTED") { active.ChildExecutionId = child.Id; active.Status = "PROPOSING"; active.Version++; }
            }, cancellation);
            if (attached.Status is not ("PROPOSING" or "REVIEW" or "COMPOSED" or "APPROVED"))
                new AccountExecutionAccess(memory, queue).Cancel(actor, library, child.Id);
            return attached;
        }
        catch (CompanionUnavailableException)
        {
            return await Change(actor, library, row.Id, (_, active, _) => {
                if (active.Status == "ADMITTED" && active.ChildExecutionId is null) Clear(active, "FAILED", "ADMISSION_" + CompanionUnavailableException.Code);
            }, cancellation);
        }
        catch (ExecutionQuotaException error)
        {
            // A known rejection before child admission is terminal and must not strand
            // the owner. Unknown commit outcomes keep ADMITTED for receipt recovery.
            return await Change(actor, library, row.Id, (_, active, _) => {
                if (active.Status == "ADMITTED" && active.ChildExecutionId is null) Clear(active, "FAILED", "ADMISSION_" + error.Code);
            }, cancellation);
        }
        catch (OperationCanceledException) { throw; } // Retry the same admission identity; never start another proposal automatically.
    }

    public async Task<FactualReviewView> Read(LibraryActor actor, Guid library, Guid id, CancellationToken cancellation)
    {
        var view = await Change(actor, library, id, (_, _, _) => { }, cancellation);
        if (view.Status != "PROPOSING") return view;
        var child = memory.InScope(actor, library, db => db.FactualReviews.Where(x => x.Id == id && x.OwnerId == actor.Id).Select(x => x.ChildExecutionId).Single());
        if (child is null) return view;
        var status = new AccountExecutionAccess(memory, queue).Read(actor, library, child.Value);
        if (status.Status is ExecutionStatus.Queued or ExecutionStatus.Running) return view;
        JsonElement? response = status.Status == ExecutionStatus.Completed ? proposals.Result(actor, library, child.Value) : null;
        return await Change(actor, library, id, (_, row, _) => {
            if (row.Status != "PROPOSING" || row.ChildExecutionId != child) return;
            if (response is null) { Clear(row, "FAILED", status.ErrorCode ?? "MODEL_FAILED"); return; }
            try { row.ProposalFacts = JsonSerializer.Serialize(codec.Read(response.Value, Source(row)).Facts, Json); row.Status = "REVIEW"; row.Version++; }
            catch (Exception error) when (error is ValidationException or JsonException or KeyNotFoundException)
            { Clear(row, "FAILED", "INVALID_FACT_PROPOSAL"); }
        }, cancellation);
    }

    private async Task<FactualReviewView> Change(LibraryActor actor, Guid library, Guid id,
        Action<AccountDatabase, FactualReview, DateTimeOffset> change, CancellationToken cancellation)
    {
        Guid? cancel = null;
        var result = await queue.Fence(async () => {
            var connection = memory.InScope(actor, library, db => db.FactualReviews.Where(x => x.Id == id && x.OwnerId == actor.Id && x.LibraryId == library)
                .Select(x => (Guid?)x.ConnectionId).SingleOrDefault()) ?? throw new LibraryUnavailableException();
            var authorized = await connections.ProposalOwned(actor, connection, cancellation);
            return memory.InScope(actor, library, db => {
                var row = db.FactualReviews.FromSqlInterpolated($"SELECT * FROM qp_operations.factual_reviews WHERE \"Id\"={id} AND \"OwnerId\"={actor.Id} AND \"LibraryId\"={library} FOR UPDATE").SingleOrDefault() ?? throw new LibraryUnavailableException();
                var now = Clock(db);
                // Recover only an already-admitted receipt after a crash between the two commits.
                // This observation never submits a packet or repeats an inference.
                if (row.ChildExecutionId is null)
                {
                    row.ChildExecutionId = db.Executions.AsNoTracking().Where(x => x.OwnerId == actor.Id && x.LibraryId == library
                        && x.OperationId == row.ChildOperationId && x.ConnectionId == row.ConnectionId && x.Kind == "ModelProposal")
                        .Select(x => (Guid?)x.Id).SingleOrDefault();
                    if (row.ChildExecutionId is not null && row.Status == "ADMITTED") { row.Status = "PROPOSING"; row.Version++; }
                }
                if (Pending(row))
                {
                    var current = Current(db, library, row.PromptId);
                    if (row.ExpiresAt <= now) { cancel = row.ChildExecutionId; Clear(row, "EXPIRED"); }
                    else if (!authorized || current is null || current.DeletedAt is not null || current.IsArchived || current.Revision != row.PromptRevision)
                    { cancel = row.ChildExecutionId; Clear(row, "INVALIDATED", "SOURCE_OR_CONNECTION_CHANGED"); }
                }
                change(db, row, now); db.SaveChanges(); return View(row);
            });
        }, cancellation);
        if (cancel is { } child) new AccountExecutionAccess(memory, queue).Cancel(actor, library, child);
        return result;
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void Validate(StartFactualReview input)
    {
        if (input is null) throw new ValidationException("Falta el encargo factual.");
        FactualTransform.ValidateTitle(input.Title);
        if (input.OperationId == Guid.Empty || input.PromptId == Guid.Empty || input.ConnectionId == Guid.Empty || input.Revision < 1 || input.Values is null
            || input.Values.Count > 60 || input.Values.Any(x => x.Key.Length > 80 || x.Value is null || x.Value.Length > 8000)
            || input.SourceFields is null || input.SourceFields.Length is < 1 or > 60 || input.SourceFields.Distinct(StringComparer.Ordinal).Count() != input.SourceFields.Length
            || input.SourceFields.Any(name => name is null || !input.Values.TryGetValue(name, out var text) || string.IsNullOrWhiteSpace(text)))
            throw new ValidationException("Elige campos de fuente reales y una conexión NOVA vigente.");
    }
}
