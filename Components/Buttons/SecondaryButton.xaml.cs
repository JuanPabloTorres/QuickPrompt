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

        public static readonly BindableProperty BorderColorProperty =
            BindableProperty.Create(
                nameof(BorderColor), 
                typeof(Color), 
                typeof(SecondaryButton), 
                Application.Current?.Resources["PrimaryBlue"] as Color ?? Colors.Blue);

        public static readonly BindableProperty TextColorProperty =
            BindableProperty.Create(
                nameof(TextColor), 
                typeof(Color), 
                typeof(SecondaryButton), 
                Application.Current?.Resources["PrimaryBlue"] as Color ?? Colors.Blue);

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

        #region Event Handlers

        private async void OnButtonTapped(object sender, TappedEventArgs e)
        {
            if (!IsEnabled || sender is not Border border)
                return;

            var animationDuration = (int)Application.Current.Resources["AnimationDurationFast"];

            await border.ScaleTo(0.95, (uint)(animationDuration / 2), Easing.CubicOut);
            await border.ScaleTo(1.0, (uint)(animationDuration / 2), Easing.CubicIn);
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
            if (!IsEnabled)
            {
                BorderColor = (Color)Application.Current.Resources["StateDisabledBackground"];
                TextColor = (Color)Application.Current.Resources["StateDisabledText"];
                Opacity = (double)Application.Current.Resources["OpacityDisabled"];
            }
            else
            {
                BorderColor = (Color)Application.Current.Resources["PrimaryBlue"];
                TextColor = (Color)Application.Current.Resources["PrimaryBlue"];
                Opacity = (double)Application.Current.Resources["OpacityFull"];
            }
        }

        #endregion
    }
}
