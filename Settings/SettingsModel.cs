using System;

namespace QuickPrompt.Settings
{
    public class SettingsModel
    {
        public bool AutoSendEnabled { get; set; } = true;
        public bool OpenExternalEnabled { get; set; } = false;
        public bool CloudSyncEnabled { get; set; } = false;
        public DateTime? LastSyncAt { get; set; }
    }
}