using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record PreparationPreferences(string? Language = null, string? Format = null, string? ProviderId = null);
public record PreferenceApproval(PreparationPreferences Preferences, bool Confirmed);
public interface IPreparationPreferences
{
    PreparationPreferences Get();
    void Save(PreparationPreferences preferences);
    void Forget();
}
public static class PreparationPreferenceRules
{
    public static void Validate(PreparationPreferences preferences)
    {
        if (preferences.Language is not (null or "es" or "en") || preferences.Format is not (null or "JSON" or "Markdown" or "Tabla" or "Texto")
            || preferences.ProviderId is { } provider && !ProviderRegistry.Providers.Any(p => p.ProviderId == provider)) throw new ValidationException("Preferencias no permitidas.");
    }
}
