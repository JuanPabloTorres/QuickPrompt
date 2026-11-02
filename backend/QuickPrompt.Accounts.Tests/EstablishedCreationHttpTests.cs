using System.Net;
using System.Net.Http.Json;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    // Real account/queue/visible-action HTTP with synthetic DOM receipts; no model or browser acceptance.
    [Fact]
    public async Task EstablishedCreationReachesHumanReviewWithoutModelAdmissionOrLibraryEffects()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail));
        var own = await LocalLibrary(owner); var grant = await PollingProposalDevice(owner, device);
        var original = ContentStart(grant.Connection.Id); VisibleSurface(owner, ContentSurface());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        (await owner.PostAsJsonAsync(VisiblePath(own.Library) + "/live", original, timeout.Token)).EnsureSuccessStatusCode();
        var gate = await DriveContentBoundary(owner, own.Library, original.Request.RunId, null, timeout.Token);
        var run = await EstablishContentTask(owner, device, own.Library, grant.Connection.Id, gate, timeout.Token);
        var review = await DriveContentBoundary(owner, own.Library, run, null, timeout.Token);
        Assert.Equal(LiveStatus.BLOCKED, review.Run.Status);
        Assert.Equal(AuthoringBriefs.ReviewContent, review.Brief!.Stage);
        Assert.Equal("Conceptualiza software de tipo <tipo>.", review.Brief.Proposed.Task);
        Assert.Equal(review.Brief.Proposed.Task, review.Brief.EstablishedTask!.Task);
        Assert.Null(review.Brief.Suggested); Assert.Null(review.Proposal);
        Assert.Equal("NONE", review.Run.Approval); Assert.False(review.Run.EffectCommitted);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);

        var confirmation = new AuthoringBriefReply(review.Brief.BasisHash, review.Brief.Proposed, true);
        var next = new AccountLiveClarification(Guid.NewGuid(), grant.Connection.Id, review.Run.Sequence, "", ContentSurface()) { Brief = confirmation };
        (await owner.PostAsJsonAsync(VisiblePath(own.Library) + $"/live/{run}/clarification", next, timeout.Token)).EnsureSuccessStatusCode();
        VisibleSurface(owner, next.NextSurface);
        var pending = await DriveContentBoundary(owner, own.Library, next.RunId, null, timeout.Token);
        Assert.Equal(LiveStatus.AWAITING_APPROVAL, pending.Run.Status);
        Assert.Equal(AuthoringBriefs.Confirm(review.Brief, confirmation).Render(), pending.Proposal!.Draft.TemplateText);
        Assert.Equal("PENDING", pending.Run.Approval); Assert.False(pending.Run.EffectCommitted);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
        Assert.Equal(0L, (await PresenceAdmissionCounts((await ProposalActor(owner)).Id)).Jobs);
        Assert.Equal(own.Prompt, await owner.GetFromJsonAsync<AccountPrompt>(
            $"/api/accounts/v1/libraries/{own.Library}/prompts/{own.Prompt.Id}"));
        (await owner.PostAsJsonAsync(VisiblePath(own.Library) + $"/live/{next.RunId}/cancel", new { }, timeout.Token)).EnsureSuccessStatusCode();
        await AwaitContentTerminal(owner, own.Library, next.RunId, timeout.Token);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
    }
}
