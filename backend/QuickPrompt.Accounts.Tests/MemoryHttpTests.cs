using System.Net;
using System.Net.Http.Json;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Cloud;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task RememberedEditorHttpChecksCookieActorConfirmationRevisionAndForgetScope()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App);
        var emailA = await Register(a, host.Mail); var emailB = await Register(b, host.Mail);
        await Login(a, emailA); await Login(b, emailB);
        var libraryA = await CreateLibrary(a); var libraryB = await CreateLibrary(b);
        var created = await a.PostAsJsonAsync($"/api/accounts/v1/libraries/{libraryA}/prompts", new PromptContent("Origen", "Explica <tema>.", 0));
        created.EnsureSuccessStatusCode(); var prompt = (await created.Content.ReadFromJsonAsync<AccountPrompt>())!;
        var prefixA = $"/api/accounts/v1/libraries/{libraryA}/memory"; var prefixB = $"/api/accounts/v1/libraries/{libraryB}/memory";
        var id = Guid.NewGuid();
        var input = new RememberEditorContext(id, 0, null, true, prompt.Id, prompt.Revision, "Contexto privado A", "Explica <tema> en español.", new() { ["tema"] = "RLS" });
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(prefixA + "/editor", input with { Confirmed = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync(prefixA + "/editor", input)).StatusCode);
        a.DefaultRequestHeaders.Remove("X-QP-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(prefixA + "/editor", input)).StatusCode);
        await Csrf(a);
        var response = await a.PostAsJsonAsync(prefixA + "/editor", input); response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<SavedEditorContext>())!;
        Assert.Equal("Explica RLS en español.", saved.CompiledText);
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync(prefixA + "/editor", input)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(prefixA + $"/sessions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(prefixB + $"/sessions/{id}")).StatusCode);
        Assert.Empty((await b.GetFromJsonAsync<RememberedContextSummary[]>(prefixB + "/sessions"))!);
        var savedBResponse = await b.PostAsJsonAsync(prefixB + "/editor", input with { PromptId = null, Revision = null, Title = "B independiente", Values = new() { ["tema"] = "B" } });
        savedBResponse.EnsureSuccessStatusCode();
        Assert.Equal("Contexto privado A", (await a.GetFromJsonAsync<SavedEditorContext>(prefixA + $"/sessions/{id}"))!.Title);
        Assert.Equal("B independiente", (await b.GetFromJsonAsync<SavedEditorContext>(prefixB + $"/sessions/{id}"))!.Title);
        (await a.PutAsJsonAsync($"/api/accounts/v1/libraries/{libraryA}/prompts/{prompt.Id}", new PromptContent("Origen", "Origen modificado <tema>.", 1))).EnsureSuccessStatusCode();
        Assert.True((await a.GetFromJsonAsync<SavedEditorContext>(prefixA + $"/sessions/{id}"))!.SourceStale);
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync(prefixA + "/editor", input with { ExpectedDraftId = saved.DraftId, ExpectedDraftRevision = saved.DraftRevision })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync(prefixA + $"/sessions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync(prefixA + $"/sessions/{id}")).StatusCode);
        Assert.Single((await a.GetFromJsonAsync<AccountPrompt[]>($"/api/accounts/v1/libraries/{libraryA}/prompts"))!);
        Assert.Single((await b.GetFromJsonAsync<RememberedContextSummary[]>(prefixB + "/sessions"))!);
    }

    [Fact]
    public async Task AccountPreferenceHttpRejectsForgedActorAndRequiresExplicitConfirmation()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        const string path = "/account/preferences/";
        var preferences = new PreparationPreferences("es", "Markdown", "ollama");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(path, new PreferenceApproval(preferences, false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(path, new { preferences, confirmed = true, ownerId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(path, new PreferenceApproval(new("es", "Markdown", "https://untrusted.example"), true))).StatusCode);
        (await a.PostAsJsonAsync(path, new PreferenceApproval(preferences, true))).EnsureSuccessStatusCode();
        Assert.Equal(preferences, await a.GetFromJsonAsync<PreparationPreferences>(path));
        Assert.Equal(new PreparationPreferences(), await b.GetFromJsonAsync<PreparationPreferences>(path));
        (await b.PostAsJsonAsync(path, new PreferenceApproval(new("en", "JSON"), true))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync(path)).StatusCode);
        Assert.Equal(new PreparationPreferences(), await a.GetFromJsonAsync<PreparationPreferences>(path));
        Assert.Equal("en", (await b.GetFromJsonAsync<PreparationPreferences>(path))!.Language);
    }
}
