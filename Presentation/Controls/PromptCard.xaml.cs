using System.Windows.Input;

namespace QuickPrompt.Controls
{
    /// <summary>
    /// Reusable card component for displaying prompt information.
    /// Features: Tap gesture, favorite toggle, metadata display (category, variables, usage).
    /// </summary>
    public partial class PromptCard : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(PromptCard), string.Empty);

        public static readonly BindableProperty DescriptionProperty =
            BindableProperty.Create(nameof(Description), typeof(string), typeof(PromptCard), string.Empty);

        public static readonly BindableProperty CategoryProperty =
            BindableProperty.Create(nameof(Category), typeof(string), typeof(PromptCard), string.Empty);

        public static readonly BindableProperty CategoryColorProperty =
            BindableProperty.Create(nameof(CategoryColor), typeof(Color), typeof(PromptCard), Colors.Gray);

        public static readonly BindableProperty IsFavoriteProperty =
            BindableProperty.Create(nameof(IsFavorite), typeof(bool), typeof(PromptCard), false);

        public static readonly BindableProperty VariableCountProperty =
            BindableProperty.Create(nameof(VariableCount), typeof(int), typeof(PromptCard), 0);

        public static readonly BindableProperty UsageCountProperty =
            BindableProperty.Create(nameof(UsageCount), typeof(int), typeof(PromptCard), 0);

        public static readonly BindableProperty DateTextProperty =
            BindableProperty.Create(nameof(DateText), typeof(string), typeof(PromptCard), string.Empty);

        public static readonly BindableProperty TapCommandProperty =
            BindableProperty.Create(nameof(TapCommand), typeof(ICommand), typeof(PromptCard), null);

        public static readonly BindableProperty FavoriteCommandProperty =
            BindableProperty.Create(nameof(FavoriteCommand), typeof(ICommand), typeof(PromptCard), null);

        // Visibility Properties
        public static readonly BindableProperty ShowDescriptionProperty =
            BindableProperty.Create(nameof(ShowDescription), typeof(bool), typeof(PromptCard), true);

        public static readonly BindableProperty ShowCategoryProperty =
            BindableProperty.Create(nameof(ShowCategory), typeof(bool), typeof(PromptCard), true);

        public static readonly BindableProperty ShowVariableCountProperty =
            BindableProperty.Create(nameof(ShowVariableCount), typeof(bool), typeof(PromptCard), true);

        public static readonly BindableProperty ShowUsageCountProperty =
            BindableProperty.Create(nameof(ShowUsageCount), typeof(bool), typeof(PromptCard), true);

        public static readonly BindableProperty ShowDateProperty =
            BindableProperty.Create(nameof(ShowDate), typeof(bool), typeof(PromptCard), true);

        #endregion

        #region Properties

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

        public string Category
        {
            get => (string)GetValue(CategoryProperty);
            set => SetValue(CategoryProperty, value);
        }

        public Color CategoryColor
        {
            get => (Color)GetValue(CategoryColorProperty);
            set => SetValue(CategoryColorProperty, value);
        }

        public bool IsFavorite
        {
            get => (bool)GetValue(IsFavoriteProperty);
            set => SetValue(IsFavoriteProperty, value);
        }

        public int VariableCount
        {
            get => (int)GetValue(VariableCountProperty);
            set => SetValue(VariableCountProperty, value);
        }

        public int UsageCount
        {
            get => (int)GetValue(UsageCountProperty);
            set => SetValue(UsageCountProperty, value);
        }

        public string DateText
        {
            get => (string)GetValue(DateTextProperty);
            set => SetValue(DateTextProperty, value);
        }

        public ICommand TapCommand
        {
            get => (ICommand)GetValue(TapCommandProperty);
            set => SetValue(TapCommandProperty, value);
        }

        public ICommand FavoriteCommand
        {
            get => (ICommand)GetValue(FavoriteCommandProperty);
            set => SetValue(FavoriteCommandProperty, value);
        }

        // Visibility
        public bool ShowDescription
        {
            get => (bool)GetValue(ShowDescriptionProperty);
            set => SetValue(ShowDescriptionProperty, value);
        }

        public bool ShowCategory
        {
            get => (bool)GetValue(ShowCategoryProperty);
            set => SetValue(ShowCategoryProperty, value);
        }

        public bool ShowVariableCount
        {
            get => (bool)GetValue(ShowVariableCountProperty);
            set => SetValue(ShowVariableCountProperty, value);
        }

        public bool ShowUsageCount
        {
            get => (bool)GetValue(ShowUsageCountProperty);
            set => SetValue(ShowUsageCountProperty, value);
        }

        public bool ShowDate
        {
            get => (bool)GetValue(ShowDateProperty);
            set => SetValue(ShowDateProperty, value);
        }

        #endregion

        public PromptCard()
        {
            InitializeComponent();
        }
    }
}
