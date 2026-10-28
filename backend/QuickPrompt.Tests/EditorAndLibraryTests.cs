using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;
public sealed class EditorAndLibraryTests
{
    [Fact] public void SameToggleButtonCreatesAndRemovesASelectedVariableExactly()
    {
        const string source = "Explica inteligencia artificial con ejemplos.";
        var start = source.IndexOf("inteligencia", StringComparison.Ordinal); var end = start + "inteligencia artificial".Length;
        var created = EditorTools.Toggle(new(source, start, end));
        Assert.Equal("Explica <inteligencia artificial> con ejemplos.", created.TemplateText);
        var restored = EditorTools.Toggle(new(created.TemplateText, created.Start, created.End, created.OriginalText));
        Assert.Equal(source, restored.TemplateText); Assert.Empty(restored.VariableNames);
    }
    [Fact] public void ToggleRestoresPunctuationAndUnicodeAndSupportsCaretInsideExistingVariable()
    {
        const string text = "Usa α, β y 李."; var created = EditorTools.Toggle(new(text, 4, text.Length));
        Assert.Equal(text, EditorTools.Toggle(new(created.TemplateText, created.Start + 2, created.Start + 2, created.OriginalText)).TemplateText);
        Assert.Equal("Usa tema.", EditorTools.Toggle(new("Usa <tema>.", 6, 9)).TemplateText);
    }
    [Fact] public void ToggleRejectsMixedSelectionsEscapesAndInvalidRangesWithoutChangingSource()
    {
        Assert.Throws<ValidationException>(() => EditorTools.Toggle(new("Usa <tema> ahora", 0, 10)));
        Assert.Throws<ValidationException>(() => EditorTools.Toggle(new("Tema", 1, 1)));
        Assert.Throws<ValidationException>(() => EditorTools.Toggle(new("Tema", 0, 100)));
        var text = @"Literal \<tema\>";
        Assert.Throws<ValidationException>(() => EditorTools.Toggle(new(text, 9, 14)));
    }
    [Fact] public void DuplicateVariableNamesAreDisambiguatedWithoutChangingExistingOccurrences()
    {
        var created = EditorTools.Toggle(new("<tema> y tema", 9, 13));
        Assert.Equal("<tema> y <tema_2>", created.TemplateText);
        Assert.Equal(new[] { "tema", "tema_2" }, created.VariableNames);
    }
    [Fact] public void PracticalLibraryIsValidCoversAllCategoriesAndImportsIdempotentlyWithoutChangingExistingData()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "biblioteca-practica.es.json"); var json = File.ReadAllText(fixture);
        var directory = Path.Combine(Path.GetTempPath(), "practical-library-" + Guid.NewGuid());
        try
        {
            var store = new LocalStore(Path.Combine(directory, "library.sqlite")); var original = store.Save(new() { Title = "Mi prompt", Template = "Mi contenido" });
            var preferences = store.Settings() with { GeneralInstructions = "Mi instrucción personal" }; store.SaveSettings(preferences);
            store.PreviewImport(json); Assert.Equal(60, store.Import(json)); Assert.Equal(0, store.Import(json));
            var pack = System.Text.Json.JsonSerializer.Deserialize<Archive>(json, LocalStore.Json)!;
            Assert.Equal(20, pack.Groups!.Length); Assert.Equal(29, pack.Templates.Select(t => t.Category).Distinct().Count());
            foreach (var template in pack.Templates)
            {
                var names = TemplateCompiler.Names(template.Template); Assert.InRange(names.Length, 1, 3);
                var compiled = TemplateCompiler.Render(template.Template, names.ToDictionary(name => name, _ => "Datos reales del usuario"));
                Assert.DoesNotContain("<", compiled); Assert.NotEmpty(compiled);
                Assert.Single(store.GroupsFor("template", template.Id));
            }
            Assert.Equal(61, store.ActiveTemplateCount()); Assert.Equal(preferences, store.Settings()); Assert.Equal(original.Template, store.FindTemplate(original.Id)!.Template);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
