using System.Text.Json;
using Microsoft.Data.Sqlite;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

// Short local operations serialized with a single lock; SQLite transactions own all multi-entity mutations.
public sealed partial class LocalStore
{
    public const int CurrentSchemaVersion = 2;
    public const int MaxArchiveBytes = 32 * 1024 * 1024;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string connectionString;
    private readonly object gate = new();
    public LocalStore(string path)
    {
        SQLitePCL.Batteries_V2.Init();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        lock (gate)
        {
            using var db = Open();
            var originalVersion = SchemaMigrations.ReadVersion(db);
            if(originalVersion==1)SchemaMigrations.BackupBeforeMigration(db,path);
            using var tx = db.BeginTransaction();
            using var probe = db.CreateCommand(); probe.Transaction = tx;
            probe.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaVersion'";
            if (Convert.ToInt32(probe.ExecuteScalar()) != 0)
            {
                probe.CommandText = "SELECT COALESCE(MAX(Version),0) FROM SchemaVersion";
                if (Convert.ToInt32(probe.ExecuteScalar()) > CurrentSchemaVersion) throw new ValidationException("Versión de DB no soportada / Unsupported DB version.");
            }
            using var cmd = db.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS SchemaVersion(Version INTEGER PRIMARY KEY);
                CREATE TABLE IF NOT EXISTS Documents(Kind TEXT NOT NULL, Id TEXT NOT NULL, Payload TEXT NOT NULL CHECK(json_valid(Payload)), PRIMARY KEY(Kind,Id));
                CREATE INDEX IF NOT EXISTS IX_Documents_Title ON Documents(Kind, json_extract(Payload,'$.title'));
                INSERT OR IGNORE INTO SchemaVersion VALUES(1);
                """;
            cmd.ExecuteNonQuery();
            SchemaMigrations.Apply(db,tx);
            using var version = db.CreateCommand(); version.Transaction = tx; version.CommandText = "SELECT MAX(Version) FROM SchemaVersion";
            if (Convert.ToInt32(version.ExecuteScalar()) != CurrentSchemaVersion) throw new ValidationException("Versión de DB no soportada / Unsupported DB version.");
            if (Get<Preferences>(db,"preferences",tx).Count == 0) Put(db, tx, "preferences", "local", new Preferences());
            tx.Commit();
        }
    }
    private SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); db.CreateFunction<string,string>("qp_fold",value=>PromptSearch.Normalize(value??""),isDeterministic:true); using var c = db.CreateCommand(); c.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;"; c.ExecuteNonQuery(); return db; }
    private static List<T> Get<T>(SqliteConnection db, string kind, SqliteTransaction? tx = null)
    {
        using var c = db.CreateCommand(); c.Transaction = tx; c.CommandText = "SELECT Payload FROM Documents WHERE Kind=$kind"; c.Parameters.AddWithValue("$kind", kind);
        using var r = c.ExecuteReader(); var list = new List<T>();
        while (r.Read()) list.Add(JsonSerializer.Deserialize<T>(r.GetString(0), Json) ?? throw new ValidationException("Dato corrupto / Corrupt record."));
        return list;
    }
    private static void Put<T>(SqliteConnection db, SqliteTransaction? tx, string kind, string id, T value)
    {
        using var c = db.CreateCommand(); c.Transaction = tx; c.CommandText = "INSERT INTO Documents VALUES($k,$i,$p) ON CONFLICT(Kind,Id) DO UPDATE SET Payload=excluded.Payload";
        c.Parameters.AddWithValue("$k",kind); c.Parameters.AddWithValue("$i",id); c.Parameters.AddWithValue("$p",JsonSerializer.Serialize(value,Json)); c.ExecuteNonQuery();
    }
    private T Read<T>(Func<SqliteConnection,T> action) { lock(gate) { using var db = Open(); return action(db); } }
    private T Write<T>(Func<SqliteConnection,SqliteTransaction,T> action) { lock(gate) { using var db = Open(); using var tx = db.BeginTransaction(); var result = action(db,tx); tx.Commit(); return result; } }
    public List<Template> Templates() => Read(db => Get<Template>(db,"template"));
    public List<TemplateVersion> Versions(Guid id) => Read(db => {
        using var c=db.CreateCommand();c.CommandText="SELECT Payload FROM Documents WHERE Kind='version' AND json_extract(Payload,'$.templateId')=$id ORDER BY json_extract(Payload,'$.revision') DESC";c.Parameters.AddWithValue("$id",id.ToString());using var r=c.ExecuteReader();var list=new List<TemplateVersion>();while(r.Read())list.Add(JsonSerializer.Deserialize<TemplateVersion>(r.GetString(0),Json)!);return list;
    });
    public List<CompiledPrompt> History() => Read(db => Get<CompiledPrompt>(db,"compiled").OrderByDescending(v=>v.CreatedAt).ToList());
    public List<LlmExecution> Executions() => Read(db => Get<LlmExecution>(db,"execution"));
    public Preferences Settings() => Read(db => Get<Preferences>(db,"preferences").Single());
    public void SaveSettings(Preferences p)
    {
        if (p.Language is not ("es" or "en") || p.Theme is not ("light" or "dark") || !OllamaClient.Models.Contains(p.Model) || p.GeneralInstructions is null || p.GeneralInstructions.Length>4000) throw new ValidationException("Preferencias inválidas / Invalid preferences.");
        Write((db,tx)=> { Put(db,tx,"preferences","local",p); return true; });
    }
    public Template Save(Template value, string reason = "edit", Guid? proposalId = null) => Write((db,tx) => SaveInside(db,tx,value,reason,proposalId));
    private static Template SaveInside(SqliteConnection db, SqliteTransaction tx, Template value, string reason, Guid? proposalId = null)
    {
        value.Validate();
        var old = GetOne<Template>(db,"template",value.Id,tx);
        if (old != null && old.Revision != value.Revision) throw new ConflictException("La plantilla cambió. Recarga / Template changed. Reload.");
        if (proposalId != null)
        {
            var proposal = Get<AiProposal>(db,"proposal",tx).SingleOrDefault(p=>p.Id==proposalId);
            if (old is null || proposal is null || proposal.Status!="pending" || proposal.TemplateId!=value.Id || proposal.BaseRevision!=value.Revision || proposal.Text!=value.Template || !TemplateCompiler.Names(old.Template).Order().SequenceEqual(TemplateCompiler.Names(value.Template).Order())) throw new ConflictException("Propuesta inválida o desactualizada / Stale proposal.");
            Put(db,tx,"proposal",proposal.Id.ToString(),proposal with {Status="accepted"});
        }
                value.Revision = old is null ? 1 : old.Revision + 1;
        value.CreatedAt = old?.CreatedAt ?? (reason=="import" ? value.CreatedAt : DateTime.UtcNow); value.UpdatedAt=reason=="import" ? value.UpdatedAt : DateTime.UtcNow;
        value.Uses=old?.Uses ?? value.Uses; value.LastUsed=old?.LastUsed ?? value.LastUsed;
        Put(db,tx,"template",value.Id.ToString(),value);
        var version = new TemplateVersion(Guid.NewGuid(),value.Id,value.Revision,value,reason,DateTime.UtcNow);
        Put(db,tx,"version",version.Id.ToString(),version);
        return value;
    }
    public CompiledPrompt Preview(Guid id,Dictionary<string,string> values,Guid? instructionId=null,string adjustments="")=>Read(db=>{using var tx=db.BeginTransaction();return BuildCompiled(db,tx,id,values,instructionId,adjustments).Prompt;});
    public CompiledPrompt Compile(Guid id,Dictionary<string,string> values,Guid? instructionId=null,string adjustments="",int? expectedRevision=null)=>Write((db,tx)=>{
        var (template,p)=BuildCompiled(db,tx,id,values,instructionId,adjustments);
        if(expectedRevision is {} revision && template.Revision!=revision)throw new ConflictException("El prompt cambió. Actualiza la biblioteca / Prompt changed. Refresh the library.");
        Put(db,tx,"compiled",p.Id.ToString(),p);template.Uses++;template.LastUsed=DateTime.UtcNow;Put(db,tx,"template",id.ToString(),template);return p;
    });
    private static (Template Template,CompiledPrompt Prompt) BuildCompiled(SqliteConnection db,SqliteTransaction tx,Guid id,Dictionary<string,string> values,Guid? instructionId,string adjustments)
    {
        if(adjustments is null || adjustments.Length>4000)throw new ValidationException("Ajustes demasiado largos / Adjustments too long.");
        var template=GetOne<Template>(db,"template",id,tx) ?? throw new ValidationException("Plantilla no disponible / Template unavailable.");
        if(!template.IsActive)throw new ValidationException("Plantilla no disponible / Template unavailable.");
        var text=TemplateCompiler.Render(template.Template,values,template.Variables);
        var prefs=SettingsInside(db,tx);
        var profile=instructionId is {} profileId ? GetOne<InstructionProfile>(db,"instruction",profileId,tx) ?? throw new ValidationException("Instrucción no disponible / Instruction unavailable.") : null;
        PromptComposition? composition=null;
        if(!string.IsNullOrWhiteSpace(prefs.GeneralInstructions) || profile!=null || !string.IsNullOrWhiteSpace(adjustments))
        {
            composition=new(text,prefs.GeneralInstructions,profile?.Text??"",adjustments,profile?.Id,profile?.Revision,profile?.Name);composition.Validate();text=composition.ForExternalEditor();
            if(text.Length>64000)throw new ValidationException("Composición demasiado larga / Composition too large.");
        }
        var p=new CompiledPrompt(Guid.NewGuid(),id,template.Revision,template.Title,text,prefs.RememberValues ? new Dictionary<string,string>(values) : new Dictionary<string,string>(),DateTime.UtcNow,Composition:composition);
        return (template,p);
    }
    private static Preferences SettingsInside(SqliteConnection db,SqliteTransaction tx)=>Get<Preferences>(db,"preferences",tx).Single();
    public void FavoriteFinal(CompiledPrompt p) => Write((db,tx)=> { Put(db,tx,"compiled",p.Id.ToString(),p with {Favorite=!p.Favorite}); return true; });
    public void SaveExecution(LlmExecution execution) => Write((db,tx)=>
    {
        if (!Get<CompiledPrompt>(db,"compiled",tx).Any(p=>p.Id==execution.CompiledId) || !OllamaClient.Models.Contains(execution.Model) || string.IsNullOrWhiteSpace(execution.Response) || execution.Response.Length>64000) throw new ValidationException("Ejecución inválida / Invalid execution.");
        Put(db,tx,"execution",execution.Id.ToString(),execution); return true;
    });
    public void SaveProposal(AiProposal p) => Write((db,tx)=> { Put(db,tx,"proposal",p.Id.ToString(),p); return true; });
    public void DraftProposal(AiProposal p) => Write((db,tx)=> { if(p.TemplateId!=null)throw new ValidationException("Not a draft proposal.");Put(db,tx,"proposal",p.Id.ToString(),p with{Status="drafted"});return true; });
    // Explicitly stage an improvement in the editor; saving a template remains a separate action.
    public Template StageDraftImprovement(Guid proposalId, Template source) => Write((db,tx)=>
    {
        source.Validate();
        var proposal=GetOne<AiProposal>(db,"proposal",proposalId,tx);
        if(proposal is null || proposal.Status!="pending" || proposal.TemplateId!=source.Id || proposal.BaseRevision!=source.Revision || !TemplateCompiler.Names(source.Template).Order().SequenceEqual(TemplateCompiler.Names(proposal.Text).Order()))
            throw new ConflictException("Propuesta inválida o desactualizada / Stale proposal.");
        var candidate=JsonSerializer.Deserialize<Template>(JsonSerializer.Serialize(source,Json),Json)!;
        candidate.Template=proposal.Text;candidate.Validate();
        Put(db,tx,"proposal",proposal.Id.ToString(),proposal with{Status="drafted"});
        return candidate;
    });
    public void Reject(AiProposal p) => Write((db,tx)=> { Put(db,tx,"proposal",p.Id.ToString(),p with {Status="rejected"}); return true; });
    public void Delete(Guid id) => Write((db,tx)=>
    {
        // History remains independently reusable, even after deleting a template.
        using var c=db.CreateCommand(); c.Transaction=tx; c.CommandText="DELETE FROM Documents WHERE (Kind='template' AND Id=$id) OR (Kind IN ('version','proposal','combination') AND json_extract(Payload,'$.templateId')=$id)"; c.Parameters.AddWithValue("$id",id.ToString()); return c.ExecuteNonQuery();
    });
    public void ClearHistory() => Write((db,tx)=> { using var c=db.CreateCommand(); c.Transaction=tx; c.CommandText="DELETE FROM Documents WHERE Kind IN ('compiled','execution','web-transfer')"; return c.ExecuteNonQuery(); });
    public string CheckIntegrity() => Read(db=> { using var c=db.CreateCommand(); c.CommandText="PRAGMA integrity_check"; return (string)c.ExecuteScalar()!; });
    public string Export() => Read(db=>
    {
        using var tx=db.BeginTransaction(deferred:true);
        var archive=new Archive(2,Get<Template>(db,"template",tx).ToArray(),Get<TemplateVersion>(db,"version",tx).ToArray(),Get<CompiledPrompt>(db,"compiled",tx).ToArray(),Get<LlmExecution>(db,"execution",tx).ToArray(),Get<AiProposal>(db,"proposal",tx).ToArray(),Get<Preferences>(db,"preferences",tx).Single(),ExportGroups(db,tx).ToArray(),ExportMemberships(db,tx).ToArray(),Get<InstructionProfile>(db,"instruction",tx).ToArray(),Get<InstructionVersion>(db,"instruction-version",tx).ToArray(),Get<PromptCombination>(db,"combination",tx).ToArray(),Get<WebTransferActivity>(db,"web-transfer",tx).ToArray());
        var json=JsonSerializer.Serialize(archive,Json);
        if(System.Text.Encoding.UTF8.GetByteCount(json)>MaxArchiveBytes)throw new ValidationException("El respaldo supera 32 MiB. Reduce el historial antes de exportar; no se generó un archivo que no pueda restaurarse / Backup exceeds 32 MiB. Reduce history before exporting; no unrestorable file was generated.");
        tx.Commit();return json;
    });
    private static Archive ParseImport(string json, bool restore)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json)>MaxArchiveBytes) throw new ValidationException("JSON supera 32 MiB / JSON exceeds 32 MiB.");
        Archive archive;
        try
        {
            using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=32});
            ValidateKeys(doc.RootElement);
            if(doc.RootElement.ValueKind==JsonValueKind.Object && doc.RootElement.EnumerateObject().Any(p=>p.Name.Equals("format",StringComparison.OrdinalIgnoreCase)))
            {
                if(restore)throw new ValidationException("Restaurar requiere un respaldo completo / Restore requires a full backup.");
                var package=JsonSerializer.Deserialize<TemplatePackage>(json,Json)??throw new ValidationException("Plantilla inválida.");
                archive=new(1,[package.ToTemplate()],[],[],[],[],new());
            }
            else if (doc.RootElement.ValueKind==JsonValueKind.Object && doc.RootElement.EnumerateObject().Any(p=>p.Name.Equals("schemaVersion",StringComparison.OrdinalIgnoreCase)))
                archive=JsonSerializer.Deserialize<Archive>(json,Json) ?? throw new ValidationException("Archivo inválido.");
            else
            {
                if(restore)throw new ValidationException("Restaurar requiere un respaldo completo versionado / Restore requires a complete versioned backup.");
                var elements=doc.RootElement.ValueKind==JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToArray() : [doc.RootElement];
                var items=elements.Select(ReadLegacyJson).ToArray();
                archive=new(1,items,[],[],[],[],new());
            }
            ValidateArchive(archive);
        }
        catch (JsonException) { throw new ValidationException("JSON corrupto o incompatible / Invalid JSON."); }
        return archive;
    }
    public int Import(string json, bool restore = false)
    {
        var archive=ParseImport(json,restore);
        return Write((db,tx)=>
        {
            var existing=Get<Template>(db,"template",tx);
            if (restore)
            {
                // Restoring library content never grants permission to replay an already-started agent effect.
                using var clear=db.CreateCommand(); clear.Transaction=tx; clear.CommandText="DELETE FROM PromptGroups; DELETE FROM Documents WHERE Kind NOT IN ('nova-run-receipt','nova-live-receipt')"; clear.ExecuteNonQuery();
                foreach (var p in archive.Templates) Put(db,tx,"template",p.Id.ToString(),p);
                foreach (var v in archive.Versions) Put(db,tx,"version",v.Id.ToString(),v);
                foreach (var h in archive.History) Put(db,tx,"compiled",h.Id.ToString(),h);
                foreach (var e in archive.Executions) Put(db,tx,"execution",e.Id.ToString(),e);
                foreach (var p in archive.Proposals) Put(db,tx,"proposal",p.Id.ToString(),p);
                Put(db,tx,"preferences","local",archive.Preferences);
                RestoreOrganization(db,tx,archive);
                return archive.Templates.Length;
            }
            int added=0;
            foreach(var item in archive.Templates)
            {
                if (existing.Any(p=>p.Id==item.Id || (p.Title==item.Title && p.Template==item.Template))) continue;
                Put(db,tx,"template",item.Id.ToString(),item);
                var versions=archive.Versions.Where(v=>v.TemplateId==item.Id).ToArray();
                if(versions.Length==0){var v=new TemplateVersion(Guid.NewGuid(),item.Id,item.Revision,item,"import",DateTime.UtcNow);Put(db,tx,"version",v.Id.ToString(),v);}
                else foreach(var v in versions){if(GetOne<TemplateVersion>(db,"version",v.Id,tx)!=null)throw new ConflictException("Colisión de versiones / Version collision.");Put(db,tx,"version",v.Id.ToString(),v);}
                existing.Add(item); added++;
            }
            var histories=Get<CompiledPrompt>(db,"compiled",tx).Select(h=>h.Id).ToHashSet();
            foreach(var h in archive.History) if(histories.Add(h.Id))Put(db,tx,"compiled",h.Id.ToString(),h);
            var runs=Get<LlmExecution>(db,"execution",tx).Select(e=>e.Id).ToHashSet();
            foreach(var e in archive.Executions) if(runs.Add(e.Id))Put(db,tx,"execution",e.Id.ToString(),e);
            // Imported proposals are audit records only; never an authorization to apply changes.
            var proposals=Get<AiProposal>(db,"proposal",tx).Select(p=>p.Id).ToHashSet();
            foreach(var p in archive.Proposals)if(proposals.Add(p.Id))Put(db,tx,"proposal",p.Id.ToString(),p with{Status=p.Status=="pending"?"rejected":p.Status});
            MergeOrganization(db,tx,archive,existing);
            return added;
        });
    }
    private static void ValidateArchive(Archive a)
    {
        if(a.SchemaVersion is not (1 or 2) || a.Templates is null || a.Templates.Length>5000 || a.Versions is null || a.History is null || a.Executions is null || a.Proposals is null || a.Preferences is null || a.Versions.Any(v=>v is null || v.Snapshot is null) || a.History.Any(h=>h is null) || a.Executions.Any(e=>e is null) || a.Proposals.Any(p=>p is null)) throw new ValidationException("Formato de respaldo incompatible / Unsupported archive.");
        foreach(var t in a.Templates) { if(t is null) throw new ValidationException("Plantilla nula."); t.Validate(); if(t.Revision<1) throw new ValidationException("Revisión inválida."); }
        if(a.Templates.Select(t=>t.Id).Distinct().Count()!=a.Templates.Length || a.History.Select(h=>h.Id).Distinct().Count()!=a.History.Length || a.Versions.Select(v=>v.Id).Distinct().Count()!=a.Versions.Length || a.Executions.Select(e=>e.Id).Distinct().Count()!=a.Executions.Length || a.Proposals.Select(p=>p.Id).Distinct().Count()!=a.Proposals.Length) throw new ValidationException("Identidades duplicadas / Duplicate identities.");
        if(a.History.Any(h=>h.Id==Guid.Empty || h.Text is null || h.Text.Length>64000 || h.Values is null || h.Values.Any(v=>v.Value is null || v.Value.Length>8000)) || a.Executions.Any(e=>!a.History.Any(h=>h.Id==e.CompiledId) || e.Response is null || e.Response.Length>64000 || !OllamaClient.Models.Contains(e.Model))) throw new ValidationException("Historial inválido / Invalid history.");
        foreach(var v in a.Versions) { v.Snapshot.Validate(); if(v.TemplateId!=v.Snapshot.Id || v.Revision!=v.Snapshot.Revision || !a.Templates.Any(t=>t.Id==v.TemplateId)) throw new ValidationException("Versión huérfana / Invalid version."); }
        if(a.Preferences.Language is not ("es" or "en") || a.Preferences.Theme is not ("light" or "dark") || !OllamaClient.Models.Contains(a.Preferences.Model) || a.Preferences.GeneralInstructions is null || a.Preferences.GeneralInstructions.Length>4000) throw new ValidationException("Preferencias inválidas.");
        if(a.Proposals.Any(p=>p.Status is not ("pending" or "accepted" or "rejected" or "drafted") || p.Text is null || p.Text.Length>32000 || !OllamaClient.Models.Contains(p.Model))) throw new ValidationException("Propuesta inválida.");
        ValidateOrganization(a);
    }
    private static void ValidateKeys(JsonElement element)
    {
        if(element.ValueKind==JsonValueKind.Object){var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var p in element.EnumerateObject()){if(!seen.Add(p.Name))throw new ValidationException("Clave JSON duplicada / Duplicate JSON key.");ValidateKeys(p.Value);}}
        else if(element.ValueKind==JsonValueKind.Array)foreach(var item in element.EnumerateArray())ValidateKeys(item);
    }
    private static Template ReadLegacyJson(JsonElement element)
    {
        var item=JsonSerializer.Deserialize<Template>(element,Json) ?? throw new ValidationException("Plantilla nula.");
        if(element.ValueKind==JsonValueKind.Object)
            foreach(var p in element.EnumerateObject())if(p.Name.Equals("VariablesJson",StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind==JsonValueKind.String)
                item.Variables=JsonSerializer.Deserialize<Dictionary<string,string>>(p.Value.GetString()!,Json) ?? throw new ValidationException("VariablesJson inválido.");
        return item;
    }
    public void SeedDemo()
    {
        if(Templates().Count>0) return;
        Save(new(){Title="Un correo que conecta",Description="Convierte una idea en un correo claro y cercano.",Category=PromptCategory.Writing,Template="Redacta un correo para <destinatario> sobre <tema>. Usa un tono <tono> y termina con una acción concreta.",Variables=new(){["tono"]="profesional"}},"demo");
        Save(new(){Title="Revisión de código",Description="Una segunda mirada a decisiones y posibles errores.",Category=PromptCategory.Programming,Template="Revisa este código <código>. Explica los errores, su causa y una corrección mínima en <lenguaje>."},"demo");
        Save(new(){Title="Aprende algo nuevo",Description="Una explicación a tu medida, con ejemplos.",Category=PromptCategory.Education,Template="Explica <concepto> para una persona con nivel <nivel>. Incluye un ejemplo y tres preguntas de práctica.",IsFavorite=true},"demo");
    }
}
