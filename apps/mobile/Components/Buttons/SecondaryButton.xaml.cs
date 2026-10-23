using System.Windows.Input;

namespace QuickPrompt.Components.Buttons
{
    /// <summary>
    /// Secondary button component for secondary actions (cancel, back, etc.).
    /// Built with 100% Design System token compliance.
    /// Uses outlined style with transparent background.
    /// </summary>
    public partial class SecondaryButton : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(SecondaryButton), string.Empty);

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SecondaryButton), null);

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SecondaryButton), null);

        // ? FIX: Don't access Application.Current.Resources in static field initializers
        // Use a safe default color instead, then set the actual color in the constructor
        public static readonly BindableProperty BorderColorProperty =
            BindableProperty.Create(
                nameof(BorderColor), 
                typeof(Color), 
                typeof(SecondaryButton), 
                Colors.Blue); // Safe default

        public static readonly BindableProperty TextColorProperty =
            BindableProperty.Create(
                nameof(TextColor), 
                typeof(Color), 
                typeof(SecondaryButton), 
                Colors.Blue); // Safe default

        public static readonly BindableProperty ImageSourceProperty =
            BindableProperty.Create(
                nameof(ImageSource), 
                typeof(ImageSource), 
                typeof(SecondaryButton), 
                null,
                propertyChanged: OnImageSourceChanged);

        public static readonly BindableProperty ShowIconProperty =
            BindableProperty.Create(nameof(ShowIcon), typeof(bool), typeof(SecondaryButton), false);

        public static readonly BindableProperty OpacityProperty =
            BindableProperty.Create(nameof(Opacity), typeof(double), typeof(SecondaryButton), 1.0);

        #endregion

        #region Properties

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
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

        public Color BorderColor
        {
            get => (Color)GetValue(BorderColorProperty);
            set => SetValue(BorderColorProperty, value);
        }

        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public ImageSource ImageSource
        {
            get => (ImageSource)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public bool ShowIcon
        {
            get => (bool)GetValue(ShowIconProperty);
            set => SetValue(ShowIconProperty, value);
        }

        public new double Opacity
        {
            get => (double)GetValue(OpacityProperty);
            set => SetValue(OpacityProperty, value);
        }

        #endregion

        #region Constructor

        public SecondaryButton()
        {
            InitializeComponent();

            // ? FIX: Set colors from resources AFTER InitializeComponent
            // This ensures Application.Current.Resources is available
            InitializeColors();

            SemanticProperties.SetDescription(this, "Secondary action button");
            AutomationId = "SecondaryButton";

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsEnabled))
                {
                    UpdateVisualState();
                }
            };
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Safely initialize colors from Application resources after the control is created
        /// </summary>
        private void InitializeColors()
        {
            try
            {
                if (Application.Current?.Resources != null)
                {
                    // Set default colors from Design System
                    if (Application.Current.Resources.TryGetValue("PrimaryBlue", out var primaryBlue) && primaryBlue is Color blueColor)
                    {
                        BorderColor = blueColor;
                        TextColor = blueColor;
                    }

                    // Set opacity from Design System
                    if (Application.Current.Resources.TryGetValue("OpacityFull", out var opacityFull) && opacityFull is double opacity)
                    {
                        Opacity = opacity;
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback to safe defaults if resource loading fails
                System.Diagnostics.Debug.WriteLine($"[SecondaryButton.InitializeColors] Failed to load resources: {ex.Message}");
                BorderColor = Colors.Blue;
                TextColor = Colors.Blue;
                Opacity = 1.0;
            }
        }

        #endregion

        #region Event Handlers

        private async void OnButtonTapped(object sender, TappedEventArgs e)
        {
            if (!IsEnabled || sender is not Border border)
                return;

            try
            {
                var animationDuration = GetResourceValue("AnimationDurationFast", 200);

                await border.ScaleTo(0.95, (uint)(animationDuration / 2), Easing.CubicOut);
                await border.ScaleTo(1.0, (uint)(animationDuration / 2), Easing.CubicIn);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SecondaryButton.OnButtonTapped] Animation failed: {ex.Message}");
            }
        }

        #endregion

        #region Property Changed Handlers

        private static void OnImageSourceChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is SecondaryButton button)
            {
                button.ShowIcon = newValue != null;
            }
        }

        #endregion

        #region Visual State Management

        private void UpdateVisualState()
        {
            try
            {
                if (!IsEnabled)
                {
                    BorderColor = GetColorResource("StateDisabledBackground", Colors.LightGray);
                    TextColor = GetColorResource("StateDisabledText", Colors.Gray);
                    Opacity = GetResourceValue("OpacityDisabled", 0.5);
                }
                else
                {
                    BorderColor = GetColorResource("PrimaryBlue", Colors.Blue);
                    TextColor = GetColorResource("PrimaryBlue", Colors.Blue);
                    Opacity = GetResourceValue("OpacityFull", 1.0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SecondaryButton.UpdateVisualState] Failed to update visual state: {ex.Message}");
            }
        }

        /// <summary>
        /// Safely retrieve a color resource with fallback
        /// </summary>
        private Color GetColorResource(string key, Color fallback)
        {
            try
            {
                if (Application.Current?.Resources != null &&
                    Application.Current.Resources.TryGetValue(key, out var resource) &&
                    resource is Color color)
                {
                    return color;
                }
            }
            catch { }

            return fallback;
        }

        /// <summary>
        /// Safely retrieve a numeric resource value with fallback
        /// </summary>
        private T GetResourceValue<T>(string key, T fallback)
        {
            try
            {
                if (Application.Current?.Resources != null &&
                    Application.Current.Resources.TryGetValue(key, out var resource) &&
                    resource is T value)
                {
                    return value;
                }
            }
            catch { }

            return fallback;
        }

        #endregion
    }
}
