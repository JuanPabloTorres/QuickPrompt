using System.Collections.Concurrent;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed class SqlitePromptRetriever(LocalStore store) : IPromptRetriever
{
    public RetrievalCandidate[] Retrieve(string query, Guid? sourceId)
    {
        var found = new Dictionary<Guid, Template>();
        void Add(CatalogQuery q) { foreach (var item in store.QueryTemplates(q).Items) found.TryAdd(item.Id, item); }
        // Bounded OR recall across existing SQL title/body/category/group search. No vectors or new schema.
        var terms = PromptSearch.Terms(query).Where(t => t.Length > 1).Take(6).ToArray();
        foreach (var term in terms) Add(new(Search: term, Size: 12));
        Add(new(Favorites: true, Size: 6)); Add(new(Size: 6, Sort: "recent", Recent: true));
        if (found.Count == 0) Add(new(Size: 6));
        if (sourceId is { } id)
        {
            var source = store.FindTemplate(id) ?? throw new MissingPromptException();
            if (!source.IsActive) throw new ValidationException("El origen está archivado.");
            found[id] = source;
        }
        return found.Values.Select(t => {
            var title = PromptSearch.Normalize(t.Title); var body = PromptSearch.Normalize(t.Description + " " + t.Template);
            var score = terms.Sum(term => title.Contains(term, StringComparison.Ordinal) ? 4 : body.Contains(term, StringComparison.Ordinal) ? 2 : 0)
                + (t.IsFavorite ? .25 : 0) + (t.LastUsed.HasValue ? .1 : 0) + (t.Id == sourceId ? 100 : 0);
            return new RetrievalCandidate(PromptLibrary.Document(t), score, t.Id == sourceId ? "Origen seleccionado explícitamente." : score >= 2 ? "Coincidencias literales; prioridad por título, favoritos y uso." : "Contexto reciente o de colección; requiere selección semántica.");
        }).OrderByDescending(c => c.Score).ThenBy(c => c.Prompt.Id).Take(6).ToArray();
    }
}

public sealed record StoredPreparation(string Fingerprint, PreparedDraft Draft);
public sealed class PreparedDraftStore(LocalStore store) : IPreparedDraftStore
{
    private readonly ConcurrentDictionary<Guid, StoredPreparation> transient = new();
    private void Trim()
    {
        foreach (var pair in transient.Where(p => p.Value.Draft.CreatedAt < DateTime.UtcNow.AddHours(-2))) transient.TryRemove(pair.Key, out _);
        foreach (var pair in transient.OrderBy(p => p.Value.Draft.CreatedAt).Take(Math.Max(0, transient.Count - 100))) transient.TryRemove(pair.Key, out _);
    }
    private StoredPreparation? Find(Guid id) { Trim(); return transient.GetValueOrDefault(id) ?? store.GetPreparation(id); }
    public PreparedDraft? Get(Guid id) => Find(id)?.Draft;
    public string? Fingerprint(Guid id) => Find(id)?.Fingerprint ?? store.PreparationFingerprint(id);
    public void Put(PreparedDraft draft, string fingerprint)
    {
        var value = new StoredPreparation(fingerprint, draft);
        if (Find(draft.Id) is { } previous && previous.Fingerprint != fingerprint) throw new ConflictException("Identificador de borrador ya utilizado.");
        store.PutPreparation(value); // Always retain a hash-only receipt; content persistence is opt-in.
        if (!draft.Remembered) transient[draft.Id] = value;
        Trim();
    }
    public PreparedDraft[] Recent() => store.RecentPreparations();
    public void Forget(Guid id) { transient.TryRemove(id, out _); store.ForgetPreparation(id); }
    public PromptDocument Save(Guid id) => store.FindPreparedSave(id) ?? store.SavePreparation(Get(id) ?? throw new ConflictException("El borrador ya no está disponible. Prepara uno nuevo o recupera uno conservado."));
}

public sealed partial class LocalStore
{
    public StoredPreparation? GetPreparation(Guid id) => Read(db => GetOne<StoredPreparation>(db, "nova-draft", id));
    public string? PreparationFingerprint(Guid id) => Read(db => GetOne<PreparationReceipt>(db, "nova-preparation", id)?.Fingerprint);
    public PreparedDraft[] RecentPreparations() => Read(db => Get<StoredPreparation>(db, "nova-draft").OrderByDescending(p => p.Draft.CreatedAt).Take(20).Select(p => p.Draft).ToArray());
    public void PutPreparation(StoredPreparation value) => Write((db, tx) =>
    {
        var previous = GetOne<PreparationReceipt>(db, "nova-preparation", value.Draft.Id, tx);
        if (previous is not null && previous.Fingerprint != value.Fingerprint) throw new ConflictException("Identificador de borrador ya utilizado.");
        Put(db, tx, "nova-preparation", value.Draft.Id.ToString(), new PreparationReceipt(value.Fingerprint));
        if (value.Draft.Remembered) Put(db, tx, "nova-draft", value.Draft.Id.ToString(), value); return true;
    });
    public void ForgetPreparation(Guid id) => Write((db, tx) => { using var command = db.CreateCommand(); command.Transaction = tx;
        command.CommandText = "DELETE FROM Documents WHERE Kind='nova-draft' AND Id=$id"; command.Parameters.AddWithValue("$id", id.ToString()); return command.ExecuteNonQuery(); });
    public PromptDocument? FindPreparedSave(Guid id) => Read(db =>
    {
        var receipt = GetOne<PreparedSaveReceipt>(db, "nova-prepared-save", id);
        return receipt is null ? null : PromptLibrary.Document(GetOne<Template>(db, "template", receipt.PromptId)
            ?? throw new ConflictException("El prompt guardado se eliminó. No se recreará al reintentar."));
    });
    public PromptDocument SavePreparation(PreparedDraft draft) => Write((db, tx) =>
    {
        var receipt = GetOne<PreparedSaveReceipt>(db, "nova-prepared-save", draft.Id, tx);
        if (receipt is not null) return PromptLibrary.Document(GetOne<Template>(db, "template", receipt.PromptId, tx)
            ?? throw new ConflictException("El prompt guardado se eliminó. No se recreará al reintentar."));
        if (draft.State is not (PreparationState.READY or PreparationState.NEEDS_INFORMATION) || string.IsNullOrWhiteSpace(draft.TemplateText)) throw new ValidationException("No hay una plantilla válida para guardar.");
        Template? prompt = null;
        if (draft.Decision == PreparationDecision.REUSE_EXISTING)
        {
            prompt = GetOne<Template>(db, "template", draft.SourceId!.Value, tx) ?? throw new MissingPromptException();
            if (prompt.Revision != draft.SourceRevision || !prompt.IsActive) throw new ConflictException("El origen cambió; prepara el prompt de nuevo.");
        }
        // Exact normalized content deduplication is explainable and independent of model similarity claims.
        prompt ??= Get<Template>(db, "template", tx).FirstOrDefault(t => t.IsActive && NormalizePrepared(t.Template) == NormalizePrepared(draft.TemplateText));
        if (prompt is null)
        {
            var names = TemplateCompiler.Names(draft.TemplateText);
            var defaults = draft.Candidates.SingleOrDefault(c => c.Prompt.Id == draft.SourceId)?.Prompt.Defaults
                .Where(value => names.Contains(value.Key)).ToDictionary() ?? new Dictionary<string, string>();
            prompt = new Template { Id = draft.Id, Title = draft.Title, Description = draft.Description, Template = draft.TemplateText,
                Category = (QuickPrompt.Domain.Enums.PromptCategory)draft.CategoryId, Variables = defaults };
            if (GetOne<Template>(db, "template", prompt.Id, tx) is not null) throw new ConflictException("Identificador ocupado.");
            SaveInside(db, tx, prompt, "nova prepared copy");
        }
        Put(db, tx, "nova-prepared-save", draft.Id.ToString(), new PreparedSaveReceipt(prompt.Id));
        return PromptLibrary.Document(prompt);
    });
    private static string NormalizePrepared(string text) => text.Replace("\r\n", "\n").Trim();
    private sealed record PreparedSaveReceipt(Guid PromptId);
    private sealed record PreparationReceipt(string Fingerprint);
}
