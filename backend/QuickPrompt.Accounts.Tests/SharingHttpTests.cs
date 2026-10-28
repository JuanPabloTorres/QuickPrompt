using System.Net;
using System.Net.Http.Json;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Sharing;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task SharedHttpSnapshotRequiresAcceptanceAndRevocationBlocksNewReadsAndImports()
    {
        await using var host = await Start();
        using var a = Client(host.App);
        using var b = Client(host.App);
        using var c = Client(host.App);
        using var anonymous = Client(host.App);
        var emailA = await Register(a, host.Mail);
        var emailB = await Register(b, host.Mail);
        var emailC = await Register(c, host.Mail);
        await Login(a, emailA);
        await Login(b, emailB);
        await Login(c, emailC);
        var libraryA = await CreateLibrary(a);
        var libraryB = await CreateLibrary(b);
        var saved = await a.PostAsJsonAsync($"/api/accounts/v1/libraries/{libraryA}/prompts", new PromptContent("Publicar", "Explica <tema>.", 0));
        saved.EnsureSuccessStatusCode();
        var prompt = (await saved.Content.ReadFromJsonAsync<AccountPrompt>())!;
        const string api = "/api/accounts/v1/sharing";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(api + "/invitations")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(api + "/publications", new { libraryId = libraryA, promptId = prompt.Id, revision = 1, ownerId = Guid.NewGuid() })).StatusCode);
        var published = await a.PostAsJsonAsync(api + "/publications", new PublicationRequest(libraryA, prompt.Id, 1));
        published.EnsureSuccessStatusCode();
        var publication = (await published.Content.ReadFromJsonAsync<SharedPrompt>())!;
        var path = api + "/publications/" + publication.Id;
        Assert.Equal(HttpStatusCode.Accepted, (await a.PostAsJsonAsync(path + "/invite", new InvitationRequest("unknown@example.invalid"))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await a.PostAsJsonAsync(path + "/invite", new InvitationRequest(emailB))).StatusCode);
        var invitation = Assert.Single((await b.GetFromJsonAsync<SharedInvitation[]>(api + "/invitations"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync(api + $"/invitations/{invitation.Id}/decision", new InvitationDecision(InvitationState.Accepted))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.PostAsJsonAsync(api + $"/invitations/{invitation.Id}/decision", new InvitationDecision(InvitationState.Accepted))).StatusCode);
        Assert.Equal(publication, await b.GetFromJsonAsync<SharedPrompt>(path));
        b.DefaultRequestHeaders.Remove("X-QP-CSRF");
        var request = new ImportRequest(libraryB, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await b.PostAsJsonAsync(path + "/import", request)).StatusCode);
        await Csrf(b);
        var result = await b.PostAsJsonAsync(path + "/import", request);
        result.EnsureSuccessStatusCode();
        var copy = (await result.Content.ReadFromJsonAsync<AccountPrompt>())!;
        Assert.Equal(prompt.Template, copy.Template);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync(path + "/revoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync(path + "/import", request)).StatusCode);
        Assert.Equal(copy, Assert.Single((await b.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{libraryB}/prompts"))!));
        Assert.Equal(prompt, Assert.Single((await a.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{libraryA}/prompts"))!));
    }
}
