using System.Windows.Input;

namespace QuickPrompt.Controls
{
    /// <summary>
    /// Reusable empty state component for when there's no data to display.
    /// Improves UX by providing clear feedback and actionable next steps.
    /// </summary>
    public partial class EmptyStateView : ContentView
    {
        public static readonly BindableProperty IconProperty =
            BindableProperty.Create(nameof(Icon), typeof(string), typeof(EmptyStateView), "&#xe872;"); // Default: inbox icon

        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(EmptyStateView), "No items found");

        public static readonly BindableProperty DescriptionProperty =
            BindableProperty.Create(nameof(Description), typeof(string), typeof(EmptyStateView), string.Empty);

        public static readonly BindableProperty ButtonTextProperty =
            BindableProperty.Create(nameof(ButtonText), typeof(string), typeof(EmptyStateView), "Get Started");

        public static readonly BindableProperty ButtonCommandProperty =
            BindableProperty.Create(nameof(ButtonCommand), typeof(ICommand), typeof(EmptyStateView), null);

        public static readonly BindableProperty ShowButtonProperty =
            BindableProperty.Create(nameof(ShowButton), typeof(bool), typeof(EmptyStateView), false);

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public string ButtonText
        {
            get => (string)GetValue(ButtonTextProperty);
            set => SetValue(ButtonTextProperty, value);
        }

        public ICommand ButtonCommand
        {
            get => (ICommand)GetValue(ButtonCommandProperty);
            set => SetValue(ButtonCommandProperty, value);
        }

        public bool ShowButton
        {
            get => (bool)GetValue(ShowButtonProperty);
            set => SetValue(ShowButtonProperty, value);
        }

        public EmptyStateView()
        {
            InitializeComponent();
        }
    }

    /// <summary>
    /// Predefined empty state configurations for common scenarios.
    /// </summary>
    public static class EmptyStates
    {
        public static class Icons
        {
            public const string Inbox = "&#xe872;";
            public const string Search = "&#xe8b6;";
            public const string Error = "&#xe001;";
            public const string Favorite = "&#xe87e;";
            public const string CloudOff = "&#xe2c0;";
            public const string Add = "&#xe145;";
        }

        public static class Messages
        {
            public const string NoPrompts = "No prompts yet";
            public const string NoResults = "No results found";
            public const string NoFavorites = "No favorites yet";
            public const string NoHistory = "No execution history";
        }

        public static class Descriptions
        {
            public const string CreateFirst = "Create your first prompt to get started";
            public const string TryDifferentSearch = "Try adjusting your search or filters";
            public const string AddFavorites = "Mark prompts as favorites to see them here";
            public const string ExecutePrompt = "Execute a prompt to see it in history";
        }
    }
}
