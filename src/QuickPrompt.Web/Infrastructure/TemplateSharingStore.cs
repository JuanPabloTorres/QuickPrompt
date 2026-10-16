using System.Text.Json;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class LocalStore
{
    public string ExportTemplate(Guid id) => Read(db=>JsonSerializer.Serialize(TemplatePackage.From(
        GetOne<Template>(db,"template",id)??throw new ValidationException("Plantilla no disponible / Template unavailable.")),Json));
    public ImportPreview PreviewImport(string json,bool restore=false)
    {
        var archive=ParseImport(json,restore);
        return Read(db=>
        {
            var existing=Get<Template>(db,"template");
            var items=archive.Templates.Select(t=>
            {
                var same=existing.FirstOrDefault(e=>e.Id==t.Id);
                var status=same!=null ? (same.Title==t.Title && same.Template==t.Template && same.Description==t.Description && same.Category==t.Category && same.Variables.OrderBy(x=>x.Key).SequenceEqual(t.Variables.OrderBy(x=>x.Key))?"duplicate":"conflict")
                    :existing.Any(e=>e.Title==t.Title&&e.Template==t.Template)?"duplicate":"new";
                return new ImportItem(t.Title,TemplateCompiler.Names(t.Template).Length,status);
            }).ToArray();
            return new ImportPreview(archive.SchemaVersion,items.Count(i=>i.Status=="new"),items.Count(i=>i.Status=="duplicate"),items.Count(i=>i.Status=="conflict"),items,restore);
        });
    }
}
