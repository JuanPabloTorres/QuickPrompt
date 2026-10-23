using System.Net.Http.Json;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Uses the installed multilingual embedder. No pull, cloud or generative-model fallback.
public sealed class OllamaEmbeddings : ILocalEmbeddings, IDisposable
{
    public const string Model = "nomic-embed-text-v2-moe:latest";
    private readonly HttpClient http;
    public OllamaEmbeddings(string endpoint)
    {
        http = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) {
            BaseAddress = OllamaClient.ValidateEndpoint(endpoint), Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 2000000
        };
    }
    public async Task<EmbeddingIdentity> Identity(CancellationToken ct)
    {
        using var response = await http.GetAsync("api/tags", ct); response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var installed = body.RootElement.GetProperty("models").EnumerateArray().SingleOrDefault(model => model.GetProperty("name").GetString() == Model);
        if (installed.ValueKind != JsonValueKind.Object || installed.GetProperty("digest").GetString() is not { Length: 64 } digest)
            throw new ValidationException("El modelo local de embeddings requerido no está instalado; no se descargará automáticamente.");
        return new(Model, digest);
    }
    public async Task<float[][]> Embed(string[] input, CancellationToken ct)
    {
        if (input.Length is < 1 or > 16 || input.Any(text => string.IsNullOrWhiteSpace(text) || System.Text.Encoding.UTF8.GetByteCount(text) > 450))
            throw new ValidationException("El lote de embeddings excede su contrato acotado.");
        using var response = await http.PostAsJsonAsync("api/embed", new { model = Model, input, truncate = false, keep_alive = "1m" }, ct);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (body.RootElement.GetProperty("model").GetString() != Model) throw new ValidationException("El servidor cambió el modelo de embeddings solicitado.");
        var vectors = body.RootElement.GetProperty("embeddings").Deserialize<float[][]>() ?? [];
        if (vectors.Length != input.Length || vectors.Any(vector => vector is null || vector.Length is < 1 or > 4096))
            throw new ValidationException("El servidor devolvió un lote de vectores incompatible.");
        return vectors.Select(VectorMath.Unit).ToArray();
    }
    public void Dispose() => http.Dispose();
}

public static class VectorMath
{
    public static float[] Unit(float[] vector)
    {
        if (vector.Length is < 1 or > 4096 || vector.Any(value => !float.IsFinite(value))) throw new ValidationException("Vector no finito o inválido.");
        var norm = Math.Sqrt(vector.Sum(value => (double)value * value));
        if (norm < 1e-12) throw new ValidationException("Vector sin magnitud.");
        return vector.Select(value => (float)(value / norm)).ToArray();
    }
    public static double Cosine(float[] left, float[] right)
    {
        if (left.Length != right.Length) throw new ValidationException("Dimensiones de embeddings incompatibles.");
        return left.Zip(right, (a, b) => (double)a * b).Sum();
    }
}
