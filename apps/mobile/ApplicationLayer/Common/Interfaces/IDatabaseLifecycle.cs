using System.Threading.Tasks;

namespace QuickPrompt.ApplicationLayer.Common.Interfaces;

// Separate startup/restore capabilities; the host binds one existing coordinator.
public interface IDatabaseInitialization
{
    Task InitializeAsync();
}
public interface IDatabaseRestore
{
    Task RestoreAsync();
}
