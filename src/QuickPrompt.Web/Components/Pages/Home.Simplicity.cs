using QuickPrompt.Local;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using QuickPrompt.Web.Infrastructure;
using Microsoft.AspNetCore.Components.Web;

namespace QuickPrompt.Web.Components.Pages;

public partial class Home
{
    [Inject]private IWebPromptTransfer Bridge{get;set;}=null!;
    private string bridgeDiagnostic="";
    private async Task CheckBridge(){var result=await Bridge.Detect();bridgeDiagnostic=result.State==TransferState.Ready?T("Extensión detectada. La sesión y el editor se comprueban al preparar.","Extension detected. Session and editor are checked when preparing."):T("Extensión no detectada. Puedes copiar y abrir.","Extension not detected. Copy and open remains available.");}
    private bool mobileDetail,usingPrompt,showAiTools,showGroupManager,showInstructionManager,bulkMode,proposalIntoDraft;
    private string destination="",missingVariable="",newGroupName="";
    private int copyCount;
    private bool revealPreview,reuseSnapshot;
    private Guid? pendingInstructionSelection;
    private async Task<bool> ConfirmEditorNavigation()=>!editing||!HasEditorChanges()||await JS.InvokeAsync<bool>("confirm",T("¿Descartar los cambios sin guardar del editor?","Discard unsaved editor changes?"));
    private async Task NavigateFromUi(string target){if(await ConfirmEditorNavigation())Navigate(target);}
    private sealed record CompletionDraft(Dictionary<string,string> Values,string InstructionId,string Adjustments);
    // Circuit-local only. Completing a template does not silently opt in to persisted reusable values.
    private readonly Dictionary<(Guid Id,int Revision),CompletionDraft> completionDrafts=[];
    private void RememberCompletion()
    {
        if(selected is not null)completionDrafts[(selected.Id,selected.Revision)]=new(new(values),instructionId,promptAdjustments);
    }
    private async Task SelectFromLibrary(Template value)
    {
        if(editing && HasEditorChanges() && !await JS.InvokeAsync<bool>("confirm",T("¿Descartar los cambios sin guardar del editor?","Discard unsaved editor changes?")))return;
        if(pendingExecution is not null && selected?.Id!=value.Id && !await JS.InvokeAsync<bool>("confirm",T("La respuesta no está guardada. ¿Cambiar de prompt y descartarla?","The response is not saved. Switch prompts and discard it?")))return;
        Select(value);
    }
    private bool HasEditorChanges()
    {
        var original=Templates.Find(draft.Id);
        return original is null ? !string.IsNullOrWhiteSpace(draft.Title+draft.Template+draft.Description) || draft.Variables.Count>0
            : draft.Title!=original.Title || draft.Template!=original.Template || draft.Description!=original.Description || draft.Category!=original.Category || !draft.Variables.OrderBy(x=>x.Key).SequenceEqual(original.Variables.OrderBy(x=>x.Key));
    }
    private void SearchLibrary(){showGroupManager=false;showInstructionManager=false;if(view is not("library" or "favorites" or "recent" or "archive"))view="library";mobileDetail=false;ResetPage();}
    private void BackToList(){mobileDetail=false;focusAfterRender="#search";}
    private bool ValidateCompletion()
    {
        if(selected is null)return false;
        missingVariable=TemplateCompiler.Names(selected.Template).FirstOrDefault(n=>string.IsNullOrWhiteSpace(values.GetValueOrDefault(n))&&string.IsNullOrWhiteSpace(selected.Variables.GetValueOrDefault(n)))??"";
        if(missingVariable=="")return true;
        mobileDetail=true;focusAfterRender="#"+VariableId(missingVariable);
        Notice(T("Completa el campo: ","Complete field: ")+missingVariable,true);return false;
    }
    private bool EnsureCompiled(){if(busy||webTransferBusy)return false;if(compiled is not null)return true;if(!ValidateCompletion())return false;Compile();return compiled is not null;}
    private async Task CopyCurrent(){if(EnsureCompiled())await Copy(compiled!.Text);}
    private void ShowDestinations(){if(EnsureCompiled())usingPrompt=!usingPrompt;}
    private void ChooseDestination(string value){if(!EnsureCompiled())return;if(destination!=value){AbandonOperation(preserveCompleteResponse:!busy);confirmed=false;}destination=value;showAiTools=false;}
    private void CloseDestination(){if(webTransferBusy)return;destination="";usingPrompt=false;}
    private void OpenAiTools(){showAiTools=true;destination="qwen";usingPrompt=false;focusAfterRender="#ai-context";}
    private void CloseLocalResult(){Stop();showAiTools=false;destination="";usingPrompt=false;}
    private Task ImproveDraft()=>Propose("improve");
    private void SaveDraftFor(Guid id){if(draft.Id==id)SaveDraft();}
    private string PreviewText
    {
        get
        {
            if(selected is null)return "";
            try{return Compiled.Preview(selected.Id,values,Guid.TryParse(instructionId,out var id)?id:null,promptAdjustments).Text;}
            catch(ValidationException){return T("Completa las variables para ver el texto final.","Fill the variables to preview the final text.");}
        }
    }
    private void CreateFilterGroup()=>Safe(()=>{var group=new PromptGroup{Name=newGroupName.Trim()};GroupAuthoring.SaveGroup(group);newGroupName="";Reload();Notice(T("Grupo creado.","Group created."));});
}
