using Microsoft.Maui.Storage;
using QuickPrompt.Services.ServiceInterfaces;
using System;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    /// <summary>
    /// Session service that manages user authentication state using secure storage.
    /// </summary>
    public class SessionService : ISessionService
    {
        private const string USER_ID_KEY = "CurrentUserId";
        private string? _cachedUserId;
        private bool _isInitialized;

        public bool IsLoggedIn => !string.IsNullOrEmpty(CurrentUserId);

        public string? CurrentUserId
        {
            get
            {
                if (!_isInitialized)
                {
                    // Synchronous access for property - cache the value
                    _cachedUserId = GetUserIdAsync().GetAwaiter().GetResult();
                    _isInitialized = true;
                }
                return _cachedUserId;
            }
        }

        public async Task SetSessionAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            try
            {
                // Try SecureStorage first (encrypted storage)
                await SecureStorage.SetAsync(USER_ID_KEY, userId);
                _cachedUserId = userId;
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                // Fallback to Preferences if SecureStorage fails
                System.Diagnostics.Debug.WriteLine($"[SessionService] SecureStorage failed, using Preferences: {ex.Message}");
                Preferences.Set(USER_ID_KEY, userId);
                _cachedUserId = userId;
                _isInitialized = true;
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                // Remove from SecureStorage
                SecureStorage.Remove(USER_ID_KEY);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionService] SecureStorage remove failed: {ex.Message}");
            }

            // Also remove from Preferences (in case fallback was used)
            Preferences.Remove(USER_ID_KEY);

            _cachedUserId = null;
            _isInitialized = true;

            await Task.CompletedTask;
        }

        public async Task<string?> GetUserIdAsync()
        {
            try
            {
                // Try SecureStorage first
                var userId = await SecureStorage.GetAsync(USER_ID_KEY);
                if (!string.IsNullOrEmpty(userId))
                    return userId;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionService] SecureStorage read failed: {ex.Message}");
            }

            // Fallback to Preferences
            var prefUserId = Preferences.Get(USER_ID_KEY, string.Empty);
            return string.IsNullOrEmpty(prefUserId) ? null : prefUserId;
        }

        public async Task<LogoutOption> ShowLogoutConfirmationAsync()
        {
            // This will be implemented with IDialogService in next step
            // For now, default to keeping local data
            await Task.CompletedTask;
            return LogoutOption.KeepLocal;
        }

        public async Task DeleteLocalDataAsync()
        {
            // This will be implemented to delete SQLite DBs in next step
            // For now, just clear preferences
            Preferences.Clear();
            await Task.CompletedTask;
        }
    }
}
