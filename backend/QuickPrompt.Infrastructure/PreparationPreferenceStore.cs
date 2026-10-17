using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed class PreparationPreferenceStore(LocalStore store) : IPreparationPreferences
{
    public PreparationPreferences Get() => store.ReadPreparationPreferences();
    public void Save(PreparationPreferences preferences) { PreparationPreferenceRules.Validate(preferences); store.WritePreparationPreferences(preferences); }
    public void Forget() => store.ForgetPreparationPreferences();
}
public sealed partial class LocalStore
{
    private static readonly Guid PreparationPreferencesId = Guid.Parse("efb699b6-6198-4b03-a25e-61e2c6382020");
    public PreparationPreferences ReadPreparationPreferences() => Read(db => GetOne<PreparationPreferences>(db, "nova-preferences", PreparationPreferencesId) ?? new());
    public void WritePreparationPreferences(PreparationPreferences preferences) => Write((db, tx) => { Put(db, tx, "nova-preferences", PreparationPreferencesId.ToString(), preferences); return true; });
    public void ForgetPreparationPreferences() => Write((db, tx) => { using var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = "DELETE FROM Documents WHERE Kind='nova-preferences'"; return cmd.ExecuteNonQuery(); });
}
