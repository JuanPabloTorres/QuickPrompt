using System.Net.Http.Json;
using System.Text.Json;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Nova.Evaluation;

// Synthetic evaluator composition only. Never changes server settings or downloads a model.
internal sealed record EvaluationLocalModel(string Name, string Digest, string[] Capabilities)
{
    public LocalModelPolicy Policy => new(new LocalModelCapability(Name, Capabilities.Contains("thinking", StringComparer.Ordinal)));

    public static async Task<EvaluationLocalModel> Load(HttpClient http, string endpoint, CancellationToken ct = default)
    {
        var name = Environment.GetEnvironmentVariable("NOVA_EVAL_MODEL") ?? LocalModelPolicy.Production.DefaultModel;
        using var tags = JsonDocument.Parse(await http.GetStringAsync(endpoint + "/api/tags", ct));
        var installed = tags.RootElement.GetProperty("models").EnumerateArray().SingleOrDefault(item => item.GetProperty("name").GetString() == name);
        if (installed.ValueKind != JsonValueKind.Object) throw new ArgumentException("The explicit evaluation model must already be installed locally.");
        var digest = installed.GetProperty("digest").GetString() ?? throw new InvalidOperationException("Missing model digest.");
        using var response = await http.PostAsJsonAsync(endpoint + "/api/show", new { model = name }, ct);
        response.EnsureSuccessStatusCode();
        using var show = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var capabilities = show.RootElement.GetProperty("capabilities").EnumerateArray().Select(item => item.GetString()!).ToArray();
        if (!capabilities.Contains("completion", StringComparer.Ordinal)) throw new ArgumentException("The evaluation model does not declare completion capability.");
        return new(name, digest, capabilities);
    }
}
