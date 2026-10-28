using System.Net;
using System.Net.Http.Json;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    [Fact]
    public async Task EditorInspectionUsesTheSharedCompilerWithoutRememberingOrExecutingAndChecksAccountSourceAndRevision()
    {
        await using var host = await Start(); using var a = Client(host.App); using var b = Client(host.App); using var anonymous = Client(host.App);
        await Login(a, await Register(a, host.Mail)); await Login(b, await Register(b, host.Mail));
        var ownA = await LocalLibrary(a); var ownB = await LocalLibrary(b);
        var root = $"/api/accounts/v1/libraries/{ownA.Library}";
        var path = root + "/memory/editor/inspect";
        var input = new InspectAccountEditor(ownA.Prompt.Id, ownA.Prompt.Revision, "Explica <tema> para <audiencia>; repite <tema>.", new());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path, input)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync(path, input)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/accounts/v1/libraries/{ownB.Library}/memory/editor/inspect", input)).StatusCode);
        a.DefaultRequestHeaders.Remove("X-QP-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(path, input)).StatusCode);
        await Csrf(a);
        using var missingResponse = await a.PostAsJsonAsync(path, input); missingResponse.EnsureSuccessStatusCode();
        var missing = (await missingResponse.Content.ReadFromJsonAsync<AccountEditorInspection>())!;
        Assert.Equal(["tema", "audiencia"], missing.VariableNames); Assert.Equal(missing.VariableNames, missing.MissingVariables); Assert.Null(missing.CompiledText);
        using var filledResponse = await a.PostAsJsonAsync(path, input with { Values = new() { ["tema"] = "RLS <literal>\nñ", ["audiencia"] = "equipo" } });
        filledResponse.EnsureSuccessStatusCode(); var filled = (await filledResponse.Content.ReadFromJsonAsync<AccountEditorInspection>())!;
        Assert.Equal("Explica RLS <literal>\nñ para equipo; repite RLS <literal>\nñ.", filled.CompiledText); Assert.Empty(filled.MissingVariables);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(path, input with { Values = new() { ["inventada"] = "valor" } })).StatusCode);
        using var blankResponse = await a.PostAsJsonAsync(path, new InspectAccountEditor(null, null, "", new())); blankResponse.EnsureSuccessStatusCode();
        Assert.Equal("", (await blankResponse.Content.ReadFromJsonAsync<AccountEditorInspection>())!.CompiledText);
        Assert.Empty((await a.GetFromJsonAsync<RememberedContextSummary[]>(root + "/memory/sessions"))!);
        Assert.Equal("[]", await a.GetStringAsync(root + "/executions"));
        Assert.Equal(ownA.Prompt, await a.GetFromJsonAsync<AccountPrompt>(root + $"/prompts/{ownA.Prompt.Id}"));
        (await a.PutAsJsonAsync(root + $"/prompts/{ownA.Prompt.Id}", new PromptContent("Nueva revisión", "Otra instrucción.", ownA.Prompt.Revision))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync(path, input)).StatusCode);
    }
}
