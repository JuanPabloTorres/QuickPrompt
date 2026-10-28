using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using QuickPrompt.Local;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task PortableDefinitionUsesTheExistingImportContractAndRequiresOwnerAndCurrentRevision()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App); using var anonymous = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var own = await LocalLibrary(a); var foreign = await LocalLibrary(b);
        var path = $"/api/accounts/v1/libraries/{own.Library}/prompts/{own.Prompt.Id}/share?revision={own.Prompt.Revision}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/accounts/v1/libraries/{foreign.Library}/prompts/{own.Prompt.Id}/share?revision=1")).StatusCode);
        var package = (await a.GetFromJsonAsync<TemplatePackage>(path))!;
        var imported = package.ToTemplate();
        Assert.Equal(own.Prompt.Title, imported.Title); Assert.Equal(own.Prompt.Template, imported.Template); Assert.Empty(imported.Variables);
        var json = JsonSerializer.SerializeToElement(package, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(new[] { "format", "formatVersion", "prompt" }, json.EnumerateObject().Select(field => field.Name));
        Assert.Equal(new[] { "title", "template", "description", "category" }, json.GetProperty("prompt").EnumerateObject().Select(field => field.Name));
        var saved = await a.PutAsJsonAsync($"/api/accounts/v1/libraries/{own.Library}/prompts/{own.Prompt.Id}", new PromptContent("Actualizada", "Otro cuerpo.", own.Prompt.Revision));
        saved.EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.Conflict, (await a.GetAsync(path)).StatusCode);
        Assert.Equal(2, (await saved.Content.ReadFromJsonAsync<AccountPrompt>())!.Revision);
    }
}
