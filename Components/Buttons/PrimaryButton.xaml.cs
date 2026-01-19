using System.Windows.Input;

namespace QuickPrompt.Components.Buttons
{
    /// <summary>
    /// Primary button component for main call-to-action interactions.
    /// Built with 100% Design System token compliance.
    /// 
    /// Features:
    /// - Uses Design System colors (PrimaryBlue, White)
    /// - Respects Design System spacing (ThicknessMd)
    /// - Follows Design System sizing (ButtonHeightMedium, RadiusMd)
    /// - Supports icons and text
    /// - Animated press feedback
    /// - Accessible (SemanticProperties, AutomationId)
    /// </summary>
    public partial class PrimaryButton : ContentView
    {
        #region Bindable Properties

        /// <summary>
        /// Button text label
        /// </summary>
        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(
                nameof(Text), 
                typeof(string), 
                typeof(PrimaryButton), 
                string.Empty);

        /// <summary>
        /// Command to execute when button is tapped
        /// </summary>
        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(
                nameof(Command), 
                typeof(ICommand), 
                typeof(PrimaryButton), 
                null);

        /// <summary>
        /// Parameter to pass to the Command
        /// </summary>
        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(
                nameof(CommandParameter), 
                typeof(object), 
                typeof(PrimaryButton), 
                null);

        /// <summary>
        /// Button background color (defaults to PrimaryBlue from Design System)
        /// </summary>
        public static readonly BindableProperty BackgroundColorProperty =
            BindableProperty.Create(
                nameof(BackgroundColor), 
                typeof(Color), 
                typeof(PrimaryButton), 
                Application.Current?.Resources["PrimaryBlue"] as Color ?? Colors.Blue);

        /// <summary>
        /// Button text color (defaults to White from Design System)
        /// </summary>
        public static readonly BindableProperty TextColorProperty =
            BindableProperty.Create(
                nameof(TextColor), 
                typeof(Color), 
                typeof(PrimaryButton), 
                Application.Current?.Resources["White"] as Color ?? Colors.White);

        /// <summary>
        /// Optional icon/image source
        /// </summary>
        public static readonly BindableProperty ImageSourceProperty =
            BindableProperty.Create(
                nameof(ImageSource), 
                typeof(ImageSource), 
                typeof(PrimaryButton), 
                null,
                propertyChanged: OnImageSourceChanged);

        /// <summary>
        /// Controls icon visibility (auto-calculated from ImageSource)
        /// </summary>
        public static readonly BindableProperty ShowIconProperty =
            BindableProperty.Create(
                nameof(ShowIcon), 
                typeof(bool), 
                typeof(PrimaryButton), 
                false);

        /// <summary>
        /// Button opacity (defaults to 1.0, reduced when disabled)
        /// </summary>
        public static readonly BindableProperty OpacityProperty =
            BindableProperty.Create(
                nameof(Opacity), 
                typeof(double), 
                typeof(PrimaryButton), 
                1.0,
                propertyChanged: OnOpacityChanged);

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

        public new Color BackgroundColor
        {
            get => (Color)GetValue(BackgroundColorProperty);
            set => SetValue(BackgroundColorProperty, value);
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

        public PrimaryButton()
        {
            InitializeComponent();

            // Set semantic properties for accessibility
            SemanticProperties.SetDescription(this, "Primary action button");
            AutomationId = "PrimaryButton";

            // Handle IsEnabled changes for visual feedback
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsEnabled))
                {
                    UpdateVisualState();
                }
            };
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle button tap with animation feedback
        /// </summary>
        private async void OnButtonTapped(object sender, TappedEventArgs e)
        {
            if (!IsEnabled || sender is not Border border)
                return;

            // ? Use Design System animation duration token
            var animationDuration = (int)Application.Current.Resources["AnimationDurationFast"];

            // Press animation: scale down
            await border.ScaleTo(0.95, (uint)(animationDuration / 2), Easing.CubicOut);

            // Release animation: scale back up
            await border.ScaleTo(1.0, (uint)(animationDuration / 2), Easing.CubicIn);
        }

        #endregion

        #region Property Changed Handlers

        private static void OnImageSourceChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is PrimaryButton button)
            {
                button.ShowIcon = newValue != null;
            }
        }

        private static void OnOpacityChanged(BindableObject bindable, object oldValue, object newValue)
        {
            // Opacity property change handled by XAML binding
        }

        #endregion

        #region Visual State Management

        /// <summary>
        /// Update visual appearance based on IsEnabled state
        /// Uses Design System tokens for disabled state
        /// </summary>
        private void UpdateVisualState()
        {
            if (!IsEnabled)
            {
                // ? Use Design System tokens for disabled state
                BackgroundColor = (Color)Application.Current.Resources["StateDisabledBackground"];
                TextColor = (Color)Application.Current.Resources["StateDisabledText"];
                Opacity = (double)Application.Current.Resources["OpacityDisabled"];
            }
            else
            {
                // ? Use Design System tokens for enabled state
                BackgroundColor = (Color)Application.Current.Resources["PrimaryBlue"];
                TextColor = (Color)Application.Current.Resources["White"];
                Opacity = (double)Application.Current.Resources["OpacityFull"];
            }
        }

        #endregion
    }
}
