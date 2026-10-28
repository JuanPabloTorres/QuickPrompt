using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class CapabilityVectorTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-vector-tests-" + Guid.NewGuid());
    private string Database => Path.Combine(directory, "knowledge.sqlite");
    private static readonly CapabilityGraph Graph = new("fixture-v1", [
        new("operation:FILL_VARIABLES", "OPERATION", "Apply actual input values."),
        new("operation:EXECUTE_LOCAL", "OPERATION", "Run with explicit authority."),
        new("tool:ui_fill_variable", "UI_TOOL", "Fill a real input.", "READ_OR_PREPARE"),
        new("tool:ui_execute_local", "UI_TOOL", "Execute with local model.", "EXPLICIT_LOCAL_EXECUTION")
    ], [new("operation:FILL_VARIABLES", "IMPLEMENTED_BY", "tool:ui_fill_variable"), new("operation:EXECUTE_LOCAL", "IMPLEMENTED_BY", "tool:ui_execute_local")]);

    [Fact] public void ReflectionGraphContainsOnlyCurrentSystemCapabilitiesWithGroundedEdges()
    {
        var graph = CapabilityGraph.FromReflection(); Assert.Equal(42, graph.Nodes.Length);
        Assert.Equal(graph.Version, CapabilityGraph.FromReflection().Version);
        Assert.Equal(21, graph.Nodes.Count(node => node.Kind == "OPERATION"));
        Assert.Contains(graph.Edges, edge => edge.From == "operation:COPY_TO_CLIPBOARD" && edge.To == "tool:ui_copy_compiled_prompt");
        Assert.All(graph.Edges, edge => { Assert.Contains(graph.Nodes, node => node.Id == edge.From); Assert.Contains(graph.Nodes, node => node.Id == edge.To); });
        Assert.All(graph.Nodes.Where(node => node.Kind == "UI_TOOL"), node => Assert.NotNull(node.Arguments));
    }

    [Fact] public async Task PersistentVectorsAndBoundedQueryMemoAvoidReembeddingUnchangedCapabilities()
    {
        var embeddings = new FakeEmbeddings();
        var service = new VectorCapabilityKnowledge(embeddings, new(Database), Graph);
        var first = await service.Retrieve("petición sintética", default); Assert.Equal("READY", first.Status);
        var encoded = embeddings.Texts.Count;
        var again = await service.Retrieve("petición sintética", default); Assert.Equal("CACHE_HIT", again.Status); Assert.Equal(encoded, embeddings.Texts.Count);
        var reopened = new VectorCapabilityKnowledge(embeddings, new(Database), Graph);
        var afterRestart = await reopened.Retrieve("otra petición sintética", default);
        Assert.Equal("READY", afterRestart.Status); Assert.Equal(encoded + 1, embeddings.Texts.Count);
        Assert.All(afterRestart.Edges, edge => Assert.Contains(afterRestart.Nodes, node => node.Id == edge.To));
        Assert.Contains("neither a user request nor permission", afterRestart.Authority);
    }

    [Fact] public async Task ModelIdentityChangesInvalidateVectorsAndQueryMemo()
    {
        var embeddings = new FakeEmbeddings(); var service = new VectorCapabilityKnowledge(embeddings, new(Database), Graph);
        await service.Retrieve("mismo texto", default); var encoded = embeddings.Texts.Count;
        embeddings.Digest = new('b', 64);
        var changed = await service.Retrieve("mismo texto", default);
        Assert.Equal("READY", changed.Status); Assert.Equal(encoded * 2, embeddings.Texts.Count);
        Assert.Contains(embeddings.Digest, changed.EmbeddingIdentity);
    }

    [Fact] public async Task ChangedAndRemovedMetadataCannotSurviveAReflectionSnapshotChange()
    {
        var embeddings = new FakeEmbeddings(); var store = new CapabilityVectorStore(Database);
        await new VectorCapabilityKnowledge(embeddings, store, Graph).Retrieve("consulta", default);
        var changed = Graph with { Version = "fixture-v2", Nodes = [Graph.Nodes[0] with { Description = "Changed input contract." }], Edges = [] };
        var result = await new VectorCapabilityKnowledge(embeddings, store, changed).Retrieve("consulta", default);
        Assert.Equal("Changed input contract.", Assert.Single(result.Nodes).Description); Assert.Empty(result.Edges);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ModelFailureDoesNotInventAnActionAndExplicitCancellationStillPropagates(int fault)
    {
        var failure = fault switch { 0 => (Exception)new HttpRequestException("synthetic unavailable"), 1 => new KeyNotFoundException("synthetic missing metadata"), _ => new InvalidOperationException("synthetic wrong JSON property type") };
        var embeddings = new FakeEmbeddings { Failure = failure }; var service = new VectorCapabilityKnowledge(embeddings, new(Database), Graph);
        var result = await service.Retrieve("consulta", default); Assert.Equal("UNAVAILABLE", result.Status); Assert.Empty(result.Nodes); Assert.Empty(result.Edges);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.Retrieve("consulta", cancellation.Token));
    }

    [Fact] public async Task UnicodeRequestsAreChunkedWithoutLossAndEmbeddingsUseTheDocumentAndQueryPrefixes()
    {
        var embeddings = new FakeEmbeddings(); var service = new VectorCapabilityKnowledge(embeddings, new(Database), Graph);
        var query = string.Concat(Enumerable.Repeat("α🪐petición ", 100));
        var result = await service.Retrieve(query, default); Assert.Equal("READY", result.Status);
        Assert.Contains(embeddings.Texts, text => text.StartsWith("search_document: ", StringComparison.Ordinal));
        var chunks = embeddings.Texts.Where(text => text.StartsWith("search_query: ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(query, string.Concat(chunks.Select(text => text["search_query: ".Length..])));
        Assert.All(embeddings.Texts, text => Assert.InRange(Encoding.UTF8.GetByteCount(text), 1, 450));
    }

    [Fact] public void VectorDatabaseRejectsDomainDatabasesWithoutModifyingThem()
    {
        var domain = new LocalStore(Database); var before = domain.NovaSnapshot();
        Assert.Throws<ValidationException>(() => new CapabilityVectorStore(Database)); Assert.Equal(before, domain.NovaSnapshot());
    }

    [Theory]
    [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void InvalidVectorComponentsCannotEnterThePersistentIndex(float invalid)
    {
        var store = new CapabilityVectorStore(Database);
        Assert.Throws<ValidationException>(() => store.Replace("model", new Dictionary<string, string> { ["x"] = "hash" }, new Dictionary<string, float[]> { ["x"] = [invalid, 1] }));
        Assert.Empty(store.Read("model", new Dictionary<string, string> { ["x"] = "hash" }));
    }

    [Fact] public void ExactVectorScoringUsesNumericalDirectionAndRejectsIncompatibleDimensions()
    {
        Assert.Equal(1, VectorMath.Cosine(VectorMath.Unit([2, 0]), VectorMath.Unit([9, 0])), 6);
        Assert.Equal(0, VectorMath.Cosine(VectorMath.Unit([2, 0]), VectorMath.Unit([0, 9])), 6);
        Assert.Throws<ValidationException>(() => VectorMath.Cosine([1, 0], [1]));
        Assert.Throws<ValidationException>(() => VectorMath.Unit([0, 0]));
    }

    private sealed class FakeEmbeddings : ILocalEmbeddings
    {
        public string Digest { get; set; } = new('a', 64);
        public List<string> Texts { get; } = [];
        public Exception? Failure { get; init; }
        public Task<EmbeddingIdentity> Identity(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(new EmbeddingIdentity(OllamaEmbeddings.Model, Digest)); }
        public Task<float[][]> Embed(string[] input, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); if (Failure is not null) throw Failure; Texts.AddRange(input);
            return Task.FromResult(input.Select(_ => new float[] { 1, 0 }).ToArray());
        }
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
