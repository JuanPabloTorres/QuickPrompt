using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    private const string ContentDecision = "Todavía no he decidido si debe conceptualizar software común o sólo AI.";
    private const string ContentRequest = "Prepara una instrucción para conceptualizar ideas de software. "
        + ContentDecision + " Pregúntame antes de redactarla.";
    private const string ContentQuestion = "Antes de redactar la plantilla, aclara esta parte de tu petición:\n«"
        + ContentDecision + "»\n¿Qué eliges o cómo quieres concretarlo?";
    private const string ContentAnswer = "Software común y AI, incluidos Web, App y Spatial 3D.";
    private static string ContentSurface() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    private static AccountLiveStart ContentStart(Guid connection, AccountPrompt? source = null) => new(connection,
        new(Guid.NewGuid(), Guid.NewGuid(), ContentRequest,
            SourceId: source?.Id, SourceRevision: source?.Revision,
            Command: new(source is null ? SemanticOperation.CREATE_DRAFT : SemanticOperation.PROPOSE_ADAPTATION,
                ContentRequest) { InputNames = ["tipo"] }));

    [Fact]
    public async Task HumanContentReplyUsesTheOriginalOwnedRequestAndRequiresFreshReviewWithoutSaving()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var other = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); await Login(other, await Register(other, host.Mail));
        var own = await LocalLibrary(owner); var foreign = await LocalLibrary(other); var grant = await PollingProposalDevice(owner, device);
        var original = ContentStart(grant.Connection.Id); var oldSurface = ContentSurface(); VisibleSurface(owner, oldSurface);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var blocked = await AskContent(owner, device, own.Library, original, null, timeout.Token);
        var firstEvents = host.App.Services.GetRequiredService<AccountNovaCoordinator>()
            .Events(await ProposalActor(owner), own.Library, original.Request.RunId, 0);
        Assert.Equal(LiveActor.SYSTEM_VALIDATION, Assert.Single(firstEvents,
            item => item.Status == LiveStatus.BLOCKED && item.ErrorCode == "NEEDS_CLARIFICATION").Actor);
        original = original with { Request = original.Request with { RunId = blocked.Run.RunId } };
        oldSurface = owner.DefaultRequestHeaders.GetValues("X-Nova-Surface").Single();
        Assert.Equal("NONE", blocked.Run.Approval); Assert.False(blocked.Run.EffectCommitted);
        var path = VisiblePath(own.Library) + $"/live/{original.Request.RunId}/clarification";
        var reply = new AccountLiveClarification(Guid.NewGuid(), grant.Connection.Id, blocked.Run.Sequence, ContentAnswer, ContentSurface());
        VisibleSurface(other, oldSurface);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(path, reply)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync(VisiblePath(foreign.Library) + $"/live/{original.Request.RunId}/clarification", reply)).StatusCode);
        VisibleSurface(owner, ContentSurface());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, reply)).StatusCode);
        VisibleSurface(owner, oldSurface);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, reply with { ConnectionId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, reply with { QuestionSequence = blocked.Run.Sequence - 1 })).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-QP-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, reply)).StatusCode); await Csrf(owner);
        using var accepted = await owner.PostAsJsonAsync(path, reply); accepted.EnsureSuccessStatusCode();
        var started = (await accepted.Content.ReadFromJsonAsync<LiveRecord>())!;
        Assert.Equal(original.Request.SessionId, started.SessionId); Assert.Equal(reply.RunId, started.RunId);
        Assert.Equal("NONE", started.Approval); Assert.False(started.EffectCommitted);
        using var replay = await owner.PostAsJsonAsync(path, reply); replay.EnsureSuccessStatusCode();
        Assert.Equal(started.RunId, (await replay.Content.ReadFromJsonAsync<LiveRecord>())!.RunId);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, reply with { Answer = "Otra respuesta" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, reply with { RunId = Guid.NewGuid() })).StatusCode);
        VisibleSurface(owner, reply.NextSurface);
        var newTaskGate = await DriveContentBoundary(owner, own.Library, reply.RunId, null, timeout.Token);
        var authoringRun = await EstablishContentTask(owner, device, own.Library, grant.Connection.Id, newTaskGate, timeout.Token);
        var driving = DriveContentBoundary(owner, own.Library, authoringRun, null, timeout.Token);
        var lease = await AwaitProposal(device, driving);
        Assert.True(AuthoringSyntheticReview.IsCurrentAuthorDecision(lease.Packet));
        var material = JsonSerializer.Deserialize<JsonElement>(lease.Packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(original.Request.Message, material.GetProperty("originalRequest").GetString());
        var clarification = Assert.Single(material.GetProperty("userClarifications").EnumerateArray());
        Assert.Equal(ContentQuestion, clarification.GetProperty("question").GetString()); Assert.Equal(ContentAnswer, clarification.GetProperty("answer").GetString());
        Assert.Equal(new[] { "<tipo>" }, material.GetProperty("inputs").EnumerateArray().Select(item => item.GetString()));
        (await FinishProposal(device, lease, ContentReply(AuthoringSyntheticReview.CurrentAuthorDecisionReady()))).EnsureSuccessStatusCode();
        var writer = await AwaitProposal(device, driving);
        Assert.False(AuthoringSyntheticReview.IsCurrentAuthorDecision(writer.Packet));
        Assert.Equal(lease.Packet.GetProperty("messages")[1].GetProperty("content").GetString(),
            writer.Packet.GetProperty("messages")[1].GetProperty("content").GetString());
        (await FinishProposal(device, writer, ContentReply(new { title = "Conceptualización", description = "", outcome = "INSTRUCTION",
            instruction = new { task = "Conceptualiza una idea de software de tipo <tipo>." }, question = (string?)null, literalReference = (int?)null }))).EnsureSuccessStatusCode();
        var suggestion = await driving;
        Assert.Equal(LiveStatus.BLOCKED, suggestion.Run.Status); Assert.NotNull(suggestion.Brief); Assert.Null(suggestion.Proposal);
        Assert.Equal("NONE", suggestion.Run.Approval); Assert.False(suggestion.Run.EffectCommitted);
        var briefPath = VisiblePath(own.Library) + $"/live/{authoringRun}/clarification";
        var confirmed = new AccountLiveClarification(Guid.NewGuid(), grant.Connection.Id, suggestion.Run.Sequence, "", ContentSurface()) {
            Brief = new(suggestion.Brief.BasisHash, suggestion.Brief.Proposed with { Constraints = ["Si incluye IA, analiza RAG y Agentic."] }, true)
        };
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(briefPath, confirmed with { Brief = confirmed.Brief with { BasisHash = "otro contrato" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(briefPath, confirmed with { Brief = confirmed.Brief with { Confirmed = false } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(briefPath, confirmed with { QuestionSequence = suggestion.Run.Sequence - 1 })).StatusCode);
        using var confirmResponse = await owner.PostAsJsonAsync(briefPath, confirmed); confirmResponse.EnsureSuccessStatusCode();
        using var confirmReplay = await owner.PostAsJsonAsync(briefPath, confirmed); confirmReplay.EnsureSuccessStatusCode();
        Assert.Equal(confirmed.RunId, (await confirmReplay.Content.ReadFromJsonAsync<LiveRecord>())!.RunId);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(briefPath, confirmed with { Brief = confirmed.Brief with {
            Content = confirmed.Brief.Content with { Task = "Otra tarea" } } })).StatusCode);
        VisibleSurface(owner, confirmed.NextSurface);
        var pending = await DriveContentBoundary(owner, own.Library, confirmed.RunId, null, timeout.Token);
        Assert.Equal(LiveStatus.AWAITING_APPROVAL, pending.Run.Status); Assert.False(pending.Run.EffectCommitted); Assert.Equal("PENDING", pending.Run.Approval);
        Assert.Equal(original.Request.Message, pending.Request);
        Assert.Equal(ContentAnswer, Assert.Single(pending.Proposal!.ContentReview!.UserClarifications).Answer);
        Assert.True(pending.Proposal.ContentReview.ConfirmedBrief!.Edited);
        Assert.Contains("Si incluye IA, analiza RAG y Agentic.", pending.Proposal.Draft.TemplateText);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode); // Confirmation has no second writer/judge call.
        Assert.Equal(own.Prompt.Id, Assert.Single((await owner.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{own.Library}/prompts"))!).Id);
        (await owner.PostAsJsonAsync(VisiblePath(own.Library) + $"/live/{confirmed.RunId}/cancel", new { })).EnsureSuccessStatusCode();
        await AwaitContentTerminal(owner, own.Library, confirmed.RunId, timeout.Token);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
    }

    [Fact]
    public async Task ChangedSourceRejectsContentContinuationAndKeepsTheNewerPrompt()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var own = await LocalLibrary(owner); var grant = await PollingProposalDevice(owner, device);
        var original = ContentStart(grant.Connection.Id, own.Prompt); VisibleSurface(owner, ContentSurface());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var blocked = await AskContent(owner, device, own.Library, original, own.Prompt, timeout.Token);
        original = original with { Request = original.Request with { RunId = blocked.Run.RunId } };
        using var update = await owner.PutAsJsonAsync($"/api/accounts/v1/libraries/{own.Library}/prompts/{own.Prompt.Id}", new PromptContent("Versión más reciente", "Explica <tema> con cuidado.", own.Prompt.Revision));
        update.EnsureSuccessStatusCode(); var current = (await update.Content.ReadFromJsonAsync<AccountPrompt>())!;
        var reply = new AccountLiveClarification(Guid.NewGuid(), grant.Connection.Id, blocked.Run.Sequence, ContentAnswer, ContentSurface());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(VisiblePath(own.Library) + $"/live/{original.Request.RunId}/clarification", reply)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(VisiblePath(own.Library) + $"/live/{reply.RunId}")).StatusCode);
        Assert.Equal(current, await owner.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{own.Library}/prompts/{own.Prompt.Id}"));
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
    }

    [Fact]
    public async Task ExplicitStopInvalidatesTheQuestionPermissionWithoutRewritingBlockedHistory()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var own = await LocalLibrary(owner); var grant = await PollingProposalDevice(owner, device);
        var original = ContentStart(grant.Connection.Id); VisibleSurface(owner, ContentSurface());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var blocked = await AskContent(owner, device, own.Library, original, null, timeout.Token);
        original = original with { Request = original.Request with { RunId = blocked.Run.RunId } };
        var path = VisiblePath(own.Library) + $"/live/{original.Request.RunId}";
        using var stop = await owner.PostAsJsonAsync(path + "/cancel", new { }); stop.EnsureSuccessStatusCode();
        Assert.Equal(LiveStatus.BLOCKED, (await stop.Content.ReadFromJsonAsync<LiveRecord>())!.Status);
        var reply = new AccountLiveClarification(Guid.NewGuid(), grant.Connection.Id, blocked.Run.Sequence, ContentAnswer, ContentSurface());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path + "/clarification", reply)).StatusCode);
        Assert.Equal(own.Prompt, await owner.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{own.Library}/prompts/{own.Prompt.Id}"));
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
    }

    [Fact]
    public async Task DurableQuestionMetadataDoesNotRestoreContinuationPermissionAfterHostRestart()
    {
        Guid library, promptId; string email, surface = ContentSurface(); AccountLiveStart original; AccountLiveClarification reply;
        await using (var first = await Start())
        {
            using var owner = Client(first.App); using var device = Client(first.App);
            email = await Register(owner, first.Mail); await Login(owner, email);
            var own = await LocalLibrary(owner); library = own.Library; promptId = own.Prompt.Id;
            var grant = await PollingProposalDevice(owner, device); original = ContentStart(grant.Connection.Id); VisibleSurface(owner, surface);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var blocked = await AskContent(owner, device, library, original, null, timeout.Token);
            original = original with { Request = original.Request with { RunId = blocked.Run.RunId } };
            reply = new(Guid.NewGuid(), grant.Connection.Id, blocked.Run.Sequence, ContentAnswer, ContentSurface());
        }
        await using var next = await Start(); using var recovered = Client(next.App); await Csrf(recovered); await Login(recovered, email); VisibleSurface(recovered, surface);
        var path = VisiblePath(library) + $"/live/{original.Request.RunId}";
        Assert.Equal(LiveStatus.BLOCKED, (await recovered.GetFromJsonAsync<LiveView>(path))!.Run.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await recovered.PostAsJsonAsync(path + "/clarification", reply)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await recovered.GetAsync(VisiblePath(library) + $"/live/{reply.RunId}")).StatusCode);
        Assert.Equal(promptId, Assert.Single((await recovered.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{library}/prompts"))!).Id);
    }

    private static async Task<LiveView> AskContent(HttpClient owner, HttpClient device, Guid library, AccountLiveStart original, AccountPrompt? source, CancellationToken cancellation)
    {
        (await owner.PostAsJsonAsync(VisiblePath(library) + "/live", original, cancellation)).EnsureSuccessStatusCode();
        var taskGate = await DriveContentBoundary(owner, library, original.Request.RunId, source, cancellation);
        var run = await EstablishContentTask(owner, device, library, original.ConnectionId, taskGate, cancellation);
        var driving = DriveContentBoundary(owner, library, run, source, cancellation);
        var lease = await AwaitProposal(device, driving);
        Assert.True(AuthoringSyntheticReview.IsCurrentAuthorDecision(lease.Packet));
        var material = JsonSerializer.Deserialize<JsonElement>(lease.Packet.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Contains(ContentDecision, material.GetProperty("originalRequest").GetString());
        Assert.Empty(material.GetProperty("userClarifications").EnumerateArray());
        (await FinishProposal(device, lease, ContentReply(new { outcome = "ASK", decisionText = ContentDecision }))).EnsureSuccessStatusCode();
        var blocked = await driving;
        Assert.Equal(LiveStatus.BLOCKED, blocked.Run.Status); Assert.Equal("NEEDS_CLARIFICATION", blocked.Run.ErrorCode); Assert.Equal(ContentQuestion, blocked.Question);
        Assert.Null(blocked.Brief); Assert.Null(blocked.Proposal);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode); // ASK cannot enqueue a writer before the human reply.
        return blocked;
    }

    private static async Task<Guid> EstablishContentTask(HttpClient owner, HttpClient device, Guid library, Guid connection,
        LiveView gate, CancellationToken cancellation)
    {
        Assert.Equal(LiveStatus.BLOCKED, gate.Run.Status);
        Assert.Equal(AuthoringBriefs.EstablishTask, gate.Brief!.Stage);
        Assert.Null(gate.Proposal); Assert.False(gate.Run.EffectCommitted);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
        var continuation = new AccountLiveClarification(Guid.NewGuid(), connection, gate.Run.Sequence, "", ContentSurface()) {
            Brief = new(gate.Brief.BasisHash, gate.Brief.Proposed with {
                Task = "Conceptualiza software de tipo <tipo>."
            }, true)
        };
        using var response = await owner.PostAsJsonAsync(VisiblePath(library) + $"/live/{gate.Run.RunId}/clarification", continuation, cancellation);
        response.EnsureSuccessStatusCode();
        VisibleSurface(owner, continuation.NextSurface);
        return continuation.RunId;
    }

    // Synthetic DOM protocol, not browser acceptance. Never approves or commits a draft.
    private static async Task<LiveView> DriveContentBoundary(HttpClient owner, Guid library, Guid run, AccountPrompt? source, CancellationToken cancellation)
    {
        var path = VisiblePath(library) + "/live/" + run;
        var state = new UiVisibleState("discover", source is null ? [] : [new(source.Id, source.Title, source.Revision)], null, null, null, null, [], [], false, false, null, null, false, false);
        var acknowledged = new HashSet<Guid>();
        while (true)
        {
            var view = (await owner.GetFromJsonAsync<LiveView>(path, cancellation))!;
            if (view.Run.Status == LiveStatus.AWAITING_APPROVAL || LiveRuns.Terminal(view.Run.Status) && !view.Run.BrowserOpen) return view;
            var poll = (await owner.GetFromJsonAsync<UiActionPoll>(path + "/actions?visible=true", cancellation))!;
            if (poll.Action is { } action && acknowledged.Add(action.ActionId))
            {
                switch (action.Tool)
                {
                    case "start": case "ui_read_visible_state": case "set_working_context": case "ui_verify_prompt_state": case "prepare_review": case "ui_search_prompt": case "ui_navigate_library": break;
                    case "ui_open_prompt": case "ui_open_editor":
                        Assert.NotNull(source);
                        state = state with { Mode = action.Tool == "ui_open_editor" ? "create" : "focus", PromptId = source.Id, Revision = source.Revision, Title = source.Title, TemplateText = source.Template, VariableNames = TemplateCompiler.Names(source.Template), Values = new() { ["tema"] = "" } };
                        break;
                    case "ui_open_create_prompt": state = state with { Mode = "create", Title = "", TemplateText = "" }; break;
                    case "ui_prepare_creation": case "ui_prepare_update":
                        var body = action.Arguments.GetProperty("templateText").GetString()!; var names = TemplateCompiler.Names(body);
                        state = state with { Title = action.Arguments.GetProperty("title").GetString(), TemplateText = body, VariableNames = names, Values = names.ToDictionary(name => name, _ => ""), DraftChanged = true,
                            Description = action.Arguments.TryGetProperty("description", out var metadata) ? metadata.GetString() : null };
                        break;
                    default: Assert.Fail("Unexpected content-boundary UI tool: " + action.Tool); break;
                }
                using var receipt = await owner.PostAsJsonAsync(path + "/actions/receipt", new UiActionReceipt(action.ActionId, action.Sequence, "EXECUTED", "VERIFIED", state), cancellation);
                Assert.True(receipt.IsSuccessStatusCode, action.Tool + ": " + await receipt.Content.ReadAsStringAsync(cancellation));
            }
            await Task.Delay(25, cancellation);
        }
    }

    private static async Task<LiveView> AwaitContentTerminal(HttpClient owner, Guid library, Guid run, CancellationToken cancellation)
    {
        while (true)
        {
            var view = (await owner.GetFromJsonAsync<LiveView>(VisiblePath(library) + "/live/" + run, cancellation))!;
            if (LiveRuns.Terminal(view.Run.Status) && !view.Run.BrowserOpen) return view;
            await Task.Delay(25, cancellation);
        }
    }
}
