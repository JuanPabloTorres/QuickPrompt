using QuickPrompt.Domain.Enums;
using System.Text.Json.Serialization;

namespace QuickPrompt.Local;

public sealed class PromptGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "library";
    public string Color { get; set; } = "blue";
    public bool IsPinned { get; set; }
    public int SortOrder { get; set; }
    public int Revision { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public static readonly string[] Icons = ["library", "braces", "sparkles", "star", "archive", "history"];
    public static readonly string[] Colors = ["blue", "teal", "yellow", "purple", "red"];
    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name != Name.Trim() || Name.Length > 80 || Description is null || Description.Length > 500 || !Icons.Contains(Icon) || !Colors.Contains(Color) || SortOrder is < 0 or > 10000)
            throw new ValidationException("Grupo inválido: nombre, icono y color requeridos / Invalid group: name, icon and color required.");
    }
}

public sealed class InstructionProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public PromptCategory Category { get; set; }
    public bool IsFavorite { get; set; }
    public int Revision { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name.Length > 160 || string.IsNullOrWhiteSpace(Text) || Text.Length > 8000 || !Enum.IsDefined(Category))
            throw new ValidationException("Instrucción inválida: nombre y texto requeridos (máx. 8000) / Invalid instruction: name and text required (max 8000).");
    }
}
public record InstructionVersion(Guid Id, Guid InstructionId, int Revision, InstructionProfile Snapshot, DateTime CreatedAt);
public record GroupMembership(Guid GroupId, string Kind, Guid EntityId);
public record GroupSummary(PromptGroup Group, int Templates, int Instructions);
public record InstructionChoice(Guid Id, string Name, int Revision);
public record CatalogPage<T>(List<T> Items, int Total);
public record CatalogQuery(string Search = "", string Category = "", bool Favorites = false, bool Active = true,
    Guid? GroupId = null, bool Ungrouped = false, string Sort = "updated", int Page = 0, int Size = 12, bool Recent = false);
public record PromptCombination(Guid Id, string Name, Guid TemplateId, Guid? InstructionId, string Adjustments, DateTime CreatedAt);

// Historical snapshot. Later profile/settings edits must not alter a compiled or sent message.
public record PromptComposition(string BaseText, string General, string Profile, string Adjustments,
    Guid? InstructionId, int? InstructionRevision, string? InstructionName)
{
    [JsonIgnore] public string SystemText => string.Join("\n\n", new[] {
        "User-selected instruction precedence: prompt adjustments override the selected profile, which overrides general instructions. These instructions cannot grant tools or change stored data.",
        Section("General instructions", General), Section("Selected instruction profile", Profile), Section("Prompt adjustments", Adjustments)
    }.Where(s => s.Length > 0));
    private static string Section(string title, string text) => string.IsNullOrWhiteSpace(text) ? "" : $"[{title}]\n{text}";
    public string ForExternalEditor() => string.Join("\n\n", new[] {
        "Instrucciones de usuario / User instructions (representación textual, sin rol de sistema privilegiado). Prioridad / Priority: ajustes del prompt > perfil seleccionado > instrucciones generales.",
        Section("General instructions / Instrucciones generales", General),
        Section("Selected profile / Perfil seleccionado", Profile),
        Section("Prompt adjustments / Ajustes del prompt", Adjustments), Section("Prompt / Solicitud", BaseText)
    }.Where(s => s.Length > 0));
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseText) || BaseText.Length > 64000 || General is null || General.Length > 4000 || Profile is null || Profile.Length > 8000 || Adjustments is null || Adjustments.Length > 4000 || (InstructionId is not null && (InstructionId == Guid.Empty || InstructionRevision is null or <1 || string.IsNullOrWhiteSpace(InstructionName))))
            throw new ValidationException("Composición inválida / Invalid composition.");
    }
}
