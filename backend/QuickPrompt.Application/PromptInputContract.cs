using System.Collections.Immutable;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum PromptInputPolicy { DEFINE, ADD, PRESERVE, CLARIFY }
public record RequestedPromptInput(string Name, string Evidence);

// A user's field control owns input names. Model-generated prose, metadata and
// examples cannot authorize fields or fill their values. Null is unconfirmed;
// an explicitly empty selection means no new fields, never "let the model infer".
public sealed class PromptInputContract
{
    public PromptInputPolicy Policy { get; }
    public ImmutableArray<RequestedPromptInput> Inputs { get; }
    public ImmutableArray<string> Names { get; }
    public string Evidence { get; }
    public string Question { get; }
    public string RequestHash { get; }
    public string SourceHash { get; }
    public bool Enforced => Policy != PromptInputPolicy.CLARIFY;

    private PromptInputContract(string request,string source,bool updating,string[]? selection)
    {
        Policy=selection is null?PromptInputPolicy.CLARIFY:!updating?PromptInputPolicy.DEFINE
            :selection.Length==0?PromptInputPolicy.PRESERVE:PromptInputPolicy.ADD;
        Inputs=(selection??[]).Select(name=>new RequestedPromptInput(name,"USER_FIELD_CONTROL")).ToImmutableArray();
        Names=((updating?TemplateCompiler.Names(source):[]).Concat(selection??[])).Distinct(StringComparer.Ordinal).ToImmutableArray();
        if(Names.Length>60)throw new ValidationException("Máximo 60 variables.");
        Evidence=selection is null?"UNCONFIRMED_INPUT_DEFINITION":"USER_FIELD_CONTROL";
        Question=selection is not null?"":updating
            ?"Indica los nombres de las variables que quieres agregar, uno por línea. Las actuales se conservan. Deja la lista vacía si sólo quieres cambiar el texto. Son nombres de campos, no sus valores."
            :"Indica los nombres de las variables de la plantilla, uno por línea. No necesitas rellenar sus valores ahora. Deja la lista vacía para crear un prompt sin variables.";
        RequestHash=LiveRuns.Hash(request);SourceHash=LiveRuns.Hash(source);
    }

    public static PromptInputContract FromUserSelection(string request,string source,bool updating,IEnumerable<string>? names)
    {
        var selected=names?.ToArray();
        ValidateSelection(selected);
        return new(request,source,updating,selected);
    }

    public static void ValidateSelection(string[]? selected)
    {
        if(selected is null)return;
        if(selected.Length>60||selected.Distinct(StringComparer.Ordinal).Count()!=selected.Length
            ||selected.Any(name=>string.IsNullOrWhiteSpace(name)||TemplateCompiler.Names($"<{name}>").SingleOrDefault()!=name))
            throw new ValidationException("Indica nombres de variables válidos, sin repetirlos ni incluir sus valores (máximo 60).");
    }

    public void ValidateBasis(string request,string source)
    {
        if(RequestHash!=LiveRuns.Hash(request)||SourceHash!=LiveRuns.Hash(source))
            throw new ValidationException("El contrato de campos pertenece a otra petición o versión del origen.");
    }

    public bool Matches(string body)=>!Enforced||Names.ToHashSet(StringComparer.Ordinal).SetEquals(TemplateCompiler.Names(body));

    public string Bind(string source,string body)
    {
        if(!Enforced)return body;
        var extra=TemplateCompiler.Names(body).Except(Names,StringComparer.Ordinal).ToArray();
        if(extra.Length>0)throw new ValidationException("Campos no autorizados en el cuerpo: "+string.Join(", ",extra)+". Usa únicamente los campos elegidos por el usuario.");
        var bound=TemplateSourceBinding.Bind(source,body,Names);
        if(!Matches(bound.Text))throw new ValidationException("La plantilla no contiene los campos elegidos.");
        return bound.Text;
    }
    /// <summary>Bind app-authorized fields for a human-reviewed proposal without re-injecting semantic source clauses.</summary>
    public string BindFieldsOnly(string body)
    {
        if (!Enforced) return body;
        var extra = TemplateCompiler.Names(body).Except(Names, StringComparer.Ordinal).ToArray();
        if (extra.Length > 0) throw new ValidationException("Campos no autorizados en el cuerpo: " + string.Join(", ", extra));
        var bound = TemplateSourceBinding.Bind("", body, Names).Text;
        if (!Matches(bound)) throw new ValidationException("La propuesta no contiene todos los campos elegidos.");
        return bound;
    }
}
