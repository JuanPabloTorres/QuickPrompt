using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol;
using QuickPrompt.Nova.Application;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Mcp;

/// <summary>Calls only the configured loopback library API. No inference, automatic retry or direct database access.</summary>
public sealed class QuickPromptApiClient(HttpClient http)
{
    private sealed record PromptPage(List<PromptDocument> Items, int Total);

    /// <summary>Preserves catalogue filtering: collection membership intersects text search before paging.</summary>
    public async Task<PromptSearchResult> Search(string query, int page, int limit, CancellationToken ct, Guid? collectionId = null)
    {
        var collectionFilter = collectionId is { } id ? $"&groupId={id:D}" : "";
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/prompts?search={Uri.EscapeDataString(query)}&page={page}&size={limit}{collectionFilter}");
        using var response = await Send(request, false, ct);
        var result = await Read<PromptPage>(response, false, ct);
        return new(result.Items.Select(item => new PromptSummary(item.Id, item.Title, item.Description, item.Revision, item.VariableNames)).ToArray(), result.Total, page, limit);
    }

    /// <summary>Projects the bounded collection catalogue (at most 500 groups) to paged public metadata.</summary>
    public async Task<CollectionSearchResult> Collections(string query, int page, int limit, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/groups");
        using var response = await Send(request, false, ct);
        var groups = await Read<GroupSummary[]>(response, false, ct);
        var terms = PromptSearch.Terms(query);
        var matches = groups.Where(item => terms.All(term => PromptSearch.Normalize(item.Group.Name).Contains(term, StringComparison.Ordinal)))
            .Select(item => new CollectionSummary(item.Group.Id, item.Group.Name, item.Templates)).ToArray();
        return new(matches.Skip(page * limit).Take(limit).ToArray(), matches.Length, page, limit);
    }

    public async Task<PromptContent> Get(Guid id, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/prompts/{id}");
        using var response = await Send(request, false, ct);
        var result = await Read<PromptDocument>(response, false, ct);
        if (result.IsArchived) throw new McpException("PROMPT_UNAVAILABLE: el prompt está archivado.");
        return PromptContent.From(result);
    }

    public async Task<PromptCaptureReceipt> Capture(PromptCaptureInput input, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/prompts/capture") { Content = JsonContent.Create(input) };
        using var response = await Send(request, true, ct);
        var result = await Read<PromptCaptureResult>(response, true, ct);
        if (result.OperationId != input.OperationId || result.Prompt is not { Revision: 1 } saved || saved.Id == Guid.Empty
            || saved.Title != input.Prompt.Title || saved.TemplateText != input.Prompt.TemplateText || saved.Description != input.Prompt.Description)
            throw new McpException("CAPTURE_UNCONFIRMED: el recibo no coincide. Reintenta sólo con la misma operationId y contenido.");
        return new(result.OperationId, PromptContent.From(result.Prompt), result.Replayed);
    }

    /// <summary>Uses the existing revision-bound compiler. No usage, execution or other persistence is acknowledged.</summary>
    public async Task<PromptRendering> Compile(Guid id, int revision, Dictionary<string, string> values, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/prompts/{id}/render")
        { Content = JsonContent.Create(new CompileInput(revision, values)) };
        using var response = await Send(request, false, ct);
        return await Read<PromptRendering>(response, false, ct);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, bool mutation, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new JsonException();
        }
        catch (Exception error) when (error is JsonException or HttpRequestException or TaskCanceledException)
        {
            throw new McpException(mutation
                ? "CAPTURE_UNCONFIRMED: respuesta incompleta; reintenta sólo con la misma operationId y contenido."
                : "INVALID_LIBRARY_RESPONSE");
        }
    }

    private async Task<HttpResponseMessage> Send(HttpRequestMessage request, bool mutation, CancellationToken ct)
    {
        try
        {
            request.Headers.Add("X-QuickPrompt-Client", "nova-v1");
            var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return response;
            var status = response.StatusCode; response.Dispose();
            throw new McpException(status switch
            {
                HttpStatusCode.BadRequest => "INVALID_INPUT: revisa los datos y completa todas las variables requeridas; consulta get_prompt para ver sus nombres.",
                HttpStatusCode.NotFound => "PROMPT_UNAVAILABLE: el registro no existe.",
                HttpStatusCode.Conflict => mutation
                    ? "CAPTURE_CONFLICT: la operación o el prompt ya cambió. Recupera el registro; no crees otra copia automáticamente."
                    : "REVISION_CONFLICT: recupera la revisión actual con get_prompt antes de completar variables.",
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "LIBRARY_ACCESS_DENIED",
                _ => mutation ? "CAPTURE_UNCONFIRMED: consulta el registro o reintenta con la misma operationId." : "LIBRARY_UNAVAILABLE"
            });
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            throw new McpException(mutation
                ? "CAPTURE_UNCONFIRMED: se perdió la respuesta; reintenta sólo con la misma operationId y contenido."
                : "LIBRARY_UNAVAILABLE: inicia QuickPrompt y comprueba la conexión local.");
        }
    }
}

public record PromptSummary(Guid Id, string Title, string Description, int Revision, string[] VariableNames);
public record PromptSearchResult(PromptSummary[] Items, int Total, int Page, int Limit);
/// <summary>Only collection identity, name and active prompt count; excludes instruction profiles.</summary>
public record CollectionSummary(Guid Id, string Name, int PromptCount);
public record CollectionSearchResult(CollectionSummary[] Items, int Total, int Page, int Limit);
/// <summary>Only requested reusable content, excluding stored defaults, use history and private settings.</summary>
public record PromptContent(Guid Id, string Title, string TemplateText, string Description, int CategoryId, int Revision, string[] VariableNames)
{
    internal static PromptContent From(PromptDocument value) => new(value.Id, value.Title, value.TemplateText, value.Description, value.CategoryId, value.Revision, value.VariableNames);
}
public record PromptCaptureReceipt(Guid OperationId, PromptContent Prompt, bool Replayed);
