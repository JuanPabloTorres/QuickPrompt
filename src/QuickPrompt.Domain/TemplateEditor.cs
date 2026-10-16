using System.Text.RegularExpressions;

namespace QuickPrompt.Local;

public sealed record PlaceholderSpan(int Start, int Length, string Name);
public sealed record EditorSelection(int Start, int End, string Text);

// Circuit-local editor history. Execution values never enter this model.
public sealed class TemplateEditor
{
    private sealed record Snapshot(string Text, Dictionary<string,string> Defaults, Dictionary<string,string> Originals);
    private readonly List<Snapshot> undo = [], redo = [];
    private Snapshot current;
    public string Text => current.Text;
    public Dictionary<string,string> Defaults => new(current.Defaults);
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public TemplateEditor(string text, IReadOnlyDictionary<string,string> defaults)
        => current = new(text, new(defaults), new());
    public void Input(string text, IReadOnlyDictionary<string,string> defaults)
    {
        if(text.Length > 32000) throw new ValidationException("Máximo 32000 caracteres / Maximum 32000 characters.");
        if(text == Text && defaults.OrderBy(x=>x.Key).SequenceEqual(current.Defaults.OrderBy(x=>x.Key))) return;
        Commit(new(text,new(defaults),new(current.Originals)));
    }
    private void Commit(Snapshot snapshot)
    {
        undo.Add(current); if(undo.Count > 100) undo.RemoveAt(0);
        redo.Clear(); current = snapshot;
    }
    public void Undo(){if(!CanUndo)return;redo.Add(current);current=undo[^1];undo.RemoveAt(undo.Count-1);}
    public void Redo(){if(!CanRedo)return;undo.Add(current);current=redo[^1];redo.RemoveAt(redo.Count-1);}
    public static IReadOnlyList<PlaceholderSpan> Spans(string text)
    {
        TemplateCompiler.Parse(text); var result = new List<PlaceholderSpan>();
        for(var i=0;i<text.Length;i++)
        {
            if(text[i]=='\\' && i+1<text.Length && text[i+1] is '<' or '>' or '\\'){i++;continue;}
            if(text[i]!='<')continue;
            var end=text.IndexOf('>',i+1);result.Add(new(i,end-i+1,text[(i+1)..end]));i=end;
        }
        return result;
    }
    public PlaceholderSpan? SelectedVariable(int start,int end)
        => Spans(Text).FirstOrDefault(s=> (start==s.Start && end==s.Start+s.Length) || (start==s.Start+1 && end==s.Start+s.Length-1));
    public static string SuggestedName(string text)
    {
        var name=Regex.Replace(text.Trim(),@"[^\p{L}\p{M}\p{N}_]+","_").Trim('_');
        if(name.Length==0)return "variable";
        if(name.Length>60)name=name[..60];
        if(!char.IsLetterOrDigit(name[0]) && name[0]!='_')name="variable_"+name;
        return name;
    }
    private void ValidateSelection(int start,int end)
    {
        if(start<0 || end<=start || end>Text.Length)throw new ValidationException("Selecciona texto / Select text.");
        foreach(var boundary in new[]{start,end})
        {
            if(boundary>0 && boundary<Text.Length && char.IsHighSurrogate(Text[boundary-1]) && char.IsLowSurrogate(Text[boundary]))throw new ValidationException("Selección incompleta / Incomplete selection.");
            var escapes=0;for(var i=boundary-1;i>=0&&Text[i]=='\\';i--)escapes++;
            if(escapes%2==1 && boundary<Text.Length && Text[boundary] is '<' or '>' or '\\')throw new ValidationException("Selecciona también el escape / Include the escape.");
        }
    }
    public void Create(int start,int end,string name)
    {
        ValidateSelection(start,end);var spans=Spans(Text);
        if(spans.Any(s=>start<s.Start+s.Length && end>s.Start))throw new ValidationException("La selección contiene una variable / Selection contains a variable.");
        ValidateName(name);
        if(spans.Any(s=>s.Name==name))throw new ValidationException("Nombre ya utilizado / Existing name.");
        var text=Text[..start]+"<"+name+">"+Text[end..];TemplateCompiler.Parse(text);
        var originals=new Dictionary<string,string>(current.Originals){[name]=Text[start..end]};
        Commit(new(text,new(current.Defaults),originals));
    }
    public void Remove(int start,int end)
    {
        var span=SelectedVariable(start,end)??throw new ValidationException("Selecciona una variable completa / Select a complete variable.");
        var text=Text[..span.Start]+current.Originals.GetValueOrDefault(span.Name,span.Name)+Text[(span.Start+span.Length)..];
        TemplateCompiler.Parse(text);var defaults=new Dictionary<string,string>(current.Defaults);var originals=new Dictionary<string,string>(current.Originals);
        if(!TemplateCompiler.Names(text).Contains(span.Name)){defaults.Remove(span.Name);originals.Remove(span.Name);}
        Commit(new(text,defaults,originals));
    }
    public void Rename(int start,int end,string name)
    {
        var span=SelectedVariable(start,end)??throw new ValidationException("Selecciona una variable completa / Select a complete variable.");
        if(name==span.Name)return;
        ValidateName(name);
        if(TemplateCompiler.Names(Text).Contains(name))throw new ValidationException("Nombre ya utilizado / Existing name.");
        var text=Text;foreach(var s in Spans(Text).Where(s=>s.Name==span.Name).Reverse())text=text[..s.Start]+"<"+name+">"+text[(s.Start+s.Length)..];
        TemplateCompiler.Parse(text);var defaults=new Dictionary<string,string>(current.Defaults);var originals=new Dictionary<string,string>(current.Originals);
        if(defaults.Remove(span.Name,out var value))defaults[name]=value;
        if(originals.Remove(span.Name,out var original))originals[name]=original;
        Commit(new(text,defaults,originals));
    }
    private static void ValidateName(string name)
    {
        var tokens=TemplateCompiler.Parse("<"+name+">");
        if(tokens.Count!=1 || tokens[0].Name!=name)throw new ValidationException("Nombre inválido / Invalid name.");
    }
}
