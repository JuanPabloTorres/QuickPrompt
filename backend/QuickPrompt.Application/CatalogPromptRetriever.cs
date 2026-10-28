using System.Text.RegularExpressions;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

/// <summary>Bounded OR recall and ranking shared by local and actor-bound catalogs. Ranking grants no action authority.</summary>
public sealed class CatalogPromptRetriever(IPromptCatalog catalog, ICollectionReader collections) : IPromptRetriever
{
    public PromptDocument ReadSource(Guid id)
    {
        using var measurement = NovaTelemetry.Measure("retrieval.source");
        var source = catalog.Get(id);
        if (source.IsArchived) throw new ValidationException("El origen está archivado.");
        return source;
    }

    public RetrievalCandidate[] Retrieve(string query, Guid? sourceId) => Retrieve(query, sourceId, null);

    public RetrievalCandidate[] Retrieve(string query, Guid? sourceId, RetrievalFilter? filter)
    {
        using var measurement = NovaTelemetry.Measure("retrieval.catalog");
        if (query is null || query.Length > 16000) throw new ValidationException("Consulta fuera de límites.");
        var found = new Dictionary<Guid, PromptDocument>();
        void Add(CatalogQuery search)
        {
            foreach (var item in catalog.Search(search with { GroupId = filter?.GroupId, Category = filter?.CategoryId?.ToString() ?? "", Favorites = filter?.Favorites == true || search.Favorites }).Items)
                found.TryAdd(item.Id, item);
        }
        // Syntax tokenization only: preserve internal dots and technical prefixes; never infer an operation.
        var terms = Regex.Matches(PromptSearch.Normalize(query), @"\.?[\p{L}\p{N}+#]+(?:\.[\p{L}\p{N}+#]+)*", RegexOptions.None, TimeSpan.FromMilliseconds(100))
            .Select(match => match.Value).Where(term => term.Length > 1).Distinct().Take(12).ToArray();
        foreach (var term in terms) Add(new(Search: term, Size: 12));
        Add(new(Favorites: true, Size: 6)); Add(new(Size: 6, Sort: "recent", Recent: true));
        if (found.Count == 0) Add(new(Size: 6));
        if (sourceId is { } id) found[id] = ReadSource(id);
        var groupNames = collections.Groups().ToDictionary(group => group.Group.Id, group => group.Group.Name);
        var duplicates = found.Values.GroupBy(prompt => PromptSearch.Normalize(prompt.TemplateText.Replace("\r\n", "\n").Trim()))
            .Where(group => group.Count() > 1).SelectMany(group => group.Select(prompt => prompt.Id)).ToHashSet();
        return found.Values.Select(prompt =>
        {
            var title = PromptSearch.Normalize(prompt.Title); var body = PromptSearch.Normalize(prompt.Description + " " + prompt.TemplateText);
            var groups = PromptSearch.Normalize(string.Join(' ', collections.Memberships(prompt.Id).Select(group => groupNames.GetValueOrDefault(group, ""))));
            var score = terms.Sum(term => title.Contains(term, StringComparison.Ordinal) ? 4 : body.Contains(term, StringComparison.Ordinal) ? 2 : groups.Contains(term, StringComparison.Ordinal) ? 3 : 0)
                + (prompt.IsFavorite ? .25 : 0) + (prompt.LastUsedAt.HasValue ? .1 : 0) + (prompt.Id == sourceId ? 100 : 0);
            var reason = prompt.Id == sourceId ? "Origen seleccionado explícitamente." : score >= 2 ? "Coincidencias literales; prioridad por título, favoritos y uso." : "Contexto reciente o de colección; requiere selección semántica.";
            return new RetrievalCandidate(prompt, score, reason + (duplicates.Contains(prompt.Id) ? " Contenido duplicado en los candidatos; conserva la identidad de cada registro." : ""));
        }).OrderByDescending(candidate => candidate.Score).ThenBy(candidate => candidate.Prompt.Id).Take(6).ToArray();
    }
}
