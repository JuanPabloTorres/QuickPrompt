using Microsoft.Data.Sqlite;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class LocalStore
{
    private static void ValidateOrganization(Archive a)
    {
        if(a.SchemaVersion==2 && (a.Groups is null || a.Memberships is null || a.Instructions is null || a.InstructionVersions is null || a.Combinations is null))throw new ValidationException("Respaldo v2 incompleto / Incomplete v2 backup.");
        var groups=a.Groups??[];var profiles=a.Instructions??[];var versions=a.InstructionVersions??[];var links=a.Memberships??[];var combos=a.Combinations??[];
        if(groups.Length>500 || profiles.Length>5000 || versions.Length>25000 || links.Length>100000 || combos.Length>5000 || groups.Any(g=>g is null) || profiles.Any(p=>p is null) || versions.Any(v=>v is null || v.Snapshot is null) || links.Any(m=>m is null) || combos.Any(c=>c is null))throw new ValidationException("Organización de respaldo inválida / Invalid backup organization.");
        foreach(var g in groups){g.Validate();if(g.Revision<1)throw new ValidationException("Revisión de grupo inválida / Invalid group revision.");}
        foreach(var p in profiles){p.Validate();if(p.Revision<1)throw new ValidationException("Revisión de instrucción inválida / Invalid instruction revision.");}
        if(groups.Select(g=>g.Id).Distinct().Count()!=groups.Length || groups.Select(g=>PromptSearch.Normalize(g.Name)).Distinct().Count()!=groups.Length || profiles.Select(p=>p.Id).Distinct().Count()!=profiles.Length || versions.Select(v=>v.Id).Distinct().Count()!=versions.Length || versions.Select(v=>(v.InstructionId,v.Revision)).Distinct().Count()!=versions.Length || links.Distinct().Count()!=links.Length || combos.Select(c=>c.Id).Distinct().Count()!=combos.Length)throw new ValidationException("Identidades de organización duplicadas / Duplicate organization identities.");
        foreach(var v in versions){v.Snapshot.Validate();if(v.Id==Guid.Empty || v.InstructionId!=v.Snapshot.Id || v.Revision!=v.Snapshot.Revision || v.Revision<1 || !profiles.Any(p=>p.Id==v.InstructionId && p.Revision>=v.Revision))throw new ValidationException("Versión de instrucción huérfana / Orphan instruction version.");}
        foreach(var m in links){ValidateKind(m.Kind);if(!groups.Any(g=>g.Id==m.GroupId) || (m.Kind=="template"?!a.Templates.Any(t=>t.Id==m.EntityId):!profiles.Any(p=>p.Id==m.EntityId)))throw new ValidationException("Relación de grupo huérfana / Orphan group membership.");}
        foreach(var c in combos)if(c.Id==Guid.Empty || string.IsNullOrWhiteSpace(c.Name) || c.Name.Length>160 || c.Adjustments is null || c.Adjustments.Length>4000 || !a.Templates.Any(t=>t.Id==c.TemplateId) || (c.InstructionId is {} id&&!profiles.Any(p=>p.Id==id)))throw new ValidationException("Combinación huérfana / Orphan combination.");
        foreach(var h in a.History)if(h.Composition is {} composition){composition.Validate();if(composition.ForExternalEditor()!=h.Text)throw new ValidationException("La vista previa no coincide con la composición / Preview does not match composition.");}
        if(a.WebTransfers is {} transfers){if(transfers.Length>200 || transfers.Select(t=>t?.Id).Distinct().Count()!=transfers.Length)throw new ValidationException("Historial web inválido / Invalid web history.");foreach(var t in transfers)ValidateTransferActivity(t);}
    }
    private static void RestoreOrganization(SqliteConnection db,SqliteTransaction tx,Archive a)
    {
        foreach(var p in a.Instructions??[])Put(db,tx,"instruction",p.Id.ToString(),p);
        foreach(var v in a.InstructionVersions??[])Put(db,tx,"instruction-version",v.Id.ToString(),v);
        foreach(var g in a.Groups??[])PutGroup(db,tx,g);
        foreach(var m in a.Memberships??[])PutMembership(db,tx,m);
        foreach(var c in a.Combinations??[])Put(db,tx,"combination",c.Id.ToString(),c);
        foreach(var t in a.WebTransfers??[])Put(db,tx,"web-transfer",t.Id.ToString(),t);
    }
    private static void MergeOrganization(SqliteConnection db,SqliteTransaction tx,Archive a,List<Template> templates)
    {
        var templateMap=a.Templates.ToDictionary(t=>t.Id,t=>templates.First(x=>x.Id==t.Id || (x.Title==t.Title&&x.Template==t.Template)).Id);
        var existing=Get<InstructionProfile>(db,"instruction",tx);var profileMap=new Dictionary<Guid,Guid>();
        foreach(var p in a.Instructions??[])
        {
            var match=existing.FirstOrDefault(x=>x.Id==p.Id || (x.Name==p.Name&&x.Text==p.Text));profileMap[p.Id]=match?.Id??p.Id;if(match!=null)continue;
            Put(db,tx,"instruction",p.Id.ToString(),p);existing.Add(p);var versions=(a.InstructionVersions??[]).Where(v=>v.InstructionId==p.Id).ToArray();
            if(versions.Length==0){var v=new InstructionVersion(Guid.NewGuid(),p.Id,p.Revision,p,DateTime.UtcNow);Put(db,tx,"instruction-version",v.Id.ToString(),v);}else foreach(var v in versions){if(GetOne<InstructionVersion>(db,"instruction-version",v.Id,tx)!=null)throw new ValidationException("Colisión de versiones importadas / Imported version collision.");Put(db,tx,"instruction-version",v.Id.ToString(),v);}
        }
        var groups=ExportGroups(db,tx);var groupMap=new Dictionary<Guid,Guid>();
        foreach(var g in a.Groups??[]){var match=groups.FirstOrDefault(x=>x.Id==g.Id || PromptSearch.Normalize(x.Name)==PromptSearch.Normalize(g.Name));groupMap[g.Id]=match?.Id??g.Id;if(match==null){PutGroup(db,tx,g);groups.Add(g);}}
        if(groups.Count>500)throw new ValidationException("Máximo 500 grupos / Maximum 500 groups.");
        foreach(var m in a.Memberships??[])PutMembership(db,tx,m with{GroupId=groupMap[m.GroupId],EntityId=m.Kind=="template"?templateMap[m.EntityId]:profileMap[m.EntityId]});
        foreach(var c in a.Combinations??[])if(GetOne<PromptCombination>(db,"combination",c.Id,tx)==null)Put(db,tx,"combination",c.Id.ToString(),c with{TemplateId=templateMap[c.TemplateId],InstructionId=c.InstructionId is {} id?profileMap[id]:null});
        foreach(var t in a.WebTransfers??[])if(GetOne<WebTransferActivity>(db,"web-transfer",t.Id,tx)==null)Put(db,tx,"web-transfer",t.Id.ToString(),t);
        PruneTransfers(db,tx);
    }
}
