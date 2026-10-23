using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Components.Pages;
public partial class Home
{
    [Inject] private IQuickAssistWorkflow Assist {get;set;}=null!;
    [Inject] private IStorageFailureClassifier StorageFailures {get;set;}=null!;
    [Inject] private ITransferActivityReader TransferHistory {get;set;}=null!;
    [Inject] private ILibraryDiagnostics Diagnostics {get;set;}=null!;
    [Inject] private IPromptPanel Panel {get;set;}=null!;
    [Inject] private IPromptReader Templates {get;set;}=null!;
    [Inject] private IPromptWriter TemplateWriter {get;set;}=null!;
    [Inject] private ITemplateAuthoring Authoring {get;set;}=null!;
    [Inject] private ICompilationReader Compiled {get;set;}=null!;
    [Inject] private ICompilationWriter Compilation {get;set;}=null!;
    [Inject] private ILocalSettings Settings {get;set;}=null!;
    [Inject] private IProposalLifecycle Proposals {get;set;}=null!;
    [Inject] private ICollectionReader Collections {get;set;}=null!;
    [Inject] private IGroupAuthoring GroupAuthoring {get;set;}=null!;
    [Inject] private IGroupMemberships Memberships {get;set;}=null!;
    [Inject] private IInstructionCatalog Instructions {get;set;}=null!;
    [Inject] private IPromptCombinations Combinations {get;set;}=null!;
    [Inject] private ILibraryArchiveReader Archives {get;set;}=null!;
    [Inject] private ILibraryArchiveWriter ArchiveWriter {get;set;}=null!;
    [Inject] private IPromptSharing Sharing {get;set;}=null!;
    [Inject] private ILegacyArchiveConverter LegacyConverter {get;set;}=null!;
    [Inject] private ILocalLlmClient Llm {get;set;}=null!;
    [Inject] private IJSRuntime JS {get;set;}=null!;
    private Preferences prefs=new();
    // Only the requested page is materialized; the catalog and launcher are filtered in SQLite.
    private CatalogPage<Template> catalog=new([],0);
    private Template? selected;
    private Template draft=new();
    private Dictionary<string,string> values=new(), suggestions=new();
    private CompiledPrompt? compiled;
    private LlmExecution? pendingExecution;
    private AiProposal? proposal;
    private Template? proposalSource;
    private string proposalOriginal="", proposalAction="",response="",aiContext="",message="",search="",category="",sort="updated",view="library",ollamaStatus="",analysis="";
    private bool error,editing,visual,busy,confirmed;
    private int page;
    private CancellationTokenSource? operation;
    private readonly CancellationTokenSource lifetime=new();
    private int contextVersion;
    private bool checkingOllama,availabilityKnown;
    private string[] availableModels=[];
    private enum LocalOperationState { Idle, Running, Complete, Cancelled, Error }
    private LocalOperationState localOperation;
    private string localError="",proposalError="";
    private string LocalStatus=>webTransferBusy?T("Transferencia web en curso. Espera o cancélala antes de consultar al modelo local.","Web transfer in progress. Wait or cancel it before querying the local model."):localOperation switch {LocalOperationState.Running=>T("Ejecutando en tu equipo…","Running on your device…"),LocalOperationState.Complete=>T("Operación completada. Revisa el resultado.","Operation complete. Review the result."),LocalOperationState.Cancelled=>T("Cancelado; ningún resultado parcial guardado.","Cancelled; no partial result saved."),LocalOperationState.Error=>T("Error recuperable: ","Recoverable error: ")+localError,_=>checkingOllama?T("Comprobando disponibilidad…","Checking availability…"):!availabilityKnown?T("Disponibilidad sin comprobar.","Availability not checked."):availableModels.Contains(prefs.Model)?T("Modelo disponible en Ollama local.","Model available in local Ollama."):T("Conexión o modelo no disponible; puedes comprobar y reintentar. La copia sigue disponible.","Connection or model unavailable; check and retry. Copying remains available.")};
    private string? focusAfterRender;
    private string? variableAfterRender;
    private DotNetObjectReference<PanelSession>? panelReference;
    private readonly (string Key,string Es,string En,string Icon)[] navigation=[("library","Biblioteca","Library","library"),("favorites","Favoritos","Favorites","star"),("recent","Recientes","Recent","history"),("groups","Grupos","Groups","library"),("instructions","Instrucciones","Instructions","braces"),("history","Historial","History","history"),("archive","Archivadas","Archived","archive"),("settings","Configuración","Settings","settings"),("help","Guía e inspiración","Guide & inspiration","circle-help")];
    private string T(string es,string en)=>prefs.Language=="es"?es:en;
    private string ViewTitle {get {var n=navigation.First(n=>n.Key==view);return T(n.Es,n.En);}}
    private IEnumerable<Template> Filtered
    {
        get => catalog.Items;
    }
    private static bool Matches(Template t,string q)=>PromptSearch.Matches(t,q);
    private IReadOnlyList<Token> DraftTokens {get{try{return TemplateCompiler.Parse(draft.Template);}catch(ValidationException){return [new(draft.Template)];}}}
    private string[] DraftNames {get{try{return TemplateCompiler.Names(draft.Template);}catch(ValidationException){return [];}}}
    protected override void OnInitialized(){prefs=Settings.Settings();Reload();}
    protected override async Task OnAfterRenderAsync(bool firstRender){if(firstRender){await JS.InvokeVoidAsync("qp.init",prefs.Language);panelReference=DotNetObjectReference.Create(new PanelSession(Panel));await JS.InvokeVoidAsync("qp.panelInit",panelReference);await CheckOllama();}if(focusAfterRender is {} selector){focusAfterRender=null;await JS.InvokeVoidAsync("qp.focus",selector);}if(variableAfterRender is {} name){variableAfterRender=null;await JS.InvokeVoidAsync("qp.selectVariable",name);}}
    private void Reload(){groups=Collections.Groups().ToList();if(Guid.TryParse(groupFilter,out var removedGroup)&&groups.All(g=>g.Group.Id!=removedGroup))groupFilter="";templateCount=Templates.ActiveCount;catalog=Templates.Search(new(Search:search,Category:category,Favorites:view=="favorites",Recent:view=="recent",Active:view!="archive",GroupId:Guid.TryParse(groupFilter,out var id)?id:null,Ungrouped:groupFilter=="ungrouped",Sort:view=="recent"?"recent":sort,Page:page));LoadInstructionChoices();}
    private void ResetPage(){page=0;bulkIds.Clear();Reload();}
    private void ChangePage(int delta){page+=delta;Reload();}
    private void Notice(string text,bool isError=false){message=text;error=isError;}
    private static Template Clone(Template t)=>WorkspaceSnapshots.Copy(t);
    private void Safe(Action action){try{action();}catch(Exception e) when((e is ValidationException or ConflictException or MissingPromptException or JsonException or IOException) || StorageFailures.IsFailure(e)){Notice(StorageFailures.IsFailure(e) ? T("Error de almacenamiento. No se aplicó la operación.","Storage error. Operation was not applied.") : e.Message,true);}}
    private void Navigate(string target){if(target is not("groups" or "instructions")){showGroupManager=false;showInstructionManager=false;}if(target=="groups"){showGroupManager=true;showInstructionManager=false;target="library";}if(target=="instructions"){showInstructionManager=true;showGroupManager=false;target="library";}AbandonOperation(preserveCompleteResponse:!busy);if(view!=target){search="";category="";groupFilter="";bulkIds.Clear();}view=target;editing=false;page=0;message="";Reload();}
    private void Select(Template t)=>SelectTemplate(t,false);
    private void SelectTemplate(Template t,bool fresh)
    {
        mobileDetail=true;
        var first=TemplateCompiler.Names(t.Template).FirstOrDefault();
        focusAfterRender=t.IsActive && first!=null?"#"+VariableId(first):".detail h2";
        // Reopening the current card is navigation, not permission to erase a user's in-progress draft.
        if(!fresh && !editing && selected?.Id==t.Id && selected.Revision==t.Revision && selected.IsActive==t.IsActive && selected.Template==t.Template)return;
        RememberCompletion();AbandonOperation();selected=Clone(t);editing=false;values=new(t.Variables);instructionId="";promptAdjustments="";if(!fresh&&completionDrafts.TryGetValue((t.Id,t.Revision),out var prior)){values=new(prior.Values);instructionId=prior.InstructionId;promptAdjustments=prior.Adjustments;}if(pendingInstructionSelection is {} explicitProfile){instructionId=explicitProfile.ToString();pendingInstructionSelection=null;}missingVariable="";revealPreview=false;reuseSnapshot=false;message="";usingPrompt=false;destination="";showAiTools=false;suggestions.Clear();compiled=null;response="";analysis="";pendingExecution=null;confirmed=false;LoadInstructionChoices();
    }
    private async Task NewTemplate(){if(!await ConfirmEditorNavigation())return;if(pendingExecution is not null&&!await JS.InvokeAsync<bool>("confirm",T("La respuesta no está guardada. ¿Crear otro prompt y descartarla?","The response is not saved. Create another prompt and discard it?")))return;RememberCompletion();Navigate("library");draft=new();editing=true;mobileDetail=true;focusAfterRender="#title";}
    private void EditSelected(){if(selected!=null){AbandonOperation();draft=Clone(selected);editing=true;focusAfterRender="#title";}}
    private void Duplicate(){if(selected!=null){draft=Clone(selected);draft.Id=Guid.NewGuid();draft.Title+=" · "+T("copia","copy");draft.Revision=1;draft.Uses=0;draft.LastUsed=null;draft.IsActive=true;editing=true;}}
    private void CancelEdit()=>editing=false;
    private void SaveDraft()=>Safe(()=> {draft.Variables=draft.Variables.Where(v=>DraftNames.Contains(v.Key)).ToDictionary();var saved=Authoring.Save(Clone(draft));Reload();Select(saved);Notice(T("Plantilla guardada.","Template saved."));});
    private void Favorite(Template t)=>Safe(()=>{var copy=Clone(t);copy.ToggleFavorite();Authoring.Save(copy,"favorite");Reload();if(selected?.Id==t.Id)selected=Clone(copy);});
    private void ArchiveSelected()=>Safe(()=>{if(selected==null)return;var copy=Clone(selected);if(copy.IsActive)copy.MarkAsDeleted();else{copy.IsActive=true;copy.DeletedAt=null;}Authoring.Save(copy,"archive/restore");Reload();Select(copy);});
    private async Task DeleteSelected(){if(selected!=null && await JS.InvokeAsync<bool>("confirm",T("¿Eliminar esta plantilla y sus versiones? El historial final permanece.","Delete this template and its versions? Final history remains.")))Safe(()=>{TemplateWriter.Delete(selected.Id,selected.Revision);selected=null;Reload();Notice(T("Plantilla eliminada.","Template deleted."));});}
    private static string VariableId(string name)=>"v-"+Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(name));
    private void SetValue(string name,string value){if(missingVariable==name)missingVariable="";reuseSnapshot=false;message="";AbandonOperation();values[name]=value;compiled=null;pendingExecution=null;response="";confirmed=false;}
    private void SetValueFor(Guid templateId,int revision,string name,string value){if(selected?.Id==templateId && selected.Revision==revision)SetValue(name,value);}
    private void CompileFor(Guid templateId,int revision){if(selected?.Id==templateId && selected.Revision==revision)Compile();}
    private IEnumerable<string> PreviousValues(string name)=>Compiled.History().Where(h=>h.TemplateId==selected?.Id && h.Values.ContainsKey(name)).Select(h=>h.Values[name]).Distinct().Take(3);
    private void Compile()=>Safe(()=>{if(selected==null || busy || webTransferBusy || !ValidateCompletion())return;compiled=Compilation.Compile(selected.Id,values,Guid.TryParse(instructionId,out var profileId)?profileId:null,promptAdjustments,expectedRevision:selected.Revision);confirmed=false;pendingExecution=null;response="";Reload();Notice(T("Texto final guardado en historial. Ya puedes copiarlo.","Final text saved to history. Ready to copy."));});
    private async Task Copy(string text){message="";try{await JS.InvokeVoidAsync("qp.copy",text);copyCount++;Notice(T("Copiado al portapapeles.","Copied to clipboard."));}catch(JSException){revealPreview=true;focusAfterRender="[data-testid=compiled]";Notice(T("El navegador bloqueó el portapapeles. Selecciona el texto y usa Ctrl+C.","Browser blocked clipboard. Select the text and use Ctrl+C."),true);}}
    private void FocusEditor(string name){visual=false;variableAfterRender=name;}
    private async Task Work(Func<CancellationToken,Task> action)
    {
        if(busy||webTransferBusy)return;CancelAssist();var cts=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);var context=contextVersion;operation=cts;busy=true;localOperation=LocalOperationState.Running;localError="";message="";
        try{await action(cts.Token);if(context==contextVersion)localOperation=LocalOperationState.Complete;}
        catch(OperationCanceledException){if(context==contextVersion){Notice(T("Operación detenida; no se guardó respuesta parcial.","Stopped; partial response was not saved."));localOperation=LocalOperationState.Cancelled;pendingExecution=null;response="";}}
        catch(Exception e) when((e is LlmUnavailableException or ValidationException or ConflictException or HttpRequestException or IOException) || StorageFailures.IsFailure(e)){if(context==contextVersion){localError=(StorageFailures.IsFailure(e) || e is IOException)?T("No se completó la operación local. Reintenta; no se guardó respuesta parcial.","Local operation did not complete. Retry; no partial response saved."):e.Message;Notice(localError,true);localOperation=LocalOperationState.Error;pendingExecution=null;response="";}}
        finally{if(operation==cts){busy=false;operation=null;}cts.Dispose();}
    }
    private void Stop()=>operation?.Cancel();
    private void AbandonOperation(bool preserveCompleteResponse=false){contextVersion++;CancelAssist();Stop();localOperation=LocalOperationState.Idle;localError="";if(!preserveCompleteResponse){response="";pendingExecution=null;}}
    private Task Propose(string action)=>Work(async ct=>
    {
        if(action=="create" && string.IsNullOrWhiteSpace(aiContext))throw new ValidationException(T("Describe tu intención.","Describe your intent."));
        var source=action=="create"?null:editing?Clone(draft):selected is null?throw new ValidationException("Selecciona una plantilla / Select a template."):Clone(selected);proposalIntoDraft=editing&&action!="create";
        var result=await Llm.Propose(action,aiContext,source,prefs.Model,ct);ct.ThrowIfCancellationRequested();
        proposal=result;proposalError="";proposalSource=source;proposalOriginal=source?.Template??aiContext;proposalAction=action;
        Proposals.SaveProposal(result);await InvokeAsync(StateHasChanged);ct.ThrowIfCancellationRequested();await JS.InvokeVoidAsync("qp.open","proposal-dialog");
    });
    private async Task AcceptProposal()
    {
        bool close=false;proposalError="";
        Safe(()=>
        {
            if(proposal==null)return;
            if(proposalAction=="analyze"){Proposals.Reject(proposal);close=true;return;}
            if(proposalIntoDraft){if(!editing||draft.Id!=proposalSource?.Id||draft.Template!=proposalOriginal)throw new ConflictException(T("El borrador cambió. Revisa la propuesta de nuevo.","Draft changed. Review a new proposal."));draft=Proposals.StageDraftImprovement(proposal.Id,draft);close=true;}
            else if(proposalSource is null){Proposals.DraftProposal(proposal);Navigate("library");draft=new(){Title=T("Nueva idea","New idea"),Template=proposal.Text};editing=true;Notice(T("Borrador abierto. Edítalo y pulsa Guardar para crear la plantilla.","Draft opened. Edit and Save to create the template."));}
            else{var updated=Clone(proposalSource);updated.Template=proposal.Text;Authoring.Save(updated,"AI accepted",proposal.Id);Reload();Select(updated);Notice(T("Propuesta aceptada. Nueva versión guardada.","Proposal accepted. New version saved."));}
            close=true;
        });
        if(close){await JS.InvokeVoidAsync("qp.close","proposal-dialog");proposal=null;}else if(error){proposalError=message;message="";}
    }
    private async Task RejectProposal(){bool rejected=false;Safe(()=>{if(proposal!=null)Proposals.Reject(proposal);rejected=true;});if(!rejected){proposalError=message;message="";return;}proposal=null;proposalError="";await JS.InvokeVoidAsync("qp.close","proposal-dialog");Notice(T("Propuesta rechazada. Original conservado.","Proposal rejected. Original preserved."));}
    private Task Suggest()=>Work(async ct=>{if(selected is null)return;var result=await Llm.Suggest(Clone(selected),aiContext,prefs.Model,ct);ct.ThrowIfCancellationRequested();suggestions=result;Notice(T("Sugerencias listas. Pulsa cada ejemplo para aplicarlo.","Suggestions ready. Click an example to apply it."));});
    private async Task Analyze(){if(selected is null)return;var a=PromptAnalyzer.Analyze(selected,values);analysis=T("Variables únicas: ","Unique variables: ")+a.Variables.Length+T(". Campos faltantes: ",". Missing fields: ")+string.Join(", ",a.MissingValues)+T(". Señales léxicas (heurística determinista) — objetivo: ",". Lexical signals (deterministic heuristic) — objective: ")+a.HasObjectiveSignal+T(", restricciones: ",", constraints: ")+a.HasConstraintSignal+T(", formato: ",", format: ")+a.HasFormatSignal+T(". La ausencia de señal sugiere revisión, no demuestra un defecto.",". Missing signals suggest review; they do not prove a defect.");await Propose("analyze");}
    private Task Execute()=>Work(async ct=>{if(compiled is null)return;var source=compiled;var model=prefs.Model;var context=contextVersion;response="";pendingExecution=null;var result=await Llm.Execute(source,model,confirmed,async text=>{if(ct.IsCancellationRequested||context!=contextVersion)return;response=text;await InvokeAsync(StateHasChanged);},ct);ct.ThrowIfCancellationRequested();response=result.Text;pendingExecution=new(Guid.NewGuid(),source.Id,model,result.Text,result.ElapsedMs,DateTime.UtcNow);Notice(T("Respuesta completa. Puedes guardarla.","Response complete. You can save it."));});
    private void SaveResponse()=>Safe(()=>{if(pendingExecution!=null){Compilation.SaveExecution(pendingExecution);pendingExecution=null;Notice(T("Resultado guardado en historial.","Result saved to history."));}});
    private void FinalFavorite(CompiledPrompt h)=>Safe(()=>Compilation.FavoriteFinal(h));
    private async Task Reuse(CompiledPrompt h){var source=Templates.Find(h.TemplateId);if(source==null){await Copy(h.Text);return;}Navigate("library");SelectTemplate(source,true);compiled=h;reuseSnapshot=true;instructionId=h.Composition?.InstructionId?.ToString()??"";promptAdjustments=h.Composition?.Adjustments??"";LoadInstructionChoices();}
    private async Task ClearHistory(){if(await JS.InvokeAsync<bool>("confirm",T("¿Borrar todo el historial, resultados y valores previos?","Delete all history, results and previous values?")))Safe(()=>{Compilation.ClearHistory();completionDrafts.Clear();compiled=null;pendingExecution=null;response="";Notice(T("Historial y valores eliminados.","History and values deleted."));});}
    private async Task ChangeLanguage(ChangeEventArgs e){prefs=prefs with{Language=e.Value?.ToString()??"es"};Settings.SaveSettings(prefs);await JS.InvokeVoidAsync("qp.language",prefs.Language);}
    private void ChangeTheme(ChangeEventArgs e){prefs=prefs with{Theme=e.Value?.ToString()??"light"};Settings.SaveSettings(prefs);}
    private void ToggleTheme(){prefs=prefs with{Theme=prefs.Theme=="light"?"dark":"light"};Settings.SaveSettings(prefs);}
    private void ChangeModel(ChangeEventArgs e){prefs=prefs with{Model=e.Value?.ToString()??LocalModelPolicy.Production.Names[0]};Settings.SaveSettings(prefs);confirmed=false;}
    private void Remember(ChangeEventArgs e){prefs=prefs with{RememberValues=e.Value is true};Settings.SaveSettings(prefs);}
    private async Task CheckOllama(){if(checkingOllama)return;checkingOllama=true;try{availableModels=await Llm.Available(lifetime.Token);availabilityKnown=true;ollamaStatus=availableModels.Length==0?T("Ollama no disponible. La biblioteca sigue funcionando.","Ollama unavailable. Library still works."):string.Join(" · ",availableModels);if(!busy)localOperation=LocalOperationState.Idle;}finally{checkingOllama=false;}if(!lifetime.IsCancellationRequested)await InvokeAsync(StateHasChanged);}
    private async Task Export(){try{await JS.InvokeVoidAsync("qp.download","quickprompt-backup-v2.json",Archives.Export());Notice(T("Respaldo exportado; contiene datos locales privados.","Backup exported; contains private local data."));}catch(Exception e)when((e is ValidationException or JSException) || StorageFailures.IsFailure(e)){Notice(e is ValidationException?e.Message:T("No se pudo exportar el respaldo. Tus datos siguen intactos.","Could not export backup. Your data remains intact."),true);}}
    private void Integrity()=>Safe(()=>Notice("SQLite integrity_check: "+Diagnostics.CheckIntegrity()));
    private async Task ReadImport(InputFileChangeEventArgs e,bool restore)
    {
        pendingImport=null;pendingRestoreSnapshot=null;importPreview=null;
        try
        {
            using var stream=e.File.OpenReadStream(Archives.MaximumBytes);string json;
            if(!restore && (e.File.Name.EndsWith(".db3",StringComparison.OrdinalIgnoreCase) || e.File.Name.EndsWith(".sqlite",StringComparison.OrdinalIgnoreCase)))json=await LegacyConverter.Convert(stream,lifetime.Token);
            else{using var reader=new StreamReader(stream);json=await reader.ReadToEndAsync(lifetime.Token);}
            using var document=JsonDocument.Parse(json);
            pendingRestoreSnapshot=null;
            if(restore){var review=Archives.InspectRestore(document.RootElement);importPreview=review.Preview;pendingRestoreSnapshot=review.ExpectedSnapshot;}
            else importPreview=Archives.Inspect(document.RootElement);
            pendingImport=json;importError="";
            await InvokeAsync(StateHasChanged);await JS.InvokeVoidAsync("qp.open","import-dialog");
        }
        catch(OperationCanceledException) when(lifetime.IsCancellationRequested) { }
        catch(Exception ex) when((ex is IOException or ValidationException or JsonException) || StorageFailures.IsFailure(ex)){Notice(T("No se pudo importar. Archivo inválido, demasiado grande o incompatible.","Import failed. Invalid, oversized or incompatible file."),true);}
    }
    private Task ImportFile(InputFileChangeEventArgs e)=>ReadImport(e,false);
    private Task RestoreFile(InputFileChangeEventArgs e)=>ReadImport(e,true);
    public void Dispose(){CancelAssist();contextVersion++;lifetime.Cancel();operation?.Cancel();lifetime.Dispose();panelReference?.Dispose();}
}
