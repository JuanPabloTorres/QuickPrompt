namespace QuickPrompt.Controls
{
    /// <summary>
    /// Reusable status badge for displaying state indicators (Success, Error, Warning, Info).
    /// Supports icon + text or text-only modes.
    /// </summary>
    public partial class StatusBadge : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(StatusBadge), string.Empty);

        public static readonly BindableProperty IconProperty =
            BindableProperty.Create(nameof(Icon), typeof(string), typeof(StatusBadge), string.Empty);

        public static readonly BindableProperty BadgeColorProperty =
            BindableProperty.Create(nameof(BadgeColor), typeof(Color), typeof(StatusBadge), Colors.Gray);

        public static readonly BindableProperty TextColorProperty =
            BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(StatusBadge), Colors.White);

        public static readonly BindableProperty ShowIconProperty =
            BindableProperty.Create(nameof(ShowIcon), typeof(bool), typeof(StatusBadge), true);

        #endregion

        #region Properties

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public Color BadgeColor
        {
            get => (Color)GetValue(BadgeColorProperty);
            set => SetValue(BadgeColorProperty, value);
        }

        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public bool ShowIcon
        {
            get => (bool)GetValue(ShowIconProperty);
            set => SetValue(ShowIconProperty, value);
        }

        #endregion

        public StatusBadge()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Factory methods for common badge types.
        /// </summary>
        public static StatusBadge Success(string text) => new()
        {
            Text = text,
            Icon = "&#xe86c;", // Check circle
            BadgeColor = Color.FromRgb(34, 197, 94), // Green
            TextColor = Colors.White
        };

        public static StatusBadge Error(string text) => new()
        {
            Text = text,
            Icon = "&#xe001;", // Error
            BadgeColor = Color.FromRgb(239, 68, 68), // Red
            TextColor = Colors.White
        };

        public static StatusBadge Warning(string text) => new()
        {
            Text = text,
            Icon = "&#xe002;", // Warning
            BadgeColor = Color.FromRgb(251, 191, 36), // Amber
            TextColor = Colors.Black
        };

        public static StatusBadge Info(string text) => new()
        {
            Text = text,
            Icon = "&#xe88e;", // Info
            BadgeColor = Color.FromRgb(59, 130, 246), // Blue
            TextColor = Colors.White
        };

        public static StatusBadge Neutral(string text) => new()
        {
            Text = text,
            Icon = string.Empty,
            BadgeColor = Color.FromRgb(156, 163, 175), // Gray
            TextColor = Colors.White,
            ShowIcon = false
        };
    }

    /// <summary>
    /// Predefined badge styles and icons.
    /// </summary>
    public static class BadgeStyles
    {
        public static class Icons
        {
            public const string Success = "&#xe86c;"; // Check circle
            public const string Error = "&#xe001;"; // Error
            public const string Warning = "&#xe002;"; // Warning
            public const string Info = "&#xe88e;"; // Info
            public const string Star = "&#xe838;"; // Star
            public const string New = "&#xe8d8;"; // Fiber new
            public const string Hot = "&#xe9ca;"; // Whatshot
        }

        public static class Colors
        {
            public static readonly Color Success = Color.FromRgb(34, 197, 94);
            public static readonly Color Error = Color.FromRgb(239, 68, 68);
            public static readonly Color Warning = Color.FromRgb(251, 191, 36);
            public static readonly Color Info = Color.FromRgb(59, 130, 246);
            public static readonly Color Neutral = Color.FromRgb(156, 163, 175);
        }
    }
}
