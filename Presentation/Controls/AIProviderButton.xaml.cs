using System.Windows.Input;

namespace QuickPrompt.Controls
{
    /// <summary>
    /// Reusable AI provider button with branding colors, icons, and status indicators.
    /// Used for selecting which AI provider to send prompts to (ChatGPT, Gemini, Grok, Copilot).
    /// </summary>
    public partial class AIProviderButton : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty ProviderNameProperty =
            BindableProperty.Create(nameof(ProviderName), typeof(string), typeof(AIProviderButton), string.Empty);

        public static readonly BindableProperty ProviderDescriptionProperty =
            BindableProperty.Create(nameof(ProviderDescription), typeof(string), typeof(AIProviderButton), string.Empty);

        public static readonly BindableProperty ProviderColorProperty =
            BindableProperty.Create(nameof(ProviderColor), typeof(Color), typeof(AIProviderButton), Colors.Gray);

        public static readonly BindableProperty ProviderIconProperty =
            BindableProperty.Create(nameof(ProviderIcon), typeof(ImageSource), typeof(AIProviderButton), null);

        public static readonly BindableProperty ProviderMaterialIconProperty =
            BindableProperty.Create(nameof(ProviderMaterialIcon), typeof(string), typeof(AIProviderButton), "&#xf06c;"); // Robot icon

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(AIProviderButton), null);

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(AIProviderButton), null);

        public static readonly BindableProperty BadgeTextProperty =
            BindableProperty.Create(nameof(BadgeText), typeof(string), typeof(AIProviderButton), string.Empty);

        // Visibility Properties
        public static readonly BindableProperty ShowDescriptionProperty =
            BindableProperty.Create(nameof(ShowDescription), typeof(bool), typeof(AIProviderButton), false);

        public static readonly BindableProperty ShowIconProperty =
            BindableProperty.Create(nameof(ShowIcon), typeof(bool), typeof(AIProviderButton), false);

        public static readonly BindableProperty ShowMaterialIconProperty =
            BindableProperty.Create(nameof(ShowMaterialIcon), typeof(bool), typeof(AIProviderButton), true);

        public static readonly BindableProperty ShowBadgeProperty =
            BindableProperty.Create(nameof(ShowBadge), typeof(bool), typeof(AIProviderButton), false);

        public static readonly BindableProperty ShowArrowProperty =
            BindableProperty.Create(nameof(ShowArrow), typeof(bool), typeof(AIProviderButton), true);

        #endregion

        #region Properties

        public string ProviderName
        {
            get => (string)GetValue(ProviderNameProperty);
            set => SetValue(ProviderNameProperty, value);
        }

        public string ProviderDescription
        {
            get => (string)GetValue(ProviderDescriptionProperty);
            set => SetValue(ProviderDescriptionProperty, value);
        }

        public Color ProviderColor
        {
            get => (Color)GetValue(ProviderColorProperty);
            set => SetValue(ProviderColorProperty, value);
        }

        public ImageSource ProviderIcon
        {
            get => (ImageSource)GetValue(ProviderIconProperty);
            set => SetValue(ProviderIconProperty, value);
        }

        public string ProviderMaterialIcon
        {
            get => (string)GetValue(ProviderMaterialIconProperty);
            set => SetValue(ProviderMaterialIconProperty, value);
        }

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public string BadgeText
        {
            get => (string)GetValue(BadgeTextProperty);
            set => SetValue(BadgeTextProperty, value);
        }

        public bool ShowDescription
        {
            get => (bool)GetValue(ShowDescriptionProperty);
            set => SetValue(ShowDescriptionProperty, value);
        }

        public bool ShowIcon
        {
            get => (bool)GetValue(ShowIconProperty);
            set => SetValue(ShowIconProperty, value);
        }

        public bool ShowMaterialIcon
        {
            get => (bool)GetValue(ShowMaterialIconProperty);
            set => SetValue(ShowMaterialIconProperty, value);
        }

        public bool ShowBadge
        {
            get => (bool)GetValue(ShowBadgeProperty);
            set => SetValue(ShowBadgeProperty, value);
        }

        public bool ShowArrow
        {
            get => (bool)GetValue(ShowArrowProperty);
            set => SetValue(ShowArrowProperty, value);
        }

        #endregion

        public AIProviderButton()
        {
            InitializeComponent();
        }
    }

    /// <summary>
    /// Predefined AI provider configurations.
    /// </summary>
    public static class AIProviders
    {
        public static class Colors
        {
            public static readonly Color ChatGPT = Color.FromRgb(16, 163, 127); // OpenAI green
            public static readonly Color Gemini = Color.FromRgb(138, 180, 248); // Google blue
            public static readonly Color Grok = Color.FromRgb(0, 0, 0); // X black
            public static readonly Color Copilot = Color.FromRgb(0, 120, 212); // Microsoft blue
        }

        public static class Icons
        {
            public const string ChatGPT = "&#xe0c3;"; // Chat icon
            public const string Gemini = "&#xe8b5;"; // Stars icon
            public const string Grok = "&#xf06c;"; // Robot icon
            public const string Copilot = "&#xe8fd;"; // Code icon
        }

        public static class Descriptions
        {
            public const string ChatGPT = "OpenAI's conversational AI";
            public const string Gemini = "Google's multimodal AI";
            public const string Grok = "X's real-time AI";
            public const string Copilot = "Microsoft's AI assistant";
        }
    }
}
