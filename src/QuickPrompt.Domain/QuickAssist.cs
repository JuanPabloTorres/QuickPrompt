namespace QuickPrompt.Local;

public enum AssistAction { Search, Favorites, Recent, OpenGroup, OpenTemplate, Favorite, PrepareValues, Create, Improve, SuggestVariables, ProposeGroup }
public sealed record AssistStep(AssistAction Action,string Argument="");
public sealed record BoundAssistStep(AssistStep Step,Guid? TemplateId=null,int? Revision=null,Guid? GroupId=null);
public sealed record PreparedAssistPlan(AssistPlan Plan,IReadOnlyList<BoundAssistStep> Steps);
public sealed record AssistPlan(AssistStep[] Steps,string Source)
{
    public void Validate()
    {
        if(Source is not("deterministic" or "ollama") || Steps is null || Steps.Length is <1 or >4 || Steps.Any(s=>s is null || !Enum.IsDefined(s.Action) || s.Argument is null || s.Argument.Length>1000))throw new ValidationException("Plan inválido; máximo 4 pasos / Invalid plan; maximum 4 steps.");
        foreach(var s in Steps)
        {
            if(s.Action is AssistAction.Search or AssistAction.OpenGroup or AssistAction.OpenTemplate or AssistAction.Create or AssistAction.ProposeGroup && string.IsNullOrWhiteSpace(s.Argument))throw new ValidationException("Faltan argumentos / Missing arguments.");
            if(s.Action is AssistAction.Search or AssistAction.OpenGroup or AssistAction.OpenTemplate && s.Argument.Length>200)throw new ValidationException("Argumento demasiado largo / Argument too long.");
        }
        if(Steps.Count(s=>s.Action is AssistAction.Create or AssistAction.Improve or AssistAction.SuggestVariables)>1)throw new ValidationException("Una propuesta IA por plan / One AI proposal per plan.");
        if(Steps.Take(Steps.Length-1).Any(s=>s.Action is AssistAction.Create or AssistAction.Improve or AssistAction.SuggestVariables or AssistAction.PrepareValues))throw new ValidationException("La propuesta debe ser el último paso / Proposal must be the last step.");
    }
}
