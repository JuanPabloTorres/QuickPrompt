using System.Net;
using System.Net.Http.Json;
using QuickPrompt.Cloud;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task VisibleFeedRenewsAtThirtySecondsAndReconnectsFromHeaderWithoutRepeatingPendingUiWork()
    {
        await using var host = await Start(); using var owner = Client(host.App); using var device = Client(host.App);
        await Login(owner, await Register(owner, host.Mail)); var own = await LocalLibrary(owner);
        var grant = await PollingProposalDevice(owner, device); var request = OpenVisible(grant.Connection.Id, own.Prompt);
        VisibleSurface(owner, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        var path = VisiblePath(own.Library) + "/live/" + request.Request.RunId;
        (await owner.PostAsJsonAsync(VisiblePath(own.Library) + "/live", request)).EnsureSuccessStatusCode();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        UiActionPoll pending;
        do {
            pending = (await owner.GetFromJsonAsync<UiActionPoll>(path + "/actions?visible=true", lifetime.Token))!;
            if (pending.Action is null) await Task.Delay(30, lifetime.Token);
        } while (pending.Action is null);
        var before = (await owner.GetFromJsonAsync<LiveView>(path, lifetime.Token))!;
        Assert.False(LiveRuns.Terminal(before.Run.Status)); Assert.False(before.Run.EffectCommitted);
        using (var response = await owner.GetAsync(path + "/events?after=0", HttpCompletionOption.ResponseHeadersRead, lifetime.Token))
        using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync(lifetime.Token)))
        {
            response.EnsureSuccessStatusCode();
            string? kind;
            do { kind = await VisibleFrameEvent(reader, lifetime.Token); Assert.NotNull(kind); } while (kind != "renew");
            Assert.Null(await VisibleFrameEvent(reader, lifetime.Token));
        }
        var after = (await owner.GetFromJsonAsync<LiveView>(path, lifetime.Token))!;
        Assert.Equal(before.Run.RunId, after.Run.RunId); Assert.Equal(before.Run.Sequence, after.Run.Sequence);
        Assert.Equal(before.Run.Status, after.Run.Status); Assert.False(after.Run.EffectCommitted);
        Assert.Equal(before.Proposal, after.Proposal);
        var retained = (await owner.GetFromJsonAsync<UiActionPoll>(path + "/actions?visible=true", lifetime.Token))!;
        Assert.Equal(pending.Action.ActionId, retained.Action!.ActionId);
        using var reconnect = new HttpRequestMessage(HttpMethod.Get, path + "/events?after=0");
        reconnect.Headers.Add("Last-Event-ID", request.Request.RunId + ":" + after.Run.Sequence);
        using (var response = await owner.SendAsync(reconnect, HttpCompletionOption.ResponseHeadersRead, lifetime.Token))
        using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync(lifetime.Token)))
        {
            response.EnsureSuccessStatusCode();
            // A stale query cursor must not replay activity already covered by Last-Event-ID.
            Assert.Equal("heartbeat", await VisibleFrameEvent(reader, lifetime.Token));
        }
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimProposal(device)).StatusCode);
        (await owner.PostAsJsonAsync(path + "/cancel", new { }, lifetime.Token)).EnsureSuccessStatusCode();
        Assert.Equal(LiveStatus.CANCELLED, (await owner.GetFromJsonAsync<LiveView>(path, lifetime.Token))!.Run.Status);
    }
}
