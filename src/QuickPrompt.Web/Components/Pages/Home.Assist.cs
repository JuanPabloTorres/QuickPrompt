using QuickPrompt.Local;
using Microsoft.JSInterop;
using QuickPrompt.Web.Infrastructure;

namespace QuickPrompt.Web.Components.Pages;
public partial class Home
{
    private string assistCommand="",assistError="";
    private PreparedAssistPlan? assistPlan;
    private bool assistBusy;
    private CancellationTokenSource? assistOperation;
    private IEnumerable<Template> AssistMatches=>assistCommand.Length is >0 and <=200 ? Store.QueryTemplates(new(Search:assistCommand,Size:6)).Items : [];
    private async Task LaunchAssist(Template template){if(!await ConfirmEditorNavigation())return;Navigate("library");Select(template);await CloseAssist();}
    private async Task AssistKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e){if(e.Key=="Enter"&&!e.ShiftKey&&!assistBusy)await PrepareAssist();}
    private void CancelAssist()=>assistOperation?.Cancel();
    private void AssistClosed(){CancelAssist();assistPlan=null;assistError="";}
    private async Task OpenAssist(){assistPlan=null;assistError="";await JS.InvokeVoidAsync("qp.open","assist-dialog",false);}
    private async Task CloseAssist(){AssistClosed();await JS.InvokeVoidAsync("qp.close","assist-dialog");}
    private async Task AssistWork(Func<CancellationToken,Task> action)
    {
        if(assistBusy)return;
        using var cts=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);assistOperation=cts;assistBusy=true;assistError="";
        var context=contextVersion;
        try{await action(cts.Token);}
        catch(OperationCanceledException){if(context==contextVersion)assistError=T("Solicitud cancelada. Puedes reintentar.","Request cancelled. You can retry.");}
        catch(Exception e)when(e is LlmUnavailableException or ValidationException or ConflictException or HttpRequestException or IOException or Microsoft.Data.Sqlite.SqliteException){if(context==contextVersion)assistError=e is Microsoft.Data.Sqlite.SqliteException?T("No se pudo guardar la propuesta. Puedes reintentar.","Could not save the proposal. You can retry."):e.Message;}
        finally{if(assistOperation==cts){assistOperation=null;assistBusy=false;}}
    }
    private async Task PrepareAssist()
    {
        assistPlan=null;assistError="";
        try{
            var plan=QuickAssistCommands.Parse(assistCommand);
            if(plan is null){if(AssistMatches.FirstOrDefault() is {} first){await LaunchAssist(first);return;}plan=new AssistPlan([new(AssistAction.Search,assistCommand.Trim())],"deterministic");plan.Validate();}
            if(plan is not null){assistPlan=Store.PrepareAssist(plan,editing?null:selected?.Id);if(!plan.Steps.Any(s=>s.Action is AssistAction.Favorite or AssistAction.ProposeGroup))await ApplyAssist();return;}
        }catch(Exception e)when(e is ValidationException or ConflictException){assistError=e.Message;}
    }
    private Task InterpretAssist()=>AssistWork(async ct=>{assistPlan=null;var interpreted=await Llm.PlanAssist(assistCommand,prefs.Model,ct);ct.ThrowIfCancellationRequested();assistPlan=Store.PrepareAssist(interpreted,editing?null:selected?.Id);});
    private Task AssistProposal(string action,string context)=>AssistWork(async ct=>
    {
        var source=action=="create"?null:selected is null?throw new ValidationException(T("Selecciona un prompt.","Select a prompt.")):Clone(selected);
        var result=await Llm.Propose(action,context,source,prefs.Model,ct);ct.ThrowIfCancellationRequested();
        Store.SaveProposal(result);proposal=result;proposalError="";proposalSource=source;proposalOriginal=source?.Template??context;proposalAction=action;proposalIntoDraft=false;
        await InvokeAsync(StateHasChanged);await JS.InvokeVoidAsync("qp.open","proposal-dialog");
    });
    private string AssistLabel(AssistAction action)=>action switch
    {
        AssistAction.Search=>T("Buscar","Search"),AssistAction.Favorites=>T("Mostrar favoritos","Show favorites"),AssistAction.Recent=>T("Mostrar recientes","Show recent"),AssistAction.OpenGroup=>T("Abrir grupo","Open group"),AssistAction.OpenTemplate=>T("Abrir prompt","Open prompt"),AssistAction.Favorite=>T("Guardar en favoritos","Save as favorite"),AssistAction.PrepareValues=>T("Proponer valores (revisión posterior)","Suggest values (review next)"),AssistAction.Create=>T("Proponer borrador","Propose draft"),AssistAction.Improve=>T("Proponer mejora","Propose improvement"),AssistAction.SuggestVariables=>T("Proponer plantilla con variables","Propose template with variables"),_=>T("Crear grupo","Create group")
    };
    private async Task ApplyAssist()
    {
        if(assistPlan is not {} prepared || busy || !await ConfirmEditorNavigation())return;
        bool applied=false;Safe(()=>{Store.ApplyAssist(prepared);applied=true;});
        if(!applied){assistError=message;message="";return;}
        editing=false; // Discard was explicitly confirmed above; avoid asking again while opening a new draft.
        var generating=prepared.Steps.Any(s=>s.Step.Action is AssistAction.Create or AssistAction.Improve or AssistAction.SuggestVariables);
        if(!generating)await CloseAssist();
        foreach(var bound in prepared.Steps)
        {
            var step=bound.Step;
            if(step.Action==AssistAction.Search){Navigate("library");search=step.Argument;ResetPage();}
            if(step.Action==AssistAction.Favorites)Navigate("favorites");
            if(step.Action==AssistAction.Recent)Navigate("recent");
            if(step.Action==AssistAction.OpenGroup){Navigate("library");groupFilter=bound.GroupId!.Value.ToString();ResetPage();}
            if(bound.TemplateId is {} id && step.Action is AssistAction.OpenTemplate or AssistAction.Favorite or AssistAction.PrepareValues or AssistAction.Improve or AssistAction.SuggestVariables){var target=Store.FindTemplate(id);if(target is not null){if(step.Action==AssistAction.Favorite && selected?.Id==id)selected=Clone(target);else Select(target);}}
            if(step.Action==AssistAction.Create)await AssistProposal("create",step.Argument);
            if(step.Action==AssistAction.Improve)await AssistProposal("improve",step.Argument);
            if(step.Action==AssistAction.PrepareValues){OpenAiTools();aiContext=step.Argument;await Suggest();}
            if(step.Action==AssistAction.SuggestVariables)await AssistProposal("create",T("Propón una plantilla reutilizable con variables a partir de este texto. Conserva sus restricciones: ","Propose a reusable template with variables from this text. Preserve its constraints: ")+selected!.Template);
        }
        Reload();
    }
}
