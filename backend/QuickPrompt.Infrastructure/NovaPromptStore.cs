using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed class NovaPromptStore(LocalStore store) : IPromptReader, IPromptWriter
{
    public Template? Find(Guid id) => store.FindTemplate(id);
    public CatalogPage<Template> Search(CatalogQuery query) => store.QueryTemplates(query);
    public Template Create(Template prompt) => store.Save(prompt, "nova create");
    public Template Change(Guid id, int revision, Action<Template> change, string reason) => store.NovaChange(id, revision, change, reason);
    public void Delete(Guid id, int revision) => store.NovaDelete(id, revision);
    public CompiledPrompt Preview(Guid id, int revision, Dictionary<string, string> values) => store.NovaPreview(id, revision, values);
    public void RecordUse(Guid id, UseInput input) => store.NovaRecordUse(id, input);
}

public sealed partial class LocalStore
{
    public Template NovaChange(Guid id, int revision, Action<Template> change, string reason) => Write((db, tx) =>
    {
        var template = GetOne<Template>(db, "template", id, tx) ?? throw new MissingPromptException();
        if (template.Revision != revision) throw new ConflictException("El prompt cambió. Recarga antes de guardar.");
        change(template);
        return SaveInside(db, tx, template, reason);
    });
    public void NovaDelete(Guid id, int revision) => Write((db, tx) =>
    {
        var template = GetOne<Template>(db, "template", id, tx) ?? throw new MissingPromptException();
        if (template.Revision != revision) throw new ConflictException("El prompt cambió. Recarga antes de eliminar.");
        using var command = db.CreateCommand(); command.Transaction = tx;
        command.CommandText = "DELETE FROM Documents WHERE (Kind='template' AND Id=$id) OR (Kind IN ('version','proposal','combination') AND json_extract(Payload,'$.templateId')=$id)";
        command.Parameters.AddWithValue("$id", id.ToString());
        return command.ExecuteNonQuery();
    });
    public CompiledPrompt NovaPreview(Guid id, int revision, Dictionary<string, string> values) => Read(db =>
    {
        using var tx = db.BeginTransaction();
        var (template, compiled) = BuildCompiled(db, tx, id, values, null, "");
        if (template.Revision != revision) throw new ConflictException("El prompt cambió. Recarga antes de copiar.");
        return compiled;
    });
    // Compile is read-only; usage is acknowledged by the UI only after clipboard success.
    // Persisted receipt makes retries idempotent, including across a process restart.
    public void NovaRecordUse(Guid id, UseInput input) => Write((db, tx) =>
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new { id, input.Revision, input.ExpectedText, values = input.Values.OrderBy(x => x.Key, StringComparer.Ordinal) }, Json))));
        var previous = GetOne<UseReceipt>(db, "nova-use", input.OperationId, tx);
        if (previous is not null)
        {
            if (previous.Hash != hash) throw new ConflictException("Identificador de uso ya utilizado.");
            return true;
        }
        var (template, compiled) = BuildCompiled(db, tx, id, input.Values, null, "");
        if (template.Revision != input.Revision) throw new ConflictException("El prompt cambió. La copia no se registró.");
        if (compiled.Text != input.ExpectedText) throw new ConflictException("Las instrucciones cambiaron. El texto copiado se conserva, pero no se registró otro contenido.");
        Put(db, tx, "compiled", compiled.Id.ToString(), compiled);
        template.Uses++; template.LastUsed = DateTime.UtcNow;
        Put(db, tx, "template", id.ToString(), template);
        Put(db, tx, "nova-use", input.OperationId.ToString(), new UseReceipt(input.OperationId, hash));
        return true;
    });
    private sealed record UseReceipt(Guid Id, string Hash);
}
