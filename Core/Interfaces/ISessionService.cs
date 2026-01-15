using System.Threading.Tasks;

namespace QuickPrompt.Services.ServiceInterfaces
{
    public interface ISessionService
    {
        bool IsLoggedIn { get; }
        string? CurrentUserId { get; }
        Task SetSessionAsync(string userId);
        Task LogoutAsync();
        Task<string?> GetUserIdAsync();
        Task<LogoutOption> ShowLogoutConfirmationAsync();
        Task DeleteLocalDataAsync();
    }

    public enum LogoutOption
    {
        KeepLocal,
        DeleteLocal
    }
}
