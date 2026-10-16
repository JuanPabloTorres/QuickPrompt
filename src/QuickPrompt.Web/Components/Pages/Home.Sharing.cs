using Microsoft.JSInterop;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Components.Pages;
public partial class Home
{
    private ImportPreview? importPreview;
    private string? pendingImport;
    private string importError="";
    private async Task ShareTemplateJson()
    {
        if(selected is null)return;
        try{await JS.InvokeVoidAsync("qp.download","quickprompt-template-v1.json",Store.ExportTemplate(selected.Id));Notice(T("Plantilla descargada sin valores ni historial.","Template downloaded without values or history."));}
        catch(Exception e)when(e is ValidationException or JSException){Notice(e.Message,true);}
    }
    private async Task ShareTemplateText()
    {
        if(selected is null)return;
        var result=await JS.InvokeAsync<string>("qp.share",selected.Title,selected.Template);
        Notice(result switch{"shared"=>T("Compartido.","Shared."),"cancelled"=>T("Compartir cancelado.","Sharing cancelled."),_=>T("Compartir no está disponible. Puedes copiar el texto o descargar JSON.","Sharing is unavailable. Copy text or download JSON.")});
    }
    private async Task CancelImport(){pendingImport=null;importPreview=null;importError="";await JS.InvokeVoidAsync("qp.close","import-dialog");}
    private async Task ApplyImport()
    {
        if(pendingImport is null || importPreview is null)return;
        var restore=importPreview.Restore;
        if(restore && !await JS.InvokeAsync<bool>("confirm",T("¿Reemplazar todos los datos por este respaldo? Exporta antes el estado actual.","Replace all data with this backup? Export your current state first.")))return;
        bool imported=false;
        Safe(()=>{
            var count=Store.Import(pendingImport,restore);Reload();selected=null;
            if(restore){completionDrafts.Clear();Stop();prefs=Store.Settings();compiled=null;pendingExecution=null;proposal=null;response="";values.Clear();suggestions.Clear();confirmed=false;editing=false;}
            Notice($"{count} "+T("plantillas procesadas sin modificar el origen.","templates processed without changing the source."));imported=true;
        });
        if(!imported){importError=message;message="";return;}
        await CancelImport();if(restore)await JS.InvokeVoidAsync("qp.language",prefs.Language);
    }
}
