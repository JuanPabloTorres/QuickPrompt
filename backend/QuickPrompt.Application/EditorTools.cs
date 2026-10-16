using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record ToggleVariableInput(string TemplateText, int Start, int End, string? OriginalText = null);
public record ToggleVariableResult(string TemplateText, string[] VariableNames, int Start, int End, string Action, string Name, string? OriginalText);

public static class EditorTools
{
    public static ToggleVariableResult Toggle(ToggleVariableInput input)
    {
        if (input.TemplateText is null || input.TemplateText.Length > 32000 || input.Start < 0 || input.End < input.Start || input.End > input.TemplateText.Length || input.OriginalText?.Length > 32000)
            throw new ValidationException("Selecciona texto dentro del contenido.");
        var spans = TemplateEditor.Spans(input.TemplateText);
        var selected = spans.FirstOrDefault(span => input.Start >= span.Start && input.End <= span.Start + span.Length
            && (input.Start != input.End || input.Start > span.Start && input.Start < span.Start + span.Length));
        if (selected is not null)
        {
            var editor = new TemplateEditor(input.TemplateText, new Dictionary<string, string>());
            editor.Remove(selected.Start, selected.Start + selected.Length);
            var literal = input.OriginalText ?? selected.Name;
            var text = input.TemplateText[..selected.Start] + literal + input.TemplateText[(selected.Start + selected.Length)..];
            var names = TemplateCompiler.Names(text);
            return new(text, names, selected.Start, selected.Start + literal.Length, "removed", selected.Name, null);
        }
        if (input.Start == input.End) throw new ValidationException("Selecciona la palabra o frase que quieres convertir en variable.");
        var original = input.TemplateText[input.Start..input.End];
        var name = original.Trim();
        try { if (TemplateCompiler.Names($"<{name}>").SingleOrDefault() != name) name = TemplateEditor.SuggestedName(original); }
        catch (ValidationException) { name = TemplateEditor.SuggestedName(original); }
        var occupied = spans.Select(span => span.Name).ToHashSet(StringComparer.Ordinal);
        var baseName = name; var suffix = 2;
        while (occupied.Contains(name)) name = (baseName.Length > 70 ? baseName[..70] : baseName) + "_" + suffix++;
        var create = new TemplateEditor(input.TemplateText, new Dictionary<string, string>());
        create.Create(input.Start, input.End, name);
        return new(create.Text, TemplateCompiler.Names(create.Text), input.Start, input.Start + name.Length + 2, "created", name, original);
    }
}
