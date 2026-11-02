namespace QuickPrompt.Web.Infrastructure;

/// <summary>Operator-owned linguistic resource selection. No detection, downloads, model selection or fallback after failure.</summary>
public sealed record AuthoringTranslationResourceOptions(string Mode = "none", string? Image = null)
{
    public IAuthoringTranslationSourceViews? Create() => Mode switch {
        "none" when Image is null => null,
        "apertium-native" when Image is null => ApertiumTranslationSourceViews.Native(),
        "apertium-docker" when Image is not null => ApertiumTranslationSourceViews.Docker(Image),
        _ => throw new InvalidOperationException("Select none, apertium-native, or apertium-docker with an explicit local image digest; unused image settings are rejected.")
    };
}
