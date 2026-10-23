using System.Windows.Input;

namespace QuickPrompt.Components.Inputs
{
    /// <summary>
    /// PasswordInput component for password entry with visibility toggle.
    /// Built with 100% Design System token compliance.
    /// </summary>
    public partial class PasswordInput : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty LabelProperty =
            BindableProperty.Create(nameof(Label), typeof(string), typeof(PasswordInput), string.Empty);

        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(PasswordInput), string.Empty, BindingMode.TwoWay);

        public static readonly BindableProperty PlaceholderProperty =
            BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(PasswordInput), string.Empty);

        public static readonly BindableProperty ErrorTextProperty =
            BindableProperty.Create(nameof(ErrorText), typeof(string), typeof(PasswordInput), string.Empty);

        public static readonly BindableProperty HelperTextProperty =
            BindableProperty.Create(nameof(HelperText), typeof(string), typeof(PasswordInput), string.Empty);

        public static readonly BindableProperty HasErrorProperty =
            BindableProperty.Create(
                nameof(HasError), 
                typeof(bool), 
                typeof(PasswordInput), 
                false,
                propertyChanged: OnHasErrorChanged);

        public static readonly BindableProperty ShowLabelProperty =
            BindableProperty.Create(nameof(ShowLabel), typeof(bool), typeof(PasswordInput), true);

        public static readonly BindableProperty ShowClearButtonProperty =
            BindableProperty.Create(nameof(ShowClearButton), typeof(bool), typeof(PasswordInput), false);

        public static readonly BindableProperty ShowHelperTextProperty =
            BindableProperty.Create(nameof(ShowHelperText), typeof(bool), typeof(PasswordInput), false);

        public static readonly BindableProperty IsPasswordVisibleProperty =
            BindableProperty.Create(nameof(IsPasswordVisible), typeof(bool), typeof(PasswordInput), false);

        public static readonly BindableProperty MaxLengthProperty =
            BindableProperty.Create(nameof(MaxLength), typeof(int), typeof(PasswordInput), int.MaxValue);

        public static readonly BindableProperty BorderColorProperty =
            BindableProperty.Create(
                nameof(BorderColor), 
                typeof(Color), 
                typeof(PasswordInput), 
                Application.Current?.Resources["BorderLight"] as Color ?? Colors.Gray);

        public static readonly BindableProperty ClearCommandProperty =
            BindableProperty.Create(nameof(ClearCommand), typeof(ICommand), typeof(PasswordInput), null);

        public static readonly BindableProperty ToggleVisibilityCommandProperty =
            BindableProperty.Create(nameof(ToggleVisibilityCommand), typeof(ICommand), typeof(PasswordInput), null);

        #endregion

        #region Properties

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

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

        public string ErrorText
        {
            get => (string)GetValue(ErrorTextProperty);
            set => SetValue(ErrorTextProperty, value);
        }

        public string HelperText
        {
            get => (string)GetValue(HelperTextProperty);
            set => SetValue(HelperTextProperty, value);
        }

        public bool HasError
        {
            get => (bool)GetValue(HasErrorProperty);
            set => SetValue(HasErrorProperty, value);
        }

        public bool ShowLabel
        {
            get => (bool)GetValue(ShowLabelProperty);
            set => SetValue(ShowLabelProperty, value);
        }

        public bool ShowClearButton
        {
            get => (bool)GetValue(ShowClearButtonProperty);
            set => SetValue(ShowClearButtonProperty, value);
        }

        public bool ShowHelperText
        {
            get => (bool)GetValue(ShowHelperTextProperty);
            set => SetValue(ShowHelperTextProperty, value);
        }

        public bool IsPasswordVisible
        {
            get => (bool)GetValue(IsPasswordVisibleProperty);
            set => SetValue(IsPasswordVisibleProperty, value);
        }

        public int MaxLength
        {
            get => (int)GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

        public Color BorderColor
        {
            get => (Color)GetValue(BorderColorProperty);
            set => SetValue(BorderColorProperty, value);
        }

        public ICommand ClearCommand
        {
            get => (ICommand)GetValue(ClearCommandProperty);
            set => SetValue(ClearCommandProperty, value);
        }

        public ICommand ToggleVisibilityCommand
        {
            get => (ICommand)GetValue(ToggleVisibilityCommandProperty);
            set => SetValue(ToggleVisibilityCommandProperty, value);
        }

        #endregion

        #region Constructor

        public PasswordInput()
        {
            InitializeComponent();

            // Initialize commands
            ClearCommand = new Command(OnClearTapped);
            ToggleVisibilityCommand = new Command(OnToggleVisibility);

            // Set accessibility
            SemanticProperties.SetDescription(this, "Password input field");
            AutomationId = "PasswordInput";

            // Update helper text visibility
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(HelperText))
                {
                    ShowHelperText = !string.IsNullOrEmpty(HelperText);
                }
                else if (e.PropertyName == nameof(Text))
                {
                    ShowClearButton = !string.IsNullOrEmpty(Text);
                }
            };
        }

        #endregion

        #region Event Handlers

        private void OnEntryFocused(object sender, FocusEventArgs e)
        {
            if (!HasError)
            {
                BorderColor = (Color)Application.Current.Resources["PrimaryBlue"];
            }
        }

        private void OnEntryUnfocused(object sender, FocusEventArgs e)
        {
            if (!HasError)
            {
                BorderColor = (Color)Application.Current.Resources["BorderLight"];
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            ShowClearButton = !string.IsNullOrEmpty(e.NewTextValue);
        }

        private void OnClearTapped()
        {
            Text = string.Empty;
            ShowClearButton = false;
        }

        private void OnToggleVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
        }

        #endregion

        #region Property Changed Handlers

        private static void OnHasErrorChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is PasswordInput input)
            {
                input.UpdateBorderColor();
            }
        }

        #endregion

        #region Visual State Management

        private void UpdateBorderColor()
        {
            if (HasError)
            {
                BorderColor = (Color)Application.Current.Resources["Danger"];
            }
            else if (InputEntry?.IsFocused == true)
            {
                BorderColor = (Color)Application.Current.Resources["PrimaryBlue"];
            }
            else
            {
                BorderColor = (Color)Application.Current.Resources["BorderLight"];
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Focus the input field programmatically
        /// </summary>
        public new void Focus()
        {
            InputEntry?.Focus();
        }

        /// <summary>
        /// Unfocus the input field programmatically
        /// </summary>
        public new void Unfocus()
        {
            InputEntry?.Unfocus();
        }

        #endregion
    }
}
