using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using QuickPrompt.Cloud.Delegation;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    private static string VisiblePath(Guid library) => $"/api/accounts/v1/libraries/{library}/nova";
    private static AccountLiveStart OpenVisible(Guid connection, AccountPrompt prompt) => new(connection,
        new(Guid.NewGuid(), Guid.NewGuid(), "Abre este prompt para leerlo.", SourceId: prompt.Id, SourceRevision: prompt.Revision,
            Command: new(SemanticOperation.OPEN_PROMPT)));
    private static void VisibleSurface(HttpClient client, string capability)
    { client.DefaultRequestHeaders.Remove("X-Nova-Surface"); client.DefaultRequestHeaders.Add("X-Nova-Surface", capability); }

    [Fact]
    public async Task AuthenticatedVisibleEngineReadsOnlyTheBoundLibraryAndDoesNotInferAnExplicitRead()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App);
        using var deviceA = Client(host.App); using var deviceB = Client(host.App); using var anonymous = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var ownA = await LocalLibrary(a); var ownB = await LocalLibrary(b);
        var grantA = await PollingProposalDevice(a, deviceA); var grantB = await PollingProposalDevice(b, deviceB);
        var requestA = OpenVisible(grantA.Connection.Id, ownA.Prompt); var requestB = OpenVisible(grantB.Connection.Id, ownB.Prompt);
        var surfaceA = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var surfaceB = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        VisibleSurface(a, surfaceA); VisibleSurface(b, surfaceB);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(VisiblePath(ownA.Library) + "/live", requestA)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await deviceA.PostAsJsonAsync(VisiblePath(ownA.Library) + "/live", requestA)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync(VisiblePath(ownA.Library) + "/live", requestB)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync(VisiblePath(ownB.Library) + "/live", requestB with { Request = requestB.Request with { SourceId = ownA.Prompt.Id } })).StatusCode);
        (await a.PostAsJsonAsync(VisiblePath(ownA.Library) + "/live", requestA)).EnsureSuccessStatusCode();
        (await b.PostAsJsonAsync(VisiblePath(ownB.Library) + "/live", requestB)).EnsureSuccessStatusCode();
        var viewA = await DriveVisibleRead(a, ownA.Library, requestA.Request.RunId, ownA.Prompt);
        var viewB = await DriveVisibleRead(b, ownB.Library, requestB.Request.RunId, ownB.Prompt);
        Assert.Equal(LiveStatus.COMPLETED, viewA.Run.Status); Assert.Equal(LiveStatus.COMPLETED, viewB.Run.Status);
        Assert.False(viewA.Run.BrowserOpen); Assert.False(viewB.Run.BrowserOpen); Assert.False(viewA.Run.EffectCommitted); Assert.False(viewB.Run.EffectCommitted);
        Assert.Null((await a.GetFromJsonAsync<UiActionPoll>(VisiblePath(ownA.Library) + $"/live/{requestA.Request.RunId}/actions?visible=true"))!.Action);
        Assert.Equal(ownA.Prompt.Template, viewA.Observation!.TemplateText); Assert.Equal(ownB.Prompt.Template, viewB.Observation!.TemplateText);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(deviceA)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(deviceB)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(VisiblePath(ownA.Library) + $"/live/{requestA.Request.RunId}")).StatusCode);
        var replay = await a.PostAsJsonAsync(VisiblePath(ownA.Library) + "/live", requestA); replay.EnsureSuccessStatusCode();
        Assert.Equal(requestA.Request.RunId, (await replay.Content.ReadFromJsonAsync<LiveRecord>())!.RunId);
        Assert.Equal(ownA.Prompt, await a.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{ownA.Library}/prompts/{ownA.Prompt.Id}"));
        var events = await a.GetFromJsonAsync<LiveEvent[]>(VisiblePath(ownA.Library) + $"/live/{requestA.Request.RunId}/activity?after=0");
        Assert.Equal("Browser", events![^1].Stage); Assert.Equal(events.Length, events[^1].Sequence);
        using var stream = await a.GetAsync(VisiblePath(ownA.Library) + $"/live/{requestA.Request.RunId}/events?after=0");
        stream.EnsureSuccessStatusCode(); Assert.Equal("text/event-stream", stream.Content.Headers.ContentType!.MediaType);
        var delivered = await stream.Content.ReadAsStringAsync(); Assert.Contains("event: activity", delivered); Assert.Contains(requestA.Request.RunId.ToString(), delivered);
        using var invalidCursor = new HttpRequestMessage(HttpMethod.Get, VisiblePath(ownA.Library) + $"/live/{requestA.Request.RunId}/events");
        invalidCursor.Headers.Add("Last-Event-ID", Guid.NewGuid() + ":0");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.SendAsync(invalidCursor)).StatusCode);
    }

    [Fact]
    public async Task VisibleActivityStopsAfterCookieStampInvalidationWithoutStartingAnotherRun()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var own = await LocalLibrary(owner);
        var grant = await PollingProposalDevice(owner, device); var request = OpenVisible(grant.Connection.Id, own.Prompt);
        VisibleSurface(owner, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        var path = VisiblePath(own.Library) + "/live/" + request.Request.RunId;
        (await owner.PostAsJsonAsync(VisiblePath(own.Library) + "/live", request)).EnsureSuccessStatusCode();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var stream = await owner.GetAsync(path + "/events?after=0", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        stream.EnsureSuccessStatusCode();
        using var reader = new StreamReader(await stream.Content.ReadAsStreamAsync(timeout.Token));
        Assert.Equal("activity", await VisibleFrameEvent(reader, timeout.Token));
        (await owner.PostAsJsonAsync("/account/logout-all", new { }, timeout.Token)).EnsureSuccessStatusCode();
        string? next;
        do { next = await VisibleFrameEvent(reader, timeout.Token); Assert.NotNull(next); } while (next != "access-lost");
        Assert.Null(await VisibleFrameEvent(reader, timeout.Token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync(path + "/events?after=0", timeout.Token)).StatusCode);
    }

    private static async Task<string?> VisibleFrameEvent(StreamReader reader, CancellationToken cancellation)
    {
        string? kind = null;
        while (await reader.ReadLineAsync(cancellation) is { } line)
        {
            if (line.StartsWith("event:", StringComparison.Ordinal)) kind = line[6..].Trim();
            if (line.Length == 0 && kind is not null) return kind;
        }
        return null;
    }

    [Fact]
    public async Task VisibleHttpControlsRequireTheBoundSurfaceAndCsrfAndOwnerCanStopAfterRevocation()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var own = await LocalLibrary(owner);
        var grant = await PollingProposalDevice(owner, device); var request = OpenVisible(grant.Connection.Id, own.Prompt);
        var capability = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"); VisibleSurface(owner, capability);
        var path = VisiblePath(own.Library) + "/live/" + request.Request.RunId;
        (await owner.PostAsJsonAsync(VisiblePath(own.Library) + "/live", request)).EnsureSuccessStatusCode();
        // Retain a real pending UI action without acknowledging it: revoke/cancel
        // must exercise an active turn, rather than race the initial browser emission.
        using var awaiting = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        UiActionPoll waiting;
        do
        {
            waiting = (await owner.GetFromJsonAsync<UiActionPoll>(path + "/actions?visible=true", awaiting.Token))!;
            Assert.False(LiveRuns.Terminal(waiting.Run.Status));
            if (waiting.Action is null) await Task.Delay(20, awaiting.Token);
        } while (waiting.Action is null);
        Assert.Equal("start", waiting.Action.Tool);
        VisibleSurface(owner, new string('x', 64));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.GetAsync(path + "/actions?visible=true")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path + "/pause", new { })).StatusCode);
        VisibleSurface(owner, capability);
        owner.DefaultRequestHeaders.Remove("X-QP-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path + "/cancel", new { })).StatusCode);
        await Csrf(owner);
        (await owner.PostAsJsonAsync($"/account/connections/{grant.Connection.Id}/revoke", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(path + "/actions?visible=true")).StatusCode);
        var stopped = await owner.PostAsJsonAsync(path + "/cancel", new { }); stopped.EnsureSuccessStatusCode();
        Assert.Equal(LiveStatus.CANCELLED, (await stopped.Content.ReadFromJsonAsync<LiveRecord>())!.Status);
        Assert.Equal(own.Prompt, await owner.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{own.Library}/prompts/{own.Prompt.Id}"));
    }

    [Fact]
    public async Task RevocationBeforeTheFirstUiActionFailsClosedAndCancellationDoesNotRewriteTheTerminalHistory()
    {
        var checks = 0; var revoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await Start(configureServices: services => services.AddScoped<IProposalConnectionAuthority>(provider =>
            new ScheduledProposalAuthority(async (actor, connection, cancellation) =>
            {
                // These are the actual owning boundaries, not an always-false fake:
                // admission, CREATED append, then first engine/browser append.
                if (Interlocked.Increment(ref checks) == 3)
                {
                    // This callback already holds the queue fence. Use its underlying
                    // account owner directly, avoiding a second nested fence.
                    Assert.True(await provider.GetRequiredService<ConnectionAccess>().Revoke(actor, connection, cancellation));
                    revoked.TrySetResult();
                }
                return await provider.GetRequiredService<DelegationBoundary>().ProposalOwned(actor, connection, cancellation);
            })));
        using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var own = await LocalLibrary(owner);
        var grant = await PollingProposalDevice(owner, device); var request = OpenVisible(grant.Connection.Id, own.Prompt);
        VisibleSurface(owner, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        var path = VisiblePath(own.Library) + "/live/" + request.Request.RunId;
        (await owner.PostAsJsonAsync(VisiblePath(own.Library) + "/live", request)).EnsureSuccessStatusCode();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await revoked.Task.WaitAsync(timeout.Token);
        LiveView view;
        do
        {
            view = (await owner.GetFromJsonAsync<LiveView>(path, timeout.Token))!;
            if (!LiveRuns.Terminal(view.Run.Status) || view.Run.BrowserOpen) await Task.Delay(20, timeout.Token);
        } while (!LiveRuns.Terminal(view.Run.Status) || view.Run.BrowserOpen);
        Assert.Equal(LiveStatus.FAILED, view.Run.Status); Assert.Equal("BROWSER_FAILURE", view.Run.ErrorCode);
        Assert.False(view.Run.EffectCommitted); Assert.False(view.Run.BrowserOpen); Assert.Null(view.Proposal);
        Assert.Null((await owner.GetFromJsonAsync<UiActionPoll>(path + "/actions?visible=true", timeout.Token))!.Action);
        var events = host.App.Services.GetRequiredService<AccountNovaCoordinator>().Events(await ProposalActor(owner), own.Library, request.Request.RunId, 0);
        Assert.DoesNotContain(events, item => item.ToolName is not null);
        var stop = await owner.PostAsJsonAsync(path + "/cancel", new { }, timeout.Token); stop.EnsureSuccessStatusCode();
        var after = (await stop.Content.ReadFromJsonAsync<LiveRecord>(timeout.Token))!;
        Assert.Equal(LiveStatus.FAILED, after.Status); Assert.Equal("BROWSER_FAILURE", after.ErrorCode);
        Assert.Equal(own.Prompt, await owner.GetFromJsonAsync<AccountPrompt>($"/api/accounts/v1/libraries/{own.Library}/prompts/{own.Prompt.Id}", timeout.Token));
    }

    private sealed class ScheduledProposalAuthority(Func<LibraryActor, Guid, CancellationToken, Task<bool>> check) : IProposalConnectionAuthority
    {
        public Task<bool> ProposalOwned(LibraryActor actor, Guid connectionId, CancellationToken cancellation) => check(actor, connectionId, cancellation);
    }

    private static async Task<LiveView> DriveVisibleRead(HttpClient owner, Guid library, Guid run, AccountPrompt prompt)
    {
        var path = VisiblePath(library) + "/live/" + run;
        var state = new UiVisibleState("discover", [new(prompt.Id, prompt.Title, prompt.Revision)], null, null, null, null, [], [], false, false, null, null, false, false);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var acknowledged = new HashSet<Guid>();
        while (true)
        {
            var view = (await owner.GetFromJsonAsync<LiveView>(path, timeout.Token))!;
            if (LiveRuns.Terminal(view.Run.Status) && !view.Run.BrowserOpen) return view;
            var poll = (await owner.GetFromJsonAsync<UiActionPoll>(path + "/actions?visible=true", timeout.Token))!;
            if (poll.Action is { } action && acknowledged.Add(action.ActionId))
            {
                Assert.Contains(action.Tool, new[] { "start", "ui_read_visible_state", "ui_navigate_library", "ui_search_prompt", "ui_open_prompt", "ui_verify_prompt_state", "set_working_context" });
                if (action.Tool == "ui_open_prompt") state = state with
                {
                    Mode = "focus",
                    PromptId = prompt.Id,
                    Revision = prompt.Revision,
                    Title = prompt.Title,
                    TemplateText = prompt.Template,
                    VariableNames = ["tema"],
                    Values = new() { ["tema"] = "" }
                };
                using var response = await owner.PostAsJsonAsync(path + "/actions/receipt", new UiActionReceipt(action.ActionId, action.Sequence, "EXECUTED", "VERIFIED", state), timeout.Token);
                response.EnsureSuccessStatusCode();
            }
            await Task.Delay(30, timeout.Token);
        }
    }

    // Protocol-level UI fixture only. Real browser/DOM acceptance is a separate gate.
    private static async Task<LiveView> DriveVisibleCreation(HttpClient owner, Guid library, Guid run, CancellationToken cancellation)
    {
        var path = VisiblePath(library) + "/live/" + run;
        var state = new UiVisibleState("discover", [], null, null, null, null, [], [], false, false, null, null, false, false);
        var acknowledged = new HashSet<Guid>(); var approved = false;
        while (true)
        {
            var view = (await owner.GetFromJsonAsync<LiveView>(path, cancellation))!;
            if (LiveRuns.Terminal(view.Run.Status) && !view.Run.BrowserOpen) return view;
            if (!approved && view.Run.Status == LiveStatus.AWAITING_APPROVAL && view.Proposal is { } pending)
            {
                Assert.False(view.Run.EffectCommitted);
                using var approval = await owner.PostAsJsonAsync(path + "/approve", new LiveApprovalInput(pending.Id, pending.Hash, pending.Revision, true), cancellation);
                approval.EnsureSuccessStatusCode(); approved = true;
            }
            var poll = (await owner.GetFromJsonAsync<UiActionPoll>(path + "/actions?visible=true", cancellation))!;
            if (poll.Action is { } action && acknowledged.Add(action.ActionId))
            {
                switch (action.Tool)
                {
                    case "start": case "ui_read_visible_state": case "set_working_context": case "ui_verify_prompt_state": case "prepare_review": break;
                    case "ui_open_create_prompt": state = state with { Mode = "create", Title = "", TemplateText = "" }; break;
                    case "ui_prepare_creation":
                        var body = action.Arguments.GetProperty("templateText").GetString()!;
                        var names = QuickPrompt.Local.TemplateCompiler.Names(body);
                        state = state with
                        {
                            Title = action.Arguments.GetProperty("title").GetString(),
                            TemplateText = body,
                            VariableNames = names,
                            Values = names.ToDictionary(name => name, _ => ""),
                            DraftChanged = true
                        };
                        break;
                    case "commit_approved":
                        Assert.True(approved); Assert.NotNull(view.Proposal);
                        using (var commit = await owner.PostAsJsonAsync(path + "/commit", view.Proposal.Draft, cancellation))
                        {
                            commit.EnsureSuccessStatusCode(); var saved = (await commit.Content.ReadFromJsonAsync<PromptDocument>(cancellation))!;
                            state = state with
                            {
                                Mode = "focus",
                                PromptId = saved.Id,
                                Revision = saved.Revision,
                                Title = saved.Title,
                                TemplateText = saved.TemplateText,
                                VariableNames = saved.VariableNames,
                                DraftChanged = false
                            };
                        }
                        break;
                    default: Assert.Fail("Unexpected UI tool in creation fixture: " + action.Tool); break;
                }
                using var receipt = await owner.PostAsJsonAsync(path + "/actions/receipt", new UiActionReceipt(action.ActionId, action.Sequence, "EXECUTED", "VERIFIED", state), cancellation);
                receipt.EnsureSuccessStatusCode();
            }
            await Task.Delay(40, cancellation);
        }
    }
}
