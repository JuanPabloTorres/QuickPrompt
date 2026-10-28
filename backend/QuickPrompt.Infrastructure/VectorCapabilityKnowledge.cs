using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed class VectorCapabilityKnowledge(ILocalEmbeddings embeddings, CapabilityVectorStore store, CapabilityGraph? snapshot = null) : ICapabilityKnowledge
{
    private readonly CapabilityGraph graph = snapshot ?? CapabilityGraph.FromReflection();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, CapabilityKnowledgeContext> queryCache = new(StringComparer.Ordinal);
    private const string ProfileVersion = "nomic-prefix-chunk384-mean-unit-v1";
    public async Task<CapabilityKnowledgeContext> Retrieve(string directive, CancellationToken ct)
    {
        using var measurement = NovaTelemetry.Measure("retrieval.capabilities.vector");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct); budget.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            await gate.WaitAsync(budget.Token);
            try { return await RetrieveCore(directive, budget.Token); }
            finally { gate.Release(); }
        }
        catch (Exception exception) when (!ct.IsCancellationRequested && exception is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or ValidationException or Microsoft.Data.Sqlite.SqliteException)
        {
            // Optional knowledge cannot turn an inference/server failure into authority,
            // an arbitrary nearest action or an outage of existing domain operations.
            return CapabilityKnowledgeContext.Unavailable(graph.Version);
        }
    }
    private async Task<CapabilityKnowledgeContext> RetrieveCore(string directive, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(directive) || Encoding.UTF8.GetByteCount(directive) > 12000)
            throw new ValidationException("Contexto vectorial vacío o demasiado extenso; no se truncará.");
        var identity = await embeddings.Identity(ct);
        var profile = identity.Model + ":" + identity.Digest + ":" + ProfileVersion;
        var cacheKey = Hash(profile + graph.Version + directive);
        if (queryCache.TryGetValue(cacheKey, out var memo)) return memo with { Status = "CACHE_HIT" };
        var hashes = graph.Nodes.ToDictionary(node => node.Id, node => Hash(JsonSerializer.Serialize(node)), StringComparer.Ordinal);
        var vectors = store.Read(profile, hashes);
        var missing = graph.Nodes.Where(node => !vectors.ContainsKey(node.Id)).ToArray();
        if (missing.Length > 0)
        {
            var generated = await Encode(missing.Select(node => node.Description).ToArray(), "search_document: ", ct);
            foreach (var pair in missing.Zip(generated)) vectors.Add(pair.First.Id, pair.Second);
        }
        var query = (await Encode([directive], "search_query: ", ct))[0];
        if (await embeddings.Identity(ct) != identity) throw new ValidationException("Cambió la identidad del modelo durante el cálculo; no se mezclarán vectores.");
        ct.ThrowIfCancellationRequested();
        if (vectors.Values.Any(vector => vector.Length != query.Length)) throw new ValidationException("El perfil de embeddings cambió de dimensión.");
        if (missing.Length > 0) store.Replace(profile, hashes, vectors);
        // Numerical ranking only retrieves knowledge. It selects no user goal,
        // source identity, execution permission or domain effect.
        var nearest = graph.Nodes.Where(node => node.Kind == "OPERATION")
            .OrderByDescending(node => VectorMath.Cosine(query, vectors[node.Id])).ThenBy(node => node.Id, StringComparer.Ordinal).Take(5).ToArray();
        var relationships = graph.Edges.Where(edge => nearest.Any(node => node.Id == edge.From)).ToArray();
        var tools = relationships.Select(edge => edge.To).Distinct(StringComparer.Ordinal).Take(10).ToHashSet(StringComparer.Ordinal);
        var nodes = nearest.Concat(graph.Nodes.Where(node => tools.Contains(node.Id))).ToArray();
        var ids = nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var context = new CapabilityKnowledgeContext("READY", graph.Version, profile, nodes, relationships.Where(edge => ids.Contains(edge.To)).ToArray());
        if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(context)) > 16000) throw new ValidationException("El subgrafo recuperado excede su presupuesto.");
        if (queryCache.Count >= 64) queryCache.Remove(queryCache.Keys.First());
        queryCache.Add(cacheKey, context);
        return context;
    }
    private async Task<float[][]> Encode(string[] texts, string prefix, CancellationToken ct)
    {
        var pieces = texts.SelectMany((text, index) => Chunks(text).Select(chunk => new { Index = index, Text = prefix + chunk })).ToArray();
        var vectors = new List<float[]>();
        foreach (var batch in pieces.Chunk(16))
        {
            var result = await embeddings.Embed(batch.Select(piece => piece.Text).ToArray(), ct);
            if (result.Length != batch.Length) throw new ValidationException("Lote de embeddings incompleto.");
            vectors.AddRange(result.Select(VectorMath.Unit));
        }
        if (vectors.Select(vector => vector.Length).Distinct().Count() != 1) throw new ValidationException("Dimensiones incompatibles en el lote.");
        return Enumerable.Range(0, texts.Length).Select(index => {
            var selected = pieces.Select((piece, offset) => (piece, offset)).Where(pair => pair.piece.Index == index).Select(pair => vectors[pair.offset]).ToArray();
            return VectorMath.Unit(Enumerable.Range(0, selected[0].Length).Select(dimension => (float)selected.Average(vector => (double)vector[dimension])).ToArray());
        }).ToArray();
    }
    internal static string[] Chunks(string text)
    {
        var chunks = new List<string>(); var current = new StringBuilder(); var bytes = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > 384) { chunks.Add(current.ToString()); current.Clear(); bytes = 0; }
            current.Append(rune.ToString()); bytes += rune.Utf8SequenceLength;
        }
        if (current.Length > 0) chunks.Add(current.ToString());
        if (chunks.Count == 0) throw new ValidationException("Documento de capacidad vacío.");
        return chunks.ToArray();
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
