using System.Text.Json;
using Microsoft.Data.Sqlite;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Local;
namespace QuickPrompt.Web.Infrastructure;
public static class LegacyImporter
{
    public const int MaximumBytes = 4_000_000;
    public static Task<string> Convert(Stream source) => Convert(source, CancellationToken.None);
    public static async Task<string> Convert(Stream source, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string temp=Path.Combine(Path.GetTempPath(),"quickprompt-import-"+Guid.NewGuid()+".sqlite");
        try
        {
            await using(var file=File.Create(temp))
            {
                var buffer=new byte[64*1024];var copied=0;
                while(true)
                {
                    var count=await source.ReadAsync(buffer.AsMemory(0,Math.Min(buffer.Length,MaximumBytes-copied+1)),ct);
                    if(count==0)break;
                    copied+=count;
                    if(copied>MaximumBytes)throw new ValidationException("DB demasiado grande.");
                    await file.WriteAsync(buffer.AsMemory(0,count),ct);
                }
            }
            ct.ThrowIfCancellationRequested();
            using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=temp,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());db.Open();
            using var check=db.CreateCommand();check.CommandText="PRAGMA integrity_check";if((string?)check.ExecuteScalar()!="ok")throw new ValidationException("DB corrupta.");
            using var query=db.CreateCommand();query.CommandText="SELECT name FROM sqlite_master WHERE type='table'";
            var tables=new HashSet<string>();using(var r=query.ExecuteReader())while(r.Read())tables.Add(r.GetString(0));
            string table=tables.Contains("PromptTemplate")?"PromptTemplate":tables.Contains("PromptTemplateDTO")?"PromptTemplateDTO":throw new ValidationException("Esquema legado no reconocido.");
            // SQL identifiers are selected from this fixed allowlist, never from a filename or payload.
            using var command=db.CreateCommand();command.CommandText=table=="PromptTemplate"?"SELECT * FROM PromptTemplate LIMIT 5001":"SELECT * FROM PromptTemplateDTO LIMIT 5001";
            var result=new List<Template>();var history=new List<CompiledPrompt>();
            using(var reader=command.ExecuteReader())
            {
                var columns=Enumerable.Range(0,reader.FieldCount).Select(reader.GetName).ToHashSet();
                if(!new[]{"Id","Title","Template","Description","Category","VariablesJson","IsFavorite"}.All(columns.Contains))throw new ValidationException("Columnas legadas incompatibles.");
                while(reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    string Text(string n)=>reader[n] is DBNull?"":reader[n].ToString()!;
                    if(!Guid.TryParse(Text("Id"),out var id))throw new ValidationException("ID legado inválido.");
                    if(!Enum.TryParse<PromptCategory>(Text("Category"),out var category))throw new ValidationException("Categoría legada inválida.");
                    var t=new Template{Id=id,Title=Text("Title"),Template=Text("Template"),Description=Text("Description"),Category=category,IsFavorite=Text("IsFavorite") is "1" or "True",Variables=string.IsNullOrWhiteSpace(Text("VariablesJson"))?new():JsonSerializer.Deserialize<Dictionary<string,string>>(Text("VariablesJson"))!};
                    if(columns.Contains("IsActive"))t.IsActive=Text("IsActive") is "1" or "True";
                    if(columns.Contains("IsDeleted") && Text("IsDeleted") is "1" or "True")t.IsActive=false;
                    if(columns.Contains("CreatedAt"))t.CreatedAt=Date(Text("CreatedAt"));
                    if(columns.Contains("UpdatedAt"))t.UpdatedAt=Date(Text("UpdatedAt"));
                    if(columns.Contains("DeletedAt") && !string.IsNullOrWhiteSpace(Text("DeletedAt")))t.DeletedAt=Date(Text("DeletedAt"));
                    t.Validate();result.Add(t);
                }
            }
            if(result.Count>5000)throw new ValidationException("Demasiadas plantillas.");
            if(tables.Contains("FinalPrompt"))
            {
                using var c=db.CreateCommand();c.CommandText="SELECT * FROM FinalPrompt LIMIT 5001";using var r=c.ExecuteReader();
                var columns=Enumerable.Range(0,r.FieldCount).Select(r.GetName).ToHashSet();
                if(!columns.Contains("Id") || !columns.Contains("CompletedText"))throw new ValidationException("FinalPrompt incompatible.");
                while(r.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    string Text(string n)=>r[n] is DBNull?"":r[n].ToString()!;
                    if(!Guid.TryParse(Text("Id"),out var id) || string.IsNullOrWhiteSpace(Text("CompletedText")))throw new ValidationException("FinalPrompt inválido.");
                    Guid sourceId=Guid.Empty;if(columns.Contains("SourcePromptId") && !string.IsNullOrWhiteSpace(Text("SourcePromptId")) && !Guid.TryParse(Text("SourcePromptId"),out sourceId))throw new ValidationException("Vínculo de finalizado inválido.");
                    history.Add(new(id,sourceId,result.FirstOrDefault(t=>t.Id==sourceId)?.Revision??1,result.FirstOrDefault(t=>t.Id==sourceId)?.Title??"Importado / Imported",Text("CompletedText"),new Dictionary<string,string>(),columns.Contains("CreatedAt")?Date(Text("CreatedAt")):DateTime.UtcNow,columns.Contains("IsFavorite") && Text("IsFavorite") is "1" or "True"));
                }
            }
            if(tables.Contains("ExecutionHistoryEntry"))
            {
                using var c=db.CreateCommand();c.CommandText="SELECT Id,PromptCompiled,ExecutedAt FROM ExecutionHistoryEntry WHERE IsDeleted=0 LIMIT 5001";using var r=c.ExecuteReader();
                while(r.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    if(!Guid.TryParse(r[0].ToString(),out var id))throw new ValidationException("ID de historial inválido.");
                    history.Add(new(id,Guid.Empty,1,"Historial legado / Legacy history",r[1].ToString()!,new Dictionary<string,string>(),Date(r[2].ToString()!)));
                }
            }
            if(history.Count>10000)throw new ValidationException("Historial demasiado grande.");
            ct.ThrowIfCancellationRequested();
            return JsonSerializer.Serialize(new Archive(1,result.ToArray(),[],history.ToArray(),[],[],new()),LocalStore.Json);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    private static DateTime Date(string value)
    {
        if(long.TryParse(value,out var ticks) && ticks>0 && ticks<=DateTime.MaxValue.Ticks)return new DateTime(ticks,DateTimeKind.Utc);
        if(DateTime.TryParse(value,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeUniversal|System.Globalization.DateTimeStyles.AdjustToUniversal,out var date))return date;
        throw new ValidationException("Fecha legada incompatible.");
    }
}
