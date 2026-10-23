using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;
public sealed partial class LocalStore
{
    public PreparedAssistPlan PrepareAssist(AssistPlan plan,Guid? selectedId=null)
    {
        plan.Validate();return Read(db=>{
            Template? target=selectedId is {} id?GetOne<Template>(db,"template",id):null;
            var result=new List<BoundAssistStep>();
            foreach(var step in plan.Steps)
            {
                Guid? groupId=null;
                if(step.Action is AssistAction.Search or AssistAction.OpenTemplate)
                {
                    var matches=QueryCatalog<Template>(db,"template",new(Search:step.Argument,Size:100));
                    if(step.Action==AssistAction.OpenTemplate){var exact=matches.Items.Where(t=>PromptSearch.Normalize(t.Title)==PromptSearch.Normalize(step.Argument)).ToArray();if(exact.Length!=1)throw new ValidationException("El título no identifica un prompt único / Title does not identify one prompt.");target=exact[0];}
                    else target=matches.Total==1?matches.Items[0]:null;
                }
                if(step.Action==AssistAction.OpenGroup){groupId=ExportGroups(db).SingleOrDefault(g=>PromptSearch.Normalize(g.Name)==PromptSearch.Normalize(step.Argument))?.Id??throw new ValidationException("Grupo no encontrado / Group not found.");}
                if(step.Action is AssistAction.Favorite or AssistAction.PrepareValues or AssistAction.Improve or AssistAction.SuggestVariables && target is not{IsActive:true})throw new ValidationException("Selecciona un prompt único antes de continuar / Select one prompt before continuing.");
                if(step.Action==AssistAction.ProposeGroup){new PromptGroup{Name=step.Argument}.Validate();if(ExportGroups(db).Any(g=>PromptSearch.Normalize(g.Name)==PromptSearch.Normalize(step.Argument)))throw new ValidationException("El grupo ya existe / Group already exists.");}
                result.Add(new(step,target?.Id,target?.Revision,groupId));
            }
            return new PreparedAssistPlan(plan,result.AsReadOnly());
        });
    }
    // Only explicitly reviewed persistent operations. Everything is validated before the transaction commits.
    public void ApplyAssist(PreparedAssistPlan prepared)=>Write((db,tx)=>
    {
        prepared.Plan.Validate();if(prepared.Steps.Count!=prepared.Plan.Steps.Length)throw new ValidationException("Plan incompatible.");
        for(var i=0;i<prepared.Steps.Count;i++)
        {
            var bound=prepared.Steps[i];if(bound.Step!=prepared.Plan.Steps[i])throw new ValidationException("Plan incompatible.");
            if(bound.TemplateId is {} id)
            {
                var t=GetOne<Template>(db,"template",id,tx);
                if(t is not{IsActive:true} || t.Revision!=bound.Revision)throw new ConflictException("El prompt cambió. Prepara el plan de nuevo / Prompt changed. Prepare the plan again.");
            }
            else if(bound.Step.Action is AssistAction.Favorite or AssistAction.PrepareValues or AssistAction.Improve or AssistAction.SuggestVariables)throw new ValidationException("Falta el prompt / Missing prompt.");
            if(bound.Step.Action==AssistAction.OpenGroup && (bound.GroupId is not {} g || GetGroup(db,tx,g) is null))throw new ConflictException("El grupo cambió / Group changed.");
        }
        foreach(var bound in prepared.Steps)
        {
            if(bound.Step.Action==AssistAction.Favorite){var t=GetOne<Template>(db,"template",bound.TemplateId!.Value,tx)!;if(!t.IsFavorite){t.IsFavorite=true;SaveInside(db,tx,t,"Quick Assist approved");}}
            if(bound.Step.Action==AssistAction.ProposeGroup)SaveGroupInside(db,tx,new(){Name=bound.Step.Argument});
        }
        return true;
    });
}
