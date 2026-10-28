using Microsoft.AspNetCore.Components;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Components.Pages;

public partial class Home
{
    private List<GroupSummary> groups=[];
    private List<InstructionChoice> instructionChoices=[];
    private string groupFilter="",batchGroup="",instructionId="",instructionSearch="",promptAdjustments="",combinationName="";
    private HashSet<Guid> bulkIds=[];
    private int templateCount;
    private bool webTransferBusy;
    private Guid webTransferRequest;
    private void OnWebTransferBusyChanged((Guid RequestId,bool Busy) value)
    {
        if(value.Busy){if(busy)throw new ConflictException(T("Hay una consulta local en curso.","A local query is running."));webTransferRequest=value.RequestId;webTransferBusy=true;}
        else if(webTransferRequest==value.RequestId){webTransferBusy=false;webTransferRequest=Guid.Empty;}
    }
    private string TransferSummary(WebTransferActivity entry)=>entry.Phase switch {
        TransferPhase.COPIED_OPENED=>T("Copiado y web abierta; pegar manualmente", "Copied and website opened; paste manually"),
        TransferPhase.INSERTED=>T("Texto insertado sin enviar", "Text inserted without sending"),
        TransferPhase.SUBMITTED_UI=>T("Interacción de envío UI; entrega sin verificar", "UI submission interaction; delivery unverified"),
        TransferPhase.BLOCKED=>T("Integración bloqueada", "Integration blocked"),
        TransferPhase.CANCELLED=>T("Cancelada antes de enviar", "Cancelled before submission"),
        TransferPhase.UNKNOWN=>T("Resultado incierto; revisa la otra web", "Unknown outcome; check the other website"),
        _=>T("Operación no completada", "Operation not completed")};
    private List<PromptCombination> CurrentCombinations=>selected is null?[]:Combinations.Combinations(selected.Id);
    private void LoadInstructionChoices()
    {
        instructionChoices=Instructions.InstructionChoices(instructionSearch);
        if(Guid.TryParse(instructionId,out var id))
        {
            var profile=Instructions.FindInstruction(id);
            if(profile is null)
            {
                instructionId="";if(pendingInstructionSelection==id)pendingInstructionSelection=null;
                if(!reuseSnapshot)InvalidateComposition();
            }
            else if(instructionChoices.All(p=>p.Id!=id))instructionChoices.Add(new(profile.Id,profile.Name,profile.Revision));
        }
    }
    private void ToggleBulk(Guid id){if(!bulkIds.Add(id))bulkIds.Remove(id);}
    private void AssignBatch(bool member)=>Safe(()=>{
        if(!Guid.TryParse(batchGroup,out var id)||bulkIds.Count==0)throw new ValidationException(T("Selecciona plantillas y un grupo.","Select templates and a group."));
        Memberships.AssignGroup(id,"template",bulkIds,member);bulkIds.Clear();Reload();Notice(T("Grupos actualizados sin duplicar plantillas.","Groups updated without duplicating templates."));
    });
    private Task OrganizationChanged(){Reload();return Task.CompletedTask;}
    private void InvalidateComposition(){AbandonOperation();compiled=null;response="";pendingExecution=null;confirmed=false;}
    private void ChangeInstruction(ChangeEventArgs e){instructionId=e.Value?.ToString()??"";InvalidateComposition();}
    private void ChangeAdjustments(string? value){promptAdjustments=value??"";InvalidateComposition();}
    private void UseInstruction(Guid id){showInstructionManager=false;pendingInstructionSelection=id;Navigate("library");instructionId=id.ToString();LoadInstructionChoices();InvalidateComposition();var first=selected is null?null:TemplateCompiler.Names(selected.Template).FirstOrDefault();focusAfterRender=selected is null?"#search":first is null?".detail h2":"#"+VariableId(first);Notice(T("Instrucción seleccionada. Elige una plantilla y revisa la composición.","Instruction selected. Choose a template and review the composition."));}
    private void SaveCombination()=>Safe(()=>{
        if(selected==null)throw new ValidationException(T("Selecciona una plantilla.","Select a template."));
        Combinations.SaveCombination(new(Guid.NewGuid(),combinationName.Trim(),selected.Id,Guid.TryParse(instructionId,out var id)?id:null,promptAdjustments,DateTime.UtcNow));combinationName="";Notice(T("Combinación guardada.","Combination saved."));
    });
    private void ApplyCombination(PromptCombination c){instructionId=c.InstructionId?.ToString()??"";promptAdjustments=c.Adjustments;LoadInstructionChoices();InvalidateComposition();Notice(T("Combinación seleccionada. Completa y genera la vista previa.","Combination selected. Fill variables and generate the preview."));}
    private void DeleteCombination(Guid id)=>Safe(()=>{Combinations.DeleteCombination(id);Notice(T("Combinación eliminada.","Combination deleted."));});
    private void SaveSelectedVersion()=>Safe(()=>{if(selected==null)return;var saved=Authoring.Save(Clone(selected),"manual snapshot");selected=Clone(saved);InvalidateComposition();Reload();Notice(T("Versión guardada. Genera de nuevo el texto final.","Version saved. Generate the final text again."));});
    private void ChangeGeneralInstructions(ChangeEventArgs e)=>Safe(()=>{var value=e.Value?.ToString()??"";var next=prefs with{GeneralInstructions=value};Settings.SaveSettings(next);prefs=next;InvalidateComposition();Notice(T("Instrucciones generales guardadas.","General instructions saved."));});
}
