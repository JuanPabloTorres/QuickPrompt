using System.Windows.Input;

namespace QuickPrompt.Components.Inputs
{
    /// <summary>
    /// SearchInput component optimized for search interactions.
    /// Built with 100% Design System token compliance.
    /// Features pill-shaped border and search icon.
    /// </summary>
    public partial class SearchInput : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(SearchInput), string.Empty, BindingMode.TwoWay);

        public static readonly BindableProperty PlaceholderProperty =
            BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(SearchInput), "Search...");

        public static readonly BindableProperty ShowClearButtonProperty =
            BindableProperty.Create(nameof(ShowClearButton), typeof(bool), typeof(SearchInput), false);

        public static readonly BindableProperty BorderColorProperty =
            BindableProperty.Create(
                nameof(BorderColor), 
                typeof(Color), 
                typeof(SearchInput), 
                Application.Current?.Resources["BorderLight"] as Color ?? Colors.Gray);

        public static readonly BindableProperty SearchCommandProperty =
            BindableProperty.Create(nameof(SearchCommand), typeof(ICommand), typeof(SearchInput), null);

        public static readonly BindableProperty ClearCommandProperty =
            BindableProperty.Create(nameof(ClearCommand), typeof(ICommand), typeof(SearchInput), null);

        #endregion

        #region Properties

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public bool ShowClearButton
        {
            get => (bool)GetValue(ShowClearButtonProperty);
            set => SetValue(ShowClearButtonProperty, value);
        }

        public Color BorderColor
        {
            get => (Color)GetValue(BorderColorProperty);
            set => SetValue(BorderColorProperty, value);
        }

        public ICommand SearchCommand
        {
            get => (ICommand)GetValue(SearchCommandProperty);
            set => SetValue(SearchCommandProperty, value);
        }

        public ICommand ClearCommand
        {
            get => (ICommand)GetValue(ClearCommandProperty);
            set => SetValue(ClearCommandProperty, value);
        }

        #endregion

        #region Constructor

        public SearchInput()
        {
            InitializeComponent();

            // Initialize clear command
            ClearCommand = new Command(OnClearTapped);

            // Set accessibility
            SemanticProperties.SetDescription(this, "Search input field");
            AutomationId = "SearchInput";

            // Update clear button visibility based on text
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Text))
                {
                    ShowClearButton = !string.IsNullOrEmpty(Text);
                }
            };
        }

        #endregion

        #region Event Handlers

        private void OnEntryFocused(object sender, FocusEventArgs e)
        {
            BorderColor = (Color)Application.Current.Resources["PrimaryBlue"];
        }

        private void OnEntryUnfocused(object sender, FocusEventArgs e)
        {
            BorderColor = (Color)Application.Current.Resources["BorderLight"];
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            ShowClearButton = !string.IsNullOrEmpty(e.NewTextValue);
            
            // Optionally trigger search on text change (debounced in real app)
            // SearchCommand?.Execute(e.NewTextValue);
        }

        private void OnClearTapped()
        {
            Text = string.Empty;
            ShowClearButton = false;
            InputEntry?.Focus(); // Keep focus for immediate re-entry
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Focus the search field programmatically
        /// </summary>
        public new void Focus()
        {
            InputEntry?.Focus();
        }

        /// <summary>
        /// Unfocus the search field programmatically
        /// </summary>
        public new void Unfocus()
        {
            InputEntry?.Unfocus();
        }

        #endregion
    }
}
