using System.Net;
using System.Net.Http.Json;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task HttpCollectionsPersistAssignmentsAndRejectOtherOwnersForgedIdentityCsrfAndStaleRevisions()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App); using var anonymous = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var library = await CreateLibrary(a); var other = await CreateLibrary(b); var root = $"/api/accounts/v1/libraries/{library}";
        var created = await a.PostAsJsonAsync(root + "/prompts", new PromptContent("En colección", "Analiza <tema>.", 0)); created.EnsureSuccessStatusCode();
        var prompt = (await created.Content.ReadFromJsonAsync<AccountPrompt>())!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(root + "/groups")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(root + "/groups")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(root + "/groups", new { name = "No crear", ownerId = Guid.NewGuid() })).StatusCode);
        a.DefaultRequestHeaders.Remove("X-QP-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(root + "/groups", new GroupInput("Sin CSRF"))).StatusCode);
        await Csrf(a);
        var groupResponse = await a.PostAsJsonAsync(root + "/groups", new GroupInput("Ideas")); groupResponse.EnsureSuccessStatusCode();
        var group = (await groupResponse.Content.ReadFromJsonAsync<PromptGroup>())!;
        var assignment = root + $"/prompts/{prompt.Id}/groups";
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync(assignment, new MembershipInput(group.Id, 1, true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/accounts/v1/libraries/{other}/prompts/{prompt.Id}/groups", new MembershipInput(group.Id, 1, true))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync(assignment, new MembershipInput(group.Id, 9, true))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync(assignment, new MembershipInput(group.Id, 1, true))).StatusCode);
        Assert.Equal(group.Id, Assert.Single((await a.GetFromJsonAsync<Guid[]>(assignment))!));
        Assert.Equal(prompt.Id, Assert.Single((await a.GetFromJsonAsync<AccountPrompt[]>(root + $"/prompts?collectionId={group.Id}"))!).Id);
        Assert.Empty((await a.GetFromJsonAsync<AccountPrompt[]>(root + "/prompts?ungrouped=true"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/accounts/v1/libraries/{other}/prompts?collectionId={group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await a.PutAsJsonAsync(root + $"/groups/{group.Id}", new GroupInput("Obsoleta", 1))).StatusCode);
        var renamed = await a.PutAsJsonAsync(root + $"/groups/{group.Id}", new GroupInput("Revisadas", 2)); renamed.EnsureSuccessStatusCode();
        var current = (await renamed.Content.ReadFromJsonAsync<PromptGroup>())!;
        Assert.Equal(3, current.Revision); Assert.Equal("Revisadas", current.Name);
        var groups = (await a.GetFromJsonAsync<GroupSummary[]>(root + "/groups"))!;
        Assert.Equal(1, Assert.Single(groups).Templates);
        using var delete = new HttpRequestMessage(HttpMethod.Delete, root + $"/groups/{group.Id}") { Content = JsonContent.Create(new ConfirmInput(3, true)) };
        Assert.Equal(HttpStatusCode.NoContent, (await a.SendAsync(delete)).StatusCode);
        Assert.Empty((await a.GetFromJsonAsync<Guid[]>(assignment))!);
        Assert.Equal(prompt, await a.GetFromJsonAsync<AccountPrompt>(root + $"/prompts/{prompt.Id}"));
        Assert.Single((await a.GetFromJsonAsync<AccountPrompt[]>(root + "/prompts?ungrouped=true"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync(root + $"/prompts?collectionId={group.Id}")).StatusCode);
    }
}
