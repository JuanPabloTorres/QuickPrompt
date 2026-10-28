using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record ToggleVariableInput(string TemplateText, int Start, int End, string? OriginalText = null);
public record ToggleVariableResult(string TemplateText, string[] VariableNames, int Start, int End, string Action, string Name, string? OriginalText);
public record RenameVariableInput(string TemplateText, int Start, int End, string Name, Dictionary<string, string>? Defaults = null);
public record RenameVariableResult(string TemplateText, string[] VariableNames, Dictionary<string, string> Defaults, int Start, int End, string PreviousName, string Name);
public record EditorCompilation(string? Text, string[] MissingVariables);

public static class EditorTools
{
    public static EditorCompilation Compile(string templateText, Dictionary<string, string> values)
    {
        ValidateValues(templateText, values); var names = TemplateCompiler.Names(templateText);
        var missing = names.Where(name => !values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value)).ToArray();
        return new(missing.Length == 0 ? TemplateCompiler.Render(templateText, values) : null, missing);
    }

    internal static void ValidateValues(string template, Dictionary<string, string>? values)
    { if (template is null || template.Length > 32000 || values is null || values.Count > 60 || values.Any(v => v.Key.Length > 80 || v.Value is null || v.Value.Length > 8000 || !TemplateCompiler.Names(template).Contains(v.Key))) throw new ValidationException("Contenido o valores inválidos."); }

    public static RenameVariableResult Rename(RenameVariableInput input)
    {
        if (input.TemplateText is null || input.TemplateText.Length > 32000 || input.Start < 0 || input.End < input.Start || input.End > input.TemplateText.Length)
            throw new ValidationException("Selecciona una variable dentro del contenido.");
        var names = TemplateCompiler.Names(input.TemplateText);
        var defaults = (input.Defaults ?? []).Where(item => names.Contains(item.Key)).ToDictionary();
        new Template { Title = "Editor", Template = input.TemplateText, Variables = new(defaults) }.Validate();
        var spans = TemplateEditor.Spans(input.TemplateText);
        var selected = spans.FirstOrDefault(span => input.Start >= span.Start && input.End <= span.Start + span.Length
            && (input.Start != input.End || input.Start > span.Start && input.Start < span.Start + span.Length))
            ?? throw new ValidationException("Selecciona una variable o coloca el cursor dentro de ella.");
        var editor = new TemplateEditor(input.TemplateText, defaults);
        editor.Rename(selected.Start, selected.Start + selected.Length, input.Name);
        var preceding = spans.Count(span => span.Name == selected.Name && span.Start < selected.Start);
        var start = selected.Start + preceding * (input.Name.Length - selected.Name.Length);
        return new(editor.Text, TemplateCompiler.Names(editor.Text), editor.Defaults, start, start + input.Name.Length + 2, selected.Name, input.Name);
    }

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
