using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Cloud;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    private static JsonElement FactualReply(FactualStatement[] facts) => JsonSerializer.SerializeToElement(new {
        done = true, done_reason = "stop", model = "qwen3:8b", coreRequestId = Guid.NewGuid().ToString("N"), modelCalls = 1,
        prompt_eval_count = 20, eval_count = 30, message = new { content = JsonSerializer.Serialize(new FactualProposal(facts), new JsonSerializerOptions(JsonSerializerDefaults.Web)) }
    });
    private static string FactualRoot(Guid library) => $"/api/accounts/v1/libraries/{library}";

    [Fact]
    public async Task UnchangedFactualStatementsPreserveUneditedProvenanceAndConfirmationReplay()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var grant = await ProposalDevice(owner, device);
        var root = FactualRoot(source.Library);
        var posted = await owner.PostAsJsonAsync(root + "/factual-reviews", new StartFactualReview(Guid.NewGuid(), source.Prompt.Id, source.Prompt.Revision,
            grant.Connection.Id, new() { ["tema"] = "El sensor pertenece a Marta." }, ["tema"], "Inventario sin cambios"));
        posted.EnsureSuccessStatusCode(); var review = (await posted.Content.ReadFromJsonAsync<FactualReviewView>())!;
        var lease = await AwaitProposal(device);
        FactualStatement[] facts = [new("Propiedad", "El sensor pertenece a Marta.", "tema", "El sensor pertenece a Marta.")];
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(device, lease, FactualReply(facts))).StatusCode);
        review = (await owner.GetFromJsonAsync<FactualReviewView>(root + $"/factual-reviews/{review.Id}"))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/confirm", new { version = review.Version, confirmed = true, facts = (object?)null, title = review.Title })).StatusCode);
        var confirmation = new ConfirmFactualReview(review.Version, true, facts, review.Title);
        var first = await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/confirm", confirmation); first.EnsureSuccessStatusCode();
        var composed = (await first.Content.ReadFromJsonAsync<FactualReviewView>())!;
        var replay = await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/confirm", confirmation);
        Assert.False(composed.Edited, $"Identical typed facts must not be marked changed by jsonb formatting. Replay status: {replay.StatusCode}.");
        replay.EnsureSuccessStatusCode();
        Assert.Equal(composed.Version, (await replay.Content.ReadFromJsonAsync<FactualReviewView>())!.Version);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
    }

    [Fact]
    public async Task FactualQuotaRejectionDoesNotStrandAnActiveReviewWithoutAnAdmittedChild()
    {
        await using var host = await Start(configureServices: services => services.AddSingleton(new ExecutionLimits(OwnerQueued: 1)));
        using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var grant = await ProposalDevice(owner, device);
        using var scope = host.App.Services.CreateScope(); var actor = await ProposalActor(owner);
        var proposals = scope.ServiceProvider.GetRequiredService<AccountModelProposals>();
        var occupying = await proposals.Submit(actor, source.Library, grant.Connection.Id, Guid.NewGuid(), ProposalPacket()); // Synthetic queued work; never inferred.
        try
        {
            var input = new StartFactualReview(Guid.NewGuid(), source.Prompt.Id, source.Prompt.Revision, grant.Connection.Id,
                new() { ["tema"] = "Dato explícito." }, ["tema"], "Admisión rechazada");
            var rejected = await owner.PostAsJsonAsync(FactualRoot(source.Library) + "/factual-reviews", input);
            rejected.EnsureSuccessStatusCode();
            var receipt = (await rejected.Content.ReadFromJsonAsync<FactualReviewView>())!;
            Assert.Equal("FAILED", receipt.Status); Assert.Equal("ADMISSION_OWNER_QUEUE_FULL", receipt.ErrorCode); Assert.Empty(receipt.SourceFields);
            var replay = await owner.PostAsJsonAsync(FactualRoot(source.Library) + "/factual-reviews", input); replay.EnsureSuccessStatusCode();
            var repeated = (await replay.Content.ReadFromJsonAsync<FactualReviewView>())!;
            Assert.Equal(receipt.Id, repeated.Id); Assert.Equal(receipt.Version, repeated.Version); Assert.Equal("FAILED", repeated.Status);
            var observed = scope.ServiceProvider.GetRequiredService<AccountAgentMemory>().InScope(actor, source.Library, db => {
                var row = db.FactualReviews.Single(x => x.OperationId == input.OperationId && x.OwnerId == actor.Id);
                return new { row.Status, ChildCount = db.Executions.Count(x => x.OwnerId == actor.Id && x.OperationId == row.ChildOperationId) };
            });
            var next = await owner.PostAsJsonAsync(FactualRoot(source.Library) + "/factual-reviews", input with { OperationId = Guid.NewGuid() });
            Assert.Equal(0, observed.ChildCount);
            Assert.True(observed.Status == "FAILED", $"Known pre-admission rejection left {observed.Status}; next explicit request returned {next.StatusCode}; child count {observed.ChildCount}.");
            next.EnsureSuccessStatusCode();
            var nextReceipt = (await next.Content.ReadFromJsonAsync<FactualReviewView>())!;
            Assert.Equal("FAILED", nextReceipt.Status); Assert.NotEqual(receipt.Id, nextReceipt.Id);
            Assert.Equal("ADMISSION_OWNER_QUEUE_FULL", nextReceipt.ErrorCode); // Same real quota, not a stranded-review conflict.
        }
        finally
        {
            scope.ServiceProvider.GetRequiredService<AccountExecutionAccess>().Cancel(actor, source.Library, occupying.Id);
            await ClaimProposal(device); // Drain only cancellation; no model worker is started.
        }
    }

    [Fact]
    public async Task FactualAdmissionRejectsForeignSourcesConnectionsAndInvalidBudgetsWithoutAChild()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App); using var other = Client(host.App); using var otherDevice = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); await Login(other, await Register(other, host.Mail));
        var source = await LocalLibrary(owner); var foreign = await LocalLibrary(other);
        var grant = await ProposalDevice(owner, device); var foreignGrant = await ProposalDevice(other, otherDevice);
        var root = FactualRoot(source.Library);
        var request = new StartFactualReview(Guid.NewGuid(), source.Prompt.Id, source.Prompt.Revision, grant.Connection.Id,
            new() { ["tema"] = "Fuente propia", ["extra"] = "No es una variable" }, ["extra"], "Documento");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(root + "/factual-reviews", request)).StatusCode);
        request = request with { SourceFields = ["tema"], ConnectionId = foreignGrant.Connection.Id };
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync(root + "/factual-reviews", request)).StatusCode);
        request = request with { ConnectionId = grant.Connection.Id, PromptId = foreign.Prompt.Id };
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync(root + "/factual-reviews", request)).StatusCode);
        request = request with { PromptId = source.Prompt.Id, Values = new() { ["tema"] = new string('x', 5000) } };
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(root + "/factual-reviews", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(otherDevice)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(root + "/factual-documents?limit=0")).StatusCode);
        await using var pool = Npgsql.NpgsqlDataSource.Create(postgres.ExecutionConnection);
        await using var command = pool.CreateCommand("SELECT count(*) FROM qp_library.factual_documents");
        var denied = await Assert.ThrowsAsync<Npgsql.PostgresException>(async () => await command.ExecuteScalarAsync());
        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        await using var changeDocument = postgres.Application.CreateCommand("UPDATE qp_library.factual_documents SET \"Title\"=\"Title\" WHERE false");
        var updateDenied = await Assert.ThrowsAsync<Npgsql.PostgresException>(async () => await changeDocument.ExecuteNonQueryAsync());
        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, updateDenied.SqlState);
        await using var eraseReview = postgres.Application.CreateCommand("DELETE FROM qp_operations.factual_reviews WHERE false");
        var deleteDenied = await Assert.ThrowsAsync<Npgsql.PostgresException>(async () => await eraseReview.ExecuteNonQueryAsync());
        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, deleteDenied.SqlState);
    }

    [Fact]
    public async Task RecordedNonliteralModelQuoteIsReviewableButCannotPassConfirmationUntilExplicitlyCorrected()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var library = await CreateLibrary(owner); var grant = await ProposalDevice(owner, device);
        var root = FactualRoot(library);
        var created = await owner.PostAsJsonAsync(root + "/prompts", new PromptContent("Recorded A source", "Transforma <notas>.", 0)); created.EnsureSuccessStatusCode();
        var source = (await created.Content.ReadFromJsonAsync<AccountPrompt>())!;
        var posted = await owner.PostAsJsonAsync(root + "/factual-reviews", new StartFactualReview(Guid.NewGuid(), source.Id, source.Revision,
            grant.Connection.Id, new() { ["notas"] = FactualTransformTests.OriginalANotes }, ["notas"], "Replay A con revisión"));
        posted.EnsureSuccessStatusCode(); var review = (await posted.Content.ReadFromJsonAsync<FactualReviewView>())!;
        var lease = await AwaitProposal(device);
        var original = JsonSerializer.Deserialize<FactualProposal>(FactualTransformTests.OriginalAProposal, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(device, lease, FactualReply(original.Facts))).StatusCode); // Replay, not inference.
        review = (await owner.GetFromJsonAsync<FactualReviewView>(root + $"/factual-reviews/{review.Id}"))!;
        Assert.Equal("REVIEW", review.Status); Assert.Equal(original.Facts, review.ProposalFacts);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/confirm", new ConfirmFactualReview(review.Version, true, original.Facts, review.Title))).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<FactualDocumentPage>(root + "/factual-documents"))!.Items);
        var confirmed = original.Facts.Select((fact, index) => index == 1 ? fact with { Quote = "Luis revisará las pruebas el 30 de octubre de 2026." } : fact).ToArray();
        var composed = await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/confirm", new ConfirmFactualReview(review.Version, true, confirmed, review.Title)); composed.EnsureSuccessStatusCode();
        review = (await composed.Content.ReadFromJsonAsync<FactualReviewView>())!;
        Assert.Equal("COMPOSED", review.Status); Assert.True(review.Edited);
        Assert.Empty((await owner.GetFromJsonAsync<FactualDocumentPage>(root + "/factual-documents"))!.Items);
        var saved = await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/approve", new FactualDecision(review.Version, true)); saved.EnsureSuccessStatusCode();
        var document = (await saved.Content.ReadFromJsonAsync<FactualDocumentView>())!;
        Assert.Equal(review.Preview, document.Text); Assert.Equal(original.Facts, document.ProposalFacts); Assert.Equal(confirmed, document.Facts);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode); // Confirmation/composition/save did not create another child.
    }

    [Fact]
    public async Task FactualDocumentsNeedTwoDecisionsAndRemainSeparateAcrossHostsAndSourceEdits()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App); using var other = Client(host.App);
        var email = await Register(owner, host.Mail); await Login(owner, email); await Login(other, await Register(other, host.Mail));
        var source = await LocalLibrary(owner); var foreign = await LocalLibrary(other); var grant = await ProposalDevice(owner, device);
        var root = FactualRoot(source.Library);
        const string notes = "Ana terminó el prototipo. Luis revisará pruebas el 30 de octubre.";
        var request = new StartFactualReview(Guid.NewGuid(), source.Prompt.Id, source.Prompt.Revision, grant.Connection.Id,
            new() { ["tema"] = notes }, ["tema"], "Acta con revisión explícita");
        var started = await owner.PostAsJsonAsync(root + "/factual-reviews", request); started.EnsureSuccessStatusCode();
        var review = (await started.Content.ReadFromJsonAsync<FactualReviewView>())!;
        Assert.Equal("PROPOSING", review.Status); Assert.Equal(notes, review.SourceFields["tema"]);
        Assert.Equal(review.Id, (await (await owner.PostAsJsonAsync(root + "/factual-reviews", request)).Content.ReadFromJsonAsync<FactualReviewView>())!.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(root + $"/factual-reviews/{review.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/approve", new FactualDecision(review.Version, true))).StatusCode);
        var lease = await AwaitProposal(device);
        FactualStatement[] wrongProposal = [new("Participantes", "Ana y Luis", "tema", "Ana terminó el prototipo.")];
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(device, lease, FactualReply(wrongProposal))).StatusCode);
        review = (await owner.GetFromJsonAsync<FactualReviewView>(root + $"/factual-reviews/{review.Id}"))!;
        Assert.Equal("REVIEW", review.Status); Assert.Equal(wrongProposal, review.ProposalFacts);
        FactualStatement[] confirmed = [new("Trabajo terminado", "Ana terminó el prototipo.", "tema", "Ana terminó el prototipo."),
            new("Tareas", "Luis revisará pruebas el 30 de octubre.", "tema", "Luis revisará pruebas el 30 de octubre.")];
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/confirm", new ConfirmFactualReview(review.Version, false, confirmed, review.Title))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/confirm", new ConfirmFactualReview(review.Version - 1, true, confirmed, review.Title))).StatusCode);
        var confirmation = new ConfirmFactualReview(review.Version, true, confirmed, review.Title);
        review = (await (await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/confirm", confirmation)).Content.ReadFromJsonAsync<FactualReviewView>())!;
        Assert.Equal("COMPOSED", review.Status); Assert.True(review.Edited); Assert.DoesNotContain("Participantes", review.Preview!);
        Assert.Empty((await owner.GetFromJsonAsync<FactualDocumentPage>(root + "/factual-documents"))!.Items);
        var obsoleteVersion = review.Version;
        var recomposed = await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/confirm", new ConfirmFactualReview(review.Version, true, confirmed, "Acta final revisada"));
        recomposed.EnsureSuccessStatusCode(); review = (await recomposed.Content.ReadFromJsonAsync<FactualReviewView>())!;
        Assert.True(review.Version > obsoleteVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/approve", new FactualDecision(obsoleteVersion, true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/approve", new FactualDecision(review.Version, false))).StatusCode);
        var approval = new FactualDecision(review.Version, true);
        await using var secondHost = await Start(); using var second = Client(secondHost.App); await Csrf(second); await Login(second, email);
        var parallel = await Task.WhenAll(owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/approve", approval),
            second.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/approve", approval));
        foreach (var response in parallel) response.EnsureSuccessStatusCode();
        var saved = parallel[0];
        var document = (await saved.Content.ReadFromJsonAsync<FactualDocumentView>())!;
        Assert.Equal(review.Preview, document.Text); Assert.Equal(confirmed, document.Facts); Assert.Equal(wrongProposal, document.ProposalFacts);
        Assert.Equal(notes, document.SourceFields["tema"]);
        Assert.Equal(document.Id, (await parallel[1].Content.ReadFromJsonAsync<FactualDocumentView>())!.Id);
        var replay = await second.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/approve", approval); replay.EnsureSuccessStatusCode();
        Assert.Equal(document.Id, (await replay.Content.ReadFromJsonAsync<FactualDocumentView>())!.Id);
        Assert.Single((await second.GetFromJsonAsync<FactualDocumentPage>(root + "/factual-documents?limit=1&page=0"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(root + $"/factual-documents/{document.Id}")).StatusCode);
        using (var scope = host.App.Services.CreateScope())
        {
            var actorB = await ProposalActor(other);
            Assert.Equal(0, scope.ServiceProvider.GetRequiredService<AccountAgentMemory>().InScope(actorB, foreign.Library,
                db => db.FactualDocuments.Count(x => x.Id == document.Id))); // Actual FORCE RLS, even without an owner predicate.
        }
        var edit = await owner.PutAsJsonAsync(root + $"/prompts/{source.Prompt.Id}", new PromptContent("Fuente editada", "Explica <tema> con claridad.", source.Prompt.Revision));
        edit.EnsureSuccessStatusCode();
        Assert.Equal(document.Text, (await second.GetFromJsonAsync<FactualDocumentView>(root + $"/factual-documents/{document.Id}"))!.Text);
        var changed = (await edit.Content.ReadFromJsonAsync<AccountPrompt>())!;
        using var delete = new HttpRequestMessage(HttpMethod.Delete, root + $"/prompts/{source.Prompt.Id}") { Content = JsonContent.Create(new { revision = changed.Revision, confirmed = true }) };
        Assert.Equal(HttpStatusCode.NoContent, (await owner.SendAsync(delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync(root + $"/factual-documents/{document.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("edit")]
    [InlineData("revoke")]
    [InlineData("expire")]
    public async Task PendingFactualReviewCannotSaveAfterItsAuthorityOrSourceChanges(string change)
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var grant = await ProposalDevice(owner, device);
        var root = FactualRoot(source.Library);
        var posted = await owner.PostAsJsonAsync(root + "/factual-reviews", new StartFactualReview(Guid.NewGuid(), source.Prompt.Id, source.Prompt.Revision,
            grant.Connection.Id, new() { ["tema"] = "El sensor pertenece a Marta." }, ["tema"], "Inventario revisado"));
        posted.EnsureSuccessStatusCode(); var review = (await posted.Content.ReadFromJsonAsync<FactualReviewView>())!;
        var lease = await AwaitProposal(device);
        FactualStatement[] facts = [new("Propiedad", "El sensor pertenece a Marta.", "tema", "El sensor pertenece a Marta.")];
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(device, lease, FactualReply(facts))).StatusCode);
        review = (await owner.GetFromJsonAsync<FactualReviewView>(root + $"/factual-reviews/{review.Id}"))!;
        var composed = await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/confirm", new ConfirmFactualReview(review.Version, true, facts, review.Title));
        composed.EnsureSuccessStatusCode(); review = (await composed.Content.ReadFromJsonAsync<FactualReviewView>())!;
        if (change == "cancel") (await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/cancel", new FactualDecision(review.Version))).EnsureSuccessStatusCode();
        else if (change == "edit") (await owner.PutAsJsonAsync(root + $"/prompts/{source.Prompt.Id}", new PromptContent("Editado", "Describe <tema>.", source.Prompt.Revision))).EnsureSuccessStatusCode();
        else if (change == "revoke") (await owner.PostAsJsonAsync($"/account/connections/{grant.Connection.Id}/revoke", new { })).EnsureSuccessStatusCode();
        else
        {
            using var scope = host.App.Services.CreateScope(); var actor = await ProposalActor(owner);
            scope.ServiceProvider.GetRequiredService<AccountAgentMemory>().InScope(actor, source.Library, db => {
                db.Database.ExecuteSqlInterpolated($"UPDATE qp_operations.factual_reviews SET \"CreatedAt\"=clock_timestamp()-interval '30 minutes',\"ExpiresAt\"=clock_timestamp()-interval '1 minute' WHERE \"Id\"={review.Id}"); return true;
            });
        }
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/approve", new FactualDecision(review.Version, true))).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<FactualDocumentPage>(root + "/factual-documents"))!.Items);
        var terminal = (await owner.GetFromJsonAsync<FactualReviewView>(root + $"/factual-reviews/{review.Id}"))!;
        Assert.Empty(terminal.SourceFields); Assert.Null(terminal.Preview);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("revoke")]
    [InlineData("edit")]
    [InlineData("crash-cancel")]
    public async Task ActiveFactualChildCannotResurrectAReviewAfterCancellationRevocationOrEdit(string change)
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var grant = await ProposalDevice(owner, device);
        var root = FactualRoot(source.Library);
        var request = new StartFactualReview(Guid.NewGuid(), source.Prompt.Id, source.Prompt.Revision,
            grant.Connection.Id, new() { ["tema"] = "Dato fuente explícito." }, ["tema"], "Revisión cancelable");
        var posted = await owner.PostAsJsonAsync(root + "/factual-reviews", request);
        posted.EnsureSuccessStatusCode(); var review = (await posted.Content.ReadFromJsonAsync<FactualReviewView>())!;
        var lease = await AwaitProposal(device); // Real DB lease is Running; no model is called.
        if (change == "crash-cancel")
        {
            using var scope = host.App.Services.CreateScope(); var actor = await ProposalActor(owner);
            scope.ServiceProvider.GetRequiredService<AccountAgentMemory>().InScope(actor, source.Library, db => {
                // Exact durable shape of the crash window after child admission but before attachment commit.
                db.Database.ExecuteSqlInterpolated($"UPDATE qp_operations.factual_reviews SET \"ChildExecutionId\"=NULL,\"Status\"='ADMITTED',\"Version\"=\"Version\"+1 WHERE \"Id\"={review.Id}"); return true;
            });
            var resumed = await owner.PostAsJsonAsync(root + "/factual-reviews", request); resumed.EnsureSuccessStatusCode();
            var recovered = (await resumed.Content.ReadFromJsonAsync<FactualReviewView>())!;
            Assert.Equal(review.Id, recovered.Id); Assert.Equal("PROPOSING", recovered.Status);
            review = recovered; change = "cancel";
        }
        if (change == "cancel") (await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/cancel", new FactualDecision(review.Version))).EnsureSuccessStatusCode();
        else if (change == "revoke") (await owner.PostAsJsonAsync($"/account/connections/{grant.Connection.Id}/revoke", new { })).EnsureSuccessStatusCode();
        else (await owner.PutAsJsonAsync(root + $"/prompts/{source.Prompt.Id}", new PromptContent("Fuente cambiada", "Describe <tema>.", source.Prompt.Revision))).EnsureSuccessStatusCode();
        var terminal = (await owner.GetFromJsonAsync<FactualReviewView>(root + $"/factual-reviews/{review.Id}"))!;
        Assert.Contains(terminal.Status, new[] { "CANCELLED", "INVALIDATED" });
        var child = (await owner.GetFromJsonAsync<ExecutionView>(root + $"/executions/{lease.Id}"))!;
        Assert.True(child.CancelRequested || child.Status == ExecutionStatus.Cancelled);
        FactualStatement[] lateFacts = [new("Hechos", "Dato fuente explícito.", "tema", "Dato fuente explícito.")];
        var late = await FinishProposal(device, lease, FactualReply(lateFacts));
        Assert.Equal(change == "revoke" ? HttpStatusCode.Unauthorized : HttpStatusCode.NoContent, late.StatusCode);
        var unchanged = (await owner.GetFromJsonAsync<FactualReviewView>(root + $"/factual-reviews/{review.Id}"))!;
        Assert.Equal(terminal.Status, unchanged.Status); Assert.Empty(unchanged.ProposalFacts); Assert.Empty(unchanged.SourceFields);
        Assert.Empty((await owner.GetFromJsonAsync<FactualDocumentPage>(root + "/factual-documents"))!.Items);
        if (change != "revoke") Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
    }

    [Fact]
    public async Task ForgettingOneDocumentAlsoForgetsItsReviewAndChildWithoutDeletingThePrompt()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var source = await LocalLibrary(owner); var grant = await ProposalDevice(owner, device);
        var root = FactualRoot(source.Library);
        var posted = await owner.PostAsJsonAsync(root + "/factual-reviews", new StartFactualReview(Guid.NewGuid(), source.Prompt.Id, source.Prompt.Revision,
            grant.Connection.Id, new() { ["tema"] = "El artículo pertenece a Sara." }, ["tema"], "Propiedad del artículo"));
        posted.EnsureSuccessStatusCode(); var review = (await posted.Content.ReadFromJsonAsync<FactualReviewView>())!;
        var lease = await AwaitProposal(device);
        FactualStatement[] facts = [new("Propiedad", "El artículo pertenece a Sara.", "tema", "El artículo pertenece a Sara.")];
        Assert.Equal(HttpStatusCode.NoContent, (await FinishProposal(device, lease, FactualReply(facts))).StatusCode);
        review = (await owner.GetFromJsonAsync<FactualReviewView>(root + $"/factual-reviews/{review.Id}"))!;
        review = (await (await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/confirm", new ConfirmFactualReview(review.Version, true, facts, review.Title))).Content.ReadFromJsonAsync<FactualReviewView>())!;
        var saved = await owner.PostAsJsonAsync(root + $"/factual-reviews/{review.Id}/approve", new FactualDecision(review.Version, true)); saved.EnsureSuccessStatusCode();
        var document = (await saved.Content.ReadFromJsonAsync<FactualDocumentView>())!;
        using var delete = new HttpRequestMessage(HttpMethod.Delete, root + $"/factual-documents/{document.Id}") { Content = JsonContent.Create(new { confirmed = true }) };
        Assert.Equal(HttpStatusCode.NoContent, (await owner.SendAsync(delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(root + $"/factual-documents/{document.Id}")).StatusCode);
        Assert.Equal(source.Prompt.Id, (await owner.GetFromJsonAsync<AccountPrompt>(root + $"/prompts/{source.Prompt.Id}"))!.Id);
        var erased = (await owner.GetFromJsonAsync<FactualReviewView>(root + $"/factual-reviews/{review.Id}"))!;
        Assert.Empty(erased.SourceFields); Assert.Empty(erased.ConfirmedFacts); Assert.Empty(erased.ProposalFacts); Assert.Null(erased.Preview);
        using var scope = host.App.Services.CreateScope(); var actor = await ProposalActor(owner);
        Assert.True(scope.ServiceProvider.GetRequiredService<AccountAgentMemory>().InScope(actor, source.Library, db =>
            !db.ProposalResults.Any(x => x.ExecutionId == lease.Id) && db.Executions.Any(x => x.Id == lease.Id && x.ForgottenAt != null)));
    }
}
