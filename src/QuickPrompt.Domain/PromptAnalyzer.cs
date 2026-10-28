namespace QuickPrompt.Local;
public record StaticAnalysis(string[] Variables,string[] MissingValues,bool HasObjectiveSignal,bool HasConstraintSignal,bool HasFormatSignal);
public static class PromptAnalyzer
{
    // These are deterministic lexical signals, not a claim of semantic understanding.
    public static StaticAnalysis Analyze(Template template,IReadOnlyDictionary<string,string> values)
    {
        var names=TemplateCompiler.Names(template.Template);
        bool Has(params string[] words)=>words.Any(w=>template.Template.Contains(w,StringComparison.OrdinalIgnoreCase));
        return new(names,names.Where(n=>string.IsNullOrWhiteSpace(values.GetValueOrDefault(n)) && string.IsNullOrWhiteSpace(template.Variables.GetValueOrDefault(n))).ToArray(),Has("explica","redacta","crea","analiza","revisa","resume","explain","write","create","analyze","review","summarize"),Has("máximo","mínimo","no ","sin ","maxim","limit","must","avoid","only","solo"),Has("json","tabla","lista","viñeta","código","table","list","bullet","code","formato","format"));
    }
}
