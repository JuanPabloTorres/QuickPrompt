using System.Windows.Input;

namespace QuickPrompt.Components.Inputs
{
    /// <summary>
    /// TextInput component for standard text entry with label, validation, and error states.
    /// Built with 100% Design System token compliance.
    /// ? PHASE 4: Fixed NullReferenceException with safe color resource access
    /// </summary>
    public partial class TextInput : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty LabelProperty =
            BindableProperty.Create(nameof(Label), typeof(string), typeof(TextInput), string.Empty);

        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(TextInput), string.Empty, BindingMode.TwoWay);

        public static readonly BindableProperty PlaceholderProperty =
            BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(TextInput), string.Empty);

        public static readonly BindableProperty ErrorTextProperty =
            BindableProperty.Create(nameof(ErrorText), typeof(string), typeof(TextInput), string.Empty);

        public static readonly BindableProperty HelperTextProperty =
            BindableProperty.Create(nameof(HelperText), typeof(string), typeof(TextInput), string.Empty);

        public static readonly BindableProperty HasErrorProperty =
            BindableProperty.Create(
                nameof(HasError), 
                typeof(bool), 
                typeof(TextInput), 
                false,
                propertyChanged: OnHasErrorChanged);

        public static readonly BindableProperty ShowLabelProperty =
            BindableProperty.Create(nameof(ShowLabel), typeof(bool), typeof(TextInput), true);

        public static readonly BindableProperty ShowClearButtonProperty =
            BindableProperty.Create(nameof(ShowClearButton), typeof(bool), typeof(TextInput), false);

        public static readonly BindableProperty ShowHelperTextProperty =
            BindableProperty.Create(nameof(ShowHelperText), typeof(bool), typeof(TextInput), false);

        public static readonly BindableProperty KeyboardProperty =
            BindableProperty.Create(nameof(Keyboard), typeof(Keyboard), typeof(TextInput), Keyboard.Default);

        public static readonly BindableProperty IsPasswordProperty =
            BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(TextInput), false);

        public static readonly BindableProperty MaxLengthProperty =
            BindableProperty.Create(nameof(MaxLength), typeof(int), typeof(TextInput), int.MaxValue);

        public static readonly BindableProperty BorderColorProperty =
            BindableProperty.Create(
                nameof(BorderColor), 
                typeof(Color), 
                typeof(TextInput), 
                GetSafeColor("BorderLight", Color.FromRgb(229, 231, 235))); // ? Safe fallback

        public static readonly BindableProperty ClearCommandProperty =
            BindableProperty.Create(nameof(ClearCommand), typeof(ICommand), typeof(TextInput), null);

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

        public Keyboard Keyboard
        {
            get => (Keyboard)GetValue(KeyboardProperty);
            set => SetValue(KeyboardProperty, value);
        }

        public bool IsPassword
        {
            get => (bool)GetValue(IsPasswordProperty);
            set => SetValue(IsPasswordProperty, value);
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

        #endregion

        #region Constructor

        public TextInput()
        {
            InitializeComponent();

            // Initialize clear command
            ClearCommand = new Command(OnClearTapped);

            // Set accessibility
            SemanticProperties.SetDescription(this, "Text input field");
            AutomationId = "TextInput";

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
                // ? Safe color access with fallback
                BorderColor = GetSafeColor("PrimaryBlue", Color.FromRgb(72, 101, 129));
            }
        }

        private void OnEntryUnfocused(object sender, FocusEventArgs e)
        {
            if (!HasError)
            {
                // ? Safe color access with fallback
                BorderColor = GetSafeColor("BorderLight", Color.FromRgb(229, 231, 235));
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

        #endregion

        #region Property Changed Handlers

        private static void OnHasErrorChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is TextInput input)
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
                // ? Safe color access with fallback
                BorderColor = GetSafeColor("Danger", Color.FromRgb(239, 68, 68));
            }
            else if (InputEntry?.IsFocused == true)
            {
                // ? Safe color access with fallback
                BorderColor = GetSafeColor("PrimaryBlue", Color.FromRgb(72, 101, 129));
            }
            else
            {
                // ? Safe color access with fallback
                BorderColor = GetSafeColor("BorderLight", Color.FromRgb(229, 231, 235));
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// ? PHASE 4: Safe color resource access with fallback
        /// Prevents NullReferenceException when Application.Current is null
        /// </summary>
        private static Color GetSafeColor(string resourceKey, Color fallback)
        {
            try
            {
                // Check if Application.Current is available
                if (Application.Current?.Resources == null)
                {
                    return fallback;
                }

                // Try to get the color from resources
                if (Application.Current.Resources.TryGetValue(resourceKey, out var resource) && resource is Color color)
                {
                    return color;
                }

                return fallback;
            }
            catch
            {
                // Return fallback if any exception occurs
                return fallback;
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
