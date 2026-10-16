using System.Text.Json;
using Microsoft.Data.Sqlite;
using QuickPrompt.Domain.Enums;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class LocalStore
{
    private static T? GetOne<T>(SqliteConnection db, string kind, Guid id, SqliteTransaction? tx = null)
    {
        using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="SELECT Payload FROM Documents WHERE Kind=$kind AND Id=$id";
        c.Parameters.AddWithValue("$kind",kind);c.Parameters.AddWithValue("$id",id.ToString());
        return c.ExecuteScalar() is string json ? JsonSerializer.Deserialize<T>(json,Json) : default;
    }
    public Template? FindTemplate(Guid id)=>Read(db=>GetOne<Template>(db,"template",id));
    public InstructionProfile? FindInstruction(Guid id)=>Read(db=>GetOne<InstructionProfile>(db,"instruction",id));
    public int ActiveTemplateCount()=>Read(db=>{using var c=db.CreateCommand();c.CommandText="SELECT COUNT(*) FROM Documents WHERE Kind='template' AND COALESCE(json_extract(Payload,'$.isActive'),1)=1";return Convert.ToInt32(c.ExecuteScalar());});
    public CatalogPage<Template> QueryTemplates(CatalogQuery query)=>Read(db=>QueryCatalog<Template>(db,"template",query));
    public CatalogPage<InstructionProfile> QueryInstructions(CatalogQuery query)=>Read(db=>QueryCatalog<InstructionProfile>(db,"instruction",query));
    private static CatalogPage<T> QueryCatalog<T>(SqliteConnection db,string kind,CatalogQuery q)
    {
        if(q.Search is null || q.Search.Length>200 || q.Page<0 || q.Size is <1 or >100 || q.Sort is not("updated" or "title" or "recent" or "usage") || (q.Category!="" && (!Enum.TryParse<PromptCategory>(q.Category,out var category) || !Enum.IsDefined(category))))throw new ValidationException("Filtro inválido / Invalid filter.");
        var title=kind=="template"?"title":"name";var body=kind=="template"?"template":"text";
        var terms=PromptSearch.Terms(q.Search);
        var categoryNames="CASE json_extract(d.Payload,'$.category') "+string.Join(" ",Enum.GetValues<PromptCategory>().Select(c=>"WHEN "+(int)c+" THEN '"+c+"'"))+" ELSE '' END";
        var searchable="qp_fold(COALESCE(json_extract(d.Payload,'$."+title+"'),'')||' '||COALESCE(json_extract(d.Payload,'$.description'),'')||' '||COALESCE(json_extract(d.Payload,'$."+body+"'),'')||' '||("+categoryNames+")||' '||COALESCE((SELECT group_concat(json_extract(g.Payload,'$.name'),' ') FROM GroupMemberships gm JOIN PromptGroups g ON g.Id=gm.GroupId WHERE gm.Kind=d.Kind AND gm.EntityId=d.Id),''))";
        var where="d.Kind=$kind AND ($category=-1 OR json_extract(d.Payload,'$.category')=$category) AND ($favorites=0 OR json_extract(d.Payload,'$.isFavorite')=1)";
        for(var i=0;i<terms.Length;i++)where+=" AND instr("+searchable+",$term"+i+")>0";
        if(q.Recent && kind=="template")where+=" AND json_extract(d.Payload,'$.lastUsed') IS NOT NULL";
        if(kind=="template")where+=" AND COALESCE(json_extract(d.Payload,'$.isActive'),1)=$active";
        where+=" AND ($group='' OR EXISTS(SELECT 1 FROM GroupMemberships m WHERE m.Kind=d.Kind AND m.EntityId=d.Id AND m.GroupId=$group)) AND ($ungrouped=0 OR NOT EXISTS(SELECT 1 FROM GroupMemberships m WHERE m.Kind=d.Kind AND m.EntityId=d.Id))";
        using var c=db.CreateCommand();c.Parameters.AddWithValue("$kind",kind);for(var i=0;i<terms.Length;i++)c.Parameters.AddWithValue("$term"+i,terms[i]);c.Parameters.AddWithValue("$category",q.Category==""?-1:(int)Enum.Parse<PromptCategory>(q.Category));c.Parameters.AddWithValue("$favorites",q.Favorites?1:0);c.Parameters.AddWithValue("$active",q.Active?1:0);c.Parameters.AddWithValue("$group",q.GroupId?.ToString()??"");c.Parameters.AddWithValue("$ungrouped",q.Ungrouped?1:0);
        c.CommandText="SELECT COUNT(*) FROM Documents d WHERE "+where;var total=Convert.ToInt32(c.ExecuteScalar());
        var order=q.Sort switch{"title"=>"qp_fold(json_extract(d.Payload,'$."+title+"')) COLLATE NOCASE", "recent"=>"json_extract(d.Payload,'$.lastUsed') DESC","usage"=>"json_extract(d.Payload,'$.uses') DESC",_=>"json_extract(d.Payload,'$.updatedAt') DESC"};
        c.CommandText="SELECT d.Payload FROM Documents d WHERE "+where+" ORDER BY "+order+",d.Id LIMIT $size OFFSET $offset";c.Parameters.AddWithValue("$size",q.Size);c.Parameters.AddWithValue("$offset",(long)q.Page*q.Size);
        using var r=c.ExecuteReader();var items=new List<T>();while(r.Read())items.Add(JsonSerializer.Deserialize<T>(r.GetString(0),Json)!);return new(items,total);
    }
    public List<InstructionChoice> InstructionChoices(string search="")=>Read(db=>{
        if(search.Length>200)throw new ValidationException("Búsqueda demasiado larga / Search too long.");
        using var c=db.CreateCommand();c.CommandText="SELECT Id,json_extract(Payload,'$.name'),json_extract(Payload,'$.revision') FROM Documents WHERE Kind='instruction' AND instr(qp_fold(json_extract(Payload,'$.name')),qp_fold($search))>0 ORDER BY json_extract(Payload,'$.isFavorite') DESC,qp_fold(json_extract(Payload,'$.name')),Id LIMIT 50";c.Parameters.AddWithValue("$search",search);
        using var r=c.ExecuteReader();var items=new List<InstructionChoice>();while(r.Read())items.Add(new(Guid.Parse(r.GetString(0)),r.GetString(1),r.GetInt32(2)));return items;
    });
    public List<GroupSummary> Groups()=>Read(db=>{
        using var c=db.CreateCommand();c.CommandText="""
            SELECT g.Payload,
                (SELECT COUNT(*) FROM GroupMemberships m JOIN Documents d ON d.Kind=m.Kind AND d.Id=m.EntityId WHERE m.GroupId=g.Id AND m.Kind='template' AND COALESCE(json_extract(d.Payload,'$.isActive'),1)=1),
                (SELECT COUNT(*) FROM GroupMemberships m WHERE m.GroupId=g.Id AND m.Kind='instruction')
            FROM PromptGroups g ORDER BY json_extract(g.Payload,'$.isPinned') DESC,json_extract(g.Payload,'$.sortOrder'),g.NameKey,g.Id
            """;
        using var r=c.ExecuteReader();var groups=new List<GroupSummary>();while(r.Read())groups.Add(new(JsonSerializer.Deserialize<PromptGroup>(r.GetString(0),Json)!,r.GetInt32(1),r.GetInt32(2)));return groups;
    });
    private static PromptGroup? GetGroup(SqliteConnection db,SqliteTransaction tx,Guid id)
    {using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="SELECT Payload FROM PromptGroups WHERE Id=$id";c.Parameters.AddWithValue("$id",id.ToString());return c.ExecuteScalar() is string s?JsonSerializer.Deserialize<PromptGroup>(s,Json):null;}
    private static void PutGroup(SqliteConnection db,SqliteTransaction tx,PromptGroup value)
    {using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT INTO PromptGroups VALUES($id,$key,$payload) ON CONFLICT(Id) DO UPDATE SET NameKey=excluded.NameKey,Payload=excluded.Payload";c.Parameters.AddWithValue("$id",value.Id.ToString());c.Parameters.AddWithValue("$key",PromptSearch.Normalize(value.Name));c.Parameters.AddWithValue("$payload",JsonSerializer.Serialize(value,Json));c.ExecuteNonQuery();}
    public PromptGroup SaveGroup(PromptGroup value)=>Write((db,tx)=>SaveGroupInside(db,tx,value));
    private static PromptGroup SaveGroupInside(SqliteConnection db,SqliteTransaction tx,PromptGroup value){
        value.Validate();var old=GetGroup(db,tx,value.Id);if(old!=null&&old.Revision!=value.Revision)throw new ConflictException("Grupo desactualizado / Stale group.");
        using var check=db.CreateCommand();check.Transaction=tx;check.CommandText="SELECT COUNT(*) FROM PromptGroups WHERE NameKey=$key AND Id<>$id";check.Parameters.AddWithValue("$key",PromptSearch.Normalize(value.Name));check.Parameters.AddWithValue("$id",value.Id.ToString());if(Convert.ToInt32(check.ExecuteScalar())>0)throw new ValidationException("Ya existe un grupo con ese nombre / A group with that name exists.");
        check.CommandText="SELECT COUNT(*) FROM PromptGroups";if(old==null&&Convert.ToInt32(check.ExecuteScalar())>=500)throw new ValidationException("Máximo 500 grupos / Maximum 500 groups.");
        value.Revision=old==null?1:old.Revision+1;value.CreatedAt=old?.CreatedAt??DateTime.UtcNow;value.UpdatedAt=DateTime.UtcNow;PutGroup(db,tx,value);return value;
    }
    public void DeleteGroup(Guid id)=>Write((db,tx)=>{using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="DELETE FROM PromptGroups WHERE Id=$id";c.Parameters.AddWithValue("$id",id.ToString());return c.ExecuteNonQuery();});
    public List<Guid> GroupsFor(string kind,Guid id)=>Read(db=>{
        ValidateKind(kind);using var c=db.CreateCommand();c.CommandText="SELECT GroupId FROM GroupMemberships WHERE Kind=$kind AND EntityId=$id ORDER BY GroupId";c.Parameters.AddWithValue("$kind",kind);c.Parameters.AddWithValue("$id",id.ToString());using var r=c.ExecuteReader();var ids=new List<Guid>();while(r.Read())ids.Add(Guid.Parse(r.GetString(0)));return ids;
    });
    private static void ValidateKind(string kind){if(kind is not("template" or "instruction"))throw new ValidationException("Tipo de relación inválido / Invalid membership kind.");}
    public void AssignGroup(Guid groupId,string kind,IEnumerable<Guid> entityIds,bool member=true)=>Write((db,tx)=>AssignGroupInside(db,tx,groupId,kind,entityIds,member));
    public PromptGroup CreateGroupAndAssign(PromptGroup group,string kind,Guid entityId)=>Write((db,tx)=>{SaveGroupInside(db,tx,group);AssignGroupInside(db,tx,group.Id,kind,[entityId],true);return group;});
    private static bool AssignGroupInside(SqliteConnection db,SqliteTransaction tx,Guid groupId,string kind,IEnumerable<Guid> entityIds,bool member){
        ValidateKind(kind);var ids=entityIds.Distinct().ToArray();if(ids.Length is <1 or >1000 || GetGroup(db,tx,groupId)==null)throw new ValidationException("Selecciona grupo y elementos / Select group and items.");
        foreach(var id in ids){using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="SELECT COUNT(*) FROM Documents WHERE Kind=$kind AND Id=$id";c.Parameters.AddWithValue("$kind",kind);c.Parameters.AddWithValue("$id",id.ToString());if(Convert.ToInt32(c.ExecuteScalar())!=1)throw new ValidationException("Elemento no disponible / Item unavailable.");}
        foreach(var id in ids){using var c=db.CreateCommand();c.Transaction=tx;c.CommandText=member?"INSERT OR IGNORE INTO GroupMemberships VALUES($group,$kind,$id)":"DELETE FROM GroupMemberships WHERE GroupId=$group AND Kind=$kind AND EntityId=$id";c.Parameters.AddWithValue("$group",groupId.ToString());c.Parameters.AddWithValue("$kind",kind);c.Parameters.AddWithValue("$id",id.ToString());c.ExecuteNonQuery();}return true;
    }
    public InstructionProfile SaveInstruction(InstructionProfile value)=>Write((db,tx)=>{
        value.Validate();var old=GetOne<InstructionProfile>(db,"instruction",value.Id,tx);if(old!=null&&old.Revision!=value.Revision)throw new ConflictException("Instrucción desactualizada / Stale instruction.");
        value.Revision=old==null?1:old.Revision+1;value.CreatedAt=old?.CreatedAt??DateTime.UtcNow;value.UpdatedAt=DateTime.UtcNow;Put(db,tx,"instruction",value.Id.ToString(),value);var v=new InstructionVersion(Guid.NewGuid(),value.Id,value.Revision,value,DateTime.UtcNow);Put(db,tx,"instruction-version",v.Id.ToString(),v);return value;
    });
    public List<InstructionVersion> InstructionVersions(Guid id)=>Read(db=>{
        using var c=db.CreateCommand();c.CommandText="SELECT Payload FROM Documents WHERE Kind='instruction-version' AND json_extract(Payload,'$.instructionId')=$id ORDER BY json_extract(Payload,'$.revision') DESC";c.Parameters.AddWithValue("$id",id.ToString());using var r=c.ExecuteReader();var list=new List<InstructionVersion>();while(r.Read())list.Add(JsonSerializer.Deserialize<InstructionVersion>(r.GetString(0),Json)!);return list;
    });
    public void DeleteInstruction(Guid id)=>Write((db,tx)=>{
        using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="DELETE FROM Documents WHERE (Kind='instruction' AND Id=$id) OR (Kind='instruction-version' AND json_extract(Payload,'$.instructionId')=$id) OR (Kind='combination' AND json_extract(Payload,'$.instructionId')=$id)";c.Parameters.AddWithValue("$id",id.ToString());return c.ExecuteNonQuery();
    });
    public PromptCombination SaveCombination(PromptCombination value)=>Write((db,tx)=>{
        if(value.Id==Guid.Empty||string.IsNullOrWhiteSpace(value.Name)||value.Name.Length>160||value.Adjustments is null||value.Adjustments.Length>4000||GetOne<Template>(db,"template",value.TemplateId,tx) is not {IsActive:true}||(value.InstructionId is {} id&&GetOne<InstructionProfile>(db,"instruction",id,tx)==null))throw new ValidationException("Combinación inválida / Invalid combination.");
        Put(db,tx,"combination",value.Id.ToString(),value);return value;
    });
    public List<PromptCombination> Combinations(Guid templateId)=>Read(db=>{
        using var c=db.CreateCommand();c.CommandText="SELECT Payload FROM Documents WHERE Kind='combination' AND json_extract(Payload,'$.templateId')=$id ORDER BY json_extract(Payload,'$.createdAt') DESC,Id LIMIT 100";c.Parameters.AddWithValue("$id",templateId.ToString());using var r=c.ExecuteReader();var list=new List<PromptCombination>();while(r.Read())list.Add(JsonSerializer.Deserialize<PromptCombination>(r.GetString(0),Json)!);return list;
    });
    public void DeleteCombination(Guid id)=>Write((db,tx)=>{using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="DELETE FROM Documents WHERE Kind='combination' AND Id=$id";c.Parameters.AddWithValue("$id",id.ToString());return c.ExecuteNonQuery();});
    private static List<PromptGroup> ExportGroups(SqliteConnection db,SqliteTransaction? tx=null)
    {using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="SELECT Payload FROM PromptGroups ORDER BY Id";using var r=c.ExecuteReader();var list=new List<PromptGroup>();while(r.Read())list.Add(JsonSerializer.Deserialize<PromptGroup>(r.GetString(0),Json)!);return list;}
    private static List<GroupMembership> ExportMemberships(SqliteConnection db,SqliteTransaction tx)
    {using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="SELECT GroupId,Kind,EntityId FROM GroupMemberships ORDER BY GroupId,Kind,EntityId";using var r=c.ExecuteReader();var list=new List<GroupMembership>();while(r.Read())list.Add(new(Guid.Parse(r.GetString(0)),r.GetString(1),Guid.Parse(r.GetString(2))));return list;}
    private static void PutMembership(SqliteConnection db,SqliteTransaction tx,GroupMembership m)
    {using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT OR IGNORE INTO GroupMemberships VALUES($group,$kind,$id)";c.Parameters.AddWithValue("$group",m.GroupId.ToString());c.Parameters.AddWithValue("$kind",m.Kind);c.Parameters.AddWithValue("$id",m.EntityId.ToString());c.ExecuteNonQuery();}
}
